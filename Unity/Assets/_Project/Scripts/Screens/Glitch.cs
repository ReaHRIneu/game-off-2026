using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>글리치 세기 설정(D-087): 끔이면 모든 프리셋을 건너뛰고, 약은 값을 절반으로 쓴다 [제안].</summary>
    public enum GlitchStrength { Off = 0, Weak = 1, Normal = 2 }

    /// <summary>셰이더에 넘길 한 프레임의 글리치 값(PixelCanvas.Apply가 읽는다).</summary>
    public struct GlitchFrame
    {
        public Vector4 rectA, rectB;   // 논리 px (left, top, right, bottom)
        public Vector4 shift;          // 띠 높이, 최대 밀림 폭, 비율, 치우침
        public Vector4 copy;           // 모드, 어긋남 폭, 밀도
        public Vector4 fx;             // 스캔라인, 노이즈, 블록 크기, 블록 확률
        public float tick;             // 30 FPS 단위 시드
    }

    /// <summary>
    /// 글리치 재생기(W-013, vfx-direction §7). 한 번에 글리치 하나만 재생한다 — 새로 재생하면 이전 것을 끝낸다(§3).
    /// 반복형(danger·logo)은 켜 두면 간격마다 한 번씩 재생하고, 다른 글리치가 재생 중이면 그 차례는 건너뛴다.
    /// 모양은 30 FPS 틱 단위로만 바뀐다(D-077). 프레임마다 문자열·List·LINQ를 쓰지 않는다.
    /// 도메인 리로드가 꺼져 있으므로 static 값은 Init이 모두 초기화한다.
    /// </summary>
    public static class Glitch
    {
        public const int TickRate = 30;
        const int SeedMod = 997;

        public static GlitchStrength Strength = GlitchStrength.Normal;
        /// <summary>자동 확인용: 0 이상이면 시간이 멈추고 이 틱의 모양을 보여 준다. 새로 재생하는 글리치도 이 틱에서 시작한다.</summary>
        public static int FreezeTick = -1;

        public static GlitchPreset ShiftIn, ShiftOut, RoomPush, Danger, GameOver, Transition, Logo;

        static GlitchSet set;
        static GlitchPreset cur;
        static Vector4 rectA, rectB;
        static int dir;
        static int seed;
        static int playCount;
        static float elapsed;

        sealed class LoopState
        {
            public GlitchPreset preset;
            public bool on, fast;
            public Vector4 rect;
            public float timer;
        }

        static readonly LoopState[] loops = new LoopState[4];
        static int loopCount;

        public static GlitchSet Set => set;
        public static Shader Shader => set != null ? set.shader : null;
        public static float DangerRatio => set != null ? set.dangerTrackingRatio : 0.7f;

        public static void Init(GlitchSet s)
        {
            set = s;
            cur = null;
            elapsed = 0f;
            playCount = 0;
            FreezeTick = -1;
            Strength = GlitchStrength.Normal;
            loopCount = 0;
            ShiftIn = ShiftOut = RoomPush = Danger = GameOver = Transition = Logo = null;
            if (s == null) return;
            ShiftIn = Get("shift_in"); ShiftOut = Get("shift_out"); RoomPush = Get("room_push");
            Danger = Get("danger"); GameOver = Get("gameover"); Transition = Get("transition"); Logo = Get("logo");
            for (int i = 0; i < loops.Length; i++) loops[i] = new LoopState();
        }

        /// <summary>이름으로 프리셋을 찾는다(처음 한 번과 자동 확인용). 없으면 null.</summary>
        public static GlitchPreset Get(string id)
        {
            if (set == null) return null;
            for (int i = 0; i < set.presets.Length; i++) if (set.presets[i].id == id) return set.presets[i];
            return null;
        }

        /// <summary>지금 이 프리셋이 재생 중인지.</summary>
        public static bool IsPlaying(GlitchPreset p) => cur != null && cur == p;
        public static bool Active => cur != null;

        // ---------- 재생 ----------

        static Vector4 R(RectInt r) => new Vector4(r.xMin, r.yMin, r.xMax, r.yMax);

        /// <summary>영역 하나(또는 둘)에 프리셋을 재생한다. dir은 미는 방향(-1, 0, 1)으로 치우침(shiftBias)의 부호를 정한다.</summary>
        public static void Play(GlitchPreset p, RectInt a, RectInt? b = null, int pushDir = 1)
        {
            if (p == null || Strength == GlitchStrength.Off) return;
            Begin(p, R(a), b.HasValue ? R(b.Value) : Vector4.zero, pushDir);
        }

        /// <summary>화면 전체(640x360)에 재생한다.</summary>
        public static void PlayFull(GlitchPreset p)
        {
            if (p == null || Strength == GlitchStrength.Off) return;
            Begin(p, new Vector4(0, 0, PixelCanvas.W, PixelCanvas.H), Vector4.zero, 1);
        }

        static void Begin(GlitchPreset p, Vector4 a, Vector4 b, int pushDir)
        {
            cur = p;
            rectA = a; rectB = b;
            dir = pushDir;
            playCount++;
            seed = (playCount * 37) % SeedMod;
            elapsed = FreezeTick >= 0 ? (FreezeTick + 0.01f) / TickRate : 0f;
        }

        /// <summary>재생 중인 글리치를 끝낸다(반복형 켜짐 상태는 유지).</summary>
        public static void Cancel() { cur = null; }

        /// <summary>반복형을 켜거나 끈다. 처음 켜면 바로 한 번 재생한다. fast면 빠른 주기(카운트다운 중 danger).</summary>
        public static void SetLoop(GlitchPreset p, bool on, RectInt rect, bool fast)
        {
            if (p == null) return;
            LoopState s = FindLoop(p, on);
            if (s == null) return;
            if (on)
            {
                if (!s.on) { s.on = true; s.timer = 0f; }
                s.rect = R(rect);
                if (fast != s.fast) { s.fast = fast; if (fast && s.timer > p.fastInterval) s.timer = p.fastInterval; }
            }
            else if (s.on)
            {
                s.on = false;
                if (cur == p) cur = null;
            }
        }

        static LoopState FindLoop(GlitchPreset p, bool create)
        {
            for (int i = 0; i < loopCount; i++) if (loops[i].preset == p) return loops[i];
            if (!create || loopCount >= loops.Length) return null;
            LoopState s = loops[loopCount++];
            s.preset = p; s.on = false; s.fast = false;
            return s;
        }

        /// <summary>자동 확인용: 반복형 프리셋을 지금 바로, 지정한 틱 모양으로 한 번 재생하고 멈춘다.</summary>
        public static void DebugBurst(GlitchPreset p, int tick)
        {
            LoopState s = FindLoop(p, false);
            if (s == null) return;
            FreezeTick = tick;
            Begin(p, s.rect, Vector4.zero, 1);
        }

        // ---------- 진행 ----------

        public static void Update(float dt)
        {
            if (set == null) return;
            if (FreezeTick < 0)
            {
                elapsed += dt;
                for (int i = 0; i < loopCount; i++) if (loops[i].on) loops[i].timer -= dt;
            }
            if (cur != null && FreezeTick < 0 && elapsed * TickRate >= cur.TotalTicks) cur = null;
            if (Strength == GlitchStrength.Off) { cur = null; return; }
            for (int i = 0; i < loopCount; i++)
            {
                LoopState s = loops[i];
                if (!s.on || s.timer > 0f) continue;
                if (cur != null) continue;   // 다른 글리치 재생 중이면 이번 차례는 건너뛴다
                Begin(s.preset, s.rect, Vector4.zero, 1);
                s.timer = s.fast ? s.preset.fastInterval : Random.Range(s.preset.intervalMin, s.preset.intervalMax);
            }
        }

        // ---------- 셰이더 값 ----------

        static float Scale(float v) => Strength == GlitchStrength.Weak ? v * 0.5f : v;

        /// <summary>이번 프레임에 그릴 글리치가 있으면 값을 채워 true를 돌려준다.</summary>
        public static bool TryGetFrame(ref GlitchFrame f)
        {
            if (cur == null || Strength == GlitchStrength.Off) return false;
            int total = cur.TotalTicks;
            int tick = FreezeTick >= 0 ? FreezeTick : Mathf.Min(total - 1, (int)(elapsed * TickRate));
            tick = Mathf.Clamp(tick, 0, total - 1);
            GlitchStage st = cur.stages[cur.stages.Length - 1];
            int acc = 0;
            for (int i = 0; i < cur.stages.Length; i++)
            {
                acc += Mathf.Max(1, cur.stages[i].ticks);
                if (tick < acc) { st = cur.stages[i]; break; }
            }

            f.rectA = rectA; f.rectB = rectB;
            int maxShift = st.maxShift <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(Scale(st.maxShift)));
            f.shift = new Vector4(st.bandHeight, maxShift, Scale(st.shiftRatio), st.shiftBias * dir);
            f.copy = new Vector4((int)st.copy, st.copyOffset, Scale(st.copyDensity), 0f);
            float area = Mathf.Max(1f, (rectA.z - rectA.x) * (rectA.w - rectA.y) + Mathf.Max(0f, rectB.z - rectB.x) * Mathf.Max(0f, rectB.w - rectB.y));
            float cells = area / Mathf.Max(1, st.blockSize * st.blockSize);
            int blocks = st.blockCount <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(Scale(st.blockCount)));
            f.fx = new Vector4(Scale(st.scan), Scale(st.noise), st.blockSize, blocks / cells);
            f.tick = (seed + tick) % SeedMod;
            return true;
        }
    }
}
