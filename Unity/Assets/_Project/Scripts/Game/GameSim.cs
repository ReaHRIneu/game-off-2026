using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>Failed(W-007): 실패 지점에 닿음. 결과 창 없이 그 의뢰를 바로 처음부터 다시 시작한다(GameView가 처리).</summary>
    public enum Outcome { Running, GameOver, Complete, Failed }

    /// <summary>
    /// 조력자가 현실과 다른 유효 상태의 장치를 지나가거나 이용한 사건 종류(W-030 F3, D-118). 판정에는 쓰이지 않고 화면이 읽는 기록이다.
    /// </summary>
    public enum UseKind { DoorPassed, ShutterPassed, HatchPassed, StairsSkipped, LaserPassed, CameraEvaded, ElevatorUsed, ElevatorSkipped, RoomBridged, Carried }

    /// <summary>사건 하나: 종류, 일어난 칸(층, 열), 맵 x(px). 링을 어디에 그릴지 정하는 값이다.</summary>
    public struct UseEvent
    {
        public UseKind Kind;
        public int Floor, Col;
        public float X;
    }

    /// <summary>
    /// 한 번의 의뢰 진행 규칙(섹터 조준·유효 상태·대상 이동·추적도·미션). 화면·입력과 분리된 순수 로직.
    /// 규칙 근거: Docs/Planning/02-02-system-rules.md §0-2, §7 (W-004, 모두 [제안] 상태).
    /// 섹터 좌표는 Vector2Int(x = 열, y = 층)로 쓴다.
    /// </summary>
    public sealed class GameSim
    {
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
        /// <summary>이번 Step에서 실제로 이동했는가(시작 대기·정지 중이면 false). 그림이 걷기/기다림을 고르는 데만 쓴다(W-009).</summary>
        public bool TargetMoving { get; private set; }
        /// <summary>해치로 떨어진 횟수(의뢰를 다시 시작하면 0). 그림이 떨어짐 애니메이션을 한 번 재생하는 데만 쓴다(W-009).</summary>
        public int FallCount { get; private set; }
        public float Tracking { get; private set; }
        public bool CountdownActive { get; private set; }
        public float Countdown { get; private set; }
        /// <summary>미션 목록 순서(0 = 최종). 완료·실패 확정 상태는 MissionDone·MissionFailed.</summary>
        public MissionDef[] Missions => Board.Missions;
        public int MissionCount => Board.Missions.Length;
        public bool MissionDone(int i) => missionDone[i];
        public bool MissionFailed(int i) => missionFailed[i];
        // 최종 미션(0)과 단말 A·B(1·2)의 W-005 이름. 미션이 그만큼 없으면 false.
        public bool MissionA => missionDone.Length > 1 && missionDone[1];
        public bool MissionB => missionDone.Length > 2 && missionDone[2];
        public bool MissionFinal => missionDone[0];
        public bool RelayUsed { get; private set; }
        public Outcome Outcome { get; private set; }

        /// <summary>
        /// 왼쪽 Shift를 누르는 중인지(뷰가 매 프레임 설정). W-014: 밀린 방이 있을 때 false로 바꾸면 그 방(맞바꾼 방 포함)이 제자리로 돌아온다(R4).
        /// 의뢰가 끝난 뒤(Complete·GameOver)에는 돌아오지 않고 그 모습을 유지한다.
        /// </summary>
        public bool ShiftHeld
        {
            get => shiftHeld;
            set
            {
                if (!value && shiftHeld) ReleasePush();
                shiftHeld = value;
            }
        }
        bool shiftHeld;

        /// <summary>Shift를 한 번이라도 뗐는가(튜토리얼 안내 단계 조건, W-005).</summary>
        public bool ShiftReleasedOnce { get; private set; }

        /// <summary>추적도가 최대치에 닿아 있는가.</summary>
        public bool TraceMaxed => Tracking >= s.trackingMax;

        /// <summary>카메라 왼쪽 위(맵 px, 정수). 조준한 섹터를 따라간다.</summary>
        public int CamX => Mathf.RoundToInt(camX);
        public int CamY => Mathf.RoundToInt(camY);

        // ---------- 방 밀기(W-014, 03-01 §2-0, R1~R11) ----------
        // 방은 "원래 자리(정체)"로 부르고, 지금 있는 칸과 따로 센다. 칸·정체 번호는 층 * 열 수 + 열.
        // roomAt[칸] = 그 칸에 지금 있는 방의 정체(없으면 -1), cellOf[정체] = 그 방이 지금 있는 칸. 미리 만든 배열이라 프레임마다 할당이 없다.
        int[] roomAt, cellOf;
        int pushedIdx = -1, swappedIdx = -1;

        /// <summary>밀기 성공·되돌아옴·막힘·처음부터 다시 횟수(뷰가 그림·소리·글리치를 고르는 데 쓴다). Reset하면 ResetSeq만 늘고 나머지는 0.</summary>
        public int PushSeq { get; private set; }
        public int ReturnSeq { get; private set; }
        public int BlockSeq { get; private set; }
        public int ResetSeq { get; private set; }
        /// <summary>마지막 밀기·되돌아옴에서 조준 방이 있던 칸(열, 층)과 간 칸. 글리치 영역용.</summary>
        public Vector2Int LastMoveFrom { get; private set; }
        public Vector2Int LastMoveTo { get; private set; }
        /// <summary>마지막 막힘에서 흔들릴 방의 현재 칸(열, 층).</summary>
        public Vector2Int LastBlockCell { get; private set; }
        /// <summary>대상이 방과 함께 옮겨진(또는 돌아온) 횟수와 마지막 이동량(px). 그림이 대상을 방과 함께 미끄러뜨리는 데 쓴다.</summary>
        public int TargetShiftSeq { get; private set; }
        public int TargetShiftDX { get; private set; }
        public int TargetShiftDY { get; private set; }
        /// <summary>지금 밀려 있는 방이 있는가.</summary>
        public bool Pushed => pushedIdx >= 0;

        // ---------- 감시 카메라·조력자 위험 반응·발각(W-016, 02-06 §2-2·§4·§5) ----------
        /// <summary>조력자가 위험(켜진 감시 구역 경계)을 보고 돌아선 횟수와 지금 머리 위 ! 가 보여야 하는 남은 시간(초). 그림이 ! 를 그리는 데 쓴다.</summary>
        public int AlertSeq { get; private set; }
        public float AlertMarkLeft { get; private set; }
        /// <summary>발각된 횟수(처음부터 다시 하면 0)와 마지막 발각에서 감시 구역이 있던 칸(x = 열, y = 층). 뷰가 글리치·로그·소리를 고르는 데 쓴다.</summary>
        public int DetectCount { get; private set; }
        public Vector2Int LastDetectCell { get; private set; }
        /// <summary>이 의뢰의 감시 카메라 수와 i번째 카메라 오브젝트.</summary>
        public int CameraCount => cameras.Length;
        public SectorObject CameraAt(int i) => cameras[i];
        /// <summary>그 카메라가 재발각 대기 중이면 남은 시간(초), 아니면 0.</summary>
        public float CameraCooldown(int i) => camCooldown[i];

        readonly SectorObject[] cameras;
        readonly float[] camCooldown;
        readonly bool[] camInZone;   // D-101: 지난 프레임에 (켜짐 && 조력자가 구역 안)이었는가 — false->true로 바뀌는 순간만 발각
        const float DefaultRedetectSeconds = 3f;

        // ---------- 장치 이용 기록(W-030 F3, D-118): 읽기 전용. 판정·순서에는 영향이 없다 ----------
        /// <summary>조력자가 현실과 다른 유효 상태의 장치를 지나가거나 이용한 횟수(Reset하면 0)와 마지막 사건.</summary>
        public int UseSeq { get; private set; }
        public UseEvent LastUse { get; private set; }
        /// <summary>최근 사건 기록 칸 수. UseAt(seq)는 UseSeq - UseHistory &lt; seq &lt;= UseSeq 일 때만 믿을 수 있다(seq는 1부터).</summary>
        public const int UseHistory = 16;
        readonly UseEvent[] useRing = new UseEvent[UseHistory];
        public UseEvent UseAt(int seq) => useRing[seq % UseHistory];
        /// <summary>지금 밀려 있는 방과 그 방과 맞바뀐 방의 정체 번호(층 * 열 수 + 열, 없으면 -1). 화면이 MOVED 표지를 붙이는 데 쓴다.</summary>
        public int PushedRoom => pushedIdx;
        public int SwappedRoom => swappedIdx;

        int trkCell = -1, trkEnterDir, trkShiftSeq, trkPushSig;
        bool trkEnteredOff;   // 지금 있는 칸의 방이 현실은 켜진 섹터 전체형 차단막인데 꺼진 상태로 들어왔는가
        int[] camEnterDir;
        bool[] camPosIn;

        void NoteUse(UseKind kind, int floor, int col, float x)
        {
            UseSeq++;
            var e = new UseEvent { Kind = kind, Floor = floor, Col = col, X = x };
            LastUse = e;
            useRing[UseSeq % UseHistory] = e;
        }

        bool wasShift;
        bool[] missionDone, missionFailed;
        float startWait;
        float camX, camY, camFromX, camFromY, camToX, camToY, camT;

        public GameSim(GameSettings settings, Board board)
        {
            s = settings;
            Board = board;
            int camN = 0;
            foreach (SectorObject o in board.Objects) if (o.Def.kind == ObjectKind.Camera) camN++;
            cameras = new SectorObject[camN];
            camN = 0;
            foreach (SectorObject o in board.Objects) if (o.Def.kind == ObjectKind.Camera) cameras[camN++] = o;
            camCooldown = new float[cameras.Length];
            camInZone = new bool[cameras.Length];
            Reset();
        }

        /// <summary>전부 초기화(추적도·미션·대상 위치·카운트다운·카메라).</summary>
        public void Reset()
        {
            int n = Board.Floors * Board.Cols;
            if (roomAt == null) { roomAt = new int[n]; cellOf = new int[n]; }
            for (int i = 0; i < n; i++)
            {
                cellOf[i] = i;
                roomAt[i] = Board.IsEmpty(i / Board.Cols, i % Board.Cols) ? -1 : i;
            }
            pushedIdx = swappedIdx = -1;
            PushSeq = ReturnSeq = BlockSeq = TargetShiftSeq = 0;
            AlertSeq = DetectCount = 0;
            UseSeq = 0;
            LastUse = default;
            trkCell = -1;
            AlertMarkLeft = 0f;
            LastDetectCell = default;
            for (int i = 0; i < camCooldown.Length; i++) camCooldown[i] = 0f;
            TargetShiftDX = TargetShiftDY = 0;
            ResetSeq++;
            Aim = InitialAim();
            TargetFloor = Board.StartFloor;
            TargetX = Board.StartX;
            TargetFacingRight = Board.StartFacingRight;
            TargetWaiting = false;
            TargetMoving = false;
            FallCount = 0;
            Tracking = 0f;
            CountdownActive = false;
            Countdown = 0f;
            missionDone = new bool[Board.Missions.Length];
            missionFailed = new bool[Board.Missions.Length];
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
            // D-101 [제안]: 시작 때 이미 켜진 구역 안이면 발각하지 않도록 기억 값을 현재 상태로 맞춘다
            for (int i = 0; i < camInZone.Length; i++) camInZone[i] = HelperInLitZone(i);
        }

        /// <summary>최종 미션을 뺀 나머지 미션 수.</summary>
        public int OtherCount => Board.Missions.Length - 1;

        /// <summary>지금 의뢰를 끝낸다면 완료로 칠 나머지 미션 수. 솜씨 미션(층 금지·추적도 제한)은 실패가 확정되지 않았으면 끝날 때 완료가 되므로 센다.</summary>
        public int OtherDone
        {
            get
            {
                int n = 0;
                for (int i = 1; i < Board.Missions.Length; i++)
                    if (missionDone[i] || (!missionFailed[i] && IsSkill(i))) n++;
                return n;
            }
        }

        bool IsSkill(int i) => Board.Missions[i].kind == MissionKind.NeverEnterFloor || Board.Missions[i].kind == MissionKind.TraceLimit || Board.Missions[i].kind == MissionKind.Unseen;

        /// <summary>지금 최종 미션을 끝낸다면 받을 별(화면의 예상 평가용). D-030: 완료한 나머지 미션 수로 계산.</summary>
        public int ProjectedStars
        {
            get
            {
                int others = OtherCount;
                if (OtherDone >= others) return 3;
                if (OtherDone * 2 >= others) return 2;
                return 1;
            }
        }

        public int Stars => MissionFinal ? ProjectedStars : 0;

        public float TrackingMax => s.trackingMax;

        // ---------- 유효 상태(02-02 §0-2: Shift 중 조준한 섹터만 설계도 상태) ----------

        /// <summary>그 섹터가 지금 설계도 상태인가(Shift를 누르는 동안 조준한 섹터).</summary>
        /// <remarks>W-014: 방향키로 밀린 방은 현실 상태로 돌아와 있으므로(R6) 밀리는 동안은 설계도 상태가 아니다.</remarks>
        public bool IsBlueprint(int floor, int col) => ShiftHeld && Aim.x == col && Aim.y == floor && pushedIdx < 0;

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
            if (Board.HasStartAim) return Board.StartAim;   // 데이터로 정한 처음 조준(W-007)
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
            if (Outcome != Outcome.Running) { TargetMoving = false; return; }

            if (wasShift && !ShiftHeld) ShiftReleasedOnce = true;
            wasShift = ShiftHeld;

            if (ShiftHeld)
                Tracking = Mathf.Min(s.trackingMax, Tracking + s.trackingRatePerSecond * dt);
            CheckTraceLimits();

            float rest = dt;
            if (startWait > 0f)
            {
                float used = Mathf.Min(startWait, rest);
                startWait -= used;
                rest -= used;
            }
            TargetMoving = false;
            float useX0 = TargetX;
            int useFloor0 = TargetFloor;
            // W-016 판정 순서(02-06 §1): 실려 감(방 밀기: TryPush·ReleasePush) -> 위험 반응 -> 기다림/돌아섬/층 이동 -> 발각
            TickSurveillance(dt);
            ReactToDanger();
            if (rest > 0f && startWait <= 0f) { MoveTarget(rest); TargetMoving = Outcome == Outcome.Running && !TargetWaiting; }
            TrackUses(useX0, useFloor0);   // W-030: 읽기만 한다
            if (Outcome == Outcome.Running) CheckDetection();
            if (Outcome == Outcome.Running) CheckPosition();
            if (Outcome != Outcome.Running) return;

            UpdateCamera(dt);
            UpdateCountdown(dt);
        }

        /// <summary>
        /// 대상 이동: 바닥선을 따라 좌우 연속 이동. 앞의 닫힌 문·켜진 차단막 앞에서 기다리고,
        /// 끝 벽에서 돌아서고, 지나가는 오브젝트(미션·중계기·계단·엘리베이터)에 효과를 준다.
        /// 한 번에 최대 몇 px만 움직이므로 같은 이동 구간 안의 사건은 가까운 순으로 처리한다.
        /// W-014: 문·오브젝트·차단막은 방의 "지금 위치"(칸)를 쓰고, 끝 벽은 방이 없는 칸(빈 칸·맵 밖)의 경계다.
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

                // 끝 벽: 지금 칸에서 앞으로 방이 이어지는 만큼 간 뒤 첫 빈 칸·맵 밖의 경계(W-014 R1)
                float wallLimit = WallLimit(prev, dir);
                if ((dir > 0 && stop >= wallLimit) || (dir < 0 && stop <= wallLimit)) { stop = wallLimit; wall = true; }

                // 앞의 닫힌 문
                foreach (SectorObject o in Board.Objects)
                {
                    if (o.Def.kind != ObjectKind.Door || EffFloor(o) != TargetFloor || ObjectOn(o)) continue;
                    float p = EffX(o) - dir * Board.BodyHalf;
                    if (Crosses(prev, stop, p, dir, true)) { stop = p; wall = false; held = true; }
                }
                // 들어갈 칸의 차단막(방 정체 기준 상태, 칸 기준 위치)
                for (int c = 0; c < Board.Cols; c++)
                {
                    int id = roomAt[TargetFloor * Board.Cols + c];
                    if (id < 0) continue;
                    int idF = id / Board.Cols, idC = id % Board.Cols;
                    if (!Board.IsWhole(idF, idC) || !SectorBlocked(idF, idC)) continue;
                    float edge = dir > 0 ? Board.SectorLeft(c) : Board.SectorLeft(c + 1);
                    float p = edge - dir * Board.BodyHalf;
                    if (Crosses(prev, stop, p, dir, true)) { stop = p; wall = false; held = true; }
                }

                // 지나가는 오브젝트(이동 구간 안, 가까운 순)
                SectorObject hit = null;
                float hitDist = float.MaxValue, hitX = 0f;
                foreach (SectorObject o in Board.Objects)
                {
                    if (EffFloor(o) != TargetFloor || o.Def.kind == ObjectKind.Door || o.Def.kind == ObjectKind.Camera) continue;   // 카메라는 지나치는 오브젝트가 아니다(구역은 ReactToDanger·CheckDetection이 판정)
                    float ox = EffX(o);
                    if (!Crosses(prev, stop, ox, dir, false)) continue;
                    float d = Mathf.Abs(ox - prev);
                    if (d < hitDist) { hitDist = d; hit = o; hitX = ox; }
                }
                if (hit != null)
                {
                    TargetX = hitX;
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

        /// <summary>대상이 있는 층에서 dir 방향으로 이어지는 방의 끝 벽 x(몸통 반폭만큼 안쪽). 빈 칸·맵 밖 앞에서 멈춘다.</summary>
        float WallLimit(float x, int dir)
        {
            int c = Mathf.Clamp(Mathf.FloorToInt(x / Board.SectorW), 0, Board.Cols - 1);
            if (dir > 0)
            {
                while (c + 1 < Board.Cols && roomAt[TargetFloor * Board.Cols + c + 1] >= 0) c++;
                return (c + 1) * Board.SectorW - Board.BodyHalf;
            }
            while (c - 1 >= 0 && roomAt[TargetFloor * Board.Cols + c - 1] >= 0) c--;
            return c * Board.SectorW + Board.BodyHalf;
        }

        // ---------- 감시 카메라(W-016) ----------

        /// <summary>
        /// 카메라의 감시 구역(맵 px): 카메라가 든 방이 지금 있는 칸에서 오른쪽으로 coverCols칸. 방이 밀리면 구역도 같이 옮겨진다(D-097).
        /// 반환값은 구역이 있는 층. left·right는 맵 x(px).
        /// </summary>
        public int CameraZone(SectorObject cam, out float left, out float right)
        {
            int ci = cellOf[cam.Floor * Board.Cols + cam.Col];
            int cf = ci / Board.Cols, cc = ci % Board.Cols;
            int cols = cam.Def.surveillance != null ? Mathf.Max(1, cam.Def.surveillance.coverCols) : 1;
            left = cc * Board.SectorW;
            right = Mathf.Min(cc + cols, Board.Cols) * Board.SectorW;
            return cf;
        }

        /// <summary>감시가 켜져 있는가. 지금 유효 상태(현실 켜짐·설계도 꺼짐은 종류 정의에서 온다)를 그대로 쓴다.</summary>
        public bool CameraOn(SectorObject cam) => ObjectOn(cam);

        /// <summary>값을 읽는 순서: ③ 의뢰 배치 덮어쓰기 -> ② 종류 정의 -> ① 공통 값(02-06 §7-1). 프레임마다 새 객체를 만들지 않는다.</summary>
        public float DetectTrackingOf(SectorObject cam)
        {
            if (cam.Def.detectTrackingOverride >= 0f) return cam.Def.detectTrackingOverride;
            SurveillanceType t = cam.Def.surveillance;
            if (t != null && t.detectTrackingOverride >= 0f) return t.detectTrackingOverride;
            return s.detectTracking;
        }

        float RedetectSecondsOf(SectorObject cam) => cam.Def.surveillance != null ? cam.Def.surveillance.redetectSeconds : DefaultRedetectSeconds;

        void TickSurveillance(float dt)
        {
            if (AlertMarkLeft > 0f) AlertMarkLeft = Mathf.Max(0f, AlertMarkLeft - dt);
            for (int i = 0; i < camCooldown.Length; i++)
                if (camCooldown[i] > 0f) camCooldown[i] = Mathf.Max(0f, camCooldown[i] - dt);
        }

        /// <summary>
        /// 조력자 위험 반응(D-096): 같은 층, 바라보는 방향 앞쪽 helperAlertDistance 안에 켜진 감시 구역의 경계가 있으면 그 자리에서 돌아서고 ! 를 띄운다.
        /// 이미 구역 안에 있으면 위험 반응이 아니라 발각 판정이다. 닫힌 문·켜진 차단막·빈 칸·끝 벽 너머는 보지 못한다.
        /// </summary>
        void ReactToDanger()
        {
            if (cameras.Length == 0 || Outcome != Outcome.Running) return;
            int dir = TargetFacingRight ? 1 : -1;
            for (int i = 0; i < cameras.Length; i++)
            {
                SectorObject cam = cameras[i];
                if (!ObjectOn(cam)) continue;
                int zf = CameraZone(cam, out float left, out float right);
                if (zf != TargetFloor) continue;
                float edge;
                if (dir > 0) { if (TargetX >= left) continue; edge = left; }
                else { if (TargetX <= right) continue; edge = right; }
                if ((edge - TargetX) * dir > s.helperAlertDistance) continue;
                if (SightBlocked(TargetX, edge, dir)) continue;
                TargetFacingRight = !TargetFacingRight;
                TargetWaiting = false;
                AlertSeq++;
                AlertMarkLeft = s.alertMarkSeconds;
                return;
            }
        }

        /// <summary>대상(x0)에서 앞쪽 edge까지 시야를 막는 것이 있는가: 사이의 닫힌 문, 켜진 차단막, 빈 칸·끝 벽(02-06 §2-2). 대상이 서 있는 칸의 차단막은 세지 않는다.</summary>
        bool SightBlocked(float x0, float edge, int dir)
        {
            foreach (SectorObject o in Board.Objects)
            {
                if (o.Def.kind != ObjectKind.Door || EffFloor(o) != TargetFloor || ObjectOn(o)) continue;
                float dx = EffX(o);
                if (dir > 0 ? (dx > x0 && dx < edge) : (dx < x0 && dx > edge)) return true;
            }
            // 끝 벽: 지금 칸에서 앞으로 방이 이어지는 끝(빈 칸·맵 밖 앞)이 경계보다 가깝다
            float wallLimit = WallLimit(x0, dir);
            if (dir > 0 ? wallLimit < edge : wallLimit > edge) return true;
            // 사이 칸들의 차단막(켜진 것): 대상 칸 다음 칸부터 경계가 있는 칸까지
            int c0 = Mathf.Clamp(Mathf.FloorToInt(x0 / Board.SectorW), 0, Board.Cols - 1);
            int c1 = Mathf.Clamp(Mathf.FloorToInt((dir > 0 ? edge : edge - 1f) / Board.SectorW), 0, Board.Cols - 1);
            for (int c = c0 + dir; dir > 0 ? c <= c1 : c >= c1; c += dir)
            {
                int id = roomAt[TargetFloor * Board.Cols + c];
                if (id < 0) return true;
                if (Board.IsWhole(id / Board.Cols, id % Board.Cols) && SectorBlocked(id / Board.Cols, id % Board.Cols)) return true;
            }
            return false;
        }

        /// <summary>그 카메라가 켜져 있고 조력자가 그 구역 안에 있는가.</summary>
        bool HelperInLitZone(int i)
        {
            SectorObject cam = cameras[i];
            if (!ObjectOn(cam)) return false;
            int zf = CameraZone(cam, out float left, out float right);
            return zf == TargetFloor && TargetX >= left && TargetX < right;
        }

        /// <summary>
        /// 발각 판정(D-094, D-101): 카메라마다 「켜짐 && 조력자가 구역 안」이 지난 프레임 false -> 이번 프레임 true로 바뀌는 순간에만 발각
        /// (Shift를 뗌·걸어 들어감·방 밀기로 실려 감·구역이 밀려 와 덮음). 구역 안에 머무는 동안은 다시 발각하지 않는다.
        /// 재발각 대기 중이면 발각하지 않는다(Shift 연타 방지). 추적도가 오르고(최대치면 기존 카운트다운), UNSEEN 미션이 실패로 확정되고,
        /// 조력자는 돌아서며 ! 를 띄운다. 그 카메라는 재발각 대기에 들어간다. 한 프레임에 한 번만(기억 값은 모든 카메라에 대해 갱신).
        /// </summary>
        void CheckDetection()
        {
            bool detected = false;
            for (int i = 0; i < cameras.Length; i++)
            {
                bool now = HelperInLitZone(i);
                bool entered = now && !camInZone[i];
                camInZone[i] = now;
                if (!entered || detected || camCooldown[i] > 0f) continue;
                SectorObject cam = cameras[i];
                int zf = CameraZone(cam, out float left, out _);
                Tracking = Mathf.Min(s.trackingMax, Tracking + DetectTrackingOf(cam));
                DetectCount++;
                LastDetectCell = new Vector2Int(Mathf.RoundToInt(left) / Board.SectorW, zf);
                for (int k = 0; k < Board.Missions.Length; k++)
                    if (Board.Missions[k].kind == MissionKind.Unseen && !missionDone[k]) missionFailed[k] = true;
                TargetFacingRight = !TargetFacingRight;
                TargetWaiting = false;
                AlertSeq++;
                AlertMarkLeft = s.alertMarkSeconds;
                camCooldown[i] = RedetectSecondsOf(cam);
                detected = true;
            }
            if (detected) CheckTraceLimits();
        }

        // ---------- 방의 지금 위치(W-014) ----------

        /// <summary>오브젝트가 든 방이 지금 있는 층.</summary>
        int EffFloor(SectorObject o) => cellOf[o.Floor * Board.Cols + o.Col] / Board.Cols;

        /// <summary>오브젝트의 맵 x(px). 방이 옮겨졌으면 그 열 차이만큼 같이 옮겨진 값.</summary>
        float EffX(SectorObject o) => o.AbsX + (cellOf[o.Floor * Board.Cols + o.Col] % Board.Cols - o.Col) * Board.SectorW;

        /// <summary>오브젝트가 든 방이 지금 있는 열.</summary>
        int EffCol(SectorObject o) => cellOf[o.Floor * Board.Cols + o.Col] % Board.Cols;

        // ---------- 장치 이용 기록(W-030 F3): 이번 Step의 이동 결과를 읽어 사건을 남긴다. 상태는 바꾸지 않는다 ----------

        int TargetCell() => TargetFloor * Board.Cols + Mathf.Clamp(Mathf.FloorToInt(TargetX / Board.SectorW), 0, Board.Cols - 1);

        bool CameraZoneHas(int i)
        {
            int zf = CameraZone(cameras[i], out float left, out float right);
            return zf == TargetFloor && TargetX >= left && TargetX < right;
        }

        void TrackUses(float preX, int preFloor)
        {
            if (Outcome != Outcome.Running) return;
            int cols = Board.Cols;
            int cell = TargetCell();
            int pushSig = PushSeq + ReturnSeq;
            if (camEnterDir == null || camEnterDir.Length != cameras.Length) { camEnterDir = new int[cameras.Length]; camPosIn = new bool[cameras.Length]; trkCell = -1; }
            // 처음이거나, 방이 밀려(실려 감·구역 이동) 위치 관계가 한꺼번에 바뀐 직후: 기준만 새로 잡고 사건은 남기지 않는다
            if (trkCell < 0 || trkShiftSeq != TargetShiftSeq || trkPushSig != pushSig)
            {
                trkCell = cell; trkEnterDir = 0; trkEnteredOff = false; trkShiftSeq = TargetShiftSeq; trkPushSig = pushSig;
                for (int i = 0; i < cameras.Length; i++) { camPosIn[i] = CameraZoneHas(i); camEnterDir[i] = 0; }
                return;
            }
            bool sameFloor = TargetFloor == preFloor;
            int dir = sameFloor && !Mathf.Approximately(TargetX, preX) ? (TargetX > preX ? 1 : -1) : 0;

            // 현실은 닫힘인 문을 열린 유효 상태로 통과
            if (dir != 0)
            {
                foreach (SectorObject o in Board.Objects)
                {
                    if (o.Def.kind != ObjectKind.Door || o.Def.realityOn || !ObjectOn(o) || EffFloor(o) != TargetFloor) continue;
                    float ox = EffX(o);
                    if (Crosses(preX, TargetX, ox, dir, false)) NoteUse(UseKind.DoorPassed, TargetFloor, EffCol(o), ox);
                }
            }

            // 칸이 바뀐 순간: 꺼진 레이저 통과(칸을 벗어나는 순간)·방 이음
            if (cell != trkCell)
            {
                int oc = trkCell % cols, nc = cell % cols;
                if (dir != 0 && sameFloor && trkCell / cols == cell / cols && Mathf.Abs(nc - oc) == 1)
                {
                    int d = nc - oc;
                    int oldId = roomAt[trkCell], newId = roomAt[cell];
                    if (oldId >= 0)
                    {
                        int idF = oldId / cols, idC = oldId % cols;
                        // 꺼진 상태로 들어와 같은 방향으로 벗어남(나중에 Shift를 떼어 다시 켜져도 이미 지나간 것)
                        if (trkEnteredOff && trkEnterDir == d && Board.IsWhole(idF, idC) && Board.Blocked(idF, idC, false))
                            NoteUse(UseKind.LaserPassed, TargetFloor, oc, oc * Board.SectorW + Board.SectorW * 0.5f);
                    }
                    if (oldId >= 0 && newId >= 0 && !(oldId / cols == newId / cols && newId % cols - oldId % cols == d))
                        NoteUse(UseKind.RoomBridged, TargetFloor, d > 0 ? nc : oc, Mathf.Max(oc, nc) * Board.SectorW);
                    trkEnterDir = d;
                }
                else trkEnterDir = 0;
                trkEnteredOff = false;
                int enterId = roomAt[cell];
                if (trkEnterDir != 0 && enterId >= 0 && Board.IsWhole(enterId / cols, enterId % cols) && Board.Blocked(enterId / cols, enterId % cols, false) && !SectorBlocked(enterId / cols, enterId % cols))
                    trkEnteredOff = true;
                trkCell = cell;
            }

            // 현실은 켜짐인 카메라 구역을 꺼진 상태에서 지나감(구역을 벗어나는 순간, 들어온 방향 그대로)
            for (int i = 0; i < cameras.Length; i++)
            {
                bool now = CameraZoneHas(i), was = camPosIn[i];
                if (now && !was) camEnterDir[i] = dir;
                else if (!now && was)
                {
                    SectorObject cam = cameras[i];
                    bool leftByWalking = dir != 0 && camEnterDir[i] == dir;          // 들어온 방향 그대로 걸어 나감
                    bool leftByStairs = !sameFloor && camEnterDir[i] != 0;            // 구역 안에서 계단·해치로 층을 옮겨 벗어남
                    if (cam.Def.realityOn && !CameraOn(cam) && (leftByWalking || leftByStairs))
                        NoteUse(UseKind.CameraEvaded, cellOf[cam.Floor * cols + cam.Col] / cols, EffCol(cam), EffX(cam));
                    camEnterDir[i] = 0;
                }
                camPosIn[i] = now;
            }
        }

        /// <summary>그 방(원래 자리)이 지금 있는 칸(x = 열, y = 층). 제자리면 같은 값.</summary>
        public Vector2Int CellOf(int floor, int col)
        {
            int ci = cellOf[floor * Board.Cols + col];
            return new Vector2Int(ci % Board.Cols, ci / Board.Cols);
        }

        /// <summary>그 방(정체 번호 = 층 * 열 수 + 열)이 지금 있는 칸 번호.</summary>
        public int CellIndexOf(int roomIndex) => cellOf[roomIndex];

        /// <summary>그 칸에 지금 있는 방의 정체 번호(없으면 -1).</summary>
        public int RoomIndexAt(int floor, int col) => roomAt[floor * Board.Cols + col];

        /// <summary>그 방이 제자리를 벗어나 있는가.</summary>
        public bool IsDisplaced(int floor, int col) => cellOf[floor * Board.Cols + col] != floor * Board.Cols + col;

        /// <summary>조준한 방이 지금 있는 칸(x = 열, y = 층). 카메라·조준 커서·설계도 겹침이 쓴다.</summary>
        public Vector2Int AimCell => CellOf(Aim.y, Aim.x);

        // ---------- 방 밀기 동작 ----------

        /// <summary>
        /// Shift를 누른 채 방향키(R3): 조준한 방을 그 방향 옆 칸으로 옮긴다. 옆 칸에 방이 있으면 맞바꾸고 비었으면 방만 옮긴다.
        /// 레일이 없는 방·맵 밖·이미 밀린 상태이면 막힘(R8)이고, 레일이 하나도 없는 레벨(챕터 0)이나 Shift를 안 누른 때는 아무 일도 없다.
        /// 성공하면 true. 막히면 false이고 BlockSeq가 늘어난다. 밀리는 순간 그 방은 현실 상태가 되고(R6) 대상은 방과 함께 옮겨진다(R5).
        /// </summary>
        public bool TryPush(int dx, int dy)
        {
            if (Outcome != Outcome.Running || !ShiftHeld || !Board.HasRails) return false;
            int src = Aim.y * Board.Cols + Aim.x;
            int nc = Aim.x + dx, nf = Aim.y + dy;
            if (pushedIdx >= 0 || !Board.HasRail(Aim.y, Aim.x) || !Board.InBounds(nf, nc))
            {
                BlockSeq++;
                LastBlockCell = CellOf(Aim.y, Aim.x);
                return false;
            }
            int dest = nf * Board.Cols + nc;
            pushedIdx = src;
            swappedIdx = roomAt[dest];
            LastMoveFrom = new Vector2Int(Aim.x, Aim.y);
            LastMoveTo = new Vector2Int(nc, nf);
            ApplySwap(src, dest);
            PushSeq++;
            return true;
        }

        /// <summary>밀린 방과 맞바꾼 방을 제자리로 돌린다(R4). 방 안에 있는 대상도 함께 돌아온다(R5).</summary>
        void ReleasePush()
        {
            if (pushedIdx < 0 || Outcome != Outcome.Running) return;
            int now = cellOf[pushedIdx];
            LastMoveFrom = new Vector2Int(now % Board.Cols, now / Board.Cols);
            LastMoveTo = new Vector2Int(pushedIdx % Board.Cols, pushedIdx / Board.Cols);
            ApplySwap(now, pushedIdx);
            pushedIdx = swappedIdx = -1;
            ReturnSeq++;
        }

        /// <summary>두 칸의 방(없을 수도 있음)을 서로 바꾸고, 대상이 그 두 칸 중 하나에 있으면 방과 함께 옮긴다(x 위치 유지).</summary>
        void ApplySwap(int a, int b)
        {
            int ra = roomAt[a], rb = roomAt[b];
            roomAt[a] = rb; roomAt[b] = ra;
            if (ra >= 0) cellOf[ra] = b;
            if (rb >= 0) cellOf[rb] = a;

            int tc = Mathf.Clamp(Mathf.FloorToInt(TargetX / Board.SectorW), 0, Board.Cols - 1);
            int ti = TargetFloor * Board.Cols + tc;
            int to = ti == a ? b : (ti == b ? a : -1);
            if (to < 0) return;
            int oldFloor = TargetFloor;
            TargetFloor = to / Board.Cols;
            TargetX += (to % Board.Cols - tc) * Board.SectorW;
            TargetShiftDX = (to % Board.Cols - tc) * Board.SectorW;
            TargetShiftDY = (TargetFloor - oldFloor) * Board.SectorH;
            TargetShiftSeq++;
            TargetWaiting = false;
            NoteUse(UseKind.Carried, TargetFloor, Mathf.Clamp(Mathf.FloorToInt(TargetX / Board.SectorW), 0, Board.Cols - 1), TargetX);   // W-030 F3: 실려 감
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
                    // 탐색 미션(오브젝트 통과) 완료 때만 추적도가 줄어든다(솜씨 미션은 감소 없음)
                    if (o.Def.mission > 0 && CompleteMission(o.Def.mission)) Relieve(s.missionRelief);
                    return false;
                case ObjectKind.Relay:
                    if (!RelayUsed) { RelayUsed = true; Relieve(s.relayRelief); }
                    return false;
                case ObjectKind.Exit:
                    CompleteMission(0);           // 최종 미션 완료 즉시 의뢰 종료
                    return true;
                case ObjectKind.FailPoint:
                    Outcome = Outcome.Failed;     // 실패 지점(W-007): 의뢰를 바로 다시 시작한다
                    return true;
                case ObjectKind.Stairs:
                case ObjectKind.Elevator:
                case ObjectKind.Hatch:
                    if (!o.Def.hasLink) return false;
                    if (!ObjectOn(o))
                    {
                        // 닫힘·멈춤이면 그냥 지나감. W-030 F3: 현실은 열림·작동인데 설계도로 닫혀 지나친 것을 기록한다(해치는 기록 대상이 아님)
                        if (o.Def.realityOn && o.Def.kind == ObjectKind.Stairs) NoteUse(UseKind.StairsSkipped, EffFloor(o), EffCol(o), EffX(o));
                        else if (o.Def.realityOn && o.Def.kind == ObjectKind.Elevator) NoteUse(UseKind.ElevatorSkipped, EffFloor(o), EffCol(o), EffX(o));
                        return false;
                    }
                    // W-014 R10: 제자리를 벗어난 방 안의 연결, 또는 도착 방이 제자리를 벗어난 연결은 작동하지 않는다(닫힘처럼 지나감)
                    if (IsDisplaced(o.Floor, o.Col) || IsDisplaced(o.Def.linkFloor, o.Def.linkCol)) return false;
                    if (!o.Def.realityOn)
                    {
                        // W-030 F3: 현실은 닫힘·멈춤인데 설계도로 열려 이용함
                        UseKind uk = o.Def.kind == ObjectKind.Hatch ? UseKind.HatchPassed : (o.Def.kind == ObjectKind.Stairs ? UseKind.ShutterPassed : UseKind.ElevatorUsed);
                        NoteUse(uk, EffFloor(o), EffCol(o), EffX(o));
                    }
                    TargetFloor = o.Def.linkFloor;
                    TargetX = o.Def.linkCol * Board.SectorW + o.Def.linkX;
                    TargetFacingRight = o.Def.linkFacingRight;
                    if (o.Def.kind == ObjectKind.Hatch) FallCount++;
                    return true;
            }
            return false;
        }

        // ---------- 미션 판정(W-007) ----------

        /// <summary>그 번호의 미션을 완료로 만든다. 이미 완료·실패 확정이면 false. 0번(최종)이면 의뢰를 끝내고 남은 솜씨 미션을 완료로 확정한다.</summary>
        bool CompleteMission(int i)
        {
            if (i < 0 || i >= missionDone.Length || missionDone[i] || missionFailed[i]) return false;
            missionDone[i] = true;
            if (i == 0)
            {
                for (int k = 1; k < missionDone.Length; k++)
                    if (IsSkill(k) && !missionFailed[k]) missionDone[k] = true;   // 끝까지 실패가 확정되지 않은 솜씨 미션은 완료
                Outcome = Outcome.Complete;
            }
            return true;
        }

        /// <summary>대상이 있는 층·섹터를 보고 섹터 진입(최종)·층 금지 미션을 판정한다.</summary>
        void CheckPosition()
        {
            int col = Mathf.Clamp(Mathf.FloorToInt(TargetX / Board.SectorW), 0, Board.Cols - 1);
            // W-014 R11: 섹터 진입·층 금지는 방의 원래 자리(정체) 기준. 방이 없는 칸이면 칸 위치를 그대로 쓴다
            int id = roomAt[TargetFloor * Board.Cols + col];
            int idFloor = id >= 0 ? id / Board.Cols : TargetFloor, idCol = id >= 0 ? id % Board.Cols : col;
            for (int i = 0; i < Board.Missions.Length && Outcome == Outcome.Running; i++)
            {
                MissionDef m = Board.Missions[i];
                if (m.kind == MissionKind.EnterSector && m.floor == idFloor && m.col == idCol) CompleteMission(i);
                else if (m.kind == MissionKind.NeverEnterFloor && m.floor == idFloor && !missionDone[i]) missionFailed[i] = true;
            }
        }

        /// <summary>추적도가 기준을 넘은 TraceLimit 미션을 실패로 확정한다(되돌리지 않음).</summary>
        void CheckTraceLimits()
        {
            for (int i = 0; i < Board.Missions.Length; i++)
                if (Board.Missions[i].kind == MissionKind.TraceLimit && !missionDone[i] && Tracking > Board.Missions[i].limit) missionFailed[i] = true;
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
            Vector2Int ac = AimCell;   // W-014: 조준한 방의 지금 칸(밀렸으면 옮겨진 칸)을 따라간다
            Vector2 c = Board.SectorCenter(ac.y, ac.x);
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
