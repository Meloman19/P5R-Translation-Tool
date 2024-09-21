using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using P5R_Packager.Common;
using PersonaEditorLib;
using PersonaEditorLib.Text;

namespace P5R_Packager.ExeTool.EXE
{
    internal sealed class BMDImporter
    {
        private const string TextDir = "TEXT";
        private const string ExeZip = "P5R_exe.zip";
        private const string TargetList = "BMD";

        private readonly PersonaEncoding _oldEncoding;
        private readonly PersonaEncoding _newEncoding;
        private readonly PersonaFont _newFont;

        private Dictionary<string, Dictionary<(int, int), string>> _bmdTextTranslate;

        public BMDImporter(string translatePath, PersonaEncoding oldEncoding, PersonaEncoding newEncoding, PersonaFont newFont)
        {
            _oldEncoding = oldEncoding;
            _newEncoding = newEncoding;
            _newFont = newFont;

            ReadTextTranslate(translatePath);
        }

        private void ReadTextTranslate(string translatePath)
        {
            var textZipPath = Path.Combine(translatePath, TextDir, ExeZip);

            var textTranslate = new Dictionary<string, Dictionary<(int, int), string>>();

            using (var zip = ZipFile.OpenRead(textZipPath))
            {
                foreach (var entry in zip.Entries)
                {
                    var sheetName = Path.GetFileNameWithoutExtension(entry.Name);

                    if (sheetName != TargetList)
                        continue;

                    string[] lines;
                    using (var entryStream = entry.Open())
                        lines = SomeHelpers.ReadAllLines(entryStream);

                    if (lines.Length < 2)
                        continue;

                    int fileNameInd;
                    int id1Ind;
                    int id2Ind;
                    int translInd;
                    {
                        var titleSpl = lines[0].Split('\t');
                        fileNameInd = Array.IndexOf(titleSpl, "FILE_NAME");
                        id1Ind = Array.IndexOf(titleSpl, "ID1");
                        id2Ind = Array.IndexOf(titleSpl, "ID2");
                        translInd = Array.IndexOf(titleSpl, "TRANSL");
                    }

                    if (fileNameInd == -1 || id1Ind == -1 || id2Ind == -1 || translInd == -1)
                        continue;

                    foreach (var line in lines.Skip(1))
                    {
                        var spl = line.Split('\t');
                        var fileName = spl[fileNameInd];
                        var ind1 = int.Parse(spl[id1Ind]);
                        var ind2 = int.Parse(spl[id2Ind]);
                        var translate = spl[translInd];

                        if (string.IsNullOrEmpty(translate))
                            continue;

                        if (!textTranslate.TryGetValue(fileName, out var file))
                        {
                            file = new Dictionary<(int, int), string>();
                            textTranslate[fileName] = file;
                        }

                        file[(ind1, ind2)] = translate;
                    }
                }
            }

            _bmdTextTranslate = textTranslate;
        }

        public void Import(Stream stream)
        {
            var bmds = FindAllBMD(stream);

            var newCharWidth = _newFont.GetCharWidth(_newEncoding);
            foreach (var bmdD in bmds)
            {
                if (!_bmdTextTranslate.TryGetValue(bmdD.name, out var translate))
                    translate = new Dictionary<(int, int), string>();

                var bmd = bmdD.bmd;
                for (int msgInd = 0; msgInd < bmd.Msg.Count; msgInd++)
                {
                    var msgData = bmd.Msg[msgInd];

                    for (int strInd = 0; strInd < msgData.MsgStrings.Length; strInd++)
                    {
                        var strData = msgData.MsgStrings[strInd];

                        var split = new MSGSplitter(strData, strInd + 1 == msgData.MsgStrings.Length);
                        if (translate.TryGetValue((msgInd, strInd), out var newStr))
                        {
                            split.ChangeBody(newStr, _newEncoding, newCharWidth);
                        }
                        else
                        {
                            split.ChangeEncoding(_oldEncoding, _newEncoding);
                        }

                        msgData.MsgStrings[strInd] = split.GetData();
                    }
                }

                var newBmdData = bmd.GetData();
                if (newBmdData.Length > bmdD.size)
                    throw new Exception($"File {bmdD.name}: the allowed length has been exceeded. Available {bmdD.size}, obtain {newBmdData.Length}");

                stream.Seek(bmdD.pos, SeekOrigin.Begin);
                for (int i = 0; i < bmdD.size; i++)
                    stream.WriteByte(0);

                stream.Seek(bmdD.pos, SeekOrigin.Begin);
                stream.Write(newBmdData, 0, newBmdData.Length);
            }
        }

        private (long pos, int size, BMD bmd, string name)[] FindAllBMD(Stream stream)
        {
            var bmdPos = FindAllBMDPosition(stream);

            var result = new List<(long, int, BMD, string)>();

            foreach (var bmdP in bmdPos)
            {
                stream.Seek(bmdP.pos, SeekOrigin.Begin);

                try
                {
                    var size = bmdP.size;
                    var buffer = new byte[size];
                    stream.Read(buffer, 0, size);

                    var bmd = new BMD(buffer);

                    if (TryGetName(bmd, _oldEncoding, out var name))
                    {
                        result.Add((bmdP.pos, bmdP.size, bmd, name));
                    }
                }
                catch { }
            }

            return result.ToArray();
        }

        private static bool TryGetName(BMD bmd, Encoding encoding, out string name)
        {
            var firstMSGName = bmd.Msg[0].Name;
            var firstMSGData = bmd.Msg[0].MsgStrings[0].GetTextBases().ToArray();
            {
                if (!firstMSGData[0].Data.SequenceEqual(new byte[] { 0xF2, 0x05, 0xFF, 0xFF }) ||
                    !firstMSGData[1].Data.SequenceEqual(new byte[] { 0xF1, 0x41 }))
                {
                    name = null;
                    return false;
                }

                firstMSGData = firstMSGData.Skip(2).ToArray();
            }

            switch (firstMSGName)
            {
                case "btl_retry_00":
                    {
                        name = "BMD_1";
                        var block = firstMSGData[0];
                        if (!block.IsText)
                        {
                            return false;
                        }
                        var text = encoding.GetString(block.Data);
                        if (text != "You can start over if you would")
                            return false;

                        var lastMSGData = bmd.Msg.Last().MsgStrings[0].GetTextBases().ToArray();
                        {
                            if (!lastMSGData[0].Data.SequenceEqual(new byte[] { 0xF2, 0x05, 0xFF, 0xFF }) ||
                                !lastMSGData[1].Data.SequenceEqual(new byte[] { 0xF1, 0x41 }))
                            {
                                name = null;
                                return false;
                            }

                            lastMSGData = lastMSGData.Skip(2).ToArray();
                        }

                        block = lastMSGData[0];
                        if (block.IsText)
                        {
                            text = encoding.GetString(block.Data);
                            if (text == "Another band of thieves")
                                return true;
                            else
                                return false;
                        }
                        else
                        {
                            if (block.Data.SequenceEqual(new byte[] { 0xF2, 0x44, 0x02, 0x01 }))
                            {
                                name = "BMD_2";
                                return true;
                            }
                            else
                                return false;
                        }
                    }
                case "btl_pget_00":
                    {
                        name = "BMD_3";
                        var block = firstMSGData[0];
                        if (!block.IsText)
                        {
                            return false;
                        }
                        var text = encoding.GetString(block.Data);
                        if (text == "Your Persona stock is currently")
                            return true;
                        else
                            return false;
                    }
                case "MSG_ERROR":
                    {
                        name = "BMD_4";
                        var block = firstMSGData[0];
                        if (!block.IsText)
                        {
                            return false;
                        }
                        var text = encoding.GetString(block.Data);
                        if (text == "Selected data contains downloadable content you do not own,")
                            return true;
                        else
                            return false;
                    }
                case "btl_support_03_001":
                    {
                        name = "BMD_5";
                        var block = firstMSGData[0];
                        if (!block.Data.SequenceEqual(new byte[] { 0xF7, 0x61, 0x09, 0x01, 0x02, 0x01, 0x00, 0x00, 0x02, 0x01, 0x01, 0x01, 0x01, 0x01 }))
                            return false;

                        block = firstMSGData[1];
                        if (!block.IsText)
                        {
                            return false;
                        }
                        var text = encoding.GetString(block.Data);
                        if (text == "We're surrounded! Three enemies!")
                            return true;
                        else
                            return false;
                    }
                case "btl_support_08_001":
                    {
                        name = "BMD_6";
                        var block = firstMSGData[0];
                        if (!block.Data.SequenceEqual(new byte[] { 0xF7, 0x61, 0x09, 0x01, 0x02, 0x01, 0x00, 0x00, 0x02, 0x01, 0x01, 0x01, 0x01, 0x01 }))
                            return false;

                        block = firstMSGData[1];
                        if (!block.IsText)
                        {
                            return false;
                        }
                        var text = encoding.GetString(block.Data);
                        if (text == "You're surrounded! Three enemies!")
                            return true;
                        else
                            return false;
                    }
                case "btl_support_09_001":
                    {
                        name = "BMD_7";
                        var block = firstMSGData[0];
                        if (!block.Data.SequenceEqual(new byte[] { 0xF7, 0x61, 0x09, 0x01, 0x02, 0x01, 0x00, 0x00, 0x02, 0x01, 0x01, 0x01, 0x01, 0x01 }))
                            return false;

                        block = firstMSGData[1];
                        if (!block.IsText)
                        {
                            return false;
                        }
                        var text = encoding.GetString(block.Data);
                        if (text == "Damn, surrounded by three")
                            return true;
                        else
                            return false;
                    }
                default:
                    name = null;
                    return false;
            }
        }

        private static (long pos, int size)[] FindAllBMDPosition(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var searchPatter = new byte[] { 0x31, 0x47, 0x53, 0x4D };

            var patternPositions = SomeHelpers.FastFindArray(stream, searchPatter).ToArray();

            var buffer = new byte[4];
            return patternPositions.Select(x =>
            {
                stream.Seek(x - 4, SeekOrigin.Begin);
                stream.Read(buffer, 0, 4);

                var pos = stream.Position - 8;
                Array.Reverse(buffer);
                var size = BitConverter.ToInt32(buffer, 0);

                return (pos, size);
            }).ToArray();
        }
    }
}