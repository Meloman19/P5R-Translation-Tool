using System;
using System.Linq;
using P5R_Packager.Common;
using P5R_Packager.DataTool;
using P5R_Packager.ExeTool;

namespace P5R_Packager
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var tool = args[0];
                switch (tool)
                {
                    case "DATA":
                        DataToolProgram.Main(args.Skip(1).ToArray());
                        break;
                    case "EXE":
                        ExeToolProgram.Main(args.Skip(1).ToArray());
                        break;
                    default:
                        throw new Exception("Unknown tool type");
                }
            }
            catch (Exception ex)
            {
                ConsoleWriter.NewLine();
                ConsoleWriter.WriteLine(ex.ToString());
                Environment.ExitCode = 1;
            }
        }
    }
}