using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    public enum Outcome { Running, GameOver, Complete }

    /// <summary>
    /// 한 번의 의뢰 진행 규칙(섹터 조준·유효 상태·대상 이동·추적도·미션). 화면·입력과 분리된 순수 로직.
    /// 규칙 근거: Docs/Planning/02-02-system-rules.md §0-2, §7 (W-004, 모두 [제안] 상태).
    /// 섹터 좌표는 Vector2Int(x = 열, y = 층)로 쓴다.
    /// </summary>
    public sealed class GameSim
    {
        public const int OtherMissionCount = 2; // 단말 A, B

        /// <summary>장면 패널 안쪽 크기(목업 기준 16,48 ~ 427,307).</summary>
        public const int ViewW = 412, ViewH = 260;

        readonly GameSettings s;
        public Board Board { get; }

        /// <summary>조준한 섹터(x = 열, y = 층).</summary>
        public Vector2Int Aim { get; private set; }
        public int TargetFloor { get; private set; }
        /// <summary>대상 중심의 맵 x(px).</summary>
        public float TargetX { get; private set; }
        public bool TargetFacingRight { get; private set; }
        /// <summary>문·차단막 앞에서 기다리는 중인가.</summary>
        public bool TargetWaiting { get; private set; }
        public float Tracking { get; private set; }
        public bool CountdownActive { get; private set; }
        public float Countdown { get; private set; }
        public bool MissionA { get; private set; }
        public bool MissionB { get; private set; }
        public bool MissionFinal { get; private set; }
        public bool RelayUsed { get; private set; }
        public Outcome Outcome { get; private set; }

        /// <summary>왼쪽 Shift를 누르는 중인지(뷰가 매 프레임 설정).</summary>
        public bool ShiftHeld { get; set; }

        /// <summary>Shift를 한 번이라도 뗐는가(튜토리얼 안내 단계 조건, W-005).</summary>
        public bool ShiftReleasedOnce { get; private set; }

        /// <summary>추적도가 최대치에 닿아 있는가.</summary>
        public bool TraceMaxed => Tracking >= s.trackingMax;

        /// <summary>카메라 왼쪽 위(맵 px, 정수). 조준한 섹터를 따라간다.</summary>
        public int CamX => Mathf.RoundToInt(camX);
        public int CamY => Mathf.RoundToInt(camY);

        bool wasShift;
        float startWait;
        float camX, camY, camFromX, camFromY, camToX, camToY, camT;

        public GameSim(GameSettings settings, Board board)
        {
            s = settings;
            Board = board;
            Reset();
        }

        /// <summary>전부 초기화(추적도·미션·대상 위치·카운트다운·카메라).</summary>
        public void Reset()
        {
            Aim = InitialAim();
            TargetFloor = Board.StartFloor;
            TargetX = Board.StartX;
            TargetFacingRight = Board.StartFacingRight;
            TargetWaiting = false;
            Tracking = 0f;
            CountdownActive = false;
            Countdown = 0f;
            MissionA = MissionB = MissionFinal = false;
            RelayUsed = false;
            Outcome = Outcome.Running;
            ShiftHeld = false;
            ShiftReleasedOnce = false;
            wasShift = false;
            startWait = s.startDelaySeconds;
            Vector2 c = CameraGoal();
            camX = camFromX = camToX = c.x;
            camY = camFromY = camToY = c.y;
            camT = 1f;
        }

        public int OtherDone => (MissionA ? 1 : 0) + (MissionB ? 1 : 0);

        /// <summary>지금 최종 미션을 끝낸다면 받을 별(화면의 예상 평가용).</summary>
        public int ProjectedStars
        {
            get
            {
                if (OtherDone >= OtherMissionCount) return 3;
                if (OtherDone * 2 >= OtherMissionCount) return 2;
                return 1;
            }
        }

        public int Stars => MissionFinal ? ProjectedStars : 0;

        public float TrackingMax => s.trackingMax;

        // ---------- 유효 상태(02-02 §0-2: Shift 중 조준한 섹터만 설계도 상태) ----------

        /// <summary>그 섹터가 지금 설계도 상태인가(Shift를 누르는 동안 조준한 섹터).</summary>
        public bool IsBlueprint(int floor, int col) => ShiftHeld && Aim.x == col && Aim.y == floor;

        /// <summary>오브젝트의 유효 상태(문·계단: 열림, 엘리베이터: 작동). 섹터 전체형 섹터의 오브젝트는 현실 상태.</summary>
        public bool ObjectOn(SectorObject o)
        {
            bool bp = false;
            if (ShiftHeld && !Board.IsWhole(o.Floor, o.Col))
            {
                bp = IsBlueprint(o.Floor, o.Col)
                    || (o.Def.shareWithLink && o.Def.hasLink && IsBlueprint(o.Def.linkFloor, o.Def.linkCol));
            }
            return bp ? o.Def.blueprintOn : o.Def.realityOn;
        }

        /// <summary>섹터 전체형 섹터의 차단막이 지금 켜져 있는가.</summary>
        public bool SectorBlocked(int floor, int col) => Board.Blocked(floor, col, IsBlueprint(floor, col));

        // ---------- 조준 ----------

        /// <summary>
        /// 조준 점프(D-038): 누른 방향(진행 축 거리 &gt; 0)의 조작 가능한 섹터 중
        /// 진행 축 거리 + 2 x 옆 축 거리가 최소인 섹터로 옮긴다. 같으면 진행 축 거리가 짧은 섹터. 후보 없으면 그대로.
        /// 거리는 섹터 중심점 사이를 섹터 칸 수(열 차이·층 차이)로 센 값이다. 같은 층 우선은 이 점수로 자연히 처리된다.
        /// Shift를 누른 동안은 무시. 옮겼으면 true.
        /// </summary>
        public bool MoveAim(int dx, int dy)
        {
            if (ShiftHeld || Outcome != Outcome.Running) return false;
            bool found = false;
            float bestScore = 0f;
            int bestAxis = 0;
            Vector2Int best = Aim;
            for (int f = 0; f < Board.Floors; f++)
            {
                for (int c = 0; c < Board.Cols; c++)
                {
                    if (!Board.IsOperable(f, c)) continue;
                    int axis = dx != 0 ? (c - Aim.x) * dx : (f - Aim.y) * dy;
                    if (axis <= 0) continue;
                    int side = dx != 0 ? Mathf.Abs(f - Aim.y) : Mathf.Abs(c - Aim.x);
                    float score = axis + s.aimSideWeight * side;
                    if (!found || score < bestScore || (Mathf.Approximately(score, bestScore) && axis < bestAxis))
                    {
                        found = true; bestScore = score; bestAxis = axis; best = new Vector2Int(c, f);
                    }
                }
            }
            if (!found) return false;
            Aim = best;
            return true;
        }

        Vector2Int InitialAim()
        {
            // 시작 섹터에서 가장 가까운(섹터 칸 수 맨해튼 거리) 조작 가능한 섹터. 없으면 시작 섹터.
            Vector2Int best = Board.StartSector;
            int bestD = int.MaxValue;
            for (int f = 0; f < Board.Floors; f++)
                for (int c = 0; c < Board.Cols; c++)
                {
                    if (!Board.IsOperable(f, c)) continue;
                    int d = Mathf.Abs(c - Board.StartSector.x) + Mathf.Abs(f - Board.StartSector.y);
                    // 거리가 같으면 시작 층(같은 층)을 우선한다 — 조준 점프의 "같은 층 우선"과 맞춤
                    bool sameFloorTie = d == bestD && f == Board.StartSector.y && best.y != Board.StartSector.y;
                    if (d < bestD || sameFloorTie) { bestD = d; best = new Vector2Int(c, f); }
                }
            return best;
        }

        // ---------- 진행 ----------

        public void Step(float dt)
        {
            if (Outcome != Outcome.Running) return;

            if (wasShift && !ShiftHeld) ShiftReleasedOnce = true;
            wasShift = ShiftHeld;

            if (ShiftHeld)
                Tracking = Mathf.Min(s.trackingMax, Tracking + s.trackingRatePerSecond * dt);

            float rest = dt;
            if (startWait > 0f)
            {
                float used = Mathf.Min(startWait, rest);
                startWait -= used;
                rest -= used;
            }
            if (rest > 0f && startWait <= 0f) MoveTarget(rest);
            if (Outcome != Outcome.Running) return;

            UpdateCamera(dt);
            UpdateCountdown(dt);
        }

        /// <summary>
        /// 대상 이동: 바닥선을 따라 좌우 연속 이동. 앞의 닫힌 문·켜진 차단막 앞에서 기다리고,
        /// 끝 벽에서 돌아서고, 지나가는 오브젝트(미션·중계기·계단·엘리베이터)에 효과를 준다.
        /// 한 번에 최대 몇 px만 움직이므로 같은 이동 구간 안의 사건은 가까운 순으로 처리한다.
        /// </summary>
        void MoveTarget(float dt)
        {
            float dist = s.targetSpeed * dt;
            TargetWaiting = false;
            for (int guard = 0; dist > 0f && guard < 8 && Outcome == Outcome.Running; guard++)
            {
                int dir = TargetFacingRight ? 1 : -1;
                float prev = TargetX;
                float stop = prev + dir * dist;
                bool wall = false, held = false;

                // 끝 벽
                float wallLimit = dir > 0 ? Board.MapW - Board.BodyHalf : Board.BodyHalf;
                if ((dir > 0 && stop >= wallLimit) || (dir < 0 && stop <= wallLimit)) { stop = wallLimit; wall = true; }

                // 앞의 닫힌 문
                foreach (SectorObject o in Board.Objects)
                {
                    if (o.Def.kind != ObjectKind.Door || o.Floor != TargetFloor || ObjectOn(o)) continue;
                    float p = o.AbsX - dir * Board.BodyHalf;
                    if (Crosses(prev, stop, p, dir, true)) { stop = p; wall = false; held = true; }
                }
                // 들어갈 섹터의 차단막
                for (int c = 0; c < Board.Cols; c++)
                {
                    if (!Board.IsWhole(TargetFloor, c) || !SectorBlocked(TargetFloor, c)) continue;
                    float edge = dir > 0 ? Board.SectorLeft(c) : Board.SectorLeft(c + 1);
                    float p = edge - dir * Board.BodyHalf;
                    if (Crosses(prev, stop, p, dir, true)) { stop = p; wall = false; held = true; }
                }

                // 지나가는 오브젝트(이동 구간 안, 가까운 순)
                SectorObject hit = null;
                float hitDist = float.MaxValue;
                foreach (SectorObject o in Board.Objects)
                {
                    if (o.Floor != TargetFloor || o.Def.kind == ObjectKind.Door) continue;
                    if (!Crosses(prev, stop, o.AbsX, dir, false)) continue;
                    float d = Mathf.Abs(o.AbsX - prev);
                    if (d < hitDist) { hitDist = d; hit = o; }
                }
                if (hit != null)
                {
                    TargetX = hit.AbsX;
                    if (Activate(hit)) return;   // 이동했거나 의뢰가 끝남
                    dist -= hitDist;
                    continue;                      // 이 오브젝트는 지나쳤으니 남은 거리를 계속 간다
                }

                dist -= Mathf.Abs(stop - prev);
                TargetX = stop;
                if (held) { TargetWaiting = true; return; }
                if (wall) TargetFacingRight = !TargetFacingRight;   // 끝 벽에 닿으면 돌아섬
                else return;
            }
        }

        /// <summary>
        /// 이동 구간(prev→stop)이 위치 p를 "지나가는가". 출발점이 p의 반대쪽에 있어야 한다(출발점이 p 위면 지나가지 않음).
        /// blockLine이 true이면 막는 선(출발점이 p 위여도 stop이 p면 멈춘 상태로 본다)이라 등호를 허용한다.
        /// </summary>
        static bool Crosses(float prev, float stop, float p, int dir, bool blockLine)
        {
            if (dir > 0) return blockLine ? (prev <= p && stop > p) : (prev < p && stop >= p);
            return blockLine ? (prev >= p && stop < p) : (prev > p && stop <= p);
        }

        /// <summary>오브젝트 효과. 대상 위치가 바뀌었거나 의뢰가 끝났으면 true.</summary>
        bool Activate(SectorObject o)
        {
            switch (o.Def.kind)
            {
                case ObjectKind.Terminal:
                    if (o.Def.mission == 1 && !MissionA) { MissionA = true; Relieve(s.missionRelief); }
                    else if (o.Def.mission == 2 && !MissionB) { MissionB = true; Relieve(s.missionRelief); }
                    return false;
                case ObjectKind.Relay:
                    if (!RelayUsed) { RelayUsed = true; Relieve(s.relayRelief); }
                    return false;
                case ObjectKind.Exit:
                    MissionFinal = true;
                    Outcome = Outcome.Complete;   // 최종 미션 완료 즉시 의뢰 종료
                    return true;
                case ObjectKind.Stairs:
                case ObjectKind.Elevator:
                    if (!o.Def.hasLink || !ObjectOn(o)) return false;   // 닫힘·멈춤이면 그냥 지나감
                    TargetFloor = o.Def.linkFloor;
                    TargetX = o.Def.linkCol * Board.SectorW + o.Def.linkX;
                    TargetFacingRight = o.Def.linkFacingRight;
                    return true;
            }
            return false;
        }

        void Relieve(float amount) => Tracking = Mathf.Max(0f, Tracking - amount);

        void UpdateCountdown(float dt)
        {
            if (Board.Tutorial) return;   // 튜토리얼(챕터 0)은 카운트다운·게임오버가 없다. 추적도는 최대치에 머문다(W-005)
            if (Tracking >= s.trackingMax)
            {
                if (!CountdownActive)
                {
                    CountdownActive = true;
                    Countdown = s.countdownSeconds;
                    return;
                }
            }
            else if (CountdownActive)
            {
                CountdownActive = false; // 100 미만이 되면 해제
                return;
            }

            if (CountdownActive)
            {
                Countdown -= dt;
                if (Countdown <= 0f)
                {
                    Countdown = 0f;
                    Outcome = Outcome.GameOver;
                }
            }
        }

        // ---------- 카메라(D-063) ----------

        /// <summary>조준한 섹터 중심이 화면 가운데에 오는 카메라 위치(맵 밖으로 나가지 않게).</summary>
        Vector2 CameraGoal()
        {
            Vector2 c = Board.SectorCenter(Aim.y, Aim.x);
            // 맵이 뷰보다 작은 방향은 카메라를 고정하고 맵을 뷰 가운데에 둔다(W-006): 카메라 값이 음수가 된다.
            float x = Board.MapW < ViewW ? (Board.MapW - ViewW) * 0.5f : Mathf.Clamp(c.x - ViewW * 0.5f, 0f, Board.MapW - ViewW);
            float y = Board.MapH < ViewH ? (Board.MapH - ViewH) * 0.5f : Mathf.Clamp(c.y - ViewH * 0.5f, 0f, Board.MapH - ViewH);
            return new Vector2(Mathf.Round(x), Mathf.Round(y));
        }

        void UpdateCamera(float dt)
        {
            Vector2 g = CameraGoal();
            if (!Mathf.Approximately(g.x, camToX) || !Mathf.Approximately(g.y, camToY))
            {
                camFromX = camX; camFromY = camY;
                camToX = g.x; camToY = g.y;
                camT = 0f;
            }
            if (camT >= 1f) { camX = camToX; camY = camToY; return; }
            camT += s.cameraSeconds <= 0f ? 1f : dt / s.cameraSeconds;
            float t = Mathf.Clamp01(camT);
            t = t * t * (3f - 2f * t);
            camX = Mathf.Lerp(camFromX, camToX, t);
            camY = Mathf.Lerp(camFromY, camToY, t);
        }
    }
}
