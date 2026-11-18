using System;
using UnityEngine;

namespace GameOff.Data
{
    /// <summary>글리치 한 단계의 요소 값(vfx-direction §7-2). 한 단계는 stageTicks 틱(1틱 = 1/30초) 동안 유지된다.</summary>
    [Serializable]
    public sealed class GlitchStage
    {
        [Min(1)] public int ticks = 1;
        [Header("줄 밀림")]
        [Range(1, 12)] public int bandHeight = 4;      // 띠 높이(논리 px)
        [Range(0, 8)] public int maxShift = 0;         // 밀림 폭 ±(논리 px)
        [Range(0f, 1f)] public float shiftRatio = 0f;  // 밀리는 띠 비율
        [Range(-1f, 1f)] public float shiftBias = 0f;  // 미는 방향 쪽 치우침(방 밀기)
        [Header("팔레트 색 복사")]
        public GlitchCopy copy = GlitchCopy.None;
        [Range(1, 4)] public int copyOffset = 2;       // 어긋남 폭(논리 px)
        [Range(0f, 1f)] public float copyDensity = 0f;
        [Header("스캔라인·노이즈·블록")]
        [Range(0f, 1f)] public float scan = 0f;
        [Range(0f, 1f)] public float noise = 0f;
        [Range(2, 16)] public int blockSize = 8;       // 블록 깨짐 조각 크기(논리 px)
        [Min(0)] public int blockCount = 0;            // 영역 안 조각 수
    }

    public enum GlitchCopy { None = 0, R = 1, C = 2, RC = 3 }

    /// <summary>역할별 프리셋 하나. 단계(stages)를 순서대로 재생한다. 반복형(loop)은 interval 간격으로 한 번씩 재생한다.</summary>
    [Serializable]
    public sealed class GlitchPreset
    {
        public string id = "";
        public GlitchStage[] stages = new GlitchStage[0];
        [Header("반복형(danger·logo)")]
        public bool loop;
        [Min(0.1f)] public float intervalMin = 2f;     // 다음 재생까지 간격(초)
        [Min(0.1f)] public float intervalMax = 3f;
        [Min(0.1f)] public float fastInterval = 1f;    // 빠른 주기(카운트다운 중 danger)

        /// <summary>전체 길이(틱). 한 번 재생하는 시간 = TotalTicks / 30초.</summary>
        public int TotalTicks
        {
            get
            {
                int t = 0;
                for (int i = 0; i < stages.Length; i++) t += Mathf.Max(1, stages[i].ticks);
                return t;
            }
        }

        public float TotalSeconds => TotalTicks / 30f;
    }

    /// <summary>글리치 셰이더와 프리셋 모음(W-013, D-087). 값은 이 에셋 하나에서 조정한다.</summary>
    [CreateAssetMenu(fileName = "GlitchSet", menuName = "GameOff/Glitch Set")]
    public sealed class GlitchSet : ScriptableObject
    {
        public Shader shader;
        [Range(0f, 1f)] public float dangerTrackingRatio = 0.7f;   // 추적도가 최대의 이 비율 이상이면 danger 반복
        public GlitchPreset[] presets = new GlitchPreset[0];
    }
}
