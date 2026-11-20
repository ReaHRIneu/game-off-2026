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
using Dt = GameOff.Data;

namespace GameOff.EditorTools
{
    /// <summary>
    /// MVP-1 작동 확인(작동 확인만, 통합 QA 아님). Play 모드에서 입력을 덮어써 테스트 의뢰를 자동으로 돌리고
    /// 결과를 Unity/Logs/w019-smoke.txt에 쓴다(W-019부터, 이전 이름 w011-smoke.txt, 이전 이름 w017-smoke.txt·w015-smoke.txt·w013-smoke.txt). W-011 확인은 W011Checks이며 캡처는 Docs/QA/captures/W-011/. 캡처는 Docs/QA/captures/W-012/에 저장한다(1920x1080 6장: 0-1 시작·계단 설계도·해치 설계도, 0-2 시작·레이저 앞·엘리베이터 설계도).
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
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w019-smoke.txt"));
            File.WriteAllLines(path, results);
            ClearProgress();
            RestorePrefs();   // W-019: 사용자 PC의 설정·기록 값을 체크 시작 전 값으로 되돌린다
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

        // ----- W-012: 오브젝트 128px 그림 확인 -----

        /// <summary>objects128/ 그림 15장이 모두 반입돼 있고 논리 크기가 지시서와 같은지(1:1 4배 그림 규칙 확인).</summary>
        static void CheckObjects128Art()
        {
            var sizes = new Dictionary<string, Vector2Int>
            {
                { "shutter_open", P(40, 70) }, { "shutter_closed", P(40, 70) }, { "hatch_open", P(40, 10) }, { "hatch_closed", P(40, 10) },
                { "elevator_running", P(56, 72) }, { "elevator_stopped", P(56, 72) }, { "blocker_on", P(160, 96) }, { "relay_box", P(16, 35) },
                { "bp_shutter_open", P(42, 72) }, { "bp_shutter_closed", P(42, 72) }, { "bp_hatch_open", P(42, 12) }, { "bp_hatch_closed", P(42, 12) },
                { "bp_elevator_running", P(58, 74) }, { "bp_elevator_stopped", P(58, 74) }, { "bp_sector", P(160, 96) },
            };
            int ok = 0;
            string bad = "";
            foreach (var kv in sizes)
            {
                string key = "objects128/" + kv.Key;
                if (view.Canvas.Has(key) && view.Canvas.WidthOf(key) == kv.Value.x && view.Canvas.HeightOf(key) == kv.Value.y) ok++;
                else bad += " " + kv.Key;
            }
            Check("W-012 objects128: " + sizes.Count + " pictures in the art set at 4x size (logical sizes match 1:1)" + (bad.Length > 0 ? " - bad:" + bad : ""), ok == sizes.Count);
        }

        /// <summary>지금 의뢰에서 그리는 모든 오브젝트·차단막·설계도 겹침 그림 키가 objects128/로 해석되는지.</summary>
        static void CheckObjectKeys128(string where)
        {
            int total = 0, hi = 0;
            string bad = "";
            foreach (SectorObject o in Sim.Board.Objects)
            {
                if (!o.Def.drawSprite) continue;
                string k = view.Renderer.ResolveObjectKey(o);
                if (k == null) continue;
                total++;
                if (k.StartsWith("objects128/")) hi++; else bad += " " + k;
                string bk = view.Renderer.ResolveBlueprintKey(o);
                if (bk == null) continue;
                total++;
                if (bk.StartsWith("objects128/")) hi++; else bad += " " + bk;
            }
            bool anyWhole = false;
            for (int f = 0; f < Sim.Board.Floors; f++)
                for (int c = 0; c < Sim.Board.Cols; c++)
                    if (Sim.Board.IsWhole(f, c)) anyWhole = true;
            if (anyWhole)
            {
                total++;
                if (view.Renderer.ResolveBlockerKey().StartsWith("objects128/")) hi++; else bad += " blocker";
            }
            total++;
            if (view.Renderer.ResolveBpSectorKey().StartsWith("objects128/")) hi++; else bad += " bp_sector";
            Check("W-012 " + where + ": every drawn object / blueprint / blocker / bp_sector key resolves to objects128/ (" + hi + "/" + total + ")" + (bad.Length > 0 ? " - old:" + bad : ""), hi == total && total > 0);
        }

        static string CaptureDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-012"));

        /// <summary>W-009부터 장면마다 캡처하지 않는다(요구 캡처 4종은 CaptureW9). 호출은 남겨 두어 시나리오 위치를 표시한다.</summary>
        static void Capture(string name)
        {
        }

        /// <summary>
        /// W-010 캡처: 캔버스 출력(2560x1440 RenderTexture)을 Game 뷰와 같은 레터박스·Bilinear 방식으로 1920x1080 PNG 하나로 저장한다.
        /// </summary>
        static void CaptureW9(string name)
        {
            Directory.CreateDirectory(CaptureDir);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir, name + "_1920x1080.png"), 1920, 1080);
        }

        // ----- W-019: PlayerPrefs 보존 -----
        static readonly Dictionary<string, int?> savedPrefs = new Dictionary<string, int?>();

        static List<string> PrefKeys()
        {
            var keys = new List<string> { ProgressStore.EnvReducedKey, ProgressStore.BgmKey, ProgressStore.SfxKey, ProgressStore.GlitchKey };
            if (view != null)
                for (int i = 0; i < view.Desk.Count; i++)
                {
                    string id = view.Desk.Entry(i).id;
                    keys.Add("gameoff.best." + id);
                    keys.Add("gameoff.cleared." + id);
                }
            return keys;
        }

        static void SnapshotPrefs()
        {
            savedPrefs.Clear();
            foreach (string k in PrefKeys()) savedPrefs[k] = PlayerPrefs.HasKey(k) ? (int?)PlayerPrefs.GetInt(k) : null;
        }

        static void RestorePrefs()
        {
            foreach (var kv in savedPrefs)
            {
                if (kv.Value.HasValue) PlayerPrefs.SetInt(kv.Key, kv.Value.Value);
                else PlayerPrefs.DeleteKey(kv.Key);
            }
            PlayerPrefs.Save();
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
            else if (which == "Left") next.Left = true;
            else if (which == "Right") next.Right = true;
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
            /// <summary>Shift를 누른 채 진행한 1/60초 단계 수(W-014: Shift 총 시간)와 지금까지의 최대 추적도.</summary>
            public int ShiftSteps;
            public float Peak;
            public float ShiftSeconds => ShiftSteps / 60f;

            public Rig(string levelPath = "Assets/_Project/Data/TestRequest.asset") : this(AssetDatabase.LoadAssetAtPath<GameOff.Data.LevelData>(levelPath))
            {
            }

            public Rig(GameOff.Data.LevelData level)
            {
                var settings = AssetDatabase.LoadAssetAtPath<GameOff.Data.GameSettings>("Assets/_Project/Data/GameSettings.asset");
                S = new GameSim(settings, new Board(level));
            }

            /// <summary>Shift를 누르고(없으면 시작) 방향키 한 번: 방 밀기 시도(W-014). 성공하면 true.</summary>
            public bool Push(int dx, int dy)
            {
                Shift = true;
                S.ShiftHeld = true;
                return S.TryPush(dx, dy);
            }

            /// <summary>Shift를 뗀다(밀린 방이 있으면 제자리로 돌아온다).</summary>
            public void Release()
            {
                Shift = false;
                S.ShiftHeld = false;
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
                    if (Shift) ShiftSteps++;
                    if (S.Tracking > Peak) Peak = S.Tracking;
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
                    if (Shift) ShiftSteps++;
                    if (S.Tracking > Peak) Peak = S.Tracking;
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
            LayoutPureChecks();
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

        /// <summary>레이아웃에 나온 그림 키가 모두 화면 그림 모음(ArtSet)에 있는가.</summary>
        /// <summary>W-015: 배치의 방 키가 모두 구시가지(ot_)로 시작하는지.</summary>
        static bool OldTownRoomsOnly(SceneLayout lay)
        {
            if (lay == null) return false;
            for (int f = 0; f < lay.Floors; f++)
                for (int c = 0; c < lay.Cols; c++)
                {
                    SceneLayout.SectorView v = lay.Sector(f, c);
                    if (v != null && v.Room != null && !v.Room.Name.StartsWith("ot_")) return false;
                }
            return true;
        }

        static bool LayoutArtOk(SceneLayout lay)
        {
            foreach (string k in lay.AllKeys)
                if (!view.Canvas.Has(k)) return false;
            return true;
        }

        /// <summary>W-010: 두 맵 배치 읽기, 바닥선·실패 지점 데이터, 조명 상태 API.</summary>
        static void LayoutPureChecks()
        {
            string[] paths = { Home1Path, Home2Path };
            string[] keys = { "scenes128/ch0_home1/layout", "scenes128/ch0_home2/layout" };
            for (int i = 0; i < 2; i++)
            {
                var level = AssetDatabase.LoadAssetAtPath<GameOff.Data.LevelData>(paths[i]);
                var b = new Board(level);
                string n = "0-" + (i + 1);
                SceneLayout L = b.Layout;
                int rooms = 0, tiles = 0, missing = 0;
                if (L != null)
                    for (int f = 0; f < 3; f++)
                        for (int c = 0; c < 4; c++)
                        {
                            var v = L.Sector(f, c);
                            if (v == null) { missing++; continue; }
                            if (v.Room != null) { rooms++; if (string.IsNullOrEmpty(v.Room.L1) || string.IsNullOrEmpty(v.Room.L2) || string.IsNullOrEmpty(v.Room.Dark)) missing++; }
                            else { tiles++; if (string.IsNullOrEmpty(v.Tile)) missing++; }
                        }
                Check(n + " layout read: layoutKey " + keys[i] + ", 12 sectors (" + rooms + " rooms + " + tiles + " earth tiles), none missing", L != null && b.LayoutKey == keys[i] && rooms + tiles == 12 && missing == 0);
                Check(n + " layout sizes match the level: sector 640x384 px, roof 96, ground 128, 3 floors x 4 cols; 1 door prop (home L4_door at floor 0, col 0, x 568)",
                    L != null && L.SectorPxW == 640 && L.SectorPxH == 384 && L.RoofPx == 96 && L.GroundPx == 128 && L.FloorYPx == 352 && L.Floors == 3 && L.Cols == 4
                    && L.Props.Length == 1 && L.Props[0].Key == "rooms128/home/L4_door" && L.Props[0].Floor == 0 && L.Props[0].Col == 0 && L.Props[0].XPx == 568);
                Check(n + " wallThickness 8 -> floor line y = 24 + 96 - 8 = 112 (picture floor line 88 = 352 px / 4), fail point x 142",
                    level.wallThickness == 8 && b.FloorLineY(0) == 112 && b.FloorLineY(1) == 208 && Mathf.Approximately(FindObj(b, GameOff.Data.ObjectKind.FailPoint, 0, 0).AbsX, 142f));
                Check(n + " old one-piece background is not the draw path (layout present; backgroundKey kept only as fallback)", L != null && !string.IsNullOrEmpty(level.backgroundKey));
            }

            // 조명 상태 API: 레벨마다 새 Board라 상태가 처음 값(전부 on)으로 시작한다
            var b1 = new Board(AssetDatabase.LoadAssetAtPath<GameOff.Data.LevelData>(Home1Path));
            SceneLayout lay = b1.Layout;
            bool start = lay.IsLightOn(0, 0, "desk_lamp_on") && lay.IsLightOn(0, 0, "monitors_on") && !lay.IsDark(0, 0);
            bool offOne = lay.SetLight(0, 0, "desk_lamp_on", false) && !lay.IsLightOn(0, 0, "desk_lamp_on") && !lay.IsDark(0, 0);
            bool offAll = lay.SetLight(0, 0, "monitors_on", false) && lay.IsDark(0, 0) && lay.Sector(0, 0).OnCount == 0;
            bool otherUntouched = !lay.IsDark(0, 1) && lay.IsLightOn(0, 1, "fluorescent_a_on");
            bool onAgain = lay.SetLight(0, 0, "monitors_on", true) && !lay.IsDark(0, 0) && lay.Sector(0, 0).OnCount == 1;
            bool badArgs = !lay.SetLight(0, 0, "no_such_light", false) && !lay.SetLight(2, 0, "bulb_on", false) && !lay.IsDark(2, 0) && !lay.SetLight(5, 5, "x", false);
            Check("W-010 light API: all on at start -> one off keeps the room lit -> all off = dark state -> other sectors untouched -> one on again = lit", start && offOne && offAll && otherUntouched && onAgain);
            Check("W-010 light API: unknown light / earth tile sector / out of range return false and never count as dark", badArgs);
            var b1b = new Board(AssetDatabase.LoadAssetAtPath<GameOff.Data.LevelData>(Home1Path));
            Check("W-010 light state is per level start (a new Board starts with the lights from layout.json)", b1b.Layout.IsLightOn(0, 0, "desk_lamp_on") && b1b.Layout.IsLightOn(0, 0, "monitors_on"));
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
            Check("0-1 objects: stairs x540, hatch x400 (link B1 col 2 x80), fail point x142 (W-010, door left edge), no security terminal (W-008)",
                st != null && Mathf.Approximately(st.AbsX, 540f) && ha != null && Mathf.Approximately(ha.AbsX, 400f) && ha.Def.hasLink && ha.Def.linkFloor == 2 && ha.Def.linkCol == 2
                && te == null && fp != null && Mathf.Approximately(fp.AbsX, 142f));
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
            Check("0-1 fail point: runner center crosses x142 on 2F -> Outcome.Failed (not GameOver, not Complete)", s.Outcome == Outcome.Failed && Mathf.Approximately(s.TargetX, 142f) && s.TargetFloor == 0);

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
            Check("0-2 fail point x142 on 3F -> Outcome.Failed", s.Outcome == Outcome.Failed && Mathf.Approximately(s.TargetX, 142f) && s.TargetFloor == 0);

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
            Note("W-010 0-1 play (room layers), " + frames + " frames: avg frame " + (sum / frames * 1000f).ToString("0.00") + " ms (max " + (max * 1000f).ToString("0.0") + " ms) - W-009 value was 6.95 ms avg");
            const int draws = 200;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < draws; i++) { view.Renderer.Draw(view); view.Canvas.Apply(); }
            double cpuMs = sw.Elapsed.TotalMilliseconds / draws;
            view.Canvas.RegionHash(0, 0, 2, 2);   // GPU 끝날 때까지 기다림(읽기)
            double allMs = sw.Elapsed.TotalMilliseconds / draws;
            Note("one scene draw (GameRenderer.Draw + Apply) x" + draws + ": CPU submit " + cpuMs.ToString("0.000") + " ms, incl. GPU wait " + allMs.ToString("0.000") + " ms per draw");
        }

        // ----- W-013: 글리치 확인 -----

        static string GlitchCaptureDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-013"));

        /// <summary>글리치가 입혀진 화면 출력(Output)을 1920x1080 PNG로 저장한다.</summary>
        static void CaptureGlitch(string name)
        {
            Directory.CreateDirectory(GlitchCaptureDir);
            view.Canvas.SaveSizedPng(Path.Combine(GlitchCaptureDir, name + "_1920x1080.png"), 1920, 1080, true);
        }

        /// <summary>프레임 n개의 평균·최대 프레임 시간(ms). 수직 동기 영향 포함(Perf와 같은 방식).</summary>
        static IEnumerator MeasureFrames(string label, int n)
        {
            yield return Frames(10);
            float sum = 0f, max = 0f;
            for (int i = 0; i < n; i++)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                sum += dt;
                if (dt > max) max = dt;
            }
            Note("W-013 perf " + label + ", " + n + " frames: avg " + (sum / n * 1000f).ToString("0.00") + " ms, max " + (max * 1000f).ToString("0.0") + " ms");
        }

        /// <summary>Time.deltaTime이 seconds초 이상 쌓일 때까지 프레임을 넘긴다(글리치 시계와 같은 단위).</summary>
        static IEnumerator WaitGameSeconds(float seconds)
        {
            float t = 0f;
            for (int i = 0; i < 2000 && t < seconds; i++)
            {
                yield return null;
                t += Mathf.Min(Time.deltaTime, 0.1f);
            }
        }

        /// <summary>
        /// TestRequest(챕터 0이 아니라 게임오버가 있는 의뢰)로 게임오버까지 진행하고 결과를 기록한다.
        /// 글리치 세기 설정에 따른 게임오버 창 타이밍과 판정 값(결과·추적도·카운트다운)을 outValues에 담는다.
        /// </summary>
        static IEnumerator GameOverRun(GlitchStrength strength, int freezeTick, string captureName, double[] outValues)
        {
            Glitch.Strength = strength;
            Glitch.FreezeTick = -1;
            Glitch.Cancel();
            var level = AssetDatabase.LoadAssetAtPath<GameOff.Data.LevelData>("Assets/_Project/Data/TestRequest.asset");
            view.DebugStartLevel(level);
            yield return Frames(3);
            shift = true;
            Advance(() => Sim.Outcome != Outcome.Running, 40f);
            shift = false;
            Outcome reached = Sim.Outcome;
            double t0 = EditorApplication.timeSinceStartup;
            if (freezeTick >= 0) Glitch.FreezeTick = freezeTick;
            yield return Frames(3);
            bool glitchOn = Glitch.IsPlaying(Glitch.GameOver);
            if (captureName != null && freezeTick >= 0)
            {
                CaptureGlitch(captureName);
                Glitch.FreezeTick = -1;
            }
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "GameOver window (" + strength + ")", 600);
            outValues[0] = (int)reached;
            outValues[1] = Sim.Tracking;
            outValues[2] = Sim.Countdown;
            outValues[3] = EditorApplication.timeSinceStartup - t0;   // 게임오버 판정 뒤 창이 뜰 때까지(초)
            outValues[4] = glitchOn ? 1 : 0;
        }

        static IEnumerator GlitchChecks()
        {
            Glitch.FreezeTick = -1;
            Glitch.Strength = GlitchStrength.Normal;

            // 1) 프리셋 7개와 셰이더
            string[] ids = { "shift_in", "shift_out", "room_push", "danger", "gameover", "transition", "logo" };
            int found = 0;
            foreach (string id in ids) if (Glitch.Get(id) != null && Glitch.Get(id).TotalTicks > 0) found++;
            Check("W-013 presets: 7 exist (shift_in, shift_out, room_push, danger, gameover, transition, logo) - found " + found, found == 7);
            Check("W-013 glitch shader loaded and supported (GameOff/GlitchPost)", Glitch.Shader != null && Glitch.Shader.isSupported && Glitch.Shader.name == "GameOff/GlitchPost");
            Check("W-013 preset lengths: shift_in/out 3 ticks, danger 2, gameover 15 (0.5 s, 3 stages), transition 4, logo 2, room_push 4",
                Glitch.ShiftIn.TotalTicks == 3 && Glitch.ShiftOut.TotalTicks == 3 && Glitch.Danger.TotalTicks == 2 && Glitch.GameOver.TotalTicks == 15
                && Glitch.GameOver.stages.Length == 3 && Glitch.Transition.TotalTicks == 4 && Glitch.Logo.TotalTicks == 2 && Glitch.RoomPush.TotalTicks == 4);
            Check("W-013 canvas Output is still 2560x1440", view.Canvas.Output.width == 2560 && view.Canvas.Output.height == 1440);

            // 2) Shift 누름·뗌 -> 조준 섹터에 글리치, 0.1초 뒤 끝
            // W-016: 앞 단계(실제 키보드 Shift 뗌)의 shift_out(0.1초)이 끝날 때까지 시간 기준으로 기다린 뒤 확인한다(프레임 수 기준은 프레임 시간에 따라 흔들렸다)
            float glitchWait = 0f;
            while (Glitch.Active && glitchWait < 1f) { yield return null; glitchWait += Mathf.Min(Time.deltaTime, 0.1f); }
            Check("W-013 before Shift: playing 0-1, no glitch playing (waited " + glitchWait.ToString("0.00") + " s for the previous glitch to end)", view.CurrentPhase == GameView.Phase.Playing && !Glitch.Active);
            RectInt sector = view.AimSectorRect();
            Check("W-013 aim sector rect " + sector + " is inside the scene view (16,48,412x260)", sector.width > 0 && sector.height > 0 && sector.xMin >= 16 && sector.yMin >= 48 && sector.xMax <= 428 && sector.yMax <= 308);
            Glitch.FreezeTick = 1;   // 캡처 1: Shift 진입 2번째 프레임(틱 1)
            shift = true;
            yield return Frames(2);
            Check("W-013 Shift press -> shift_in playing", Glitch.IsPlaying(Glitch.ShiftIn));
            CaptureGlitch("01-0-1-shift-in-frame2");
            Glitch.FreezeTick = -1;
            yield return WaitGameSeconds(0.25f);
            Check("W-013 shift_in ends about 0.1 s later (inactive after 0.25 s)", !Glitch.Active);
            shift = false;
            yield return Frames(2);
            Check("W-013 Shift release -> shift_out playing", Glitch.IsPlaying(Glitch.ShiftOut));
            yield return WaitGameSeconds(0.25f);
            Check("W-013 shift_out ends about 0.1 s later", !Glitch.Active);

            // 3) 세기 설정: 끔이면 활성되지 않음, 약은 값 절반
            Glitch.Strength = GlitchStrength.Off;
            shift = true;
            yield return Frames(3);
            Check("W-013 strength Off: Shift press does not activate any glitch", !Glitch.Active);
            shift = false;
            yield return Frames(3);
            Glitch.Strength = GlitchStrength.Weak;
            shift = true;
            yield return Frames(2);
            var wf = new GlitchFrame();
            bool weakOk = Glitch.TryGetFrame(ref wf);
            Check("W-013 strength Weak: glitch active with halved values (max shift " + wf.shift.y + ", ratio " + wf.shift.z.ToString("0.00") + ")", weakOk && Glitch.IsPlaying(Glitch.ShiftIn) && wf.shift.y <= 3f);
            shift = false;
            Glitch.Strength = GlitchStrength.Normal;
            Glitch.Cancel();
            yield return Frames(2);

            // 4) 성능: 0-1 플레이 중 프레임 시간. 글리치 없을 때 / danger 켠 상태 / 화면 전체 gameover
            yield return MeasureFrames("0-1 no glitch", 300);
            Glitch.SetLoop(Glitch.Danger, true, new RectInt(16, 48, 412, 260), false);
            Glitch.DebugBurst(Glitch.Danger, 0);   // 시간을 멈춰 매 프레임 danger 모양을 그린다(실제는 2~3초마다 2틱뿐이라 이것이 최악 값)
            yield return MeasureFrames("0-1 danger every frame (worst case; real use is 2 ticks every 2-3 s)", 300);
            Glitch.FreezeTick = -1;
            Glitch.SetLoop(Glitch.Danger, false, new RectInt(16, 48, 412, 260), false);
            Glitch.Cancel();
            Glitch.FreezeTick = 7;
            Glitch.PlayFull(Glitch.GameOver);
            yield return MeasureFrames("0-1 full-screen gameover stage 2 every frame (worst case)", 300);
            Glitch.FreezeTick = -1;
            Glitch.Cancel();
            // 후처리 한 번의 비용: 글리치 없는 Apply와 글리치 있는 Apply를 200번씩(GPU 끝날 때까지 기다림)
            {
                const int n = 200;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < n; i++) view.Canvas.Apply();
                view.Canvas.RegionHash(0, 0, 2, 2);
                double none = sw.Elapsed.TotalMilliseconds / n;
                Glitch.FreezeTick = 7;
                Glitch.PlayFull(Glitch.GameOver);
                sw.Restart();
                for (int i = 0; i < n; i++) view.Canvas.Apply();
                view.Canvas.RegionHash(0, 0, 2, 2);
                double full = sw.Elapsed.TotalMilliseconds / n;
                Glitch.Cancel();
                Glitch.SetLoop(Glitch.Danger, true, new RectInt(16, 48, 412, 260), false);
                Glitch.DebugBurst(Glitch.Danger, 0);
                sw.Restart();
                for (int i = 0; i < n; i++) view.Canvas.Apply();
                view.Canvas.RegionHash(0, 0, 2, 2);
                double rect = sw.Elapsed.TotalMilliseconds / n;
                Glitch.FreezeTick = -1;
                Glitch.SetLoop(Glitch.Danger, false, new RectInt(16, 48, 412, 260), false);
                Glitch.Cancel();
                Note("W-013 glitch pass cost per Apply incl. GPU wait x" + n + ": no glitch " + none.ToString("0.000") + " ms, danger scene-view rect " + rect.ToString("0.000") + " ms, full-screen gameover " + full.ToString("0.000") + " ms");
            }

            // 5) 게임오버: 끔/보통 비교, 글리치 0.5초 뒤에 창, 캡처 3(2단계)
            var off = new double[5];
            var normal = new double[5];
            yield return GameOverRun(GlitchStrength.Off, -1, null, off);
            yield return GameOverRun(GlitchStrength.Normal, 7, "03-gameover-stage2", normal);
            Check("W-013 gameover reached with Off and Normal", (int)off[0] == (int)Outcome.GameOver && (int)normal[0] == (int)Outcome.GameOver);
            Check("W-013 judgement identical Off vs Normal: outcome, tracking (" + off[1].ToString("0.0") + " / " + normal[1].ToString("0.0") + "), countdown ("
                + off[2].ToString("0.00") + " / " + normal[2].ToString("0.00") + ")", off[0] == normal[0] && Math.Abs(off[1] - normal[1]) < 0.001 && Math.Abs(off[2] - normal[2]) < 0.001);
            Check("W-013 gameover glitch playing right after the outcome (Normal) and not playing with strength Off", normal[4] > 0.5 && off[4] < 0.5);
            Check("W-013 gameover window about 0.5 s after the outcome with Normal (" + normal[3].ToString("0.00") + " s) and at once with Off (" + off[3].ToString("0.00") + " s)",
                normal[3] > 0.4 && normal[3] < 1.2 && off[3] < 0.3);
            Glitch.Strength = GlitchStrength.Normal;

            // 6) 0-2: 추적도 70% 이상에서 danger 반복, 캡처 2
            var level02 = AssetDatabase.LoadAssetAtPath<GameOff.Data.LevelData>("Assets/_Project/Data/Ch0Home_2.asset");
            view.DebugStartLevel(level02);
            yield return Frames(30);
            Check("W-013 0-2 started, tracking 0, danger not playing", view.CurrentPhase == GameView.Phase.Playing && Sim.Tracking < 1f && !Glitch.IsPlaying(Glitch.Danger));
            shift = true;
            Advance(() => Sim.Tracking >= Sim.TrackingMax * 0.7f, 10f);
            bool dangerSeen = false;
            for (int i = 0; i < 90 && !dangerSeen; i++) { yield return null; dangerSeen = Glitch.IsPlaying(Glitch.Danger); }   // Shift 진입 글리치(0.1초)가 끝나면 바로 재생
            Check("W-013 tracking reached 70% (" + Sim.Tracking.ToString("0") + ") -> danger glitch plays", Sim.Tracking >= Sim.TrackingMax * 0.7f && dangerSeen);
            Glitch.DebugBurst(Glitch.Danger, 0);
            yield return Frames(2);
            CaptureGlitch("02-0-2-danger");
            Glitch.FreezeTick = -1;
            shift = false;
            yield return Frames(2);

            // 7) 의뢰 접속 전환(캡처 4): 일시정지 -> 바탕화면 -> 0-2 접속
            yield return Press("Esc");
            yield return Press("Down");
            yield return Press("Down");
            yield return Press("Down");   // W-019: DESKTOP은 index 3
            yield return Press("Enter");
            Check("W-013 pause DESKTOP -> request list", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder);
            yield return Frames(3);
            yield return Press("Enter");   // 패널 열기
            yield return WaitUntil(() => view.PanelSettled, "panel settled (glitch)");
            Glitch.FreezeTick = 1;
            yield return Press("Enter");   // CONNECT
            Check("W-013 CONNECT -> Connecting with transition glitch playing", view.CurrentPhase == GameView.Phase.Connecting && Glitch.IsPlaying(Glitch.Transition));
            CaptureGlitch("04-connect-transition");
            Glitch.FreezeTick = -1;
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "connect -> Briefing (glitch)");
            for (int i = 0; i < 4 && view.CurrentPhase == GameView.Phase.Briefing; i++) yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing, "briefing -> Playing (glitch)");

            // 8) 메인 로고(캡처 5): 일시정지 -> 바탕화면 -> 폴더 -> 메인
            yield return Press("Esc");
            yield return Press("Down");
            yield return Press("Down");
            yield return Press("Down");   // W-019: DESKTOP은 index 3
            yield return Press("Enter");
            yield return Press("Esc");
            yield return Press("Esc");
            Check("W-013 back on Main", view.CurrentPhase == GameView.Phase.Main);
            bool logoSeen = false;
            for (int i = 0; i < 6 && !logoSeen; i++) { yield return null; logoSeen = Glitch.IsPlaying(Glitch.Logo); }
            Check("W-013 Main screen: logo glitch loop plays", logoSeen);
            Glitch.DebugBurst(Glitch.Logo, 0);
            yield return Frames(2);
            CaptureGlitch("05-main-logo");
            Glitch.FreezeTick = -1;
            Glitch.Strength = GlitchStrength.Normal;
            Glitch.Cancel();
        }

        // ===================================================================================================
        // W-014: 방 밀기(Shift + 방향키) 규칙 R1~R11과 챕터 1 의뢰 1-1·1-2. 작동 확인만(통합 QA 아님).
        // 규칙 확인은 화면 없이 GameSim을 1/60초씩 직접 진행하고, 의뢰 진행은 같은 방식의 자동 입력으로 경로 전체를 돌린다.
        // 캡처는 Docs/QA/captures/W-015/(W-015부터, W-014 캡처 폴더는 덮어쓰지 않음). 03-01 §2-3 추적도 추정과 비교하려고 경로마다 Shift 총 시간·최종·최대 추적도를 INFO로 남긴다.
        // ===================================================================================================

        const string Old1LevelPath = "Assets/_Project/Data/Ch1Old_1.asset";
        const string Old2LevelPath = "Assets/_Project/Data/Ch1Old_2.asset";

        static string CaptureDir16 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-016"));

        /// <summary>W-016부터 W-015 캡처 폴더는 덮어쓰지 않는다(기존 캡처 호출은 시나리오 위치 표시용으로 남김). 새 캡처는 CaptureW16.</summary>
        static void CaptureW14(string name)
        {
        }

        static void CaptureW16(string name)
        {
            Directory.CreateDirectory(CaptureDir16);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir16, name + "_1920x1080.png"), 1920, 1080, true);   // 글리치가 입혀진 출력(발각 순간 확인)
        }

        static Dt.ObjectDef PushObj(Dt.ObjectKind kind, int floor, int col, float x, bool real, bool bp) =>
            new Dt.ObjectDef { kind = kind, floor = floor, col = col, x = x, drawX = x, drawSprite = false, realityOn = real, blueprintOn = bp, mission = -1, detectTrackingOverride = -1f };

        /// <summary>
        /// 규칙 확인용 레벨 P(그림 없음): 2층 x 3열, 빈 칸 (층 0, 열 1), 레일 방 B1(층 1, 열 1).
        /// B1 안: 계단 S1 x60(열림, 연결 = 층 0 열 0 x60 왼쪽), 문 x150(현실 닫힘 -> 설계도 열림). A1(층 1, 열 0): 계단 S2 x60(열림, 연결 = B1 x40 오른쪽).
        /// 미션: 0번 = 아무것도 안 닿는 최종, 1번 = 0층 금지. 시작은 호출 때 정한다.
        /// </summary>
        static Dt.LevelData MakePushLevel(int startFloor, int startCol, float startX, bool right)
        {
            var l = ScriptableObject.CreateInstance<Dt.LevelData>();
            l.floors = 2; l.cols = 3; l.sectorWidth = 160; l.sectorHeight = 96; l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.sectors = new[] { new Dt.SectorDef { floor = 1, col = 1, target = Dt.SectorTarget.Objects, rail = true } };
            l.emptyCells = new[] { new Dt.CellDef { floor = 0, col = 1 } };
            Dt.ObjectDef s1 = PushObj(Dt.ObjectKind.Stairs, 1, 1, 60, true, true);
            s1.hasLink = true; s1.linkFloor = 0; s1.linkCol = 0; s1.linkX = 60; s1.linkFacingRight = false;
            Dt.ObjectDef s2 = PushObj(Dt.ObjectKind.Stairs, 1, 0, 60, true, true);
            s2.hasLink = true; s2.linkFloor = 1; s2.linkCol = 1; s2.linkX = 40; s2.linkFacingRight = true;
            l.objects = new[] { s1, PushObj(Dt.ObjectKind.Door, 1, 1, 150, false, true), s2 };
            l.startFloor = startFloor; l.startCol = startCol; l.startX = startX; l.startFacingRight = right;
            l.hasStartAim = true; l.startAimFloor = 1; l.startAimCol = 1;
            l.missions = new[]
            {
                new Dt.MissionDef { kind = Dt.MissionKind.PassObject },
                new Dt.MissionDef { kind = Dt.MissionKind.NeverEnterFloor, floor = 0 },
            };
            return l;
        }

        /// <summary>규칙 확인용 레벨 Q: 1층 x 3열, 빈 칸 없음, 레일 방 B(열 1). 최종 미션 = 열 1 섹터 진입. 시작 = 열 0 x100 왼쪽.</summary>
        static Dt.LevelData MakeSwapLevel()
        {
            var l = ScriptableObject.CreateInstance<Dt.LevelData>();
            l.floors = 1; l.cols = 3; l.sectorWidth = 160; l.sectorHeight = 96; l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.sectors = new[] { new Dt.SectorDef { floor = 0, col = 1, target = Dt.SectorTarget.Objects, rail = true } };
            l.objects = new Dt.ObjectDef[0];
            l.startFloor = 0; l.startCol = 0; l.startX = 100; l.startFacingRight = false;
            l.hasStartAim = true; l.startAimFloor = 0; l.startAimCol = 1;
            l.missions = new[] { new Dt.MissionDef { kind = Dt.MissionKind.EnterSector, floor = 0, col = 1 } };
            return l;
        }

        static bool Near(float a, float b, float tol = 0.6f) => Mathf.Abs(a - b) <= tol;

        static void W14PureChecks()
        {
            // ----- 챕터 0 회귀: 레일이 없는 레벨은 Shift + 방향키가 아무 일도 하지 않는다 -----
            {
                var r = new Rig(Home1Path);
                GameSim s = r.S;
                Vector2Int aim0 = s.Aim;
                r.Shift = true; s.ShiftHeld = true;
                bool a = s.TryPush(1, 0), b = s.TryPush(0, -1);
                Check("W-014 chapter 0 (no rails): Shift + arrow does nothing - no push, no block event, aim and rooms unchanged, MoveAim still ignored in Shift",
                    !a && !b && s.BlockSeq == 0 && s.PushSeq == 0 && !s.Pushed && s.Aim == aim0 && !s.IsDisplaced(1, 3) && !s.MoveAim(-1, 0) && !s.Board.HasRails);
            }

            // ----- 1-1 데이터 -----
            {
                var r = new Rig(Old1LevelPath);
                GameSim s = r.S;
                Board b = s.Board;
                Check("W-014 1-1 map: 2 floors x 5 cols (map 800x248), not tutorial, rails, 3 missions (final, terminal, TRACE<=40), guide steps on (showGuide)",
                    b.Floors == 2 && b.Cols == 5 && b.MapW == 800 && b.MapH == 248 && !b.Tutorial && b.HasRails && b.Missions.Length == 3
                    && b.Missions[2].kind == Dt.MissionKind.TraceLimit && Near(b.Missions[2].limit, 40f) && b.BuildingName == "OLD TOWN A/B");
                Check("W-014 1-1 empty cells are exactly (2F col 2) and (1F col 2); rail only on the A end room (2F col 1)",
                    b.IsEmpty(0, 2) && b.IsEmpty(1, 2) && !b.IsEmpty(0, 1) && !b.IsEmpty(1, 1) && b.HasRail(0, 1) && !b.HasRail(0, 4) && !b.HasRail(1, 0));
                Check("W-014 1-1 operable (aimable) sectors: A end room (rail) and B stairs room", b.IsOperable(0, 1) && b.IsOperable(0, 4) && !b.IsOperable(1, 1));
                Check("W-014 1-1 start: 1F col 0 x20 facing right, first aim by data = start room (col 0, floor 1)", s.TargetFloor == 1 && Near(s.TargetX, 20f) && s.TargetFacingRight && s.Aim == P(0, 1));
                bool climbed = r.Until(() => s.TargetFloor == 0, 60f);
                Check("W-014 1-1 A stairs (open): runner climbs to the A end room, x80 facing right (map x240)", climbed && Near(s.TargetX, 240f) && s.TargetFacingRight);
                bool turned = r.Until(() => !s.TargetFacingRight, 30f);
                Check("W-014 R1: runner turns back at the empty cell beyond the A end room (2F col 2): x = 320 - 9 = 311", turned && Near(s.TargetX, 311f, 0.5f) && s.TargetFloor == 0);
                Rig big = new Rig(Old1LevelPath);
                big.Shift = true;
                big.Run(20f);
                Check("W-014 1-1 is not a tutorial: tracking at max starts the countdown and the request ends in GAME OVER (not suppressed)", big.S.CountdownActive && big.S.Outcome == Outcome.GameOver);
                var tut = new Tutorial(AssetDatabase.LoadAssetAtPath<Dt.LevelData>(Old1LevelPath));
                var r2 = new Rig(Old1LevelPath);
                r2.Shift = true; r2.Run(5.5f);
                Check("W-014 1-1 guide box at TRACE MAX shows the step text, not the SANDBOX warning (guide-only level)", r2.S.TraceMaxed && tut.Text(r2.S) == "SOME ROOMS SIT ON RAILS. AIM AT ONE." && !tut.Warning(r2.S));
            }

            // ----- 1-2 데이터 -----
            {
                var r = new Rig(Old2LevelPath);
                GameSim s = r.S;
                Board b = s.Board;
                Check("W-014/W-016 1-2 map: 3 floors x 5 cols (map 800x344), not tutorial, 3 missions (final, terminal, UNSEEN since W-016), no guide", b.Floors == 3 && b.Cols == 5 && b.MapW == 800 && b.MapH == 344 && !b.Tutorial
                    && b.Missions.Length == 3 && b.Missions[2].kind == Dt.MissionKind.Unseen && AssetDatabase.LoadAssetAtPath<Dt.LevelData>(Old2LevelPath).showGuide == false);
                int empties = 0;
                for (int f = 0; f < 3; f++) for (int c = 0; c < 5; c++) if (b.IsEmpty(f, c)) empties++;
                Check("W-014 1-2 empty cells: 6 ((3F 0, 2), (2F 0, 1), (1F 2, 4)); rails on the empty store (1F col 1) and the roof exit room (3F col 1); laser room is whole-sector",
                    empties == 6 && b.IsEmpty(0, 0) && b.IsEmpty(0, 2) && b.IsEmpty(1, 0) && b.IsEmpty(1, 1) && b.IsEmpty(2, 2) && b.IsEmpty(2, 4)
                    && b.HasRail(2, 1) && b.HasRail(0, 1) && b.IsWhole(1, 3) && b.Blocked(1, 3, false) && !b.Blocked(1, 3, true));
                Check("W-014 1-2 start: 1F col 0 x20 facing right, first aim = the empty store (col 1, floor 2) (nearest operable)", s.TargetFloor == 2 && Near(s.TargetX, 20f) && s.TargetFacingRight && s.Aim == P(1, 2));
            }

            // ----- 규칙 단위 (1-1·1-2 실데이터) -----
            {
                // 레일이 없는 방: 막힘, 추적도는 Shift 시간분만(R8, R9)
                var r = new Rig(Old1LevelPath);
                GameSim s = r.S;
                r.Jump(1, 0); r.Jump(1, 0);
                bool aimedStairs = s.Aim == P(4, 0);
                bool pushed = r.Push(1, 0);
                Check("W-014 R2/R8: room without rail (B stairs room): push is blocked - no move, BlockSeq 1, aim and rooms unchanged", aimedStairs && !pushed && s.BlockSeq == 1 && !s.Pushed && !s.IsDisplaced(0, 4) && s.PushSeq == 0 && s.Aim == P(4, 0));
                r.Run(0.5f);
                Check("W-014 R8/R9: tracking after a blocked push + 0.5 s Shift = 10.0 (Shift time only, no extra cost); aim still locked in Shift (R7)", Near(s.Tracking, 10f, 0.01f) && !s.MoveAim(-1, 0));
                // 맵 밖
                r = new Rig(Old1LevelPath); s = r.S;
                r.Jump(1, 0);
                bool a = s.Aim == P(1, 0), up = r.Push(0, -1);
                Check("W-014 R3: push off the map (rail room up from 2F) is blocked", a && !up && s.BlockSeq == 1 && !s.IsDisplaced(0, 1));
                // 이미 밀린 상태에서 또 누름
                bool right = r.Push(1, 0), again = r.Push(1, 0), other = r.Push(-1, 0);
                Check("W-014 R8: while already pushed, another arrow is blocked (BlockSeq +2), room stays where it is", right && !again && !other && s.BlockSeq == 3 && s.Pushed && s.CellOf(0, 1) == P(2, 0) && s.PushSeq == 1);
                r.Release();
                Check("W-014 R4: Shift released -> the pushed room is back home (not displaced, not pushed)", !s.Pushed && !s.IsDisplaced(0, 1) && s.CellOf(0, 1) == P(1, 0) && s.RoomIndexAt(0, 2) == -1 && s.RoomIndexAt(0, 1) == 1);
            }
            {
                var r = new Rig(Old2LevelPath);
                GameSim s = r.S;
                int camBefore = s.CamY;
                Check("W-014 1-2 camera starts on the empty store (floor 2): camY 84 (map bottom)", camBefore == 84);
                bool off = r.Push(0, 1);
                Check("W-014 R3: push off the map (empty store down from 1F) is blocked", !off && s.BlockSeq == 1 && !s.Pushed);
                // 빈 칸으로 밀기
                bool up = r.Push(0, -1);
                Check("W-014 R3: push into an empty cell: the empty store moves up to 2F col 1, its old cell becomes empty (no swap partner)",
                    up && s.Pushed && s.CellOf(2, 1) == P(1, 1) && s.RoomIndexAt(2, 1) == -1 && s.RoomIndexAt(1, 1) == 11 && s.AimCell == P(1, 1) && s.PushSeq == 1);
                r.Run(0.5f);
                Check("W-014 camera follows the aim room's present cell: camY 84 -> 38 after the push", s.CamY == 38);
                r.Release();
                Check("W-014 R4: release returns it; camera goes back", !s.Pushed && s.CellOf(2, 1) == P(1, 2) && s.RoomIndexAt(2, 1) == 11 && s.ReturnSeq == 1);
                // 맞바꾸기
                r = new Rig(Old2LevelPath); s = r.S;
                bool sw = r.Push(-1, 0);
                Check("W-014 R3/D-086: push into a cell with a room swaps them: store <-> shop (both change cells)",
                    sw && s.CellOf(2, 1) == P(0, 2) && s.CellOf(2, 0) == P(1, 2) && s.RoomIndexAt(2, 0) == 11 && s.RoomIndexAt(2, 1) == 10 && s.IsDisplaced(2, 0) && s.IsDisplaced(2, 1));
                r.Release();
                Check("W-014 R4: after release both swapped rooms are home", !s.IsDisplaced(2, 0) && !s.IsDisplaced(2, 1) && s.RoomIndexAt(2, 0) == 10 && s.RoomIndexAt(2, 1) == 11);
            }
            {
                // 대상 태워 옮기기
                var r = new Rig(Old2LevelPath);
                GameSim s = r.S;
                bool inRoom = r.Until(() => s.TargetFloor == 2 && s.TargetX >= 277f && s.TargetFacingRight, 90f);
                float x0 = s.TargetX;
                bool pushedRight = r.Push(1, 0);
                Check("W-014 R5: push right with the runner inside: runner rides along, x + 160, same floor, still facing right, not waiting",
                    inRoom && pushedRight && Near(s.TargetX, x0 + 160f, 0.01f) && s.TargetFloor == 2 && s.TargetFacingRight && s.TargetShiftSeq == 1 && s.TargetShiftDX == 160 && s.TargetShiftDY == 0);
                float xa = s.TargetX;
                r.Run(0.25f);
                Check("W-014 R5: the runner keeps walking while carried (x grows by about 12 px in 0.25 s)", s.TargetX > xa + 10f && s.TargetX < xa + 14f);
                float xin = s.TargetX;
                r.Release();
                Check("W-014 R4/R5: release with the runner still in the room: it goes home with the room (x - 160, same floor)", s.TargetFloor == 2 && Near(s.TargetX, xin - 160f, 0.01f) && s.TargetShiftDX == -160);

                r = new Rig(Old2LevelPath); s = r.S;
                r.Until(() => s.TargetFloor == 2 && s.TargetX >= 277f && s.TargetFacingRight, 90f);
                x0 = s.TargetX;
                bool pushedUp = r.Push(0, -1);
                Check("W-014 R5: push up: runner changes floor (2 -> 1) and keeps x (no stairs needed)", pushedUp && s.TargetFloor == 1 && Near(s.TargetX, x0, 0.01f) && s.TargetShiftDY == -96);
                r.Run(0.3f);
                xin = s.TargetX;
                r.Release();
                Check("W-014 R4/R5: release after an up push with the runner inside: runner goes back down to floor 2, x unchanged", s.TargetFloor == 2 && Near(s.TargetX, xin, 0.01f));

                // 대상이 나간 뒤 떼면 대상은 그대로
                r = new Rig(Old2LevelPath); s = r.S;
                r.Until(() => s.TargetFloor == 2 && s.TargetX >= 277f && s.TargetFacingRight, 90f);
                r.Push(0, -1);
                bool out2 = r.Until(() => s.TargetX >= 330f, 10f);
                float xout = s.TargetX;
                int shiftsBefore = s.TargetShiftSeq;
                r.Release();
                Check("W-014 R4: runner already out of the room (2F corridor, x>=330): releasing sends the room home but the runner stays", out2 && s.TargetFloor == 1 && Near(s.TargetX, xout, 0.01f) && s.TargetShiftSeq == shiftsBefore && !s.IsDisplaced(2, 1));
            }

            // ----- R6, R10, R11 (규칙 확인용 레벨) -----
            {
                var r = new Rig(MakePushLevel(1, 1, 20f, true));
                GameSim s = r.S;
                SectorObject door = r.Obj(Dt.ObjectKind.Door, 1, 1);
                r.Shift = true; s.ShiftHeld = true;
                bool bpOnly = s.IsBlueprint(1, 1) && s.ObjectOn(door);
                bool pushed = s.TryPush(0, -1);
                bool real = pushed && s.Pushed && !s.IsBlueprint(1, 1) && !s.ObjectOn(door);
                Check("W-014 R6: Shift alone -> the rail room is a blueprint (door open); the arrow press makes it reality again (not blueprint, door closed) while pushed", bpOnly && real);
                r.Release();
                Check("W-014 R6: after release the room is home and shows reality (door closed), no blueprint without Shift", !s.Pushed && !s.IsBlueprint(1, 1) && !s.ObjectOn(door));

                // R10: 밀린 방 안의 계단은 작동하지 않는다
                r = new Rig(MakePushLevel(1, 1, 20f, true)); s = r.S;
                door = r.Obj(Dt.ObjectKind.Door, 1, 1);
                r.Push(0, -1);
                bool past = r.Until(() => s.TargetX >= 230f, 15f);
                Check("W-014 R10: stairs inside a displaced room do nothing: the runner walks past S1 (x220) on the same floor (no link jump)", past && s.TargetFloor == 0 && s.TargetX >= 230f && s.TargetX < 320f);
                bool wait = r.Until(() => s.TargetWaiting, 15f);
                Check("W-014 R6/R10: pushed room is in reality: the closed door (x150 of the room, map x310) holds the runner at x301", wait && Near(s.TargetX, 301f) && s.TargetFloor == 0);
                var c = new Rig(MakePushLevel(1, 1, 20f, true));
                bool climbed = c.Until(() => c.S.TargetFloor == 0, 20f);
                Check("W-014 R10 control: same room at home, S1 works - the runner climbs to 2F col 0 x60 facing left", climbed && Near(c.S.TargetX, 60f) && !c.S.TargetFacingRight);

                // R10: 도착 방이 제자리를 벗어나면 연결이 작동하지 않는다
                r = new Rig(MakePushLevel(1, 0, 20f, true)); s = r.S;
                r.Push(0, -1);
                bool passed = r.Until(() => s.TargetX >= 100f, 15f);
                Check("W-014 R10: a link whose arrival room is displaced does nothing: runner passes S2 (x60) and continues on the same floor, no jump", passed && s.TargetFloor == 1 && s.TargetX >= 100f && s.TargetX < 155f);
                c = new Rig(MakePushLevel(1, 0, 20f, true));
                c.Until(() => c.S.TargetX >= 190f, 15f);
                Check("W-014 R10 control: arrival room at home, S2 works - the runner jumps to B1 x40 (map x200)", c.S.TargetX >= 190f && c.S.TargetX < 205f);

                // R11: 층 금지는 방의 원래 자리 기준
                r = new Rig(MakePushLevel(1, 1, 100f, false)); s = r.S;
                r.Push(0, -1);
                r.Until(() => s.TargetX <= 200f, 15f);
                bool stillOk = !s.MissionFailed(1) && s.TargetFloor == 0 && s.TargetX > 160f;
                r.Until(() => s.TargetX < 150f, 15f);
                Check("W-014 R11: floor-ban (floor 0) mission by original place: the carried room on cell floor 0 does not fail it; walking into the real floor-0 room (A0) does",
                    stillOk && s.MissionFailed(1) && s.TargetFloor == 0 && s.TargetX < 150f);

                // R11: 섹터 진입 미션은 방의 원래 자리 기준
                r = new Rig(MakeSwapLevel()); s = r.S;
                bool sw = r.Push(-1, 0);
                bool notYet = sw && !s.MissionFinal && Near(s.TargetX, 260f) && s.CellOf(0, 1) == P(0, 0) && s.CellOf(0, 0) == P(1, 0);
                r.Until(() => s.Outcome != Outcome.Running, 10f);
                Check("W-014 R11: enter-sector mission (col 1) is judged by the room's original place: right after the swap the runner (cell col 1, hosting room col 0) has not completed it; entering the swapped room (now in cell 0) does",
                    notYet && s.MissionFinal && s.Outcome == Outcome.Complete && s.TargetX < 160f);
            }
        }

        // ----- 의뢰 경로(자동 입력) -----

        /// <summary>1-1 경로: A 끝 방을 →로 밀어 대상을 B로 나르고(나르기), 필요하면 B 계단을 닫아 단말을 찍고(탐색), 출구까지. 별·추적도를 로그에 남긴다.</summary>
        static void Old1Route(string label, bool closeStairs, int expectStars, string estimate)
        {
            var r = new Rig(Old1LevelPath);
            GameSim s = r.S;
            bool ok = true;
            r.Jump(1, 0);
            ok &= s.Aim == P(1, 0);
            ok &= r.Until(() => s.TargetFloor == 0 && s.TargetX >= 277f && s.TargetFacingRight, 60f);
            ok &= r.Push(1, 0);                                   // 나르기: 조력자가 A 끝 방에서 오른쪽으로 걸을 때 Shift + →
            ok &= r.Until(() => s.TargetX >= 490f, 10f);          // B 복도에 넘어갈 때까지 누름
            r.Release();                                           // 넘어간 뒤 뗌(방은 제자리로)
            ok &= !s.IsDisplaced(0, 1);
            if (closeStairs)
            {
                r.Jump(1, 0);
                ok &= s.Aim == P(4, 0);
                ok &= r.Until(() => s.TargetX >= 671f, 20f);
                r.Shift = true; s.ShiftHeld = true;               // B 계단 닫기(설계도 상태)
                ok &= r.Until(() => s.TargetX >= 700f, 5f);
                r.Release();
                ok &= r.Until(() => s.MissionDone(1), 20f);       // 단말
            }
            ok &= r.Until(() => s.Outcome != Outcome.Running, 90f);
            bool traceOk = !s.MissionFailed(2);
            Check("W-014 1-1 " + label + ": cleared by the automatic route (Complete, " + expectStars + " stars, final mission done, terminal " + (closeStairs ? "done" : "skipped") + ", TRACE<=40 " + (traceOk ? "kept" : "broken") + ")",
                ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.Stars == expectStars && s.MissionDone(1) == closeStairs && traceOk);
            Note("1-1 " + label + ": Shift total " + r.ShiftSeconds.ToString("0.00") + " s, final tracking " + s.Tracking.ToString("0.0") + ", peak " + r.Peak.ToString("0.0") + ", stars " + s.Stars + " (03-01 §2-3 estimate: " + estimate + ")");
        }

        /// <summary>
        /// 1-2 경로. terminal이면 창고 나르기 2번(되돌리기 포함)으로 단말을 찍는다.
        /// 기본 흐름(W-016): 빈 점포째 위로(올리기) -> 레이저 -> 중계기 -> 계단실 카메라 끄기(조력자가 경계 48px 앞에서 돌아서기 전에 계단실을 조준해 Shift, 계단을 오를 때까지 누름) -> 계단 -> 옥상 출구 방 끌어오기.
        /// 발각 0회로 UNSEEN을 지켜야 한다.
        /// </summary>
        static void Old2Route(string label, bool terminal, int expectStars, string estimate)
        {
            var r = new Rig(Old2LevelPath);
            GameSim s = r.S;
            bool ok = true;
            string stop = "";
            if (terminal)
            {
                ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX >= 277f && s.TargetFacingRight, 90f);
                ok &= r.Push(1, 0);                                // 나르기 1: 골목 위에 놓아 창고로 건너보냄
                ok &= r.Until(() => s.TargetX >= 483f, 10f);
                r.Release();
                if (!ok) stop = "carry1";
                ok &= r.Until(() => s.MissionDone(1), 30f);        // 단말
                ok &= r.Until(() => !s.TargetFacingRight, 30f);    // 창고 끝(빈 칸)에서 돌아섬
                ok &= r.Until(() => s.TargetX <= 515f, 30f);
                ok &= r.Push(1, 0);                                // 나르기 2: 창고 왼쪽 끝에서 다시 →(방을 골목 위로)
                ok &= r.Until(() => s.TargetX <= 470f, 10f);       // 조력자가 방 안에 들어오면
                r.Release();                                       // 방 안에 있는 채로 뗌 -> 방과 함께 제자리로(되돌리기)
                if (stop == "" && !ok) stop = "carry2";
                ok &= s.TargetFloor == 2 && s.TargetX < 320f;
            }
            ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX >= 277f && s.TargetX < 310f && s.TargetFacingRight, 90f);
            ok &= r.Push(0, -1);                                   // 올리기
            ok &= s.TargetFloor == 1;
            ok &= r.Until(() => s.TargetX >= 330f, 10f);
            r.Release();
            if (stop == "" && !ok) stop = "lift";
            r.Jump(1, 0);
            ok &= s.Aim == P(3, 1);                                // 레이저 복도
            ok &= r.Until(() => s.TargetWaiting, 30f);
            r.Shift = true; s.ShiftHeld = true;
            ok &= r.Until(() => s.TargetX > 490f, 10f);
            r.Release();
            if (stop == "" && !ok) stop = "laser";
            ok &= r.Until(() => s.RelayUsed, 10f);
            r.Jump(1, 0);
            ok &= s.Aim == P(4, 1);                                // 계단실(카메라·계단)
            ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 585f, 20f);   // 경계(640) 48px 앞(592)에서 돌아서기 전에
            ok &= s.AlertSeq == 0;
            r.Shift = true; s.ShiftHeld = true;                    // 카메라 끄기(설계도 상태)
            ok &= r.Until(() => s.TargetFloor == 0, 10f);          // 구역을 지나 계단 2F -> 3F
            r.Release();
            if (stop == "" && !ok) stop = "camera/stairs";
            r.Jump(-1, 0); r.Jump(-1, 0);                          // (4,1) -> (3,1) -> 옥상 출구 방
            ok &= s.Aim == P(1, 0);
            ok &= r.Until(() => s.TargetFloor == 0 && !s.TargetFacingRight && s.TargetX <= 503f && s.TargetX >= 489f, 60f);
            ok &= r.Push(1, 0);                                    // 끌어오기: 출구 방을 빈 칸 (3F, 열 2)로
            ok &= s.CellOf(0, 1) == P(2, 0);
            ok &= r.Until(() => s.Outcome != Outcome.Running, 15f);
            if (stop == "" && !ok) stop = "pull";
            bool unseenOk = !s.MissionFailed(2) && s.MissionDone(2);
            Check("W-016 1-2 " + label + ": cleared by the automatic route (Complete, " + expectStars + " stars, terminal " + (terminal ? "done" : "skipped") + ", detections " + s.DetectCount + ", UNSEEN " + (unseenOk ? "kept" : "broken") + ")" + (ok ? "" : " - stopped at " + stop),
                ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.Stars == expectStars && s.MissionDone(1) == terminal && s.DetectCount == 0 && unseenOk);
            Note("1-2 " + label + ": Shift total " + r.ShiftSeconds.ToString("0.00") + " s, final tracking " + s.Tracking.ToString("0.0") + ", peak " + r.Peak.ToString("0.0") + ", detections " + s.DetectCount + ", stars " + s.Stars + " (03-01 §2-3 estimate: " + estimate + ")");
            if (r.Peak >= s.TrackingMax) Note("1-2 " + label + ": PEAK REACHED THE MAX (100) - 03-01 §2-3 needs recalculation (do not change numbers without a design decision)");
        }

        /// <summary>
        /// 발각 경로(UNSEEN 실패 확인): 계단실 카메라를 끄고 구역 안에서 Shift를 떼어 발각시킨다(★1 평가 확인용).
        /// 그 뒤 완주는 요구하지 않는다: 발각 +40 때문에 추적도가 100에 닿아 카운트다운이 돌 수 있어, 끝까지 가면 결과가 달라진다. 결과는 INFO로만 남긴다.
        /// </summary>
        static void Old2DetectRoute()
        {
            var r = new Rig(Old2LevelPath);
            GameSim s = r.S;
            bool ok = true;
            ok &= Old2ToCamera(r);
            r.Shift = true; s.ShiftHeld = true;
            ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 660f, 10f);
            float before = s.Tracking;
            r.Release();
            r.Run(1f / 30f);
            Check("W-016 1-2 detect route: Shift released inside the zone -> detected once (+" + (s.Tracking - before).ToString("0.0") + " tracking), UNSEEN failed, helper turned back, projected stars 1 (terminal skipped)",
                ok && s.DetectCount == 1 && Near(s.Tracking, before + 40f, 0.01f) && s.MissionFailed(2) && !s.TargetFacingRight && s.ProjectedStars == 1);
            r.Until(() => s.Outcome != Outcome.Running, 60f);
            Note("1-2 detect route (informational, not required to finish): outcome " + s.Outcome + ", detections " + s.DetectCount + ", final tracking " + s.Tracking.ToString("0.0") + ", peak " + r.Peak.ToString("0.0") + ", UNSEEN " + (s.MissionFailed(2) ? "failed" : "kept"));
        }

        /// <summary>1-2를 처음부터 올리기 -> 레이저 -> 중계기까지 진행하고 계단실을 조준한다(카메라 앞 상태). 성공하면 true.</summary>
        static bool Old2ToCamera(Rig r)
        {
            GameSim s = r.S;
            bool ok = true;
            ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX >= 277f && s.TargetX < 310f && s.TargetFacingRight, 90f);
            ok &= r.Push(0, -1);
            ok &= r.Until(() => s.TargetX >= 330f, 10f);
            r.Release();
            r.Jump(1, 0);
            ok &= s.Aim == P(3, 1);
            ok &= r.Until(() => s.TargetWaiting, 30f);
            r.Shift = true; s.ShiftHeld = true;
            ok &= r.Until(() => s.TargetX > 490f, 10f);
            r.Release();
            ok &= r.Until(() => s.RelayUsed, 10f);
            r.Jump(1, 0);
            ok &= s.Aim == P(4, 1);
            return ok;
        }

        static void W14RouteChecks()
        {
            Old1Route("star-2 route (carry + exit, stairs open)", false, 2, "나르기 0.9 s, trace 18");
            Old1Route("star-3 route (carry + close B stairs for the terminal)", true, 3, "나르기 0.9 + 계단 닫기 0.6 = 1.5 s, trace 30, terminal -30");
            Old2Route("star-2 route (lift + laser + camera off + pull, no terminal, UNSEEN kept)", false, 2, "올리기 0.9 + 레이저 0.7 + 끌어오기 0.9 = 2.5 s (+ 카메라 통과, W-016 추가), relay -40");
            Old2DetectRoute();
            Old2Route("star-3 route (carry x2 with a return + lift + laser + camera off + pull, UNSEEN kept)", true, 3, "위 + 나르기 2번 1.8 s = 4.3 s (+ 카메라 통과, W-016 추가), relay -40, terminal -30");
        }

        // ===================================================================================================
        // W-016: 감시 카메라·조력자 위험 반응·발각(R-C1~R-C5)과 3층 데이터. 화면 없이 GameSim을 1/60초씩 직접 진행한다.
        // ===================================================================================================

        const string CamTypePath = "Assets/_Project/Data/Surveillance_cam_basic.asset";

        /// <summary>규칙 확인용 레벨 C: 1층 x 3열(빈 칸 없음). 열 0 = 시작, 열 1 = (doorClosed면 닫힌 문 / 아니면 열린 문) x140, 열 2 = 카메라 방(구역 왼쪽 경계 x 320).</summary>
        static Dt.LevelData MakeCameraLevel(bool withDoor, bool doorClosed, Dt.SurveillanceType type, float objOverride = -1f)
        {
            var l = ScriptableObject.CreateInstance<Dt.LevelData>();
            l.floors = 1; l.cols = 3; l.sectorWidth = 160; l.sectorHeight = 96; l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.sectors = new Dt.SectorDef[0];
            Dt.ObjectDef cam = PushObj(Dt.ObjectKind.Camera, 0, 2, 80, true, false);
            cam.surveillance = type;
            cam.detectTrackingOverride = objOverride;
            l.objects = withDoor
                ? new[] { PushObj(Dt.ObjectKind.Door, 0, 1, 140, !doorClosed, true), cam }
                : new[] { cam };
            l.startFloor = 0; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 0; l.startAimCol = 2;
            l.missions = new[] { new Dt.MissionDef { kind = Dt.MissionKind.PassObject }, new Dt.MissionDef { kind = Dt.MissionKind.Unseen } };
            return l;
        }

        static void W16PureChecks()
        {
            var type = AssetDatabase.LoadAssetAtPath<Dt.SurveillanceType>(CamTypePath);
            var settings = AssetDatabase.LoadAssetAtPath<Dt.GameSettings>("Assets/_Project/Data/GameSettings.asset");

            // ----- 데이터 3층 -----
            Check("W-016 data layer 1 (GameSettings): helper alert distance 48, detect tracking 40, alert mark 0.5 s",
                settings != null && Near(settings.helperAlertDistance, 48f, 0.001f) && Near(settings.detectTracking, 40f, 0.001f) && Near(settings.alertMarkSeconds, 0.5f, 0.001f));
            Check("W-016 data layer 2 (SurveillanceType asset): id cam_basic, 1 col, reality on / blueprint off, no tracking override, redetect 3 s, art key camera",
                type != null && type.id == "cam_basic" && type.coverCols == 1 && type.realityOn && !type.blueprintOn && type.detectTrackingOverride < 0f && Near(type.redetectSeconds, 3f, 0.001f) && type.artKey == "camera");
            {
                var r = new Rig(Old2LevelPath);
                SectorObject cam = r.Obj(Dt.ObjectKind.Camera, 1, 4);
                var list = AssetDatabase.LoadAssetAtPath<Dt.RequestList>("Assets/_Project/Data/RequestList.asset");
                string[] names = null;
                foreach (var e in list.entries) if (e.id == "old-1-2") names = e.missionNames;
                Check("W-016 data layer 3 (1-2 placement): one camera at (floor 1, col 4) with cam_basic, drawn at x 120, no per-level override (-1), reality on / blueprint off, mission names end with UNSEEN",
                    cam != null && cam.Def.surveillance == type && Near(cam.Def.x, 120f, 0.001f) && Near(cam.Def.drawX, 120f, 0.001f) && cam.Def.detectTrackingOverride < 0f && cam.Def.realityOn && !cam.Def.blueprintOn && cam.Def.drawSprite
                    && r.S.CameraCount == 1 && names != null && names.Length == 3 && names[2] == "UNSEEN" && r.S.Board.Missions[2].kind == Dt.MissionKind.Unseen);
                Check("W-016 1-2 camera room (floor 1, col 4) is aimable (operable) and the old TRACE<=60 mission is no longer in the list", r.S.Board.IsOperable(1, 4) && System.Array.TrueForAll(r.S.Board.Missions, m => m.kind != Dt.MissionKind.TraceLimit));
                Check("W-016 art set has camera_on / camera_off (24x18 logical) and surveil_zone (160x96) at 4x size",
                    view.Canvas.Has("objects128/camera_on") && view.Canvas.Has("objects128/camera_off") && view.Canvas.Has("objects128/surveil_zone")
                    && view.Canvas.WidthOf("objects128/camera_on") == 24 && view.Canvas.HeightOf("objects128/camera_on") == 18
                    && view.Canvas.WidthOf("objects128/camera_off") == 24 && view.Canvas.WidthOf("objects128/surveil_zone") == 160 && view.Canvas.HeightOf("objects128/surveil_zone") == 96);
                Check("W-016 chapter 0 and 1-1 have no camera (no detection in the tutorial)",
                    new Rig("Assets/_Project/Data/Ch0Home_1.asset").S.CameraCount == 0 && new Rig("Assets/_Project/Data/Ch0Home_2.asset").S.CameraCount == 0 && new Rig(Old1LevelPath).S.CameraCount == 0);
            }
            {
                // 값 읽는 순서: ③ 배치 덮어쓰기 -> ② 종류 -> ① 공통
                var t2 = ScriptableObject.CreateInstance<Dt.SurveillanceType>();
                t2.detectTrackingOverride = 15f;
                // 구역 바깥(열 1 x100)에서 Shift로 들어간 뒤 떼는 레벨로 첫 발각 한 번의 추적도 값을 읽는다
                float Direct(Dt.SurveillanceType ty, float objOv)
                {
                    var lv = MakeCameraLevel(false, false, ty, objOv);
                    lv.startCol = 1; lv.startX = 100;   // D-101: 구역 바깥에서 Shift(꺼짐)로 걸어 들어간 뒤 떼는 순간 발각
                    var rr = new Rig(lv);
                    rr.Run(2.9f);                                   // 시작 대기(3초)는 Shift 없이 보낸다(추적도가 최대치에 닿지 않게)
                    rr.Shift = true; rr.S.ShiftHeld = true;
                    rr.Until(() => rr.S.TargetX >= 360f, 10f);
                    float before = rr.S.Tracking;                   // 발각 직전 값 — 차이만 읽는다
                    rr.Release();
                    rr.Run(0.1f);
                    return rr.S.Tracking - before;
                }
                float a = Direct(type, -1f), b = Direct(t2, -1f), c = Direct(t2, 25f), d = Direct(null, -1f);
                Check("W-016 value order: common 40 (type without override) / type override 15 / placement override 25 beats type / no type falls back to common 40 - got " + a + " / " + b + " / " + c + " / " + d,
                    Near(a, 40f, 0.01f) && Near(b, 15f, 0.01f) && Near(c, 25f, 0.01f) && Near(d, 40f, 0.01f));
                UnityEngine.Object.DestroyImmediate(t2);
            }

            // ----- R-C1: 켜진 카메라 앞에서 돌아섬 -----
            {
                var r = new Rig(Old2LevelPath);
                GameSim s = r.S;
                bool ok = Old2ToCamera(r);
                float trackBefore = s.Tracking;
                ok &= r.Until(() => s.AlertSeq > 0 || s.TargetX > 640f, 20f);
                float turnX = s.TargetX;
                bool turned = s.AlertSeq == 1 && !s.TargetFacingRight && s.TargetFloor == 1;
                float mark0 = s.AlertMarkLeft;
                r.Run(0.25f);
                bool markHalf = s.AlertMarkLeft > 0.2f && s.AlertMarkLeft < 0.3f;
                r.Run(0.3f);
                bool markGone = s.AlertMarkLeft <= 0f;
                Check("R-C1 camera on: the helper turns back 48 px before the zone border (x " + turnX.ToString("0.0") + " of 592), alert mark " + mark0.ToString("0.00") + " s then gone after 0.55 s, no detection, tracking unchanged",
                    ok && turned && Near(turnX, 592.4f, 0.9f) && Near(mark0, 0.5f, 0.001f) && markHalf && markGone && s.DetectCount == 0 && !s.MissionFailed(2) && Near(s.Tracking, trackBefore, 0.01f));
            }

            // ----- R-C2 ~ R-C4 (한 흐름) -----
            {
                var r = new Rig(Old2LevelPath);
                GameSim s = r.S;
                bool ok = Old2ToCamera(r);
                ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 585f, 20f);
                r.Shift = true; s.ShiftHeld = true;
                SectorObject cam = r.Obj(Dt.ObjectKind.Camera, 1, 4);
                ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 660f, 10f);
                Check("R-C2 aim the camera room + Shift: camera off, the helper walks into the zone (x " + s.TargetX.ToString("0.0") + " >= 640) without turning back, no detection",
                    ok && !s.CameraOn(cam) && s.AlertSeq == 0 && s.DetectCount == 0 && s.TargetFacingRight && s.TargetX >= 640f);

                float before = s.Tracking;
                r.Release();
                r.Run(1f / 60f);
                Check("R-C3 Shift released inside the zone: detected once - tracking +" + (s.Tracking - before).ToString("0.0") + " (40), UNSEEN failed, helper turned back with an alert mark, detect cell (4,1), cooldown ~3 s",
                    s.DetectCount == 1 && Near(s.Tracking, before + 40f, 0.01f) && s.MissionFailed(2) && !s.TargetFacingRight && s.AlertMarkLeft > 0.4f && s.LastDetectCell == P(4, 1) && s.CameraCooldown(0) > 2.9f && s.CameraCooldown(0) <= 3f);

                // 조력자는 레이저(켜짐)에 막혀 구역 안(x 649)에서 기다린다 -> D-101: 구역 안에 머물러도 다시 발각되지 않는다
                r.Run(5f);
                float waitX = s.TargetX;
                Check("R-C4 stay inside the lit zone: after 5 s the helper still stands inside the zone (x " + waitX.ToString("0.0") + ", waiting at the laser) and there is no extra detection (count " + s.DetectCount + ", tracking " + s.Tracking.ToString("0.0") + ")",
                    s.DetectCount == 1 && s.TargetWaiting && waitX >= 640f && Near(s.Tracking, before + 40f, 0.01f));
                // 대조: 껐다가 재발각 대기가 끝난 뒤 떼면 한 번 더 발각된다
                r.Shift = true; s.ShiftHeld = true;
                r.Run(0.5f);
                bool off = !s.CameraOn(cam) && s.CameraCooldown(0) <= 0f;
                r.Release();
                r.Run(1f / 60f);
                Check("R-C4 control: Shift on then released after the wait ended (camera cooldown 0) detects once more (count " + s.DetectCount + ", tracking " + s.Tracking.ToString("0.0") + " = min(max, " + before.ToString("0.0") + " + 80))",
                    off && s.DetectCount == 2 && Near(s.Tracking, Mathf.Min(s.TrackingMax, before + 80f), 0.01f));
                // 대기 중(3초 안) 껐다 켜면 발각 없음
                r.Shift = true; s.ShiftHeld = true;
                r.Run(0.5f);
                r.Release();
                r.Run(1f / 60f);
                Check("R-C4 Shift spam: off then on again inside the 3 s wait does not detect (count " + s.DetectCount + ", cooldown " + s.CameraCooldown(0).ToString("0.00") + ")",
                    s.DetectCount == 2 && s.CameraCooldown(0) > 0f);
            }

            // ----- R-C6: 꺼진 구역 안에서 Shift를 떼면 발각 1회, 시작 때 이미 켜진 구역 안이면 발각 없음 -----
            {
                var lv = MakeCameraLevel(false, false, type);
                lv.startCol = 2; lv.startX = 80;
                var rr = new Rig(lv);
                rr.Run(0.5f);
                Check("R-C6 start inside a lit zone: no detection while staying (count " + rr.S.DetectCount + ")", rr.S.DetectCount == 0);
                var lv2 = MakeCameraLevel(false, false, type);
                lv2.startCol = 2; lv2.startX = 80;
                var r2 = new Rig(lv2);
                r2.Shift = true; r2.S.ShiftHeld = true;
                r2.Run(0.5f);
                bool offIn = !r2.S.CameraOn(r2.S.CameraAt(0)) && r2.S.DetectCount == 0;
                r2.Release();
                r2.Run(1f / 60f);
                Check("R-C6 helper inside the zone while Shift (camera off): releasing Shift detects once (count " + r2.S.DetectCount + ")", offIn && r2.S.DetectCount == 1);
            }

            // ----- R-C5: 사이에 닫힌 문이 있으면 위험 반응 없음 -----
            {
                var rc = new Rig(MakeCameraLevel(true, true, type));
                GameSim s = rc.S;
                rc.Run(3.2f);   // 시작 대기 3초
                rc.Run(8f);
                bool closed = s.AlertSeq == 0 && s.DetectCount == 0 && s.TargetWaiting && s.TargetFacingRight && s.TargetX > 285f && s.TargetX < 295f;
                Check("R-C5 a closed door lies between the helper and the zone border (door x 300, border x 320, 29 px apart): no turn, no alert mark; the helper waits at the door (x " + s.TargetX.ToString("0.0") + ")", closed);
                var ro = new Rig(MakeCameraLevel(true, false, type));
                ro.Run(3.2f);
                ro.Run(8f);
                GameSim so = ro.S;
                Check("R-C5 control: with the door open the same helper turns back 48 px before the border (alert count " + so.AlertSeq + ", no detection)",
                    so.AlertSeq >= 1 && so.DetectCount == 0);
                // 문을 닫았다가(Shift 설계도: 열림) 같은 문이 열리면 이어서 카메라를 본다: 닫힌 문이 열린 순간 위험 반응
                var rd = new Rig(MakeCameraLevel(true, true, type));
                rd.Jump(-1, 0);   // 문 방(열 1)을 조준
                rd.Run(3.2f);
                rd.Run(6f);
                rd.Shift = true; rd.S.ShiftHeld = true;   // 문 방(열 1)을 조준하고 Shift: 문 열림(설계도)
                rd.Run(0.2f);
                Check("R-C5 door opened (blueprint): the closed door no longer blocks sight, the waiting helper now sees the border and turns back (alert count " + rd.S.AlertSeq + ")",
                    rd.S.AlertSeq >= 1 && rd.S.DetectCount == 0);
            }
        }

        /// <summary>대화 없는 의뢰(챕터 1): 패널을 열고 접속해 곧바로 Playing이 될 때까지 기다린다.</summary>
        static IEnumerator OpenAndConnectNoBriefing()
        {
            yield return Press("Enter");   // 패널 열기
            yield return WaitUntil(() => view.PanelSettled, "panel settled (chapter 1)");
            yield return Press("Enter");   // CONNECT
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing, "connect -> Playing (no briefing)");
        }

        static bool LogHas(string line)
        {
            foreach (string l in view.CommandLog) if (l == line) return true;
            return false;
        }

        /// <summary>
        /// 1-2 화면 흐름 앞부분(실제 입력 경로): 빈 점포째 Shift + ↑로 올리기 -> 레이저 복도를 조준해 Shift -> 조력자가 레이저 복도에 들어서면(x 490) 뗌.
        /// 끝나면 조력자는 레이저 복도 안(x 490 부근)에서 오른쪽으로 걷고 있고 Shift는 뗀 상태(중계기·카메라 경계는 아직 앞).
        /// </summary>
        static IEnumerator Old2RealToLaser()
        {
            Advance(() => Sim.TargetFloor == 2 && Sim.TargetX >= 277f && Sim.TargetFacingRight, 40f);
            shift = true;
            yield return Frames(2);
            next.Up = true;
            yield return Frames(2);
            Check("W-014 1-2 Shift + ↑: the empty store lifts to 2F (cell col 1, floor 1), runner changes floor with it, same x", Sim.Pushed && Sim.CellOf(2, 1) == P(1, 1) && Sim.TargetFloor == 1 && LogHas("> PUSH UP"));
            yield return Frames(16);
            view.Renderer.FinishSlides();
            yield return Frames(2);
            CaptureW14("04-1-2-store-lifted");
            yield return Frames(1);
            Advance(() => Sim.TargetX >= 330f, 10f);
            shift = false;
            yield return Frames(3);
            Check("W-014 1-2 runner out in the 2F corridor, store back home on 1F", !Sim.Pushed && Sim.TargetFloor == 1 && !Sim.IsDisplaced(2, 1));

            yield return Jump(Dir.Right, 3, 1);        // 레이저 복도
            Advance(() => Sim.TargetWaiting, 30f);
            shift = true;
            yield return Frames(3);
            view.Renderer.FinishSlides();
            Check("W-014 1-2 Shift on the laser corridor: blueprint overlay (whole room), not pushed", Sim.IsBlueprint(1, 3) && !Sim.Pushed && !Sim.SectorBlocked(1, 3));
            yield return Frames(14);
            CaptureW14("06-1-2-laser-shift");
            yield return Frames(1);
            Advance(() => Sim.TargetX > 490f, 10f);
            shift = false;
        }

        /// <summary>
        /// W-016 화면 확인(1-2): 캡처 4장. ① 계단실 카메라 켜짐(감시 빛) ③ 조력자가 경계 앞에서 돌아섬 + ! ② 카메라 방 조준 + Shift(설계도, 꺼짐) ④ 구역 안에서 Shift를 뗀 발각 순간(Danger 글리치).
        /// 끝에 일시정지 메뉴로 의뢰를 처음부터 다시 시작한다(발각으로 UNSEEN이 실패했고 추적도가 높아졌으므로).
        /// </summary>
        static IEnumerator Old2RealCameraDemo()
        {
            yield return Jump(Dir.Right, 4, 1);        // 계단실(카메라·계단) 조준
            yield return Frames(20);
            view.Renderer.FinishSlides();
            yield return Frames(2);
            SectorObject cam = Sim.CameraAt(0);
            string camKey = view.Renderer.ResolveObjectKey(cam);
            Check("W-016 1-2 camera room aimed without Shift: camera is on, draws objects128/camera_on, the surveillance light key is in the art set, no blueprint overlay on it",
                Sim.CameraOn(cam) && camKey == "objects128/camera_on" && view.Canvas.Has("objects128/surveil_zone") && !Sim.IsBlueprint(1, 4) && view.Renderer.ResolveBlueprintKey(cam) == null);
            CaptureW16("01-camera-on-surveillance-light");
            yield return Frames(1);

            // ③ 조력자가 켜진 카메라의 경계 앞에서 돌아선다(실제 프레임으로 기다려 ! 표시가 보이는 프레임을 캡처)
            yield return WaitUntil(() => Sim.AlertMarkLeft > 0f, "alert mark shows (helper turns back at the camera zone border)", 1500);
            Check("W-016 1-2 the helper turned back before the border (x " + Sim.TargetX.ToString("0.0") + "), alert mark showing (" + Sim.AlertMarkLeft.ToString("0.00") + " s left), no detection",
                Sim.AlertSeq == 1 && !Sim.TargetFacingRight && Sim.AlertMarkLeft > 0f && Sim.DetectCount == 0 && !Sim.MissionFailed(2));
            CaptureW16("03-helper-turns-alert-mark");
            yield return Frames(1);

            // 조력자가 레이저 앞에서 기다리면 레이저를 다시 끄고 계단실로 돌아온다
            Advance(() => Sim.TargetWaiting, 40f);
            yield return Jump(Dir.Left, 3, 1);
            shift = true;
            yield return Frames(3);
            Advance(() => Sim.TargetX > 490f, 15f);
            shift = false;
            yield return Frames(2);
            yield return Jump(Dir.Right, 4, 1);
            Advance(() => Sim.TargetFloor == 1 && Sim.TargetX >= 585f, 20f);

            // ② 카메라 방 조준 + Shift: 설계도 겹침, 꺼진 카메라 그림
            shift = true;
            yield return Frames(14);
            view.Renderer.FinishSlides();
            Check("W-016 1-2 aim the camera room + Shift: blueprint overlay, camera off (draws objects128/camera_off), no surveillance light, the helper is not turning back",
                Sim.IsBlueprint(1, 4) && !Sim.CameraOn(cam) && Sim.AlertSeq == 1 && Sim.DetectCount == 0);
            CaptureW16("02-camera-off-blueprint-shift");
            yield return Frames(1);
            Advance(() => Sim.TargetFloor == 1 && Sim.TargetX >= 660f, 10f);

            // ④ 구역 안에서 Shift를 뗀다 -> 발각: Danger 글리치(틱 1을 멈춰 캡처), 로그, 경보음
            float trackBefore = Sim.Tracking;
            int sounds = view.AlertSoundCount;
            Glitch.FreezeTick = 1;
            shift = false;
            yield return Frames(3);
            Check("W-016 1-2 Shift released inside the zone: detected - Danger glitch playing, log '> CAMERA: SPOTTED', alert sound played (+" + (view.AlertSoundCount - sounds) + "), tracking +" + (Sim.Tracking - trackBefore).ToString("0.0") + ", UNSEEN failed, helper turned back",
                Sim.DetectCount == 1 && Glitch.IsPlaying(Glitch.Danger) && LogHas("> CAMERA: SPOTTED") && view.AlertSoundCount == sounds + 1 && Sim.Tracking >= Mathf.Min(Sim.TrackingMax, trackBefore + 39.9f) && Sim.MissionFailed(2) && !Sim.TargetFacingRight);
            CaptureW16("04-detected-danger-glitch");
            Glitch.FreezeTick = -1;
            yield return Frames(1);

            // 처음부터 다시(일시정지 메뉴 Restart)
            next.Esc = true;
            yield return Frames(2);
            Check("W-016 1-2 Esc -> Paused (before Restart)", view.CurrentPhase == GameView.Phase.Paused);
            yield return Press("Down");
            yield return Press("Enter");
            yield return Frames(2);
            Check("W-016 1-2 Restart: playing again, tracking 0, no detections, UNSEEN not failed, alert cleared, camera on", view.CurrentPhase == GameView.Phase.Playing && Sim.Tracking == 0f && Sim.DetectCount == 0 && !Sim.MissionFailed(2) && Sim.AlertMarkLeft == 0f && Sim.CameraOn(Sim.CameraAt(0)));
        }

        /// <summary>
        /// 화면 흐름으로 챕터 1 확인(W-014): 챕터 1 폴더 -> 1-1(안내 3단계·밀기·막힘·계단 닫기 탐색·★3 클리어) -> 1-2(올리기·레이저 Shift·끌어오기·클리어).
        /// 캡처 6장: 1-1 시작 / 1-1 A 끝 방 밀린 상태(조력자 안) / 1-1 막힘 흔들림 / 1-2 빈 점포 위로 민 상태 / 1-2 출구 방 끌어온 상태 / 1-2 레이저 복도 Shift.
        /// </summary>
        static IEnumerator Ch1Run()
        {
            view.Progress.SetCleared("home-0-1");
            view.Progress.SetCleared("home-0-2");
            if (view.MainIndex != 0) yield return Press("Up");
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop (chapter 1 run)");
            Check("W-014 chapter 1 folder is open after chapter 0 is cleared (CH1 open, CH2 still locked)", view.Desk.ChapterOpen(1) && !view.Desk.ChapterOpen(2) && !view.InFolder);
            yield return Press("Down");
            Check("W-014 Down selects the CH1 folder", view.FolderIndex == 1);
            yield return Press("Enter");
            Check("W-014 Enter on CH1 -> request list with REQ_1-1 selected; list has 4 requests (0-1, 0-2, 1-1, 1-2)",
                view.InFolder && view.FolderIndex == 1 && view.DeskIndex == 2 && view.Desk.Count == 4 && view.Desk.RequestName(2) == "REQ_1-1" && view.Desk.RequestName(3) == "REQ_1-2");
            Check("W-014 REQ_1-1 playable (NEW), REQ_1-2 locked until 1-1 clears", view.Desk.State(2) == RequestState.New && view.Desk.State(3) == RequestState.Locked && !view.Desk.IsTutorial(2));
            yield return OpenAndConnectNoBriefing();
            yield return Frames(3);

            // ===== 의뢰 1-1 =====
            Check("W-014 1-1 starts without a briefing: Playing on the MISSION tab, no dialogue shown, header OLD TOWN A/B", view.CurrentTab == GameView.Tab.Mission && view.Shown.Count == 0 && Sim.Board.BuildingName == "OLD TOWN A/B" && view.Desk.Entry(2).missionNames.Length == 3);
            Check("W-014 1-1 draws with the room layers (layout loaded, every room/tile texture in the art set)", Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch1_old1/layout" && LayoutArtOk(Sim.Board.Layout));
            Check("W-014 1-1 guide step 1: no badge, 'SOME ROOMS SIT ON RAILS. AIM AT ONE.' (guide box on a non-tutorial request)", view.Tut != null && !Sim.Board.Tutorial && TutIs(0, null, "SOME ROOMS SIT ON RAILS. AIM AT ONE."));
            Check("W-014 1-1 is not a tutorial: 3 missions listed, star rating shown (RATING row), guide box on", Sim.MissionCount == 3 && !Sim.Board.Tutorial && view.Tut != null);
            Check("W-015 1-1 layout uses old-town rooms only (every room key starts with ot_) and the rail texture is in the art set", OldTownRoomsOnly(Sim.Board.Layout) && view.Canvas.Has("rooms128/common/rail"));
            CaptureW14("01-1-1-start");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            yield return Jump(Dir.Right, 1, 0);   // 시작 방 -> A 끝 방(레일)
            yield return Frames(3);
            Check("W-014 1-1 guide step 2 when the aim reaches the rail room: [SHIFT] [→] 'HOLD SHIFT, THEN PRESS → TO SLIDE IT.'", TutIs(1, "SHIFT", "HOLD SHIFT, THEN PRESS → TO SLIDE IT.") && view.Tut.Key2(Sim) == "→");
            Advance(() => Sim.TargetFloor == 0 && Sim.TargetX >= 277f && Sim.TargetFacingRight, 40f);
            float tx0 = Sim.TargetX;
            shift = true;
            yield return Frames(2);
            next.Right = true;
            yield return Frames(2);
            Check("W-014 1-1 Shift + →: the A end room slides to the empty cell (col 2) and the runner rides along (x + 160); guide step 3; command log 'PUSH RIGHT'; tracking rising",
                Sim.Pushed && Sim.CellOf(0, 1) == P(2, 0) && Sim.TargetFloor == 0 && Sim.TargetX > tx0 + 150f && TutIs(2, null, "LET GO ONLY AFTER SHE STEPS OUT.") && LogHas("> PUSH RIGHT") && Sim.Tracking > 0f);
            yield return Frames(16);            // 카메라(0.2초)가 밀린 방의 새 칸으로 다 따라갈 때까지
            view.Renderer.FinishSlides();
            yield return Frames(2);
            Vector2Int roomPos = view.Renderer.DebugRoomPos(Sim, 0, 1);
            Check("W-014 1-1 pushed room is drawn at its present cell (map x 320, y 24 + 0) once the slide ends", roomPos == P(320, 24));
            CaptureW14("02-1-1-pushed-with-helper");
            yield return Frames(1);

            // 막힘: 이미 밀린 상태에서 또 누름 -> 흔들림 + 소리, 추적도는 시간분만
            float trBefore = Sim.Tracking;
            double tBlock0 = EditorApplication.timeSinceStartup;
            int blocks = Sim.BlockSeq, sounds = view.BlockSoundCount;
            view.Renderer.FreezeShakeTick = 1;   // 흔들림 4틱 중 2번째 모양(좌우 2논리px)으로 멈춰 캡처
            next.Right = true;
            yield return Frames(2);
            double blockElapsed = EditorApplication.timeSinceStartup - tBlock0;
            float blockRise = Sim.Tracking - trBefore;
            Check("W-014 1-1 second arrow while pushed is blocked: BlockSeq +1, short block sound played (+1), room stays pushed, tracking rose only by Shift time (rise " + blockRise.ToString("0.00") + " in " + blockElapsed.ToString("0.00") + " s, rate 20/s, no extra cost; BlockSeq " + Sim.BlockSeq + ", sounds " + view.BlockSoundCount + ")",
                Sim.BlockSeq == blocks + 1 && view.BlockSoundCount == sounds + 1 && Sim.Pushed && blockRise <= 20f * (float)(blockElapsed + 0.15) && blockRise >= 0f);
            CaptureW14("03-1-1-blocked-shake");
            yield return Frames(1);
            view.Renderer.FreezeShakeTick = -1;

            Advance(() => Sim.TargetX >= 483f, 10f);   // 조력자가 B 복도로 넘어갈 때까지 누른다
            shift = false;
            yield return Frames(3);
            Check("W-014 1-1 Shift released after the runner stepped out: room back home, runner stays in B corridor (col 3)", !Sim.Pushed && !Sim.IsDisplaced(0, 1) && Sim.TargetFloor == 0 && Sim.TargetX >= 483f);

            yield return Jump(Dir.Right, 4, 0);        // B 계단실
            Advance(() => Sim.TargetX >= 671f, 20f);
            shift = true;
            yield return Frames(3);
            Check("W-014 1-1 Shift on the B stairs room: blueprint (not pushed), stairs closed", Sim.IsBlueprint(0, 4) && !Sim.Pushed);
            Advance(() => Sim.TargetX >= 700f, 5f);
            shift = false;
            Advance(() => Sim.MissionDone(1), 20f);
            Check("W-014 1-1 terminal reached with the stairs closed: B-SIDE TERMINAL done, tracking relieved, still floor 0", Sim.MissionDone(1) && Sim.TargetFloor == 0 && Sim.Tracking < 5f);
            Advance(() => Sim.Outcome != Outcome.Running, 90f);
            Check("W-014 1-1 star-3 route in the real view: Complete, 3 stars, TRACE<=40 kept", Sim.Outcome == Outcome.Complete && Sim.Stars == 3 && !Sim.MissionFailed(2));
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "Ended 1-1");
            Check("W-014 1-1 result window, no ending dialogue; 3 stars saved, cleared flag set", view.Desk.Best(2) == 3 && view.Desk.IsCleared(2));
            yield return Press("Enter");
            Check("W-014 result Enter -> CH1 request list (REQ_1-1 selected); REQ_1-2 now unlocked", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder && view.FolderIndex == 1 && view.DeskIndex == 2 && view.Desk.State(3) == RequestState.New);

            // ===== 의뢰 1-2 =====
            yield return Press("Down");
            Check("W-014 Down selects REQ_1-2", view.DeskIndex == 3);
            yield return OpenAndConnectNoBriefing();
            yield return Frames(3);
            Check("W-014 1-2 starts: no guide box (showGuide off, not a tutorial), layout loaded, 3 floors x 5 cols", view.Tut == null && Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch1_old2/layout" && LayoutArtOk(Sim.Board.Layout) && Sim.Board.Floors == 3 && Sim.Board.Cols == 5);
            Check("W-015 1-2 layout uses old-town rooms only (every room key starts with ot_)", OldTownRoomsOnly(Sim.Board.Layout));
            CaptureW14("03-1-2-start");
            yield return Frames(1);
            // --- W-016: 1회차(연출 확인): 켜진 카메라 앞에서 돌아섬 -> 카메라 끄기 -> 구역 안에서 Shift를 떼 발각 -> 처음부터 다시 ---
            yield return Old2RealToLaser();
            yield return Old2RealCameraDemo();
            // --- 2회차(완주): 발각 0회로 UNSEEN을 지키며 계단·옥상 출구 방까지 ---
            yield return Old2RealToLaser();
            yield return Jump(Dir.Right, 4, 1);        // 계단실(카메라·계단)
            Advance(() => Sim.TargetFloor == 1 && Sim.TargetX >= 585f, 20f);
            Check("W-016 1-2 clean pass: camera on before the border, helper has not turned back yet (x " + Sim.TargetX.ToString("0.0") + " < 592), no alert, no detection", Sim.CameraOn(Sim.CameraAt(0)) && Sim.AlertSeq == 0 && Sim.DetectCount == 0);
            shift = true;                              // 카메라 끄기(설계도 상태), 계단을 오를 때까지 누름
            yield return Frames(2);
            Advance(() => Sim.TargetFloor == 0, 20f);
            shift = false;
            yield return Frames(3);
            Check("W-016 1-2 clean pass: the helper went through the camera room and up the stairs with 0 detections, UNSEEN not failed", Sim.TargetFloor == 0 && Sim.DetectCount == 0 && !Sim.MissionFailed(2) && !LogHas("> CAMERA: SPOTTED"));
            yield return Jump(Dir.Left, 3, 1);
            yield return Jump(Dir.Left, 1, 0);        // 옥상 출구 방
            Advance(() => Sim.TargetFloor == 0 && !Sim.TargetFacingRight && Sim.TargetX <= 503f && Sim.TargetX >= 489f, 60f);
            CaptureW14("04-1-2-roof-exit-room");
            yield return Frames(1);
            shift = true;
            yield return Frames(2);
            next.Right = true;
            yield return Frames(2);
            Check("W-014 1-2 Shift + →: the roof exit room is pulled to the empty cell (3F col 2); runner is not inside it", Sim.Pushed && Sim.CellOf(0, 1) == P(2, 0) && Sim.TargetFloor == 0 && Sim.TargetX > 440f);
            yield return Frames(16);
            view.Renderer.FinishSlides();
            yield return Frames(2);
            CaptureW14("05-1-2-exit-room-pulled");
            yield return Frames(1);
            Advance(() => Sim.Outcome != Outcome.Running, 10f);
            shift = false;
            Check("W-014 1-2 the runner walks into the pulled exit room and reaches the exit: Complete (final mission ends the request at once)", Sim.Outcome == Outcome.Complete && Sim.MissionFinal);
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "Ended 1-2");
            Check("W-014 1-2 result: stars saved (best >= 1), cleared flag set", view.Desk.Best(3) >= 1 && view.Desk.IsCleared(3));
            Note("1-2 in the real view: stars " + Sim.Stars + ", tracking at the end " + Sim.Tracking.ToString("0.0"));
        }

        // ----- W-011: 바깥 환경 이펙트(시간대·날씨) 확인 -----

        static string CaptureDir11 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-011"));

        /// <summary>W-011 캡처: 캔버스 출력을 1920x1080 PNG로 저장한다(W-015와 같은 방식).</summary>
        static void CaptureW11(string name)
        {
            Directory.CreateDirectory(CaptureDir11);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir11, name + "_1920x1080.png"), 1920, 1080);
        }

        static Dt.LevelData LoadLevel(string file) => AssetDatabase.LoadAssetAtPath<Dt.LevelData>("Assets/_Project/Data/" + file);

        static bool PartsEqual(EnvironmentFx a, EnvironmentFx b)
        {
            if (a.Parts.Length != b.Parts.Length || a.Slots.Length != b.Slots.Length) return false;
            for (int i = 0; i < a.Parts.Length; i++)
            {
                EnvironmentFx.Particle p = a.Parts[i], q = b.Parts[i];
                if (p.kind != q.kind || p.sx != q.sx || p.sy != q.sy || p.baseX != q.baseX || p.baseY != q.baseY) return false;
            }
            return true;
        }

        static bool AllSnapped(EnvironmentFx fx)
        {
            for (int i = 0; i < fx.Parts.Length; i++)
                if (fx.Parts[i].sx % 2 != 0 || fx.Parts[i].sy % 2 != 0) return false;
            return true;
        }

        static EnvironmentFx.Particle[] Snapshot(EnvironmentFx fx) => (EnvironmentFx.Particle[])fx.Parts.Clone();

        static bool SameXY(EnvironmentFx.Particle[] a, EnvironmentFx.Particle[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].sx != b[i].sx || a[i].sy != b[i].sy) return false;
            return true;
        }

        static string PresetName(Dt.TimeOfDay t, Dt.Weather w) => t + "-" + w;

        static void W011DataChecks()
        {
            var set = AssetDatabase.LoadAssetAtPath<Dt.EnvironmentSet>("Assets/_Project/Data/EnvironmentSet.asset");
            Check("W-011 EnvironmentSet asset exists and the scene's GameView uses it (renderer has the set)", set != null && view.Renderer.EnvSet == set);
            if (set == null) return;
            Check("W-011 EnvironmentSet has exactly 10 presets (day/night x clear/cloudy/rain/snow/fog)", set.presets != null && set.presets.Length == 10);

            // environment-fx §2 표: 하늘 위, 하늘 아래, 실내 어둠 분모, 창 빛 기호, 창 빛 분모, 입자 종류, 입자 색 기호
            var K = Dt.EnvParticleKinds.None;
            var rows = new[]
            {
                new object[] { Dt.TimeOfDay.Day, Dt.Weather.Clear, "b", "C", 0, "W", 8, K, "" },
                new object[] { Dt.TimeOfDay.Day, Dt.Weather.Cloudy, "2", "3", 16, "3", 16, Dt.EnvParticleKinds.Clouds, "W" },
                new object[] { Dt.TimeOfDay.Day, Dt.Weather.Rain, "4", "2", 8, "3", 16, Dt.EnvParticleKinds.Clouds | Dt.EnvParticleKinds.Rain | Dt.EnvParticleKinds.Drips, "C" },
                new object[] { Dt.TimeOfDay.Day, Dt.Weather.Snow, "3", "U", 16, "W", 8, Dt.EnvParticleKinds.Snow, "W" },
                new object[] { Dt.TimeOfDay.Day, Dt.Weather.Fog, "3", "U", 8, "W", 16, Dt.EnvParticleKinds.Fog, "W" },
                new object[] { Dt.TimeOfDay.Night, Dt.Weather.Clear, "a", "e", 4, "b", 16, K, "" },
                new object[] { Dt.TimeOfDay.Night, Dt.Weather.Cloudy, "e", "J", 4, "", 0, Dt.EnvParticleKinds.Clouds, "H" },
                new object[] { Dt.TimeOfDay.Night, Dt.Weather.Rain, "e", "J", 4, "", 0, Dt.EnvParticleKinds.Clouds | Dt.EnvParticleKinds.Rain | Dt.EnvParticleKinds.Drips, "3" },
                new object[] { Dt.TimeOfDay.Night, Dt.Weather.Snow, "J", "H", 4, "U", 16, Dt.EnvParticleKinds.Snow, "W" },
                new object[] { Dt.TimeOfDay.Night, Dt.Weather.Fog, "J", "H", 4, "", 0, Dt.EnvParticleKinds.Fog, "U" },
            };
            int ok = 0;
            string bad = "";
            foreach (object[] r in rows)
            {
                var t = (Dt.TimeOfDay)r[0]; var w = (Dt.Weather)r[1];
                Dt.EnvPreset p = set.Find(t, w);
                bool good = p != null && p.skyTopSym == (string)r[2] && p.skyBottomSym == (string)r[3] && p.ambientDenominator == (int)r[4]
                    && p.beamSym == (string)r[5] && p.beamDenominator == (int)r[6] && p.particles == (Dt.EnvParticleKinds)r[7] && p.particleSym == (string)r[8];
                if (good)
                {
                    // 기호의 실제 색(아트 팔레트)과 저장된 색이 같은가
                    Color32 c;
                    good = Dt.EnvironmentSet.TryPalette(p.skyTopSym, out c) && c.Equals(p.skyTop) && Dt.EnvironmentSet.TryPalette(p.skyBottomSym, out c) && c.Equals(p.skyBottom)
                        && Dt.EnvironmentSet.TryPalette("a", out c) && c.Equals(p.ambientColor)
                        && (p.beamSym == "" || (Dt.EnvironmentSet.TryPalette(p.beamSym, out c) && c.Equals(p.beamColor)))
                        && (p.particleSym == "" || (Dt.EnvironmentSet.TryPalette(p.particleSym, out c) && c.Equals(p.particleColor)));
                }
                if (good) ok++; else bad += " " + PresetName(t, w);
            }
            Check("W-011 all 10 presets match environment-fx §2 (sky top/bottom, indoor dark density, window light color/density, particle kinds/color) and their colors are the art palette values" + (bad.Length > 0 ? " - bad:" + bad : ""), ok == 10);
            Check("W-011 particle counts per 208x144 window = environment-fx §3 (rain 70, drips 8, snow 60, clouds 6, fog bands 3), cap 600, open-air room ot_roofhall",
                set.rainPerWindow == 70 && set.dripsPerWindow == 8 && set.snowPerWindow == 60 && set.cloudsPerWindow == 6 && set.fogBandsPerWindow == 3 && set.maxParticles == 600 && set.IsOpenAir("ot_roofhall"));

            // 의뢰별 기본값(D-103 ⑤): 챕터 0 두 의뢰 밤·비, 1-1 밤·흐림, 1-2 밤·안개
            var want = new Dictionary<string, (Dt.TimeOfDay, Dt.Weather)>
            {
                { "home-0-1", (Dt.TimeOfDay.Night, Dt.Weather.Rain) }, { "home-0-2", (Dt.TimeOfDay.Night, Dt.Weather.Rain) },
                { "old-1-1", (Dt.TimeOfDay.Night, Dt.Weather.Cloudy) }, { "old-1-2", (Dt.TimeOfDay.Night, Dt.Weather.Fog) },
            };
            int found = 0;
            string badL = "";
            for (int i = 0; i < view.Desk.Count; i++)
            {
                string id = view.Desk.Entry(i).id;
                if (!want.TryGetValue(id, out var exp)) continue;
                Dt.LevelData lv = view.Desk.Entry(i).level;
                if (lv != null && lv.timeOfDay == exp.Item1 && lv.weather == exp.Item2) found++; else badL += " " + id;
            }
            Check("W-011 request defaults: 0-1 and 0-2 Night/Rain, 1-1 Night/Cloudy, 1-2 Night/Fog" + (badL.Length > 0 ? " - bad:" + badL : ""), found == 4);
            Dt.LevelData sand = LoadLevel("TestRequest.asset");
            Check("W-011 level default (TestRequest, sandbox levels): Night/Clear", sand != null && sand.timeOfDay == Dt.TimeOfDay.Night && sand.weather == Dt.Weather.Clear);
        }

        static IEnumerator W011Checks()
        {
            W011DataChecks();
            var set = AssetDatabase.LoadAssetAtPath<Dt.EnvironmentSet>("Assets/_Project/Data/EnvironmentSet.asset");
            if (set == null) yield break;
            view.Progress.EnvFxReduced = false;
            shift = false;

            // ===== 챕터 0 의뢰 0-1로 들어가기(브리핑 중 입자가 계속 흐르는지) =====
            if (view.CurrentPhase == GameView.Phase.Ended) yield return Press("Enter");   // 1-2 결과 -> 챕터 1 의뢰 목록
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop (W-011)");
            if (view.InFolder) yield return Press("Esc");   // 챕터 폴더 화면으로
            if (view.FolderIndex != 0) yield return Press("Up");
            yield return Press("Enter");   // CH0 폴더 열기
            Check("W-011 setup: CH0 request list with REQ_0-1 selected", view.InFolder && view.FolderIndex == 0 && view.DeskIndex == 0);
            yield return Press("Enter");   // 패널 열기
            yield return WaitUntil(() => view.PanelSettled, "panel settled (W-011)");
            yield return Press("Enter");   // CONNECT
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "connect -> Briefing (W-011)");
            yield return Frames(3);
            EnvironmentFx env = view.Renderer.Env;
            Check("W-011 0-1 briefing: environment exists (Night/Rain preset, windows found in the layout)", env != null && env.Preset.time == Dt.TimeOfDay.Night && env.Preset.weather == Dt.Weather.Rain && env.Slots.Length > 0);
            if (env == null) yield break;
            double tb0 = env.Time;
            var snapB0 = Snapshot(env);
            yield return Frames(30);
            Check("W-011 particles keep flowing during the briefing dialogue (environment time " + tb0.ToString("0.00") + " -> " + env.Time.ToString("0.00") + ", positions changed)", env.Time > tb0 + 0.1 && !SameXY(snapB0, env.Parts));
            for (int i = 0; i < 4 && view.CurrentPhase == GameView.Phase.Briefing; i++) yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing, "briefing -> Playing (W-011)");
            yield return Frames(20);

            // ===== 0-1 플레이: 시드·격자·정지·Shift·줄이기·메뉴 =====
            Dt.LevelData home1 = view.Desk.Entry(view.ActiveIndex).level;
            env = view.Renderer.Env;
            Check("W-011 0-1 playing: environment is the Night/Rain preset and the sky was drawn", env != null && env.Preset.weather == Dt.Weather.Rain && env.SkyDrawn);
            Check("W-011 particle budget: " + env.Parts.Length + " particles allocated (<= 600) in " + env.Slots.Length + " window(s)", env.Parts.Length > 0 && env.Parts.Length <= 600);

            // 같은 의뢰를 두 번 시작하면 같은 시드 -> 첫 프레임 입자 위치가 같다
            var b1 = new Board(home1);
            var b2 = new Board(home1);
            var fxA = new EnvironmentFx(view.Canvas, b1, env.Preset, set);
            var fxB = new EnvironmentFx(view.Canvas, b2, env.Preset, set);
            Check("W-011 same request started twice: same seed (" + b1.EnvSeed + ") and identical first-frame particle positions (" + fxA.Parts.Length + " particles)", b1.EnvSeed == b2.EnvSeed && b1.EnvSeed == view.Sim.Board.EnvSeed && fxA.Parts.Length > 0 && PartsEqual(fxA, fxB));
            var fxC = new EnvironmentFx(view.Canvas, new Board(LoadLevel("Ch1Old_2.asset")), set.Find(Dt.TimeOfDay.Night, Dt.Weather.Rain), set);
            Check("W-011 a different request has a different seed", fxC.Seed != fxA.Seed);
            var arrBefore = env.Parts;
            env.Reseed();
            Check("W-011 restart (Reseed) returns to the first-frame positions of the same seed and keeps the same array", PartsEqual(fxA, env) && ReferenceEquals(arrBefore, env.Parts) && env.Time == 0.0);
            yield return Frames(10);

            // 모든 입자 위치가 2px 배수
            Check("W-011 every particle position is a multiple of 2 px (2px grid), after " + env.Time.ToString("0.00") + " s", AllSnapped(env));

            // 일시정지 중 멈춤
            var arrRef = env.Parts;
            yield return Press("Esc");
            Check("W-011 setup: Esc -> Paused", view.CurrentPhase == GameView.Phase.Paused);
            yield return Frames(2);
            double tp0 = env.Time;
            var snapP = Snapshot(env);
            yield return Frames(45);
            Check("W-011 particles do not move while paused (environment time " + tp0.ToString("0.000") + " -> " + env.Time.ToString("0.000") + ")", env.Time == tp0 && SameXY(snapP, env.Parts));
            CaptureW11("14-pause-menu");
            yield return Frames(1);

            // 줄이기 토글: W-019부터 일시정지 SETTINGS(index 2) 안의 ENV FX 줄(index 2), 값 저장, 게임 시간 멈춤 유지
            yield return Press("Down"); yield return Press("Down");
            Check("W-011 pause menu: 2 Downs reach SETTINGS (index 2, W-019 order)", view.PauseIndex == 2);
            yield return Press("Enter");
            yield return Press("Down"); yield return Press("Down");
            Check("W-011 settings opened from pause: 2 Downs reach the ENV FX row (index 2)", view.CurrentPhase == GameView.Phase.Settings && view.SettingsIndex == 2);
            yield return Press("Enter");
            Check("W-011 settings ENV FX: Enter turns the reduction ON, stays frozen, value kept", view.Progress.EnvFxReduced && view.GameFrozen);
            Check("W-011 the reduction is saved (a freshly created ProgressStore reads it back as ON)", new ProgressStore().EnvFxReduced);
            yield return Frames(2);
            CaptureW11("14b-pause-menu-env-off");
            yield return Frames(1);
            yield return Press("Enter");
            Check("W-011 settings ENV FX: Enter again turns it OFF (saved)", !view.Progress.EnvFxReduced && !new ProgressStore().EnvFxReduced && view.GameFrozen);
            yield return Press("Esc");   // 설정 -> 일시정지 메뉴
            Check("W-011 setup: Esc in settings -> Paused", view.CurrentPhase == GameView.Phase.Paused);
            yield return Press("Esc");
            Check("W-011 setup: Esc -> Playing", view.CurrentPhase == GameView.Phase.Playing);
            yield return Frames(15);

            // 입자 배열은 의뢰 중 다시 할당되지 않는다(일시정지·재시작 포함)
            yield return Press("Esc");
            yield return Press("Down");
            yield return Press("Enter");   // RESTART
            yield return Frames(5);
            Check("W-011 the particle array is the same object after 60+ frames, a pause and a pause-menu Restart (no re-allocation during the request)", view.Renderer.Env == env && ReferenceEquals(arrRef, view.Renderer.Env.Parts) && view.CurrentPhase == GameView.Phase.Playing);

            // 0-1의 창 방(주인공 방·로비)은 조준할 수 있는 방이 아니라 처음 화면에 안 들어온다. 확인·캡처용으로 (0,0)에 레일을 더한 복제 의뢰(이름·시드 같음)로 다시 시작해 창이 보이게 한다.
            Dt.LevelData cl = UnityEngine.Object.Instantiate(home1);
            cl.name = home1.name;
            var secs = new List<Dt.SectorDef>(cl.sectors) { new Dt.SectorDef { floor = 0, col = 0, target = Dt.SectorTarget.Objects, rail = true } };
            cl.sectors = secs.ToArray();
            view.DebugStartLevel(cl);
            yield return Frames(3);
            for (int i = 0; i < 6 && Sim.Aim.x != 0; i++) Sim.MoveAim(-1, 0);
            yield return Frames(40);
            env = view.Renderer.Env;
            Check("W-011 0-1 (clone with a rail at the home room): aim at (0,0) so the home room window and the lobby window are in view (" + env.LeDrawn + " particles drawn)", Sim.Aim == P(0, 0) && env.LeDrawn > 0 && env.Preset.weather == Dt.Weather.Rain);
            CaptureW11("11-ch0-night-rain");
            yield return Frames(1);

            // Shift 중 절반
            int nFull = env.LeDrawn, gFull = env.GlassDrawn;
            shift = true;
            yield return Frames(4);
            int nHalf = env.LeDrawn, gHalf = env.GlassDrawn;
            CaptureW11("12-shift");
            yield return Frames(1);
            shift = false;
            yield return Frames(4);
            Check("W-011 while Shift is held, half of the particles are drawn (" + nFull + " -> " + nHalf + ", glass drops " + gFull + " -> " + gHalf + ")", nFull >= 10 && Math.Abs(nHalf - (nFull + 1) / 2) <= 1 && Math.Abs(gHalf - (gFull + 1) / 2) <= 1);

            // 줄이기 ON: 입자 0, 하늘·밝기는 그대로
            int nOn = env.LeDrawn;
            view.Progress.EnvFxReduced = true;
            yield return Frames(4);
            Check("W-011 reduction ON: 0 particles drawn (LE, glass, rooftop) while the sky is still drawn with the same colors (was " + nOn + " particles)",
                nOn > 0 && env.LeDrawn == 0 && env.GlassDrawn == 0 && env.SecondDrawn == 0 && env.SkyDrawn && env.LastSkyTop.Equals(env.Preset.skyTop) && env.LastSkyBottom.Equals(env.Preset.skyBottom));
            CaptureW11("13-reduced-on");
            yield return Frames(1);
            view.Progress.EnvFxReduced = false;
            yield return Frames(4);
            Check("W-011 reduction OFF again: particles are drawn again (" + env.LeDrawn + ")", env.LeDrawn == nOn);
            UnityEngine.Object.Destroy(cl);

            // 밤 실내 조명 자동 켜기: layout의 auto는 밤에 켜고 낮에 끈다
            {
                string json = "{\"floors\":1,\"cols\":1,\"sector_px\":[640,384],\"roof_px\":96,\"ground_px\":128,\"floor_y_px\":352,\"tiles\":{\"roof\":\"r\",\"earth\":\"e\"},"
                    + "\"rooms\":{\"x\":{\"L1\":\"a\",\"L2\":\"b\",\"dark\":\"d\",\"L3\":{\"lamp\":\"l\"}}},\"sectors\":[{\"floor\":0,\"col\":0,\"room\":\"x\",\"lights\":{\"lamp\":\"auto\"}}]}";
                bool night = SceneLayout.Parse(json, true).IsLightOn(0, 0, "lamp");
                bool day = SceneLayout.Parse(json, false).IsLightOn(0, 0, "lamp");
                Check("W-011 night auto lighting: a light whose start state is auto is ON at night and OFF by day (no layout uses auto yet; the layout files only have on/off)", night && !day);
            }

            // ===== 1-2: 옥상 입자, 10프리셋 캡처·성능 =====
            Dt.LevelData old2 = LoadLevel("Ch1Old_2.asset");
            view.DebugStartLevel(old2);
            yield return Frames(3);
            for (int i = 0; i < 6 && Sim.Aim != P(4, 1); i++)
                if (!Sim.MoveAim(1, 0)) Sim.MoveAim(0, -1);
            view.Renderer.DebugForceEnv(Dt.TimeOfDay.Night, Dt.Weather.Rain);
            yield return Frames(40);   // 카메라가 따라가고(0.2초) 환경이 다시 만들어질 때까지
            env = view.Renderer.Env;
            Check("W-011 1-2 setup: aim at the stairs room (4,1) so the rooftop hall is in view, preset forced to Night/Rain", Sim.Aim == P(4, 1) && env != null && env.Preset.weather == Dt.Weather.Rain);
            int rooftopSlots = 0;
            for (int i = 0; i < env.Slots.Length; i++) if (env.Slots[i].openAir) rooftopSlots++;
            Check("W-011 1-2: the rooftop hall (ot_roofhall) is an open-air window slot", rooftopSlots == 1);
            Check("W-011 1-2 rooftop: rain is drawn a second time over the rooftop sector (" + env.SecondDrawn + " particles), above L2 and below the objects and the helper (steps LE " + view.Renderer.StepLe + " < second " + view.Renderer.StepSecond + " < objects " + view.Renderer.StepObjects + " < helper " + view.Renderer.StepRunner + ")",
                env.SecondDrawn > 0 && view.Renderer.StepLe > 0 && view.Renderer.StepLe < view.Renderer.StepSecond && view.Renderer.StepSecond < view.Renderer.StepObjects && view.Renderer.StepObjects < view.Renderer.StepRunner);
            var arr12 = env.Parts;
            yield return Frames(60);
            Check("W-011 1-2: the particle array stays the same object for 60 frames", ReferenceEquals(arr12, view.Renderer.Env.Parts));

            var presetInfo = new List<string>();
            double worstEnvMs = 0, baseCpu = 0, baseAll = 0;
            int worstParts = 0;
            foreach (Dt.TimeOfDay t in new[] { Dt.TimeOfDay.Day, Dt.TimeOfDay.Night })
                foreach (Dt.Weather w in new[] { Dt.Weather.Clear, Dt.Weather.Cloudy, Dt.Weather.Rain, Dt.Weather.Snow, Dt.Weather.Fog })
                {
                    view.Renderer.DebugForceEnv(t, w);
                    yield return Frames(30);
                    EnvironmentFx fx = view.Renderer.Env;
                    bool kindsOk = fx != null && fx.Preset.time == t && fx.Preset.weather == w;
                    yield return Frames(1);
                    CaptureW11("01-" + (t == Dt.TimeOfDay.Day ? "day" : "night") + "-" + w.ToString().ToLowerInvariant());
                    yield return Frames(1);
                    float sum = 0f, max = 0f;
                    double envSum = 0, envMax = 0;
                    const int frames = 300;
                    for (int i = 0; i < frames; i++)
                    {
                        yield return null;
                        float dt = Time.unscaledDeltaTime;
                        sum += dt; if (dt > max) max = dt;
                        double em = fx.DrawMs;
                        envSum += em; if (em > envMax) envMax = em;
                    }
                    if (envMax > worstEnvMs) worstEnvMs = envMax;
                    if (fx.Parts.Length > worstParts) worstParts = fx.Parts.Length;
                    // 한 장면 그리기(GameRenderer.Draw + Apply)를 100번: 제출 시간과 GPU 대기 포함 시간. 입자 없는 낮·맑음과의 차이가 입자·밝기를 더한 몫이다
                    const int draws = 100;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < draws; i++) { view.Renderer.Draw(view); view.Canvas.Apply(); }
                    double cpuMs = sw.Elapsed.TotalMilliseconds / draws;
                    view.Canvas.RegionHash(0, 0, 2, 2);
                    double allMs = sw.Elapsed.TotalMilliseconds / draws;
                    if (t == Dt.TimeOfDay.Day && w == Dt.Weather.Clear) { baseCpu = cpuMs; baseAll = allMs; }
                    Note("W-011 preset " + PresetName(t, w) + " in 1-2 (rooftop in view), " + frames + " frames: avg frame " + (sum / frames * 1000f).ToString("0.00") + " ms (max " + (max * 1000f).ToString("0.0") + " ms), environment draw avg " + (envSum / frames).ToString("0.000") + " ms (max " + envMax.ToString("0.000") + " ms), particles allocated " + fx.Parts.Length + ", drawn LE " + fx.LeDrawn + " / glass " + fx.GlassDrawn + " / rooftop " + fx.SecondDrawn
                        + "; scene draw x" + draws + ": CPU submit " + cpuMs.ToString("0.000") + " ms (+" + (cpuMs - baseCpu).ToString("0.000") + " vs Day-Clear), incl. GPU wait " + allMs.ToString("0.000") + " ms (+" + (allMs - baseAll).ToString("0.000") + ")");
                    presetInfo.Add(PresetName(t, w) + (kindsOk ? "" : "?"));
                }
            Check("W-011 all 10 presets were drawn in turn (each started, sky drawn) and the particle budget stayed <= 600 (largest array " + worstParts + ")", presetInfo.Count == 10 && !presetInfo.Exists(s => s.EndsWith("?")) && worstParts <= 600);
            view.Renderer.DebugClearForceEnv();
            view.Progress.EnvFxReduced = false;
            yield return Frames(3);
        }


        // ----- W-019: 메인 메뉴·설정 화면 -----

        static string CaptureDir19 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-019"));

        static void CaptureW19(string name)
        {
            Directory.CreateDirectory(CaptureDir19);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir19, name + "_1920x1080.png"), 1920, 1080);
        }

        /// <summary>어느 화면에서든 Esc·Enter로 메인 화면까지 돌아간다.</summary>
        static IEnumerator ToMain()
        {
            for (int i = 0; i < 20 && view.CurrentPhase != GameView.Phase.Main; i++)
            {
                switch (view.CurrentPhase)
                {
                    case GameView.Phase.Playing: yield return Press("Esc"); break;
                    case GameView.Phase.Paused:
                        for (int k = 0; k < 4 && view.PauseIndex != 3; k++) yield return Press("Down");
                        yield return Press("Enter");
                        break;
                    case GameView.Phase.Briefing:
                    case GameView.Phase.Ending:
                    case GameView.Phase.Ended: yield return Press("Enter"); break;
                    case GameView.Phase.Desktop:
                    case GameView.Phase.Settings:
                    case GameView.Phase.Credits: yield return Press("Esc"); break;
                    default: yield return Frames(3); break;
                }
            }
        }

        static List<string> AllIds()
        {
            var ids = new List<string>();
            for (int i = 0; i < view.Desk.Count; i++) ids.Add(view.Desk.Entry(i).id);
            return ids;
        }

        static bool MainIs(params string[] items)
        {
            if (view.MainItems.Count != items.Length) return false;
            for (int i = 0; i < items.Length; i++) if (view.MainItems[i] != items[i]) return false;
            return true;
        }

        static IEnumerator W019Checks()
        {
            var ids = AllIds();
            view.Progress.EnvFxReduced = false;

            // ----- 메인: 기록 없음 -----
            view.Progress.Clear(ids);
            yield return ToMain();
            yield return Frames(4);
            Check("W-019 no record: main has 3 items (no QUIT in the Editor), first is NEW GAME", MainIs("NEW GAME", "SETTINGS", "CREDITS") && view.MainIndex == 0);
            Check("W-019 QUIT is hidden in the Editor (QuitAvailable false, no QUIT item)", !GameView.QuitAvailable && !new List<string>(view.MainItems).Contains("QUIT"));
            CaptureW19("01-main-no-record");
            yield return Frames(1);

            // ----- 메인: 기록 있음 -----
            view.Progress.SetCleared(ids[0]);
            view.Progress.SetBest(ids[1], 2);
            yield return Frames(4);
            Check("W-019 with a record: main has 4 items, first is CONTINUE", MainIs("CONTINUE", "NEW GAME", "SETTINGS", "CREDITS") && view.MainIndex == 0);
            CaptureW19("02-main-with-record");
            yield return Frames(1);

            // CONTINUE -> 부팅 -> 바탕화면, 기록 유지
            yield return Press("Enter");
            Check("W-019 CONTINUE -> Booting", view.CurrentPhase == GameView.Phase.Booting);
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop (CONTINUE)");
            Check("W-019 CONTINUE: desktop reached, record kept", view.CurrentPhase == GameView.Phase.Desktop && view.Progress.IsCleared(ids[0]) && view.Progress.GetBest(ids[1]) == 2);
            yield return ToMain();
            yield return Frames(2);

            // ----- NEW GAME: 확인 창 -----
            view.Progress.BgmVolume = 3;
            yield return Press("Down");
            Check("W-019 Down selects NEW GAME (index 1)", view.MainIndex == 1 && view.MainItems[1] == "NEW GAME");
            yield return Press("Enter");
            Check("W-019 NEW GAME with a record opens the erase window, first choice is NO", view.EraseConfirm && view.EraseIndex == 0 && view.CurrentPhase == GameView.Phase.Main);
            yield return Frames(3);
            CaptureW19("03-erase-confirm");
            yield return Frames(1);
            yield return Press("Enter");   // NO
            Check("W-019 NO: back to main, record kept", !view.EraseConfirm && view.CurrentPhase == GameView.Phase.Main && view.Progress.IsCleared(ids[0]) && view.Progress.GetBest(ids[1]) == 2);
            yield return Press("Enter");
            yield return Press("Esc");
            Check("W-019 Esc in the erase window: closes it, main, record kept", !view.EraseConfirm && view.CurrentPhase == GameView.Phase.Main && view.Progress.IsCleared(ids[0]));
            yield return Press("Enter");
            yield return Press("Right");
            Check("W-019 Right moves to YES", view.EraseConfirm && view.EraseIndex == 1);
            yield return Press("Enter");   // YES
            Check("W-019 YES: record erased (no cleared flag, 0 stars), BGM setting kept, Booting starts",
                view.CurrentPhase == GameView.Phase.Booting && !view.Progress.IsCleared(ids[0]) && view.Progress.GetBest(ids[1]) == 0 && !view.Progress.HasAnyProgress(ids)
                && view.Progress.BgmVolume == 3 && new ProgressStore().BgmVolume == 3);
            view.Progress.BgmVolume = 7;
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop (after erase)");
            yield return ToMain();
            yield return Frames(4);
            Check("W-019 after erase main shows 3 items again (NEW GAME first)", MainIs("NEW GAME", "SETTINGS", "CREDITS"));

            // ----- 메인에서 설정 -----
            yield return Press("Down");
            yield return Press("Enter");
            Check("W-019 SETTINGS opened from main (not from pause), first row BGM", view.CurrentPhase == GameView.Phase.Settings && !view.SettingsFromPause && view.SettingsIndex == 0);
            yield return Frames(3);
            CaptureW19("04-settings-from-main");
            yield return Frames(1);

            for (int i = 0; i < 5; i++) yield return Press("Right");
            Check("W-019 BGM Right x5 from 7 stops at 10", view.Progress.BgmVolume == 10);
            for (int i = 0; i < 12; i++) yield return Press("Left");
            Check("W-019 BGM Left x12 stops at 0", view.Progress.BgmVolume == 0);
            for (int i = 0; i < 4; i++) yield return Press("Right");
            Check("W-019 BGM saved: a new ProgressStore reads 4", view.Progress.BgmVolume == 4 && new ProgressStore().BgmVolume == 4);
            yield return Press("Down");
            for (int i = 0; i < 5; i++) yield return Press("Right");
            Check("W-019 SFX Right x5 from 7 stops at 10", view.SettingsIndex == 1 && view.Progress.SfxVolume == 10);
            for (int i = 0; i < 12; i++) yield return Press("Left");
            Check("W-019 SFX Left x12 stops at 0", view.Progress.SfxVolume == 0);
            for (int i = 0; i < 9; i++) yield return Press("Right");
            Check("W-019 SFX saved: a new ProgressStore reads 9", view.Progress.SfxVolume == 9 && new ProgressStore().SfxVolume == 9);

            // ENV FX
            yield return Press("Down");
            Check("W-019 Down selects ENV FX row", view.SettingsIndex == 2);
            yield return Press("Enter");
            Check("W-019 ENV FX Enter: same value as EnvFxReduced (ON reduction), saved", view.Progress.EnvFxReduced && new ProgressStore().EnvFxReduced);
            yield return Press("Right");
            Check("W-019 ENV FX Right toggles it back (reduction OFF), saved", !view.Progress.EnvFxReduced && !new ProgressStore().EnvFxReduced);

            // GLITCH
            yield return Press("Down");
            Check("W-019 Down selects GLITCH row, starts NORMAL", view.SettingsIndex == 3 && view.Progress.GlitchSetting == GlitchStrength.Normal && Glitch.Strength == GlitchStrength.Normal);
            yield return Press("Left");
            Check("W-019 GLITCH Left -> LOW (Weak)", view.Progress.GlitchSetting == GlitchStrength.Weak && Glitch.Strength == GlitchStrength.Weak);
            yield return Press("Left");
            yield return Press("Left");
            Check("W-019 GLITCH Left again -> OFF, stops at OFF, saved", Glitch.Strength == GlitchStrength.Off && new ProgressStore().GlitchSetting == GlitchStrength.Off);
            Glitch.PlayFull(Glitch.Transition);
            var gf = new GlitchFrame();
            Check("W-019 GLITCH OFF: no glitch frame is produced (TryGetFrame false)", !Glitch.TryGetFrame(ref gf) && !Glitch.Active);
            yield return Press("Right");
            yield return Press("Right");
            yield return Press("Right");
            Check("W-019 GLITCH Right x3 -> NORMAL (stops), saved", Glitch.Strength == GlitchStrength.Normal && new ProgressStore().GlitchSetting == GlitchStrength.Normal);
            Glitch.PlayFull(Glitch.Transition);
            Check("W-019 GLITCH NORMAL: a glitch frame exists again", Glitch.TryGetFrame(ref gf));
            Glitch.Cancel();

            // CONTROLS
            yield return Press("Down");
            Check("W-019 Down selects CONTROLS row", view.SettingsIndex == 4);
            yield return Press("Enter");
            Check("W-019 CONTROLS Enter opens the controls window", view.ControlsOpen && view.CurrentPhase == GameView.Phase.Settings);
            yield return Frames(3);
            CaptureW19("06-controls");
            yield return Frames(1);
            yield return Press("Esc");
            Check("W-019 Esc closes the controls window (still in settings)", !view.ControlsOpen && view.CurrentPhase == GameView.Phase.Settings);

            // Esc -> 메인(SETTINGS 선택)
            yield return Press("Esc");
            Check("W-019 main: Esc from settings -> main with SETTINGS selected", view.CurrentPhase == GameView.Phase.Main && view.MainItems[view.MainIndex] == "SETTINGS");
            yield return Press("Enter");
            yield return Press("Down"); yield return Press("Down"); yield return Press("Down"); yield return Press("Down"); yield return Press("Down");
            Check("W-019 5 Downs reach BACK (index 5)", view.SettingsIndex == 5);
            yield return Press("Enter");
            Check("W-019 BACK Enter -> main with SETTINGS selected", view.CurrentPhase == GameView.Phase.Main && view.MainItems[view.MainIndex] == "SETTINGS");

            // ----- 일시정지에서 설정 -----
            view.Progress.BgmVolume = 7; view.Progress.SfxVolume = 7;
            yield return Press("Up");
            Check("W-019 Up selects NEW GAME again", view.MainItems[view.MainIndex] == "NEW GAME");
            yield return Press("Enter");   // 기록 없음 -> 바로 시작
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop (W-019 pause leg)");
            yield return Press("Enter");   // 폴더 열기
            yield return OpenAndConnect();
            Check("W-019 pause leg: playing 0-1", view.CurrentPhase == GameView.Phase.Playing);
            yield return Frames(5);
            yield return Press("Esc");
            Check("W-019 Esc -> Paused", view.CurrentPhase == GameView.Phase.Paused);
            yield return Frames(3);
            CaptureW19("07-pause-menu");
            yield return Frames(1);
            yield return Press("Down"); yield return Press("Down");
            yield return Press("Enter");
            Check("W-019 pause SETTINGS opens the settings screen from pause", view.CurrentPhase == GameView.Phase.Settings && view.SettingsFromPause && view.GameFrozen);
            yield return Frames(3);
            CaptureW19("05-settings-from-pause");
            yield return Frames(1);

            // 설정 화면에 있는 동안 게임 시간 멈춤
            float trk = Sim.Tracking;
            Vector2 tpos = new Vector2(Sim.TargetX, Sim.TargetFloor);
            int seq0 = Sim.ResetSeq;
            EnvironmentFx envFx = view.Renderer.Env;
            double envT = envFx != null ? envFx.Time : 0;
            yield return Frames(90);
            Check("W-019 game time stays frozen while the settings screen is open (sim and environment)",
                Mathf.Approximately(Sim.Tracking, trk) && new Vector2(Sim.TargetX, Sim.TargetFloor) == tpos && Sim.ResetSeq == seq0 && (envFx == null || envFx.Time == envT));

            // ENV FX는 일시정지 쪽과 같은 값
            yield return Press("Down"); yield return Press("Down");
            yield return Press("Enter");
            Check("W-019 ENV FX toggled inside pause settings is the shared EnvFxReduced value", view.Progress.EnvFxReduced && new ProgressStore().EnvFxReduced);
            yield return Press("Enter");
            Check("W-019 ENV FX toggled back", !view.Progress.EnvFxReduced);

            // 일시정지 -> SETTINGS -> BACK -> 일시정지(SETTINGS 선택)
            yield return Press("Down"); yield return Press("Down");
            yield return Press("Enter");   // CONTROLS
            Check("W-019 CONTROLS opens inside pause settings", view.ControlsOpen);
            yield return Press("Esc");
            Check("W-019 Esc closes controls (still settings)", !view.ControlsOpen && view.CurrentPhase == GameView.Phase.Settings);
            yield return Press("Down");
            Check("W-019 Down selects BACK", view.SettingsIndex == 5);
            yield return Press("Enter");
            Check("W-019 BACK -> pause menu with SETTINGS selected", view.CurrentPhase == GameView.Phase.Paused && view.PauseIndex == 2);
            yield return Press("Enter");
            yield return Press("Esc");
            Check("W-019 Esc in pause settings -> pause menu with SETTINGS selected", view.CurrentPhase == GameView.Phase.Paused && view.PauseIndex == 2);

            // SFX 0이면 막힘 소리는 센다(횟수는 오름)만 소리를 내지 않는다(음량 0, 재생 생략)
            var blockMethod = typeof(GameView).GetMethod("PlayBlockSound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            view.Progress.SfxVolume = 0;
            int bc = view.BlockSoundCount;
            blockMethod.Invoke(view, null);
            Check("W-019 SFX 0: block sound counted (" + bc + " -> " + view.BlockSoundCount + ") but gain is 0 (playback skipped)", view.BlockSoundCount == bc + 1 && view.LastSfxGain == 0f);
            view.Progress.SfxVolume = 10;
            blockMethod.Invoke(view, null);
            Check("W-019 SFX 10: gain 0.5 (the old fixed volume)", Mathf.Approximately(view.LastSfxGain, 0.5f));
            view.Progress.SfxVolume = 7;
            blockMethod.Invoke(view, null);
            Check("W-019 SFX 7: gain 0.35", Mathf.Approximately(view.LastSfxGain, 0.35f));

            // 정리: 기본 설정으로
            view.Progress.BgmVolume = 7; view.Progress.SfxVolume = 7; view.Progress.EnvFxReduced = false;
            view.Progress.GlitchSetting = GlitchStrength.Normal; Glitch.Strength = GlitchStrength.Normal;
            yield return Press("Esc");
            Check("W-019 Esc -> Playing (end of W-019 checks)", view.CurrentPhase == GameView.Phase.Playing);
        }

        static IEnumerator Run()
        {
            yield return null;
            SnapshotPrefs();   // W-019: 끝날 때 되돌릴 사용자 값을 먼저 저장
            ClearProgress();
            view.Progress.BgmVolume = 7; view.Progress.SfxVolume = 7; view.Progress.EnvFxReduced = false;
            view.Progress.GlitchSetting = GlitchStrength.Normal; Glitch.Strength = GlitchStrength.Normal;   // 기본 설정에서 시작
            PureChecks();
            Ch0PureChecks();
            W14PureChecks();
            W16PureChecks();
            W14RouteChecks();
            Check("start: Main phase", view.CurrentPhase == GameView.Phase.Main);
            yield return Frames(3);
            yield return Frames(1);

            // ESC는 메인에서 아무 동작 없음
            yield return Press("Esc");
            Check("Esc on Main does nothing", view.CurrentPhase == GameView.Phase.Main);

            // 크레딧 왕복
            // (W-019: 메뉴가 NEW GAME / SETTINGS / CREDITS라 CREDITS까지 Down 2번, 돌아올 때 Up 2번)
            yield return Press("Down");
            yield return Press("Down");
            Check("Down x2 selects CREDITS", view.MainIndex == 2);
            yield return Press("Enter");
            Check("CREDITS -> Credits", view.CurrentPhase == GameView.Phase.Credits);
            yield return Press("Esc");
            Check("Esc on Credits -> Main", view.CurrentPhase == GameView.Phase.Main);
            yield return Press("Up");
            yield return Press("Up");
            Check("Up x2 selects the first item (NEW GAME)", view.MainIndex == 0);

            // NEW GAME(예전 START) -> 부팅 -> 바탕화면(챕터 폴더 화면)
            yield return Press("Enter");
            Check("NEW GAME -> Booting", view.CurrentPhase == GameView.Phase.Booting);
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop");
            Check("Booting -> Desktop shows the chapter folder screen (not inside a folder, panel closed)",
                view.CurrentPhase == GameView.Phase.Desktop && !view.InFolder && !view.PanelOpen && view.FolderIndex == 0);
            Check("3 chapter folders: CH0 open, CH1 and CH2 locked (no requests yet)", view.Desk.ChapterOpen(0) && !view.Desk.ChapterOpen(1) && !view.Desk.ChapterOpen(2));
            Check("folder names CH0_HOME / CH1_??? / CH2_???", RequestBoard.ChapterName(0) == "CH0_HOME" && RequestBoard.ChapterName(1) == "CH1_???" && RequestBoard.ChapterName(2) == "CH2_???");
            Check("request list: chapter 0 requests 0-1 and 0-2 first (test level removed); W-014 adds chapter 1 requests 1-1 and 1-2 after them", view.Desk.Count == 4 && view.Desk.Number(0) == "0-1" && view.Desk.Number(1) == "0-2"
                && view.Desk.Entry(0).chapter == 0 && view.Desk.Entry(1).chapter == 0 && view.Desk.Number(2) == "1-1" && view.Desk.Number(3) == "1-2" && view.Desk.Entry(2).chapter == 1 && view.Desk.Entry(3).chapter == 1);
            yield return Frames(3);
            Capture("01-folder-screen");
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
            Check("art set has runner / hatch / hatch blueprint (objects128, W-012)", view.Canvas.Has("sprites/runner_right") && view.Canvas.Has("sprites/runner_left") && view.Canvas.Has("objects128/hatch_closed")
                && view.Canvas.Has("objects128/hatch_open") && view.Canvas.Has("objects128/bp_hatch_open"));
            CheckObjects128Art();
            Check("W-010 0-1 draws with the room layers (layout loaded, all room/tile/door textures in the art set)", Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch0_home1/layout" && LayoutArtOk(Sim.Board.Layout));
            Check("0-1 MISSION tab: no RATING line (rows below the single mission are blank like each other)",
                Sim.Board.Tutorial && Sim.MissionCount == 1 && view.Canvas.RegionHash(444, 114, 70, 9) == view.Canvas.RegionHash(444, 90, 70, 9));
            Capture("20-0-1-start-step1");
            CheckObjectKeys128("0-1 start");
            CaptureW9("01-0-1-start");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            // W-009: 조력자 시트(걷기·기다림·떨어짐 + 좌우)와 4배 해상도 확인
            Check("W-009 runner128 sheets present: walk/wait/fall x right/left; frames 16/24/10 (D-077, 30 fps)",
                view.Canvas.FrameCount("anim/runner128_walk_sheet") == 16 && view.Canvas.FrameCount("anim/runner128_wait_sheet") == 24 && view.Canvas.FrameCount("anim/runner128_fall_sheet") == 10
                && view.Canvas.FrameCount("anim/runner128_walk_left_sheet") == 16 && view.Canvas.FrameCount("anim/runner128_wait_left_sheet") == 24 && view.Canvas.FrameCount("anim/runner128_fall_left_sheet") == 10);
            // 시트는 한 줄 8칸 격자(128px × 8 = 1024px) — Web 최대 텍스처 2048 안
            Check("W-009 sheet textures are grid (wait sheet 1024 px wide = 256 logical, <= 2048 px), fall does not loop, walk loops",
                view.Canvas.WidthOf("anim/runner128_wait_sheet") == 256 && !view.Canvas.Loops("anim/runner128_fall_sheet") && view.Canvas.Loops("anim/runner128_walk_sheet"));
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
                    stObj != null && !stObj.Def.blueprintOn && view.Canvas.Has("objects128/bp_shutter_closed") && Sim.IsBlueprint(1, 3));
            }
            Capture("40-0-1-stairs-shift-overlay");
            CheckObjectKeys128("0-1 stairs Shift overlay");
            CaptureW9("02-0-1-shift-stairs");
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
            shift = true;             // W-012: 해치 섹터 설계도 겹침 캡처(조준만 해 두고 대상은 아직 멀리 있다)
            yield return Frames(3);
            CaptureW9("03-0-1-shift-hatch");
            yield return Frames(1);
            shift = false;
            yield return Frames(2);
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
            Check("W-010 0-2 draws with the room layers (layout loaded, all textures in the art set)", Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch0_home2/layout" && LayoutArtOk(Sim.Board.Layout));
            Check("0-2 MISSION tab: single mission, no RATING line", Sim.Board.Tutorial && Sim.MissionCount == 1 && view.Canvas.RegionHash(444, 114, 70, 9) == view.Canvas.RegionHash(444, 90, 70, 9));
            Capture("29-0-2-start-step1");
            CheckObjectKeys128("0-2 start");
            CaptureW9("04-0-2-start");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            Advance(() => Sim.TargetX >= 215f, 30f);
            shift = true;
            yield return Frames(3);
            {
                SectorObject elObj = FindObj(Sim.Board, GameOff.Data.ObjectKind.Elevator, 1, 1);
                Check("0-2 elevator Shift overlay: blueprint state is stopped -> overlay art is bp_elevator_stopped (art present)",
                    elObj != null && !elObj.Def.blueprintOn && view.Canvas.Has("objects128/bp_elevator_stopped") && Sim.IsBlueprint(1, 1));
            }
            Capture("41-0-2-elevator-shift-overlay");
            CaptureW9("06-0-2-shift-elevator");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            Advance(() => Sim.TargetX >= 260f, 10f);
            shift = false;
            Check("0-2 Shift on the elevator sector stops it -> runner passes by on floor 1", Sim.TargetFloor == 1 && Sim.TargetX >= 260f);
            Advance(() => Sim.TargetWaiting, 20f);
            yield return Frames(3);
            Check("0-2 waits before the laser (x~311); step 2 'SOME ROOMS SHIFT AS A WHOLE...' (no badge)", Sim.TargetWaiting && Mathf.Abs(Sim.TargetX - 311f) < 0.5f && TutIs(1, null, "SOME ROOMS SHIFT AS A WHOLE. AIM AT THE ROOM."));
            Check("W-009 0-2 waiting before the laser: wait animation (not walk)", Sim.TargetWaiting && !Sim.TargetMoving && view.Renderer.RunnerAnim == "wait" && view.Renderer.RunnerSheet == "anim/runner128_wait_sheet");
            Capture("31-0-2-laser-front");
            CaptureW9("05-0-2-wait-laser");
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
            Check("both 0-1 and 0-2 cleared -> CH1 opens now that it has requests (no stars needed, W-014); cleared flags saved", view.Desk.IsCleared(0) && view.Desk.IsCleared(1) && view.Desk.ChapterOpen(1) && view.Desk.IsPlayable(2) && !view.Desk.IsPlayable(3));

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
            yield return Press("Down");   // W-019: DESKTOP은 index 3
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
            useReal = false;

            Note("W-014 diagnostic before the W-013 glitch checks: shift_out still playing = " + Glitch.IsPlaying(Glitch.ShiftOut) + " (a real-keyboard Shift release only 7 frames earlier; at about 8 ms per frame that is 0.06 s < the 0.1 s the preset lasts)");
            yield return GlitchChecks();
            yield return Ch1Run();
            yield return W011Checks();   // W-011: 바깥 환경 이펙트(시간대·날씨)
            yield return W019Checks();   // W-019: 메인 메뉴·설정 화면

            Finish();
        }
    }
}
