using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using P5R_Packager.Common;
using PersonaEditorLib;
using PersonaEditorLib.Other;

namespace P5R_Packager.DataTool.TextImport
{
    internal sealed class SpeakerImporter
    {
        private const string TextDir = "TEXT";
        private const string TextZip = "P5R_text.zip";

        private readonly PersonaEncoding _oldEncoding;
        private readonly PersonaEncoding _newEncoding;

        private Dictionary<string, string> _bmdNameTranslate;

        public SpeakerImporter(string translatePath, PersonaEncoding oldEncoding, PersonaEncoding newEncoding)
        {
            _oldEncoding = oldEncoding;
            _newEncoding = newEncoding;

            ReadTextTranslate(translatePath);
        }

        private void ReadTextTranslate(string translatePath)
        {
            var textZipPath = Path.Combine(translatePath, TextDir, TextZip);

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
                }
            }

            _bmdNameTranslate = nameTranslate;
        }

        public bool Import(string relFilePath, GameFile gameFile)
        {
            if (relFilePath.ToUpper() != "FONT\\SPEAKER.DAT")
                return false;

            var dat = gameFile.GameData as DAT;

            byte[] buffer = new byte[48];
            using (var inMS = new MemoryStream(dat.Data))
            using (var outMS = new MemoryStream(dat.Data.Length))
            {
                while (true)
                {
                    var readed = inMS.Read(buffer, 0, buffer.Length);
                    if (readed < buffer.Length)
                        break;

                    var oldName = _oldEncoding.GetString(buffer, 2, buffer.Length - 2).Replace("\0", "");

                    if (!_bmdNameTranslate.TryGetValue(oldName, out var newName))
                        newName = oldName;

                    var newNameData = SomeHelpers.TrimLength(newName, _newEncoding, 45);

                    Array.Fill<byte>(buffer, 0, 2, buffer.Length - 2);
                    newNameData.CopyTo(buffer, 2);

                    outMS.Write(buffer);
                }

                gameFile.GameData = new DAT(outMS.ToArray());
            }

            return true;
        }
    }
}