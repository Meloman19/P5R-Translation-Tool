using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using P5R_Packager.Common;
using PersonaEditorLib;
using PersonaEditorLib.FileContainer;

namespace P5R_Packager.DataTool.CrosswordImport
{
    internal sealed class CrosswordImporter
    {
        private const string TextDir = "TEXT";
        private const string CrosswordZip = "P5R_crossword.zip";

        private readonly PersonaEncoding _newEncoding;

        private Dictionary<string, string[]> _crosswordTranslate;

        public CrosswordImporter(string translatePath, PersonaEncoding newEncoding)
        {
            _newEncoding = newEncoding;

            ReadCrosswordTranslate(translatePath);
        }

        private void ReadCrosswordTranslate(string translatePath)
        {
            var path = Path.Combine(translatePath, TextDir, CrosswordZip);

            var result = new Dictionary<string, string[]>();

            if (File.Exists(path))
                using (var zip = ZipFile.OpenRead(path))
                {
                    foreach (var entry in zip.Entries)
                    {
                        var name = Path.GetFileNameWithoutExtension(entry.Name);

                        if (!name.StartsWith("CROSSWORD"))
                            continue;

                        string[] lines;
                        using (var entryStream = entry.Open())
                            lines = SomeHelpers.ReadAllLines(entryStream);

                        result.Add(name, lines);
                    }
                }

            _crosswordTranslate = result;
        }

        private static void SetCellData(CRWDField field, string value, int q)
        {
            var spl = value.Split(',');

            var row = int.Parse(spl[0]);
            var col = int.Parse(spl[1]);
            var dir = spl[2] == "V" ? CRWDDirection.Down : CRWDDirection.Across;

            var cell = field.Cells[row, col];
            if (q == -1)
            {
                cell.MainQuestion = true;
                cell.MainDirection = dir;
            }
            else
            {
                cell.SideQuestion = q;
                cell.SideDirection = dir;
            }
        }

        public bool Import(string relFilePath, GameFile gameFile)
        {
            var path = Path.GetDirectoryName(relFilePath.ToUpper());
            if (path != "MINIGAME\\CROSSWORD")
                return false;

            var gfNameWE = Path.GetFileNameWithoutExtension(gameFile.Name).ToUpper();
            if (!_crosswordTranslate.TryGetValue(gfNameWE, out string[] translate))
                return false;

            var bin = gameFile.GameData as BIN;
            if (bin == null)
                return false;

            Import(bin, gameFile.Name, _newEncoding, translate);

            return true;
        }

        private static void Import(BIN pak, string fileName, PersonaEncoding encoding, string[] text)
        {
            var crwd = new CRWD(pak, fileName);

            crwd.Field = new CRWDField();
            for (int i = 0; i < text.Length; i++)
            {
                var spl = text[i].Split('\t');
                var tag = spl[0];

                if (tag == "MAIN_Q_T")
                {
                    crwd.MainQ = encoding.GetBytes(spl[1]);
                    SetCellData(crwd.Field, spl[2], -1);
                }
                else if (tag == "ANSW_T")
                {
                    crwd.Answer = encoding.GetBytes(spl[1]);
                }
                else if (tag.StartsWith("SIDE_Q_") && tag.EndsWith("_T"))
                {
                    var sideQi = int.Parse(tag.Replace("SIDE_Q_", "").Replace("_T", "")) - 1;
                    var sideQs = spl[1];

                    if (sideQs == CRWD.NotUsedS)
                    {
                        crwd.SideQ[sideQi] = CRWD.NotUsed;
                    }
                    else
                    {
                        crwd.SideQ[sideQi] = encoding.GetBytes(sideQs);
                        SetCellData(crwd.Field, spl[2], sideQi);
                    }
                }
                else if (tag.StartsWith("CRWD_T"))
                {
                    var index = i + 2;
                    var cells = crwd.Field.Cells;

                    for (int r = 0; r < cells.GetLength(0); r++)
                    {
                        var fSpl = text[index].Split('\t').Skip(5).ToArray();

                        for (int c = 0; c < cells.GetLength(1); c++)
                        {
                            var cell = cells[r, c];

                            var ch = fSpl[c];

                            if (ch.Length > 1)
                                throw new Exception("Wrong char");

                            if (string.IsNullOrEmpty(ch))
                            {
                                cell.Available = false;
                                cell.Char = null;
                            }
                            else
                            {
                                cell.Available = true;

                                var ind = encoding.GetIndex(ch[0]);
                                var a1 = new byte[2];
                                byte byte2 = System.Convert.ToByte(((ind - 0x20) % 0x80) + 0x80);
                                byte byte1 = System.Convert.ToByte(((ind - 0x20 - byte2) / 0x80) + 0x81);
                                a1[0] = byte2;
                                a1[1] = byte1;

                                var a2 = BitConverter.ToUInt16(a1, 0);
                                cell.Char = a2;
                            }
                        }

                        index++;
                    }
                }
            }

            crwd.UpdatePAK();
        }
    }
}