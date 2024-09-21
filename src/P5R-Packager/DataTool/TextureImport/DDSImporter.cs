using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuxiliaryLibraries.Tools;
using AuxiliaryLibraries.WPF.Wrapper;
using Newtonsoft.Json;
using P5R_Packager.Common;
using P5R_Packager.DataTool.TextImport;
using PersonaEditorLib;
using PersonaEditorLib.Sprite;

namespace P5R_Packager.DataTool.TextureImport
{
    internal sealed class DDSImporter
    {
        private const string TextureDir = "TEX";

        private readonly string _texturePath;
        private Dictionary<string, string> _ddsDuplicates;
        private Dictionary<string, (int W, int H)> _originSize;

        public DDSImporter(string translatePath)
        {
            _texturePath = Path.Combine(translatePath, TextureDir);
            ReadTranslation();
        }

        private void ReadTranslation()
        {
            var dupl = SomeHelpers.ReadResource("DUPL_DDS.json");

            var data = JsonConvert.DeserializeObject<DuplicateModel[]>(dupl);

            _ddsDuplicates = data.SelectMany(x => x.Duplicates.Select(y => (dupl: y, orig: x.File))).ToDictionary(x => x.dupl.ToUpper(), x => x.orig.ToUpper());

            var sizes = SomeHelpers.ReadResource("ORIGIN_SIZE.json");

            var sizeData = JsonConvert.DeserializeObject<TextureSizeModel[]>(sizes);

            _originSize = sizeData.ToDictionary(x => x.RelativeFilePath.ToUpper(), x => (x.Width, x.Height));
        }

        public bool Import(string relFilePath, GameFile gameFile)
        {
            bool anyChange = false;

            var relDirPath = Path.GetDirectoryName(relFilePath);

            var ddsGFs = gameFile.EnumerateDDSExceptSPD().ToArray();
            foreach (var ddsGF in ddsGFs)
            {
                var imagePath = Path.Combine(_texturePath, relDirPath, Path.GetFileNameWithoutExtension(ddsGF.Name) + ".png").ToUpper();
                if (!File.Exists(imagePath))
                {
                    var relPath = Path.Combine(relDirPath, Path.GetFileNameWithoutExtension(ddsGF.Name) + ".png").ToUpper();
                    if (!_ddsDuplicates.TryGetValue(relPath, out var ddsDuplicate))
                        continue;

                    imagePath = Path.Combine(_texturePath, ddsDuplicate).ToUpper();
                    if (!File.Exists(imagePath))
                        continue;
                }

                var relImagePath = IOTools.RelativePath(imagePath, _texturePath.ToUpper());
                var originSize = _originSize[relImagePath];

                var dds = ddsGF.GameData as DDS;

                var scaleFactor = TextureHelper.GetScaleFactor(originSize, dds);

                var bitmapSource = ImageTools.OpenPNG(imagePath);
                bitmapSource = TextureHelper.Resize(bitmapSource, scaleFactor);
                var bitmap = bitmapSource.GetBitmap();

                dds.SetBitmap(bitmap);
                anyChange |= true;
            }

            return anyChange;
        }
    }
}