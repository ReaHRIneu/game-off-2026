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
        static readonly string[] Folders = { "backgrounds", "scenes", "ui", "tiles", "sprites", "portraits", "objects", "objects128", "anim" };   // preview/는 복사하지 않음
        // W-010: 방 레이어(rooms128)와 맵 배치(scenes128)는 하위 폴더 구조를 그대로 복사한다(PNG + layout.json)
        static readonly string[] TreeFolders = { "rooms128", "scenes128" };

        // W-032: 지금 씬·ArtSet이 쓰지 않는 메인 로비 시안·본그림(main_lobby_*)과 helper 시안(helper_*)은 반입하지 않는다.
        // 반입하면 Setup MVP-1이 ArtSet.asset·Main.unity를 무관하게 바꿔 「modified externally」 창을 만든다. 쓰게 되면 이 접두어에서 뺀다
        // 2026-11-27 정리: 새 그림으로 바뀐 이전 판(러너 시트 v2·v3·W-043판, 해커 초상 v2·v3)도 빌드에 넣지 않는다.
        // 원본 출력(Art/Export)과 레시피는 되돌림용으로 남는다. 되돌릴 때는 여기서 접두어를 빼고 Import Art → Setup MVP-1
        static readonly string[] SkipPrefixes = { "main_lobby_", "helper_", "runner128_v2_", "runner128_v3_", "runner64_x2_", "hacker_v2_", "hacker_v3_", "room_screen_boot_" };   // room_screen_boot_: W-045 이후 부팅이 화면 전체 연출이라 안 씀
        // 이전 러너 초상(portraits/runner_<표정>, W-040 격자판). 새 초상은 runner_v4_x6_<표정>
        static readonly string[] SkipNames = { "runner_neutral.png", "runner_joy.png", "runner_sadness.png", "runner_anger.png", "runner_fear.png", "runner_surprise.png", "runner_disgust.png" };

        static bool Skipped(string file)
        {
            string n = Path.GetFileName(file);
            if (n == "main_lobby_v6_x8.png") return false;   // W-043: PixelLab 가공 합성 한 장
            if (n.StartsWith("main_lobby_v6_x8_")) return false;   // W-050: 메인 화면 움직임 덮개 그림(blink_hacker, blink_runner, hands_l, hands_r, mon_l1, mon_l2)
            if (n == "main_lobby_v5.png") return false;   // W-041: 메인 화면 합성 그림 한 장만 반입한다(레이어 4장은 쓰지 않음)
            foreach (string p in SkipPrefixes) if (n.StartsWith(p)) return true;
            foreach (string s in SkipNames) if (n == s) return true;
            return false;
        }

        /// <summary>쓰지 않는 옛 맵 배치(scenes128/*_tmp)는 반입하지 않는다(2026-11-27 정리).</summary>
        static bool SkippedTree(string rel) => rel.Replace('\\', '/').Contains("_tmp/");

        public const string FontAtlasPath = "Assets/_Project/Resources/galmuri9_atlas.png";
        static readonly string[] FontFiles = { "galmuri9_atlas.png", "galmuri9_glyphs.json" };

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
                    if (Skipped(f)) continue;
                    string dst = Path.Combine(ddir, Path.GetFileName(f));
                    // 내용이 같으면 건드리지 않는다(.meta·재임포트 유지)
                    if (File.Exists(dst) && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(dst), File.ReadAllBytes(f))) continue;
                    File.Copy(f, dst, true);
                    copied++;
                }
            }
            foreach (string folder in TreeFolders)
            {
                string sdir = Path.Combine(src, folder);
                if (!Directory.Exists(sdir)) continue;
                foreach (string f in Directory.GetFiles(sdir, "*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".png" && ext != ".json") continue;
                    string rel = f.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (SkippedTree(rel)) continue;
                    string dst = Path.Combine(dstRoot, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    if (File.Exists(dst) && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(dst), File.ReadAllBytes(f))) continue;
                    File.Copy(f, dst, true);
                    copied++;
                }
            }
            // W-034(D-124): Galmuri9 비트맵 글꼴(Art/Export/font)은 Resources로 복사한다(PixelCanvas가 Resources에서 읽는다)
            foreach (string name in FontFiles)
            {
                string f = Path.Combine(src, "font", name);
                if (!File.Exists(f)) continue;
                string dst = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project/Resources", name));
                if (File.Exists(dst) && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(dst), File.ReadAllBytes(f))) continue;
                File.Copy(f, dst, true);
                copied++;
            }
            AssetDatabase.Refresh();
            Debug.Log("[ArtImporter] 복사한 파일: " + copied);
        }

        void OnPreprocessTexture()
        {
            if (assetPath == FontAtlasPath)
            {
                // 글자 모양 마스크를 만들 때 픽셀을 읽는다. 점 확대·무압축
                var fi = (TextureImporter)assetImporter;
                fi.textureType = TextureImporterType.Default;
                fi.filterMode = FilterMode.Point;
                fi.textureCompression = TextureImporterCompression.Uncompressed;
                fi.mipmapEnabled = false;
                fi.isReadable = true;
                fi.alphaIsTransparency = true;
                fi.npotScale = TextureImporterNPOTScale.None;
                fi.wrapMode = TextureWrapMode.Clamp;
                return;
            }
            if (!assetPath.StartsWith(ArtRoot + "/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            // 128 단위 그림(anim/ 및 이후 4배 그림, W-009): Bilinear(D-076). 기존 32px 계열은 Point(4배 Point 확대에 사용)
            // W-010: 방 레이어(rooms128/)·맵 배치(scenes128/)도 같은 4배 규칙(PixelCanvas: 이름에 128이 들어가면 4배 그림)
            string relKey = assetPath.Substring(ArtRoot.Length + 1);
            bool hiRes = relKey.StartsWith("anim/") || relKey.Contains("128") || relKey.EndsWith("_4x.png");
            bool roomLayer = relKey.StartsWith("rooms128/") || relKey.StartsWith("scenes128/");
            ti.spritePixelsPerUnit = hiRes ? 128 : 32;
            ti.filterMode = hiRes ? FilterMode.Bilinear : FilterMode.Point;
            if (hiRes) ti.maxTextureSize = 4096;       // 24프레임 시트(3072px)가 줄어들지 않게
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.isReadable = !roomLayer;                 // 글꼴 아틀라스 마스크를 만들 때 픽셀을 읽는다. 방 레이어는 읽지 않으므로 끈다(메모리 절약, web-optimization §4)
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
