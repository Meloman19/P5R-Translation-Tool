using System;
using System.IO;
using System.Linq;
using PersonaEditorLib;
using PersonaEditorLib.Other;

namespace P5R_Packager.DataTool.OtherImport
{
    internal sealed class PLGImporter
    {
        private const string PlgDir = "PLG";

        private readonly string _plgPath;

        public PLGImporter(string translatePath)
        {
            _plgPath = Path.Combine(translatePath, PlgDir);
        }

        public bool Import(string relFilePath, GameFile gameFile)
        {
            bool anyChange = false;

            var relDirPath = Path.GetDirectoryName(relFilePath);

            var datGFs = gameFile.GetAllObjectFiles(FormatEnum.DAT).ToArray();
            foreach (var datGF in datGFs)
            {
                if (Path.GetExtension(datGF.Name).ToUpper() != ".PLG")
                    continue;

                var plgPath = Path.Combine(_plgPath, relDirPath, datGF.Name).ToUpper();
                if (Path.GetFileName(datGF.Name).Equals("DAY.PLG", StringComparison.InvariantCultureIgnoreCase)
                    && !Path.GetFileName(relFilePath).Equals("DAY.PLG", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Единственный хак
                    var dir = Path.GetDirectoryName(plgPath);
                    plgPath = Path.Combine(dir, "DAY(01).PLG");
                }

                if (!File.Exists(plgPath))
                    continue;

                var newData = File.ReadAllBytes(plgPath);
                var newDat = new DAT(newData);
                datGF.GameData = newDat;
                anyChange |= true;
            }

            return anyChange;
        }
    }
}