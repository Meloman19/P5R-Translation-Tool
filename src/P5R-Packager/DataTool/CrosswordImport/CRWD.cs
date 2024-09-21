using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonaEditorLib;
using PersonaEditorLib.FileContainer;
using PersonaEditorLib.Other;
using PersonaEditorLib.Text;

namespace P5R_Packager.DataTool.CrosswordImport
{
    public class CRWD
    {
        public static readonly string NotUsedS = "[XLOC_NOTUSED]";
        public static readonly byte[] NotUsed = new byte[] { 0x5B, 0x58, 0x4C, 0x4F, 0x43, 0x5F, 0x4E, 0x4F, 0x54, 0x55, 0x53, 0x45, 0x44, 0x5D };

        private static readonly byte[] BMDCode1 = new byte[] { 0xF2, 0x05, 0xFF, 0xFF };
        private static readonly byte[] BMDCode2 = new byte[] { 0xF1, 0x41 };
        private static readonly byte[] BMDCodeN = new byte[] { 0x00 };
        private static readonly byte[] BMDCodeN1 = new byte[] { 0xF1, 0x21 };
        private static readonly byte[] BMDCodeN2 = new byte[] { 0x0A };

        private BIN pak;

        public CRWD(BIN pak, string fileName)
        {
            this.pak = pak;
            Parse(pak, fileName);
        }

        public CRWD(string path)
        {
            Parse(path);
        }

        private void Parse(string path)
        {
            try
            {
                pak = new BIN(path);
            }
            catch
            {
                throw new Exception("Target file not BIN");
            }

            var fileName = Path.GetFileName(path);
            Parse(pak, fileName);
        }

        private void Parse(BIN pak, string fileName)
        {
            if (pak.SubFiles.Count != 2)
                throw new Exception("BIN contain more or less than 2 files");

            var fileNameWE = Path.GetFileNameWithoutExtension(fileName).ToLower();
            Name = fileNameWE;

            var bmdGF = pak.SubFiles.Find(x => x.GameData.Type == FormatEnum.BMD);
            if (bmdGF == null)
                throw new Exception("Not founded BMD");
            if (bmdGF.Name != $"data/{fileNameWE}.bmd ")
                throw new Exception("Incorrect BMD name");

            ParseBMD(bmdGF.GameData as BMD);

            var datGF = pak.SubFiles.Find(x => x.GameData.Type == FormatEnum.DAT);
            if (datGF == null)
                throw new Exception("Not founded DAT");
            if (datGF.Name != $"data/{fileNameWE}.dat ")
                throw new Exception("Incorrect DAT name");

            Field = new CRWDField(datGF.GameData as DAT);
        }

        private void ParseBMD(BMD bmd)
        {
            if (bmd.Name.Count > 0)
            {
                if (bmd.Name.Count != 1 || !bmd.Name[0].NameBytes.SequenceEqual(NotUsed))
                {
                    throw new Exception("BMD containt names?!");
                }
            }

            var msgNameTemplate = "CROSSWORD_00_TEXT_";
            for (int i = 0; i < bmd.Msg.Count; i++)
            {
                var msg = bmd.Msg[i];

                var msgName = msgNameTemplate + i.ToString().PadLeft(2, '0');
                if (msg.Name != msgName)
                    throw new Exception($"BMD line have incorrect name: \"{msg.Name}\"|\"{msgName}\"");

                if (msg.MsgStrings.Length != 1)
                    throw new Exception($"BMD line ({i}) have more than 1 strings");

                var data = msg.MsgStrings[0];
                var splittedData = data.GetTextBases().ToList();

                {
                    var code1 = splittedData.First();
                    if (!code1.Data.SequenceEqual(BMDCode1))
                        throw new Exception("BMD line: wrong code1");
                    splittedData.Remove(code1);

                    var code2 = splittedData.First();
                    if (!code2.Data.SequenceEqual(BMDCode2))
                        throw new Exception("BMD line: wrong code2");
                    splittedData.Remove(code2);

                    var codeN = splittedData.Last();
                    if (!codeN.Data.SequenceEqual(BMDCodeN))
                        throw new Exception("BMD line: wrong codeN");
                    splittedData.Remove(codeN);

                    var codeN1 = splittedData.Last();
                    if (!codeN1.Data.SequenceEqual(BMDCodeN1))
                        throw new Exception("BMD line: wrong codeN1");
                    splittedData.Remove(codeN1);

                    var codeN2 = splittedData.Last();
                    if (!codeN2.Data.SequenceEqual(BMDCodeN2))
                        throw new Exception("BMD line: wrong codeN2");
                    splittedData.Remove(codeN2);
                }

                if (splittedData.Count != 1 || !splittedData[0].IsText)
                    throw new Exception("BMD line: wrong text");

                var textData = splittedData[0].Data;
                switch (i)
                {
                    case 0:
                        MainQ = textData;
                        break;
                    case 1:
                        Answer = textData;
                        break;
                    default:
                        SideQ.Add(textData);
                        break;
                }
            }
        }

        private string Name { get; set; }

        public byte[] MainQ { get; set; }

        public byte[] Answer { get; set; }

        public List<byte[]> SideQ { get; } = new List<byte[]>();

        public CRWDField Field { get; set; }

        public void UpdatePAK()
        {
            InsertToBMD();

            var datGF = pak.SubFiles.Find(x => x.GameData.Type == FormatEnum.DAT);
            datGF.GameData = new DAT(Field.GetData());
        }

        public byte[] GetData()
        {
            UpdatePAK();

            return pak.GetData();
        }

        private void InsertToBMD()
        {
            var bmd = pak.SubFiles.Find(x => x.GameData.Type == FormatEnum.BMD).GameData as BMD;
            UpdateBMD(bmd, 0, MainQ);
            UpdateBMD(bmd, 1, Answer);
            for (int i = 0; i < SideQ.Count; i++)
                UpdateBMD(bmd, i + 2, SideQ[i]);
        }

        private static void UpdateBMD(BMD bmd, int index, byte[] buffer)
        {
            var msg = bmd.Msg[index];
            var data = msg.MsgStrings[0];
            var splitted = data.GetTextBases().ToList();
            splitted[2] = new TextBaseElement(true, buffer);
            msg.MsgStrings[0] = splitted.GetByteArray();
        }
    }
}