using Newtonsoft.Json;
using System;
using System.Linq;

namespace P5R_Packager.ExeTool.EXE
{
    internal class SimpleData
    {
        public string Comment { get; set; }

        public string Section { get; set; }

        public string Offset { get; set; }

        public string Size { get; set; }

        [JsonIgnore]
        public long OffsetInt => Convert.ToInt64(Offset, 16);

        [JsonIgnore]
        public long SizeInt => Convert.ToInt64(Size, 16);
    }

    internal class ExePointerDataModel
    {
        public string Comment { get; set; }

        public string PtrSection { get; set; }

        public string PtrDataSection { get; set; }

        public string PtrOffset { get; set; }

        public string PtrSize { get; set; }

        [JsonIgnore]
        public int PtrOffsetInt => Convert.ToInt32(PtrOffset, 16);

        [JsonIgnore]
        public int PtrSizeInt => Convert.ToInt32(PtrSize, 16);
    }

    internal class MovieSubBlock
    {
        public string Comment { get; set; }

        public string[] Offsets { get; set; }

        [JsonIgnore]
        public long[] LongOffsets => Offsets.Select(x => Convert.ToInt64(x, 16)).ToArray();
    }

    internal class ExeDataModel
    {
        public string OtherListName { get; set; }

        public SimpleData PCMovieData { get; set; }

        public SimpleData ChatData { get; set; }

        public ExePointerDataModel[] PtrArray { get; set; }

        public MovieSubBlock[] MovieSubBlocks { get; set; }
    }
}