using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AsmResolver.PE.File;
using AuxiliaryLibraries.Extensions;
using AuxiliaryLibraries.Tools;
using P5R_Packager.Common;
using PersonaEditorLib;
using PersonaEditorLib.Text;

namespace P5R_Packager.ExeTool.EXE
{
    internal class PtrArrayImporter
    {
        private sealed class TranslateData
        {
            public byte[] EngData { get; set; }

            public string Eng { get; set; }

            public string Transl { get; set; }
        }

        private const string TextDir = "TEXT";
        private const string ExeZip = "P5R_exe.zip";
        private const string TargetList = "ARRAY";

        private readonly PersonaEncoding _oldEncoding;
        private readonly PersonaEncoding _newEncoding;
        private readonly ExePointerDataModel[] _arrayData;

        private TranslateData[] _textData;

        public PtrArrayImporter(string translatePath, PersonaEncoding oldEncoding, PersonaEncoding newEncoding, ExeDataModel exeData)
        {
            _oldEncoding = oldEncoding;
            _newEncoding = newEncoding;
            _arrayData = exeData.PtrArray;

            ReadText(translatePath);
        }

        private void ReadText(string translatePath)
        {
            var textZipPath = Path.Combine(translatePath, TextDir, ExeZip);

            using (var zip = ZipFile.OpenRead(textZipPath))
            {
                foreach (var entry in zip.Entries)
                {
                    var sheetName = Path.GetFileNameWithoutExtension(entry.Name);

                    if (sheetName != TargetList)
                        continue;

                    string[] lines;
                    using (var entryStream = entry.Open())
                        lines = SomeHelpers.ReadAllLines(entryStream);

                    if (lines.Length < 2)
                        continue;

                    int engDataInd, engInd, translInd;
                    {
                        var titleSpl = lines[0].Split('\t');
                        engDataInd = Array.IndexOf(titleSpl, "ENG_DATA");
                        engInd = Array.IndexOf(titleSpl, "ENG");
                        translInd = Array.IndexOf(titleSpl, "TRANSL");
                    }

                    if (engDataInd == -1 || engInd == -1 || translInd == -1)
                        continue;

                    var textData = new List<TranslateData>();

                    for (int i = 1; i < lines.Length; i++)
                    {
                        var spl = lines[i].Split('\t');
                        var eng = spl[engInd];
                        if (string.IsNullOrEmpty(eng))
                            continue;
                        var transl = spl[translInd];
                        if (!StringTool.TryParseArray(spl[engDataInd], out var engData))
                            throw new Exception($"Could not read ENG_DATA for text {eng}");

                        var data = new TranslateData
                        {
                            EngData = engData,
                            Eng = eng,
                            Transl = transl
                        };
                        textData.Add(data);
                    }

                    _textData = textData.ToArray();
                }
            }
        }

        public void Import(Stream stream, IPEFile peFile)
        {
            var imageBase = peFile.OptionalHeader.ImageBase;

            foreach (var targetGroup in _arrayData.GroupBy(x => x.PtrDataSection))
            {
                var ptrDataSection = targetGroup.Key;

                var targetSection = peFile.Sections.FirstOrDefault(x => x.Name == ptrDataSection);
                if (targetSection == null)
                    throw new Exception($"Will not find a sector {ptrDataSection} in EXE");

                var blocksList = new List<CStringData>();

                foreach (var pointerGroup in targetGroup.GroupBy(x => x.PtrSection))
                {
                    var ptrSection = pointerGroup.Key;
                    var pointerSection = peFile.Sections.FirstOrDefault(x => x.Name == ptrSection);
                    if (pointerSection == null)
                        throw new Exception($"Will not find a sector {ptrSection} in EXE");

                    var toAdd = pointerGroup.SelectMany(x => ReadAllCString(stream, x.PtrOffsetInt, x.PtrSizeInt, imageBase, pointerSection, targetSection)).ToArray();
                    blocksList.AddRange(toAdd);
                }

                var blocks = blocksList.ToArray();

                FillWithNewData(blocks);

                CStringHelper.PackCStrings(stream, blocks,
                    sectionOffset: (long)targetSection.Offset, imageBase: imageBase, sectionRva: targetSection.Rva);
            }
        }

        private static CStringData[] ReadAllCString(Stream stream, int pOffset, int pSize, ulong imageBase, PESection pointerSec, PESection targetSec)
        {
            if (pOffset % 8 != 0 || pSize % 8 != 0)
                throw new Exception("Something is wrong with the parameters of the array block");

            var result = new List<CStringData>();

            var pCount = pSize / 8;
            var buffer = new byte[8];
            for (int i = 0; i < pCount; i++)
            {
                var absolutePointerPosition = (long)pointerSec.Offset + pOffset + i * 8;
                stream.Seek(absolutePointerPosition, SeekOrigin.Begin);

                long blockPosition;
                {
                    stream.Read(buffer, 0, buffer.Length);
                    var pnt = BitConverter.ToUInt64(buffer, 0);
                    blockPosition = (long)(pnt - targetSec.Rva - imageBase);
                }

                stream.Seek((long)targetSec.Offset + blockPosition, SeekOrigin.Begin);
                var data = new List<byte>();
                int readedByte;
                while ((readedByte = stream.ReadByte()) != 0)
                {
                    if (readedByte == -1)
                        throw new Exception("EOF");

                    data.Add((byte)readedByte);
                }

                var cString = new CStringData
                {
                    Data = data.ToArray(),
                    BlockPosition = blockPosition,
                    BlockSize = (data.Count / 4 + 1) * 4,
                    AbsolutePointerPositions = new[] { absolutePointerPosition },
                };

                result.Add(cString);
            }

            return result.ToArray();
        }

        private void FillWithNewData(CStringData[] blocks)
        {
            var index = 1;

            foreach (var block in blocks)
            {
                var translate = _textData.FirstOrDefault(x => x.EngData.ArrayEquals(block.Data));
                string newText;
                if (translate == null)
                {
                    newText = index.ToString();
                    index++;
                }
                else
                {
                    newText = translate.Transl;
                }

                if (string.IsNullOrEmpty(newText))
                    newText = " ";

                var newData = newText.GetTextBases(_newEncoding).GetByteArray();

                block.NewText = newText;
                block.NewData = newData;
            }
        }
    }
}