using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AuxiliaryLibraries.Tools;

namespace P5R_Packager.Common
{
    internal static class SomeHelpers
    {
        public static string ReadResource(string name)
        {
            using Stream stream = typeof(SomeHelpers).Assembly.GetManifestResourceStream($"P5R_Packager.TranslationData.{name}");
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static byte[] ReadResourceData(string name)
        {
            using Stream stream = typeof(SomeHelpers).Assembly.GetManifestResourceStream($"P5R_Packager.TranslationData.{name}");

            var buffer = new byte[stream.Length];
            stream.Read(buffer);
            return buffer;
        }

        public static byte[] TrimLength(string value, Encoding encoding, int maxLength)
        {
            byte[] buffer;
            while (true)
            {
                buffer = encoding.GetBytes(value);

                if (buffer.Length <= maxLength)
                    break;
                else
                {
                    value = value.Substring(0, value.Length - 1);
                }
            }

            return buffer;
        }

        public static byte[] TrimLengthSys(List<string> value, Encoding encoding, int maxLength)
        {
            if (maxLength < 1)
                throw new ArgumentOutOfRangeException(nameof(maxLength));

            var splitted = value.ToList();

            byte[] buffer;
            while (true)
            {
                buffer = splitted.SelectMany(x =>
                {
                    if (x.StartsWith('{') &&
                        x.EndsWith('}') &&
                        StringTool.TryParseArray(x.Substring(1, x.Length - 2), out var sys))
                    {
                        return sys;
                    }
                    else
                    {
                        return encoding.GetBytes(x);
                    }
                }).ToArray();

                if (buffer.Length <= maxLength)
                    break;
                else
                {
                    var lastIndex = splitted.Count - 1;
                    var last = splitted[lastIndex];
                    if (last.StartsWith('{') &&
                        last.EndsWith('}') &&
                        StringTool.TryParseArray(last.Substring(1, last.Length - 2), out var sys))
                    {
                        splitted.RemoveAt(splitted.Count - 1);
                    }
                    else if (last.Length != 0)
                    {
                        splitted[lastIndex] = last.Substring(0, last.Length - 1);
                    }
                    else
                    {
                        splitted.RemoveAt(lastIndex);
                    }
                }
            }

            return buffer;
        }

        public static string[] ReadAllLines(Stream stream)
        {
            var list = new List<string>();

            using (var sr = new StreamReader(stream))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    list.Add(line);
                }
            }

            return list.ToArray();
        }

        public static IEnumerable<long> FastFindArray(Stream stream, long from, long size, byte[] array)
        {
            stream.Seek(from, SeekOrigin.Begin);

            if (array.Length > stream.Length)
                yield break;
            else
            {
                int arrayOffset = 0, currentByte;
                int readed = 0;

                while ((currentByte = stream.ReadByte()) != -1)
                {
                    if (readed >= size)
                        break;
                    readed++;
                Label:
                    if (currentByte == array[arrayOffset])
                    {
                        if (arrayOffset + 1 == array.Length)
                        {
                            yield return stream.Position - array.Length;
                            arrayOffset = 0;
                            continue;
                        }

                        arrayOffset++;
                    }
                    else if (arrayOffset != 0)
                    {
                        arrayOffset = Index(array, 1, arrayOffset);
                        goto Label;
                    }
                }
            }
        }

        public static IEnumerable<long> FastFindArray(Stream stream, byte[] array)
        {
            stream.Seek(0, SeekOrigin.Begin);

            if (array.Length > stream.Length)
                yield break;
            else
            {
                int arrayOffset = 0, currentByte;

                while ((currentByte = stream.ReadByte()) != -1)
                {
                Label:
                    if (currentByte == array[arrayOffset])
                    {
                        if (arrayOffset + 1 == array.Length)
                        {
                            yield return stream.Position - array.Length;
                            arrayOffset = 0;
                            continue;
                        }

                        arrayOffset++;
                    }
                    else if (arrayOffset != 0)
                    {
                        arrayOffset = Index(array, 1, arrayOffset);
                        goto Label;
                    }
                }
            }
        }

        private static int Index(byte[] array, int start, int end)
        {
            if (start >= end)
                return 0;
            else
            {
                int arrayOffset = 0;

                bool fail = false;
                for (int i = start; i < end; i++, arrayOffset++)
                {
                    if (array[i] != array[arrayOffset])
                    {
                        fail = true;
                        break;
                    }
                }

                if (fail)
                    return Index(array, start + 1, end);
                else
                    return arrayOffset;
            }
        }
    }
}
