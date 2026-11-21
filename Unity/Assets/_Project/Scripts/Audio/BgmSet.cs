using System;
using UnityEngine;

namespace GameOff.Audio
{
    /// <summary>곡 슬롯 하나(W-020, bgm-plan §4-3). clip이 비어 있으면 그 슬롯은 무음이다.</summary>
    [Serializable]
    public sealed class BgmEntry
    {
        public string slot;
        public AudioClip clip;
    }

    /// <summary>BGM 곡 슬롯 8개: 곡 6 + 효과곡 2(sting_*, 1회만 재생). 클립은 GameOff/Import Audio 메뉴가 채운다.</summary>
    [CreateAssetMenu(fileName = "BgmSet", menuName = "GameOff/Bgm Set")]
    public sealed class BgmSet : ScriptableObject
    {
        public const string Title = "bgm_title", Desk = "bgm_desk", Briefing = "bgm_briefing", Ch0 = "bgm_ch0", Ch1 = "bgm_ch1", Alert = "bgm_alert",
            StingClear = "sting_clear", StingGameOver = "sting_gameover";

        public static readonly string[] Slots = { Title, Desk, Briefing, Ch0, Ch1, Alert, StingClear, StingGameOver };

        public BgmEntry[] entries = Array.Empty<BgmEntry>();

        /// <summary>효과곡(sting_*)은 반복하지 않고 1번만 재생한다.</summary>
        public static bool IsSting(string slot) => slot != null && slot.StartsWith("sting_", StringComparison.Ordinal);

        public AudioClip ClipOf(string slot)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].slot == slot) return entries[i].clip;
            return null;
        }

        public BgmEntry Find(string slot)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].slot == slot) return entries[i];
            return null;
        }
    }
}
