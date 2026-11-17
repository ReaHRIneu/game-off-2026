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
    /// 결과를 Unity/Logs/w007-smoke.txt에 쓴다. 캡처는 Docs/QA/captures/W-009/에 저장한다(W-009에서 CaptureW9만 저장).
    /// W-002에서 화면 흐름 확인을, W-003에서 브리핑·종료 대화·탭 전환·표정 확인을 더했다.
    /// W-004에서 타일 격자 시절 항목을 섹터 모델(조준 점프·유효 상태·문·차단막·계단·엘리베이터·카메라 등) 확인으로 바꿨다.
    /// W-005에서 화면 시나리오를 챕터 0 의뢰(0-1·0-2)와 챕터 폴더 화면, 튜토리얼 안내 단계로 바꿨다.
    /// W-004 테스트 의뢰(TestRequest)는 의뢰 목록에서 빠졌으므로 규칙 확인(섹터 모델·카운트다운)은 화면 없이 그 에셋으로 한다.
    /// 긴 이동은 sim을 1/60초씩 직접 진행(Advance)해 빠르고 같은 결과가 나오게 한다.
    /// W-007에서 챕터 0 확인을 새 챕터 0(주인공 건물 침입자 저지: 실패 지점·해치·미션 목록·미션 실패 표시)으로 바꿨다. 캡처는 Docs/QA/captures/W-007/.
    /// W-008에서 튜토리얼 의뢰의 미션을 최종 미션 하나로, 별(RATING·결과·아이콘)을 없애고 클리어 기준 열림 조건·설계도 닫힘/멈춤 겹침 그림 확인으로 바꿨다. 캡처는 Docs/QA/captures/W-008/.
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
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w007-smoke.txt"));
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

        static string CaptureDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-009"));

        /// <summary>W-009부터 장면마다 캡처하지 않는다(요구 캡처 4종은 CaptureW9). 호출은 남겨 두어 시나리오 위치를 표시한다.</summary>
        static void Capture(string name)
        {
        }

        /// <summary>
        /// W-009 캡처: 캔버스 출력(2560x1440 RenderTexture)을 Game 뷰와 같은 레터박스·Bilinear 방식으로 1920x1080과 2560x1440 PNG로 저장하고,
        /// 실제 Game 뷰 화면도 함께 저장한다(_gameview, 에디터 Game 뷰 크기 그대로).
        /// </summary>
        static void CaptureW9(string name)
        {
            Directory.CreateDirectory(CaptureDir);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir, name + "_1920x1080.png"), 1920, 1080);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir, name + "_2560x1440.png"), 2560, 1440);
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

        // ----- 챕터 0 규칙(화면 없이, W-007: 주인공 건물 침입자 저지) -----

        const string Home1Path = "Assets/_Project/Data/Ch0Home_1.asset";
        const string Home2Path = "Assets/_Project/Data/Ch0Home_2.asset";

        static void Ch0PureChecks()
        {
            Home1PureChecks();
            Home2PureChecks();
            ChapterGateChecks();

            // 대조: 튜토리얼이 아닌 W-004 테스트 의뢰는 같은 조건에서 카운트다운이 시작되고 게임오버가 된다
            var t = new Rig();
            t.Shift = true;
            t.Until(() => t.S.CountdownActive, 20f);
            Check("non-tutorial (W-004 test level): countdown starts at tracking max", t.S.CountdownActive && t.S.Tracking >= t.S.TrackingMax);
            t.Until(() => t.S.Outcome != Outcome.Running, 20f);
            Check("non-tutorial: countdown end -> GameOver", t.S.Outcome == Outcome.GameOver);
            // 카운트다운 해제(추적도 100 직후 단말 A 통과로 100 미만) — 탐색 미션(PASS_OBJECT) 완료는 추적도를 줄인다
            t = new Rig();
            t.Until(() => t.S.TargetWaiting, 30f);
            t.Shift = true;
            t.Until(() => t.S.TargetX > 190f, 10f);
            t.Until(() => t.S.CountdownActive, 20f);
            t.Shift = false;
            bool active = t.S.CountdownActive && t.S.Tracking >= 100f && !t.S.MissionA;
            t.Until(() => t.S.MissionA, 20f);
            Check("non-tutorial: countdown cancels when mission A drops tracking below max", active && !t.S.CountdownActive && t.S.Outcome == Outcome.Running && t.S.Tracking < 100f);
        }

        static void Home1PureChecks()
        {
            var r = new Rig(Home1Path);
            GameSim s = r.S;
            Board b = s.Board;
            Check("0-1 map: 3 floors x 4 cols, 640x344, tutorial level", b.Floors == 3 && b.Cols == 4 && b.MapW == 640 && b.MapH == 344 && b.Tutorial && b.BuildingName == "HOME BLDG");
            bool setOk = true;
            int operable = 0;
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++)
                {
                    bool op = b.IsOperable(f, c);
                    if (op) operable++;
                    if (op != (f == 1 && (c == 2 || c == 3))) setOk = false;
                }
            Check("0-1 operable sectors are exactly the hatch (col 2, floor 1) and the stairs (col 3, floor 1)", setOk && operable == 2);
            Check("0-1 start: floor 1, col 1 x30 (map x190), facing right, first aim = stairs sector (3,1) by data (the hatch sector is nearer)",
                s.TargetFloor == 1 && Mathf.Approximately(s.TargetX, 190f) && s.TargetFacingRight && s.Aim == P(3, 1));
            SectorObject st = FindObj(b, GameOff.Data.ObjectKind.Stairs, 1, 3), ha = FindObj(b, GameOff.Data.ObjectKind.Hatch, 1, 2);
            SectorObject te = FindObj(b, GameOff.Data.ObjectKind.Terminal, 1, 0), fp = FindObj(b, GameOff.Data.ObjectKind.FailPoint, 0, 0);
            Check("0-1 objects: stairs x540, hatch x400 (link B1 col 2 x80), fail point x120, no security terminal (W-008)",
                st != null && Mathf.Approximately(st.AbsX, 540f) && ha != null && Mathf.Approximately(ha.AbsX, 400f) && ha.Def.hasLink && ha.Def.linkFloor == 2 && ha.Def.linkCol == 2
                && te == null && fp != null && Mathf.Approximately(fp.AbsX, 120f));
            Check("0-1 missions: only the final ENTER_SECTOR (floor 2, col 2) (W-008)",
                s.MissionCount == 1 && s.Missions[0].kind == GameOff.Data.MissionKind.EnterSector && s.Missions[0].floor == 2 && s.Missions[0].col == 2);

            // 튜토리얼: 추적도가 최대치여도 카운트다운·게임오버가 없고 추적도는 최대치에 머문다
            r.Shift = true;
            r.Run(20f);
            Check("tutorial: tracking stays at max, no countdown, still running after 20 s of Shift", s.Tracking >= s.TrackingMax && !s.CountdownActive && s.Outcome == Outcome.Running);

            // 계단을 열어 둔 채: 2층에 오르는 순간 KEEP THEM OFF 2F 실패 확정 -> 실패 지점에서 Failed
            r = new Rig(Home1Path);
            s = r.S;
            bool up = r.Until(() => s.TargetFloor == 0, 60f);
            Check("0-1 stairs open (no Shift): runner climbs to 2F, col 3 x60, facing left", up && Mathf.Approximately(s.TargetX, 540f) && !s.TargetFacingRight);
            Check("0-1 entering 2F: no mission fails (final mission only), still running", s.MissionCount == 1 && !s.MissionFailed(0) && !s.MissionDone(0) && s.Outcome == Outcome.Running);
            r.Until(() => s.Outcome != Outcome.Running, 60f);
            Check("0-1 fail point: runner center crosses x120 on 2F -> Outcome.Failed (not GameOver, not Complete)", s.Outcome == Outcome.Failed && Mathf.Approximately(s.TargetX, 120f) && s.TargetFloor == 0);

            // 해치: 닫힌 채면 그냥 지나감, 설계도 상태(조준 + Shift)일 때만 지하로
            r = new Rig(Home1Path);
            s = r.S;
            r.Jump(-1, 0);
            Check("0-1 aim jump ← from the stairs sector goes to the hatch sector (2,1)", s.Aim == P(2, 1));
            r.Until(() => s.TargetX >= 430f, 60f);
            Check("0-1 hatch closed (aimed, no Shift): runner passes over it, stays on floor 1", s.TargetFloor == 1 && s.TargetX >= 430f && !s.TargetWaiting);
            r = new Rig(Home1Path);
            s = r.S;
            r.Jump(-1, 0);
            r.Until(() => s.TargetX >= 380f, 60f);
            r.Shift = true;
            r.Until(() => s.Outcome != Outcome.Running, 5f);
            r.Shift = false;
            Check("0-1 hatch opened first (Shift on its sector): falls to B1 col 2 x80 facing right -> Complete (ENTER_SECTOR)",
                s.Outcome == Outcome.Complete && s.TargetFloor == 2 && Mathf.Approximately(s.TargetX, 400f) && s.TargetFacingRight && s.MissionFinal);
            Check("0-1 hatch-first: final mission done, tracking below max", s.MissionDone(0) && s.Tracking < s.TrackingMax);

            // ★3 흐름: 계단 막기 -> 끝 벽 -> 다시 막기 -> 해치는 닫힌 채 -> 단말 -> 돌아와 해치 열기
            r = new Rig(Home1Path);
            s = r.S;
            r.Until(() => s.TargetX >= 520f, 60f);
            Check("0-1 star-3 route: hatch passed while closed on the first pass (floor 1)", s.TargetFloor == 1 && s.TargetX >= 520f);
            r.Shift = true;
            r.Until(() => s.TargetX >= 560f, 10f);
            r.Shift = false;
            Check("0-1 Shift on the stairs sector seals the stairs: passes by on floor 1", s.TargetFloor == 1 && s.TargetX >= 560f);
            r.Until(() => !s.TargetFacingRight, 60f);
            Check("0-1 right end wall: turns back toward the hatch (facing left, floor 1)", s.TargetFloor == 1 && !s.TargetFacingRight && s.TargetX >= 630f);
            r.Until(() => s.TargetX <= 560f, 10f);
            r.Shift = true;
            r.Until(() => s.TargetX <= 515f, 10f);
            r.Shift = false;
            Check("0-1 stairs sealed again on the way back (still floor 1)", s.TargetFloor == 1 && s.TargetX <= 515f);
            r.Until(() => s.TargetX <= 100f, 30f);
            r.Until(() => s.TargetFacingRight && s.TargetX >= 150f, 30f);
            r.Jump(-1, 0);
            Check("0-1 aim jump ← to the hatch sector for the return trip", s.Aim == P(2, 1));
            r.Until(() => s.TargetX >= 380f, 30f);
            float tBefore = s.Tracking;
            r.Shift = true;
            r.Until(() => s.Outcome != Outcome.Running, 5f);
            r.Shift = false;
            Check("0-1 hatch opened on the way back: Complete (final mission done)", s.Outcome == Outcome.Complete && s.TargetFloor == 2 && Mathf.Approximately(s.TargetX, 400f) && s.MissionDone(0));
            Check("0-1 finishing lowers no tracking", s.Tracking >= tBefore);
        }

        /// <summary>0-2 레이저·엘리베이터·중계기·해치 경로를 끝까지 진행한다. W-008: 미션은 최종 미션 하나뿐이라 추적도 제한 분기는 없다.</summary>
        static void Home2Run(Rig r)
        {
            GameSim s = r.S;
            r.Until(() => s.TargetX >= 215f, 60f);
            r.Shift = true;
            r.Until(() => s.TargetX >= 260f, 10f);
            r.Shift = false;
            r.Until(() => s.TargetWaiting, 20f);
            r.Jump(1, 0);
            r.Shift = true;
            r.Until(() => s.TargetX > 330f, 10f);
            r.Shift = false;
            r.Until(() => s.RelayUsed, 10f);
            r.Jump(1, 0);
            r.Until(() => s.TargetX >= 540f, 10f);
            r.Shift = true;
            r.Until(() => s.Outcome != Outcome.Running, 5f);
            r.Shift = false;
        }

        static void Home2PureChecks()
        {
            var r = new Rig(Home2Path);
            GameSim s = r.S;
            Board b = s.Board;
            Check("0-2 map: 3 floors x 4 cols, 640x344, tutorial level", b.Floors == 3 && b.Cols == 4 && b.MapW == 640 && b.MapH == 344 && b.Tutorial && b.BuildingName == "HOME BLDG");
            bool setOk = true;
            int operable = 0;
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++)
                {
                    bool op = b.IsOperable(f, c);
                    if (op) operable++;
                    if (op != (f == 1 && c >= 1 && c <= 3)) setOk = false;
                }
            Check("0-2 operable sectors are exactly elevator (1,1), laser (2,1), hatch (3,1) (col, floor)", setOk && operable == 3);
            Check("0-2 laser sector is whole-type and on in reality, off in blueprint", b.IsWhole(1, 2) && b.Blocked(1, 2, false) && !b.Blocked(1, 2, true));
            Check("0-2 start: floor 1, x 30, facing right, aim = elevator sector (1,1)", s.TargetFloor == 1 && Mathf.Approximately(s.TargetX, 30f) && s.TargetFacingRight && s.Aim == P(1, 1));
            Check("0-2 missions: only the final ENTER_SECTOR (floor 2, col 3) (W-008)",
                s.MissionCount == 1 && s.Missions[0].kind == GameOff.Data.MissionKind.EnterSector && s.Missions[0].floor == 2 && s.Missions[0].col == 3);

            // 엘리베이터를 작동 상태로 둠: 3층으로 올라가 KEEP THEM OFF 3F 실패 -> 실패 지점
            bool up = r.Until(() => s.TargetFloor == 0, 60f);
            Check("0-2 elevator running (no Shift): rides to 3F col 1 x80 facing left",
                up && Mathf.Approximately(s.TargetX, 240f) && !s.TargetFacingRight);
            r.Until(() => s.Outcome != Outcome.Running, 60f);
            Check("0-2 fail point x120 on 3F -> Outcome.Failed", s.Outcome == Outcome.Failed && Mathf.Approximately(s.TargetX, 120f) && s.TargetFloor == 0);

            // 전체 경로
            r = new Rig(Home2Path);
            s = r.S;
            r.Until(() => s.TargetX >= 215f, 60f);
            r.Shift = true;
            r.Until(() => s.TargetX >= 260f, 10f);
            r.Shift = false;
            Check("0-2 Shift on the elevator sector stops it: runner passes by on floor 1 (3F not entered)", s.TargetFloor == 1 && s.TargetX >= 260f);
            r.Until(() => s.TargetWaiting, 20f);
            r.Run(2f);
            Check("0-2 waits before the laser sector (x~311) and stays", s.TargetWaiting && Mathf.Abs(s.TargetX - 311f) < 0.5f && s.TargetFloor == 1);
            r.Jump(1, 0);
            Check("0-2 aim jump → to the laser sector (2,1)", s.Aim == P(2, 1));
            r.Shift = true;
            r.Until(() => s.TargetX > 330f, 10f);
            r.Shift = false;
            r.Run(0.05f);   // Shift 해제가 sim에 반영되도록 한 걸음
            Check("0-2 Shift on the whole-type laser sector turns the laser off: runner enters; laser back on after release", s.TargetX > 330f && !s.TargetWaiting && s.SectorBlocked(1, 2));
            r.Until(() => s.TargetX >= 415f, 10f);
            float tr = s.Tracking;
            r.Until(() => s.RelayUsed, 10f);
            Check("0-2 relay in the laser corridor lowers tracking", s.RelayUsed && s.Tracking < tr);
            r.Jump(1, 0);
            Check("0-2 aim jump → to the hatch sector (3,1)", s.Aim == P(3, 1));
            r.Until(() => s.TargetX >= 540f, 10f);
            r.Shift = true;
            r.Until(() => s.Outcome != Outcome.Running, 5f);
            r.Shift = false;
            Check("0-2 hatch opened: falls to B1 col 3 (boiler room) x80 facing left -> Complete", s.Outcome == Outcome.Complete && s.TargetFloor == 2 && Mathf.Approximately(s.TargetX, 560f) && !s.TargetFacingRight);
            Check("0-2 full route: final mission done", s.MissionDone(0));

            // Home2Run 보조 경로로도 끝까지 진행되는지
            r = new Rig(Home2Path);
            s = r.S;
            Home2Run(r);
            Check("0-2 helper route: Complete", s.Outcome == Outcome.Complete && s.MissionDone(0));
        }

        /// <summary>W-008: 챕터 열림 조건 = 앞 챕터 의뢰 모두 클리어(별 없는 튜토리얼 포함). 가짜 의뢰 목록으로 확인한다.</summary>
        static void ChapterGateChecks()
        {
            var list = ScriptableObject.CreateInstance<GameOff.Data.RequestList>();
            var tutLevel = ScriptableObject.CreateInstance<GameOff.Data.LevelData>();
            tutLevel.tutorial = true;
            var nextLevel = ScriptableObject.CreateInstance<GameOff.Data.LevelData>();
            list.entries = new[]
            {
                new GameOff.Data.RequestEntry { id = "w008-gate-a", chapter = 0, level = tutLevel },
                new GameOff.Data.RequestEntry { id = "w008-gate-b", chapter = 0, level = tutLevel },
                new GameOff.Data.RequestEntry { id = "w008-gate-c", chapter = 1, level = nextLevel },
            };
            var store = new ProgressStore();
            store.Clear(new[] { "w008-gate-a", "w008-gate-b", "w008-gate-c" });
            var board = new RequestBoard(list, store);
            Check("gate: chapter 1 closed at start; tutorial entries are tutorial, chapter 1 entry is not", !board.ChapterOpen(1) && board.IsTutorial(0) && board.IsTutorial(1) && !board.IsTutorial(2));
            store.SetCleared("w008-gate-a");
            Check("gate: only 0-1 cleared (no stars) -> 0-2 playable, chapter 1 still closed", board.IsCleared(0) && board.State(0) == RequestState.Clear && board.IsPlayable(1) && !board.ChapterOpen(1) && board.Best(0) == 0);
            store.SetCleared("w008-gate-b");
            Check("gate: 0-1 and 0-2 cleared (no stars) -> chapter 1 opens", board.ChapterOpen(1) && board.IsPlayable(2));
            store.Clear(new[] { "w008-gate-a", "w008-gate-b", "w008-gate-c" });
            Check("gate: clearing progress closes chapter 1 again", !board.ChapterOpen(1));
            UnityEngine.Object.DestroyImmediate(list);
            UnityEngine.Object.DestroyImmediate(tutLevel);
            UnityEngine.Object.DestroyImmediate(nextLevel);
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

        static void Note(string text)
        {
            results.Add("INFO " + text);
            Debug.Log("[SMOKE] INFO " + text);
        }

        /// <summary>
        /// W-009 성능(Editor Play, 0-1 플레이 중). 평균 프레임 시간(Time.unscaledDeltaTime, 수직 동기 영향 포함)과
        /// 한 장면 그리기(GameRenderer.Draw + Apply)의 CPU 제출 시간·GPU까지 포함한 시간을 잰다.
        /// </summary>
        static IEnumerator Perf()
        {
            yield return Frames(20);
            float sum = 0f, max = 0f;
            const int frames = 300;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                sum += dt;
                if (dt > max) max = dt;
            }
            Note("0-1 play, " + frames + " frames: avg frame " + (sum / frames * 1000f).ToString("0.00") + " ms (max " + (max * 1000f).ToString("0.0") + " ms)");
            const int draws = 200;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < draws; i++) { view.Renderer.Draw(view); view.Canvas.Apply(); }
            double cpuMs = sw.Elapsed.TotalMilliseconds / draws;
            view.Canvas.RegionHash(0, 0, 2, 2);   // GPU 끝날 때까지 기다림(읽기)
            double allMs = sw.Elapsed.TotalMilliseconds / draws;
            Note("one scene draw (GameRenderer.Draw + Apply) x" + draws + ": CPU submit " + cpuMs.ToString("0.000") + " ms, incl. GPU wait " + allMs.ToString("0.000") + " ms per draw");
        }

        static IEnumerator Run()
        {
            yield return null;
            ClearProgress();
            PureChecks();
            Ch0PureChecks();
            Check("start: Main phase", view.CurrentPhase == GameView.Phase.Main);
            yield return Frames(3);
            CaptureW9("01-main");
            yield return Frames(1);

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
            Check("folder names CH0_HOME / CH1_??? / CH2_???", RequestBoard.ChapterName(0) == "CH0_HOME" && RequestBoard.ChapterName(1) == "CH1_???" && RequestBoard.ChapterName(2) == "CH2_???");
            Check("request list has only chapter 0 requests 0-1 and 0-2 (test level removed)", view.Desk.Count == 2 && view.Desk.Number(0) == "0-1" && view.Desk.Number(1) == "0-2"
                && view.Desk.Entry(0).chapter == 0 && view.Desk.Entry(1).chapter == 0);
            yield return Frames(3);
            Capture("01-folder-screen");
            CaptureW9("02-desktop");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            // 잠긴 폴더: 패널에 LOCKED, Enter 무시, 열린 폴더로 돌아오면 패널 닫힘
            yield return Press("Down");
            Check("Down selects CH1 folder", view.FolderIndex == 1 && !view.InFolder);
            yield return Press("Enter");
            Check("Enter on locked folder opens LOCKED panel", view.PanelOpen && !view.InFolder);
            yield return WaitUntil(() => view.PanelSettled, "panel settled (locked folder)");
            yield return Frames(2);
            Capture("02-folder-locked-panel");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
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
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
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

            // ===== 의뢰 0-1 NIGHT INTRUDER =====
            yield return Press("Enter");
            Check("Enter opens detail panel for REQ_0-1", view.PanelOpen);
            yield return WaitUntil(() => view.PanelSettled, "panel settled (0-1)");
            yield return Frames(2);
            var e0 = view.Desk.Entry(0);
            var e1 = view.Desk.Entry(1);
            Check("0-1 entry: NIGHT INTRUDER / HOME SYSTEM / summary / mission names", e0.title == "NIGHT INTRUDER" && e0.client == "HOME SYSTEM"
                && e0.summary == "SOMEONE BROKE IN.\nKEEP THEM AWAY FROM YOU.\nTRAP THEM BELOW."
                && e0.missionNames.Length == 1 && e0.missionNames[0] == "TRAP THE INTRUDER");
            Check("0-2 entry: LOCKDOWN / HOME SYSTEM / summary / mission names", e1.title == "LOCKDOWN" && e1.client == "HOME SYSTEM"
                && e1.summary == "THEY GOT OUT.\nSTOP THE ELEVATOR.\nSEND THEM TO THE BOILER ROOM."
                && e1.missionNames.Length == 1 && e1.missionNames[0] == "TRAP THE INTRUDER");
            Capture("04-panel-0-1");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            yield return Press("Enter");
            Check("Enter on panel -> Connecting", view.CurrentPhase == GameView.Phase.Connecting);
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "Briefing 0-1");
            Check("0-1 briefing: SYSTEM line on LOG tab, face surprise", view.CurrentTab == GameView.Tab.Log && view.Shown.Count == 1
                && view.Shown[0].speaker == GameOff.Data.Speaker.Client && view.Shown[0].speakerName == "SYSTEM" && view.Shown[0].text == "1F INTRUDER DETECTED." && view.FaceKey == "surprise");
            yield return Press("Enter");
            Check("0-1 briefing line 2 (ME: NOT TONIGHT., face anger)", view.Shown.Count == 2 && view.Shown[1].speaker == GameOff.Data.Speaker.Me && view.Shown[1].text == "NOT TONIGHT." && view.FaceKey == "anger");
            yield return Frames(3);
            Capture("05-briefing-0-1");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing, "Playing 0-1");
            Check("briefing end -> Playing on MISSION tab", view.CurrentTab == GameView.Tab.Mission);
            yield return Frames(3);
            Check("0-1 tutorial step 1: SHIFT badge, 'THE INTRUDER HEADS FOR THE STAIRS...'", TutIs(0, "SHIFT", "THE INTRUDER HEADS FOR THE STAIRS. HOLD SHIFT TO SEAL THEM."));
            Check("0-1 start: floor 1, col 1 x30 (map x190) facing right, aim = stairs sector (3,1)", Sim.TargetFloor == 1 && Mathf.Approximately(Sim.TargetX, 190f) && Sim.TargetFacingRight && Sim.Aim == P(3, 1));
            Check("0-1 header building name HOME BLDG, map 640x344", Sim.Board.BuildingName == "HOME BLDG" && Sim.Board.MapW == 640 && Sim.Board.MapH == 344);
            Check("art set has runner / hatch / hatch blueprint", view.Canvas.Has("sprites/runner_right") && view.Canvas.Has("sprites/runner_left") && view.Canvas.Has("objects/hatch_closed")
                && view.Canvas.Has("objects/hatch_open") && view.Canvas.Has("objects/bp_hatch_open") && view.Canvas.Has("scenes/ch0_home1_base") && view.Canvas.Has("scenes/ch0_home2_base"));
            Check("0-1 MISSION tab: no RATING line (rows below the single mission are blank like each other)",
                Sim.Board.Tutorial && Sim.MissionCount == 1 && view.Canvas.RegionHash(444, 114, 70, 9) == view.Canvas.RegionHash(444, 90, 70, 9));
            Capture("20-0-1-start-step1");
            // W-009: 조력자 시트(걷기·기다림·떨어짐 + 좌우)와 4배 해상도 확인
            Check("W-009 runner128 sheets present: walk/wait/fall x right/left; frames 16/24/10 (D-077, 30 fps)",
                view.Canvas.FrameCount("anim/runner128_walk_sheet") == 16 && view.Canvas.FrameCount("anim/runner128_wait_sheet") == 24 && view.Canvas.FrameCount("anim/runner128_fall_sheet") == 10
                && view.Canvas.FrameCount("anim/runner128_walk_left_sheet") == 16 && view.Canvas.FrameCount("anim/runner128_wait_left_sheet") == 24 && view.Canvas.FrameCount("anim/runner128_fall_left_sheet") == 10);
            Check("W-009 sheet textures not downscaled (wait sheet 3072 px = 768 logical), fall does not loop, walk loops",
                view.Canvas.WidthOf("anim/runner128_wait_sheet") == 768 && !view.Canvas.Loops("anim/runner128_fall_sheet") && view.Canvas.Loops("anim/runner128_walk_sheet"));
            Check("W-009 canvas output is a 2560x1440 RenderTexture (logical 640x360 x4)", view.Canvas.Output.width == 2560 && view.Canvas.Output.height == 1440);
            Check("W-009 holds/fps respected: walk 16 ticks at 30 fps = 0.533 s, frame at 0.0 s is 0 and at 0.5 s is 15",
                Mathf.Abs(view.Canvas.SheetSeconds("anim/runner128_walk_sheet") - 16f / 30f) < 0.001f && view.Canvas.FrameAt("anim/runner128_walk_sheet", 0f) == 0 && view.Canvas.FrameAt("anim/runner128_walk_sheet", 0.5f) == 15
                && view.Canvas.FrameAt("anim/runner128_fall_sheet", 5f) == 9);
            Check("W-009 0-1 before the start delay: judgement not running yet, runner shows the wait animation (right-facing sheet)",
                !Sim.TargetMoving && view.Renderer.RunnerAnim == "wait" && view.Renderer.RunnerSheet == "anim/runner128_wait_sheet");
            yield return WaitUntil(() => Sim.TargetMoving, "runner starts walking (start delay 3 s)", 600);
            yield return Jump(Dir.Left, 2, 1);   // 카메라가 조준한 섹터를 따라가므로, 걷는 모습이 장면에 들어오게 해치 섹터로 조준을 옮긴다
            yield return WaitUntil(() => Sim.TargetX >= 300f, "runner walks into the camera view", 600);
            yield return Frames(3);
            Check("W-009 0-1 runner walks: walk animation, right-facing sheet, wait -> walk switched without delaying the judgement",
                Sim.TargetMoving && view.Renderer.RunnerAnim == "walk" && view.Renderer.RunnerSheet == "anim/runner128_walk_sheet");
            CaptureW9("04-0-1-walk");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            // 계단을 열어 둔 채 두면: 2층 진입 -> 실패 지점 -> 바로 다시 시작
            Advance(() => Sim.TargetFloor == 0, 40f);
            yield return Frames(3);
            Check("0-1 runner climbed the open stairs to 2F: no mission failed, still running", Sim.TargetFloor == 0 && !Sim.MissionFailed(0) && !Sim.MissionDone(0) && Sim.Outcome == Outcome.Running);
            Capture("21-0-1-2f-climbed");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            Advance(() => Sim.Outcome == Outcome.Failed, 30f);
            yield return Frames(3);
            double tNotice = EditorApplication.timeSinceStartup;
            Check("0-1 fail point: restarts at once - Playing, no result window, no briefing (log kept), floor 1 x190, aim (3,1), missions reset",
                view.CurrentPhase == GameView.Phase.Playing && Sim.Outcome == Outcome.Running && Sim.TargetFloor == 1 && Mathf.Approximately(Sim.TargetX, 190f) && Sim.Aim == P(3, 1)
                && !Sim.MissionFailed(0) && view.Shown.Count == 2 && view.CurrentTab == GameView.Tab.Mission);
            Check("0-1 restart notice in the guide box: 'THEY REACHED YOU. TRY AGAIN.' (no badge)", view.Tut.NoticeActive && view.Tut.Text(Sim) == "THEY REACHED YOU. TRY AGAIN." && view.Tut.Key(Sim) == null);
            Capture("22-0-1-restart-notice");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            yield return WaitUntil(() => !view.Tut.NoticeActive, "restart notice ends", 900);
            double noticeSeconds = EditorApplication.timeSinceStartup - tNotice;
            Check("0-1 restart notice lasts about 2 s (measured from 3 frames after the restart: " + noticeSeconds.ToString("0.00") + " s) and then step 1 returns",
                noticeSeconds > 1.3 && noticeSeconds < 3.5 && TutIs(0, "SHIFT", "THE INTRUDER HEADS FOR THE STAIRS. HOLD SHIFT TO SEAL THEM."));

            // 돌아오는 흐름
            Advance(() => Sim.TargetX >= 520f, 40f);
            shift = true;
            yield return Frames(3);
            {
                SectorObject stObj = FindObj(Sim.Board, GameOff.Data.ObjectKind.Stairs, 1, 3);
                Check("0-1 stairs Shift overlay: blueprint state is closed -> overlay art is bp_shutter_closed (art present, not bp_shutter_open)",
                    stObj != null && !stObj.Def.blueprintOn && view.Canvas.Has("objects/bp_shutter_closed") && Sim.IsBlueprint(1, 3));
            }
            Capture("40-0-1-stairs-shift-overlay");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            Advance(() => Sim.TargetX >= 560f, 10f);
            Check("0-1 Shift on the stairs sector seals them -> runner passes by on floor 1, tracking rose", Sim.TargetFloor == 1 && Sim.TargetX >= 560f && Sim.Tracking > 0f && Sim.Outcome == Outcome.Running);
            yield return Frames(2);
            Check("step still 1 while Shift is held", view.Tut.Index == 0);
            shift = false;
            yield return Frames(3);
            Check("0-1 tutorial step 2 after Shift released: 'IT ONLY HOLDS WHILE YOU PRESS.' (no badge)", TutIs(1, null, "IT ONLY HOLDS WHILE YOU PRESS."));
            Capture("23-0-1-step2");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            Advance(() => !Sim.TargetFacingRight, 30f);
            yield return Frames(3);
            Check("0-1 tutorial step 3 when the runner turns back toward the hatch: arrow badge ←", TutIs(2, "\u2190", "PRESS \u2190 TO AIM AT THE HATCH."));
            Check("W-009 left-facing runner uses the walk_left sheet", Sim.TargetMoving && view.Renderer.RunnerAnim == "walk" && view.Renderer.RunnerSheet == "anim/runner128_walk_left_sheet");
            Capture("24-0-1-step3-left-arrow");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            Advance(() => Sim.TargetX <= 560f, 10f);
            shift = true;
            Advance(() => Sim.TargetX <= 515f, 10f);
            shift = false;
            Check("0-1 stairs sealed again on the way back (floor 1)", Sim.TargetFloor == 1 && Sim.TargetX <= 515f);
            Advance(() => Sim.TargetX <= 100f, 20f);
            Advance(() => Sim.TargetFacingRight && Sim.TargetX >= 150f, 20f);
            yield return Jump(Dir.Left, 2, 1);
            yield return Frames(3);
            Check("0-1 tutorial step 4 when the aim reaches the hatch sector: SHIFT badge, 'HOLD SHIFT TO DROP THEM.'", TutIs(3, "SHIFT", "HOLD SHIFT TO DROP THEM."));
            Capture("25-0-1-step4-aim-hatch");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            Advance(() => Sim.TargetX >= 385f, 20f);
            shift = true;
            Advance(() => Sim.Outcome != Outcome.Running, 5f);
            shift = false;
            Check("0-1 hatch opened -> falls to B1 col 2 -> Complete, tracking below max",
                Sim.Outcome == Outcome.Complete && Sim.TargetFloor == 2 && Sim.MissionFinal && Sim.Tracking < 100f);
            {
                // W-009: 판정은 이미 끝났고(B1 도착, Complete), 떨어짐 그림은 그 뒤를 따라가며 한 번만 재생된다
                int played0 = view.Renderer.FallsPlayed;
                yield return Frames(2);
                Check("W-009 hatch fall: judgement already done (B1, Complete) while the fall animation starts", Sim.TargetFloor == 2 && Sim.Outcome == Outcome.Complete && view.Renderer.RunnerAnim == "fall");
                yield return WaitUntil(() => view.Renderer.FallsPlayed > played0, "fall animation finishes", 300);
                yield return Frames(30);
                Check("W-009 hatch fall played exactly once, then back to wait (runner no longer moving)", view.Renderer.FallsPlayed == played0 + 1 && view.Renderer.RunnerAnim == "wait");
            }
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending, "ending 0-1");
            Check("0-1 ending line 1 (???: HEY! WHO LOCKED THIS?!, face neutral)", view.Shown.Count == 3 && view.Shown[2].speakerName == "???" && view.Shown[2].text == "HEY! WHO LOCKED THIS?!"
                && view.CurrentTab == GameView.Tab.Log && view.FaceKey == "neutral");
            yield return Frames(3);
            Capture("26-0-1-ending");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            yield return Press("Enter");
            Check("0-1 ending line 2 (ME: STAY THERE.)", view.Shown.Count == 4 && view.Shown[3].speaker == GameOff.Data.Speaker.Me && view.Shown[3].text == "STAY THERE." && view.FaceKey == "neutral");
            yield return Press("Enter");
            Check("ending end -> result (Ended), tutorial result shows CLEAR (no stars), no hint", view.CurrentPhase == GameView.Phase.Ended && Sim.Board.Tutorial && view.ResultHint == null);
            yield return Frames(5);
            Capture("27-0-1-result-clear");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            // 결과 Enter -> 그 챕터의 의뢰 목록, 0-1 클리어 후 0-2 열림
            yield return Press("Enter");
            Check("result Enter -> request list of CH0 (folder open, REQ_0-1 selected, panel closed)", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder && view.FolderIndex == 0 && view.DeskIndex == 0 && !view.PanelOpen);
            Check("0-1 cleared: no stars saved (best 0), cleared flag set, REQ_0-1 CLEAR", view.Desk.Best(0) == 0 && view.Desk.IsCleared(0) && view.Desk.IsTutorial(0) && view.Desk.State(0) == RequestState.Clear);
            Check("CH1 stays locked while REQ_0-2 is not cleared", !view.Desk.ChapterOpen(1));
            Check("0-1 cleared -> REQ_0-2 opens (playable, NEW)", view.Desk.IsUnlocked(1) && view.Desk.IsPlayable(1) && view.Desk.State(1) == RequestState.New);
            Check("CH1 / CH2 have no requests yet (locked)", !view.Desk.ChapterOpen(1) && !view.Desk.ChapterOpen(2));
            yield return Frames(3);
            Capture("28-request-list-after-clear");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            // ===== 의뢰 0-2 LOCKDOWN =====
            yield return Press("Down");
            Check("Down selects REQ_0-2", view.DeskIndex == 1);
            yield return OpenAndConnect();
            Check("0-2 briefing log: ??? line + ME line", view.Shown.Count == 2 && view.Shown[0].speakerName == "???" && view.Shown[0].text == "I'M OUT. WHOEVER YOU ARE, I'M NOT STAYING."
                && view.Shown[1].speaker == GameOff.Data.Speaker.Me && view.Shown[1].text == "WE'LL SEE.");
            Check("0-2 playing, tutorial step 1 (no badge): 'THE ELEVATOR GOES UP TO YOU...'", view.CurrentPhase == GameView.Phase.Playing && TutIs(0, null, "THE ELEVATOR GOES UP TO YOU. HOLD SHIFT TO STOP IT."));
            Check("0-2 start: floor 1 x 30 facing right, aim = elevator sector (1,1), header HOME BLDG", Sim.TargetFloor == 1 && Mathf.Approximately(Sim.TargetX, 30f) && Sim.TargetFacingRight && Sim.Aim == P(1, 1) && Sim.Board.BuildingName == "HOME BLDG");
            yield return Frames(3);
            Check("0-2 MISSION tab: single mission, no RATING line", Sim.Board.Tutorial && Sim.MissionCount == 1 && view.Canvas.RegionHash(444, 114, 70, 9) == view.Canvas.RegionHash(444, 90, 70, 9));
            Capture("29-0-2-start-step1");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            Advance(() => Sim.TargetX >= 215f, 30f);
            shift = true;
            yield return Frames(3);
            {
                SectorObject elObj = FindObj(Sim.Board, GameOff.Data.ObjectKind.Elevator, 1, 1);
                Check("0-2 elevator Shift overlay: blueprint state is stopped -> overlay art is bp_elevator_stopped (art present)",
                    elObj != null && !elObj.Def.blueprintOn && view.Canvas.Has("objects/bp_elevator_stopped") && Sim.IsBlueprint(1, 1));
            }
            Capture("41-0-2-elevator-shift-overlay");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            Advance(() => Sim.TargetX >= 260f, 10f);
            shift = false;
            Check("0-2 Shift on the elevator sector stops it -> runner passes by on floor 1", Sim.TargetFloor == 1 && Sim.TargetX >= 260f);
            Advance(() => Sim.TargetWaiting, 20f);
            yield return Frames(3);
            Check("0-2 waits before the laser (x~311); step 2 'SOME ROOMS SHIFT AS A WHOLE...' (no badge)", Sim.TargetWaiting && Mathf.Abs(Sim.TargetX - 311f) < 0.5f && TutIs(1, null, "SOME ROOMS SHIFT AS A WHOLE. AIM AT THE ROOM."));
            Check("W-009 0-2 waiting before the laser: wait animation (not walk)", Sim.TargetWaiting && !Sim.TargetMoving && view.Renderer.RunnerAnim == "wait" && view.Renderer.RunnerSheet == "anim/runner128_wait_sheet");
            Capture("31-0-2-laser-front");
            CaptureW9("03-0-2-wait-laser");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            yield return Jump(Dir.Right, 2, 1);
            shift = true;
            Advance(() => Sim.TargetX > 330f, 10f);
            shift = false;
            Check("0-2 Shift on the laser sector turns it off -> runner enters", Sim.TargetX > 330f && Sim.TargetFloor == 1);
            yield return Frames(3);
            Check("W-009 0-2 wait -> walk again after the laser turns off", Sim.TargetMoving && view.Renderer.RunnerAnim == "walk");
            float trBefore = Sim.Tracking;
            Advance(() => Sim.RelayUsed, 10f);
            yield return Frames(3);
            Check("0-2 relay used: tracking lowered; step 3 'RELAYS LOWER YOUR TRACE.'", Sim.RelayUsed && Sim.Tracking < trBefore && TutIs(2, null, "RELAYS LOWER YOUR TRACE."));
            yield return Jump(Dir.Right, 3, 1);
            Advance(() => Sim.TargetX >= 540f, 10f);
            shift = true;
            Advance(() => Sim.Outcome != Outcome.Running, 5f);
            shift = false;
            Check("0-2 hatch opened -> B1 boiler room -> Complete",
                Sim.Outcome == Outcome.Complete && Sim.TargetFloor == 2 && Sim.MissionFinal);
            yield return ToEnded();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "Ended 0-2");
            Check("0-2 ending dialogue shown (??? / ME / ME / ???)", view.Shown.Count == 6 && view.Shown[2].speakerName == "???" && view.Shown[2].text == "OK OK. I JUST NEEDED A PLACE TO HIDE."
                && view.Shown[3].text == "YOUR FACE IS ON MY CAMERA." && view.Shown[4].text == "WORK FOR ME. I ERASE YOUR RECORD." && view.Shown[5].speakerName == "???" && view.Shown[5].text == "... DEAL.");
            Check("0-2 result has no hint line, no stars saved", view.ResultHint == null && view.Desk.Best(1) == 0);
            yield return Frames(5);
            Capture("32-0-2-result-clear");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            yield return Press("Enter");
            Check("result Enter -> request list of CH0 (REQ_0-2 selected)", view.InFolder && view.DeskIndex == 1 && view.CurrentPhase == GameView.Phase.Desktop);
            Check("both 0-1 and 0-2 cleared -> CH1 opens only if it has requests (no stars needed); cleared flags saved", view.Desk.IsCleared(0) && view.Desk.IsCleared(1) && !view.Desk.ChapterOpen(1));

            // ===== 챕터 0 추적도 최대치: 게임오버 없음 + 안내 상자 경고 =====
            yield return Press("Up");
            Check("Up selects REQ_0-1", view.DeskIndex == 0);
            yield return OpenAndConnect();
            Check("0-1 replay -> Playing, tracking 0, step 1", view.CurrentPhase == GameView.Phase.Playing && Sim.Tracking == 0f && TutIs(0, "SHIFT", "THE INTRUDER HEADS FOR THE STAIRS. HOLD SHIFT TO SEAL THEM."));
            shift = true;
            Advance(() => Sim.TraceMaxed, 20f);
            Check("tracking reaches max", Sim.TraceMaxed && Sim.Tracking >= 100f);
            yield return Frames(3);
            Check("tracking max: warning text replaces the step text, no badge", view.Tut.Text(Sim) == "TRACE MAX - SANDBOX ONLY." && view.Tut.Key(Sim) == null);
            Check("tracking max: face surprise (no countdown face)", view.FaceKey == "surprise");
            Capture("17-trace-max");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
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
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
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
            Check("pause menu Restart resets (tracking, missions, target, tutorial step)", view.CurrentPhase == GameView.Phase.Playing && Sim.Tracking == 0f && !Sim.MissionDone(0) && Sim.TargetFloor == 1
                && Mathf.Approximately(Sim.TargetX, 190f) && !Sim.ShiftReleasedOnce && view.Tut.Index == 0);
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
            yield return Perf();

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
