using System;
using UnityEngine;

namespace GameOff.Audio
{
    /// <summary>효과음 항목 하나(W-020, sfx-plan §2·§4). clips가 비어 있으면 그 ID는 소리가 나지 않는다(G08·A05는 코드 합성 소리가 대신 난다).</summary>
    [Serializable]
    public sealed class SfxEntry
    {
        [Tooltip("소리 번호(sfx-plan §2). 예: U01")]
        public string id;
        [Tooltip("후보 클립. 둘 이상이면 재생할 때마다 무작위로 고른다")]
        public AudioClip[] clips;
        [Tooltip("음량 보정(dB). 사건음 0, UI·발소리 -8, 나머지 -3이 처음 값 [제안]")]
        public float gainDb;
        [Tooltip("재생마다 음높이를 ±이 반음 안에서 무작위로 흔든다")]
        public float pitchRandomSemitones;
        [Tooltip("반복 재생(StartLoop·StopLoop로 켜고 끈다). G04·E01만 켠다")]
        public bool loop;
    }

    /// <summary>
    /// 효과음 목록(W-020): ID → 클립·음량·음높이 무작위. 재사용 ID(U08 = U02·U03, D03 = U01)와 BGM 효과곡(T05·T06)은 코드에서 다른 소리로 처리하지만
    /// 배정표 전체를 빈 항목으로 둔다. 클립은 GameOff/Import Audio 메뉴가 채운다.
    /// </summary>
    [CreateAssetMenu(fileName = "SfxSet", menuName = "GameOff/Sfx Set")]
    public sealed class SfxSet : ScriptableObject
    {
        /// <summary>sfx-plan §2의 모든 ID(48개).</summary>
        public static readonly string[] AllIds =
        {
            "U01", "U02", "U03", "U04", "U05", "U06", "U07", "U08", "U09",
            "D01", "D02", "D03",
            "G01", "G02", "G03", "G04", "G05", "G06", "G07", "G08",
            "O01", "O02", "O03", "O04", "O05", "O06",
            "A01", "A02", "A03", "A04", "A05", "A06", "A07", "A08", "A09", "A10", "A11", "A12",
            "T01", "T02", "T03", "T04", "T05", "T06", "T07", "T08",
            "E01", "E02",
        };

        public SfxEntry[] entries = Array.Empty<SfxEntry>();

        /// <summary>그 ID의 항목(없으면 null).</summary>
        public SfxEntry Find(string id)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].id == id) return entries[i];
            return null;
        }

        /// <summary>처음 음량(dB) [제안, sfx-plan §4]: UI·대화·발소리 -8, 조준·Shift 험·글리치·환경음 -3, 나머지 사건음 0.</summary>
        public static float DefaultGainDb(string id)
        {
            switch (id[0])
            {
                case 'U':
                case 'D': return -8f;
                case 'E': return -3f;
            }
            switch (id)
            {
                case "A01": return -8f;
                case "G01":
                case "G02":
                case "G04":
                case "T08": return -3f;
            }
            return 0f;
        }

        /// <summary>처음 음높이 무작위(반음): 자주 나는 U01·G01·A01만 ±1.</summary>
        public static float DefaultPitchSemitones(string id) => id == "U01" || id == "G01" || id == "A01" ? 1f : 0f;

        /// <summary>처음 반복 여부: G04·E01만 켠다.</summary>
        public static bool DefaultLoop(string id) => id == "G04" || id == "E01";
    }
}
