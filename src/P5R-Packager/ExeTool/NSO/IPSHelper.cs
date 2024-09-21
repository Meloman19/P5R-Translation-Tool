using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace P5R_Packager.ExeTool.NSO
{
    public static class IPSHelper
    {
        public static IPS32 Diff2Files(string origin, string patched)
        {
            {
                var originFI = new FileInfo(origin);
                var patchFI = new FileInfo(patched);

                if (!originFI.Exists || !patchFI.Exists)
                    throw new Exception();

                if (originFI.Length != patchFI.Length)
                    throw new Exception();

                if (originFI.Length > IPS32.MAX_FILE_SIZE)
                    throw new Exception();
            }

            var originData = File.ReadAllBytes(origin);
            var patchedData = File.ReadAllBytes(patched);
            return Diff2Files(originData, patchedData);
        }

        public static IPS32 Diff2Files(byte[] originData, byte[] patchedData, int startOffset = 0)
        {
            var diffs = GetDiff(originData, patchedData, startOffset).ToArray();

            var ips = new IPS32();

            foreach (var diff in diffs)
            {
                var offset = Convert.ToUInt32(diff.offset);
                var length = diff.size;

                if (offset == IPS32.EOF)
                {
                    offset--;
                    length++;
                }

                while (length > 0)
                {
                    var chunkSize = Convert.ToUInt16(Math.Min(IPS32.MAX_RECORD_SIZE, length));

                    var data = new byte[chunkSize];
                    Array.Copy(patchedData, offset, data, 0, chunkSize);

                    ips.Records.Add(new IPS32Record(offset, data));
                    offset += chunkSize;
                    length -= chunkSize;
                }
            }

            return ips;
        }

        public static IEnumerable<(int offset, int size)> GetDiff(byte[] origin, byte[] patched, int startOffset = 0)
        {
            if (origin.Length != patched.Length)
                throw new Exception();

            var offset = -1;
            var size = 0;
            for (int i = startOffset; i < origin.Length; i++)
            {
                if (size == 0)
                {
                    if (origin[i] != patched[i])
                    {
                        offset = i;
                        size++;
                    }
                }
                else
                {
                    if (origin[i] != patched[i])
                    {
                        size++;
                    }
                    else
                    {
                        yield return (offset, size);
                        offset = -1;
                        size = 0;
                    }
                }
            }

            if (size != 0)
            {
                yield return (offset, size);
            }
        }
    }
}