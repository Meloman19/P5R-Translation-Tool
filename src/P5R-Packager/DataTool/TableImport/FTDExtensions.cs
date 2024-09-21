using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AuxiliaryLibraries.Extensions;
using P5R_Packager.Common;
using PersonaEditorLib.Other;

namespace P5R_Packager.DataTool.TableImport
{
    internal static class FTDExtensions
    {
        public static void ImportText_1Entry(this FTD ftd, Encoding oldEncoding, Encoding newEncoding, Dictionary<int, string> translate, int leftPadding = 0, int rightPadding = 0)
        {
            var oneEntry = ftd.Entries.Single();

            for (int i = 0; i < oneEntry.Length; i++)
            {
                var entry = oneEntry[i];

                byte[] oldArray = entry.SubArray(leftPadding, entry.Length - rightPadding - leftPadding);
                if (Encoding.ASCII.GetString(oldArray).Trim('\0') == "NULL" || oldArray.Length <= 1)
                    continue;

                string newText;
                if (translate.TryGetValue(i, out var text) && !string.IsNullOrEmpty(text))
                    newText = text;
                else
                    newText = oldEncoding.GetString(oldArray).TrimEnd('\0');

                byte[] buffer = SomeHelpers.TrimLength(newText, newEncoding, entry.Length - leftPadding - rightPadding - 1);

                for (int k = leftPadding; k < entry.Length - rightPadding; k++)
                    entry[k] = 0;

                if (buffer.Length >= entry.Length - leftPadding - rightPadding)
                    Buffer.BlockCopy(buffer, 0, entry, leftPadding, entry.Length - leftPadding - rightPadding - 1);
                else
                    Buffer.BlockCopy(buffer, 0, entry, leftPadding, buffer.Length);
            }
        }

        public static void ImportText_1EntryUTF8(this FTD ftd, Encoding oldEncoding, Encoding newEncoding, Dictionary<int, string> translate, int leftPadding = 0, int rightPadding = 0)
        {
            var oneEntry = ftd.Entries.Single();

            for (int i = 0; i < oneEntry.Length; i++)
            {
                var entry = oneEntry[i];

                byte[] oldArray = entry.SubArray(leftPadding, entry.Length - rightPadding - leftPadding);
                if (Encoding.ASCII.GetString(oldArray).Trim('\0') == "NULL" || oldArray.Length <= 1)
                    continue;

                string newText;
                if (translate.TryGetValue(i, out var text) && !string.IsNullOrEmpty(text))
                    newText = text;
                else
                    newText = oldEncoding.GetString(oldArray).TrimEnd('\0');

                newText = oldEncoding.GetString(newEncoding.GetBytes(newText));

                byte[] buffer = SomeHelpers.TrimLength(newText, Encoding.UTF8, entry.Length - leftPadding - rightPadding - 1);

                for (int k = leftPadding; k < entry.Length - rightPadding; k++)
                    entry[k] = 0;

                if (buffer.Length >= entry.Length - leftPadding - rightPadding)
                    Buffer.BlockCopy(buffer, 0, entry, leftPadding, entry.Length - leftPadding - rightPadding - 1);
                else
                    Buffer.BlockCopy(buffer, 0, entry, leftPadding, buffer.Length);
            }
        }

        public static void ImportText_MultiEntry(this FTD ftd, Encoding oldEncoding, Encoding newEncoding, Dictionary<int, string> translate)
        {
            for (int i = 0; i < ftd.Entries.Count; i++)
            {
                var entry = ftd.Entries[i].Single();

                if (Encoding.ASCII.GetString(entry).Trim('\0') == "NULL" || entry.Length <= 1)
                    continue;

                string newText;
                if (translate.TryGetValue(i, out var text) && !string.IsNullOrEmpty(text))
                    newText = text;
                else
                    newText = oldEncoding.GetString(entry).TrimEnd('\0');

                ftd.Entries[i][0] = newEncoding.GetBytes(newText).Concat(new byte[] { 0 }).ToArray();
            }
        }

        public static void Import_cmmPC_PARAM_Help(this FTD ftd, Encoding oldEncoding, Encoding newEncoding, Dictionary<int, string> translate)
        {
            var index = 0;

            for (int entryIndex = 0; entryIndex < 5; entryIndex++)
            {
                IEnumerable<byte> entryEnum = Enumerable.Empty<byte>();

                for (int i = 0; i < 5; i++)
                {
                    string newText;
                    if (translate.TryGetValue(index, out var text) && !string.IsNullOrEmpty(text))
                        newText = text;
                    else
                    {
                        var sub = ftd.Entries[0][entryIndex].SubArray(i * 20, 20);
                        newText = oldEncoding.GetString(sub).TrimEnd('\0');
                    }

                    var data = SomeHelpers.TrimLength(newText, newEncoding, 19);

                    entryEnum = entryEnum.Concat(data);

                    var add = 20 - data.Length;

                    entryEnum = entryEnum.Concat(Enumerable.Repeat((byte)0, add));

                    index++;
                }

                ftd.Entries[0][entryIndex] = entryEnum.ToArray();
            }
        }

        public static void ImportText_fldPanelMsg(this FTD ftd, Encoding oldEncoding, Encoding newEncoding, Dictionary<int, string> translate)
        {
            var index = 0;
            foreach (var entry in ftd.Entries[0])
            {
                using (var MS = new MemoryStream(entry))
                {
                    while (true)
                    {
                        var buffer = new byte[40];
                        var curPos = MS.Position;
                        var readed = MS.Read(buffer, 0, 40);

                        if (readed == 40)
                        {
                            if (!Encoding.ASCII.GetString(buffer).StartsWith("NULL"))
                            {
                                string newText;
                                if (translate.TryGetValue(index, out var trans) && !string.IsNullOrEmpty(trans))
                                    newText = trans;
                                else
                                    newText = oldEncoding.GetString(buffer).TrimEnd('\0');

                                MS.Position = curPos;

                                for (int i = 0; i < 0x20; i++)
                                {
                                    MS.WriteByte(0);
                                }

                                MS.Position = curPos;

                                var data = SomeHelpers.TrimLength(newText, newEncoding, 0x20 - 1);
                                for (int i = 0; i < data.Length; i++)
                                {
                                    MS.WriteByte(data[i]);
                                }
                            }

                            MS.Position = curPos + 56;
                            index++;
                        }
                        else break;
                    }
                }
            }
        }

        public static void ImportText_fldLMapStation(this FTD ftd, Encoding oldEncoding, Encoding newEncoding, Dictionary<int, string> translate)
        {
            void WriteStringTo(MemoryStream ms, string str, int pos, int size)
            {
                if (string.IsNullOrEmpty(str))
                {
                    str = " ";
                }

                ms.Position = pos;
                for (int i = 0; i < size; i++)
                {
                    ms.WriteByte(0);
                }
                ms.Position = pos;
                var data = SomeHelpers.TrimLength(str, newEncoding, size - 1);
                for (int i = 0; i < data.Length; i++)
                {
                    ms.WriteByte(data[i]);
                }
            }

            string ReadTranslateOrString(MemoryStream ms, int ind, int pos, int size)
            {
                if (translate.TryGetValue(ind, out string str) && !string.IsNullOrEmpty(str))
                {
                    return str;
                }

                ms.Position = pos;
                var data = new byte[size];
                ms.Read(data, 0, data.Length);

                return oldEncoding.GetString(data).TrimEnd('\0');
            }

            var nullBytes = new byte[] { 0x80, 0xAE, 0x80, 0xB5, 0x80, 0xAC, 0x80, 0xAC };

            var index = 0;
            foreach (var entry in ftd.Entries[0])
            {
                using (var MS = new MemoryStream(entry))
                {
                    var nullData = new byte[8];
                    MS.Read(nullData, 0, 8);
                    if (nullData.SequenceEqual(nullBytes))
                    {
                        index += 8;
                        continue;
                    }

                    // title
                    var newText = ReadTranslateOrString(MS, index, 0, 0x20);
                    WriteStringTo(MS, newText, 0, 0x20);

                    // about1
                    newText = ReadTranslateOrString(MS, index + 1, 0x30, 0x40);
                    WriteStringTo(MS, newText, 0x30, 0x40);

                    // about2
                    newText = ReadTranslateOrString(MS, index + 2, 0x70, 0x40);
                    WriteStringTo(MS, newText, 0x70, 0x40);

                    // add1
                    newText = ReadTranslateOrString(MS, index + 3, 0xB0, 0x30);
                    WriteStringTo(MS, newText, 0xB0, 0x30);

                    // add2
                    newText = ReadTranslateOrString(MS, index + 4, 0xE0, 0x30);
                    WriteStringTo(MS, newText, 0xE0, 0x30);

                    // add3
                    newText = ReadTranslateOrString(MS, index + 5, 0x110, 0x30);
                    WriteStringTo(MS, newText, 0x110, 0x30);

                    // add4
                    newText = ReadTranslateOrString(MS, index + 6, 0x140, 0x30);
                    WriteStringTo(MS, newText, 0x140, 0x30);

                    // add5
                    newText = ReadTranslateOrString(MS, index + 7, 0x170, 0x30);
                    WriteStringTo(MS, newText, 0x170, 0x30);

                    index += 8;
                }
            }
        }
    }
}
