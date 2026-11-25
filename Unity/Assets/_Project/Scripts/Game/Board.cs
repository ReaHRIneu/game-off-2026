using System.Collections.Generic;
using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>방향키 방향(조준 점프 확인용).</summary>
    public enum Dir { Right, Down, Left, Up }

    /// <summary>맵에 놓인 오브젝트 하나(LevelData의 ObjectDef + 맵 좌표).</summary>
    public sealed class SectorObject
    {
        public ObjectDef Def;
        public int Floor => Def.floor;
        public int Col => Def.col;
        /// <summary>맵 전체 기준 상호작용 x(px).</summary>
        public float AbsX;
        /// <summary>맵 전체 기준 그림 왼쪽 x(px).</summary>
        public int DrawAbsX;
    }

    /// <summary>
    /// LevelData(측면 단면도 섹터 모델)를 해석한 결과(W-004). 층은 위에서 0, 열은 왼쪽에서 0.
    /// 좌표는 맵 왼쪽 위가 (0,0)인 px이다. 타일 격자 해석(W-001)은 이 모델로 대체했다.
    /// </summary>
    public sealed class Board
    {
        /// <summary>대상 몸통 절반 폭(px). 문·차단막 앞에서 이만큼 떨어져 선다. 도트 그림 courier의 불투명 폭 약 18px 기준.</summary>
        public const float BodyHalf = 9f;

        public int Floors { get; }
        public int Cols { get; }
        public int SectorW { get; }
        public int SectorH { get; }
        public int Roof { get; }
        public int Wall { get; }
        public int MapW => Cols * SectorW;
        public int MapH { get; }
        public string BackgroundKey { get; }
        /// <summary>방 레이어 배치(W-010). null이면 BackgroundKey 한 장 그림으로 그린다. 빛 상태도 여기에 있다.</summary>
        public SceneLayout Layout { get; }
        public string LayoutKey { get; }
        /// <summary>장면 패널 머리글에 보이는 건물 이름(W-006).</summary>
        public string BuildingName { get; }
        /// <summary>튜토리얼 의뢰인가(W-005). 켜져 있으면 추적도가 최대여도 게임오버가 없다.</summary>
        public bool Tutorial { get; }
        /// <summary>Shift 중 추적도 상승 배율(W-031, D-120). 기본 1.</summary>
        public float TrackingRateScale { get; }
        public IReadOnlyList<SectorObject> Objects => objects;
        /// <summary>미션 목록(0번 = 최종). 레벨에 목록이 없으면 W-005 형식(최종 + 단말 A·B 통과)으로 만든다.</summary>
        public MissionDef[] Missions { get; }
        /// <summary>처음 조준 섹터를 데이터로 정했는가(W-007).</summary>
        public bool HasStartAim { get; }
        public Vector2Int StartAim { get; }
        public int StartFloor { get; }
        public float StartX { get; }
        public bool StartFacingRight { get; }
        public Vector2Int StartSector => new Vector2Int(startCol, StartFloor);
        /// <summary>레일이 있는 방이 하나라도 있는가(W-014). 없으면 방 밀기 입력은 아무 일도 하지 않는다(챕터 0과 같은 동작).</summary>
        public bool HasRails { get; }
        /// <summary>시계 표시가 있는 방이 하나라도 있는가(W-035). 없으면 박자 시계·Shift + 숫자는 아무 일도 하지 않는다(챕터 0·1과 같은 동작).</summary>
        public bool HasClocks { get; }
        /// <summary>주기 레이저 방이 하나라도 있는가(W-038).</summary>
        public bool HasPeriodicLasers { get; }
        /// <summary>바깥 시간대(W-011). 의뢰마다 고정이다.</summary>
        public TimeOfDay TimeOfDay { get; }
        /// <summary>바깥 날씨(W-011). 의뢰마다 고정이다.</summary>
        public Weather Weather { get; }
        /// <summary>환경 입자 난수 시드(W-011): 의뢰 에셋 이름·제목에서 정한다. 같은 의뢰는 다시 해도 같은 모습이 된다.</summary>
        public int EnvSeed { get; }

        const int LayoutPx = 4;   // 방 그림 1px = 논리 1/4px (PixelCanvas.Z와 같다)
        readonly int startCol;
        readonly List<SectorObject> objects = new List<SectorObject>();
        readonly bool[,] whole;
        readonly bool[,] realBlocked;
        readonly bool[,] bpBlocked;
        readonly bool[,] rail;       // W-014: 밀기 표시(레일)가 있는 방
        readonly bool[,] empty;      // W-014: 방이 없는 칸(원래 자리 기준)
        readonly bool[,] clock;      // W-035: 시계 표시(톱니)가 있는 방. Shift + 숫자로 박자를 앞당길 수 있다
        readonly bool[,] periodic;   // W-038: 주기 레이저 방(섹터 전체형 + 시계 표시)

        public Board(LevelData level)
        {
            Floors = level.floors;
            Cols = level.cols;
            SectorW = level.sectorWidth;
            SectorH = level.sectorHeight;
            Roof = level.roofHeight;
            Wall = level.wallThickness;
            MapH = Roof + Floors * SectorH + level.groundHeight;
            BackgroundKey = level.backgroundKey;
            BuildingName = level.buildingName ?? "";
            LayoutKey = level.layoutKey ?? "";
            TimeOfDay = level.timeOfDay;
            Weather = level.weather;
            EnvSeed = StableHash((level.name ?? "") + "|" + (level.title ?? "") + "|" + LayoutKey);
            if (level.layoutJson != null && LayoutKey.Length > 0)
            {
                // 레벨 시작 때 한 번 읽어 배열로 보관한다(프레임마다 읽지 않음)
                SceneLayout lay = null;
                try { lay = SceneLayout.Parse(level.layoutJson.text, level.timeOfDay == TimeOfDay.Night); }
                catch (System.Exception e) { Debug.LogError("[Board] layout.json을 읽지 못했습니다: " + e.Message); }
                if (lay != null && (lay.Floors != Floors || lay.Cols != Cols || lay.SectorPxW != SectorW * LayoutPx || lay.SectorPxH != SectorH * LayoutPx
                    || lay.RoofPx != Roof * LayoutPx || lay.GroundPx != level.groundHeight * LayoutPx))
                {
                    Debug.LogError("[Board] layout.json 크기가 레벨 데이터와 맞지 않습니다: " + LayoutKey);
                    lay = null;
                }
                Layout = lay;
            }
            Tutorial = level.tutorial;
            TrackingRateScale = level.trackingRateScale;
            StartFloor = level.startFloor;
            startCol = level.startCol;
            StartX = level.startCol * level.sectorWidth + level.startX;   // 맵 전체 기준 px. 시작 섹터(열)의 왼쪽에서 startX(W-007: 시작 열이 0이 아닌 의뢰)
            StartFacingRight = level.startFacingRight;
            HasStartAim = level.hasStartAim;
            StartAim = new Vector2Int(level.startAimCol, level.startAimFloor);
            Missions = level.missions != null && level.missions.Length > 0
                ? level.missions
                : new[] { new MissionDef { kind = MissionKind.PassObject }, new MissionDef { kind = MissionKind.PassObject }, new MissionDef { kind = MissionKind.PassObject } };

            whole = new bool[Floors, Cols];
            realBlocked = new bool[Floors, Cols];
            bpBlocked = new bool[Floors, Cols];
            rail = new bool[Floors, Cols];
            empty = new bool[Floors, Cols];
            clock = new bool[Floors, Cols];
            periodic = new bool[Floors, Cols];
            if (level.sectors != null)
                foreach (SectorDef s in level.sectors)
                {
                    if (!InBounds(s.floor, s.col)) { Debug.LogError("[Board] 섹터 위치가 맵 밖입니다: " + s.floor + "," + s.col); continue; }
                    whole[s.floor, s.col] = s.target == SectorTarget.Whole;
                    realBlocked[s.floor, s.col] = s.realityBlocked;
                    bpBlocked[s.floor, s.col] = s.blueprintBlocked;
                    rail[s.floor, s.col] = s.rail;
                    if (s.rail) HasRails = true;
                    clock[s.floor, s.col] = s.clock;
                    if (s.clock) HasClocks = true;
                    if (s.periodicLaser)
                    {
                        // W-038: 주기 레이저는 섹터 전체형 차단막이고 시계 표시를 함께 가진다. 현실·설계도 값이 같아 Shift만으로는 꺼지지 않는다(박자 표가 상태를 정한다)
                        periodic[s.floor, s.col] = true;
                        whole[s.floor, s.col] = true;
                        realBlocked[s.floor, s.col] = bpBlocked[s.floor, s.col] = true;
                        clock[s.floor, s.col] = true;
                        HasClocks = true;
                        HasPeriodicLasers = true;
                    }
                }
            if (level.emptyCells != null)
                foreach (CellDef e in level.emptyCells)
                {
                    if (!InBounds(e.floor, e.col)) { Debug.LogError("[Board] 빈 칸 위치가 맵 밖입니다: " + e.floor + "," + e.col); continue; }
                    empty[e.floor, e.col] = true;
                }
            if (level.objects != null)
                foreach (ObjectDef d in level.objects)
                {
                    if (!InBounds(d.floor, d.col)) { Debug.LogError("[Board] 오브젝트 위치가 맵 밖입니다: " + d.floor + "," + d.col); continue; }
                    // W-016: 감시 카메라의 켜짐 상태(현실·설계도)는 종류 정의(② SurveillanceType)가 정한다. 종류가 없으면 배치 값을 그대로 쓴다
                    ObjectDef def = d;
                    if (def.kind == ObjectKind.Camera && def.surveillance != null)
                    {
                        def.realityOn = def.surveillance.realityOn;
                        def.blueprintOn = def.surveillance.blueprintOn;
                    }
                    objects.Add(new SectorObject
                    {
                        Def = def,
                        AbsX = d.col * SectorW + d.x,
                        DrawAbsX = d.col * SectorW + Mathf.RoundToInt(d.drawX),
                    });
                }
        }

        /// <summary>문자열 해시(실행마다·기기마다 같은 값이어야 해서 string.GetHashCode를 쓰지 않는다).</summary>
        static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; i++) h = (h ^ s[i]) * 16777619u;
                return (int)h;
            }
        }

        public bool InBounds(int floor, int col) => floor >= 0 && col >= 0 && floor < Floors && col < Cols;

        // ---------- 좌표 ----------

        public int SectorLeft(int col) => col * SectorW;
        public int SectorTop(int floor) => Roof + floor * SectorH;
        /// <summary>그 층의 바닥선 y(그림 아래 끝이 닿는 선).</summary>
        public int FloorLineY(int floor) => SectorTop(floor) + SectorH - Wall;
        public Vector2 SectorCenter(int floor, int col) => new Vector2(col * SectorW + SectorW * 0.5f, SectorTop(floor) + SectorH * 0.5f);

        // ---------- 섹터 상태 ----------

        /// <summary>섹터 전체형인가(아니면 오브젝트형).</summary>
        public bool IsWhole(int floor, int col) => whole[floor, col];

        /// <summary>섹터 전체형의 차단막 켜짐 여부. blueprint가 true면 설계도 상태.</summary>
        public bool Blocked(int floor, int col, bool blueprint) => whole[floor, col] && (blueprint ? bpBlocked[floor, col] : realBlocked[floor, col]);

        /// <summary>방이 없는 칸인가(원래 자리 기준, W-014).</summary>
        public bool IsEmpty(int floor, int col) => empty[floor, col];

        /// <summary>밀기 표시(레일)가 있는 방인가(W-014).</summary>
        public bool HasRail(int floor, int col) => rail[floor, col];

        /// <summary>주기 레이저 방인가(W-038).</summary>
        public bool IsPeriodicLaser(int floor, int col) => periodic[floor, col];

        /// <summary>시계 표시(톱니)가 있는 방인가(W-035).</summary>
        public bool HasClock(int floor, int col) => clock[floor, col];

        /// <summary>조작 가능한 섹터 = 현실 상태와 설계도 상태가 다른 것이 있는 섹터, 또는 레일이 있는 방(W-014: 밀기 방은 조준할 수 있다).</summary>
        public bool IsOperable(int floor, int col)
        {
            if (rail[floor, col] || clock[floor, col]) return true;   // W-035: 시계 방도 조준할 수 있다
            if (whole[floor, col]) return realBlocked[floor, col] != bpBlocked[floor, col];
            foreach (SectorObject o in objects)
                if (o.Floor == floor && o.Col == col && o.Def.realityOn != o.Def.blueprintOn) return true;
            return false;
        }
    }
}
