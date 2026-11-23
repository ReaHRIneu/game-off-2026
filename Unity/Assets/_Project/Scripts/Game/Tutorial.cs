using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 조작 튜토리얼 상태(W-005 → W-033, D-122). 단계 데이터는 LevelData.tutorialSteps(Mvp1Setup)이고, 단계는 앞으로만 넘어간다.
    /// 단계가 시작되면 게임 시간이 멈추고(Frozen), 요구한 입력이 들어오면 걷히면서 게임이 이어진다.
    /// 어둡게·네온 강조·말풍선은 GameRenderer.DrawTutorialStep이 그린다.
    /// </summary>
    public sealed class Tutorial
    {
        /// <summary>추적도가 최대치일 때 안내 상자에 보이는 문구.</summary>
        public const string TraceMaxText = "TRACE MAX - SANDBOX ONLY.";

        /// <summary>실패 지점에 닿아 다시 시작한 직후 안내 상자에 보이는 문구(W-007).</summary>
        public const string FailNoticeText = "THEY REACHED YOU. TRY AGAIN.";
        public const float FailNoticeSeconds = 2f;

        /// <summary>
        /// 자동 확인 전용(W-033): true면 모든 단계를 즉시 지난 것으로 본다(멈춤·강조 없음). 게임 설정에는 노출하지 않는다.
        /// </summary>
        public static bool AutoPass;

        /// <summary>단계가 끝날 때 어둡게·강조가 걷히는 시간(초).</summary>
        public const float FadeSeconds = 0.15f;

        readonly TutorialStep[] steps;
        readonly bool tutorialLevel;   // W-014: false면 단계 안내만 켠 일반 의뢰(챕터 1). SANDBOX 경고 문구를 쓰지 않는다
        float noticeLeft, fadeLeft;
        int startPush;

        /// <summary>다음에 보일(또는 지금 보이는) 단계 번호. 지난 단계 수와 같다. 실패 재시작에서도 유지된다.</summary>
        public int Index { get; private set; }
        /// <summary>단계가 지금 화면에 떠 있는가(멈춤 상태). AutoPass면 항상 false.</summary>
        public bool Active { get; private set; }
        public bool Frozen => Active && !AutoPass;
        public bool NoticeActive => noticeLeft > 0f;
        public int StepCount => steps.Length;
        /// <summary>틀린 키를 누른 횟수와 마지막 시각(배지 흔들림용).</summary>
        public int WrongSeq { get; private set; }
        public float WrongAt { get; private set; } = -999f;
        /// <summary>어둡게·강조가 보이는 정도 0~1(걷히는 0.15초 동안 줄어든다).</summary>
        public float Dim => Frozen ? 1f : Mathf.Clamp01(fadeLeft / FadeSeconds);
        /// <summary>마지막으로 걷힌 단계(걷히는 동안 그림이 그 영역을 계속 그린다).</summary>
        public int FadingIndex => Index - 1;

        public Tutorial(LevelData level)
        {
            steps = level.tutorialSteps ?? new TutorialStep[0];
            tutorialLevel = level.tutorial;
        }

        /// <summary>의뢰를 처음부터 다시 할 때: 모든 단계를 다시 띄운다.</summary>
        public void Reset()
        {
            Index = 0;
            Active = false;
            noticeLeft = fadeLeft = 0f;
        }

        /// <summary>실패해서 다시 시작할 때: 이미 지난 단계는 다시 띄우지 않는다(같은 의뢰 안).</summary>
        public void RestartAfterFail()
        {
            Active = false;
            fadeLeft = 0f;
        }

        /// <summary>실패 후 재시작 안내를 seconds초 동안 보인다(Reset 뒤에 부른다).</summary>
        public void ShowNotice(float seconds) => noticeLeft = seconds;

        /// <summary>안내·걷힘 시간을 줄인다.</summary>
        public void Tick(float dt)
        {
            if (noticeLeft > 0f) noticeLeft = Mathf.Max(0f, noticeLeft - dt);
            if (fadeLeft > 0f) fadeLeft = Mathf.Max(0f, fadeLeft - dt);
        }

        /// <summary>다음 단계의 시작 조건이 참이면 단계를 띄운다(멈춤 시작). 한 번에 한 단계.</summary>
        public void Update(GameSim sim)
        {
            if (AutoPass) { Index = steps.Length; Active = false; return; }
            if (Active || Index >= steps.Length || !Met(steps[Index], sim)) return;
            Active = true;
            startPush = sim.PushSeq;
            TutorialStep st = steps[Index];
            if (st.input == TutorialInput.Press && st.untilAim && sim.Aim.x == st.aimCol && sim.Aim.y == st.aimFloor) Complete();   // 조준이 이미 가 있으면 건너뜀
        }

        void Complete()
        {
            Active = false;
            Index++;
            fadeLeft = FadeSeconds;
        }

        static bool ArrowMatches(TutorialStep st, InputFrame raw)
        {
            if (st.keyDx == 0 && st.keyDy == 0) return raw.Left || raw.Right || raw.Up || raw.Down;
            return (st.keyDx < 0 && raw.Left) || (st.keyDx > 0 && raw.Right) || (st.keyDy < 0 && raw.Up) || (st.keyDy > 0 && raw.Down);
        }

        static void KeepOnlyStepArrow(TutorialStep st, ref InputFrame f, InputFrame raw)
        {
            if (st.keyDx == 0 && st.keyDy == 0) return;
            f.Left = st.keyDx < 0 && raw.Left; f.Right = st.keyDx > 0 && raw.Right;
            f.Up = st.keyDy < 0 && raw.Up; f.Down = st.keyDy > 0 && raw.Down;
        }

        /// <summary>
        /// 멈춘 단계에서 이 프레임 입력을 걸러 낸다: 받는 키만 남기고 나머지는 지운다(틀린 키 = 입력 무시 + 배지 흔들림).
        /// 걸러 낸 입력은 평소 입력 처리(조준·밀기·Shift)에 그대로 넣는다. 시간만 진행하지 않는다.
        /// </summary>
        public InputFrame Filter(InputFrame raw, bool shiftEdge, GameSim sim)
        {
            TutorialStep st = steps[Index];
            InputFrame f = raw;
            f.Bracket = false;
            f.Enter = false;
            bool wrong = raw.Bracket;
            bool anyArrow = raw.Left || raw.Right || raw.Up || raw.Down;
            switch (st.input)
            {
                case TutorialInput.Enter:
                    f.Left = f.Right = f.Up = f.Down = false;
                    f.LeftShift = sim.ShiftHeld;          // 멈춘 동안 Shift 상태는 그대로 둔다(밀린 방이 되돌아가지 않게)
                    wrong |= anyArrow || shiftEdge;
                    break;
                case TutorialInput.Press:
                    bool ok = ArrowMatches(st, raw);
                    if (!ok) f.Left = f.Right = f.Up = f.Down = false;
                    else KeepOnlyStepArrow(st, ref f, raw);
                    wrong |= (anyArrow && !ok) || shiftEdge || raw.Enter;
                    f.LeftShift = false;                   // Shift를 누르고 있으면 방향키가 방 밀기가 되므로 조준만 받는다
                    break;
                case TutorialInput.Hold:
                    f.Left = f.Right = f.Up = f.Down = false;
                    wrong |= anyArrow || raw.Enter;
                    break;
                case TutorialInput.ShiftArrow:
                    bool match = raw.LeftShift && ArrowMatches(st, raw);
                    if (!match) f.Left = f.Right = f.Up = f.Down = false;
                    else KeepOnlyStepArrow(st, ref f, raw);
                    wrong |= (anyArrow && !match) || raw.Enter;
                    break;
            }
            if (wrong) { WrongSeq++; WrongAt = Time.realtimeSinceStartup; }
            return f;
        }

        /// <summary>입력을 처리한 뒤 단계가 끝났는지 본다. 끝났으면 단계를 넘기고 true.</summary>
        public bool Evaluate(InputFrame raw, bool shiftEdge, GameSim sim)
        {
            TutorialStep st = steps[Index];
            bool done = false;
            switch (st.input)
            {
                case TutorialInput.Enter: done = raw.Enter; break;
                case TutorialInput.Press: done = ArrowMatches(st, raw) && (!st.untilAim || (sim.Aim.x == st.aimCol && sim.Aim.y == st.aimFloor)); break;
                case TutorialInput.Hold: done = shiftEdge; break;
                case TutorialInput.ShiftArrow: done = sim.PushSeq != startPush; break;
            }
            if (done) Complete();
            return done;
        }

        /// <summary>단계 하나의 키 배지 목록(공백으로 나열한 key). Enter 단계에서 비어 있으면 ENTER.</summary>
        public static string[] KeysOf(TutorialStep st)
        {
            if (!string.IsNullOrEmpty(st.key)) return st.key.Split(' ');
            return st.input == TutorialInput.Enter ? new[] { "ENTER" } : new string[0];
        }

        /// <summary>자동 확인·그리기용: i번째 단계 데이터.</summary>
        public TutorialStep StepAt(int i) => steps[i];

        /// <summary>지금 떠 있는 단계 문구(없으면 빈 문자열).</summary>
        public string StepText => Active && Index < steps.Length ? steps[Index].text : "";

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
                case TutorialCond.TargetOnFloor: return sim.TargetFloor == st.floor;
                case TutorialCond.TargetPast:
                    if (sim.TargetFloor != st.floor) return false;
                    float ax = st.col * sim.Board.SectorW + st.x;
                    return st.facingRight ? sim.TargetFacingRight && sim.TargetX >= ax : !sim.TargetFacingRight && sim.TargetX <= ax;
            }
            return false;
        }

        /// <summary>작은 안내 상자(단계가 아닌 알림)에 보일 문구. 실패 후 재시작 안내가 먼저, 그다음 추적도 최대 경고(튜토리얼 의뢰만). 없으면 빈 문자열.</summary>
        public string Text(GameSim sim)
        {
            if (NoticeActive) return FailNoticeText;
            if (tutorialLevel && sim.TraceMaxed && !Frozen) return TraceMaxText;
            return "";
        }

        /// <summary>추적도 최대치 경고 상태인가(튜토리얼 의뢰만, W-014).</summary>
        public bool Warning(GameSim sim) => tutorialLevel && sim.TraceMaxed && !NoticeActive && !Frozen;
    }
}
