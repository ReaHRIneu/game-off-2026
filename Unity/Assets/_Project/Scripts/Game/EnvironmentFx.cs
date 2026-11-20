using System;
using System.Collections.Generic;
using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 바깥 환경 이펙트(W-011, D-103, Docs/Art/environment-fx.md): 하늘색(L0)·환경 입자(LE)·창 유리 물방울(L1+)·실내 어둠과 창 빛(L3).
    /// 입자는 의뢰 시작 때 고정 크기 struct 배열(Parts) 하나로 한 번만 만들고 프레임마다 새로 할당하지 않는다(web-optimization §4).
    /// 위치는 창 그림(128px) 기준 px이며, 시간(Time)에서 직접 계산해 2px 격자에 붙인다. 시간은 Advance(dt)로만 흐르므로
    /// 일시정지 중에는 멈추고, 같은 시드·같은 시간이면 같은 위치가 된다(의뢰마다 고정 시드).
    /// 그림은 PixelCanvas의 RT px 그리기(FillPx·BlitPx·TilePx)만 쓴다 — 새 셰이더 없음.
    /// </summary>
    public sealed class EnvironmentFx
    {
        public const byte KRain = 1, KDrip = 2, KSnow = 3, KCloud = 4, KFog = 5;

        /// <summary>입자 하나. 고정 크기 값 형식이라 배열 하나로 보관한다. sx·sy는 창 왼쪽 위 기준 px로 2의 배수(2px 격자).</summary>
        public struct Particle
        {
            public byte kind;
            public byte variant;       // 눈: 1이면 큰 점, 구름: 모양 번호
            public short len;          // 물방울 길이, 안개 띠 길이
            public int baseX, baseY;
            public float spd;          // 초당 px(30 FPS 기준 프레임당 px x 30)
            public float phase;
            public int sx, sy;
        }

        /// <summary>창(구멍) 하나. 위치·크기는 방 그림 px(layout.json windows).</summary>
        public struct Slot
        {
            public int floor, col;
            public int wx, wy, ww, wh;
            public int first, count;
            public bool openAir;
        }

        // 창 1개 기준(environment-fx §3, 208 x 144)
        const float RefArea = 208f * 144f;
        const int CloudShapes = 6;

        public readonly EnvPreset Preset;
        public readonly EnvironmentSet Set;
        public readonly int Seed;
        public readonly Particle[] Parts;
        public readonly Slot[] Slots;
        /// <summary>도시 배경 그림 키(뒤에서 앞으로). 낮이면 3단 색으로 다시 칠한 그림.</summary>
        public readonly string[] CityKeys;

        readonly int sectorCols;
        readonly int[] sectorFirstSlot, sectorSlotCount;
        readonly int sectorW, ceilY, floorY;
        readonly string ambientTile, beamTile, beamBottomTile, skyChecker;
        readonly string[] fogTiles = new string[3];
        // 구름 6가지 + 빗줄기를 한 장에 모은 그림(텍스처 전환 없이 한 번에 그리려고, 성능 규칙 environment-fx §4)
        string atlasKey;
        int rainSrcY;
        readonly int[] cloudSrcY = new int[CloudShapes];
        readonly int[] cloudW = new int[CloudShapes], cloudH = new int[CloudShapes];
        readonly Color32 rainColor;
        readonly bool hasRain, hasDrips, hasSnow, hasClouds, hasFog;

        /// <summary>환경 시간(초). Advance로만 흐른다.</summary>
        public double Time { get; private set; }

        // ---------- 프레임 상태(자동 확인용으로 열어 둔다) ----------
        bool half, reduced;
        int viewX0, viewY0, viewX1, viewY1;
        int seqLe, seqGlass, seqSecond;
        long drawTicks;
        public int LeDrawn { get; private set; }
        public int GlassDrawn { get; private set; }
        public int SecondDrawn { get; private set; }
        /// <summary>이번 프레임 환경 그리기에 쓴 시간(ms, CPU 제출 기준).</summary>
        public double DrawMs => drawTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        /// <summary>이번 프레임에 하늘을 그렸는가, 그린 위·아래 색(자동 확인용).</summary>
        public bool SkyDrawn { get; private set; }
        public Color32 LastSkyTop { get; private set; }
        public Color32 LastSkyBottom { get; private set; }

        // 환경 어둠에서 창을 빼는 사각형 작업 공간(프레임마다 새로 만들지 않음)
        readonly int[] rects = new int[4 * 32];
        readonly int[] rectsTmp = new int[4 * 32];

        public EnvironmentFx(PixelCanvas cv, Board board, EnvPreset preset, EnvironmentSet set)
        {
            Preset = preset;
            Set = set;
            Seed = board.EnvSeed;
            SceneLayout lay = board.Layout;
            sectorCols = lay.Cols;
            sectorW = lay.SectorPxW;
            ceilY = lay.CeilYPx;
            floorY = lay.FloorYPx;
            hasRain = (preset.particles & EnvParticleKinds.Rain) != 0;
            hasDrips = (preset.particles & EnvParticleKinds.Drips) != 0;
            hasSnow = (preset.particles & EnvParticleKinds.Snow) != 0;
            hasClouds = (preset.particles & EnvParticleKinds.Clouds) != 0;
            hasFog = (preset.particles & EnvParticleKinds.Fog) != 0;
            rainColor = preset.particleColor;

            // 타일·모양 그림은 한 번만 만들어 캔버스에 등록한다
            ambientTile = preset.ambientDenominator > 0 ? EnsureTile(cv, preset.ambientDenominator, preset.ambientColor) : null;
            if (preset.beamDenominator > 0)
            {
                beamTile = EnsureTile(cv, preset.beamDenominator, preset.beamColor);
                beamBottomTile = EnsureTile(cv, Math.Max(4, preset.beamDenominator / 2), preset.beamColor);   // 바닥에 닿은 자국: 한 단계 진하게(최대 1/4)
            }
            skyChecker = EnsureTile(cv, 2, preset.skyBottom);
            if (hasFog) { fogTiles[0] = EnsureTile(cv, 16, rainColor); fogTiles[1] = EnsureTile(cv, 8, rainColor); fogTiles[2] = EnsureTile(cv, 4, rainColor); }
            if (hasClouds || hasRain) MakeAtlas(cv, preset.skyBottom, preset.skyTop, rainColor, hasClouds, hasRain);   // 구름 위는 밝은 색(하늘 아래색), 아래는 어두운 색(하늘 위색)
            CityKeys = MakeCityKeys(cv, preset.time == TimeOfDay.Day);

            // 창 목록: 섹터 순서대로. 방 종류가 같아도 섹터마다 따로 둔다
            int n = 0;
            for (int f = 0; f < lay.Floors; f++)
                for (int c = 0; c < lay.Cols; c++)
                {
                    SceneLayout.SectorView v = lay.Sector(f, c);
                    if (v != null && v.Room != null) n += v.Room.Windows.Length;
                }
            Slots = new Slot[n];
            sectorFirstSlot = new int[lay.Floors * lay.Cols];
            sectorSlotCount = new int[lay.Floors * lay.Cols];
            int si = 0;
            for (int f = 0; f < lay.Floors; f++)
                for (int c = 0; c < lay.Cols; c++)
                {
                    SceneLayout.SectorView v = lay.Sector(f, c);
                    int idx = f * lay.Cols + c;
                    sectorFirstSlot[idx] = si;
                    if (v == null || v.Room == null) continue;
                    foreach (RectInt w in v.Room.Windows)
                    {
                        Slots[si] = new Slot { floor = f, col = c, wx = w.x, wy = w.y, ww = w.width, wh = w.height, openAir = set.IsOpenAir(v.Room.Name) };
                        si++;
                        sectorSlotCount[idx]++;
                    }
                }

            // 입자 수: 창 면적에 비례하고, 전체가 상한(maxParticles)을 넘지 않게 줄인다
            var counts = new int[n * 5];
            long total = 0;
            for (int i = 0; i < n; i++)
            {
                float area = Slots[i].ww * Slots[i].wh / RefArea;
                if (hasRain) counts[i * 5 + 0] = Math.Max(4, Mathf.RoundToInt(set.rainPerWindow * area));
                if (hasDrips) counts[i * 5 + 1] = Math.Max(2, Mathf.RoundToInt(set.dripsPerWindow * area));
                if (hasSnow) counts[i * 5 + 2] = Math.Max(4, Mathf.RoundToInt(set.snowPerWindow * area));
                if (hasClouds) counts[i * 5 + 3] = Math.Max(2, Mathf.RoundToInt(set.cloudsPerWindow * area));
                if (hasFog) counts[i * 5 + 4] = Math.Max(2, set.fogBandsPerWindow);
                for (int k = 0; k < 5; k++) total += counts[i * 5 + k];
            }
            if (total > set.maxParticles)
            {
                double f2 = set.maxParticles / (double)total;
                total = 0;
                for (int k = 0; k < counts.Length; k++)
                {
                    if (counts[k] > 0) counts[k] = Math.Max(1, (int)(counts[k] * f2));
                    total += counts[k];
                }
            }
            Parts = new Particle[total];
            int pi = 0;
            for (int i = 0; i < n; i++)
            {
                Slots[i].first = pi;
                int c5 = 0;
                for (int k = 0; k < 5; k++) c5 += counts[i * 5 + k];
                Slots[i].count = c5;
                pi += c5;
            }
            slotCounts = counts;
            Generate();
        }

        readonly int[] slotCounts;

        /// <summary>처음 위치로 되돌린다(의뢰를 처음부터 다시 시작할 때). 배열은 다시 만들지 않고 값만 채운다.</summary>
        public void Reseed()
        {
            Generate();
        }

        // ---------- 난수(시드 고정) ----------

        struct Rng
        {
            uint s;
            public Rng(int seed, int stream)
            {
                unchecked
                {
                    s = (uint)seed * 747796405u + (uint)(stream + 1) * 2891336453u + 1442695040u;
                    if (s == 0) s = 0x9E3779B9u;
                    for (int i = 0; i < 4; i++) Next();
                }
            }

            public uint Next()
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                return s;
            }

            /// <summary>[a, b) 정수</summary>
            public int Range(int a, int b) => b <= a ? a : a + (int)(Next() % (uint)(b - a));
            public float Float() => (Next() & 0xFFFFFF) / 16777216f;
        }

        void Generate()
        {
            Time = 0.0;
            for (int i = 0; i < Slots.Length; i++)
            {
                Slot s = Slots[i];
                var rng = new Rng(Seed, i);
                int p = s.first;
                int nRain = slotCounts[i * 5 + 0], nDrip = slotCounts[i * 5 + 1], nSnow = slotCounts[i * 5 + 2], nCloud = slotCounts[i * 5 + 3], nFog = slotCounts[i * 5 + 4];
                // 그리는 순서(뒤에서 앞으로): 구름 -> 비 -> 눈 -> 안개. 물방울은 유리 쪽에서 따로 그린다
                for (int k = 0; k < nCloud; k++)
                    Parts[p++] = new Particle { kind = KCloud, baseX = rng.Range(0, s.ww + 90), baseY = 4 + rng.Range(0, 56), spd = 3f * 30f, variant = (byte)(k % CloudShapes) };
                for (int k = 0; k < nRain; k++)
                    Parts[p++] = new Particle { kind = KRain, baseX = rng.Range(0, s.ww + 20), baseY = rng.Range(0, s.wh + 36), spd = 24f * 30f };
                for (int k = 0; k < nDrip; k++)
                    Parts[p++] = new Particle { kind = KDrip, baseX = 6 + rng.Range(0, Math.Max(1, s.ww - 12)), baseY = rng.Range(0, s.wh + 6), len = (short)(2 * rng.Range(3, 8)), spd = 5f * 30f };
                for (int k = 0; k < nSnow; k++)
                    Parts[p++] = new Particle { kind = KSnow, baseX = rng.Range(0, s.ww), baseY = rng.Range(0, s.wh + 36), spd = rng.Range(2, 4) * 30f, phase = rng.Float(), variant = (byte)(rng.Float() < 0.35f ? 1 : 0) };
                for (int k = 0; k < nFog; k++)
                {
                    int len = Math.Max(40, (int)(s.ww * 0.7f)) & ~1;
                    int row = (s.wh * (k + 1) / (nFog + 1) - 12) & ~1;
                    Parts[p++] = new Particle { kind = KFog, baseX = rng.Range(0, s.ww + len), baseY = Math.Max(0, row), len = (short)len, spd = 2f * 30f };
                }
            }
            Evaluate();
        }

        /// <summary>환경 시간을 dt초 흘린다(일시정지 중에는 0을 준다).</summary>
        public void Advance(float dt)
        {
            if (dt <= 0f) return;
            Time += dt;
            Evaluate();
        }

        static double Mod(double v, double m) => v - Math.Floor(v / m) * m;
        static int Snap(double v) => (int)Math.Floor(v / 2.0) * 2;

        /// <summary>시간에서 모든 입자의 위치(2px 격자)를 계산한다. environment-fx §3: 속도는 30 FPS 기준 프레임당 px를 초당으로 환산, 화면 밖은 위(왼쪽)에서 다시 시작.</summary>
        void Evaluate()
        {
            double t = Time;
            for (int i = 0; i < Slots.Length; i++)
            {
                Slot s = Slots[i];
                for (int k = s.first; k < s.first + s.count; k++)
                {
                    Particle p = Parts[k];
                    switch (p.kind)
                    {
                        case KRain:
                            p.sx = Snap(p.baseX);
                            p.sy = Snap(-20 + Mod(p.baseY + p.spd * t, s.wh + 36));
                            break;
                        case KDrip:
                            p.sx = Snap(p.baseX);
                            p.sy = Snap(Mod(p.baseY + p.spd * t, s.wh + 6) - 4);
                            break;
                        case KSnow:
                            p.sx = Snap(p.baseX + 6.0 * Math.Sin(2.0 * Math.PI * (t * (2.0 / 3.0) + p.phase)));
                            p.sy = Snap(-10 + Mod(p.baseY + p.spd * t, s.wh + 36));
                            break;
                        case KCloud:
                            p.sx = Snap(-45 + Mod(p.baseX + p.spd * t, s.ww + 90) - cloudW[p.variant] * 0.5);
                            p.sy = Snap(p.baseY - cloudH[p.variant] * 0.5);
                            break;
                        case KFog:
                            p.sx = Snap(-p.len + Mod(p.baseX + p.spd * t, s.ww + p.len));
                            p.sy = Snap(p.baseY);
                            break;
                    }
                    Parts[k] = p;
                }
            }
        }

        // ---------- 타일·모양 그림 만들기(한 번만) ----------

        static readonly Dictionary<long, string> tileKeys = new Dictionary<long, string>();

        static string Hex(Color32 c) => c.r.ToString("x2") + c.g.ToString("x2") + c.b.ToString("x2");

        /// <summary>
        /// 밀도 분모(2·4·8·16)와 색으로 디더 타일(16x16 px, 2px 점)을 만들어 캔버스에 등록하고 그림 키를 돌려준다.
        /// 무늬는 tools/art/prop.py의 _level_hit과 같다(1/2 바둑판, 1/4, 1/8, 1/16).
        /// </summary>
        static string EnsureTile(PixelCanvas cv, int den, Color32 color)
        {
            long id = ((long)den << 32) | (uint)(color.r | (color.g << 8) | (color.b << 16));
            if (!tileKeys.TryGetValue(id, out string key))
            {
                key = "env/dot" + den + "_" + Hex(color);
                tileKeys[id] = key;
            }
            if (cv.Has(key)) return key;
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var px = new Color32[16 * 16];
            for (int gy = 0; gy < 8; gy++)
                for (int gx = 0; gx < 8; gx++)
                {
                    if (!DotHit(den, gx, gy)) continue;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                            px[(gy * 2 + dy) * 16 + gx * 2 + dx] = color;   // 텍스처 y는 아래에서 위: 8x8 무늬가 위아래 대칭이 아니므로 아래에서 센 줄을 위 줄로 뒤집어 맞춘다
                }
            // 무늬 기준은 왼쪽 위 원점이다. 텍스처는 아래 원점이라 줄을 뒤집는다(16px 한 타일 단위)
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 16; x++)
                {
                    Color32 a = px[y * 16 + x], b = px[(15 - y) * 16 + x];
                    px[y * 16 + x] = b; px[(15 - y) * 16 + x] = a;
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            cv.RegisterTexture(key, tex);
            return key;
        }

        /// <summary>점 격자(gx, gy; 점 1개 = 2x2 px) 위의 한 칸이 이 밀도에서 점이 되는가.</summary>
        public static bool DotHit(int den, int gx, int gy)
        {
            switch (den)
            {
                case 2: return (gx + gy) % 2 == 0;
                case 4: return gx % 2 == 0 && gy % 2 == 0;
                case 8: return gx % 4 == 0 && gy % 2 == 0 && (gx / 4 + gy / 2) % 2 == 0;
                case 16: return gx % 4 == 0 && gy % 4 == 0 && (gx / 4 + gy / 4) % 2 == 0;
            }
            return false;
        }

        /// <summary>
        /// 구름 모양 6가지(environment-fx §3: 디더 타원, 위 밝은 색·아래 어두운 색)와 빗줄기(2x8 두 토막, 바람에 1/4 기울기)를 한 장에 모아 한 번 만든다.
        /// 모양은 고정이고 위치만 움직인다. 한 장이라 구름·비를 그릴 때 텍스처 전환이 없다.
        /// </summary>
        void MakeAtlas(PixelCanvas cv, Color32 top, Color32 bottom, Color32 rain, bool clouds, bool withRain)
        {
            int atlasW = 4, atlasH = 0;
            for (int i = 0; i < CloudShapes; i++)
            {
                var rng = new Rng(7919, i);
                int rw = rng.Range(15, 27) * 2, rh = rng.Range(4, 7) * 2;
                cloudW[i] = rw * 2; cloudH[i] = rh * 2;
                if (clouds) { cloudSrcY[i] = atlasH; atlasH += cloudH[i]; atlasW = Math.Max(atlasW, cloudW[i]); }
            }
            rainSrcY = atlasH;
            if (withRain) atlasH += 16;
            atlasKey = "env/atlas_" + (clouds ? Hex(top) + "_" + Hex(bottom) : "x") + "_" + (withRain ? Hex(rain) : "x");
            if (cv.Has(atlasKey)) return;
            var px = new Color32[atlasW * atlasH];
            // 텍스처는 아래 원점이라 그림의 y 줄을 (atlasH - 1 - y)에 넣는다
            if (clouds)
                for (int i = 0; i < CloudShapes; i++)
                {
                    int w = cloudW[i], h = cloudH[i], rw = w / 2, rh = h / 2;
                    for (int gy = 0; gy < h / 2; gy++)
                        for (int gx = 0; gx < w / 2; gx++)
                        {
                            double x = gx * 2, y = gy * 2;
                            double d = ((x - rw) / rw) * ((x - rw) / rw) + ((y - rh) / rh) * ((y - rh) / rh);
                            Color32 col;
                            if (d < 0.55) col = y < rh ? top : bottom;
                            else if (d < 1 && DotHit(4, gx, gy)) col = bottom;
                            else continue;
                            for (int dy = 0; dy < 2; dy++)
                                for (int dx = 0; dx < 2; dx++)
                                    px[(atlasH - 1 - (cloudSrcY[i] + gy * 2 + dy)) * atlasW + gx * 2 + dx] = col;
                        }
                }
            if (withRain)
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 2; x++)
                        px[(atlasH - 1 - (rainSrcY + y)) * atlasW + (y < 8 ? 2 : 0) + x] = rain;   // 위 토막은 오른쪽, 아래 토막은 2px 왼쪽
            var tex = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply(false);
            cv.RegisterTexture(atlasKey, tex);
        }

        static readonly string[] CityLayerKeys = { "backgrounds/city_layer1_far", "backgrounds/city_layer2_mid", "backgrounds/city_layer3_tower", "backgrounds/city_layer5_near" };

        /// <summary>도시 배경 그림 키. 낮에는 불 꺼진 도시를 밝기 3단(팔레트 4·2·3)으로 다시 칠한 그림을 한 번 만들어 쓴다(environment-fx §2).</summary>
        static string[] MakeCityKeys(PixelCanvas cv, bool day)
        {
            var keys = new List<string>();
            foreach (string k in CityLayerKeys)
            {
                if (!cv.Has(k)) continue;
                if (!day) { keys.Add(k); continue; }
                string dk = k + "_day";
                cv.RegisterRecolor(k, dk, DayRamp);
                keys.Add(cv.Has(dk) ? dk : k);
            }
            return keys.ToArray();
        }

        static Color32 DayRamp(Color32 c)
        {
            float lum = (c.r * 3f + c.g * 6f + c.b) / 10f;
            if (lum < 50f) return new Color32(50, 56, 74, 255);     // 팔레트 4
            if (lum < 90f) return new Color32(68, 76, 98, 255);     // 팔레트 2
            return new Color32(104, 114, 140, 255);                 // 팔레트 3
        }

        // ---------- 프레임 ----------

        /// <summary>프레임 시작: Shift 중 절반 그리기, 환경 효과 줄이기(입자 끄기), 보이는 영역(RT px)을 정한다.</summary>
        public void BeginFrame(bool shiftHalf, bool reducedFx, int vx0, int vy0, int vx1, int vy1)
        {
            half = shiftHalf; reduced = reducedFx;
            viewX0 = vx0; viewY0 = vy0; viewX1 = vx1; viewY1 = vy1;
            seqLe = seqGlass = seqSecond = 0;
            LeDrawn = GlassDrawn = SecondDrawn = 0;
            drawTicks = 0;
            SkyDrawn = false;
        }

        /// <summary>이 섹터의 창 번호 범위.</summary>
        public void SlotRange(int floor, int col, out int first, out int count)
        {
            int idx = floor * sectorCols + col;
            first = sectorFirstSlot[idx];
            count = sectorSlotCount[idx];
        }

        public bool IsOpenAir(int slot) => Slots[slot].openAir;

        bool Visible(int slot, int ox, int oy, out int x0, out int y0, out int x1, out int y1)
        {
            Slot s = Slots[slot];
            x0 = ox + s.wx; y0 = oy + s.wy; x1 = x0 + s.ww; y1 = y0 + s.wh;
            return x1 > viewX0 && x0 < viewX1 && y1 > viewY0 && y0 < viewY1;
        }

        bool Take(ref int seq)
        {
            if (!half) return true;
            return (seq++ & 1) == 0;
        }

        // ---------- L0 하늘 ----------

        /// <summary>
        /// 하늘색(L0): 위 색 → 가로 띠 디더 한 줄(1/2) → 아래 색. 점 디더를 넓게 깔면 눈처럼 보여 띠로 나눈다(environment-fx §6).
        /// 장면 뷰(논리 좌표) 전체를 채운다. 입자 끄기와 무관하게 항상 그린다.
        /// </summary>
        public void DrawSky(PixelCanvas cv, int vx, int vy, int vw, int vh)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int x0 = vx * PixelCanvas.Z, y0 = vy * PixelCanvas.Z, w = vw * PixelCanvas.Z, h = vh * PixelCanvas.Z;
            int mid = y0 + ((h / 3) & ~3);
            cv.FillPx(x0, y0, w, h, Preset.skyBottom);
            cv.FillPx(x0, y0, w, mid - y0, Preset.skyTop);
            cv.TilePx(skyChecker, x0, mid, w, 4, x0, mid);   // 경계 2줄(점 2행)에 아래 색 바둑판
            SkyDrawn = true;
            LastSkyTop = Preset.skyTop;
            LastSkyBottom = Preset.skyBottom;
            drawTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
        }

        // ---------- LE 입자 ----------

        /// <summary>
        /// 창 뒤 입자(LE, 방 껍데기 L1 아래): 구름·비·눈·안개를 창 구멍 안에만 그린다. ox, oy는 그 섹터의 방 그림 왼쪽 위(RT px). 그린 입자 수를 돌려준다.
        /// </summary>
        public int DrawLe(PixelCanvas cv, int slot, int ox, int oy)
        {
            if (reduced || !Visible(slot, ox, oy, out int x0, out int y0, out int x1, out int y1)) return 0;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            cv.SetClipPx(x0, y0, x1, y1);
            // 옥상(지붕 없는 방)은 비·눈·안개를 L2 위 두 번째 그리기(DrawSecond)가 같은 위치에 그린다. 그 아래에서 한 번 더 그려도 가려질 뿐 같은 그림이라 구름만 그린다(성능)
            int n = DrawParticles(cv, slot, x0, y0, true, Slots[slot].openAir, ref seqLe);
            cv.ClearClipPx();
            LeDrawn += n;
            drawTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            return n;
        }

        /// <summary>옥상(지붕 없는 방) 두 번째 입자: L2 위·L4 아래. 구름은 하늘에만 있어 제외한다(D-103 ①).</summary>
        public int DrawSecond(PixelCanvas cv, int slot, int ox, int oy)
        {
            if (reduced || !Slots[slot].openAir || !Visible(slot, ox, oy, out int x0, out int y0, out int x1, out int y1)) return 0;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            cv.SetClipPx(x0, y0, x1, y1);
            int n = DrawParticles(cv, slot, x0, y0, false, false, ref seqSecond);
            cv.ClearClipPx();
            SecondDrawn += n;
            drawTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            return n;
        }

        int DrawParticles(PixelCanvas cv, int slot, int x0, int y0, bool withClouds, bool cloudsOnly, ref int seq)
        {
            Slot s = Slots[slot];
            int drawn = 0;
            int fogMask = 0, fogIdx = 0;   // 안개 띠는 단계(타일)마다 묶어 그리려고 그릴 띠를 먼저 정한다
            for (int k = s.first; k < s.first + s.count; k++)
            {
                Particle p = Parts[k];
                if (p.kind == KDrip || (p.kind == KCloud && !withClouds) || (cloudsOnly && p.kind != KCloud)) continue;
                if (!Take(ref seq)) { if (p.kind == KFog) fogIdx++; continue; }
                drawn++;
                int x = x0 + p.sx, y = y0 + p.sy;
                switch (p.kind)
                {
                    case KRain:
                        // 길이 16, 바람에 1/4 기울기: 2x8 두 토막(아래 토막이 왼쪽으로 2px). 아틀라스의 빗줄기 4x16 한 장
                        cv.BlitPartPx(atlasKey, 0, rainSrcY, 4, 16, x - 2, y);
                        break;
                    case KSnow:
                        int sz = p.variant == 1 ? 4 : 2;
                        cv.FillPx(x, y, sz, sz, rainColor);
                        break;
                    case KCloud:
                        cv.BlitPartPx(atlasKey, 0, cloudSrcY[p.variant], cloudW[p.variant], cloudH[p.variant], x, y);
                        break;
                    case KFog:
                        if (fogIdx < 32) fogMask |= 1 << fogIdx;
                        fogIdx++;
                        break;
                }
            }
            if (fogMask != 0)
            {
                // 가로 띠 디더 3단계: 위 1/16, 가운데 1/8, 아래 1/4(8px씩). 같은 타일끼리 모아 그린다
                for (int b = 0; b < 3; b++)
                {
                    int fi = 0;
                    for (int k = s.first; k < s.first + s.count; k++)
                    {
                        Particle p = Parts[k];
                        if (p.kind != KFog) continue;
                        if (fi < 32 && (fogMask & (1 << fi)) != 0)
                            cv.TilePx(fogTiles[b], x0 + p.sx, y0 + p.sy + b * 8, p.len, 8, x0 + p.sx, y0 + p.sy);
                        fi++;
                    }
                }
            }
            return drawn;
        }

        /// <summary>창 유리 물방울(L1+, 비일 때): 창 범위 안에 길이 6~14px 선을 그린다.</summary>
        public int DrawGlass(PixelCanvas cv, int slot, int ox, int oy)
        {
            if (reduced || !hasDrips || !Visible(slot, ox, oy, out int x0, out int y0, out int x1, out int y1)) return 0;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            cv.SetClipPx(x0, y0, x1, y1);
            Slot s = Slots[slot];
            int drawn = 0;
            for (int k = s.first; k < s.first + s.count; k++)
            {
                Particle p = Parts[k];
                if (p.kind != KDrip || !Take(ref seqGlass)) continue;
                drawn++;
                cv.FillPx(x0 + p.sx, y0 + p.sy, 2, p.len, rainColor);
            }
            cv.ClearClipPx();
            GlassDrawn += drawn;
            drawTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            return drawn;
        }

        // ---------- L3 환경 밝기 ----------

        /// <summary>
        /// 실내 어둠(천장선~바닥선 전체 디더, 창 유리 범위 제외)과 창 빛(창 아래에서 바닥으로 기운 띠)을 그린다. 입자 끄기와 무관하게 항상 그린다.
        /// 방 종류의 창 목록을 쓰므로 섹터마다 한 번 부른다. ox, oy는 그 섹터의 방 그림 왼쪽 위(RT px).
        /// </summary>
        public void DrawRoomLight(PixelCanvas cv, SceneLayout.Room room, int ox, int oy)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            RectInt[] wins = room.Windows;
            if (ambientTile != null)
            {
                int n = SubtractWindows(0, ceilY, sectorW, floorY, wins);
                for (int i = 0; i < n; i++)
                {
                    int rx0 = rects[i * 4], ry0 = rects[i * 4 + 1], rx1 = rects[i * 4 + 2], ry1 = rects[i * 4 + 3];
                    cv.TilePx(ambientTile, ox + rx0, oy + ry0, rx1 - rx0, ry1 - ry0, ox, oy);
                }
            }
            if (beamTile != null && wins.Length > 0)
            {
                cv.SetClipPx(ox, oy, ox + sectorW, oy + floorY);
                for (int w = 0; w < wins.Length; w++)
                {
                    RectInt win = wins[w];
                    int top = win.yMax + 20;
                    for (int y = top; y < floorY; y += 2)
                    {
                        int off = (int)((y - top) * 0.7f) & ~1;
                        int bx0 = win.x + off, bx1 = win.xMax + off;
                        if (bx0 >= sectorW) continue;
                        if (bx1 > sectorW) bx1 = sectorW;
                        cv.TilePx(y < floorY - 6 ? beamTile : beamBottomTile, ox + bx0, oy + y, bx1 - bx0, 2, ox, oy);
                    }
                }
                cv.ClearClipPx();
            }
            drawTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
        }

        /// <summary>사각형 (x0,y0)~(x1,y1)에서 창 구멍을 뺀 나머지를 rects에 담고 개수를 돌려준다.</summary>
        int SubtractWindows(int x0, int y0, int x1, int y1, RectInt[] wins)
        {
            int n = 1;
            rects[0] = x0; rects[1] = y0; rects[2] = x1; rects[3] = y1;
            for (int w = 0; w < wins.Length; w++)
            {
                int wx0 = wins[w].x, wy0 = wins[w].y, wx1 = wins[w].xMax, wy1 = wins[w].yMax;
                int m = 0;
                for (int i = 0; i < n && m < 28; i++)
                {
                    int ax0 = rects[i * 4], ay0 = rects[i * 4 + 1], ax1 = rects[i * 4 + 2], ay1 = rects[i * 4 + 3];
                    if (wx1 <= ax0 || wx0 >= ax1 || wy1 <= ay0 || wy0 >= ay1)
                    {
                        m = Put(m, ax0, ay0, ax1, ay1);
                        continue;
                    }
                    int cx0 = Math.Max(ax0, wx0), cx1 = Math.Min(ax1, wx1), cy0 = Math.Max(ay0, wy0), cy1 = Math.Min(ay1, wy1);
                    if (cy0 > ay0) m = Put(m, ax0, ay0, ax1, cy0);          // 위
                    if (cy1 < ay1) m = Put(m, ax0, cy1, ax1, ay1);          // 아래
                    if (cx0 > ax0) m = Put(m, ax0, cy0, cx0, cy1);          // 왼쪽
                    if (cx1 < ax1) m = Put(m, cx1, cy0, ax1, cy1);          // 오른쪽
                }
                Array.Copy(rectsTmp, rects, m * 4);
                n = m;
            }
            return n;
        }

        int Put(int m, int x0, int y0, int x1, int y1)
        {
            if (m >= 32) return m;
            rectsTmp[m * 4] = x0; rectsTmp[m * 4 + 1] = y0; rectsTmp[m * 4 + 2] = x1; rectsTmp[m * 4 + 3] = y1;
            return m + 1;
        }
    }
}
