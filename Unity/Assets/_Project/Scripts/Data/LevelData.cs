using UnityEngine;

namespace GameOff.Data
{
    /// <summary>
    /// 의뢰 1개의 두 층(현실·설계도) 문자열 격자.
    /// 범례: # 벽, . 바닥, S 시작(바닥), D 닫힌 문, O 열린 문, &gt; &lt; ^ v 방향 칸, E 출구, A·B 체크 지점, R 중계기.
    /// 좌표는 (x, y), 왼쪽 위가 (0,0). 시작 위치는 현실 층의 S를 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "GameOff/Level Data")]
    public sealed class LevelData : ScriptableObject
    {
        public string title = "Test request";
        public string[] realityRows;
        public string[] blueprintRows;
    }
}
