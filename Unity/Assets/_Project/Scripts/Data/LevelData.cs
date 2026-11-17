using System;
using UnityEngine;

namespace GameOff.Data
{
    /// <summary>섹터에서 층 어긋남으로 바뀌는 대상 종류(D-065).</summary>
    public enum SectorTarget { Objects, Whole }

    /// <summary>오브젝트 종류(02-02 §0-2-2). 문·계단·엘리베이터만 현실/설계도 상태가 다를 수 있다.</summary>
    public enum ObjectKind { Door, Stairs, Elevator, Terminal, Relay, Exit }

    /// <summary>튜토리얼 안내 단계로 넘어가는 조건(W-005, 03-01 §1-1·§1-2).</summary>
    public enum TutorialCond
    {
        Start,          // 처음부터
        ShiftReleased,  // 처음 Shift를 뗀 뒤
        TargetWaiting,  // 대상이 (floor, col) 문·차단막 앞에서 기다릴 때(col이 0 미만이면 층만 본다)
        AimAt,          // 조준이 (floor, col) 섹터에 왔을 때
        RelayUsed,      // 대상이 중계기를 지난 뒤
    }

    /// <summary>튜토리얼 안내 한 단계. key가 비어 있지 않으면 안내 상자에 키 배지를 함께 그린다.</summary>
    [Serializable]
    public struct TutorialStep
    {
        public TutorialCond cond;
        public int floor, col;
        public string key;               // 키 배지 글자(예: SHIFT, →). 없으면 비움
        public string text;
    }

    /// <summary>
    /// 섹터 하나의 정의. 목록에 없는 섹터는 "오브젝트형"이다.
    /// 섹터 전체형이면 현실/설계도의 차단막(켜짐 여부)이 섹터 상태다.
    /// </summary>
    [Serializable]
    public struct SectorDef
    {
        public int floor, col;
        public SectorTarget target;
        public bool realityBlocked;      // 섹터 전체형: 현실 상태에서 차단막이 켜져 있는가
        public bool blueprintBlocked;    // 섹터 전체형: 설계도 상태에서 차단막이 켜져 있는가
    }

    /// <summary>
    /// 섹터에 속한 오브젝트 하나. x는 섹터 왼쪽 기준 px이며 "대상 중심이 닿는 위치"(문은 막는 선)다.
    /// </summary>
    [Serializable]
    public struct ObjectDef
    {
        public ObjectKind kind;
        public int floor, col;
        public float x;                  // 상호작용 위치(섹터 왼쪽 기준 px)
        public float drawX;              // 그림 왼쪽 위치(섹터 왼쪽 기준 px, 음수 가능). 시안 scene_building.py 배치
        public bool drawSprite;          // false면 현실 그림을 그리지 않음(설계도 겹침만)
        public bool realityOn;           // 문·계단: 열림 / 엘리베이터: 작동
        public bool blueprintOn;
        public bool hasLink;             // 계단·엘리베이터: 연결 정보
        public int linkFloor, linkCol;
        public float linkX;              // 도착 위치(섹터 왼쪽 기준 px, 대상 중심)
        public bool linkFacingRight;     // 도착 후 바라보는 방향
        public bool shareWithLink;       // true면 연결된 섹터를 조준해도 이 오브젝트가 설계도 상태가 된다(엘리베이터)
        public int mission;              // -1 없음, 0 최종, 1 A, 2 B (미션 목록 순서)
    }

    /// <summary>
    /// 의뢰 1개의 측면 단면도 맵(W-004). 층은 위에서 0, 열은 왼쪽에서 0.
    /// 섹터 크기·지붕 높이·바닥 두께는 배경 PNG(manifest 메타)와 같아야 한다.
    /// 아래 realityRows·blueprintRows는 W-001 타일 격자 시절 데이터로, 지금은 쓰지 않는다(삭제하지 않고 보존).
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "GameOff/Level Data")]
    public sealed class LevelData : ScriptableObject
    {
        public string title = "Test request";

        [Header("섹터 모델 (W-004)")]
        public int floors = 3;
        public int cols = 5;
        public int sectorWidth = 160;
        public int sectorHeight = 96;
        public int roofHeight = 24;
        public int wallThickness = 6;
        public int groundHeight = 32;
        public string backgroundKey = "scenes/building_test_base";
        public SectorDef[] sectors;
        public ObjectDef[] objects;
        public int startFloor, startCol;
        public float startX;
        public bool startFacingRight = true;

        [Header("튜토리얼 (W-005, 챕터 0 전용)")]
        public bool tutorial;                    // 켜면 단계 안내를 띄우고 추적도가 최대여도 게임오버가 없다
        public TutorialStep[] tutorialSteps;
        public string starTwoHint;               // 이 의뢰를 ★★로 끝냈을 때 결과 창에 한 줄(없으면 비움)

        [Header("W-001 타일 격자 (사용 안 함)")]
        public string[] realityRows;
        public string[] blueprintRows;
    }
}
