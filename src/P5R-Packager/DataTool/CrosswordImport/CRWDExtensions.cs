using System;
using System.Collections.Generic;

namespace P5R_Packager.DataTool.CrosswordImport
{
    public static class CRWDExtensions
    {
        public static IEnumerable<T> Enumerate<T>(this T[,] data)
        {
            for (int r = 0; r < data.GetLength(0); r++)
                for (int c = 0; c < data.GetLength(1); c++)
                    yield return data[r, c];
        }

        public static void Shuffle<T>(this Random rng, T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }
    }
}