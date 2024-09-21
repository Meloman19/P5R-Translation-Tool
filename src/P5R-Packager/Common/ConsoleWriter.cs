using System;

namespace P5R_Packager.Common
{
    internal static class ConsoleWriter
    {
        private static int currentLineLeng = 0;

        public static void ReWriteLine(string template, params object[] args)
        {
            var message = string.Format(template, args);
            ReWriteLine(message);
        }

        public static void ReWriteLine(string message)
        {
            message = message.PadRight(currentLineLeng, ' ');
            currentLineLeng = message.Length;

            Console.Write("\r" + message);
        }

        public static void WriteLine(string message)
        {
            message = message.PadRight(currentLineLeng, ' ');
            currentLineLeng = 0;

            Console.WriteLine(message);
        }

        public static void NewLine()
        {
            currentLineLeng = 0;
            Console.WriteLine();
        }
    }
}
