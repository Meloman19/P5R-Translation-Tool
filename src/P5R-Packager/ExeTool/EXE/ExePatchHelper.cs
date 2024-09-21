using AsmResolver.PE.File;
using P5R_Packager.ExeTool.NSO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace P5R_Packager.ExeTool.EXE
{
    internal static class ExePatchHelper
    {
        public static (long, byte[])[] GetPatch(byte[] origin, byte[] patched, PEFile peFile)
        {
            var diffs = IPSHelper.GetDiff(origin, patched).ToList();

            var patch = new List<(long, byte[])>();

            foreach (var section in peFile.Sections)
            {
                var dataOffset = (long)section.Offset;
                var dataSize = section.Contents.GetPhysicalSize();
                var dataHighOffset = dataOffset + dataSize;

                var sectionDiffs = diffs.FindAll(x =>
                {
                    if (x.offset < dataOffset)
                        return false;

                    if (x.offset + x.size > dataHighOffset)
                        return false;

                    return true;
                }).ToArray();

                if (sectionDiffs.Length == 0)
                    continue;

                var sectionBase = (long)peFile.OptionalHeader.ImageBase + section.Rva;

                foreach (var diff in sectionDiffs)
                {
                    var baseOffset = sectionBase + (diff.offset - dataOffset);
                    var data = patched.AsSpan().Slice(diff.offset, diff.size).ToArray();
                    patch.Add((baseOffset, data));
                }
            }

            return patch.ToArray();
        }
    }
}
