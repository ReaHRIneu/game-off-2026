using System;
using UnityEngine;

namespace GameOff.Data
{
    [Serializable]
    public sealed class RequestEntry
    {
        public string id;
        public string client;
        public string title;
        [TextArea] public string summary;   // 줄바꿈으로 여러 줄
        public string[] missionNames;
        public LevelData level;             // 없으면 잠긴 빈 자리
    }

    /// <summary>의뢰 목록. 순서가 곧 번호와 잠금 해제 순서다.</summary>
    [CreateAssetMenu(fileName = "RequestList", menuName = "GameOff/Request List")]
    public sealed class RequestList : ScriptableObject
    {
        public RequestEntry[] entries;
    }
}
