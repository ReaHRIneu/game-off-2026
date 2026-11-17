using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 챕터 0 튜토리얼 안내 상태(W-005, 03-01 §1-1·§1-2). 단계 조건은 LevelData의 tutorialSteps 데이터이고,
    /// 단계는 앞으로만 넘어간다. 단계 안내는 LOG 탭 대화가 아니라 장면 패널 위쪽의 안내 상자로 보인다(D-045 유지).
    /// </summary>
    public sealed class Tutorial
    {
        /// <summary>추적도가 최대치일 때 안내 상자에 보이는 문구.</summary>
        public const string TraceMaxText = "TRACE MAX - SANDBOX ONLY.";

        readonly TutorialStep[] steps;

        public int Index { get; private set; }
        public int StepCount => steps.Length;

        public Tutorial(LevelData level)
        {
            steps = level.tutorialSteps ?? new TutorialStep[0];
        }

        public void Reset() => Index = 0;

        /// <summary>다음 단계의 조건이 참이면 넘어간다(한 번에 여러 단계도 가능).</summary>
        public void Update(GameSim sim)
        {
            while (Index + 1 < steps.Length && Met(steps[Index + 1], sim)) Index++;
        }

        static bool Met(TutorialStep st, GameSim sim)
        {
            switch (st.cond)
            {
                case TutorialCond.Start: return true;
                case TutorialCond.ShiftReleased: return sim.ShiftReleasedOnce;
                case TutorialCond.TargetWaiting:
                    if (!sim.TargetWaiting || sim.TargetFloor != st.floor) return false;
                    return st.col < 0 || Mathf.FloorToInt(sim.TargetX / sim.Board.SectorW) == st.col;
                case TutorialCond.AimAt: return sim.Aim.x == st.col && sim.Aim.y == st.floor;
                case TutorialCond.RelayUsed: return sim.RelayUsed;
            }
            return false;
        }

        /// <summary>지금 보여 줄 문구. 추적도가 최대치면 그 경고가 우선한다.</summary>
        public string Text(GameSim sim)
        {
            if (sim.TraceMaxed) return TraceMaxText;
            return steps.Length == 0 ? "" : steps[Index].text;
        }

        /// <summary>지금 보여 줄 키 배지 글자(없으면 null). 최대치 경고 중에는 배지를 그리지 않는다.</summary>
        public string Key(GameSim sim)
        {
            if (sim.TraceMaxed || steps.Length == 0) return null;
            return string.IsNullOrEmpty(steps[Index].key) ? null : steps[Index].key;
        }
    }
}
