using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using GameOff.Data;
using UnityEngine;

namespace GameOff.EditorTools
{
    /// <summary>Art/Export/manifest.json에서 9-slice 테두리와 글자 좌표를 읽는다(필요한 값만 정규식으로 읽음).</summary>
    public static class ArtManifest
    {
        public static string ExportDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Art/Export"));
        static string ManifestPath => Path.Combine(ExportDir, "manifest.json");

        // 키(예: "ui/button.png") -> 본문 텍스트. 최상위 키는 들여쓰기 2칸(json indent=2).
        static Dictionary<string, string> Blocks()
        {
            var blocks = new Dictionary<string, string>();
            if (!File.Exists(ManifestPath)) return blocks;
            string text = File.ReadAllText(ManifestPath);
            var starts = Regex.Matches(text, @"(?m)^  ""([^""]+)"": \{");
            for (int i = 0; i < starts.Count; i++)
            {
                int from = starts[i].Index + starts[i].Length;
                int to = i + 1 < starts.Count ? starts[i + 1].Index : text.Length;
                blocks[starts[i].Groups[1].Value] = text.Substring(from, to - from);
            }
            return blocks;
        }

        /// <summary>(왼쪽, 위, 오른쪽, 아래) px. 없으면 null.</summary>
        public static int[] NineSlice(string relPng)
        {
            if (!Blocks().TryGetValue(relPng, out string body)) return null;
            var m = Regex.Match(body, @"""nine_slice_LTRB""\s*:\s*\[\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\]");
            if (!m.Success) return null;
            return new[] { int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value) };
        }

        public struct AnimMeta { public int frameW, frameH, frames, fps; public int[] holds; public bool loop; }

        /// <summary>애니메이션 시트 메타(frame_size·frames·fps·holds·loop). 시트가 아니면 false.</summary>
        public static bool Anim(string relPng, out AnimMeta meta)
        {
            meta = default;
            if (!Blocks().TryGetValue(relPng, out string body)) return false;
            var fs = Regex.Match(body, @"""frame_size""\s*:\s*\[\s*(\d+)\s*,\s*(\d+)\s*\]");
            var fr = Regex.Match(body, @"""frames""\s*:\s*(\d+)");
            var fp = Regex.Match(body, @"""fps""\s*:\s*(\d+)");
            if (!fs.Success || !fr.Success || !fp.Success) return false;
            meta.frameW = int.Parse(fs.Groups[1].Value); meta.frameH = int.Parse(fs.Groups[2].Value);
            meta.frames = int.Parse(fr.Groups[1].Value); meta.fps = int.Parse(fp.Groups[1].Value);
            meta.loop = Regex.IsMatch(body, @"""loop""\s*:\s*true");
            var hm = Regex.Match(body, @"""holds""\s*:\s*\[([^\]]*)\]");
            if (hm.Success)
            {
                var list = new List<int>();
                foreach (Match n in Regex.Matches(hm.Groups[1].Value, @"\d+")) list.Add(int.Parse(n.Value));
                meta.holds = list.ToArray();
            }
            return true;
        }

        public static List<GlyphEntry> Glyphs()
        {
            var list = new List<GlyphEntry>();
            if (!Blocks().TryGetValue("ui/font5x7_atlas.png", out string body)) return list;
            int gi = body.IndexOf("\"glyphs\"");
            if (gi < 0) return list;
            string g = body.Substring(gi);
            var ms = Regex.Matches(g, @"""(\\u[0-9a-fA-F]{4}|\\.|[^""\\])""\s*:\s*\[\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\]");
            foreach (Match m in ms)
            {
                string k = m.Groups[1].Value;
                int code = k.Length > 1 && k[0] == '\\' && k[1] == 'u' ? System.Convert.ToInt32(k.Substring(2), 16) : k[k.Length - 1];
                list.Add(new GlyphEntry { code = code, x = int.Parse(m.Groups[2].Value), y = int.Parse(m.Groups[3].Value), w = int.Parse(m.Groups[4].Value), h = int.Parse(m.Groups[5].Value) });
            }
            return list;
        }
    }
}
