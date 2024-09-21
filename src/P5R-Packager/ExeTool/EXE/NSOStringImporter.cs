using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AuxiliaryLibraries.Extensions;
using AuxiliaryLibraries.Tools;
using P5R_Packager.Common;
using P5R_Packager.ExeTool.NSO;
using PersonaEditorLib;
using PersonaEditorLib.Text;

namespace P5R_Packager.ExeTool.EXE
{
    internal class NSOStringImporter
    {
        private sealed class TranslateData
        {
            public bool Founded { get; set; } = false;

            public string SheetName { get; set; }

            public byte[] EngData { get; set; }

            public string Eng { get; set; }

            public string Transl { get; set; }

            public bool IsUTF8 { get; set; }
        }

        private const string TextDir = "TEXT";
        private const string ExeZip = "P5R_exe.zip";
        private const string TargetListMovie = "MOVIE";
        private const string TargetListArray = "ARRAY";
        private const string TargetListOther = "OTHER_NS";
        private readonly PersonaEncoding _oldEncoding;
        private readonly PersonaEncoding _newEncoding;
        private readonly ExeDataModel _exeData;

        private TranslateData[] _textData;

        public NSOStringImporter(string translatePath, PersonaEncoding oldEncoding, PersonaEncoding newEncoding, ExeDataModel exeData)
        {
            _oldEncoding = oldEncoding;
            _newEncoding = newEncoding;
            _exeData = exeData;

            ReadText(translatePath);
        }

        private void ReadText(string translatePath)
        {
            var textZipPath = Path.Combine(translatePath, TextDir, ExeZip);

            using (var zip = ZipFile.OpenRead(textZipPath))
            {
                var textData = new List<TranslateData>();

                foreach (var entry in zip.Entries)
                {
                    var sheetName = Path.GetFileNameWithoutExtension(entry.Name);

                    if (sheetName != TargetListMovie && sheetName != TargetListArray && sheetName != "ARRAY_NS")
                        continue;

                    string[] lines;
                    using (var entryStream = entry.Open())
                        lines = SomeHelpers.ReadAllLines(entryStream);

                    if (lines.Length < 2)
                        continue;

                    int engDataInd, engInd, translInd, isUtf8Ind;
                    {
                        var titleSpl = lines[0].Split('\t');
                        engDataInd = Array.IndexOf(titleSpl, "ENG_DATA");
                        engInd = Array.IndexOf(titleSpl, "ENG");
                        translInd = Array.IndexOf(titleSpl, "TRANSL");
                        isUtf8Ind = Array.IndexOf(titleSpl, "UTF8");
                    }

                    if (engDataInd == -1 || engInd == -1 || translInd == -1)
                        continue;

                    for (int i = 1; i < lines.Length; i++)
                    {
                        var spl = lines[i].Split('\t');
                        var eng = spl[engInd];
                        if (string.IsNullOrEmpty(eng))
                            continue;
                        var transl = spl[translInd];
                        if (string.IsNullOrEmpty(spl[engDataInd]))
                            continue;
                        if (!StringTool.TryParseArray(spl[engDataInd], out var engData))
                            throw new Exception($"Не удалось прочитать ENG_DATA для текста {eng}");

                        bool isUtf8 = false;
                        if (isUtf8Ind != -1)
                        {
                            isUtf8 = bool.TryParse(spl[isUtf8Ind], out var isutf8) ? isutf8 : false;
                        }

                        if (textData.Find(x => x.EngData.ArrayEquals(engData)) != null)
                        {
                            continue;
                        }

                        var sheet = sheetName;
                        if (sheet == "ARRAY_NS")
                            sheet = TargetListArray;

                        var data = new TranslateData
                        {
                            SheetName = sheet,
                            EngData = engData,
                            Eng = eng,
                            Transl = transl,
                            IsUTF8 = isUtf8
                        };
                        textData.Add(data);
                    }
                }

                _textData = textData.ToArray();
            }
        }

        public void Import(NSOFile nso)
        {
            var text = nso.Text;
            var ro = nso.Ro;
            var dat = nso.Data;

            var allCString = new List<CStringData>();

            {
                var allReloc = new List<(long relocOffset, long ptrPos, long sPos)>();
                using (var ms = new MemoryStream(ro.Data))
                using (var reader = new BinaryReader(ms))
                {
                    ms.Seek(0x60, SeekOrigin.Begin);

                    {
                        while (true)
                        {
                            var relocOffset = reader.ReadInt64();
                            long code = reader.ReadInt64();
                            if (code != 0x403)
                                break;

                            var pointerPosition = ms.Position;
                            long sPos = reader.ReadInt64();

                            allReloc.Add((relocOffset, pointerPosition, sPos));
                        }
                    }
                }

                var editReloc = new List<(long relocOffset, long ptrPos, long sPos)>();
                using (var ms = new MemoryStream(dat.Data))
                using (var reader = new BinaryReader(ms))
                {
                    foreach (var movieBlock in _exeData.MovieSubBlocks)
                    {
                        foreach (var offset in movieBlock.LongOffsets)
                        {
                            ms.Seek(offset, SeekOrigin.Begin);
                            _ = reader.ReadInt64();
                            while (reader.ReadUInt64() != 0xFFFFFFFFFFFFFFFF)
                            {
                                var currentPos = ms.Position;
                                var memoryOffset = currentPos + dat.MemoryOffset;

                                var relocs = allReloc.FindAll(x => x.relocOffset == memoryOffset);
                                if (relocs.Count != 1)
                                    throw new Exception("");

                                var reloc = relocs.First();
                                allReloc.Remove(reloc);
                                editReloc.Add(reloc);

                                _ = reader.ReadInt64();
                            }
                        }
                    }
                }

                foreach (var arrayBlock in _exeData.PtrArray)
                {
                    var seg = nso.GetSegmentByName(arrayBlock.PtrSection);
                    using (var ms = new MemoryStream(seg.Data))
                    using (var reader = new BinaryReader(ms))
                    {
                        ms.Seek(arrayBlock.PtrOffsetInt, SeekOrigin.Begin);

                        var count = arrayBlock.PtrSizeInt / 8;
                        for (int i = 0; i < count; i++)
                        {
                            var currentPos = ms.Position;
                            var memoryOffset = currentPos + dat.MemoryOffset;

                            var relocs = allReloc.FindAll(x => x.relocOffset == memoryOffset);
                            if (relocs.Count != 1)
                                throw new Exception("");

                            var reloc = relocs.First();
                            allReloc.Remove(reloc);
                            editReloc.Add(reloc);

                            _ = reader.ReadInt64();
                        }
                    }
                }

                int index = 1;
                var dataList = new List<byte>();
                var editGroup = editReloc.GroupBy(x => x.sPos).Select(x => (x.Key, x.Select(x => x.ptrPos).ToArray())).ToArray();
                foreach ((long sPos, long[] ptrPos) reloc in editGroup)
                {
                    if (reloc.sPos < ro.MemoryOffset || reloc.sPos > ro.MemoryOffset + ro.Data.Length)
                        continue;

                    dataList.Clear();
                    using (var ms = new MemoryStream(ro.Data))
                    using (var reader = new BinaryReader(ms))
                    {
                        ms.Seek(reloc.sPos - ro.MemoryOffset, SeekOrigin.Begin);

                        int readedByte;
                        while ((readedByte = ms.ReadByte()) != 0)
                        {
                            if (readedByte == -1)
                                throw new Exception("EOF");

                            dataList.Add((byte)readedByte);
                        }
                    }

                    if (dataList.Count == 0)
                        continue;

                    string newText;
                    byte[] newData;

                    var data = dataList.ToArray();
                    var translateData = _textData.FirstOrDefault(x => x.EngData.ArrayEquals(data));
                    if (translateData != null)
                    {
                        translateData.Founded = true;

                        if (translateData.SheetName == TargetListMovie)
                        {
                            newText = translateData.Transl;
                            var splittedText = newText
                                .Split(new string[] { "<BR>" }, StringSplitOptions.None)
                                .Select(x => _oldEncoding.GetString(_newEncoding.GetBytes(x)));
                            var encodedText = string.Join("<BR>", splittedText);
                            if (string.IsNullOrEmpty(encodedText))
                                newText = " ";

                            newData = Encoding.UTF8.GetBytes(encodedText);
                        }
                        else if (translateData.SheetName == TargetListOther)
                        {
                            newText = translateData.Transl;
                            if (string.IsNullOrEmpty(newText))
                                newText = " ";

                            var splTransl = newText.SplitBySystem();
                            Encoding targetEncoding = _newEncoding;

                            if (translateData.IsUTF8)
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
                            newData = SomeHelpers.TrimLengthSys(splTransl, targetEncoding, 255);
                        }
                        else
                        {
                            newText = translateData.Transl;
                            if (string.IsNullOrEmpty(newText))
                                newText = " ";

                            newData = newText.GetTextBases(_newEncoding).GetByteArray();
                        }
                    }
                    else
                    {
                        newText = index.ToString().PadLeft(4, '0');
                        index++;

                        newData = Encoding.UTF8.GetBytes(newText);
                    }

                    var anyOldString = allReloc.Any(x => x.sPos == reloc.sPos);
                    var cString = new CStringData
                    {
                        Data = data,
                        BlockPosition = reloc.sPos - ro.MemoryOffset,
                        BlockSize = data.Length + 1,
                        AbsolutePointerPositions = reloc.ptrPos,
                        NewData = newData,
                        NewText = newText,
                        NotReuseSpace = anyOldString
                    };
                    allCString.Add(cString);
                }
            }

            var canReuse = allCString.Where(x => !x.NotReuseSpace).ToArray();
            var notFounded = _textData.Where(x => x.Founded == false).ToArray();

            using (var ms = new MemoryStream(ro.Data))
                CStringHelper.PackCStringsNS(ms, allCString.ToArray(), ro.MemoryOffset);
        }
    }
}
