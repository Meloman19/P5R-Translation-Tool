using AuxiliaryLibraries.Extensions;
using AuxiliaryLibraries.Tools;
using K4os.Compression.LZ4;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace P5R_Packager.ExeTool.NSO
{
    public sealed class NSOFile
    {
        private struct RelativeHeader
        {
            public uint Offset;
            public uint Size;
        }

        private struct SegmentHeader
        {
            public uint FileOffset;
            public uint MemoryOffset;
            public uint Size;
        }

        private readonly static byte[] NSOMagic = new byte[] { 0x4E, 0x53, 0x4F, 0x30 };

        private NSOFlags _flags;
        private uint _bss;
        private byte[] _moduleId;

        private RelativeHeader _embedded;
        private RelativeHeader _dynStr;
        private RelativeHeader _dynSym;

        private NSOSegment _textSegment;
        private NSOSegment _roSegment;
        private NSOSegment _dataSegment;

        public NSOFile(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                Read(reader);
        }

        public NSOFile(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                Read(reader);
        }

        public ReadOnlySpan<byte> ModuleId => _moduleId;

        public NSOSegment Text => _textSegment;

        public NSOSegment Ro => _roSegment;

        public NSOSegment Data => _dataSegment;

        private void Read(BinaryReader reader)
        {
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            {
                var magic = reader.ReadBytes(4);
                if (!magic.ArrayEquals(NSOMagic))
                    throw new Exception("NSO: wrong magic");

                var version = reader.ReadUInt32();
                if (version != 0)
                    throw new Exception("NSO: supported only 0 version");

                var reserved1 = reader.ReadBytes(4);
                if (reserved1.Any(x => x != 0))
                    throw new Exception("NSO: reserved1 not empty");

                _flags = (NSOFlags)reader.ReadUInt32();
            }

            var textSegment = reader.ReadStruct<SegmentHeader>();

            var moduleNameOffset = reader.ReadUInt32();

            var roSegment = reader.ReadStruct<SegmentHeader>();

            var moduleNameSize = reader.ReadUInt32();

            var dataSegment = reader.ReadStruct<SegmentHeader>();

            _bss = reader.ReadUInt32();
            _moduleId = reader.ReadBytes(0x20);

            var textFileSize = reader.ReadUInt32();
            var roFileSize = reader.ReadUInt32();
            var dataFileSize = reader.ReadUInt32();

            var reserved2 = reader.ReadBytes(0x1C);
            if (reserved2.Any(x => x != 0))
                throw new Exception("NSO: reserved2 not empty");

            _embedded = reader.ReadStruct<RelativeHeader>();
            _dynStr = reader.ReadStruct<RelativeHeader>();
            _dynSym = reader.ReadStruct<RelativeHeader>();

            var textHash = reader.ReadBytes(0x20);
            var roHash = reader.ReadBytes(0x20);
            var dataHash = reader.ReadBytes(0x20);

            {
                reader.BaseStream.Seek(moduleNameOffset, SeekOrigin.Begin);
                var moduleName = reader.ReadBytes((int)moduleNameSize);
                if (moduleName.Length != 0 && (moduleName.Length != 1 || moduleName[0] != 0))
                    throw new Exception("");

                _textSegment = ReadSegment(reader, textSegment, textFileSize, textHash, NSOFlags.TextCompressed, ".text");
                _roSegment = ReadSegment(reader, roSegment, roFileSize, roHash, NSOFlags.RoCompressed, ".rodata");
                _dataSegment = ReadSegment(reader, dataSegment, dataFileSize, dataHash, NSOFlags.DataCompressed, ".data");
            }
        }

        private NSOSegment ReadSegment(BinaryReader reader, SegmentHeader segmentHeader, uint fileSize, byte[] hash, NSOFlags compressedFlag, string name)
        {
            reader.BaseStream.Seek(segmentHeader.FileOffset, SeekOrigin.Begin);

            var data = reader.ReadBytes(Convert.ToInt32(fileSize));
            if (_flags.HasFlag(compressedFlag))
            {
                var newData = new byte[segmentHeader.Size];
                var written = LZ4Codec.Decode(data, newData);
                if (written < 0)
                    throw new Exception("NSO Segment: wrong size");

                if (written != segmentHeader.Size)
                    throw new Exception("NSO Segment: wrong size");

                data = newData;
                _flags &= ~compressedFlag;
            }

            if (data.Length != segmentHeader.Size)
                throw new Exception("NSO Segment: wrong size");

            var calcHash = GetSHA256(data);
            if (!calcHash.ArrayEquals(hash))
                throw new Exception("NSO Segment: wrong hash");

            return new NSOSegment(segmentHeader.MemoryOffset, data, name);
        }

        public byte[] GetData(bool compressed)
        {
            NSOFlags newFlags;
            uint moduleNameOffset = 0x100;
            byte[] moduleName;

            SegmentHeader textSegment, roSegment, dataSegment;
            byte[] textData, roData, dataData;

            if (compressed)
            {
                newFlags = _flags | NSOFlags.TextCompressed | NSOFlags.RoCompressed | NSOFlags.DataCompressed;
                moduleName = new byte[] { 0x00 };

                textSegment = new SegmentHeader
                {
                    FileOffset = Convert.ToUInt32(moduleNameOffset + moduleName.Length),
                    MemoryOffset = _textSegment.MemoryOffset,
                    Size = Convert.ToUInt32(_textSegment.Data.Length)
                };
                textData = GetCompressed(_textSegment.Data);

                roSegment = new SegmentHeader
                {
                    FileOffset = Convert.ToUInt32(textSegment.FileOffset + textData.Length),
                    MemoryOffset = _roSegment.MemoryOffset,
                    Size = Convert.ToUInt32(_roSegment.Data.Length)
                };
                roData = GetCompressed(_roSegment.Data);

                dataSegment = new SegmentHeader
                {
                    FileOffset = Convert.ToUInt32(roSegment.FileOffset + roData.Length),
                    MemoryOffset = _dataSegment.MemoryOffset,
                    Size = Convert.ToUInt32(_dataSegment.Data.Length)
                };
                dataData = GetCompressed(_dataSegment.Data);
            }
            else
            {
                newFlags = _flags;
                moduleName = Array.Empty<byte>();

                textSegment = new SegmentHeader
                {
                    FileOffset = moduleNameOffset + _textSegment.MemoryOffset,
                    MemoryOffset = _textSegment.MemoryOffset,
                    Size = Convert.ToUInt32(_textSegment.Data.Length)
                };
                textData = _textSegment.Data;

                roSegment = new SegmentHeader
                {
                    FileOffset = moduleNameOffset + _roSegment.MemoryOffset,
                    MemoryOffset = _roSegment.MemoryOffset,
                    Size = Convert.ToUInt32(_roSegment.Data.Length)
                };
                roData = _roSegment.Data;

                dataSegment = new SegmentHeader
                {
                    FileOffset = moduleNameOffset + _dataSegment.MemoryOffset,
                    MemoryOffset = _dataSegment.MemoryOffset,
                    Size = Convert.ToUInt32(_dataSegment.Data.Length)
                };
                dataData = _dataSegment.Data;
            }

            var textHash = GetSHA256(_textSegment.Data);
            var roHash = GetSHA256(_roSegment.Data);
            var dataHash = GetSHA256(_dataSegment.Data);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(NSOMagic);
                writer.Write(0);
                writer.Write(0);
                writer.Write((uint)newFlags);

                writer.WriteStruct(textSegment);
                writer.Write(moduleNameOffset);

                writer.WriteStruct(roSegment);
                writer.Write(moduleName.Length);

                writer.WriteStruct(dataSegment);
                writer.Write(_bss);

                writer.Write(_moduleId);

                writer.Write(textData.Length);
                writer.Write(roData.Length);
                writer.Write(dataData.Length);

                writer.Write(new byte[0x1C]);

                writer.WriteStruct(_embedded);
                writer.WriteStruct(_dynStr);
                writer.WriteStruct(_dynSym);

                writer.Write(textHash);
                writer.Write(roHash);
                writer.Write(dataHash);

                ms.Position = moduleNameOffset;
                writer.Write(moduleName);
                ms.Position = textSegment.FileOffset;
                writer.Write(textData);
                ms.Position = roSegment.FileOffset;
                writer.Write(roData);
                ms.Position = dataSegment.FileOffset;
                writer.Write(dataData);

                writer.Flush();
                return ms.ToArray();
            }
        }

        private static byte[] GetSHA256(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(data);
            }
        }

        private static byte[] GetCompressed(byte[] data)
        {
            var buffer = new byte[data.Length];
            var written = LZ4Codec.Encode(data, buffer);

            if (written < 0)
                throw new Exception("NSO: buffer too small");

            if (written == buffer.Length)
                return buffer;
            else
            {
                var returnBuffer = new byte[written];
                Array.Copy(buffer, returnBuffer, written);
                return returnBuffer;
            }
        }
    }
}