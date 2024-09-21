using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AsmResolver.PE.File;
using P5R_Packager.Common;
using P5R_Packager.ExeTool.NSO;
using PersonaEditorLib;

namespace P5R_Packager.ExeTool.EXE
{
    internal class ChatNameImporter
    {
        private const string TextDir = "TEXT";
        private const string TextZip = "P5R_text.zip";

        private const int ChatNameMaxSize = 0x2E;
        private const int ChatNameBlockSize = 0x30;
        private const int ChatNameCount = 0x20;

        private readonly PersonaEncoding _oldEncoding;
        private readonly PersonaEncoding _newEncoding;
        private readonly string _sectionName;
        private readonly long _pos;

        private Dictionary<string, string> _bmdNameTranslate;

        public ChatNameImporter(string translatePath, PersonaEncoding oldEncoding, PersonaEncoding newEncoding, ExeDataModel exeData)
        {
            _oldEncoding = oldEncoding;
            _newEncoding = newEncoding;

            _sectionName = exeData.ChatData.Section;
            _pos = exeData.ChatData.OffsetInt;

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
                    if (sheetName != "NAMES")
                        continue;

                    string[] lines;
                    using (var entryStream = entry.Open())
                        lines = SomeHelpers.ReadAllLines(entryStream);

                    if (lines.Length < 2)
                        continue;

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

            _bmdNameTranslate = nameTranslate;
        }

        public void Import(Stream stream, IPEFile peFile)
        {
            var targetSection = peFile.Sections.FirstOrDefault(x => x.Name == _sectionName);
            if (targetSection == null)
                throw new Exception($"Не найдет сектор {_sectionName} в EXE");

            stream.Seek((long)targetSection.Offset + _pos, SeekOrigin.Begin);

            Import(stream);
        }

        public void Import(NSOFile nso)
        {
            NSOSegment seg;
            switch (_sectionName)
            {
                case ".data":
                    seg = nso.Data; break;
                case ".rodata":
                    seg = nso.Ro; break;
                default:
                    throw new Exception("");
            }

            using (var ms = new MemoryStream(seg.Data))
            {
                ms.Seek(_pos, SeekOrigin.Begin);

                Import(ms);
            }
        }

        private void Import(Stream stream)
        {
            var buffer = new byte[ChatNameMaxSize];
            for (int i = 0; i < ChatNameCount; i++)
            {
                stream.Read(buffer, 0, buffer.Length);

                var oldName = _oldEncoding.GetString(buffer.TakeWhile(x => x != 0).ToArray());
                if (!_bmdNameTranslate.TryGetValue(oldName, out var newName))
                    newName = oldName;

                var newNameData = SomeHelpers.TrimLength(newName, _newEncoding, buffer.Length - 1);
                Array.Clear(buffer, 0, buffer.Length);
                Array.Copy(newNameData, buffer, newNameData.Length);

                stream.Seek(-buffer.Length, SeekOrigin.Current);
                stream.Write(buffer, 0, buffer.Length);

                stream.Seek(ChatNameBlockSize - ChatNameMaxSize, SeekOrigin.Current);
            }
        }
    }
}