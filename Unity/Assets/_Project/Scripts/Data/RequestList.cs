using System;
using UnityEngine;

namespace GameOff.Data
{
    [Serializable]
    public sealed class RequestEntry
    {
        public string id;
        public int chapter;                 // 챕터 번호(0부터). 바탕화면 챕터 폴더 구분
        public string client;
        public string title;
        [TextArea] public string summary;   // 줄바꿈으로 여러 줄
        public string[] missionNames;
        public LevelData level;             // 없으면 잠긴 빈 자리
        public DialogueData dialogue;       // 시작·종료 대화(없으면 대화 없이 진행)
        [Tooltip("W-035: 정식 의뢰와 구분되는 점검용 시험 의뢰(예: TEST_2-0). 잠금 없이 열려 있고 번호는 챕터-0, 이름은 TEST_챕터-0이다")]
        public bool testEntry;
    }

    /// <summary>의뢰 목록. 순서가 곧 번호와 잠금 해제 순서다.</summary>
    [CreateAssetMenu(fileName = "RequestList", menuName = "GameOff/Request List")]
    public sealed class RequestList : ScriptableObject
    {
        public RequestEntry[] entries;
    }
}
