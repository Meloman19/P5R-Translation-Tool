using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuxiliaryLibraries.IO;

namespace P5R_Packager.DataTool.TableImport
{
    public sealed class MTD
    {
        private const uint Magic1 = 0x00010000;
        private const uint Magic2 = 0x77544430;
        private const uint Magic3 = 0x00000001;
        private const int HeaderSize = 0x20;

        private bool _endHeader = false;

        public MTD(byte[] data)
        {
            Read(data);
        }

        private void Read(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReaderEndian(ms))
            {
                var fullSize = ReadFileHeader(reader);

                var headerSize = reader.ReadUInt32();
                if (headerSize != HeaderSize)
                    throw new Exception("MTD: headerSize");

                var unknown = reader.ReadBytes(0x10);
                if (unknown.Any(x => x != 0))
                    throw new Exception("MTD: unknown");

                var dataSize = reader.ReadUInt32();
                if (fullSize - dataSize != 0x30)
                    throw new Exception("MTD: delta size");

                var dataBlockCount = reader.ReadUInt32();

                var unknown2 = reader.ReadUInt32();
                if (unknown2 != 0)
                    throw new Exception("MTD: unknown2");

                if (dataSize % dataBlockCount != 0)
                    throw new Exception("MTD: block size");

                var dataBlockSize = dataSize / dataBlockCount;
                for (int i = 0; i < dataBlockCount; i++)
                {
                    var blockData = reader.ReadBytes((int)dataBlockSize);
                    Entities.Add(blockData);
                }

                if (ms.Position == ms.Length)
                    return;

                _endHeader = true;
                var endHeaderSize = ReadFileHeader(reader);
                if (fullSize != endHeaderSize)
                    throw new Exception("MDT: end wrong size");

                if (ms.Position != ms.Length)
                    throw new Exception("MDT: file not ended");
            }
        }

        private uint ReadFileHeader(BinaryReader reader)
        {
            var magic1 = reader.ReadUInt32();
            if (magic1 != Magic1)
                throw new Exception("MTD: magic1");

            var magic2 = reader.ReadUInt32();
            if (magic2 != Magic2)
                throw new Exception("MTD: magic2");

            var fullSize = reader.ReadUInt32();
            var magic3 = reader.ReadUInt32();
            if (magic3 != Magic3)
                throw new Exception("MTD: magic3");

            return fullSize;
        }

        public List<byte[]> Entities { get; } = new List<byte[]>();

        public byte[] GetData()
        {
            var dataBlockSize = Entities[0].Length;
            var dataBlockCount = Entities.Count;
            var dataSize = dataBlockSize * dataBlockCount;
            var fullDataSize = dataSize + 0x30;

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriterEndian(ms))
            {
                WriteFileHeader(writer, (uint)fullDataSize);

                writer.Write((uint)HeaderSize);
                writer.Write((uint)0);
                writer.Write((uint)0);
                writer.Write((uint)0);

                writer.Write((uint)0);
                writer.Write((uint)dataSize);
                writer.Write((uint)dataBlockCount);
                writer.Write((uint)0);

                foreach (var entity in Entities)
                {
                    if (entity.Length != dataBlockSize)
                        throw new Exception("MTD: entity size");

                    writer.Write(entity);
                }

                if (_endHeader)
                    WriteFileHeader(writer, (uint)fullDataSize);

                return ms.ToArray();
            }
        }

        private static void WriteFileHeader(BinaryWriter writer, uint fullSize)
        {
            writer.Write((uint)Magic1);
            writer.Write((uint)Magic2);
            writer.Write((uint)fullSize);
            writer.Write((uint)Magic3);
        }
    }
}