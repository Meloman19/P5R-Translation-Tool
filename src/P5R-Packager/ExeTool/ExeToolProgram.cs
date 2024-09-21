using System;
using System.IO;
using System.Linq;
using AsmResolver.PE.File;
using Newtonsoft.Json;
using P5R_Packager.Common;
using P5R_Packager.ExeTool.EXE;
using P5R_Packager.ExeTool.NSO;
using PersonaEditorLib;

namespace P5R_Packager.ExeTool
{
    internal class ExeToolProgram
    {
        private class InputArgs
        {
            public string InputPath { get; set; } = string.Empty;

            public string OutputPath { get; set; } = string.Empty;

            public string TranslationPath { get; set; } = string.Empty;

            public string OldEncodingName { get; set; } = string.Empty;

            public string NewEncodingName { get; set; } = string.Empty;

            public bool Patch { get; set; } = false;

            public PersonaEncoding OldEncoding { get; set; }

            public PersonaEncoding NewEncoding { get; set; }

            public PersonaFont NewFont { get; set; }
        }

        public static void Main(string[] args)
        {
            var inputArgs = ReadArgs(args.Skip(1).ToArray());

            var type = args[0];
            switch (type)
            {
                case "STEAM":
                    SteamMain(inputArgs);
                    break;
                case "GAMEPASS":
                    GamepassMain(inputArgs);
                    break;
                case "SWITCH":
                    SwitchMain(inputArgs);
                    break;
                default:
                    throw new KnownException("Unknown type");
            }
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
                    case "-patch":
                        arg.Patch = true;
                        break;
                    default:
                        throw new KnownException($"Unknown argument: {argKey}");
                }
            }

            if (string.IsNullOrEmpty(arg.InputPath))
                throw new KnownException("The path to the executable file is not set. Key: -input");
            else if (!File.Exists(arg.InputPath))
                throw new KnownException("The specified executable file does not exist. Key: -input");

            if (string.IsNullOrEmpty(arg.OutputPath))
                throw new KnownException("The path to the output directory is not set. Key: -output");
            else if (File.Exists(arg.OutputPath))
                throw new KnownException("The path to the file is specified, but it is required to the output directory. Key: -output");

            if (string.IsNullOrEmpty(arg.TranslationPath))
                throw new KnownException("The path to the translation folder is not set. Key: -translate");

            if (string.IsNullOrEmpty(arg.OldEncodingName))
                throw new KnownException("The name of the old font is not set. Key: -oldenc");
            else
            {
                var oldEncPath = Path.Combine(arg.TranslationPath, arg.OldEncodingName + ".fntmap");
                if (!File.Exists(oldEncPath))
                    throw new KnownException($"The new encoding file with the specified name ({Path.GetFileName(oldEncPath)}) does not exist in the Translation folder.");

                arg.OldEncoding = new PersonaEncoding(oldEncPath);
            }

            if (string.IsNullOrEmpty(arg.NewEncodingName))
                throw new KnownException("The name of the new font is not set. Key: -newenc");
            else
            {
                var newEncPath = Path.Combine(arg.TranslationPath, arg.NewEncodingName + ".fntmap");
                if (!File.Exists(newEncPath))
                    throw new KnownException($"The new encoding file with the specified name ({Path.GetFileName(newEncPath)}) does not exist in the Translation folder.");

                arg.NewEncoding = new PersonaEncoding(newEncPath);

                var newFontPath = Path.Combine(arg.TranslationPath, arg.NewEncodingName + ".fnt");
                if (!File.Exists(newFontPath))
                    throw new KnownException($"The new font file with the specified name ({Path.GetFileName(newFontPath)}) does not exist in the Translation folder.");

                arg.NewFont = new PersonaFont(newFontPath);
            }

            return arg;
        }

        private static void GamepassMain(InputArgs inputArgs)
        {
            ProcessExe(inputArgs, false);
        }

        private static void SteamMain(InputArgs inputArgs)
        {
            ProcessExe(inputArgs, true);
        }

        private static void ProcessExe(InputArgs inputArgs, bool steam)
        {
            var exeDataName = steam ? "EXE_DATA_ST.json" : "EXE_DATA_GP.json";
            var exeDataText = SomeHelpers.ReadResource(exeDataName);
            var exeDataModel = JsonConvert.DeserializeObject<ExeDataModel>(exeDataText);

            var bmdImporter = new BMDImporter(inputArgs.TranslationPath, inputArgs.OldEncoding, inputArgs.NewEncoding, inputArgs.NewFont);
            var movieImporter = new MOVIEImporter(inputArgs.TranslationPath, inputArgs.OldEncoding, inputArgs.NewEncoding, exeDataModel);
            var chatNameImporter = new ChatNameImporter(inputArgs.TranslationPath, inputArgs.OldEncoding, inputArgs.NewEncoding, exeDataModel);
            var ptrArrayImporter = new PtrArrayImporter(inputArgs.TranslationPath, inputArgs.OldEncoding, inputArgs.NewEncoding, exeDataModel);
            var otherImporter = new OtherImporter(inputArgs.TranslationPath, inputArgs.OldEncoding, inputArgs.NewEncoding, exeDataModel);

            var exeData = File.ReadAllBytes(inputArgs.InputPath);
            var originExeData = exeData.ToArray();
            var peFile = PEFile.FromBytes(exeData);

            using (var ms = new MemoryStream(exeData))
            {
                bmdImporter.Import(ms);
                movieImporter.Import(ms, peFile);
                chatNameImporter.Import(ms, peFile);
                ptrArrayImporter.Import(ms, peFile);
                otherImporter.Import(ms, peFile);
            }

            if (inputArgs.Patch)
            {
                var outputFile = Path.Combine(inputArgs.OutputPath, "TRANSL.DAT");
                var patch = ExePatchHelper.GetPatch(originExeData, exeData, peFile);

                using (var fs = File.Create(outputFile))
                using (var writer = new BinaryWriter(fs))
                {
                    foreach (var p in patch)
                    {
                        writer.Write(p.Item1);
                        writer.Write(p.Item2.Length);
                        writer.Write(p.Item2);
                    }
                }

                var dll = SomeHelpers.ReadResourceData("proxy.dll");
                var outputDll = Path.Combine(inputArgs.OutputPath, "xinput1_4.dll");
                File.WriteAllBytes(outputDll, dll);
            }
            else
            {
                var outputFile = Path.Combine(inputArgs.OutputPath, Path.GetFileName(inputArgs.InputPath));
                File.WriteAllBytes(outputFile, exeData);
            }
        }

        static void SwitchMain(InputArgs inputArgs)
        {
            var exeDataText = SomeHelpers.ReadResource("EXE_DATA_NS.json");
            var exeDataModel = JsonConvert.DeserializeObject<ExeDataModel>(exeDataText);

            NSOFile nso;
            using (var fs = File.OpenRead(inputArgs.InputPath))
                nso = new NSOFile(fs);

            var originData = nso.GetData(false);

            var chatNameImporter = new ChatNameImporter(inputArgs.TranslationPath, inputArgs.OldEncoding, inputArgs.NewEncoding, exeDataModel);
            var bmdImporter = new BMDImporter(inputArgs.TranslationPath, inputArgs.OldEncoding, inputArgs.NewEncoding, inputArgs.NewFont);
            var nsoImporter = new NSOStringImporter(inputArgs.TranslationPath, inputArgs.OldEncoding, inputArgs.NewEncoding, exeDataModel);
            var otherImporter = new OtherImporter(inputArgs.TranslationPath, inputArgs.OldEncoding, inputArgs.NewEncoding, exeDataModel);

            chatNameImporter.Import(nso);
            nsoImporter.Import(nso);

            otherImporter.Import(nso);
            using (var ms = new MemoryStream(nso.Data.Data))
                bmdImporter.Import(ms);

            if (inputArgs.Patch)
            {
                var moduleId = Convert.ToHexString(nso.ModuleId);
                var outputFile = Path.Combine(inputArgs.OutputPath, moduleId + ".ips");

                var patchedData = nso.GetData(false);
                var patch = IPSHelper.Diff2Files(originData, patchedData, 0x100);
                patch.WriteToFile(outputFile);
            }
            else
            {
                var outputFile = Path.Combine(inputArgs.OutputPath, Path.GetFileName(inputArgs.InputPath));
                var newData = nso.GetData(true);
                File.WriteAllBytes(outputFile, newData);
            }
        }
    }
}