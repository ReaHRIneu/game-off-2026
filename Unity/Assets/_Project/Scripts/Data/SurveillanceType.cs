using UnityEngine;

namespace GameOff.Data
{
    /// <summary>
    /// 감시 종류 정의(02-06 §7-3 ② 종류 정의, W-016). 이 종류를 쓰는 모든 의뢰가 같은 값을 쓴다.
    /// 의뢰 배치(LevelData.ObjectDef)에서 발각 추적도만 덮어쓸 수 있다. 공통 값은 GameSettings(① 층)에 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "Surveillance", menuName = "GameOff/Surveillance Type")]
    public sealed class SurveillanceType : ScriptableObject
    {
        [Tooltip("종류 ID(영문 소문자). 예: cam_basic")]
        public string id = "cam_basic";
        [Tooltip("감시 구역 칸 수: 1 = 카메라가 속한 섹터만. 2 이상이면 그 방에서 오른쪽으로 이어지는 칸까지")]
        [Min(1)] public int coverCols = 1;
        [Tooltip("현실 상태에서 켜져 있는가")]
        public bool realityOn = true;
        [Tooltip("설계도 상태(Shift로 조준한 방)에서 켜져 있는가")]
        public bool blueprintOn = false;
        [Tooltip("발각 추적도 덮어쓰기. 0 미만이면 공통 값(GameSettings.detectTracking)을 쓴다")]
        public float detectTrackingOverride = -1f;
        [Tooltip("재발각 대기(초): 한 번 발각한 뒤 이 시간 동안 같은 카메라는 다시 발각하지 않는다")]
        [Min(0f)] public float redetectSeconds = 3f;
        [Tooltip("카메라 그림 키(objects128/ 아래, 뒤에 _on·_off가 붙는다). 예: camera")]
        public string artKey = "camera";
    }
}
