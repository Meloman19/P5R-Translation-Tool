using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using P5R_Packager.Common;
using PersonaEditorLib;
using PersonaEditorLib.Text;

namespace P5R_Packager.DataTool.TextImport
{
    internal sealed class BMDImporter
    {
        private const string TextDir = "TEXT";
        private const string TextZip = "P5R_text.zip";

        private readonly PersonaEncoding _oldEncoding;
        private readonly PersonaEncoding _newEncoding;
        private readonly PersonaFont _newFont;

        private Dictionary<string, string> _bmdDuplicates;
        private Dictionary<string, Dictionary<(int, int), string>> _bmdTextTranslate;
        private Dictionary<string, string> _bmdNameTranslate;


        public BMDImporter(string translatePath, PersonaEncoding oldEncoding, PersonaEncoding newEncoding, PersonaFont newFont)
        {
            _oldEncoding = oldEncoding;
            _newEncoding = newEncoding;
            _newFont = newFont;

            ReadDuplicates();
            ReadTextTranslate(translatePath);
        }

        private void ReadDuplicates()
        {
            var dupl = SomeHelpers.ReadResource("DUPL_BMD.json");

            var data = JsonConvert.DeserializeObject<DuplicateModel[]>(dupl);

            _bmdDuplicates = data.SelectMany(x => x.Duplicates.Select(y => (dupl: y, orig: x.File))).ToDictionary(x => x.dupl, x => x.orig);
        }

        private void ReadTextTranslate(string translatePath)
        {
            var textZipPath = Path.Combine(translatePath, TextDir, TextZip);

            var textTranslate = new Dictionary<string, Dictionary<(int, int), string>>();
            var nameTranslate = new Dictionary<string, string>();

            using (var zip = ZipFile.OpenRead(textZipPath))
            {
                foreach (var entry in zip.Entries)
                {
                    var sheetName = Path.GetFileNameWithoutExtension(entry.Name);

                    string[] lines;
                    using (var entryStream = entry.Open())
                        lines = SomeHelpers.ReadAllLines(entryStream);

                    if (lines.Length < 2)
                        continue;

                    if (sheetName == "NAMES")
                    {
                        int engInd;
                        int translInd;
                        {
                            var titleSpl = lines[0].Split('\t');
                            engInd = Array.IndexOf(titleSpl, "ENG");
                            translInd = Array.IndexOf(titleSpl, "TRANSL");
                        }

                        if (engInd == -1 || translInd == -1)
                            continue;

                        foreach (var line in lines.Skip(1))
                        {
                            var spl = line.Split('\t');
                            var eng = spl[engInd];
                            var transl = spl[translInd];

                            if (string.IsNullOrEmpty(transl))
                                continue;

                            if (!nameTranslate.ContainsKey(eng))
                                nameTranslate.Add(eng, transl);
                        }
                    }
                    else
                    {
                        int fileNameInd;
                        int id1Ind;
                        int id2Ind;
                        int translInd;
                        {
                            var titleSpl = lines[0].Split('\t');
                            fileNameInd = Array.IndexOf(titleSpl, "FILE_NAME");
                            id1Ind = Array.IndexOf(titleSpl, "ID1");
                            id2Ind = Array.IndexOf(titleSpl, "ID2");
                            translInd = Array.IndexOf(titleSpl, "TRANSL");
                        }

                        if (fileNameInd == -1 || id1Ind == -1 || id2Ind == -1 || translInd == -1)
                            continue;

                        foreach (var line in lines.Skip(1))
                        {
                            var spl = line.Split('\t');
                            var fileName = spl[fileNameInd];
                            var ind1 = int.Parse(spl[id1Ind]);
                            var ind2 = int.Parse(spl[id2Ind]);
                            var translate = spl[translInd];

                            if (string.IsNullOrEmpty(translate))
                                continue;

                            var temp = sheetName + "|" + fileName;

                            if (!textTranslate.TryGetValue(temp, out var file))
                            {
                                file = new Dictionary<(int, int), string>();
                                textTranslate[temp] = file;
                            }

                            file[(ind1, ind2)] = translate;
                        }
                    }
                }
            }

            _bmdTextTranslate = textTranslate;
            _bmdNameTranslate = nameTranslate;
        }

        public bool Import(string relFilePath, GameFile gameFile)
        {
            var bmdGameFiles = gameFile.GetAllObjectFiles(PersonaEditorLib.FormatEnum.BMD).ToArray();
            if (!bmdGameFiles.Any())
                return false;

            var newCharW = _newFont.GetCharWidth(_newEncoding);

            var relDirPath = Path.GetDirectoryName(relFilePath);
            var relDir = string.Join("-", relDirPath.Split('\\')).ToUpper();

            foreach (var bmdGameFile in bmdGameFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(bmdGameFile.Name.Replace('/', '+')).ToUpper();
                var temp = relDir + "|" + fileName;

                if (_bmdDuplicates.ContainsKey(temp))
                    temp = _bmdDuplicates[temp];

                if (!_bmdTextTranslate.TryGetValue(temp, out var fileTr))
                    fileTr = new Dictionary<(int, int), string>();

                var bmd = bmdGameFile.GameData as BMD;

                for (int msgInd = 0; msgInd < bmd.Msg.Count; msgInd++)
                {
                    var msgData = bmd.Msg[msgInd];

                    for (int strInd = 0; strInd < msgData.MsgStrings.Length; strInd++)
                    {
                        var strData = msgData.MsgStrings[strInd];

                        var split = new MSGSplitter(strData, strInd + 1 == msgData.MsgStrings.Length);
                        if (fileTr.TryGetValue((msgInd, strInd), out var newStr))
                        {
                            split.ChangeBody(newStr, _newEncoding, newCharW);
                        }
                        else
                        {
                            split.ChangeEncoding(_oldEncoding, _newEncoding);
                        }

                        msgData.MsgStrings[strInd] = split.GetData();
                    }
                }

                foreach (var name in bmd.Name)
                {
                    var oldNameBases = name.NameBytes.GetTextBases().ToArray();

                    if (oldNameBases.Length != 1)
                        throw new Exception($"There is more or less than one block in the name. File {relFilePath}");

                    if (!oldNameBases[0].IsText)
                        continue;

                    var oldName = _oldEncoding.GetString(name.NameBytes).Replace("\0", "");

                    if (_bmdNameTranslate.TryGetValue(oldName, out var newName))
                    {
                        name.NameBytes = _newEncoding.GetBytes(newName);
                    }
                    else
                    {
                        name.NameBytes = _newEncoding.GetBytes(oldName);
                    }
                }
            }

            return true;
        }
    }
}