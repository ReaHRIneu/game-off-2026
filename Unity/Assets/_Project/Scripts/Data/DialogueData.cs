using System;
using UnityEngine;

namespace GameOff.Data
{
    public enum Speaker { Client, Me }

    /// <summary>문자 메시지 한 줄. 화자(CLIENT/ME), 시각 글자, 내용, 그 줄에서 해커 초상의 표정.</summary>
    [Serializable]
    public struct DialogueLine
    {
        public Speaker speaker;
        public string time;         // 예: "21:04" (고정 글자)
        public string text;
        public string hackerFace;   // 초상 표정 이름: neutral, disgust, joy, surprise, fear, sadness, anger
    }

    /// <summary>
    /// 의뢰 1개의 시작(브리핑)·종료 대화 데이터(D-045, D-046). 대사 내용은 시나리오 확정 때 이 에셋만 교체한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue", menuName = "GameOff/Dialogue Data")]
    public sealed class DialogueData : ScriptableObject
    {
        public DialogueLine[] briefing;
        public DialogueLine[] ending;
    }
}
