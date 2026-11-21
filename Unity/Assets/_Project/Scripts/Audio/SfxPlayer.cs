using System.Collections.Generic;
using GameOff.Game;
using UnityEngine;

namespace GameOff.Audio
{
    /// <summary>
    /// 효과음 재생기(W-020, sfx-plan §1·§4). ID로 재생을 요청하면 SfxSet의 클립을 고른다. 클립이 없으면 조용히 넘어간다(오류·로그 없음).
    /// 막힘(G08)·경보(A05)는 클립이 없을 때 W-014·W-016의 코드 합성 소리를 쓴다.
    /// 규칙: 같은 ID는 한 프레임에 1번, 동시 재생 4개까지(넘치면 가장 오래된 것을 끊음). 최종 음량 = 항목 음량 x (SfxVolume / VolumeMax) x 0.5.
    /// 요청 횟수·마지막 음량은 자동 확인이 읽는다. 클립이 없어도 요청 횟수는 오른다.
    /// </summary>
    public sealed class SfxPlayer
    {
        public const int MaxVoices = 4;
        /// <summary>기존 막힘·경보 소리의 기준 음량(0.5). SfxVolume 10일 때 항목 음량 0 dB의 최종 음량.</summary>
        public const float BaseGain = 0.5f;

        sealed class Stat { public int requests, played, lastFrame = -1; }
        sealed class Voice { public AudioSource src; public float endTime; public long order; }
        sealed class Loop { public string id; public AudioSource src; public bool active; }

        readonly SfxSet set;
        readonly ProgressStore progress;
        readonly GameObject host;
        readonly Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
        readonly Dictionary<string, AudioClip> debugClips = new Dictionary<string, AudioClip>();
        readonly Voice[] voices = new Voice[MaxVoices];
        readonly List<Loop> loops = new List<Loop>();
        long order;
        AudioClip blockClip, alertClip;

        public SfxSet Set => set;
        /// <summary>마지막으로 재생을 요청한 소리의 최종 음량(0이면 소리를 내지 않았다). 같은 프레임 중복·클립 없음이어도 갱신된다.</summary>
        public float LastGain { get; private set; } = -1f;

        public SfxPlayer(GameObject host, SfxSet set, ProgressStore progress)
        {
            this.host = host;
            this.set = set;
            this.progress = progress;
            EnsureListener(host);
            for (int i = 0; i < voices.Length; i++) voices[i] = new Voice { src = NewSource(), endTime = -1f };
        }

        /// <summary>장면에 AudioListener가 없으면 host에 붙인다(Setup이 만든 카메라에는 없다).</summary>
        public static void EnsureListener(GameObject host)
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
        }

        AudioSource NewSource()
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        Stat StatOf(string id)
        {
            if (!stats.TryGetValue(id, out Stat st)) { st = new Stat(); stats[id] = st; }
            return st;
        }

        /// <summary>그 ID의 재생 요청 횟수(클립이 없어도 오른다).</summary>
        public int RequestCount(string id) => stats.TryGetValue(id, out Stat st) ? st.requests : 0;

        /// <summary>그 ID가 실제로 재생을 시작한 횟수(클립 없음·음량 0·같은 프레임 중복은 세지 않는다).</summary>
        public int PlayedCount(string id) => stats.TryGetValue(id, out Stat st) ? st.played : 0;

        /// <summary>지금 재생 중인 효과음 수(반복음 제외, 최대 4).</summary>
        public int ActiveVoices
        {
            get
            {
                int n = 0;
                for (int i = 0; i < voices.Length; i++) if (voices[i].endTime > Time.unscaledTime) n++;
                return n;
            }
        }

        /// <summary>자동 확인용: 항목 대신 쓸 클립을 메모리에만 지정한다(에셋은 건드리지 않음). null이면 지정 해제.</summary>
        public void DebugSetClip(string id, AudioClip clip)
        {
            if (clip == null) debugClips.Remove(id); else debugClips[id] = clip;
        }

        float GainOf(string id, SfxEntry e)
        {
            float db = e != null ? e.gainDb : SfxSet.DefaultGainDb(id);
            return Mathf.Pow(10f, db / 20f) * (progress.SfxVolume / (float)ProgressStore.VolumeMax) * BaseGain;
        }

        AudioClip PickClip(string id, SfxEntry e)
        {
            if (debugClips.TryGetValue(id, out AudioClip dbg)) return dbg;
            if (e != null && e.clips != null && e.clips.Length > 0)
            {
                int n = 0;
                for (int i = 0; i < e.clips.Length; i++) if (e.clips[i] != null) n++;
                if (n > 0)
                {
                    int pick = Random.Range(0, n);
                    for (int i = 0; i < e.clips.Length; i++)
                        if (e.clips[i] != null && pick-- == 0) return e.clips[i];
                }
            }
            if (id == "G08") return blockClip != null ? blockClip : (blockClip = MakeBlockClip());
            if (id == "A05") return alertClip != null ? alertClip : (alertClip = MakeAlertClip());
            return null;
        }

        static float PitchOf(SfxEntry e, string id, float extraSemitones)
        {
            float r = e != null ? e.pitchRandomSemitones : SfxSet.DefaultPitchSemitones(id);
            float semis = extraSemitones + (r > 0f ? Random.Range(-r, r) : 0f);
            return semis == 0f ? 1f : Mathf.Pow(2f, semis / 12f);
        }

        /// <summary>효과음 한 번. extraSemitones는 호출하는 쪽이 더하는 음높이(별마다 한 단계 올리기 등).</summary>
        public void Play(string id, float extraSemitones = 0f)
        {
            Stat st = StatOf(id);
            st.requests++;
            SfxEntry e = set != null ? set.Find(id) : null;
            float gain = GainOf(id, e);
            LastGain = gain;
            if (st.lastFrame == Time.frameCount) return;   // 같은 ID는 한 프레임에 1번
            st.lastFrame = Time.frameCount;
            if (gain <= 0f) return;                         // SfxVolume 0: 재생 생략
            AudioClip clip = PickClip(id, e);
            if (clip == null) return;                       // 클립 없음: 조용히 넘어간다

            // 빈 소리 칸을 찾고, 없으면 가장 오래된 소리를 끊고 쓴다(동시 4개)
            Voice v = null;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i].endTime <= Time.unscaledTime) { v = voices[i]; break; }
            if (v == null)
            {
                v = voices[0];
                for (int i = 1; i < voices.Length; i++) if (voices[i].order < v.order) v = voices[i];
                v.src.Stop();
            }
            float pitch = PitchOf(e, id, extraSemitones);
            v.src.clip = clip;
            v.src.loop = false;
            v.src.pitch = pitch;
            v.src.volume = Mathf.Clamp01(gain);
            v.src.Play();
            v.endTime = Time.unscaledTime + clip.length / Mathf.Max(0.01f, pitch);
            v.order = ++order;
            st.played++;
        }

        // ---------- 반복음(G04 Shift 험, E01 빗소리) ----------

        Loop LoopOf(string id)
        {
            for (int i = 0; i < loops.Count; i++) if (loops[i].id == id) return loops[i];
            var l = new Loop { id = id, src = NewSource() };
            l.src.loop = true;
            loops.Add(l);
            return l;
        }

        public bool IsLooping(string id)
        {
            for (int i = 0; i < loops.Count; i++) if (loops[i].id == id) return loops[i].active;
            return false;
        }

        /// <summary>반복음 켜기. 이미 켜져 있으면 아무 일도 없다(요청 횟수는 켜질 때만 센다).</summary>
        public void StartLoop(string id)
        {
            Loop l = LoopOf(id);
            if (l.active) return;
            StatOf(id).requests++;
            l.active = true;
            Refresh(l, true);
        }

        public void StopLoop(string id)
        {
            for (int i = 0; i < loops.Count; i++)
                if (loops[i].id == id && loops[i].active) { loops[i].active = false; loops[i].src.Stop(); }
        }

        public void StopAllLoops()
        {
            for (int i = 0; i < loops.Count; i++) if (loops[i].active) { loops[i].active = false; loops[i].src.Stop(); }
        }

        /// <summary>켜져 있어야 하면 켜고 아니면 끈다(상태를 보고 반복음을 맞출 때).</summary>
        public void SetLoop(string id, bool on)
        {
            if (on) StartLoop(id); else StopLoop(id);
        }

        /// <summary>설정에서 효과음 음량이 바뀌어도 반복음이 따라가게 매 프레임 부른다.</summary>
        public void Tick()
        {
            for (int i = 0; i < loops.Count; i++) if (loops[i].active) Refresh(loops[i], false);
        }

        void Refresh(Loop l, bool restart)
        {
            SfxEntry e = set != null ? set.Find(l.id) : null;
            float gain = GainOf(l.id, e);
            LastGain = gain;
            AudioClip clip = PickClip(l.id, e);
            if (clip == null || gain <= 0f)
            {
                if (l.src.isPlaying) l.src.Stop();
                return;
            }
            if (restart || l.src.clip != clip) { l.src.clip = clip; l.src.Stop(); }
            l.src.volume = Mathf.Clamp01(gain);
            if (!l.src.isPlaying) l.src.Play();
        }

        // ---------- 코드 합성 소리(W-014 막힘, W-016 경보): 클립이 없을 때의 대체 소리 [제안] ----------

        /// <summary>높은 사각파 두 번(880 Hz, 660 Hz) 짧게 0.16초.</summary>
        static AudioClip MakeAlertClip()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.16f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float freq = t < 0.5f ? 880f : 660f;
                phase += freq / rate;
                float local = t < 0.5f ? t * 2f : (t - 0.5f) * 2f;
                float square = (phase % 1f) < 0.5f ? 1f : -1f;
                data[i] = square * 0.3f * (1f - local * 0.6f);
            }
            var clip = AudioClip.Create("alert", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>낮은 사각파가 220 Hz에서 150 Hz로 내려가는 0.09초 음(빠른 감쇠).</summary>
        static AudioClip MakeBlockClip()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.09f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float freq = Mathf.Lerp(220f, 150f, t);
                phase += freq / rate;
                float square = (phase % 1f) < 0.5f ? 1f : -1f;
                data[i] = square * 0.35f * (1f - t) * (1f - t);
            }
            var clip = AudioClip.Create("block", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
