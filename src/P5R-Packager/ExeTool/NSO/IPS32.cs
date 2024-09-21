using AuxiliaryLibraries.IO;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace P5R_Packager.ExeTool.NSO
{
    public sealed class IPS32
    {
        public const uint MAX_FILE_SIZE = 0xFFFFFFFF;
        public const uint MAX_RECORD_SIZE = 0xFFFF;
        public const uint EOF = 0x45454F46;

        private readonly static byte[] IPSMagic = new byte[] { 0x49, 0x50, 0x53, 0x33, 0x32 };
        private readonly static byte[] IPSEnd = new byte[] { 0x45, 0x45, 0x4F, 0x46 };

        public List<IPS32Record> Records { get; } = new List<IPS32Record>();

        public void WriteToFile(string filename)
        {
            using (var fs = File.Create(filename))
                WriteToStream(fs);
        }

        public void WriteToStream(Stream stream)
        {
            using (var writer = new BinaryWriterEndian(stream, Encoding.UTF8, true))
            {
                writer.Write(IPSMagic);
                foreach (var record in Records)
                {
                    writer.Write(record.Offset);
                    writer.Write(record.Length);
                    writer.Write(record.Data);
                }
                writer.Write(IPSEnd);
            }
        }
    }
}