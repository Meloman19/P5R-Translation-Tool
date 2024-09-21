using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AsmResolver.PE.File;
using AuxiliaryLibraries.Extensions;
using AuxiliaryLibraries.Tools;
using P5R_Packager.Common;
using PersonaEditorLib;

namespace P5R_Packager.ExeTool.EXE
{
    public sealed class CStringSpaceBlock
    {
        public long Position { get; set; }

        public long Size { get; set; }
    }

    public sealed class CStringData
    {
        public byte[] Data { get; set; }

        public long BlockPosition { get; set; }

        public long BlockSize { get; set; }

        public long[] AbsolutePointerPositions { get; set; }

        public string NewText { get; set; }

        public byte[] NewData { get; set; }

        public long NewBlockPosition { get; set; }

        public bool NotReuseSpace { get; set; } = false;
    }

    internal static class CStringHelper
    {
        public static void PackCStringsNS(Stream stream, CStringData[] blocks, long memoryOffset)
        {
            var workBlocks = GetWorkBlocks(blocks);

            ClearWorkBlocks(stream, workBlocks, 0);

            RelocateBlocks(workBlocks, blocks);

            WriteBlockAndPointer(stream, blocks, sectionOffset: 0, imageBase: (ulong)memoryOffset, sectionRva: 0);
        }

        public static void PackCStrings(Stream stream, CStringData[] blocks, long sectionOffset, ulong imageBase, uint sectionRva)
        {
            var workBlocks = GetWorkBlocks(blocks);

            ClearWorkBlocks(stream, workBlocks, sectionOffset);

            RelocateBlocks(workBlocks, blocks);

            WriteBlockAndPointer(stream, blocks, sectionOffset: sectionOffset, imageBase: imageBase, sectionRva: sectionRva);
        }

        private static CStringSpaceBlock[] GetWorkBlocks(CStringData[] blocks)
        {
            var result = new List<CStringSpaceBlock>();

            var sortedBlocks = blocks.Where(b => !b.NotReuseSpace).OrderBy(b => b.BlockPosition).ToArray();

            CStringSpaceBlock current = null;
            for (int i = 0; i < sortedBlocks.Length; i++)
            {
                var block = sortedBlocks[i];

                if (current == null)
                {
                    current = new CStringSpaceBlock();
                    current.Position = block.BlockPosition;
                    current.Size = block.BlockSize;
                }
                else
                {
                    if (current.Position + current.Size > block.BlockPosition)
                        throw new Exception("Блок вышел за рамки");

                    if (current.Position + current.Size == block.BlockPosition)
                    {
                        current.Size += block.BlockSize;
                    }
                    else
                    {
                        result.Add(current);
                        current = new CStringSpaceBlock();
                        current.Position = block.BlockPosition;
                        current.Size = block.BlockSize;
                    }
                }
            }

            if (current != null)
            {
                result.Add(current);
            }

            return result.ToArray();
        }

        private static void ClearWorkBlocks(Stream stream, CStringSpaceBlock[] workBlocks, long sectionOffset)
        {
            foreach (var workBlock in workBlocks)
            {
                stream.Seek(sectionOffset + workBlock.Position, SeekOrigin.Begin);
                for (int i = 0; i < workBlock.Size; i++)
                    stream.WriteByte(0);
            }
        }

        private static void RelocateBlocks(CStringSpaceBlock[] workBlocks, CStringData[] blocks)
        {
            var b = blocks.ToList();

            foreach (var workBlock in workBlocks)
            {
                var pos = workBlock.Position;
                var free = workBlock.Size;
                while (true)
                {
                    if (b.Count == 0)
                        return;

                    var blockToInsert = b.Find(x => x.NewData.Length + 1 < free);
                    if (blockToInsert == null)
                        break;

                    b.Remove(blockToInsert);
                    var alignSize = (blockToInsert.NewData.Length / 4 + 1) * 4;
                    blockToInsert.NewBlockPosition = pos;
                    pos += alignSize;
                    free -= alignSize;
                }
            }

            if (b.Count != 0)
                throw new Exception("Not all the blocks fit");
        }

        private static void WriteBlockAndPointer(Stream stream, CStringData[] blocks, long sectionOffset, ulong imageBase, uint sectionRva)
        {
            foreach (var block in blocks)
            {
                stream.Seek(sectionOffset + block.NewBlockPosition, SeekOrigin.Begin);
                stream.Write(block.NewData, 0, block.NewData.Length);

                var newPointerData = GetPointerData(imageBase, sectionRva, block.NewBlockPosition);
                foreach (var absolutePointerPosition in block.AbsolutePointerPositions)
                {
                    stream.Seek(absolutePointerPosition, SeekOrigin.Begin);
                    stream.Write(newPointerData, 0, newPointerData.Length);
                }
            }
        }

        private static byte[] GetPointerData(ulong imageBase, uint rva, long pos)
        {
            var pointer = imageBase + rva + (ulong)pos;

            var data = BitConverter.GetBytes(pointer);

            return data;
        }
    }

    internal sealed class MOVIEImporter
    {
        private sealed class TranslateData
        {
            public byte[] EngData { get; set; }

            public string Eng { get; set; }

            public string Transl { get; set; }
        }

        private const string TextDir = "TEXT";
        private const string ExeZip = "P5R_exe.zip";
        private const string TargetList = "MOVIE";

        private static readonly byte[][] IgnoreStrings = new[]
        {
            new byte[] { 0x3C, 0x42, 0x52 ,0x3E },
            new byte[] { 0x73, 0x65, 0x65, 0x6B, 0x20, 0x70, 0x61, 0x75, 0x73, 0x65, 0x20, 0x5B, 0x25, 0x64, 0x5D, 0x0A },
        };

        private readonly PersonaEncoding _oldEncoding;
        private readonly PersonaEncoding _newEncoding;
        private readonly string _sectorName;
        private readonly long _subPos;
        private readonly long _subSize;

        private TranslateData[] _textData;

        public MOVIEImporter(string translatePath, PersonaEncoding oldEncoding, PersonaEncoding newEncoding, ExeDataModel exeData)
        {
            _oldEncoding = oldEncoding;
            _newEncoding = newEncoding;
            _sectorName = exeData.PCMovieData.Section;
            _subPos = exeData.PCMovieData.OffsetInt;
            _subSize = exeData.PCMovieData.SizeInt;

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

                    var translateDatas = new List<TranslateData>();

                    foreach (var line in lines.Skip(1))
                    {
                        var spl = line.Split('\t');
                        var eng = spl[engInd];
                        if (string.IsNullOrEmpty(eng))
                            continue;
                        var transl = spl[translInd];
                        if (!StringTool.TryParseArray(spl[engDataInd], out var engData))
                            throw new Exception($"Не удалось прочитать ENG_DATA для текста {eng}");

                        translateDatas.Add(new TranslateData
                        {
                            EngData = engData,
                            Eng = eng,
                            Transl = transl
                        });
                    }

                    _textData = translateDatas.ToArray();
                }
            }
        }

        public void Import(Stream stream, IPEFile peFile)
        {
            var targetSection = peFile.Sections.FirstOrDefault(x => x.Name == _sectorName);
            if (targetSection == null)
                throw new Exception($"Will not find a sector {_sectorName}  in EXE");

            (ulong ImageBase, uint SecRVA, ulong SecOffset, uint PhysSize) secData = (peFile.OptionalHeader.ImageBase, targetSection.Rva, targetSection.Offset, targetSection.Contents.GetPhysicalSize());

            var allSubStrings = ReadAllCString(stream, _subPos, _subSize, secData).ToArray();

            FillWithNewData(allSubStrings);

            ReadPointerPositions(stream, allSubStrings, secData);

            CStringHelper.PackCStrings(stream, allSubStrings,
                sectionOffset: (long)secData.SecOffset, imageBase: secData.ImageBase, sectionRva: secData.SecRVA);
        }

        private static CStringData[] ReadAllCString(Stream stream, long subPos, long subSize, (ulong ImageBase, uint SecRVA, ulong SecOffset, uint PhysSize) secData)
        {
            if (subPos % 16 != 0 || subSize % 16 != 0)
                throw new Exception("Что-то не так с параметрами блока с субтитрами");

            var data = new List<byte>();
            var ignore = new List<CStringData>();
            var result = new List<CStringData>();

            stream.Seek((long)secData.SecOffset + subPos, SeekOrigin.Begin);
            for (int i = 0; i < subSize; i++)
            {
                var b = stream.ReadByte();
                if (b == -1)
                    throw new Exception("Неожиданный конец потока");

                var B = (byte)b;
                if (B == 0)
                {
                    if (data.Count == 0)
                        continue;

                    var block = new CStringData
                    {
                        Data = data.ToArray(),
                        BlockPosition = subPos + i - data.Count
                    };
                    data.Clear();

                    if (IgnoreStrings.Any(x => x.ArrayEquals(block.Data)))
                        ignore.Add(block);

                    result.Add(block);
                }
                else
                {
                    if (data.Count == 0)
                    {
                        var last = result.LastOrDefault();
                        if (last != null)
                        {
                            var nextPos = subPos + i;
                            last.BlockSize = nextPos - last.BlockPosition;
                        }
                    }

                    data.Add(B);
                }
            }

            {
                var last = result.Last();
                last.BlockSize = subPos + subSize - last.BlockPosition;
            }

            return result.Except(ignore).ToArray();
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

                var splittedText = newText
                    .Split(new string[] { "<BR>" }, StringSplitOptions.None)
                    .Select(x => _oldEncoding.GetString(_newEncoding.GetBytes(x)));
                var encodedText = string.Join("<BR>", splittedText);
                if (string.IsNullOrEmpty(encodedText))
                    newText = " ";

                var newData = Encoding.UTF8.GetBytes(encodedText);

                block.NewText = newText;
                block.NewData = newData;
            }
        }

        private static void ReadPointerPositions(Stream stream, CStringData[] blocks, (ulong ImageBase, uint SecRVA, ulong SecOffset, uint PhysSize) secData)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[8];
            var b = blocks.ToDictionary(x => secData.ImageBase + secData.SecRVA + (ulong)x.BlockPosition, x => x);

            while (stream.Read(buffer, 0, 8) == 8)
            {
                var ul = BitConverter.ToUInt64(buffer, 0);

                if (b.TryGetValue(ul, out var bl))
                {
                    if (bl.AbsolutePointerPositions != null)
                        throw new Exception("This block already has a pointer");

                    bl.AbsolutePointerPositions = new[] { stream.Position - 8 };
                }
            }

            if (blocks.Any(x => x.AbsolutePointerPositions == null))
                throw new Exception("There are blocks without a pointer");
        }
    }
}