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
    /// 결과를 Unity/Logs/w003-smoke.txt에 쓴다. 캡처는 Docs/QA/captures/W-003/에 저장한다.
    /// W-002에서 화면 흐름 확인을, W-003에서 브리핑·종료 대화·탭 전환·설계도 겹침·표정 확인을 더했다.
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
                deadline = EditorApplication.timeSinceStartup + 600;
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
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w003-smoke.txt"));
            File.WriteAllLines(path, results);
            ClearProgress();
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

        static string CaptureDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-003"));

        /// <summary>의뢰 화면 캡처: 캔버스 원본 2배(_x2)와 실제 Game 뷰 출력(_gameview, 정수 배율·레터박스 포함).</summary>
        static void Capture(string name)
        {
            Directory.CreateDirectory(CaptureDir);
            view.Canvas.SavePng(Path.Combine(CaptureDir, name + "_x2.png"), 2);
            ScreenCapture.CaptureScreenshot(Path.Combine(CaptureDir, name + "_gameview.png"));
        }

        static void ClearProgress()
        {
            if (view == null) return;
            var ids = new List<string>();
            for (int i = 0; i < view.Desk.Count; i++) ids.Add(view.Desk.Entry(i).id);
            view.Progress.Clear(ids);
        }

        /// <summary>메뉴 화면 캡처는 W-002에서 끝냈다. W-003에서는 저장하지 않는다.</summary>
        static void CaptureCanvas(string name)
        {
        }

        static IEnumerator Press(string which)
        {
            if (which == "Enter") next.Enter = true;
            else if (which == "Bracket") next.Bracket = true;
            else if (which == "Esc") next.Esc = true;
            else if (which == "Up") next.Up = true;
            else if (which == "Down") next.Down = true;
            yield return null;
            yield return null;
        }

        /// <summary>바탕화면에서 선택한 의뢰를 열고(패널) 접속해 게임이 시작될 때까지 기다린다.</summary>
        static IEnumerator OpenAndConnect()
        {
            yield return Press("Enter");   // 패널 열기
            yield return WaitUntil(() => view.PanelSettled, "panel settled");
            yield return Press("Enter");   // CONNECT
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "connect -> Briefing");
            for (int i = 0; i < 4 && view.CurrentPhase == GameView.Phase.Briefing; i++) yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing, "briefing -> Playing");
        }

        /// <summary>종료 대화가 있으면 Enter로 모두 넘겨 결과 창(Ended)까지 간다.</summary>
        static IEnumerator ToEnded()
        {
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending || view.CurrentPhase == GameView.Phase.Ended, "Ending or Ended");
            for (int i = 0; i < 4 && view.CurrentPhase == GameView.Phase.Ending; i++) yield return Press("Enter");
        }

        static Vector2Int P(int x, int y) => new Vector2Int(x, y);

        // ----- 시나리오 -----

        static IEnumerator Run()
        {
            yield return null;
            ClearProgress();
            Check("start: Main phase", view.CurrentPhase == GameView.Phase.Main);
            yield return Frames(3);
            CaptureCanvas("01-main");

            // ESC는 메인에서 아무 동작 없음
            yield return Press("Esc");
            Check("Esc on Main does nothing", view.CurrentPhase == GameView.Phase.Main);

            // 크레딧 왕복
            yield return Press("Down");
            Check("Down selects CREDITS", view.MainIndex == 1);
            yield return Press("Enter");
            Check("CREDITS -> Credits", view.CurrentPhase == GameView.Phase.Credits);
            yield return Frames(3);
            CaptureCanvas("06-credits");
            yield return Press("Esc");
            Check("Esc on Credits -> Main", view.CurrentPhase == GameView.Phase.Main);
            yield return Press("Up");
            Check("Up selects START", view.MainIndex == 0);

            // START -> 부팅(모니터 켜짐·로딩) -> 바탕화면
            yield return Press("Enter");
            Check("START -> Booting", view.CurrentPhase == GameView.Phase.Booting);
            yield return WaitUntil(() => view.BootStage >= 2, "boot stage 2");
            yield return Frames(2);
            CaptureCanvas("02-boot-stage3");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop");
            Check("Booting -> Desktop (panel closed)", view.CurrentPhase == GameView.Phase.Desktop && !view.PanelOpen);
            yield return Frames(3);
            CaptureCanvas("03-desktop");

            // 잠긴 의뢰: 패널은 열리지만 CONNECT는 무시
            yield return Press("Down");
            Check("Down selects REQ_02", view.DeskIndex == 1);
            Check("REQ_02 is locked (no level)", !view.Desk.IsPlayable(1) && view.Desk.State(1) == RequestState.Locked);
            yield return Press("Enter");
            Check("Enter on locked icon opens panel (LOCKED view)", view.PanelOpen);
            yield return WaitUntil(() => view.PanelSettled, "panel settled (locked)");
            yield return Press("Enter");
            Check("Enter on locked request ignored", view.CurrentPhase == GameView.Phase.Desktop);
            yield return Press("Up");
            Check("Up selects REQ_01 (panel stays open)", view.DeskIndex == 0 && view.PanelOpen);
            yield return Press("Esc");
            Check("Esc closes panel", view.CurrentPhase == GameView.Phase.Desktop && !view.PanelOpen);
            yield return Press("Esc");
            Check("Esc on Desktop -> Main", view.CurrentPhase == GameView.Phase.Main);
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop again");

            // REQ_01: 패널 -> 접속 로딩 -> 게임
            yield return Press("Enter");
            Check("Enter opens detail panel", view.PanelOpen);
            yield return WaitUntil(() => view.PanelSettled, "panel settled (capture)");
            yield return Frames(2);
            CaptureCanvas("04-panel");
            yield return Press("Enter");
            Check("Enter on panel -> Connecting", view.CurrentPhase == GameView.Phase.Connecting);
            yield return Frames(30);
            CaptureCanvas("05-connecting");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "Briefing");
            Check("connect -> Briefing (dialogue first)", view.CurrentPhase == GameView.Phase.Briefing);
            Check("briefing: LOG tab, 1 line, face neutral", view.CurrentTab == GameView.Tab.Log && view.Shown.Count == 1 && view.FaceKey == "neutral");
            shift = true;
            yield return Frames(40);
            shift = false;
            Check("sim frozen during briefing (Shift ignored)", Sim.Tracking == 0f && Sim.TargetPos == P(1, 1));
            next.Bracket = true; yield return null; yield return null;
            next.Esc = true; yield return null; yield return null;
            Check("] and Esc ignored during dialogue", view.CurrentPhase == GameView.Phase.Briefing && view.CurrentTab == GameView.Tab.Log);
            yield return Press("Enter");
            Check("Enter -> 2nd line (ME, neutral)", view.Shown.Count == 2 && view.Shown[1].speaker == GameOff.Data.Speaker.Me);
            yield return Press("Enter");
            Check("Enter -> 3rd line", view.Shown.Count == 3);
            yield return Frames(3);
            Capture("01-briefing");
            yield return Press("Enter");
            Check("4th line: face disgust", view.Shown.Count == 4 && view.FaceKey == "disgust");
            yield return Frames(3);
            Capture("01b-briefing-last");
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing, "Playing");
            Check("briefing end -> Playing on MISSION tab", view.CurrentPhase == GameView.Phase.Playing && view.CurrentTab == GameView.Tab.Mission);
            Check("play face neutral at start", view.FaceKey == "neutral");
            next.Bracket = true; yield return null; yield return null;
            Check("] -> LOG tab (briefing log visible)", view.CurrentTab == GameView.Tab.Log && view.Shown.Count == 4);
            yield return Frames(3);
            Capture("03-log-tab-play");
            next.Bracket = true; yield return null; yield return null;
            Check("] -> MISSION tab again", view.CurrentTab == GameView.Tab.Mission);
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
            int cellX = GameOff.Game.GameRenderer.GridX(Sim.Board) + Sim.Aim.x * 32, cellY = GameOff.Game.GameRenderer.GridY(Sim.Board) + Sim.Aim.y * 32;
            yield return Frames(2);
            int hashPlain = view.Canvas.RegionHash(cellX, cellY, 32, 32);
            shift = true; // 설계도 상태로 방향 변경
            yield return Frames(5);
            int hashShift = view.Canvas.RegionHash(cellX, cellY, 32, 32);
            Check("Shift overlays blueprint tile on aimed cell (canvas changed)", hashPlain != hashShift);
            Capture("02-play-shift-overlay");
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
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending, "ending dialogue");
            Check("exit reached -> Complete", Sim.Outcome == Outcome.Complete);
            Check("Complete -> Ending dialogue (LOG tab, 5th line)", view.CurrentTab == GameView.Tab.Log && view.Shown.Count == 5 && view.FaceKey == "neutral");
            Check("3 stars", Sim.Stars == 3);
            yield return Frames(3);
            Capture("05-ending-dialogue");
            yield return Press("Enter");
            Check("ending 2nd line: face joy", view.Shown.Count == 6 && view.FaceKey == "joy" && view.CurrentPhase == GameView.Phase.Ending);
            yield return Press("Enter");
            Check("ending end -> result (Ended)", view.CurrentPhase == GameView.Phase.Ended);
            Check("result 3 stars -> face joy", view.FaceKey == "joy");
            yield return Frames(5);
            Capture("06-result-3stars");
            yield return Frames(5);

            // 2) 결과 Enter -> 바탕화면(별 갱신) -> 다시 수락: 전부 초기화
            yield return Press("Enter");
            Check("result Enter -> Desktop (panel closed)", view.CurrentPhase == GameView.Phase.Desktop && !view.PanelOpen);
            Check("best stars saved = 3", view.Desk.Best(0) == 3);
            Check("REQ_01 state CLEAR", view.Desk.State(0) == RequestState.Clear);
            Check("REQ_02 unlocked but no level -> still not playable", view.Desk.IsUnlocked(1) && !view.Desk.IsPlayable(1));
            yield return Frames(3);
            CaptureCanvas("07-desktop-after-clear");
            yield return OpenAndConnect();
            Check("re-accept -> Playing", view.CurrentPhase == GameView.Phase.Playing);
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
            yield return ToEnded();
            Check("basic route -> Complete with 1 star", Sim.Outcome == Outcome.Complete && Sim.Stars == 1);
            Check("result 1 star -> face neutral", view.CurrentPhase == GameView.Phase.Ended && view.FaceKey == "neutral");
            yield return Frames(5);
            Capture("06b-result-1star");
            yield return Frames(5);
            yield return Press("Enter");
            Check("result Enter -> Desktop", view.CurrentPhase == GameView.Phase.Desktop);
            Check("best stars stay max (3 after a 1-star clear)", view.Desk.Best(0) == 3);
            yield return OpenAndConnect();

            // 4) 카운트다운 → 게임오버
            shift = true;
            yield return WaitUntil(() => Sim.Tracking >= 70f, "tracking 70");
            Check("tracking >= 70 -> face surprise", !Sim.CountdownActive && view.FaceKey == "surprise");
            yield return WaitUntil(() => Sim.CountdownActive, "countdown starts");
            Check("countdown starts at tracking 100", Sim.Tracking >= 100f);
            Check("countdown -> face fear", view.FaceKey == "fear");
            yield return Frames(30);
            Capture("04-countdown");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "game over");
            shift = false;
            Check("countdown end -> GameOver (no ending dialogue)", Sim.Outcome == Outcome.GameOver && view.Shown.Count == 4);
            Check("game over -> face sadness", view.FaceKey == "sadness");
            yield return Frames(5);
            Capture("07-gameover");
            yield return Frames(5);
            next.Enter = true;
            yield return null;
            yield return null;
            Check("game over Enter -> restart in place (D-031)", view.CurrentPhase == GameView.Phase.Playing && Sim.Tracking == 0f);
            Check("restart skips briefing (MISSION tab, log kept)", view.CurrentTab == GameView.Tab.Mission && view.Shown.Count == 4);

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
            Capture("08-paused");
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
            Check("pause Restart skips briefing", view.CurrentTab == GameView.Tab.Mission && view.Shown.Count == 4);

            // 6-2) 일시정지 -> DESKTOP
            next.Esc = true;
            yield return null;
            yield return null;
            Check("Esc -> Paused (before DESKTOP)", view.CurrentPhase == GameView.Phase.Paused);
            yield return Press("Down");
            yield return Press("Down");
            yield return Press("Enter");
            Check("pause DESKTOP -> Desktop", view.CurrentPhase == GameView.Phase.Desktop && !view.PanelOpen);
            yield return OpenAndConnect();
            Check("accept again -> Playing", view.CurrentPhase == GameView.Phase.Playing);

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
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.RightBracket));
            InputSystem.Update();
            yield return Frames(3);
            Check("[real input] ] key toggles tab", view.CurrentTab == GameView.Tab.Log);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            InputSystem.Update();
            yield return Frames(2);
            InputSystem.RemoveDevice(kb);

            Finish();
        }
    }
}
