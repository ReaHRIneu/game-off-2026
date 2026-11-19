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

        /// <summary>실패 지점에 닿아 다시 시작한 직후 안내 상자에 보이는 문구(W-007).</summary>
        public const string FailNoticeText = "THEY REACHED YOU. TRY AGAIN.";
        public const float FailNoticeSeconds = 2f;

        readonly TutorialStep[] steps;
        readonly bool tutorialLevel;   // W-014: false면 단계 안내만 켠 일반 의뢰(챕터 1). SANDBOX 경고 문구를 쓰지 않는다
        float noticeLeft;

        public int Index { get; private set; }
        public bool NoticeActive => noticeLeft > 0f;
        public int StepCount => steps.Length;

        public Tutorial(LevelData level)
        {
            steps = level.tutorialSteps ?? new TutorialStep[0];
            tutorialLevel = level.tutorial;
        }

        public void Reset()
        {
            Index = 0;
            noticeLeft = 0f;
        }

        /// <summary>실패 후 재시작 안내를 seconds초 동안 보인다(Reset 뒤에 부른다).</summary>
        public void ShowNotice(float seconds) => noticeLeft = seconds;

        /// <summary>안내 시간을 줄인다(플레이 중에만 부른다).</summary>
        public void Tick(float dt)
        {
            if (noticeLeft > 0f) noticeLeft = Mathf.Max(0f, noticeLeft - dt);
        }

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
                case TutorialCond.TargetTurnedBack: return sim.TargetFloor == st.floor && !sim.TargetFacingRight;
                case TutorialCond.FirstPushed: return sim.PushSeq > 0;
            }
            return false;
        }

        /// <summary>지금 보여 줄 문구. 실패 후 재시작 안내가 가장 우선이고, 그다음이 추적도 최대 경고다.</summary>
        public string Text(GameSim sim)
        {
            if (NoticeActive) return FailNoticeText;
            if (tutorialLevel && sim.TraceMaxed) return TraceMaxText;
            return steps.Length == 0 ? "" : steps[Index].text;
        }

        /// <summary>지금 보여 줄 키 배지 글자(없으면 null). 최대치 경고 중에는 배지를 그리지 않는다.</summary>
        public string Key(GameSim sim)
        {
            if (NoticeActive || (tutorialLevel && sim.TraceMaxed) || steps.Length == 0) return null;
            return string.IsNullOrEmpty(steps[Index].key) ? null : steps[Index].key;
        }

        /// <summary>두 번째 키 배지 글자(없으면 null, W-014).</summary>
        public string Key2(GameSim sim)
        {
            if (NoticeActive || (tutorialLevel && sim.TraceMaxed) || steps.Length == 0) return null;
            return string.IsNullOrEmpty(steps[Index].key2) ? null : steps[Index].key2;
        }

        /// <summary>추적도 최대치 경고 상태인가(튜토리얼 의뢰만, W-014).</summary>
        public bool Warning(GameSim sim) => tutorialLevel && sim.TraceMaxed;
    }
}
