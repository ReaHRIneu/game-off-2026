using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    public enum Outcome { Running, GameOver, Complete }

    /// <summary>
    /// 한 번의 의뢰 진행 규칙(층 어긋남·대상 이동·추적도·미션). 화면·입력과 분리된 순수 로직.
    /// </summary>
    public sealed class GameSim
    {
        public const int OtherMissionCount = 2; // 체크 지점 A, B

        readonly GameSettings s;
        public Board Board { get; }

        public Vector2Int Aim { get; private set; }
        public Vector2Int TargetPos { get; private set; }
        public Dir TargetDir { get; private set; }
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

        float moveTimer;

        public GameSim(GameSettings settings, Board board)
        {
            s = settings;
            Board = board;
            Reset();
        }

        /// <summary>전부 초기화(추적도·미션·대상 위치·카운트다운).</summary>
        public void Reset()
        {
            Aim = InitialAim();
            TargetPos = Board.Start;
            TargetDir = Dir.Right;
            Tracking = 0f;
            CountdownActive = false;
            Countdown = 0f;
            MissionA = MissionB = MissionFinal = false;
            RelayUsed = false;
            Outcome = Outcome.Running;
            ShiftHeld = false;
            moveTimer = s.startDelaySeconds;
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

        /// <summary>조작 가능한 칸 = 현실 상태와 설계도 상태가 다른 칸.</summary>
        public bool IsOperable(int x, int y)
        {
            Cell r = Board.Reality[x, y], b = Board.Blueprint[x, y];
            return r.Kind != b.Kind || (r.Kind == CellKind.Arrow && r.Dir != b.Dir);
        }

        /// <summary>
        /// 조준 점프(D-038): 누른 방향(진행 축 거리 &gt; 0)의 조작 가능한 칸 중
        /// 진행 축 거리 + 2 x 옆 축 거리가 최소인 칸으로 옮긴다. 같으면 진행 축 거리가 짧은 칸. 후보 없으면 그대로.
        /// Shift를 누른 동안은 무시. 옮겼으면 true.
        /// </summary>
        public bool MoveAim(int dx, int dy)
        {
            if (ShiftHeld || Outcome != Outcome.Running) return false;
            bool found = false;
            int bestScore = 0, bestAxis = 0;
            Vector2Int best = Aim;
            for (int y = 0; y < Board.Height; y++)
            {
                for (int x = 0; x < Board.Width; x++)
                {
                    if (!IsOperable(x, y)) continue;
                    int axis = dx != 0 ? (x - Aim.x) * dx : (y - Aim.y) * dy;
                    if (axis <= 0) continue;
                    int side = dx != 0 ? Mathf.Abs(y - Aim.y) : Mathf.Abs(x - Aim.x);
                    int score = axis + 2 * side;
                    if (!found || score < bestScore || (score == bestScore && axis < bestAxis))
                    {
                        found = true; bestScore = score; bestAxis = axis; best = new Vector2Int(x, y);
                    }
                }
            }
            if (!found) return false;
            Aim = best;
            return true;
        }

        Vector2Int InitialAim()
        {
            // 시작 지점에서 가장 가까운(맨해튼 거리) 조작 가능한 칸. 없으면 시작 지점.
            Vector2Int best = Board.Start;
            int bestD = int.MaxValue;
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                {
                    if (!IsOperable(x, y)) continue;
                    int d = Mathf.Abs(x - Board.Start.x) + Mathf.Abs(y - Board.Start.y);
                    if (d < bestD) { bestD = d; best = new Vector2Int(x, y); }
                }
            return best;
        }

        /// <summary>그 순간의 유효 칸 상태. Shift를 누른 동안 조준한 칸만 설계도 상태.</summary>
        public Cell Effective(int x, int y)
        {
            if (ShiftHeld && x == Aim.x && y == Aim.y) return Board.Blueprint[x, y];
            return Board.Reality[x, y];
        }

        public void Step(float dt)
        {
            if (Outcome != Outcome.Running) return;

            if (ShiftHeld)
                Tracking = Mathf.Min(s.trackingMax, Tracking + s.trackingRatePerSecond * dt);

            moveTimer -= dt;
            while (moveTimer <= 0f && Outcome == Outcome.Running)
            {
                moveTimer += s.moveIntervalSeconds;
                TryMove();
            }
            if (Outcome != Outcome.Running) return;

            UpdateCountdown(dt);
        }

        void TryMove()
        {
            int dx = 0, dy = 0;
            switch (TargetDir)
            {
                case Dir.Right: dx = 1; break;
                case Dir.Left: dx = -1; break;
                case Dir.Down: dy = 1; break;
                case Dir.Up: dy = -1; break;
            }
            int nx = TargetPos.x + dx, ny = TargetPos.y + dy;
            if (!Board.InBounds(nx, ny)) return;

            Cell c = Effective(nx, ny);
            if (!c.Passable) return; // 제자리 대기(다음 간격에 다시 판정)

            TargetPos = new Vector2Int(nx, ny);
            switch (c.Kind)
            {
                case CellKind.Arrow:
                    TargetDir = c.Dir;
                    break;
                case CellKind.CheckA:
                    if (!MissionA) { MissionA = true; Relieve(s.missionRelief); }
                    break;
                case CellKind.CheckB:
                    if (!MissionB) { MissionB = true; Relieve(s.missionRelief); }
                    break;
                case CellKind.Relay:
                    if (!RelayUsed) { RelayUsed = true; Relieve(s.relayRelief); }
                    break;
                case CellKind.Exit:
                    MissionFinal = true;
                    Outcome = Outcome.Complete; // 최종 미션 완료 즉시 의뢰 종료
                    break;
            }
        }

        void Relieve(float amount) => Tracking = Mathf.Max(0f, Tracking - amount);

        void UpdateCountdown(float dt)
        {
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
    }
}
