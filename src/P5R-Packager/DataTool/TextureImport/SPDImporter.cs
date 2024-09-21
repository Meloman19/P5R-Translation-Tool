using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AuxiliaryLibraries.Tools;
using AuxiliaryLibraries.WPF.Wrapper;
using Newtonsoft.Json;
using P5R_Packager.Common;
using PersonaEditorLib;
using PersonaEditorLib.Sprite;
using PersonaEditorLib.SpriteContainer;

namespace P5R_Packager.DataTool.TextureImport
{
    internal sealed class SPDImporter
    {
        private const string TextureDir = "TEX";

        private readonly string _texturePath;
        private Dictionary<string, (int W, int H)> _originSize;

        public SPDImporter(string translatePath)
        {
            _texturePath = Path.Combine(translatePath, TextureDir);
            ReadTranslation();
        }

        private void ReadTranslation()
        {
            var sizes = SomeHelpers.ReadResource("ORIGIN_SIZE.json");

            var sizeData = JsonConvert.DeserializeObject<TextureSizeModel[]>(sizes);

            _originSize = sizeData.ToDictionary(x => x.RelativeFilePath.ToUpper(), x => (x.Width, x.Height));
        }

        public bool Import(string relFilePath, GameFile gameFile)
        {
            bool anyChange = false;

            var relDirPath = Path.GetDirectoryName(relFilePath);

            var spdGFs = gameFile.GetAllObjectFiles(FormatEnum.SPD).ToArray();
            foreach (var spdGF in spdGFs)
            {
                var spd = spdGF.GameData as SPD;
                var spdName = spdGF.Name.ToUpper();

                var updatedDDS = new List<(int Index, int Scale)>();

                foreach (var ddsGD in spd.SubFiles)
                {
                    var dds = ddsGD.GameData as DDS;

                    var imagePath = Path.Combine(_texturePath, relDirPath, spdName, Path.GetFileNameWithoutExtension(ddsGD.Name) + ".png");
                    if (!File.Exists(imagePath))
                        continue;

                    imagePath = Path.GetFullPath(imagePath);
                    var relImagePath = IOTools.RelativePath(imagePath, _texturePath).ToUpper();
                    var originSize = _originSize[relImagePath];

                    var scaleFactor = TextureHelper.GetScaleFactor(originSize, dds);

                    var bitmapSource = ImageTools.OpenPNG(imagePath);
                    bitmapSource = TextureHelper.Resize(bitmapSource, scaleFactor);
                    var bitmap = bitmapSource.GetBitmap();

                    dds.SetBitmap(bitmap);

                    updatedDDS.Add(((int)ddsGD.Tag, scaleFactor));

                    anyChange |= true;
                }

                var xmlPath = Path.Combine(_texturePath, relDirPath, spdName, Path.GetFileNameWithoutExtension(spdName) + ".xml");
                if (File.Exists(xmlPath))
                {
                    var xDoc = XDocument.Load(xmlPath);
                    var root = xDoc.Element("SpriteInfo");
                    foreach (var key in root.Elements().ToArray())
                    {
                        if (TextureHelper.RemoveNode(key, updatedDDS))
                        {
                            key.Remove();
                            continue;
                        }

                        TextureHelper.Resize(key, updatedDDS);
                    }

                    spd.SetTable(xDoc);
                    anyChange |= true;
                }
            }

            return anyChange;
        }
    }
}