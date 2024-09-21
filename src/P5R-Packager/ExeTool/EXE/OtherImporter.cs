using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AsmResolver.PE.File;
using AuxiliaryLibraries.Tools;
using P5R_Packager.Common;
using P5R_Packager.ExeTool.NSO;
using PersonaEditorLib;
using PersonaEditorLib.Text;

namespace P5R_Packager.ExeTool.EXE
{
    internal class OtherImporter
    {
        private class TranslateData
        {
            public string Section { get; set; }

            public int Offset { get; set; }

            public int Length { get; set; }

            public string Eng { get; set; }

            public string Transl { get; set; }

            public bool IsUTF8 { get; set; }

            public int? Fill { get; set; }
        }

        private const string TextDir = "TEXT";
        private const string ExeZip = "P5R_exe.zip";

        private readonly PersonaEncoding _oldEncoding;
        private readonly PersonaEncoding _newEncoding;

        private readonly List<TranslateData> _translate = new List<TranslateData>();

        public OtherImporter(string translatePath, PersonaEncoding oldEncoding, PersonaEncoding newEncoding, ExeDataModel exeDataModel)
        {
            _oldEncoding = oldEncoding;
            _newEncoding = newEncoding;

            ReadText(translatePath, exeDataModel.OtherListName);
        }

        private void ReadText(string translatePath, string otherListName)
        {
            var textZipPath = Path.Combine(translatePath, TextDir, ExeZip);

            using (var zip = ZipFile.OpenRead(textZipPath))
            {
                foreach (var entry in zip.Entries)
                {
                    var sheetName = Path.GetFileNameWithoutExtension(entry.Name);

                    if (sheetName != otherListName)
                        continue;

                    string[] lines;
                    using (var entryStream = entry.Open())
                        lines = SomeHelpers.ReadAllLines(entryStream);

                    if (lines.Length < 2)
                        continue;

                    int sectionNameInd;
                    int offsetInd;
                    int lengthInd;
                    int engInd;
                    int translInd;
                    int isutf8Ind;
                    int fillInd;
                    {
                        var spl = lines[0].Split('\t');
                        sectionNameInd = Array.IndexOf(spl, "SECTION");
                        offsetInd = Array.IndexOf(spl, "OFFSET");
                        lengthInd = Array.IndexOf(spl, "LENGTH");
                        engInd = Array.IndexOf(spl, "ENG");
                        translInd = Array.IndexOf(spl, "TRANSL");
                        isutf8Ind = Array.IndexOf(spl, "UTF8");
                        fillInd = Array.IndexOf(spl, "FILL");
                    }

                    if (sectionNameInd == -1 || offsetInd == -1 ||
                        lengthInd == -1 || engInd == -1 ||
                        translInd == -1 || isutf8Ind == -1 ||
                        fillInd == -1)
                        return;

                    foreach (var line in lines.Skip(1))
                    {
                        var spl = line.Split('\t');
                        if (string.IsNullOrEmpty(spl[sectionNameInd]))
                            continue;

                        var data = new TranslateData
                        {
                            Section = spl[sectionNameInd],
                            Offset = int.Parse(spl[offsetInd]),
                            Length = int.Parse(spl[lengthInd]),
                            Eng = spl[engInd],
                            Transl = spl[translInd],
                            IsUTF8 = bool.TryParse(spl[isutf8Ind], out var isutf8) ? isutf8 : false,
                            Fill = int.TryParse(spl[fillInd], out var fill) ? fill : null,
                        };

                        _translate.Add(data);
                    }
                }
            }
        }

        public void Import(Stream stream, IPEFile peFile)
        {
            foreach (var tr in _translate)
            {
                var section = peFile.Sections.FirstOrDefault(x => x.Name == tr.Section);
                if (section == null)
                    continue;

                Import(stream, tr, (long)section.Offset);
            }
        }

        public void Import(NSOFile nso)
        {
            foreach (var tr in _translate)
            {
                var seg = nso.GetSegmentByName(tr.Section);
                if (seg == null)
                    continue;

                using (var ms = new MemoryStream(seg.Data))
                    Import(ms, tr, 0);
            }
        }

        private void Import(Stream stream, TranslateData tr, long sectionOffset)
        {
            var buffer = new byte[tr.Length];
            var splTransl = (string.IsNullOrEmpty(tr.Transl) ? " " : tr.Transl).SplitBySystem();
            Encoding targetEncoding = _newEncoding;

            if (tr.IsUTF8)
            {
                splTransl = splTransl.Select(x =>
                {
                    if (x.StartsWith('{') &&
                       x.EndsWith('}') &&
                       StringTool.TryParseArray(x.Substring(1, x.Length - 2), out var _))
                    {
                        return x;
                    }
                    else
                    {
                        return _oldEncoding.GetString(_newEncoding.GetBytes(x));
                    }
                }).ToList();
                targetEncoding = Encoding.UTF8;
            }

            byte[] translData;
            if (tr.Fill.HasValue)
            {
                translData = SomeHelpers.TrimLengthSys(splTransl, targetEncoding, tr.Fill.Value);

                bool isCenterFill = tr.Eng == "ENGLISH" || tr.Eng == "JAPANESE";

                if (isCenterFill)
                {
                    bool left = true;
                    while (translData.Length < tr.Fill.Value)
                    {
                        if (left)
                        {
                            translData = new byte[] { 0x20 }.Concat(translData).ToArray();
                            left = false;
                        }
                        else
                        {
                            translData = translData.Concat(new byte[] { 0x20 }).ToArray();
                            left = true;
                        }
                    }
                }
                else
                {
                    var fillTo = Enumerable.Repeat<byte>(0x20, tr.Fill.Value - translData.Length).ToArray();
                    translData = translData.Concat(fillTo).ToArray();
                }
            }
            else
            {
                translData = SomeHelpers.TrimLengthSys(splTransl, targetEncoding, tr.Length - 1);
            }

            Array.Copy(translData, buffer, translData.Length);

            stream.Seek(sectionOffset + tr.Offset, SeekOrigin.Begin);
            stream.Write(buffer, 0, buffer.Length);
        }
    }
}