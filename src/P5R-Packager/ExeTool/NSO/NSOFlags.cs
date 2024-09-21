using System;

namespace P5R_Packager.ExeTool.NSO
{
    [Flags]
    public enum NSOFlags : uint
    {
        None = 0,
        TextCompressed = 1,
        RoCompressed = 2,
        DataCompressed = 4,
        TextHash = 8,
        RoHash = 16,
        DataHash = 32,
    }
}