namespace P5R_Packager.ExeTool.NSO
{
    internal static class NSOExt
    {
        public static NSOSegment GetSegmentByName(this NSOFile nso, string section)
        {
            switch (section)
            {
                case ".data":
                    return nso.Data;
                case ".rodata":
                    return nso.Ro;
                case ".text":
                    return nso.Text;
                default:
                    return null;
            }
        }
    }
}