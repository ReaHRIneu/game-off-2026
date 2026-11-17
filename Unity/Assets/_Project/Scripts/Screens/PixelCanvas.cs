using System.Collections.Generic;
using System.IO;
using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 640x360 도트 캔버스(CPU). 그림 1픽셀 = 캔버스 1픽셀이고, 화면에는 정수 배율 + Point 필터로만 확대한다.
    /// 내부 버퍼는 왼쪽 위가 (0,0)이다. 반입 PNG는 Read/Write가 켜져 있어야 한다(임포트 설정이 켠다).
    /// </summary>
    public sealed class PixelCanvas
    {
        public const int W = 640, H = 360;

        sealed class Img
        {
            public int w, h;
            public Color32[] px;     // 왼쪽 위 기준
            public int nl, nt, nr, nb;
        }

        readonly Color32[] buf = new Color32[W * H];
        readonly Color32[] flipped = new Color32[W * H];
        readonly Dictionary<string, Img> imgs = new Dictionary<string, Img>();
        readonly Dictionary<int, GlyphEntry> glyphs = new Dictionary<int, GlyphEntry>();
        readonly Img atlas;

        public Texture2D Texture { get; }

        public PixelCanvas(ArtSet art)
        {
            Texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (art == null) return;
            foreach (var e in art.entries)
            {
                if (e.texture == null) continue;
                imgs[e.key] = Load(e);
            }
            foreach (var g in art.glyphs) glyphs[g.code] = g;
            imgs.TryGetValue("ui/font5x7_atlas", out atlas);
        }

        static Img Load(ArtEntry e)
        {
            var t = e.texture;
            var src = t.GetPixels32();   // 왼쪽 아래 기준
            var img = new Img { w = t.width, h = t.height, px = new Color32[src.Length], nl = e.nl, nt = e.nt, nr = e.nr, nb = e.nb };
            for (int y = 0; y < t.height; y++)
                System.Array.Copy(src, (t.height - 1 - y) * t.width, img.px, y * t.width, t.width);
            return img;
        }

        // ---------- 기본 그리기 ----------

        public void Clear(Color32 c)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] = c;
        }

        // 그리기 제한 영역(장면 패널처럼 일부 영역 안에만 그릴 때). 기본은 캔버스 전체.
        int clipX0, clipY0, clipX1 = W, clipY1 = H;

        /// <summary>이후의 Fill·Blit·Text가 이 사각형 안에만 그려지게 한다. ClearClip으로 해제.</summary>
        public void SetClip(int x, int y, int w, int h)
        {
            clipX0 = System.Math.Max(0, x); clipY0 = System.Math.Max(0, y);
            clipX1 = System.Math.Min(W, x + w); clipY1 = System.Math.Min(H, y + h);
        }

        public void ClearClip()
        {
            clipX0 = 0; clipY0 = 0; clipX1 = W; clipY1 = H;
        }

        public void Fill(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = System.Math.Max(clipY0, y); yy < System.Math.Min(clipY1, y + h); yy++)
                for (int xx = System.Math.Max(clipX0, x); xx < System.Math.Min(clipX1, x + w); xx++)
                    buf[yy * W + xx] = c;
        }

        /// <summary>1px 테두리(양 끝 포함 좌표).</summary>
        public void Box(int x0, int y0, int x1, int y1, Color32 c)
        {
            Fill(x0, y0, x1 - x0 + 1, 1, c);
            Fill(x0, y1, x1 - x0 + 1, 1, c);
            Fill(x0, y0, 1, y1 - y0 + 1, c);
            Fill(x1, y0, 1, y1 - y0 + 1, c);
        }

        void Over(int x, int y, Color32 s)
        {
            if (x < clipX0 || y < clipY0 || x >= clipX1 || y >= clipY1 || s.a == 0) return;
            int i = y * W + x;
            if (s.a == 255) { buf[i] = s; return; }
            Color32 d = buf[i];
            int a = s.a;
            buf[i] = new Color32((byte)((s.r * a + d.r * (255 - a)) / 255), (byte)((s.g * a + d.g * (255 - a)) / 255),
                (byte)((s.b * a + d.b * (255 - a)) / 255), 255);
        }

        public bool Has(string key) => imgs.ContainsKey(key);

        public void Blit(string key, int x, int y)
        {
            if (!imgs.TryGetValue(key, out var im)) return;
            BlitRegion(im, 0, 0, im.w, im.h, x, y, im.w, im.h);
        }

        /// <summary>그림의 일부(sx,sy,sw,sh)를 (x,y)에 그린다. 그림 밖은 건너뛴다. 큰 배경을 카메라로 잘라 그릴 때 쓴다.</summary>
        public void BlitPart(string key, int sx, int sy, int sw, int sh, int x, int y)
        {
            if (!imgs.TryGetValue(key, out var im)) return;
            for (int yy = 0; yy < sh; yy++)
            {
                int srcY = sy + yy;
                if (srcY < 0 || srcY >= im.h) continue;
                for (int xx = 0; xx < sw; xx++)
                {
                    int srcX = sx + xx;
                    if (srcX < 0 || srcX >= im.w) continue;
                    Over(x + xx, y + yy, im.px[srcY * im.w + srcX]);
                }
            }
        }

        /// <summary>가로·세로 1/2 정수 축소(홀수 칸 표본)로 그린다. 로고처럼 큰 그림을 줄일 때 쓴다.</summary>
        public void BlitHalf(string key, int x, int y)
        {
            if (!imgs.TryGetValue(key, out var im)) return;
            for (int yy = 0; yy < im.h / 2; yy++)
                for (int xx = 0; xx < im.w / 2; xx++)
                    Over(x + xx, y + yy, im.px[(yy * 2 + 1) * im.w + xx * 2 + 1]);
        }

        /// <summary>정수 배율 s로 확대해 그린다(최근접). 별 아이콘 등을 키울 때 쓴다.</summary>
        public void BlitScaled(string key, int x, int y, int s)
        {
            if (!imgs.TryGetValue(key, out var im)) return;
            for (int yy = 0; yy < im.h; yy++)
                for (int xx = 0; xx < im.w; xx++)
                {
                    Color32 c = im.px[yy * im.w + xx];
                    if (c.a == 0) continue;
                    for (int sy = 0; sy < s; sy++)
                        for (int sx = 0; sx < s; sx++)
                            Over(x + xx * s + sx, y + yy * s + sy, c);
                }
        }

        // ---------- 고정 층(매 프레임 다시 그리지 않는 배경) ----------

        Color32[] layer;

        /// <summary>지금 버퍼를 고정 층으로 저장한다. 이후 RestoreLayer로 한 번에 되돌린다.</summary>
        public void SaveLayer() => layer = (Color32[])buf.Clone();

        public bool HasLayer => layer != null;

        public void RestoreLayer() => System.Array.Copy(layer, buf, buf.Length);

        /// <summary>영역의 픽셀 내용을 하나의 수로 요약한다(자동 확인용: 그림이 바뀌었는지 비교).</summary>
        public int RegionHash(int x, int y, int w, int h)
        {
            unchecked
            {
                int hash = 17;
                for (int yy = y; yy < y + h; yy++)
                    for (int xx = x; xx < x + w; xx++)
                    {
                        Color32 c = buf[yy * W + xx];
                        hash = hash * 31 + (c.r | (c.g << 8) | (c.b << 16));
                    }
                return hash;
            }
        }

        public int WidthOf(string key) => imgs.TryGetValue(key, out var im) ? im.w : 0;
        public int HeightOf(string key) => imgs.TryGetValue(key, out var im) ? im.h : 0;

        // 원본 영역(sx,sy,sw,sh)을 목표 영역(dx,dy,dw,dh)에 반복해서 깐다(늘리지 않음 = 타일 방식).
        void BlitRegion(Img im, int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh)
        {
            if (sw <= 0 || sh <= 0) return;
            for (int y = 0; y < dh; y++)
                for (int x = 0; x < dw; x++)
                    Over(dx + x, dy + y, im.px[(sy + y % sh) * im.w + sx + x % sw]);
        }

        /// <summary>9-slice: 모서리는 그대로, 변과 가운데는 타일 반복(점 무늬 줄이 늘어나지 않게).</summary>
        public void Nine(string key, int x, int y, int w, int h)
        {
            if (!imgs.TryGetValue(key, out var im)) return;
            int l = im.nl, t = im.nt, r = im.nr, b = im.nb;
            int cw = im.w - l - r, ch = im.h - t - b;
            int mw = w - l - r, mh = h - t - b;
            BlitRegion(im, 0, 0, l, t, x, y, l, t);
            BlitRegion(im, l, 0, cw, t, x + l, y, mw, t);
            BlitRegion(im, im.w - r, 0, r, t, x + w - r, y, r, t);
            BlitRegion(im, 0, t, l, ch, x, y + t, l, mh);
            BlitRegion(im, l, t, cw, ch, x + l, y + t, mw, mh);
            BlitRegion(im, im.w - r, t, r, ch, x + w - r, y + t, r, mh);
            BlitRegion(im, 0, im.h - b, l, b, x, y + h - b, l, b);
            BlitRegion(im, l, im.h - b, cw, b, x + l, y + h - b, mw, b);
            BlitRegion(im, im.w - r, im.h - b, r, b, x + w - r, y + h - b, r, b);
        }

        // ---------- 글자(5x7 비트맵) ----------

        public static int TextWidth(string s, int scale = 1) => string.IsNullOrEmpty(s) ? 0 : (s.Length * 6 - 1) * scale;

        /// <summary>대문자로 바꿔 그린다. 없는 글자는 '?'. 반환값은 그린 폭.</summary>
        public int Text(int x, int y, string s, Color32 c, int scale = 1)
        {
            if (atlas == null || string.IsNullOrEmpty(s)) return 0;
            s = s.ToUpperInvariant();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '_' && !glyphs.ContainsKey('_'))
                {
                    // 글꼴 아틀라스에 아직 없는 '_'(pixfont.py 에는 있음): 맨 아래 줄로 임시 그림
                    Fill(x + i * 6 * scale, y + 6 * scale, 5 * scale, scale, c);
                    continue;
                }
                if (!glyphs.TryGetValue(s[i], out var g) && !glyphs.TryGetValue('?', out g)) continue;
                for (int gy = 0; gy < g.h; gy++)
                    for (int gx = 0; gx < g.w; gx++)
                    {
                        if (atlas.px[(g.y + gy) * atlas.w + g.x + gx].a == 0) continue;
                        Fill(x + (i * 6 + gx) * scale, y + gy * scale, scale, scale, c);
                    }
            }
            return TextWidth(s, scale);
        }

        // ---------- 출력 ----------

        public void Apply()
        {
            for (int y = 0; y < H; y++)
                System.Array.Copy(buf, y * W, flipped, (H - 1 - y) * W, W);
            Texture.SetPixels32(flipped);
            Texture.Apply(false);
        }

        /// <summary>현재 캔버스를 PNG로 저장한다(scale배 최근접 확대).</summary>
        public void SavePng(string path, int scale = 1)
        {
            var tex = new Texture2D(W * scale, H * scale, TextureFormat.RGBA32, false);
            var px = new Color32[W * scale * H * scale];
            for (int y = 0; y < H * scale; y++)
                for (int x = 0; x < W * scale; x++)
                    px[(H * scale - 1 - y) * W * scale + x] = buf[(y / scale) * W + x / scale];
            tex.SetPixels32(px);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
