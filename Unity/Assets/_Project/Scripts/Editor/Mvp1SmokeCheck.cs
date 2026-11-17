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
    /// 결과를 Unity/Logs/w004-smoke.txt에 쓴다. 캡처는 Docs/QA/captures/W-004/에 저장한다.
    /// W-002에서 화면 흐름 확인을, W-003에서 브리핑·종료 대화·탭 전환·표정 확인을 더했다.
    /// W-004에서 타일 격자 시절 항목을 섹터 모델(조준 점프·유효 상태·문·차단막·계단·엘리베이터·카메라 등) 확인으로 바꿨다.
    /// 긴 이동은 sim을 1/60초씩 직접 진행(Advance)해 빠르고 같은 결과가 나오게 한다.
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
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w004-smoke.txt"));
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

        static string CaptureDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-004"));

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

        /// <summary>
        /// 화면 sim을 1/60초씩 직접 진행한다(조건이 될 때까지). Shift는 스크립트의 shift 값을 쓴다.
        /// 의뢰가 끝나면(Complete·GameOver) 멈춘다. 조건을 못 채우면 Check 실패로 남긴다.
        /// </summary>
        static bool Advance(Func<bool> until, float maxSeconds, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            int n = (int)(maxSeconds * 60f);
            for (int i = 0; i < n; i++)
            {
                if (until()) return true;
                Sim.ShiftHeld = shift;
                Sim.Step(1f / 60f);
                if (Sim.Outcome != Outcome.Running) break;
            }
            bool ok = until();
            if (!ok) Check("advance condition not reached (line " + line + ")", false);
            return ok;
        }

        // ----- 화면 없이 규칙만 확인(새 GameSim을 1/60초씩 직접 진행) -----

        sealed class Rig
        {
            public GameSim S;
            public bool Shift;

            public Rig()
            {
                var settings = AssetDatabase.LoadAssetAtPath<GameOff.Data.GameSettings>("Assets/_Project/Data/GameSettings.asset");
                var level = AssetDatabase.LoadAssetAtPath<GameOff.Data.LevelData>("Assets/_Project/Data/TestRequest.asset");
                S = new GameSim(settings, new Board(level));
            }

            /// <summary>조건이 될 때까지(또는 의뢰가 끝날 때까지) 진행. 조건이 참이면 true.</summary>
            public bool Until(Func<bool> cond, float maxSeconds = 150f)
            {
                int n = (int)(maxSeconds * 60f);
                for (int i = 0; i < n; i++)
                {
                    if (cond()) return true;
                    S.ShiftHeld = Shift;
                    S.Step(1f / 60f);
                    if (S.Outcome != Outcome.Running) break;
                }
                return cond();
            }

            public void Run(float seconds)
            {
                int n = (int)(seconds * 60f);
                for (int i = 0; i < n; i++)
                {
                    S.ShiftHeld = Shift;
                    S.Step(1f / 60f);
                }
            }

            /// <summary>Shift 없이 방향키를 한 번 눌러 조준 점프. 결과 조준 섹터(열, 층)를 돌려준다.</summary>
            public Vector2Int Jump(int dx, int dy)
            {
                Shift = false;
                S.ShiftHeld = false;
                S.MoveAim(dx, dy);
                return S.Aim;
            }

            public SectorObject Obj(GameOff.Data.ObjectKind kind, int floor, int col)
            {
                foreach (SectorObject o in S.Board.Objects)
                    if (o.Def.kind == kind && o.Floor == floor && o.Col == col) return o;
                return null;
            }
        }

        static void CheckAim(string name, Rig r, int dx, int dy, int expectCol, int expectFloor)
        {
            Vector2Int a = r.Jump(dx, dy);
            Check("aim " + name + " -> (col " + expectCol + ", floor " + expectFloor + ")" + (a == P(expectCol, expectFloor) ? "" : " got " + a), a == P(expectCol, expectFloor));
        }

        static void PureChecks()
        {
            var r = new Rig();
            GameSim s = r.S;
            Board b = s.Board;
            Check("map: 3 floors x 5 cols, 800x344", b.Floors == 3 && b.Cols == 5 && b.MapW == 800 && b.MapH == 344);

            // 조작 가능한 섹터 = (0,2) (4,2) (4,1) (4,0) (3,0) (열, 층)
            int operable = 0;
            bool setOk = true;
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++)
                {
                    bool op = b.IsOperable(f, c);
                    if (op) operable++;
                    bool expect = (c == 0 && f == 2) || (c == 4 && f == 2) || (c == 4 && f == 1) || (c == 4 && f == 0) || (c == 3 && f == 0);
                    if (op != expect) setOk = false;
                }
            Check("operable sectors are exactly D1, S1, E(2F), E(3F), B1 sectors", setOk && operable == 5);
            Check("start: floor 2, x 40, facing right, aim (0,2)", s.TargetFloor == 2 && Mathf.Approximately(s.TargetX, 40f) && s.TargetFacingRight && s.Aim == P(0, 2));

            // 조준 점프(조작 가능한 섹터만, ↑↓ 층 이동, 같은 층 우선, 후보 없으면 유지)
            CheckAim("Left from (0,2) keeps", r, -1, 0, 0, 2);
            CheckAim("Down from (0,2) keeps", r, 0, 1, 0, 2);
            CheckAim("Up from (0,2) (score 8 beats 9,10)", r, 0, -1, 3, 0);
            r = new Rig();
            CheckAim("Right from (0,2)", r, 1, 0, 4, 2);
            CheckAim("Right from (4,2) keeps", r, 1, 0, 4, 2);
            CheckAim("Up from (4,2)", r, 0, -1, 4, 1);
            CheckAim("Up from (4,1)", r, 0, -1, 4, 0);
            CheckAim("Left from (4,0)", r, -1, 0, 3, 0);
            CheckAim("Down from (3,0)", r, 0, 1, 4, 1);
            CheckAim("Left from (4,1) (other floor, only candidate side)", r, -1, 0, 3, 0);
            r = new Rig();
            r.Jump(1, 0);
            CheckAim("Left from (4,2): same floor (0,2) wins over (3,0)", r, -1, 0, 0, 2);
            r.S.ShiftHeld = true;
            Check("MoveAim ignored while Shift held", !r.S.MoveAim(1, 0) && r.S.Aim == P(0, 2));
            r.S.ShiftHeld = false;

            // 유효 상태: 섹터 전체형 / 오브젝트형
            r = new Rig();
            s = r.S;
            SectorObject d1 = r.Obj(GameOff.Data.ObjectKind.Door, 2, 0), s1 = r.Obj(GameOff.Data.ObjectKind.Stairs, 2, 4);
            SectorObject e2 = r.Obj(GameOff.Data.ObjectKind.Elevator, 1, 4), e3 = r.Obj(GameOff.Data.ObjectKind.Elevator, 0, 4);
            SectorObject s2 = r.Obj(GameOff.Data.ObjectKind.Stairs, 1, 3), d2 = r.Obj(GameOff.Data.ObjectKind.Door, 1, 2);
            s.ShiftHeld = false;
            Check("no Shift: all reality (D1 closed, S1 closed, E stopped, B1 on)", !s.ObjectOn(d1) && !s.ObjectOn(s1) && !s.ObjectOn(e2) && !s.ObjectOn(e3) && s.SectorBlocked(0, 3));
            s.ShiftHeld = true; // 조준 (0,2)
            Check("Shift on (0,2): D1 open only (object-type)", s.ObjectOn(d1) && !s.ObjectOn(s1) && !s.ObjectOn(e3) && s.SectorBlocked(0, 3));
            Check("Shift: S2 and D2 stay open (no difference)", s.ObjectOn(s2) && s.ObjectOn(d2));
            s.ShiftHeld = false; r.Jump(1, 0); s.ShiftHeld = true; // (4,2)
            Check("Shift on (4,2): S1 open, D1 closed", s.ObjectOn(s1) && !s.ObjectOn(d1) && !s.ObjectOn(e2));
            s.ShiftHeld = false; r.Jump(0, -1); s.ShiftHeld = true; // (4,1)
            Check("Shift on (4,1): elevator running for both sectors (shared)", s.ObjectOn(e2) && s.ObjectOn(e3) && !s.ObjectOn(s1));
            s.ShiftHeld = false; r.Jump(0, -1); s.ShiftHeld = true; // (4,0)
            Check("Shift on (4,0): elevator running for both sectors (shared)", s.ObjectOn(e2) && s.ObjectOn(e3));
            s.ShiftHeld = false; r.Jump(-1, 0); s.ShiftHeld = true; // (3,0)
            Check("Shift on (3,0): blocker off (whole-type), others unchanged", !s.SectorBlocked(0, 3) && !s.ObjectOn(e3) && !s.ObjectOn(d1));

            // 카메라: 조준 섹터가 보이게 정수 px로 따라가고 맵 밖으로 나가지 않음
            r = new Rig();
            Check("camera starts (0,84)", r.S.CamX == 0 && r.S.CamY == 84);
            r.Jump(1, 0); r.Run(0.5f);
            Check("camera -> (388,84) after aim (4,2)", r.S.CamX == 388 && r.S.CamY == 84);
            r.Jump(0, -1); r.Run(0.5f);
            Check("camera -> (388,38) after aim (4,1)", r.S.CamX == 388 && r.S.CamY == 38);
            r.Jump(-1, 0); r.Run(0.5f);
            Check("camera -> (354,0) after aim (3,0)", r.S.CamX == 354 && r.S.CamY == 0);
            r.Jump(0, 1); r.Run(0.05f);
            Check("camera tween: partway after 0.05s (not yet at goal)", r.S.CamY > 0 && r.S.CamY < 38 && r.S.CamX >= 354 && r.S.CamX <= 388);

            // 문 앞 대기 -> Shift로 통과
            r = new Rig();
            r.Until(() => r.S.TargetWaiting, 30f);
            float wx = r.S.TargetX;
            r.Run(3f);
            Check("door D1 closed -> waits at x~145 and stays", r.S.TargetWaiting && Mathf.Abs(wx - 145f) < 0.5f && Mathf.Approximately(r.S.TargetX, wx) && r.S.TargetFloor == 2);
            r.Shift = true;
            r.Until(() => r.S.TargetX > 170f, 10f);
            r.Shift = false;
            Check("Shift on aimed sector -> passes D1", r.S.TargetX > 170f && !r.S.TargetWaiting);

            // 닫힌 계단: 지나쳐 끝 벽에서 돌아섬, 돌아올 때 문(오른쪽에서)에 막힘
            r.Until(() => r.S.MissionA, 30f);
            Check("terminal A (pass-by mission)", r.S.MissionA && !r.S.MissionB);
            r.Until(() => !r.S.TargetFacingRight, 60f);
            Check("closed stairs S1 ignored, right end wall -> turns around (x~791, floor 2)", !r.S.TargetFacingRight && r.S.TargetX >= 790.5f && r.S.TargetFloor == 2);
            r.Until(() => r.S.TargetWaiting, 60f);
            Check("coming back, door D1 blocks from the right (x~163)", r.S.TargetWaiting && Mathf.Abs(r.S.TargetX - 163f) < 0.5f && r.S.TargetFloor == 2);

            // ★ 흐름: D1 -> 단말 A -> S1 열기 -> 2F(오른쪽) -> 엘리베이터 멈춤 -> 끝 벽 -> 중계기 -> 출구
            r = new Rig();
            r.Until(() => r.S.TargetWaiting, 30f);
            r.Shift = true;
            r.Until(() => r.S.TargetX > 170f, 10f);
            r.Shift = false;
            r.Until(() => r.S.MissionA, 30f);
            r.Jump(1, 0);                                   // (4,2) S1
            r.Until(() => r.S.TargetX >= 690f, 60f);
            r.Shift = true;
            r.Until(() => r.S.TargetFloor == 1, 10f);
            r.Shift = false;
            Check("S1 open -> floor 1, col 3, x80, facing right", r.S.TargetFloor == 1 && Mathf.Approximately(r.S.TargetX, 560f) && r.S.TargetFacingRight);
            r.Until(() => !r.S.TargetFacingRight, 60f);
            Check("elevator stopped -> passes by, right end wall turn on floor 1", r.S.TargetFloor == 1 && r.S.TargetX >= 790.5f);
            r.Until(() => r.S.Outcome != Outcome.Running, 90f);
            Check("star route: relay used, exit reached -> Complete (A done, B not)", r.S.Outcome == Outcome.Complete && r.S.RelayUsed && r.S.MissionFinal && r.S.MissionA && !r.S.MissionB);
            Check("star route stars = 2 (final + 1 of 2 others; formula others*2 >= 2)", r.S.Stars == 2);
            Check("star route tracking stays below max", r.S.Tracking < r.S.TrackingMax);
        }

        // ----- 시나리오 -----

        static IEnumerator Run()
        {
            yield return null;
            ClearProgress();
            PureChecks();
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
            Check("sim frozen during briefing (Shift ignored)", Sim.Tracking == 0f && Sim.TargetFloor == 2 && Mathf.Approximately(Sim.TargetX, 40f));
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
            // ===== W-004 섹터 모델: 화면에서 ★★★ 흐름 끝까지 =====
            Check("target starts floor 2 x 40 facing right", Sim.TargetFloor == 2 && Mathf.Approximately(Sim.TargetX, 40f) && Sim.TargetFacingRight);
            Check("initial aim = nearest operable sector (col 0, floor 2)", Sim.Aim == P(0, 2));
            Check("initial camera (0,84) inside map", Sim.CamX == 0 && Sim.CamY == 84);
            yield return Frames(3);
            Capture("01-start-1F");

            // 문 D1 앞 대기
            Advance(() => Sim.TargetWaiting, 30f);
            Check("target waits before closed door D1 (floor 2, x~145)", Sim.TargetWaiting && Mathf.Abs(Sim.TargetX - 145f) < 0.5f && Sim.TargetFloor == 2);

            // 조준 점프 + 카메라 따라가기 + 화면 밖 대상 표시
            yield return Jump(Dir.Right, 4, 2);
            yield return WaitUntil(() => Sim.CamX == 388 && Sim.CamY == 84, "camera follows aim (388,84)");
            Check("camera follows aim and stays inside map (388,84)", Sim.CamX == 388 && Sim.CamY == 84);
            yield return Frames(3);
            Capture("05-target-offscreen");
            yield return Jump(Dir.Left, 0, 2);
            yield return WaitUntil(() => Sim.CamX == 0 && Sim.CamY == 84, "camera back (0,84)");

            // 문 D1: Shift로 열고 지나감
            shift = true;
            Advance(() => Sim.TargetX > 170f, 10f);
            shift = false;
            Check("Shift on aimed sector opens D1 -> target passes", Sim.TargetX > 170f && Sim.TargetFloor == 2 && Sim.Outcome == Outcome.Running);
            Advance(() => Sim.MissionA, 30f);
            Check("terminal A passed -> mission A done", Sim.MissionA && !Sim.MissionB);

            // 계단 셔터 S1 (오브젝트형): 조준 + Shift 겹침 + 이동
            yield return Jump(Dir.Right, 4, 2);
            yield return WaitUntil(() => Sim.CamX == 388 && Sim.CamY == 84, "camera at stairs sector");
            Advance(() => Sim.TargetX >= 650f, 60f);
            yield return Frames(2);
            int sx0 = 16 + Sim.Board.SectorLeft(4) - Sim.CamX, sy0 = 48 + Sim.Board.SectorTop(2) - Sim.CamY;
            int hashPlain = view.Canvas.RegionHash(sx0, sy0, 160, 96);
            shift = true;
            yield return Frames(3);
            int hashShift = view.Canvas.RegionHash(sx0, sy0, 160, 96);
            Check("Shift overlays blueprint on aimed (object-type) sector (canvas changed)", hashPlain != hashShift);
            Capture("02-shift-object-stairs");
            Advance(() => Sim.TargetFloor == 1, 20f);
            shift = false;
            Check("S1 open -> target moves to (floor 1, col 3) x80 facing right", Sim.TargetFloor == 1 && Mathf.Approximately(Sim.TargetX, 3 * 160 + 80f) && Sim.TargetFacingRight);

            // 엘리베이터: (1,4) <-> (0,4), 작동일 때만 이동, 도착 후 왼쪽
            yield return Jump(Dir.Up, 4, 1);
            Advance(() => Sim.TargetX >= 700f, 40f);
            Check("elevator not running -> target still on floor 1", Sim.TargetFloor == 1);
            shift = true;
            Advance(() => Sim.TargetFloor == 0, 10f);
            shift = false;
            Check("elevator running -> target moves to (floor 0, col 4) x80 facing left", Sim.TargetFloor == 0 && Mathf.Approximately(Sim.TargetX, 4 * 160 + 80f) && !Sim.TargetFacingRight);
            yield return Frames(3);
            Capture("04-elevator-3F");

            // 차단막 B1 (섹터 전체형): 켜짐이면 앞에서 대기, Shift로 끄면 통과
            yield return Jump(Dir.Left, 3, 0);
            Advance(() => Sim.TargetWaiting, 20f);
            Check("blocker on -> target waits before sector (floor 0, col 3) x~649", Sim.TargetWaiting && Mathf.Abs(Sim.TargetX - 649f) < 0.5f && Sim.TargetFloor == 0);
            yield return WaitUntil(() => Sim.CamX == 354 && Sim.CamY == 0, "camera at blocker sector (354,0)");
            yield return Frames(2);
            int bx0 = 16 + Sim.Board.SectorLeft(3) - Sim.CamX, by0 = 48 + Sim.Board.SectorTop(0) - Sim.CamY;
            int hashB0 = view.Canvas.RegionHash(bx0, by0, 160, 96);
            shift = true;
            yield return Frames(3);
            int hashB1 = view.Canvas.RegionHash(bx0, by0, 160, 96);
            Check("Shift on whole-type sector turns blocker off (canvas changed)", hashB0 != hashB1 && !Sim.SectorBlocked(0, 3));
            Capture("03-shift-whole-blocker");
            Advance(() => Sim.TargetX < 620f, 10f);
            shift = false;
            Check("blocker off -> target enters sector", Sim.TargetX < 620f && Sim.TargetFloor == 0);
            Advance(() => Sim.MissionB, 30f);
            Check("terminal B passed -> mission B done", Sim.MissionB);

            // 끝 벽 돌아섬, 차단막은 다시 켜져 있어 되돌아올 때 또 대기
            Advance(() => Sim.TargetFacingRight, 30f);
            Check("left end wall -> target turns around (x~9)", Sim.TargetFacingRight && Sim.TargetX <= 9.5f);
            Advance(() => Sim.TargetWaiting, 30f);
            Check("blocker back on -> target waits at x~471 (from the left)", Sim.TargetWaiting && Mathf.Abs(Sim.TargetX - 471f) < 0.5f);
            shift = true;
            Advance(() => Sim.TargetX > 500f, 10f);
            shift = false;
            Check("passes blocker again with Shift", Sim.TargetX > 500f);

            // 엘리베이터로 2F로 복귀
            yield return Jump(Dir.Right, 4, 0);
            Advance(() => Sim.TargetX >= 700f, 40f);
            shift = true;
            Advance(() => Sim.TargetFloor == 1, 10f);
            shift = false;
            Check("elevator from 3F -> floor 1 col 4 x80 facing left", Sim.TargetFloor == 1 && Mathf.Approximately(Sim.TargetX, 4 * 160 + 80f) && !Sim.TargetFacingRight);
            Advance(() => Sim.RelayUsed, 40f);
            Check("relay used while passing (floor 1)", Sim.RelayUsed && Sim.TargetFloor == 1);
            Advance(() => Sim.Outcome != Outcome.Running, 40f);
            Check("exit reached -> Complete", Sim.Outcome == Outcome.Complete && Sim.MissionFinal && Sim.Tracking < 100f);
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending, "ending dialogue");
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
                && !Sim.RelayUsed && Sim.TargetFloor == 2 && Mathf.Approximately(Sim.TargetX, 40f) && Sim.TargetFacingRight && !Sim.CountdownActive && Sim.Aim == P(0, 2));

            // 4) 카운트다운 -> 게임오버
            shift = true;
            Advance(() => Sim.Tracking >= 70f, 20f);
            Check("tracking >= 70 -> face surprise", !Sim.CountdownActive && view.FaceKey == "surprise");
            Advance(() => Sim.CountdownActive, 20f);
            Check("countdown starts at tracking 100", Sim.CountdownActive && Sim.Tracking >= 100f);
            Check("countdown -> face fear", view.FaceKey == "fear");
            yield return Frames(30);
            Capture("04-countdown");
            Advance(() => Sim.Outcome != Outcome.Running, 20f);
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

            // 5) 카운트다운 해제: 추적도 100(카운트다운 시작) 직후 단말 A 통과(-30)로 100 미만 -> 해제
            Advance(() => Sim.TargetWaiting, 30f);
            shift = true;
            Advance(() => Sim.TargetX > 190f, 10f);   // 문 D1 통과(추적도 약 20)
            Advance(() => Sim.CountdownActive, 20f);  // 계속 눌러 100까지
            shift = false;
            Check("countdown active at tracking max (before terminal A)", Sim.CountdownActive && Sim.Tracking >= 100f && !Sim.MissionA);
            Advance(() => Sim.MissionA, 20f);
            Check("countdown cancels when mission A drops tracking below max", !Sim.CountdownActive && Sim.Outcome == Outcome.Running && Sim.Tracking < 100f);
            yield return Frames(5);

            // 6) 일시정지
            Vector2 tpos = new Vector2(Sim.TargetX, Sim.TargetFloor);
            float trk = Sim.Tracking;
            next.Esc = true;
            yield return null;
            yield return null;
            Check("Esc -> Paused", view.CurrentPhase == GameView.Phase.Paused);
            Capture("08-paused");
            yield return Frames(90);
            Check("sim frozen while paused", new Vector2(Sim.TargetX, Sim.TargetFloor) == tpos && Mathf.Approximately(Sim.Tracking, trk));
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
            Check("pause menu Restart resets", view.CurrentPhase == GameView.Phase.Playing && Sim.Tracking == 0f && !Sim.MissionA && Sim.TargetFloor == 2 && Mathf.Approximately(Sim.TargetX, 40f));
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
