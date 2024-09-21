using System;
using System.IO;
using System.Linq;
using P5R_Packager.Common;
using PersonaEditorLib;
using PersonaEditorLib.Other;

namespace P5R_Packager.DataTool
{
    internal class DataToolProgram
    {
        private sealed class InputArgs
        {
            public string InputPath { get; set; } = string.Empty;

            public string OutputPath { get; set; } = string.Empty;

            public string TranslationPath { get; set; } = string.Empty;

            public string OldEncodingName { get; set; } = string.Empty;

            public string NewEncodingName { get; set; } = string.Empty;

            public bool CopyToOutput { get; set; } = false;
        }

        public static void Main(string[] args)
        {
            var arg = ReadArgs(args);

            InsertTranslate(arg);
        }

        private static InputArgs ReadArgs(string[] args)
        {
            var arg = new InputArgs();

            for (int i = 0; i < args.Length; i++)
            {
                var argKey = args[i];

                switch (argKey.ToLower())
                {
                    case "-input":
                        arg.InputPath = args[i + 1];
                        i++;
                        break;
                    case "-output":
                        arg.OutputPath = args[i + 1];
                        i++;
                        break;
                    case "-translation":
                        arg.TranslationPath = args[i + 1];
                        i++;
                        break;
                    case "-oldenc":
                        arg.OldEncodingName = args[i + 1];
                        i++;
                        break;
                    case "-newenc":
                        arg.NewEncodingName = args[i + 1];
                        i++;
                        break;
                    case "-copy2out":
                        arg.CopyToOutput = true;
                        break;
                    default:
                        throw new KnownException($"Unknown key: {argKey}");
                }
            }

            if (string.IsNullOrEmpty(arg.InputPath))
                throw new KnownException($"The path to the input directory is not set (unpacked EN.CPK). Key: -input");
            else if (!Directory.Exists(arg.InputPath))
                throw new KnownException($"The specified input directory does not exist. Key: -input");

            if (string.IsNullOrEmpty(arg.OutputPath))
                throw new KnownException($"The path to the output directory is not set. Key: -output");

            if (string.IsNullOrEmpty(arg.TranslationPath))
                throw new KnownException($"The path to the translation folder is not set. Key: -translate");

            if (string.IsNullOrEmpty(arg.OldEncodingName))
                throw new KnownException($"The name of the old font is not set. Key: -oldenc");

            if (string.IsNullOrEmpty(arg.NewEncodingName))
                throw new KnownException($"The name of the new font is not set. Key: -newenc");

            return arg;
        }

        private static void InsertTranslate(InputArgs args)
        {
            var inputPath = Path.GetFullPath(args.InputPath);
            var outputPath = Path.GetFullPath(args.OutputPath);
            var translatePath = Path.GetFullPath(args.TranslationPath);
            var copy2output = args.CopyToOutput;

            string template = "Files processed: {0} of {1}";
            var files = Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories).ToArray();
            var total = files.Length;
            var counter = 0;
            ConsoleWriter.ReWriteLine(template, counter, total);

            PersonaEncoding oldEncoding;
            PersonaEncoding newEncoding;
            PersonaFont newFont;
            {
                oldEncoding = new PersonaEncoding(Path.Combine(translatePath, args.OldEncodingName + ".fntmap"));
                newEncoding = new PersonaEncoding(Path.Combine(translatePath, args.NewEncodingName + ".fntmap"));
                newFont = new PersonaFont(Path.Combine(translatePath, args.NewEncodingName + ".fnt"));
            }

            var textImporter = new TextImport.BMDImporter(translatePath, oldEncoding, newEncoding, newFont);
            var tableImporter = new TableImport.TableImporter(translatePath, oldEncoding, newEncoding);
            var crosswordImporter = new CrosswordImport.CrosswordImporter(translatePath, newEncoding);
            var ddsImporter = new TextureImport.DDSImporter(translatePath);
            var spdImporter = new TextureImport.SPDImporter(translatePath);
            var plgImporter = new OtherImport.PLGImporter(translatePath);
            var speakerImporter = new TextImport.SpeakerImporter(translatePath, oldEncoding, newEncoding);

            var lastUpdate = DateTime.Now;
            foreach (var file in files)
            {
                try
                {
                    var fileData = File.ReadAllBytes(file);
                    var relFilePath = AuxiliaryLibraries.Tools.IOTools.RelativePath(file, inputPath);
                    var newFilePath = Path.Combine(outputPath, relFilePath);
                    var newDirPath = Path.GetDirectoryName(newFilePath);

                    var anyChange = false;
                    var gameFile = PersonaEditorLib.GameFormatHelper.OpenFile(Path.GetFileName(file), fileData);
                    if (gameFile == null)
                        gameFile = new GameFile(Path.GetFileName(file), new DAT(fileData));

                    anyChange |= textImporter.Import(relFilePath, gameFile);
                    anyChange |= tableImporter.Import(relFilePath, gameFile);
                    anyChange |= crosswordImporter.Import(relFilePath, gameFile);
                    anyChange |= ddsImporter.Import(relFilePath, gameFile);
                    anyChange |= spdImporter.Import(relFilePath, gameFile);
                    anyChange |= plgImporter.Import(relFilePath, gameFile);
                    anyChange |= speakerImporter.Import(relFilePath, gameFile);

                    if (anyChange)
                    {
                        Directory.CreateDirectory(newDirPath);
                        File.WriteAllBytes(newFilePath, gameFile.GameData.GetData());
                    }
                    else if (copy2output)
                    {
                        Directory.CreateDirectory(newDirPath);
                        File.WriteAllBytes(newFilePath, fileData);
                    }
                }
                finally
                {
                    counter++;
                    if ((DateTime.Now - lastUpdate).TotalSeconds > 1)
                    {
                        lastUpdate = DateTime.Now;
                        ConsoleWriter.ReWriteLine(template, counter, total);
                    }
                }
            }

            // The last step is to copy the new font, if it differs from the old one, to the FONT folder
            {
                if (args.OldEncodingName != args.NewEncodingName)
                {
                    var newFNTPath = Path.Combine(translatePath, args.NewEncodingName + ".FNT");
                    var copyToPath = Path.Combine(outputPath, "FONT", "FONT0.FNT");
                    File.Copy(newFNTPath, copyToPath, true);
                }
            }
        }
    }
}