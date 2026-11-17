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
    /// 결과를 Unity/Logs/w005-smoke.txt에 쓴다. 캡처는 Docs/QA/captures/W-005/에 저장한다.
    /// W-002에서 화면 흐름 확인을, W-003에서 브리핑·종료 대화·탭 전환·표정 확인을 더했다.
    /// W-004에서 타일 격자 시절 항목을 섹터 모델(조준 점프·유효 상태·문·차단막·계단·엘리베이터·카메라 등) 확인으로 바꿨다.
    /// W-005에서 화면 시나리오를 챕터 0 의뢰(0-1·0-2)와 챕터 폴더 화면, 튜토리얼 안내 단계로 바꿨다.
    /// W-004 테스트 의뢰(TestRequest)는 의뢰 목록에서 빠졌으므로 규칙 확인(섹터 모델·카운트다운)은 화면 없이 그 에셋으로 한다.
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
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w005-smoke.txt"));
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

        static string CaptureDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-005"));

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

            public Rig(string levelPath = "Assets/_Project/Data/TestRequest.asset")
            {
                var settings = AssetDatabase.LoadAssetAtPath<GameOff.Data.GameSettings>("Assets/_Project/Data/GameSettings.asset");
                var level = AssetDatabase.LoadAssetAtPath<GameOff.Data.LevelData>(levelPath);
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

        // ----- 챕터 0 규칙(화면 없이, W-005) -----

        const string Ch01Path = "Assets/_Project/Data/Ch0_1.asset";
        const string Ch02Path = "Assets/_Project/Data/Ch0_2.asset";

        static void Ch0PureChecks()
        {
            // 의뢰 0-1: 1층 x 4섹터, 조작 가능한 섹터는 D1(열 0)·D2(열 2)
            var r = new Rig(Ch01Path);
            GameSim s = r.S;
            Board b = s.Board;
            Check("0-1 map: 1 floor x 4 cols, 640x152, tutorial level", b.Floors == 1 && b.Cols == 4 && b.MapW == 640 && b.MapH == 152 && b.Tutorial);
            bool setOk = true;
            for (int c = 0; c < b.Cols; c++)
                if (b.IsOperable(0, c) != (c == 0 || c == 2)) setOk = false;
            Check("0-1 operable sectors are exactly D1 (col 0) and D2 (col 2)", setOk);
            Check("0-1 start: floor 0, x 30, facing right, aim (0,0)", s.TargetFloor == 0 && Mathf.Approximately(s.TargetX, 30f) && s.TargetFacingRight && s.Aim == P(0, 0));
            Check("0-1 doors D1 x154 and D2 x154, exit x138", FindObj(b, GameOff.Data.ObjectKind.Door, 0, 0) != null && Mathf.Approximately(FindObj(b, GameOff.Data.ObjectKind.Door, 0, 0).AbsX, 154f)
                && FindObj(b, GameOff.Data.ObjectKind.Door, 0, 2) != null && Mathf.Approximately(FindObj(b, GameOff.Data.ObjectKind.Door, 0, 2).AbsX, 2 * 160 + 154f)
                && FindObj(b, GameOff.Data.ObjectKind.Exit, 0, 3) != null && Mathf.Approximately(FindObj(b, GameOff.Data.ObjectKind.Exit, 0, 3).AbsX, 3 * 160 + 138f));

            // 튜토리얼: 추적도가 최대치여도 카운트다운·게임오버가 없고 추적도는 최대치에 머문다
            r.Shift = true;
            r.Run(20f);
            Check("tutorial: tracking stays at max, no countdown, still running after 20 s of Shift", s.Tracking >= s.TrackingMax && !s.CountdownActive && s.Outcome == Outcome.Running);
            // 대조: 튜토리얼이 아닌 W-004 테스트 의뢰는 같은 조건에서 카운트다운이 시작되고 게임오버가 된다
            var t = new Rig();
            t.Shift = true;
            t.Until(() => t.S.CountdownActive, 20f);
            Check("non-tutorial (W-004 test level): countdown starts at tracking max", t.S.CountdownActive && t.S.Tracking >= t.S.TrackingMax);
            t.Until(() => t.S.Outcome != Outcome.Running, 20f);
            Check("non-tutorial: countdown end -> GameOver", t.S.Outcome == Outcome.GameOver);
            // 카운트다운 해제(추적도 100 직후 단말 A 통과로 100 미만)
            t = new Rig();
            t.Until(() => t.S.TargetWaiting, 30f);
            t.Shift = true;
            t.Until(() => t.S.TargetX > 190f, 10f);
            t.Until(() => t.S.CountdownActive, 20f);
            t.Shift = false;
            bool active = t.S.CountdownActive && t.S.Tracking >= 100f && !t.S.MissionA;
            t.Until(() => t.S.MissionA, 20f);
            Check("non-tutorial: countdown cancels when mission A drops tracking below max", active && !t.S.CountdownActive && t.S.Outcome == Outcome.Running && t.S.Tracking < 100f);

            // 의뢰 0-2: 2층 x 4섹터, 조작 가능한 섹터는 S1 섹터(열 3, 1층)와 차단막 섹터(열 2, 2층)
            r = new Rig(Ch02Path);
            s = r.S;
            b = s.Board;
            Check("0-2 map: 2 floors x 4 cols, 640x248, tutorial level", b.Floors == 2 && b.Cols == 4 && b.MapW == 640 && b.MapH == 248 && b.Tutorial);
            setOk = true;
            int operable = 0;
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++)
                {
                    bool op = b.IsOperable(f, c);
                    if (op) operable++;
                    if (op != ((f == 1 && c == 3) || (f == 0 && c == 2))) setOk = false;
                }
            Check("0-2 operable sectors are exactly S1 sector (3,floor 1) and blocker sector (2,floor 0)", setOk && operable == 2);
            Check("0-2 arrival stairs (floor 0, col 3) not operable, blocker sector is whole-type", !b.IsOperable(0, 3) && b.IsWhole(0, 2));
            Check("0-2 start: floor 1, x 30, facing right", s.TargetFloor == 1 && Mathf.Approximately(s.TargetX, 30f) && s.TargetFacingRight);
            // 거리 동점(S1·차단막 모두 3)이면 시작 층 우선 → S1 섹터(열 3, 층 1)
            Check("0-2 initial aim = S1 sector (3,1): distance tie, same floor as start wins", s.Aim == P(3, 1));
        }

        static SectorObject FindObj(Board b, GameOff.Data.ObjectKind kind, int floor, int col)
        {
            foreach (SectorObject o in b.Objects)
                if (o.Def.kind == kind && o.Floor == floor && o.Col == col) return o;
            return null;
        }

        /// <summary>sim을 seconds초 동안 1/60초씩 진행한다(의뢰가 끝나면 멈춤). Shift는 스크립트의 shift 값.</summary>
        static void RunSeconds(float seconds)
        {
            int n = (int)(seconds * 60f);
            for (int i = 0; i < n; i++)
            {
                Sim.ShiftHeld = shift;
                Sim.Step(1f / 60f);
                if (Sim.Outcome != Outcome.Running) break;
            }
        }

        static bool TutIs(int index, string key, string text) =>
            view.Tut != null && view.Tut.Index == index && view.Tut.Key(Sim) == key && view.Tut.Text(Sim) == text;

        // ----- 시나리오 -----

        static IEnumerator Run()
        {
            yield return null;
            ClearProgress();
            PureChecks();
            Ch0PureChecks();
            Check("start: Main phase", view.CurrentPhase == GameView.Phase.Main);
            yield return Frames(3);

            // ESC는 메인에서 아무 동작 없음
            yield return Press("Esc");
            Check("Esc on Main does nothing", view.CurrentPhase == GameView.Phase.Main);

            // 크레딧 왕복
            yield return Press("Down");
            Check("Down selects CREDITS", view.MainIndex == 1);
            yield return Press("Enter");
            Check("CREDITS -> Credits", view.CurrentPhase == GameView.Phase.Credits);
            yield return Press("Esc");
            Check("Esc on Credits -> Main", view.CurrentPhase == GameView.Phase.Main);
            yield return Press("Up");
            Check("Up selects START", view.MainIndex == 0);

            // START -> 부팅 -> 바탕화면(챕터 폴더 화면)
            yield return Press("Enter");
            Check("START -> Booting", view.CurrentPhase == GameView.Phase.Booting);
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop");
            Check("Booting -> Desktop shows the chapter folder screen (not inside a folder, panel closed)",
                view.CurrentPhase == GameView.Phase.Desktop && !view.InFolder && !view.PanelOpen && view.FolderIndex == 0);
            Check("3 chapter folders: CH0 open, CH1 and CH2 locked (no requests yet)", view.Desk.ChapterOpen(0) && !view.Desk.ChapterOpen(1) && !view.Desk.ChapterOpen(2));
            Check("folder names CH0_SANDBOX / CH1_??? / CH2_???", RequestBoard.ChapterName(0) == "CH0_SANDBOX" && RequestBoard.ChapterName(1) == "CH1_???" && RequestBoard.ChapterName(2) == "CH2_???");
            Check("request list has only chapter 0 requests 0-1 and 0-2 (test level removed)", view.Desk.Count == 2 && view.Desk.Number(0) == "0-1" && view.Desk.Number(1) == "0-2"
                && view.Desk.Entry(0).chapter == 0 && view.Desk.Entry(1).chapter == 0);
            yield return Frames(3);
            Capture("01-folder-screen");

            // 잠긴 폴더: 패널에 LOCKED, Enter 무시, 열린 폴더로 돌아오면 패널 닫힘
            yield return Press("Down");
            Check("Down selects CH1 folder", view.FolderIndex == 1 && !view.InFolder);
            yield return Press("Enter");
            Check("Enter on locked folder opens LOCKED panel", view.PanelOpen && !view.InFolder);
            yield return WaitUntil(() => view.PanelSettled, "panel settled (locked folder)");
            yield return Frames(2);
            Capture("02-folder-locked-panel");
            yield return Press("Enter");
            Check("Enter on locked folder again is ignored", view.CurrentPhase == GameView.Phase.Desktop && !view.InFolder && view.PanelOpen);
            yield return Press("Down");
            Check("Down to CH2 (locked): panel stays", view.FolderIndex == 2 && view.PanelOpen);
            yield return Press("Down");
            Check("Down wraps to CH0 (open): panel closes", view.FolderIndex == 0 && !view.PanelOpen);
            yield return Press("Down");
            yield return Press("Enter");
            yield return Press("Esc");
            Check("Esc closes the folder panel first (stays on folder screen)", view.CurrentPhase == GameView.Phase.Desktop && !view.InFolder && !view.PanelOpen && view.FolderIndex == 1);
            yield return Press("Up");
            Check("Up selects CH0 again", view.FolderIndex == 0);
            yield return Press("Esc");
            Check("Esc on folder screen -> Main", view.CurrentPhase == GameView.Phase.Main);
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop again");
            Check("back on folder screen with CH0 selected", !view.InFolder && view.FolderIndex == 0);

            // 폴더 열기: 의뢰 목록(REQ_0-1, REQ_0-2)
            yield return Press("Enter");
            Check("Enter on CH0 -> request list inside folder (REQ_0-1 selected)", view.InFolder && view.FolderIndex == 0 && view.DeskIndex == 0 && !view.PanelOpen);
            Check("list names REQ_0-1 / REQ_0-2", view.Desk.RequestName(0) == "REQ_0-1" && view.Desk.RequestName(1) == "REQ_0-2");
            Check("REQ_0-1 playable (NEW), REQ_0-2 locked until 0-1 clears", view.Desk.State(0) == RequestState.New && view.Desk.State(1) == RequestState.Locked);
            yield return Frames(3);
            Capture("03-request-list");
            yield return Press("Down");
            Check("Down selects REQ_0-2", view.DeskIndex == 1);
            yield return Press("Enter");
            Check("Enter on locked request opens panel (LOCKED view)", view.PanelOpen);
            yield return WaitUntil(() => view.PanelSettled, "panel settled (locked request)");
            yield return Press("Enter");
            Check("Enter on locked request ignored", view.CurrentPhase == GameView.Phase.Desktop);
            yield return Press("Esc");
            Check("Esc closes panel (still inside folder)", view.InFolder && !view.PanelOpen);
            yield return Press("Esc");
            Check("Esc in request list -> folder screen", !view.InFolder && view.CurrentPhase == GameView.Phase.Desktop);
            yield return Press("Enter");
            Check("Enter reopens CH0 -> list", view.InFolder && view.DeskIndex == 0);

            // ===== 의뢰 0-1 ★★★ 흐름 =====
            yield return Press("Enter");
            Check("Enter opens detail panel for REQ_0-1", view.PanelOpen);
            yield return WaitUntil(() => view.PanelSettled, "panel settled (0-1)");
            yield return Frames(2);
            Capture("04-panel-0-1");
            yield return Press("Enter");
            Check("Enter on panel -> Connecting", view.CurrentPhase == GameView.Phase.Connecting);
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "Briefing 0-1");
            Check("0-1 briefing: SANDBOX line 1 on LOG tab, face neutral", view.CurrentTab == GameView.Tab.Log && view.Shown.Count == 1
                && view.Shown[0].speaker == GameOff.Data.Speaker.Client && view.Shown[0].text == "TRAINING SIM A LOADED." && view.FaceKey == "neutral");
            yield return Press("Enter");
            Check("0-1 briefing line 2 (ME, neutral)", view.Shown.Count == 2 && view.Shown[1].speaker == GameOff.Data.Speaker.Me && view.Shown[1].text == "LET'S GET THIS OVER WITH." && view.FaceKey == "neutral");
            yield return Frames(3);
            Capture("05-briefing-0-1");
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing, "Playing 0-1");
            Check("briefing end -> Playing on MISSION tab", view.CurrentTab == GameView.Tab.Mission);
            yield return Frames(3);
            Check("0-1 tutorial step 1: SHIFT badge, 'THE DOOR IS LOCKED...'", TutIs(0, "SHIFT", "THE DOOR IS LOCKED. HOLD SHIFT TO SHIFT IT."));
            Check("0-1 start: floor 0 x 30 facing right, aim (0,0), camera inside map", Sim.TargetFloor == 0 && Mathf.Approximately(Sim.TargetX, 30f) && Sim.TargetFacingRight && Sim.Aim == P(0, 0) && Sim.CamY == 0);
            Capture("06-0-1-step1");

            Advance(() => Sim.TargetWaiting, 30f);
            Check("0-1 target waits before D1 (x~145)", Sim.TargetWaiting && Mathf.Abs(Sim.TargetX - 145f) < 0.5f);
            shift = true;
            Advance(() => Sim.TargetX > 170f, 10f);
            Check("Shift on D1 sector opens D1 -> target passes, tracking rose", Sim.TargetX > 170f && Sim.Tracking > 0f && Sim.Outcome == Outcome.Running);
            yield return Frames(2);
            Check("step still 1 while Shift is held", view.Tut.Index == 0);
            shift = false;
            yield return Frames(3);
            Check("0-1 tutorial step 2 after Shift released: 'TRACE RISES WHILE YOU HOLD...' (no badge)", TutIs(1, null, "TRACE RISES WHILE YOU HOLD. KEEP IT SHORT."));
            Capture("07-0-1-step2");
            Advance(() => Sim.MissionB, 40f);
            Check("terminals A and B passed", Sim.MissionA && Sim.MissionB);
            Advance(() => Sim.TargetWaiting, 40f);
            Check("0-1 target waits before D2 (x~465, sector col 2)", Sim.TargetWaiting && Mathf.Abs(Sim.TargetX - 465f) < 0.5f);
            yield return Frames(3);
            Check("0-1 tutorial step 3 when waiting at D2: arrow badge", TutIs(2, "→", "PRESS → TO JUMP YOUR AIM TO THE NEXT ROOM."));
            Capture("08-0-1-step3-arrow");
            yield return Jump(Dir.Right, 2, 0);
            yield return Frames(3);
            Check("0-1 tutorial step 4 when aim reaches D2 sector: SHIFT badge, 'NOW HOLD SHIFT.'", TutIs(3, "SHIFT", "NOW HOLD SHIFT."));
            Capture("09-0-1-step4");
            shift = true;
            Advance(() => Sim.Outcome != Outcome.Running, 30f);
            shift = false;
            Check("0-1 exit reached -> Complete with 3 stars, tracking below max", Sim.Outcome == Outcome.Complete && Sim.MissionFinal && Sim.Stars == 3 && Sim.Tracking < 100f);
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending, "ending 0-1");
            Check("0-1 ending line 1 (SANDBOX: SIM A CLEAR.)", view.Shown.Count == 3 && view.Shown[2].text == "SIM A CLEAR." && view.CurrentTab == GameView.Tab.Log);
            yield return Press("Enter");
            Check("0-1 ending line 2 (ME: NEXT.)", view.Shown.Count == 4 && view.Shown[3].text == "NEXT." && view.FaceKey == "neutral");
            yield return Press("Enter");
            Check("ending end -> result (Ended), 3 stars, no star-2 hint", view.CurrentPhase == GameView.Phase.Ended && view.ResultHint == null);
            yield return Frames(5);
            Capture("10-0-1-result-3stars");

            // 결과 Enter -> 그 챕터의 의뢰 목록, 0-1 클리어 후 0-2 열림
            yield return Press("Enter");
            Check("result Enter -> request list of CH0 (folder open, REQ_0-1 selected, panel closed)", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder && view.FolderIndex == 0 && view.DeskIndex == 0 && !view.PanelOpen);
            Check("best stars saved = 3, REQ_0-1 CLEAR", view.Desk.Best(0) == 3 && view.Desk.State(0) == RequestState.Clear);
            Check("0-1 cleared -> REQ_0-2 opens (playable, NEW)", view.Desk.IsUnlocked(1) && view.Desk.IsPlayable(1) && view.Desk.State(1) == RequestState.New);
            Check("CH1 stays locked (no requests yet)", !view.Desk.ChapterOpen(1) && !view.Desk.ChapterOpen(2));
            yield return Frames(3);
            Capture("11-request-list-after-clear");

            // ===== 의뢰 0-2 ★★ 흐름 =====
            yield return Press("Down");
            Check("Down selects REQ_0-2", view.DeskIndex == 1);
            yield return OpenAndConnect();
            Check("0-2 briefing log: SANDBOX + ME lines, last face disgust", view.Shown.Count == 2 && view.Shown[0].text == "SIM B. STAIRS AND RELAYS." && view.Shown[1].text == "FINE.");
            Check("0-2 playing, tutorial step 1: 'STAIRS WORK ONLY WHEN THE SHUTTER IS OPEN.' (no badge)", view.CurrentPhase == GameView.Phase.Playing && TutIs(0, null, "STAIRS WORK ONLY WHEN THE SHUTTER IS OPEN."));
            Check("0-2 start: floor 1 x 30 facing right, camera inside map", Sim.TargetFloor == 1 && Mathf.Approximately(Sim.TargetX, 30f) && Sim.TargetFacingRight && Sim.CamY == 0);
            yield return Frames(3);
            Capture("12-0-2-step1");
            yield return Jump(Dir.Right, 3, 1);
            Advance(() => Sim.TargetX >= 500f, 30f);
            Check("terminal A passed on the way", Sim.MissionA && !Sim.MissionB && Sim.TargetFloor == 1);
            shift = true;
            yield return Frames(3);
            Capture("13-0-2-shift-stairs");
            Advance(() => Sim.TargetFloor == 0, 10f);
            shift = false;
            Check("star-2 route: S1 opened on the first pass -> floor 0, col 3 x50, facing left", Sim.TargetFloor == 0 && Mathf.Approximately(Sim.TargetX, 3 * 160 + 50f) && !Sim.TargetFacingRight && !Sim.MissionB);
            Advance(() => Sim.TargetWaiting, 20f);
            Check("target waits before blocker sector on floor 0 (x~489)", Sim.TargetWaiting && Sim.TargetFloor == 0 && Mathf.Abs(Sim.TargetX - 489f) < 0.5f);
            yield return Frames(3);
            Check("0-2 tutorial step 2 when waiting at the blocker: 'SOME ROOMS SHIFT AS A WHOLE...' (no badge)", TutIs(1, null, "SOME ROOMS SHIFT AS A WHOLE. AIM AT THE ROOM."));
            Capture("14-0-2-step2-blocker");
            yield return Jump(Dir.Left, 2, 0);
            shift = true;
            Advance(() => Sim.TargetX < 470f, 10f);
            shift = false;
            Check("Shift on whole-type sector turns blocker off -> target enters", Sim.TargetX < 470f && Sim.TargetFloor == 0);
            Advance(() => Sim.RelayUsed, 30f);
            yield return Frames(3);
            Check("0-2 tutorial step 3 after the relay: 'RELAYS LOWER YOUR TRACE.'", TutIs(2, null, "RELAYS LOWER YOUR TRACE."));
            Advance(() => Sim.Outcome != Outcome.Running, 30f);
            Check("0-2 exit reached -> Complete with 2 stars (A done, B missed)", Sim.Outcome == Outcome.Complete && Sim.MissionA && !Sim.MissionB && Sim.Stars == 2);
            yield return ToEnded();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "Ended 0-2 star 2");
            Check("0-2 ending dialogue shown (SIM B CLEAR. / OK. joy)", view.Shown.Count == 4 && view.Shown[2].text == "SIM B CLEAR." && view.Shown[3].text == "OK.");
            Check("0-2 star-2 result hint line 'TERMINAL B WAS PAST THE STAIRS. TRY AGAIN?'", view.ResultHint == "TERMINAL B WAS PAST THE STAIRS. TRY AGAIN?");
            yield return Frames(5);
            Capture("15-0-2-result-2stars");
            yield return Press("Enter");
            Check("result Enter -> request list of CH0 (REQ_0-2 selected), best 2", view.InFolder && view.DeskIndex == 1 && view.Desk.Best(1) == 2 && view.CurrentPhase == GameView.Phase.Desktop);

            // ===== 의뢰 0-2 ★★★ 흐름 =====
            yield return OpenAndConnect();
            Check("0-2 again: tutorial step restarts at 1", view.CurrentPhase == GameView.Phase.Playing && TutIs(0, null, "STAIRS WORK ONLY WHEN THE SHUTTER IS OPEN."));
            yield return Jump(Dir.Right, 3, 1);
            shift = false;
            Advance(() => Sim.MissionB, 40f);
            Check("star-3 route: S1 closed on the first pass -> ignored, terminal B reached", Sim.MissionB && Sim.TargetFloor == 1);
            Advance(() => !Sim.TargetFacingRight, 40f);
            Check("right end wall -> target turns around on floor 1", Sim.TargetFloor == 1 && !Sim.TargetFacingRight && Sim.TargetX >= 630f);
            Advance(() => Sim.TargetX <= 560f, 10f);
            shift = true;
            Advance(() => Sim.TargetFloor == 0, 10f);
            shift = false;
            Check("S1 opened on the way back -> floor 0, col 3 x50, facing left", Sim.TargetFloor == 0 && Mathf.Approximately(Sim.TargetX, 3 * 160 + 50f) && !Sim.TargetFacingRight);
            Advance(() => Sim.TargetWaiting, 20f);
            yield return Jump(Dir.Left, 2, 0);
            shift = true;
            Advance(() => Sim.TargetX < 470f, 10f);
            shift = false;
            Advance(() => Sim.Outcome != Outcome.Running, 30f);
            Check("0-2 star-3 route -> Complete with 3 stars", Sim.Outcome == Outcome.Complete && Sim.MissionA && Sim.MissionB && Sim.Stars == 3);
            yield return ToEnded();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "Ended 0-2 star 3");
            Check("0-2 star-3 result has no hint line", view.ResultHint == null && view.Desk.Best(1) == 3);
            yield return Frames(5);
            Capture("16-0-2-result-3stars");
            yield return Press("Enter");
            Check("result Enter -> request list again", view.InFolder && view.CurrentPhase == GameView.Phase.Desktop && view.DeskIndex == 1);

            // ===== 챕터 0 추적도 최대치: 게임오버 없음 + 안내 상자 경고 =====
            yield return Press("Up");
            Check("Up selects REQ_0-1", view.DeskIndex == 0);
            yield return OpenAndConnect();
            Check("0-1 replay -> Playing, tracking 0, step 1", view.CurrentPhase == GameView.Phase.Playing && Sim.Tracking == 0f && TutIs(0, "SHIFT", "THE DOOR IS LOCKED. HOLD SHIFT TO SHIFT IT."));
            shift = true;
            Advance(() => Sim.TraceMaxed, 20f);
            Check("tracking reaches max", Sim.TraceMaxed && Sim.Tracking >= 100f);
            yield return Frames(3);
            Check("tracking max: warning text replaces the step text, no badge", view.Tut.Text(Sim) == "TRACE MAX - SANDBOX ONLY." && view.Tut.Key(Sim) == null);
            Check("tracking max: face surprise (no countdown face)", view.FaceKey == "surprise");
            Capture("17-trace-max");
            RunSeconds(10f);
            yield return Frames(3);
            Check("tracking max for 10+ more seconds: no countdown, still running, tracking stays at max", Sim.TraceMaxed && !Sim.CountdownActive && Sim.Outcome == Outcome.Running && view.CurrentPhase == GameView.Phase.Playing);
            shift = false;
            yield return Frames(3);

            // 일시정지(튜토리얼도 같은 흐름)
            Vector2 tpos = new Vector2(Sim.TargetX, Sim.TargetFloor);
            float trk = Sim.Tracking;
            next.Esc = true;
            yield return null;
            yield return null;
            Check("Esc -> Paused", view.CurrentPhase == GameView.Phase.Paused);
            Capture("18-paused");
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
            view.SendMessage("OnApplicationFocus", false);
            yield return null;
            Check("focus lost -> Paused", view.CurrentPhase == GameView.Phase.Paused);
            next.Down = true;
            yield return null;
            yield return null;
            next.Enter = true;
            yield return null;
            yield return null;
            Check("pause menu Restart resets (tracking, missions, target, tutorial step)", view.CurrentPhase == GameView.Phase.Playing && Sim.Tracking == 0f && !Sim.MissionA && Sim.TargetFloor == 0
                && Mathf.Approximately(Sim.TargetX, 30f) && !Sim.ShiftReleasedOnce && view.Tut.Index == 0);
            Check("pause Restart skips briefing (MISSION tab, log kept)", view.CurrentTab == GameView.Tab.Mission && view.Shown.Count == 2);

            // 일시정지 -> DESKTOP: 그 챕터의 의뢰 목록으로
            next.Esc = true;
            yield return null;
            yield return null;
            Check("Esc -> Paused (before DESKTOP)", view.CurrentPhase == GameView.Phase.Paused);
            yield return Press("Down");
            yield return Press("Down");
            yield return Press("Enter");
            Check("pause DESKTOP -> request list of CH0 (REQ_0-1)", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder && view.FolderIndex == 0 && view.DeskIndex == 0 && !view.PanelOpen);
            yield return OpenAndConnect();
            Check("accept again -> Playing", view.CurrentPhase == GameView.Phase.Playing);

            // 실제 Input System 키보드: 오른쪽 Shift 무시, 왼쪽 Shift 인식
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
