using System;
using UnityEngine;

namespace GameOff.Data
{
    /// <summary>섹터에서 층 어긋남으로 바뀌는 대상 종류(D-065).</summary>
    public enum SectorTarget { Objects, Whole }

    /// <summary>
    /// 오브젝트 종류(02-02 §0-2-2). 문·계단·엘리베이터·해치만 현실/설계도 상태가 다를 수 있다.
    /// Hatch(W-007): 계단과 같은 연결 규칙(열림이면 연결 섹터로 이동). FailPoint(W-007): 대상 중심이 지나가면 의뢰 실패 → 즉시 재시작.
    /// Camera(W-016): 감시 카메라(방에 속한 상태 오브젝트, 02-06 §4). 켜져 있으면 그 방 전체가 감시 구역.
    /// Press(W-035, D-112): 4박 주기로 오르내리는 프레스(챕터 2). x는 프레스 가운데(섹터 왼쪽 기준 px), 폭은 BeatClock.PressHalfWidth x 2. 현실·설계도 상태는 쓰지 않고 박자 시계(GameSim)가 정한다.
    /// 직렬화 값이 바뀌지 않도록 새 종류는 끝에 추가한다.
    /// </summary>
    public enum ObjectKind { Door, Stairs, Elevator, Terminal, Relay, Exit, Hatch, FailPoint, Camera, Press }

    /// <summary>
    /// 미션 종류(02-05 §2, W-007). 목록의 0번이 최종 미션이다.
    /// PassObject: 오브젝트(ObjectDef.mission이 이 미션의 번호)를 지남 / EnterSector: 대상이 (floor, col) 섹터에 들어감 /
    /// NeverEnterFloor: 그 층에 들어가는 순간 실패 확정 / TraceLimit: 추적도가 limit을 넘는 순간 실패 확정 /
    /// Unseen(W-016, D-098): 감시에 한 번도 발각되지 않음 — 발각 순간 실패 확정, 끝까지 발각이 없으면 완료. 새 종류는 끝에 추가한다.
    /// </summary>
    public enum MissionKind { PassObject, EnterSector, NeverEnterFloor, TraceLimit, Unseen }

    /// <summary>미션 하나의 정의. 이름은 RequestEntry.missionNames(같은 순서)에 있다.</summary>
    [Serializable]
    public struct MissionDef
    {
        public MissionKind kind;
        public int floor, col;           // EnterSector: 섹터 / NeverEnterFloor: floor만
        public float limit;              // TraceLimit: 추적도 기준(이 값을 넘으면 실패)
    }

    /// <summary>튜토리얼 안내 단계로 넘어가는 조건(W-005, 03-01 §1-1·§1-2).</summary>
    public enum TutorialCond
    {
        Start,          // 처음부터
        ShiftReleased,  // 처음 Shift를 뗀 뒤
        TargetWaiting,  // 대상이 (floor, col) 문·차단막 앞에서 기다릴 때(col이 0 미만이면 층만 본다)
        AimAt,          // 조준이 (floor, col) 섹터에 왔을 때
        RelayUsed,      // 대상이 중계기를 지난 뒤
        TargetTurnedBack, // 대상이 floor층에서 왼쪽을 보고 있을 때(끝 벽에서 돌아선 뒤, W-007)
        FirstPushed,    // 방을 처음 밀었을 때(W-014). 새 조건은 끝에 추가한다
        TargetOnFloor,  // W-033: 대상이 floor층에 있을 때
        TargetPast,     // W-033: 대상이 floor층에서 절대 x(col * 섹터 폭 + x)에 닿았을 때. facingRight면 오른쪽을 보며 x 이상, 아니면 왼쪽을 보며 x 이하
    }

    /// <summary>W-033: 튜토리얼 단계가 끝나려면 필요한 입력(D-122).</summary>
    public enum TutorialInput
    {
        Enter,       // Enter 한 번(설명만 있는 단계)
        Press,       // 방향키 한 번(keyDx·keyDy 방향, 둘 다 0이면 방향키 아무거나). untilAim이면 조준이 aimFloor·aimCol에 갈 때까지 반복
        Hold,        // 왼쪽 Shift를 누르기 시작하면 끝(그 뒤 게임은 누르는 동안 정상 진행)
        ShiftArrow,  // Shift를 누른 채 keyDx·keyDy 방향키로 방을 밀면 끝
        ShiftDigit,  // W-038: Shift를 누른 채 숫자 1~3(조준한 시계 방에 들어가야 함)이 받아들여지면 끝
    }

    /// <summary>W-033: 튜토리얼이 어둡게 덮을 때 뚫어 두는 영역 종류.</summary>
    public enum TutorialFocus
    {
        None,
        Scene,   // 장면 뷰 전체
        Trace,   // 아래 TRACE 게이지
        Mission, // 오른쪽 MISSION 패널
        Room,    // (floor, col) 방(원래 자리 기준, 밀려도 방을 따라간다)
        Cell,    // (floor, col) 칸(고정, 빈 칸 포함)
        BeatBar, // W-038: 장면 왼쪽 위 박자 표시
    }

    /// <summary>튜토리얼 안내 한 단계. key가 비어 있지 않으면 안내 상자에 키 배지를 함께 그린다.</summary>
    [Serializable]
    public struct TutorialStep
    {
        public TutorialCond cond;
        public int floor, col;
        public string key;               // 키 배지 글자(예: SHIFT, →). 없으면 비움
        public string text;
        public string key2;              // (사용 안 함, W-033부터 key에 공백으로 나열)
        [Header("W-033 조작 튜토리얼(어둡게 + 네온 강조 + 키 입력 대기)")]
        public TutorialInput input;
        public int keyDx, keyDy;         // Press·ShiftArrow가 받는 방향. 둘 다 0이면 방향키 아무거나(Press)
        public bool untilAim;            // Press: 조준이 (aimFloor, aimCol)에 갈 때까지 같은 단계에 머문다
        public int aimFloor, aimCol;
        public float x;                  // TargetPast 조건의 섹터 안 x
        public bool facingRight;         // TargetPast 조건의 방향
        public TutorialFocus focus, focus2;   // 뚫을 영역(최대 2개)
        public int focusFloor, focusCol, focus2Floor, focus2Col;
    }

    /// <summary>방 칸 하나의 위치(층, 열). 빈 칸 목록에 쓴다(W-014).</summary>
    [Serializable]
    public struct CellDef
    {
        public int floor, col;
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
        public bool rail;                // W-014: 밀기 표시(레일). 오브젝트형·섹터 전체형 모두 가질 수 있다. 새 필드는 끝에 둔다
        public bool clock;               // W-035: 시계 표시(톱니). Shift + 숫자로 이 방의 박자를 앞당길 수 있다(챕터 2, D-112)
        public bool periodicLaser;       // W-038: 주기 레이저. 섹터 전체형 차단막이 박자 표(켜짐·켜짐·켜짐·꺼짐)대로 켜지고 꺼진다. 시계 표시를 함께 켠 것으로 본다(Board가 맞춤)
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
        public int mission;              // -1 없음, 아니면 LevelData.missions의 번호(0 = 최종, 1~ = 나머지). 목록이 비면 0 최종·1 A·2 B(W-005 형식)
        [Header("감시 카메라 (W-016, 02-06 §7-4)")]
        [Tooltip("kind가 Camera일 때 종류 정의(② SurveillanceType). 상태·구역·재발각 대기는 여기서 읽는다")]
        public SurveillanceType surveillance;
        [Tooltip("kind가 Camera일 때 발각 추적도 덮어쓰기. 0 미만이면 종류 값, 그것도 없으면 공통 값을 쓴다. 구조체라 기본값을 못 넣으므로 만드는 쪽(Mvp1Setup)이 -1로 채운다")]
        public float detectTrackingOverride;
        [Tooltip("W-033: 그림 이름(objects/ 뒤, 예: rollshutter). 있으면 종류 기본 그림 대신 <이름>_open·_closed(설계도 bp_ 접두)를 쓴다. 판정은 종류 그대로")]
        public string artKey;
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
        public string buildingName = "";         // 장면 패널 머리글에 보이는 건물 이름(W-006)

        [Header("섹터 모델 (W-004)")]
        public int floors = 3;
        public int cols = 5;
        public int sectorWidth = 160;
        public int sectorHeight = 96;
        public int roofHeight = 24;
        public int wallThickness = 6;
        public int groundHeight = 32;
        public string backgroundKey = "scenes/building_test_base";
        [Tooltip("W-010: 맵 배치 키(예: scenes128/ch0_home1/layout). 값이 있으면 방 레이어(128px)로 그리고, 비어 있으면 backgroundKey 한 장 그림을 쓴다")]
        public string layoutKey = "";
        [Tooltip("layoutKey가 가리키는 layout.json(Setup이 연결)")]
        public TextAsset layoutJson;
        public SectorDef[] sectors;
        public ObjectDef[] objects;
        public int startFloor, startCol;
        public float startX;
        public bool startFacingRight = true;
        public bool hasStartAim;                 // true면 처음 조준 섹터를 아래 값으로 정한다(W-007). false면 시작 섹터에서 가장 가까운 조작 가능 섹터
        public int startAimFloor, startAimCol;

        [Header("미션 목록 (W-007)")]
        public MissionDef[] missions;            // 0번 = 최종. 비어 있으면 W-005 형식(최종 + 단말 A·B 통과)으로 본다

        [Header("튜토리얼 (W-005, 챕터 0 전용)")]
        public bool tutorial;                    // 켜면 단계 안내를 띄우고 추적도가 최대여도 게임오버가 없다
        public TutorialStep[] tutorialSteps;
        public string starTwoHint;               // 이 의뢰를 ★★로 끝냈을 때 결과 창에 한 줄(없으면 비움)

        [Header("W-001 타일 격자 (사용 안 함)")]
        public string[] realityRows;
        public string[] blueprintRows;

        [Header("방 밀기 (W-014, 챕터 1)")]
        [Tooltip("방이 없는 칸(빈 칸). 대상에게는 끝 벽이다. 비어 있으면 지금과 같다(맵 가장자리만 끝 벽)")]
        public CellDef[] emptyCells;
        [Tooltip("튜토리얼이 아니어도 단계 안내만 켠다(카운트다운·게임오버는 그대로)")]
        public bool showGuide;

        [Header("추적도 완화 (W-031, D-120)")]
        [Tooltip("Shift 중 추적도 상승 배율. 상승량 = GameSettings.trackingRatePerSecond x 이 값. 챕터 1 의뢰 0.7, 그 밖 1.0")]
        public float trackingRateScale = 1f;

        [Header("바깥 환경 (W-011, D-103)")]
        [Tooltip("바깥 시간대. 의뢰마다 고정이며 플레이 중에는 바뀌지 않는다. 기본 Night")]
        public TimeOfDay timeOfDay = TimeOfDay.Night;
        [Tooltip("바깥 날씨. 기본 Clear. 규칙·판정에는 영향이 없다(연출만)")]
        public Weather weather = Weather.Clear;
    }
}
