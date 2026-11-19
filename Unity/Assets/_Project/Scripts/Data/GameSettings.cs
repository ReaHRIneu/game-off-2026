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
        [Min(0.05f)] public float moveIntervalSeconds = 0.5f;   // W-001 칸 이동 간격(섹터 모델에서는 쓰지 않음)
        [Min(1f)] public float targetSpeed = 48f;               // [제안] W-004 대상 이동 속도(px/초), 바닥선을 따라 연속 이동
        [Min(1f)] public float aimSideWeight = 2f;              // 조준 점프: 옆 축 거리 가중치

        [Header("카메라 [제안]")]
        [Min(0f)] public float cameraSeconds = 0.2f;            // 조준 섹터를 따라가는 이동 연출 시간

        [Header("초상 표정 [제안]")]
        [Min(0f)] public float surpriseTracking = 70f;         // 추적도가 이 값 이상이면 surprise(카운트다운 중은 fear)

        [Header("화면 연출 [제안]")]
        [Min(0.05f)] public float bootStageSeconds = 0.375f;   // 모니터 켜짐 로딩 4단계(합계 1.5초)
        [Min(0.05f)] public float connectSeconds = 1f;         // 게임 진입 로딩
        [Min(0f)] public float panelSlideSeconds = 0.25f;      // 상세 패널 등장

        [Header("인물·감시 D-098 (W-016, 02-06 §7-2 ① 공통 규칙 값)")]
        [Min(0f)] public float helperAlertDistance = 48f;      // 조력자 경계 거리(px): 앞쪽 이 거리 안의 켜진 감시 구역 경계에서 돌아선다(D-096)
        [Min(0f)] public float detectTracking = 40f;           // 발각 추적도(기본). 종류·의뢰 배치에서 덮어쓸 수 있다(D-094)
        [Min(0.05f)] public float alertMarkSeconds = 0.5f;     // 돌아설 때 머리 위 ! 표시 시간
    }
}
