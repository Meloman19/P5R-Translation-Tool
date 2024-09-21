using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuxiliaryLibraries.Extensions;
using Newtonsoft.Json.Linq;
using PersonaEditorLib.Other;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace P5R_Packager.DataTool.CrosswordImport
{
    public sealed class CRWDField
    {
        public const ushort CloseValue = 0xFFFE;
        public const ushort BlankValue = 0xFFFF;

        public CRWDCell[,] Cells { get; } = new CRWDCell[10, 10];

        public CRWDField()
        {
            for (int r = 0; r < Cells.GetLength(0); r++)
                for (int c = 0; c < Cells.GetLength(1); c++)
                {
                    Cells[r, c] = new CRWDCell(r, c);
                }
        }

        public CRWDField(DAT dat) : this()
        {
            using (var ms = new MemoryStream(dat.Data))
                Parse(ms);
        }

        private void Parse(Stream stream)
        {
            ushort[] answer;
            ushort[] alph;

            using (var binaryReader = new BinaryReader(stream))
            {
                var a = ParseHeader(binaryReader);
                answer = a.answer;
                ParseOpenClose(binaryReader);
                ParseChar(binaryReader);

                alph = ReadBlock(binaryReader).Concat(ReadBlock(binaryReader)).ToArray();

                ParseQuestions(binaryReader, a.countQ);

                if (stream.Length != stream.Position)
                    throw new Exception("Field: not end of file");
            }

            if (!CheckField(Cells))
                throw new Exception("Field: Not Valid");
            if (!CheckAnswer(Cells, answer))
                throw new Exception("Field: Answer Not Valid");
        }

        private (int countQ, ushort[] answer) ParseHeader(BinaryReader reader)
        {
            var block = ReadBlock(reader);

            int row = 0, column = 0, countQ = 0, ansL = 0;
            CRWDDirection dir = CRWDDirection.Down;

            ushort[] answer;

            for (int i = 0; i < block.Length; i++)
            {
                var val = block[i];
                switch (i)
                {
                    case 0:
                        column = val;
                        break;
                    case 1:
                        row = val;
                        break;
                    case 2:
                        if (!Enum.IsDefined(typeof(CRWDDirection), (int)val))
                            throw new Exception("Field: main Q wrong direction");
                        dir = (CRWDDirection)val;
                        break;
                    case 3:
                        countQ = val;
                        break;
                    case 4:
                        ansL = val;
                        break;
                    default:
                        if (val != 0)
                            throw new Exception("Filed Header: not zero");
                        break;
                }
            }

            var mainCell = Cells[row, column];
            mainCell.MainQuestion = true;
            mainCell.MainDirection = dir;

            var answerBlock = ReadBlock(reader);
            for (int i = 0; i < answerBlock.Length; i++)
            {
                var val = answerBlock[i];
                if (i < ansL && val == CloseValue)
                    throw new Exception("Field Answer: answer have closevalue");

                if (i >= ansL && val != CloseValue)
                    throw new Exception("Filed Answer: answer not ended with closevalue");
            }

            answer = answerBlock.Take(ansL).ToArray();

            return (countQ, answer);
        }

        private void ParseOpenClose(BinaryReader reader)
        {
            for (int r = 0; r < Cells.GetLength(0); r++)
            {
                var block = ReadBlock(reader);

                for (int c = 0; c < Cells.GetLength(1); c++)
                {
                    var cell = Cells[r, c];

                    switch (block[c])
                    {
                        case CloseValue:
                            cell.Available = false;
                            break;
                        case BlankValue:
                            cell.Available = true;
                            break;
                        default:
                            throw new Exception("Field OpenClose: wrong value");
                    }
                }
            }
        }

        private void ParseChar(BinaryReader reader)
        {
            for (int r = 0; r < Cells.GetLength(0); r++)
            {
                var block = ReadBlock(reader);

                for (int c = 0; c < Cells.GetLength(1); c++)
                {
                    var cell = Cells[r, c];

                    // In theory, inaccessible cells should be marked CloseValue.
                    // The format is apparently not strict, so they can contain any characters. 
                    // They are the easiest to ignore so as not to be distracted.
                    // You can pack it back with a normal value.
                    if (cell.Available)
                        cell.Char = block[c];
                }
            }
        }

        private void ParseQuestions(BinaryReader reader, int countQ)
        {
            for (int i = 0; i < 11; i++)
            {
                var block = ReadBlock(reader);
                if (i < countQ)
                {
                    int row = 0, column = 0;
                    CRWDDirection dir = CRWDDirection.Down;
                    for (int k = 0; k < block.Length; k++)
                    {
                        var val = block[k];

                        switch (k)
                        {
                            case 0:
                                column = val;
                                break;
                            case 1:
                                row = val;
                                break;
                            case 2:
                                if (!Enum.IsDefined(typeof(CRWDDirection), (int)val))
                                    throw new Exception("Field: main Q wrong direction");
                                dir = (CRWDDirection)val;
                                break;
                            default:
                                if (val != 0)
                                    throw new Exception("Filed Header: not zero");
                                break;
                        }
                    }
                    var cell = Cells[row, column];
                    cell.SideQuestion = i;
                    cell.SideDirection = dir;
                }
                else
                {
                    if (block.Any(x => x != 0))
                        throw new Exception("Field Q: not question not empty");
                }
            }
        }

        private static bool CheckField(CRWDCell[,] field)
        {
            if (field.Enumerate().Any(x => !x.IsValid()))
                return false;

            if (field.Enumerate().Count(x => x.MainQuestion) != 1)
                return false;

            return true;
        }

        private static bool CheckAnswer(CRWDCell[,] field, ushort[] answer)
        {
            var mainCell = field.Enumerate().FirstOrDefault(x => x.MainQuestion);
            if (mainCell == null)
                return false;

            if (mainCell.MainDirection == CRWDDirection.Across)
            {
                var row = mainCell.Row;
                if (mainCell.Column + answer.Length > 10)
                    return false;

                for (int i = 0; i < answer.Length; i++)
                {
                    var cell = field[row, mainCell.Column + i];

                    if (cell.Char != answer[i])
                        return false;
                }

                if (mainCell.Column + answer.Length < 10)
                {
                    var nextCell = field[row, mainCell.Column + answer.Length];

                    if (nextCell.Char.HasValue)
                        return false;
                }
            }
            else
            {
                var column = mainCell.Column;
                if (mainCell.Row + answer.Length > 10)
                    return false;

                for (int i = 0; i < answer.Length; i++)
                {
                    var cell = field[mainCell.Row + i, column];

                    if (cell.Char != answer[i])
                        return false;
                }

                if (mainCell.Row + answer.Length < 10)
                {
                    var nextCell = field[mainCell.Row + answer.Length, column];

                    if (nextCell.Char.HasValue)
                        return false;
                }
            }

            return true;
        }

        private static ushort[] ReadBlock(BinaryReader reader)
        {
            var list = new List<ushort>();

            for (int i = 0; i < 10; i++)
            {
                list.Add(reader.ReadUInt16());
            }

            for (int i = 0; i < 2; i++)
            {
                var a = reader.ReadUInt16();
                if (a != 0)
                    throw new System.Exception("Field: Wrong align");
            }

            return list.ToArray();
        }

        public byte[] GetData()
        {
            if (!CheckField(Cells))
                throw new Exception("Not Valid");

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteHeader(writer);
                WriteOpenClose(writer);
                WriteChar(writer);
                WriteAlph(writer);
                WriteQ(writer);

                return ms.ToArray();
            }
        }

        private void WriteHeader(BinaryWriter writer)
        {
            var answer = GetAnswer(Cells);

            ushort[] block = new ushort[10];
            var mainCell = Cells.Enumerate().First(x => x.MainQuestion);
            block[0] = (ushort)mainCell.Column;
            block[1] = (ushort)mainCell.Row;
            block[2] = (ushort)mainCell.MainDirection;
            block[3] = (ushort)Cells.Enumerate().Count(x => x.SideQuestion.HasValue);
            block[4] = (ushort)answer.Length;
            WriteBlock(writer, block);

            block = Enumerable.Repeat(CloseValue, 10).ToArray();
            Array.Copy(answer, block, answer.Length);
            WriteBlock(writer, block);
        }

        private void WriteOpenClose(BinaryWriter writer)
        {
            for (int r = 0; r < Cells.GetLength(0); r++)
            {
                ushort[] block = new ushort[10];

                for (int c = 0; c < Cells.GetLength(1); c++)
                {
                    var cell = Cells[r, c];

                    block[c] = cell.Available ? BlankValue : CloseValue;
                }

                WriteBlock(writer, block);
            }
        }

        private void WriteChar(BinaryWriter writer)
        {
            for (int r = 0; r < Cells.GetLength(0); r++)
            {
                ushort[] block = new ushort[10];

                for (int c = 0; c < Cells.GetLength(1); c++)
                {
                    var cell = Cells[r, c];

                    block[c] = cell.Char ?? CloseValue;
                }

                WriteBlock(writer, block);
            }
        }

        private void WriteAlph(BinaryWriter writer)
        {
            var existChars = Cells.Enumerate().Where(x => x.Char.HasValue).Select(x => x.Char.Value).ToArray();
            var addedCount = 20 - existChars.Length;

            var random = new Random();
            var addedChars = Enumerable.Repeat(1, addedCount).Select(x =>
            {
                var r = random.Next(0, existChars.Length);
                return existChars[r];
            }).ToArray();

            var resultChars = existChars.Concat(addedChars).ToArray();
            random.Shuffle(resultChars);

            var b1 = resultChars.SubArray(0, 10);
            var b2 = resultChars.SubArray(10, 10);

            WriteBlock(writer, b1);
            WriteBlock(writer, b2);
        }

        private void WriteQ(BinaryWriter writer)
        {
            for (int i = 0; i < 11; i++)
            {
                var block = new ushort[10];

                var Q = Cells.Enumerate().FirstOrDefault(x => x.SideQuestion == i);
                if (Q != null)
                {
                    block[0] = (ushort)Q.Column;
                    block[1] = (ushort)Q.Row;
                    block[2] = (ushort)Q.SideDirection;
                }

                WriteBlock(writer, block);
            }
        }

        private static ushort[] GetAnswer(CRWDCell[,] cells)
        {
            var mainCell = cells.Enumerate().First(x => x.MainQuestion);

            var res = new List<ushort>();
            res.Add(mainCell.Char.Value);

            if (mainCell.MainDirection == CRWDDirection.Across)
            {
                var row = mainCell.Row;
                var col = mainCell.Column + 1;
                while (true)
                {
                    if (col >= 10)
                        break;

                    var cell = cells[row, col];
                    if (cell.Char.HasValue)
                        res.Add(cell.Char.Value);
                    else
                        break;

                    col++;
                }
            }
            else
            {
                var row = mainCell.Row + 1;
                var col = mainCell.Column;
                while (true)
                {
                    if (row >= 10)
                        break;

                    var cell = cells[row, col];
                    if (cell.Char.HasValue)
                        res.Add(cell.Char.Value);
                    else
                        break;

                    row++;
                }
            }

            return res.ToArray();
        }

        private static void WriteBlock(BinaryWriter writer, ushort[] block)
        {
            if (block.Length != 10)
                throw new Exception("");

            foreach (var item in block)
                writer.Write(item);

            writer.Write((ushort)0);
            writer.Write((ushort)0);
        }
    }
}