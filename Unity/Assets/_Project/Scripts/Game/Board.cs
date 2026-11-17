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
        /// <summary>장면 패널 머리글에 보이는 건물 이름(W-006).</summary>
        public string BuildingName { get; }
        /// <summary>튜토리얼 의뢰인가(W-005). 켜져 있으면 추적도가 최대여도 게임오버가 없다.</summary>
        public bool Tutorial { get; }
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

        readonly int startCol;
        readonly List<SectorObject> objects = new List<SectorObject>();
        readonly bool[,] whole;
        readonly bool[,] realBlocked;
        readonly bool[,] bpBlocked;

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
            Tutorial = level.tutorial;
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
            if (level.sectors != null)
                foreach (SectorDef s in level.sectors)
                {
                    if (!InBounds(s.floor, s.col)) { Debug.LogError("[Board] 섹터 위치가 맵 밖입니다: " + s.floor + "," + s.col); continue; }
                    whole[s.floor, s.col] = s.target == SectorTarget.Whole;
                    realBlocked[s.floor, s.col] = s.realityBlocked;
                    bpBlocked[s.floor, s.col] = s.blueprintBlocked;
                }
            if (level.objects != null)
                foreach (ObjectDef d in level.objects)
                {
                    if (!InBounds(d.floor, d.col)) { Debug.LogError("[Board] 오브젝트 위치가 맵 밖입니다: " + d.floor + "," + d.col); continue; }
                    objects.Add(new SectorObject
                    {
                        Def = d,
                        AbsX = d.col * SectorW + d.x,
                        DrawAbsX = d.col * SectorW + Mathf.RoundToInt(d.drawX),
                    });
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

        /// <summary>조작 가능한 섹터 = 현실 상태와 설계도 상태가 다른 것이 있는 섹터.</summary>
        public bool IsOperable(int floor, int col)
        {
            if (whole[floor, col]) return realBlocked[floor, col] != bpBlocked[floor, col];
            foreach (SectorObject o in objects)
                if (o.Floor == floor && o.Col == col && o.Def.realityOn != o.Def.blueprintOn) return true;
            return false;
        }
    }
}
