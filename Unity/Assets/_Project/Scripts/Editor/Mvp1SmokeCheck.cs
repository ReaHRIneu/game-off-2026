using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GameOff.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GameOff.EditorTools
{
    /// <summary>
    /// MVP-1 작동 확인(작동 확인만, 통합 QA 아님). Play 모드에서 입력을 덮어써 테스트 의뢰를 자동으로 돌리고
    /// 결과를 Unity/Logs/w001-smoke.txt에 쓴다. 캡처는 Docs/QA/captures/W-001/에 저장한다.
    /// 메뉴: GameOff/Run MVP-1 Smoke Check
    /// </summary>
    [InitializeOnLoad]
    public static class Mvp1SmokeCheck
    {
        const string PrefKey = "GameOff.Mvp1Smoke";
        static IEnumerator routine;
        static InputFrame next;
        static bool shift;
        static bool useReal;
        static GameView view;
        static readonly List<string> results = new List<string>();
        static double deadline;

        static Mvp1SmokeCheck()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("GameOff/Run MVP-1 Smoke Check")]
        public static void Start()
        {
            EditorPrefs.SetBool(PrefKey, true);
            EditorSceneManager.OpenScene(Mvp1Setup.ScenePath);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(PrefKey, false))
            {
                EditorPrefs.SetBool(PrefKey, false);
                view = UnityEngine.Object.FindAnyObjectByType<GameView>();
                results.Clear();
                useReal = false;
            stack.Clear();
            routine = Run();
                deadline = EditorApplication.timeSinceStartup + 240;
                GameInput.Override = Provide;
                EditorApplication.update += Watchdog;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                GameInput.Override = null;
                EditorApplication.update -= Watchdog;
            }
        }

        static void Watchdog()
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Check("watchdog timeout", false);
                Finish();
            }
        }

        static InputFrame Provide()
        {
            Advance();
            if (useReal) return GameInput.ReadReal();
            var f = next;
            f.LeftShift = shift;
            next = default;
            return f;
        }

        // 중첩 코루틴(yield return Other())을 지원하는 단순 실행기: 한 프레임에 한 번 진행한다.
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();

        static void Advance()
        {
            if (routine == null) return;
            if (stack.Count == 0) stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
                return; // yield return null: 다음 프레임에 계속
            }
            routine = null;
        }

        static void Check(string name, bool ok)
        {
            results.Add((ok ? "PASS " : "FAIL ") + name);
            Debug.Log("[SMOKE] " + (ok ? "PASS " : "FAIL ") + name);
        }

        static void Finish()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w001-smoke.txt"));
            File.WriteAllLines(path, results);
            GameInput.Override = null;
            EditorApplication.update -= Watchdog;
            EditorApplication.isPlaying = false;
        }

        // ----- 시나리오 도우미 -----

        static GameSim Sim => view.Sim;

        static IEnumerator WaitUntil(Func<bool> cond, string what, int maxFrames = 3000)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (cond()) yield break;
                yield return null;
            }
            Check("timeout waiting: " + what, false);
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Click()
        {
            next.Enter = true;
            yield return null;
        }

        /// <summary>방향키를 한 번 눌러 조준 점프(Shift 해제 상태). 기대 칸에 못 가면 Check 실패로 남긴다.</summary>
        static IEnumerator Jump(Dir d, int x, int y)
        {
            shift = false;
            if (Sim.Aim == P(x, y)) yield break;
            if (d == Dir.Right) next.Right = true;
            else if (d == Dir.Left) next.Left = true;
            else if (d == Dir.Down) next.Down = true;
            else next.Up = true;
            yield return null;
            yield return null;
            Check("jump " + d + " -> (" + x + "," + y + ")", Sim.Aim == P(x, y));
        }

        static void Capture(string name)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-001"));
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
        }

        static Vector2Int P(int x, int y) => new Vector2Int(x, y);

        // ----- 시나리오 -----

        static IEnumerator Run()
        {
            yield return null;
            Check("start: Title phase", view.CurrentPhase == GameView.Phase.Title);
            Capture("01-title");
            yield return Frames(3);

            // 1) 우회 경로: 별 3개
            yield return Click();
            Check("Enter -> Playing", view.CurrentPhase == GameView.Phase.Playing);
            Check("target starts at S (1,1)", Sim.TargetPos == P(1, 1));
            Check("initial aim = nearest operable cell (3,1)", Sim.Aim == P(3, 1));
            next.Left = true; yield return null; yield return null;
            next.Up = true; yield return null; yield return null;
            Check("aim stays when no candidate", Sim.Aim == P(3, 1));
            next.Down = true; yield return null; yield return null;
            Check("Down jumps to (6,3)", Sim.Aim == P(6, 3));
            next.Right = true; yield return null; yield return null;
            next.Right = true; yield return null; yield return null;
            Check("Right from (6,3) jumps to (8,1) (score 6)", Sim.Aim == P(8, 1));
            next.Left = true; yield return null; yield return null;
            Check("Left from (8,1) jumps to (3,1) (score 5 < 6)", Sim.Aim == P(3, 1));
            yield return Jump(Dir.Left, 3, 1);
            yield return WaitUntil(() => Sim.TargetPos == P(2, 1), "target at (2,1)");
            Check("target waited ~3s then moved (still Playing)", view.CurrentPhase == GameView.Phase.Playing);
            shift = true; // 설계도 상태로 방향 변경
            yield return Frames(5);
            Capture("02-shift-overlay");
            Check("shift raises tracking", Sim.Tracking > 0f);
            yield return WaitUntil(() => Sim.TargetPos == P(3, 1), "target at (3,1)");
            Check("arrow (3,1) turned down via blueprint", Sim.TargetDir == Dir.Down);
            shift = false;
            float trackingAfterRelease = Sim.Tracking;
            yield return Frames(30);
            Check("tracking does not decay after release", Mathf.Approximately(trackingAfterRelease, Sim.Tracking));

            // Shift 중 방향키 무시
            Vector2Int aimBefore = Sim.Aim;
            shift = true;
            next.Right = true;
            yield return null;
            shift = false;
            Check("arrow keys ignored while Shift held", Sim.Aim == aimBefore);

            yield return Jump(Dir.Down, 6, 3);
            yield return WaitUntil(() => Sim.TargetPos == P(5, 3), "target at (5,3)");
            Check("mission A done at (4,3), tracking relieved", Sim.MissionA);
            shift = true; // 문 열기
            yield return WaitUntil(() => Sim.TargetPos == P(6, 3), "target in door (6,3)");
            shift = false;
            yield return WaitUntil(() => Sim.RelayUsed, "relay used");
            Check("mission B done", Sim.MissionB);
            Check("relay used once", Sim.RelayUsed);
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "end");
            Check("exit reached -> Complete", Sim.Outcome == Outcome.Complete);
            Check("3 stars", Sim.Stars == 3);
            yield return Frames(5);
            Capture("03-result-3stars");
            yield return Frames(5);

            // 2) 다시 시작: 전부 초기화
            next.Enter = true;
            yield return null;
            yield return null;
            Check("restart -> Playing", view.CurrentPhase == GameView.Phase.Playing);
            Check("restart resets state", Sim.Tracking == 0f && !Sim.MissionA && !Sim.MissionB && !Sim.MissionFinal
                && !Sim.RelayUsed && Sim.TargetPos == P(1, 1) && Sim.TargetDir == Dir.Right && !Sim.CountdownActive);

            // 3) 기본 경로: 윗줄 문(8,1)만 열고 출구 → 별 1개
            yield return Jump(Dir.Right, 8, 1);
            yield return WaitUntil(() => Sim.TargetPos == P(6, 1), "target at (6,1)");
            // 문이 닫혀 있으면 (7,1)에서 대기해야 함
            yield return WaitUntil(() => Sim.TargetPos == P(7, 1), "target at (7,1)");
            yield return Frames(120);
            Check("target waits before closed door (8,1)", Sim.TargetPos == P(7, 1));
            shift = true;
            yield return WaitUntil(() => Sim.TargetPos == P(8, 1), "target in door (8,1)");
            shift = false;
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "end 2");
            Check("basic route -> Complete with 1 star", Sim.Outcome == Outcome.Complete && Sim.Stars == 1);
            yield return Frames(5);
            Capture("04-result-1star");
            yield return Frames(5);
            next.Enter = true;
            yield return null;
            yield return null;

            // 4) 카운트다운 → 게임오버
            shift = true;
            yield return WaitUntil(() => Sim.CountdownActive, "countdown starts");
            Check("countdown starts at tracking 100", Sim.Tracking >= 100f);
            yield return Frames(30);
            Capture("05-countdown");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "game over");
            shift = false;
            Check("countdown end -> GameOver", Sim.Outcome == Outcome.GameOver);
            yield return Frames(5);
            Capture("06-gameover");
            yield return Frames(5);
            next.Enter = true;
            yield return null;
            yield return null;

            // 5) 카운트다운 해제: 추적도 100(카운트다운 시작) 후 체크 지점 B 통과(-30)로 100 미만 -> 해제
            yield return Jump(Dir.Left, 3, 1);
            shift = true;
            yield return WaitUntil(() => Sim.TargetPos == P(3, 1), "target at (3,1) (cancel scenario)");
            shift = false;
            yield return Jump(Dir.Left, 3, 1); // 문(6,3)이 아닌 칸에 조준
            shift = true;
            yield return WaitUntil(() => Sim.CountdownActive, "countdown active (cancel scenario)");
            Check("countdown active at tracking max", Sim.CountdownActive && Sim.Tracking >= 100f);
            Check("door still closed -> target waits at (5,3)", Sim.TargetPos.x <= 5 && Sim.TargetPos.y == 3);
            shift = false;
            yield return Jump(Dir.Down, 6, 3);
            shift = true; // 문을 열어 B 지점으로 보냄(추적도는 100 유지)
            yield return WaitUntil(() => Sim.TargetPos == P(6, 3), "target in door (cancel scenario)");
            shift = false;
            yield return WaitUntil(() => Sim.MissionB, "mission B (cancel scenario)");
            Check("countdown cancels when tracking drops below max", !Sim.CountdownActive && Sim.Outcome == Outcome.Running && Sim.Tracking < 100f);
            yield return Frames(5);

            // 6) 일시정지
            Vector2Int tpos = Sim.TargetPos;
            float trk = Sim.Tracking;
            next.Esc = true;
            yield return null;
            yield return null;
            Check("Esc -> Paused", view.CurrentPhase == GameView.Phase.Paused);
            Capture("07-paused");
            yield return Frames(90);
            Check("sim frozen while paused", Sim.TargetPos == tpos && Mathf.Approximately(Sim.Tracking, trk));
            next.Down = true;
            yield return null;
            yield return null;
            next.Up = true;
            yield return null;
            yield return null;
            next.Enter = true; // Continue
            yield return null;
            yield return null;
            Check("Continue resumes", view.CurrentPhase == GameView.Phase.Playing);
            // 포커스 상실 -> 자동 일시정지
            view.SendMessage("OnApplicationFocus", false);
            yield return null;
            Check("focus lost -> Paused", view.CurrentPhase == GameView.Phase.Paused);
            // Restart 메뉴
            next.Down = true;
            yield return null;
            yield return null;
            next.Enter = true;
            yield return null;
            yield return null;
            Check("pause menu Restart resets", view.CurrentPhase == GameView.Phase.Playing && Sim.Tracking == 0f && !Sim.MissionA && Sim.TargetPos == P(1, 1));

            // 7) 실제 Input System 키보드: 오른쪽 Shift 무시, 왼쪽 Shift 인식
            useReal = true;
            var kb = InputSystem.AddDevice<Keyboard>("SmokeKeyboard");
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.RightShift));
            InputSystem.Update();
            yield return Frames(20);
            Check("[real input] Right Shift ignored (tracking 0)", Sim.Tracking == 0f);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.LeftShift));
            InputSystem.Update();
            yield return Frames(30);
            Check("[real input] Left Shift raises tracking", Sim.Tracking > 0f);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            InputSystem.Update();
            yield return Frames(2);
            InputSystem.RemoveDevice(kb);

            Finish();
        }
    }
}
