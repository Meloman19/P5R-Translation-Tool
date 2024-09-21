using System;

namespace P5R_Packager.ExeTool.NSO
{
    public sealed class IPS32Record
    {
        public IPS32Record(uint offset, byte[] changedData)
        {
            if (offset == IPS32.EOF)
                throw new ArgumentException();

            if (changedData.Length > IPS32.MAX_RECORD_SIZE)
                throw new ArgumentException();

            Offset = offset;
            Length = Convert.ToUInt16(changedData.Length);
            Data = changedData;
        }

        public uint Offset { get; }

        public ushort Length { get; }

        public byte[] Data { get; }
    }
}