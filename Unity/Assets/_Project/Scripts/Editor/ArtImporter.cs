using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameOff.EditorTools
{
    /// <summary>
    /// Art/Export의 PNG를 Unity/Assets/_Project/Art/로 복사하고(메뉴 GameOff/Import Art),
    /// 그 경로의 텍스처에 도트용 임포트 설정을 자동 적용한다(AssetPostprocessor).
    /// </summary>
    public sealed class ArtImporter : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/_Project/Art";
        static readonly string[] Folders = { "backgrounds", "scenes", "ui", "tiles", "sprites", "portraits", "objects" };   // preview/는 복사하지 않음

        [MenuItem("GameOff/Import Art")]
        public static void ImportArt()
        {
            string src = ArtManifest.ExportDir;
            string dstRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project/Art"));
            int copied = 0;
            foreach (string folder in Folders)
            {
                string sdir = Path.Combine(src, folder);
                if (!Directory.Exists(sdir)) continue;
                string ddir = Path.Combine(dstRoot, folder);
                Directory.CreateDirectory(ddir);
                foreach (string f in Directory.GetFiles(sdir, "*.png"))
                {
                    string dst = Path.Combine(ddir, Path.GetFileName(f));
                    // 내용이 같으면 건드리지 않는다(.meta·재임포트 유지)
                    if (File.Exists(dst) && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(dst), File.ReadAllBytes(f))) continue;
                    File.Copy(f, dst, true);
                    copied++;
                }
            }
            AssetDatabase.Refresh();
            Debug.Log("[ArtImporter] 복사한 PNG: " + copied);
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot + "/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 32;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.isReadable = true;                       // 화면 캔버스가 픽셀을 읽어 합성한다
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;

            // manifest의 LTRB(왼·위·오른·아래) -> Unity Sprite Border(왼·아래·오른·위)
            string rel = assetPath.Substring(ArtRoot.Length + 1);
            int[] b = ArtManifest.NineSlice(rel);
            ti.spriteBorder = b == null ? Vector4.zero : new Vector4(b[0], b[3], b[2], b[1]);
        }
    }
}
