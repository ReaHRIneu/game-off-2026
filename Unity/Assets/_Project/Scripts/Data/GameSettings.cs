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

        [Header("초상 표정 [제안]")]
        [Min(0f)] public float surpriseTracking = 70f;         // 추적도가 이 값 이상이면 surprise(카운트다운 중은 fear)

        [Header("화면 연출 [제안]")]
        [Min(0.05f)] public float bootStageSeconds = 0.375f;   // 모니터 켜짐 로딩 4단계(합계 1.5초)
        [Min(0.05f)] public float connectSeconds = 1f;         // 게임 진입 로딩
        [Min(0f)] public float panelSlideSeconds = 0.25f;      // 상세 패널 등장
    }
}
