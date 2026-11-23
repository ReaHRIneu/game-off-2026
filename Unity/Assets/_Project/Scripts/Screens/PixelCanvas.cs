using System.Collections.Generic;
using System.IO;
using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 도트 캔버스(GPU, W-009). 논리 좌표는 640x360을 그대로 쓰고, 내부에서 4배(Z)해 2560x1440 RenderTexture에 그린다(D-075).
    /// 4배 그림(anim/ 시트, 이름에 128이 들어가거나 _4x로 끝나는 그림)은 논리 좌표 1 = 실제 4px 기준 1:1로 그리고,
    /// 기존 32px 계열 그림은 4배 Point(최근접) 확대로 그린다. 같은 이름 뒤에 _4x를 붙인 그림이 있으면 그것을 먼저 쓴다 [제안].
    /// 그리기 호출은 모아 두었다가 Apply(또는 읽기·층 복사) 때 한 번에 GPU로 보낸다.
    /// 화면 출력(레터박스·Bilinear)은 GameView가 Output을 그려 처리한다(D-076).
    /// </summary>
    public sealed class PixelCanvas
    {
        public const int W = 640, H = 360;
        /// <summary>논리 좌표 1 = 실제 Z px.</summary>
        public const int Z = 4;
        public const int RW = W * Z, RH = H * Z;

        sealed class Img
        {
            public Texture2D tex;
            public int w, h;
            public int px;                  // 그림 1픽셀이 RT에서 차지하는 px: 기존 32px 계열 4(확대), 4배 그림 1(1:1)
            public int nl, nt, nr, nb;
            public int frameW, frameH, frames, fps;
            public int[] holds;
            public bool loop;
        }

        struct Quad
        {
            public float x0, y0, x1, y1;        // RT px, 왼쪽 위 원점
            public float l0x, l0y, l1x, l1y;    // 타일 좌표(타일이면 0..n, 아니면 0..1)
            public float tile;
            public Vector4 rect;                // (u0, vTop, du, dv): uv = rect.xy + l * rect.zw
            public Color col;
        }

        readonly Dictionary<string, Img> imgs = new Dictionary<string, Img>();
        readonly Dictionary<int, GlyphEntry> glyphs = new Dictionary<int, GlyphEntry>();
        readonly Img atlas;           // 글꼴 아틀라스(마스크 텍스처 기준)
        readonly Texture2D mask;
        readonly Material mat;
        readonly List<Quad> quads = new List<Quad>(4096);
        Texture batchTex;
        RenderTexture rt, layerRt, fxRt;
        bool hasLayer;
        readonly Material fxMat;       // 글리치 후처리(W-013). 셰이더가 없으면 null
        bool glitchApplied;            // 이번 프레임 Apply가 글리치 결과를 fxRt에 그렸는지
        GlitchFrame gf;

        static readonly int IdRes = Shader.PropertyToID("_Res"), IdRectA = Shader.PropertyToID("_RectA"), IdRectB = Shader.PropertyToID("_RectB"),
            IdShift = Shader.PropertyToID("_Shift"), IdCopy = Shader.PropertyToID("_Copy"), IdFx = Shader.PropertyToID("_Fx"), IdTick = Shader.PropertyToID("_Tick"),
            IdColR = Shader.PropertyToID("_ColR"), IdColC = Shader.PropertyToID("_ColC"), IdColW = Shader.PropertyToID("_ColW"), IdColA = Shader.PropertyToID("_ColA");
        const float BrightThreshold = 0.30f;   // 복사할 "밝은 픽셀"의 선형 밝기 기준 [제안]

        /// <summary>
        /// 그려진 2560x1440 결과(Bilinear). 화면 출력은 이것을 레터박스로 그린다.
        /// 글리치가 재생 중이면 글리치를 입힌 복사본(fxRt), 아니면 원본이다. 원본(rt)은 항상 글리치 없는 그림으로 남는다(고정 층·RegionHash 기준).
        /// </summary>
        public RenderTexture Output => glitchApplied ? fxRt : rt;

        public PixelCanvas(ArtSet art, Shader glitchShader = null)
        {
            var shader = Shader.Find("GameOff/PixelQuad");
            mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (glitchShader != null) fxMat = new Material(glitchShader) { hideFlags = HideFlags.HideAndDontSave };
            CreateTargets();
            if (art == null) return;
            foreach (var e in art.entries)
            {
                if (e.texture == null) continue;
                bool hi = e.frames > 0 || e.key.StartsWith("anim/") || e.key.Contains("128") || e.key.EndsWith("_4x");
                imgs[e.key] = new Img
                {
                    tex = e.texture, w = e.texture.width, h = e.texture.height, px = hi ? 1 : Z,
                    nl = e.nl, nt = e.nt, nr = e.nr, nb = e.nb,
                    frameW = e.frameW, frameH = e.frameH, frames = e.frames, fps = e.fps, holds = e.holds, loop = e.loop,
                };
            }
            foreach (var g in art.glyphs) glyphs[g.code] = g;
            if (imgs.TryGetValue("ui/font5x7_atlas", out var a))
            {
                atlas = a;
                // 글자 모양만 쓰는 마스크: 알파가 있는 칸은 흰색 불투명, 나머지는 투명. 색은 정점 색으로 입힌다.
                var src = a.tex.GetPixels32();
                for (int i = 0; i < src.Length; i++) src[i] = src[i].a == 0 ? new Color32(255, 255, 255, 0) : new Color32(255, 255, 255, 255);
                mask = new Texture2D(a.w, a.h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                mask.SetPixels32(src);
                mask.Apply(false);
            }
        }

        void CreateTargets()
        {
            rt = new RenderTexture(RW, RH, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                useMipMap = true, autoGenerateMips = true,   // 화면이 작아질 때 Bilinear가 알맞은 단계를 고른다
            };
            rt.Create();
            layerRt = new RenderTexture(RW, RH, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            layerRt.Create();
            if (fxMat != null)
            {
                // 글리치 결과용 새 렌더 텍스처 1장(W-013). 출력용이라 rt와 같은 Bilinear·밉맵 설정
                fxRt = new RenderTexture(RW, RH, 0, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                    useMipMap = true, autoGenerateMips = true,
                };
                fxRt.Create();
            }
            glitchApplied = false;
            hasLayer = false;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = null;
        }

        /// <summary>RenderTexture 내용이 사라졌으면(장치 손실 등) 다시 만들고 true를 돌려준다. 호출한 쪽이 화면을 다시 그린다.</summary>
        public bool CheckLost()
        {
            if (rt != null && rt.IsCreated() && layerRt.IsCreated() && (fxRt == null || fxRt.IsCreated())) return false;
            quads.Clear();
            batchTex = null;
            if (rt != null) rt.Release();
            if (layerRt != null) layerRt.Release();
            if (fxRt != null) fxRt.Release();
            CreateTargets();
            return true;
        }

        static Color Lin(Color32 c) => ((Color)c).linear;   // 선형 색 공간 프로젝트: 정점 색은 선형으로 넘긴다

        Img Find(string key)
        {
            if (imgs.TryGetValue(key + "_4x", out var im)) return im;
            return imgs.TryGetValue(key, out im) ? im : null;
        }

        // ---------- 그리기 묶음 ----------

        void Add(Texture tex, float dx, float dy, float dw, float dh, bool tile, float tilesX, float tilesY, Vector4 rect, Color col, float loX = 0f, float loY = 0f)
        {
            if (dw <= 0f || dh <= 0f) return;
            float cx0 = clipX0 * Z, cy0 = clipY0 * Z, cx1 = clipX1 * Z, cy1 = clipY1 * Z;   // 그리기 제한(논리 -> RT px)
            if (pxClipOn)
            {
                // W-011: RT px 단위 추가 제한(창 구멍 안에만 그리기). 논리 제한과 겹치는 부분만 남긴다
                cx0 = Mathf.Max(cx0, pcx0); cy0 = Mathf.Max(cy0, pcy0); cx1 = Mathf.Min(cx1, pcx1); cy1 = Mathf.Min(cy1, pcy1);
            }            if (dx + dw <= cx0 || dy + dh <= cy0 || dx >= cx1 || dy >= cy1) return;
            float fx0 = Mathf.Max(0f, (cx0 - dx) / dw), fx1 = Mathf.Min(1f, (cx1 - dx) / dw);
            float fy0 = Mathf.Max(0f, (cy0 - dy) / dh), fy1 = Mathf.Min(1f, (cy1 - dy) / dh);
            float lx = tile ? tilesX : 1f, ly = tile ? tilesY : 1f;
            if (tex != batchTex) { Flush(); batchTex = tex; }
            quads.Add(new Quad
            {
                x0 = dx + fx0 * dw, y0 = dy + fy0 * dh, x1 = dx + fx1 * dw, y1 = dy + fy1 * dh,
                l0x = fx0 * lx + loX, l1x = fx1 * lx + loX, l0y = fy0 * ly + loY, l1y = fy1 * ly + loY,
                tile = tile ? 1f : 0f, rect = rect, col = col,
            });
        }

        void Flush()
        {
            if (quads.Count == 0) return;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            mat.SetTexture("_MainTex", batchTex);
            mat.SetVector("_Res", new Vector4(RW, RH, 0, 0));
            mat.SetPass(0);
            GL.Begin(GL.QUADS);
            for (int i = 0; i < quads.Count; i++)
            {
                Quad q = quads[i];
                GL.Color(q.col);
                Vert(q, q.x0, q.y0, q.l0x, q.l0y);
                Vert(q, q.x1, q.y0, q.l1x, q.l0y);
                Vert(q, q.x1, q.y1, q.l1x, q.l1y);
                Vert(q, q.x0, q.y1, q.l0x, q.l1y);
            }
            GL.End();
            RenderTexture.active = prev;
            quads.Clear();
        }

        static void Vert(Quad q, float x, float y, float lx, float ly)
        {
            GL.MultiTexCoord3(0, lx, ly, q.tile);
            GL.MultiTexCoord3(1, q.rect.x, q.rect.y, 0f);
            GL.MultiTexCoord3(2, q.rect.z, q.rect.w, 0f);
            GL.Vertex3(x, y, 0f);
        }

        /// <summary>그림의 일부(sx,sy,sw,sh: 그림 px, 왼쪽 위 기준)를 RT px 영역에 그린다. tile이면 반복해서 깐다(늘리지 않음).</summary>
        void DrawImg(Img im, int sx, int sy, int sw, int sh, float dx, float dy, float dw, float dh, bool tile)
        {
            if (sw <= 0 || sh <= 0) return;
            var rect = new Vector4(sx / (float)im.w, 1f - sy / (float)im.h, sw / (float)im.w, -sh / (float)im.h);
            float tx = tile ? dw / (sw * im.px) : 1f, ty = tile ? dh / (sh * im.px) : 1f;
            Add(im.tex, dx, dy, dw, dh, tile, tx, ty, rect, Color.white);
        }

        // ---------- 기본 그리기 ----------

        public void Clear(Color32 c)
        {
            quads.Clear();
            batchTex = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Lin(c));
            RenderTexture.active = prev;
        }

        // 그리기 제한 영역(논리 좌표). 기본은 캔버스 전체.
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

        static readonly Vector4 FullRect = new Vector4(0f, 1f, 1f, -1f);

        // ---------- RT px 단위 그리기(W-011 환경 이펙트) ----------
        // 방 그림(128px 규격)의 1px = RT 1px이다. 입자·디더 점은 2px 격자에 놓여 논리 좌표(4px)로는 표현할 수 없어 RT px로 직접 그린다.

        int pcx0, pcy0, pcx1, pcy1;
        bool pxClipOn;

        /// <summary>이후의 그리기를 RT px 사각형(x0,y0 ~ x1,y1 미포함) 안에만 하도록 추가로 제한한다. 논리 제한(SetClip)과 함께 적용된다.</summary>
        public void SetClipPx(int x0, int y0, int x1, int y1)
        {
            pcx0 = x0; pcy0 = y0; pcx1 = x1; pcy1 = y1;
            pxClipOn = true;
        }

        public void ClearClipPx()
        {
            pxClipOn = false;
        }

        /// <summary>RT px 좌표·크기로 단색 사각형을 채운다.</summary>
        public void FillPx(int x, int y, int w, int h, Color32 c)
        {
            Color col = Lin(c);
            col.a = 1f;
            Add(Texture2D.whiteTexture, x, y, w, h, false, 1f, 1f, FullRect, col);
        }

        /// <summary>그림을 RT px 좌표에 그린다(왼쪽 위 기준).</summary>
        public void BlitPx(string key, int x, int y)
        {
            Img im = Find(key);
            if (im == null) return;
            DrawImg(im, 0, 0, im.w, im.h, x, y, im.w * im.px, im.h * im.px, false);
        }

        /// <summary>그림의 일부(sx,sy,sw,sh: 그림 px)를 RT px 좌표에 그린다. 여러 모양을 한 장에 모은 그림(환경 입자 아틀라스)에서 하나를 꺼낼 때 쓴다.</summary>
        public void BlitPartPx(string key, int sx, int sy, int sw, int sh, int x, int y)
        {
            Img im = Find(key);
            if (im == null) return;
            DrawImg(im, sx, sy, sw, sh, x, y, sw * im.px, sh * im.px, false);
        }

        /// <summary>
        /// 작은 반복 그림(디더 타일)으로 RT px 사각형을 깐다. 타일 무늬는 (originX, originY)에서 시작하는 격자에 맞춘다:
        /// 방 왼쪽 위를 원점으로 주면 어느 부분을 그려도 무늬가 이어진다.
        /// </summary>
        public void TilePx(string key, int x, int y, int w, int h, int originX, int originY)
        {
            Img im = Find(key);
            if (im == null || w <= 0 || h <= 0) return;
            float tw = im.w * im.px, th = im.h * im.px;
            var rect = new Vector4(0f, 1f, 1f, -1f);
            Add(im.tex, x, y, w, h, true, w / tw, h / th, rect, Color.white, (x - originX) / tw, (y - originY) / th);
        }

        /// <summary>코드로 만든 텍스처를 그림 키로 등록한다(디더 타일·구름 모양). 이미 있으면 그대로 두고 false. px는 그림 1픽셀이 RT에서 차지하는 px(1 = 1:1).</summary>
        public bool RegisterTexture(string key, Texture2D tex, int px = 1)
        {
            if (imgs.ContainsKey(key)) return false;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            imgs[key] = new Img { tex = tex, w = tex.width, h = tex.height, px = px };
            return true;
        }

        /// <summary>
        /// 이미 등록된 그림을 색 변환해 새 키로 등록한다(낮의 도시: 밝기 3단으로 다시 칠함). 원본 텍스처를 읽을 수 있어야 한다.
        /// 이미 있거나 원본이 없으면 false. 한 번만 만들어 두고 계속 쓴다(프레임마다 만들지 않음).
        /// </summary>
        public bool RegisterRecolor(string srcKey, string dstKey, System.Func<Color32, Color32> map)
        {
            if (imgs.ContainsKey(dstKey)) return false;
            if (!imgs.TryGetValue(srcKey, out Img src) || src.tex == null || !src.tex.isReadable) return false;
            Color32[] px = src.tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
                if (px[i].a != 0) px[i] = map(px[i]);
            var tex = new Texture2D(src.w, src.h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply(false);
            tex.filterMode = src.tex.filterMode;
            tex.wrapMode = TextureWrapMode.Clamp;
            imgs[dstKey] = new Img { tex = tex, w = src.w, h = src.h, px = src.px };
            return true;
        }

        public void Fill(int x, int y, int w, int h, Color32 c)
        {
            Color col = Lin(c);
            col.a = 1f;   // 이전 CPU 방식과 같이 Fill은 알파 없이 덮어쓴다
            Add(Texture2D.whiteTexture, x * Z, y * Z, w * Z, h * Z, false, 1f, 1f, FullRect, col);
        }

        /// <summary>알파가 있는 단색 사각형(논리 좌표, W-028 반투명 연출). alpha 0~1.</summary>
        public void FillAlpha(int x, int y, int w, int h, Color32 c, float alpha)
        {
            Color col = Lin(c);
            col.a = Mathf.Clamp01(alpha);
            Add(Texture2D.whiteTexture, x * Z, y * Z, w * Z, h * Z, false, 1f, 1f, FullRect, col);
        }

        /// <summary>
        /// W-028 연출용 작은 반복 무늬를 코드로 한 번만 등록한다(그림 파일 없음). 키: fx/dash_h_rd·fx/dash_v_rd(빨간 점선 1px, 한 칸 걸러 한 칸),
        /// fx/dith25_cy·fx/dith50_cy·fx/dith75_cy(밝은 청사진 파랑 디더 2x2 무늬). 1픽셀 = 논리 1px.
        /// </summary>
        public void EnsureFxTextures()
        {
            if (imgs.ContainsKey("fx/dash_h_rd")) return;
            var on = new Color32(240, 72, 102, 255);
            var off = new Color32(0, 0, 0, 0);
            RegisterTexture("fx/dash_h_rd", MakeTex(2, 1, new[] { on, off }), Z);
            RegisterTexture("fx/dash_v_rd", MakeTex(1, 2, new[] { on, off }), Z);
            var cy = new Color32(140, 210, 255, 255);
            RegisterTexture("fx/dith25_cy", MakeTex(2, 2, new[] { cy, off, off, off }), Z);
            RegisterTexture("fx/dith50_cy", MakeTex(2, 2, new[] { cy, off, off, cy }), Z);
            RegisterTexture("fx/dith75_cy", MakeTex(2, 2, new[] { cy, cy, cy, off }), Z);
        }

        static Texture2D MakeTex(int w, int h, Color32[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>1px 테두리(양 끝 포함 좌표).</summary>
        public void Box(int x0, int y0, int x1, int y1, Color32 c)
        {
            Fill(x0, y0, x1 - x0 + 1, 1, c);
            Fill(x0, y1, x1 - x0 + 1, 1, c);
            Fill(x0, y0, 1, y1 - y0 + 1, c);
            Fill(x1, y0, 1, y1 - y0 + 1, c);
        }

        public bool Has(string key) => Find(key) != null;

        public void Blit(string key, int x, int y)
        {
            Img im = Find(key);
            if (im == null) return;
            DrawImg(im, 0, 0, im.w, im.h, x * Z, y * Z, im.w * im.px, im.h * im.px, false);
        }

        /// <summary>그림의 일부(sx,sy,sw,sh: 논리 좌표 크기)를 (x,y)에 그린다. 그림 밖은 건너뛴다. 큰 배경을 카메라로 잘라 그릴 때 쓴다.</summary>
        public void BlitPart(string key, int sx, int sy, int sw, int sh, int x, int y)
        {
            Img im = Find(key);
            if (im == null) return;
            int lpx = Z / im.px;   // 그림 px / 논리 px (기존 그림 1, 4배 그림 4)
            int ix = sx * lpx, iy = sy * lpx, iw = sw * lpx, ih = sh * lpx;
            int cx0 = System.Math.Max(0, ix), cy0 = System.Math.Max(0, iy);
            int cx1 = System.Math.Min(im.w, ix + iw), cy1 = System.Math.Min(im.h, iy + ih);
            if (cx1 <= cx0 || cy1 <= cy0) return;
            float dx = x * Z + (cx0 - ix) * im.px, dy = y * Z + (cy0 - iy) * im.px;
            DrawImg(im, cx0, cy0, cx1 - cx0, cy1 - cy0, dx, dy, (cx1 - cx0) * im.px, (cy1 - cy0) * im.px, false);
        }

        /// <summary>가로·세로 1/2 크기로 그린다(논리 좌표 기준). 로고처럼 큰 그림을 줄일 때 쓴다. 이전의 홀수 칸 표본 대신 그림 전체를 2배 확대(Point)한다.</summary>
        public void BlitHalf(string key, int x, int y)
        {
            Img im = Find(key);
            if (im == null) return;
            DrawImg(im, 0, 0, im.w, im.h, x * Z, y * Z, im.w * im.px / 2f, im.h * im.px / 2f, false);
        }

        /// <summary>정수 배율 s로 확대해 그린다(최근접). 별 아이콘 등을 키울 때 쓴다.</summary>
        public void BlitScaled(string key, int x, int y, int s)
        {
            Img im = Find(key);
            if (im == null) return;
            DrawImg(im, 0, 0, im.w, im.h, x * Z, y * Z, im.w * im.px * s, im.h * im.px * s, false);
        }

        // ---------- 애니메이션 시트 ----------

        /// <summary>격자 시트(한 줄에 시트 폭 / 프레임 폭 칸, 왼쪽 위부터)의 한 프레임(0부터)을 (x,y)에 그린다.
        /// Web 최대 텍스처 크기(2048)를 넘지 않도록 시트는 여러 줄로 내보낸다. 한 줄 시트도 그대로 읽힌다.</summary>
        public void BlitFrame(string key, int frame, int x, int y)
        {
            Img im = Find(key);
            if (im == null || im.frames <= 0) return;
            frame = Mathf.Clamp(frame, 0, im.frames - 1);
            int cols = Mathf.Max(1, im.w / im.frameW);
            int sx = (frame % cols) * im.frameW, sy = (frame / cols) * im.frameH;
            DrawImg(im, sx, sy, im.frameW, im.frameH, x * Z, y * Z, im.frameW * im.px, im.frameH * im.px, false);
        }

        public bool IsSheet(string key) { var im = Find(key); return im != null && im.frames > 0; }
        public int FrameCount(string key) { var im = Find(key); return im == null ? 0 : im.frames; }
        public bool Loops(string key) { var im = Find(key); return im != null && im.loop; }

        static int HoldOf(Img im, int i) => im.holds != null && i < im.holds.Length ? System.Math.Max(1, im.holds[i]) : 1;

        static int TotalTicks(Img im)
        {
            int t = 0;
            for (int i = 0; i < im.frames; i++) t += HoldOf(im, i);
            return t;
        }

        /// <summary>한 번 재생하는 데 걸리는 시간(초). holds가 없으면 프레임당 1틱, 1틱 = 1/fps초.</summary>
        public float SheetSeconds(string key)
        {
            var im = Find(key);
            return im == null || im.frames <= 0 || im.fps <= 0 ? 0f : TotalTicks(im) / (float)im.fps;
        }

        /// <summary>시작 후 seconds초 때 보여 줄 프레임 번호. 반복이 아니면 마지막 프레임에서 멈춘다.</summary>
        public int FrameAt(string key, float seconds)
        {
            var im = Find(key);
            if (im == null || im.frames <= 0) return 0;
            int total = TotalTicks(im);
            int tick = Mathf.Max(0, Mathf.FloorToInt(seconds * System.Math.Max(1, im.fps)));
            tick = im.loop ? tick % total : System.Math.Min(tick, total - 1);
            for (int i = 0; i < im.frames; i++)
            {
                tick -= HoldOf(im, i);
                if (tick < 0) return i;
            }
            return im.frames - 1;
        }

        // ---------- 고정 층(매 프레임 다시 그리지 않는 배경) ----------

        /// <summary>지금 그림을 고정 층으로 저장한다(RenderTexture 복사). 이후 RestoreLayer로 한 번에 되돌린다.</summary>
        public void SaveLayer()
        {
            Flush();
            Graphics.Blit(rt, layerRt);
            hasLayer = true;
        }

        public bool HasLayer => hasLayer;

        public void RestoreLayer()
        {
            quads.Clear();   // 되돌리기 전에 모인 그림은 덮어쓰이므로 버린다
            batchTex = null;
            Graphics.Blit(layerRt, rt);
        }

        /// <summary>영역(논리 좌표)의 그림 내용을 하나의 수로 요약한다(자동 확인용: 그림이 바뀌었는지 비교). GPU 읽기라 가끔만 쓴다.</summary>
        public int RegionHash(int x, int y, int w, int h)
        {
            Flush();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w * Z, h * Z, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(x * Z, RH - (y + h) * Z, w * Z, h * Z), 0, 0, false);
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            Object.Destroy(tex);
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < px.Length; i++) hash = hash * 31 + (px[i].r | (px[i].g << 8) | (px[i].b << 16));
                return hash;
            }
        }

        public int WidthOf(string key) { var im = Find(key); return im == null ? 0 : im.w * im.px / Z; }
        public int HeightOf(string key) { var im = Find(key); return im == null ? 0 : im.h * im.px / Z; }

        /// <summary>9-slice: 모서리는 그대로, 변과 가운데는 타일 반복(점 무늬 줄이 늘어나지 않게). x,y,w,h는 논리 좌표.</summary>
        public void Nine(string key, int x, int y, int w, int h)
        {
            Img im = Find(key);
            if (im == null) return;
            int l = im.nl, t = im.nt, r = im.nr, b = im.nb;
            int cw = im.w - l - r, ch = im.h - t - b;
            int p = im.px;
            float X = x * Z, Y = y * Z, Wd = w * Z, Hd = h * Z;
            float mw = Wd - (l + r) * p, mh = Hd - (t + b) * p;
            Region(im, 0, 0, l, t, X, Y, l * p, t * p);
            Region(im, l, 0, cw, t, X + l * p, Y, mw, t * p);
            Region(im, im.w - r, 0, r, t, X + Wd - r * p, Y, r * p, t * p);
            Region(im, 0, t, l, ch, X, Y + t * p, l * p, mh);
            Region(im, l, t, cw, ch, X + l * p, Y + t * p, mw, mh);
            Region(im, im.w - r, t, r, ch, X + Wd - r * p, Y + t * p, r * p, mh);
            Region(im, 0, im.h - b, l, b, X, Y + Hd - b * p, l * p, b * p);
            Region(im, l, im.h - b, cw, b, X + l * p, Y + Hd - b * p, mw, b * p);
            Region(im, im.w - r, im.h - b, r, b, X + Wd - r * p, Y + Hd - b * p, r * p, b * p);
        }

        void Region(Img im, int sx, int sy, int sw, int sh, float dx, float dy, float dw, float dh)
        {
            bool tile = !Mathf.Approximately(dw, sw * im.px) || !Mathf.Approximately(dh, sh * im.px);
            DrawImg(im, sx, sy, sw, sh, dx, dy, dw, dh, tile);
        }

        // ---------- 글자(5x7 비트맵) ----------

        public static int TextWidth(string s, int scale = 1) => string.IsNullOrEmpty(s) ? 0 : (s.Length * 6 - 1) * scale;

        /// <summary>대문자로 바꿔 그린다. 없는 글자는 '?'. 반환값은 그린 폭.</summary>
        public int Text(int x, int y, string s, Color32 c, int scale = 1)
        {
            if (atlas == null || string.IsNullOrEmpty(s)) return 0;
            s = s.ToUpperInvariant();
            Color col = Lin(c);
            col.a = 1f;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '_' && !glyphs.ContainsKey('_'))
                {
                    // 글꼴 아틀라스에 아직 없는 '_'(pixfont.py 에는 있음): 맨 아래 줄로 임시 그림
                    Fill(x + i * 6 * scale, y + 6 * scale, 5 * scale, scale, c);
                    continue;
                }
                if (s[i] == '→' && !glyphs.ContainsKey('→'))
                {
                    // 글꼴 아틀라스에 아직 없는 '→'(W-005 튜토리얼 키 배지용): 5x7 칸 안에 화살표를 직접 그림
                    int ax = x + i * 6 * scale;
                    Fill(ax, y + 3 * scale, 5 * scale, scale, c);
                    Fill(ax + 3 * scale, y + 2 * scale, scale, scale, c);
                    Fill(ax + 3 * scale, y + 4 * scale, scale, scale, c);
                    Fill(ax + 2 * scale, y + 1 * scale, scale, scale, c);
                    Fill(ax + 2 * scale, y + 5 * scale, scale, scale, c);
                    continue;
                }
                if (s[i] == '←' && !glyphs.ContainsKey('←'))
                {
                    // 글꼴 아틀라스에 없는 '←'(W-007 튜토리얼 키 배지용): '→'의 좌우 반전을 직접 그림
                    int ax = x + i * 6 * scale;
                    Fill(ax, y + 3 * scale, 5 * scale, scale, c);
                    Fill(ax + 1 * scale, y + 2 * scale, scale, scale, c);
                    Fill(ax + 1 * scale, y + 4 * scale, scale, scale, c);
                    Fill(ax + 2 * scale, y + 1 * scale, scale, scale, c);
                    Fill(ax + 2 * scale, y + 5 * scale, scale, scale, c);
                    continue;
                }
                if (s[i] == '+' && !glyphs.ContainsKey('+'))
                {
                    // 글꼴 아틀라스에 없는 '+'(W-019 조작법 창 L-SHIFT + ARROW): 5x7 칸 안에 십자를 직접 그림
                    int ax = x + i * 6 * scale;
                    Fill(ax, y + 3 * scale, 5 * scale, scale, c);
                    Fill(ax + 2 * scale, y + 1 * scale, scale, 5 * scale, c);
                    continue;
                }
                if (s[i] == '=' && !glyphs.ContainsKey('='))
                {
                    // 글꼴 아틀라스에 없는 '='(W-007 미션 이름 TRACE <= 40): 5x7 칸 안에 가로선 두 줄을 직접 그림
                    int ax = x + i * 6 * scale;
                    Fill(ax, y + 2 * scale, 5 * scale, scale, c);
                    Fill(ax, y + 4 * scale, 5 * scale, scale, c);
                    continue;
                }
                if (!glyphs.TryGetValue(s[i], out var g) && !glyphs.TryGetValue('?', out g)) continue;
                var rect = new Vector4(g.x / (float)atlas.w, 1f - g.y / (float)atlas.h, g.w / (float)atlas.w, -g.h / (float)atlas.h);
                Add(mask, (x + i * 6 * scale) * Z, y * Z, g.w * scale * Z, g.h * scale * Z, false, 1f, 1f, rect, col);
            }
            return TextWidth(s, scale);
        }

        // ---------- 출력 ----------

        /// <summary>모아 둔 그리기를 RenderTexture에 반영한다. 화면에는 Output을 그린다.</summary>
        public void Apply()
        {
            Flush();
            glitchApplied = false;
            if (fxMat == null || !Glitch.TryGetFrame(ref gf)) return;   // 재생 중인 글리치가 없으면 후처리를 건너뛴다
            fxMat.SetVector(IdRes, new Vector4(RW, RH, 1f / RW, 1f / RH));
            fxMat.SetVector(IdRectA, gf.rectA); fxMat.SetVector(IdRectB, gf.rectB);
            fxMat.SetVector(IdShift, gf.shift);
            fxMat.SetVector(IdCopy, new Vector4(gf.copy.x, gf.copy.y, gf.copy.z, BrightThreshold));
            fxMat.SetVector(IdFx, gf.fx);
            fxMat.SetFloat(IdTick, gf.tick);
            fxMat.SetColor(IdColR, Lin(PalR)); fxMat.SetColor(IdColC, Lin(PalC)); fxMat.SetColor(IdColW, Lin(PalW)); fxMat.SetColor(IdColA, Lin(PalA));
            Graphics.Blit(rt, fxRt, fxMat);
            glitchApplied = true;
        }

        // 글리치가 쓰는 팔레트 값(tools/art/artkit.py PIX의 R, C, W, a와 같다)
        static readonly Color32 PalR = new Color32(240, 72, 102, 255), PalC = new Color32(140, 210, 255, 255),
            PalW = new Color32(236, 246, 255, 255), PalA = new Color32(10, 10, 24, 255);

        /// <summary>현재 캔버스를 PNG로 저장한다(논리 크기의 scale배; 4면 2560x1440 원본, 그 밖은 Bilinear 확대·축소).</summary>
        public void SavePng(string path, int scale = 1)
        {
            SaveSizedPng(path, W * scale, H * scale);
        }

        /// <summary>
        /// 16:9 화면 크기(width x height)로 맞춰 PNG로 저장한다. 게임 화면 출력(GameView.DrawCanvas)과 같은 Bilinear 확대·축소이며
        /// 1920x1080, 2560x1440 같은 16:9 크기용이다.
        /// </summary>
        public void SaveSizedPng(string path, int width, int height, bool withGlitch = false)
        {
            Flush();
            var tmp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(withGlitch ? Output : rt, tmp);
            var prev = RenderTexture.active;
            RenderTexture.active = tmp;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            tex.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tmp);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
