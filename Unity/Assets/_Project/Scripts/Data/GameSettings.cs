using UnityEngine;

namespace GameOff.Data
{
    /// <summary>
    /// 임시 밸런스 값 모음(근거: Docs/Planning/02-01-system-design.md의 [임시] 값).
    /// 밸런스 기획 때 이 에셋 하나만 바꾼다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "GameOff/Game Settings")]
    public sealed class GameSettings : ScriptableObject
    {
        [Header("추적도")]
        [Min(1f)] public float trackingMax = 100f;
        [Min(0f)] public float trackingRatePerSecond = 20f;
        [Min(0f)] public float missionRelief = 30f;
        [Min(0f)] public float relayRelief = 40f;
        [Min(0.1f)] public float countdownSeconds = 5f;

        [Header("대상 이동")]
        [Min(0f)] public float startDelaySeconds = 3f;
        [Min(0.05f)] public float moveIntervalSeconds = 0.5f;
    }
}
