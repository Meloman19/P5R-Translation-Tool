namespace P5R_Packager.ExeTool.NSO
{
    public class NSOSegment
    {
        private uint _memoryOffset;
        private byte[] _data;

        public NSOSegment(uint memoryOffset, byte[] data, string name)
        {
            _memoryOffset = memoryOffset;
            _data = data;
            Name = name;
        }

        public uint MemoryOffset => _memoryOffset;

        public byte[] Data => _data;

        public string Name { get; }
    }
}