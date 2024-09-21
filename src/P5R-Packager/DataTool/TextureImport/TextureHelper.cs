using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using PersonaEditorLib;
using PersonaEditorLib.Sprite;

namespace P5R_Packager.DataTool.TextureImport
{
    internal static class TextureHelper
    {
        public static IEnumerable<GameFile> EnumerateDDSExceptSPD(this GameFile gameFile)
        {
            if (gameFile.GameData.Type == FormatEnum.SPD)
                yield break;

            if (gameFile.GameData.Type == FormatEnum.DDS)
                yield return gameFile;

            foreach (var sub in gameFile.GameData.SubFiles)
                foreach (var gf in EnumerateDDSExceptSPD(sub))
                    yield return gf;
        }

        public static int GetScaleFactor((int W, int H) originSize, DDS dds)
        {
            if (originSize.W % dds.Width != 0
                || originSize.H % dds.Height != 0)
                throw new System.Exception("The scale is not integer");

            var wFactor = originSize.W / dds.Width;
            var hFactor = originSize.H / dds.Height;

            if (wFactor <= 0 || hFactor <= 0)
                throw new System.Exception("The scale is less than one");

            if (wFactor != hFactor)
                throw new System.Exception("The scales of the axes are different");

            return wFactor;
        }

        public static BitmapSource Resize(BitmapSource source, int scale)
        {
            if (scale == 1)
                return source;

            if (source.PixelWidth % scale != 0 ||
                source.PixelHeight % scale != 0)
                throw new System.Exception("The specified scale creates a non-integer size");

            var image = new TransformedBitmap(source, new ScaleTransform(1d / scale, 1d / scale));
            return image;
        }

        public static bool RemoveNode(XElement keyNode, List<(int Index, int Scale)> updated)
        {
            int texIndex = int.Parse(keyNode.Element("TextureIndex").Value);

            return !updated.Any(x => x.Index == texIndex);
        }

        public static void Resize(XElement keyNode, List<(int Index, int Scale)> updated)
        {
            var texIndexNode = keyNode.Element("TextureIndex");
            int texIndex = int.Parse(texIndexNode.Value);

            var scale = updated.Find(x => x.Index == texIndex).Scale;
            if (scale == 1)
                return;

            var X0Node = keyNode.Element("X");
            var Y0Node = keyNode.Element("Y");
            var widthNode = keyNode.Element("Width");
            var heightNode = keyNode.Element("Height");

            int X0 = int.Parse(X0Node.Value);
            int Y0 = int.Parse(Y0Node.Value);
            int width = int.Parse(widthNode.Value);
            int height = int.Parse(heightNode.Value);


            X0Node.Value = (X0 / scale).ToString();
            Y0Node.Value = (Y0 / scale).ToString();
            widthNode.Value = (width / scale).ToString();
            heightNode.Value = (height / scale).ToString();
        }
    }
}