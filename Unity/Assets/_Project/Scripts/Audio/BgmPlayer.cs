using GameOff.Game;
using UnityEngine;

namespace GameOff.Audio
{
    /// <summary>
    /// BGM 재생기(W-020, bgm-plan §4-3). 화면(단계)이 정한 곡 슬롯 이름을 받아 곡을 바꾼다.
    /// 곡이 바뀌면 0.5초 교차 페이드, 같은 곡이면 이어서 재생한다. 일시정지 중에는 -10 dB. 음량 = BgmVolume / VolumeMax x 기준값.
    /// 클립이 비어 있어도 슬롯 이름(CurrentTrack)은 바뀐다(자동 확인용). 효과곡(sting_*)은 1번만 재생한다.
    /// </summary>
    public sealed class BgmPlayer
    {
        /// <summary>BGM 기준 음량. 효과음과 같은 0.5(sfx-plan §4: BGM -18 dBFS 기준 위에 사건음이 약 3 dB 크게) [제안].</summary>
        public const float BaseGain = 0.5f;
        /// <summary>일시정지 중 감쇠(dB, sfx-plan §4).</summary>
        public const float PauseDb = -10f;
        public const float FadeSeconds = 0.5f;

        sealed class Slot { public AudioSource src; public float level; public bool active; }

        readonly BgmSet set;
        readonly ProgressStore progress;
        readonly Slot[] slots = new Slot[2];
        int activeIdx = -1;

        /// <summary>지금 곡의 슬롯 이름. 곡이 없으면 빈 문자열.</summary>
        public string CurrentTrack { get; private set; } = "";
        /// <summary>지금 목표 음량(0~1). 일시정지·BgmVolume이 반영된 값이며 실제 음량은 이 값을 향해 페이드한다.</summary>
        public float TargetVolume { get; private set; }

        public BgmPlayer(GameObject host, BgmSet set, ProgressStore progress)
        {
            this.set = set;
            this.progress = progress;
            SfxPlayer.EnsureListener(host);
            for (int i = 0; i < slots.Length; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                slots[i] = new Slot { src = s };
            }
        }

        /// <summary>목표 음량: BgmVolume 0이면 0, 일시정지면 -10 dB.</summary>
        public float ComputeTarget(bool frozen)
        {
            float v = progress.BgmVolume / (float)ProgressStore.VolumeMax * BaseGain;
            return frozen ? v * Mathf.Pow(10f, PauseDb / 20f) : v;
        }

        /// <summary>매 프레임: 원하는 곡(빈 문자열이면 곡 없음)과 일시정지 여부를 받아 곡을 바꾸고 페이드를 진행한다.</summary>
        public void Update(string wanted, bool frozen, float dt)
        {
            wanted = wanted ?? "";
            if (wanted != CurrentTrack) SwitchTo(wanted);
            TargetVolume = ComputeTarget(frozen);
            float step = FadeSeconds <= 0f ? 1f : dt / FadeSeconds;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot s = slots[i];
                s.level = Mathf.MoveTowards(s.level, s.active ? 1f : 0f, step);
                s.src.volume = s.level * TargetVolume;
                if (!s.active && s.level <= 0f && s.src.isPlaying) s.src.Stop();
            }
        }

        void SwitchTo(string slot)
        {
            CurrentTrack = slot;
            int old = activeIdx;
            if (old >= 0) slots[old].active = false;
            activeIdx = -1;
            if (slot.Length == 0) return;

            // 쓸 칸: 방금 끝나는 칸의 반대쪽(곡이 없던 상태에서는 소리가 더 작은 칸)
            int idx = old >= 0 ? 1 - old : (slots[0].level <= slots[1].level ? 0 : 1);
            Slot s = slots[idx];
            s.active = true;
            s.level = 0f;
            activeIdx = idx;
            AudioClip clip = set != null ? set.ClipOf(slot) : null;
            if (clip == null) { s.src.Stop(); return; }   // 비어 있는 슬롯: 슬롯 이름만 바꾸고 조용히 넘어간다
            s.src.clip = clip;
            s.src.loop = !BgmSet.IsSting(slot);
            s.src.volume = 0f;
            s.src.Play();
        }
    }
}
