using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GameOff.Audio;
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
    /// 결과를 Uni../Logs/w036-smoke.txt에 쓴다(W-036 확인은 W036PureChecks·W036RouteChecks·W036Checks이며 캡처는 Docs/QA/captures/W-036/, 이전 이름 w035-smoke.txt, W-030부터, W-030 확인은 W030PureChecks·W030Checks이며 캡처는 Docs/QA/captures/W-030/, 이전 이름 w029-smoke.txt, W-029 확인은 W029Checks·W029RouteChecks이며 캡처는 Docs/QA/captures/W-029/, 이전 이름 w028-smoke.txt, W-028 확인은 W028Checks이며 캡처는 Docs/QA/captures/W-028/, 이전 이름 w020-smoke.txt, 이전 이름 w019-smoke.txt·w011-smoke.txt, 이전 이름 w017-smoke.txt·w015-smoke.txt·w013-smoke.txt). W-011 확인은 W011Checks이며 캡처는 Docs/QA/captures/W-011/. 캡처는 Docs/QA/captures/W-012/에 저장한다(1920x1080 6장: 0-1 시작·계단 설계도·해치 설계도, 0-2 시작·레이저 앞·엘리베이터 설계도).
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
            EditorPrefs.SetBool(ProgressStore.TestStoreFlag, true);   // W-034: 실제 사용자 저장 값을 쓰지 않는 테스트 전용 저장 공간
            ClearTestStore();
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
                deadline = EditorApplication.timeSinceStartup + 1800;
                GameInput.Override = Provide;
                EditorApplication.update += Watchdog;
                ForceInputFocus(true);
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorPrefs.SetBool(ProgressStore.TestStoreFlag, false);
                GameInput.Override = null;
                EditorApplication.update -= Watchdog;
                ForceInputFocus(false);
            }
        }

        // W-038 점검: Editor 창에 포커스가 없으면 Input System이 키보드를 끄거나 Game 뷰로 보내지 않아
        // [real input] 체크가 실행 환경에 따라 실패했다. 스모크 동안만 포커스와 무관하게 입력을 받게 하고 끝나면 되돌린다(빌드에는 영향 없음).
        static bool focusForced;
        static InputSettings.BackgroundBehavior savedBackground;
        static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;

        static void ForceInputFocus(bool on)
        {
            var s = InputSystem.settings;
            if (s == null) return;
            if (on && !focusForced)
            {
                savedBackground = s.backgroundBehavior;
                savedEditorInput = s.editorInputBehaviorInPlayMode;
                s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                focusForced = true;
            }
            else if (!on && focusForced)
            {
                s.backgroundBehavior = savedBackground;
                s.editorInputBehaviorInPlayMode = savedEditorInput;
                focusForced = false;
            }
        }

        /// <summary>테스트 전용 저장 공간(플래그가 켜져 있는 동안의 ProgressStore)을 비운다. 첫 실행과 같은 빈 상태로 시작하기 위함(W-034).</summary>
        static void ClearTestStore()
        {
            var list = AssetDatabase.LoadAssetAtPath<Dt.RequestList>("Assets/_Project/Data/RequestList.asset");
            var ids = new List<string>();
            if (list != null) foreach (var e in list.entries) ids.Add(e.id);
            new ProgressStore().ClearAll(ids);
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
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w041-smoke.txt"));
            File.WriteAllLines(path, results);
            ClearProgress();
            if (view != null) view.Progress.ClearAll(AllIds());   // W-034: 테스트 전용 저장 공간을 비운다
            Loc.Current = Language.English;
            RestorePrefs();   // W-019: 사용자 PC의 설정·기록 값을 체크 시작 전 값으로 되돌린다
            GameInput.Override = null;
            Tutorial.AutoPass = false;
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
            /// <summary>W-030: 이 Rig를 진행하며 읽은 장치 이용 사건(GameSim.UseSeq)을 모두 모은다.</summary>
            public readonly List<UseEvent> Uses = new List<UseEvent>();
            int useSeenRig;

            public void Collect()
            {
                while (useSeenRig < S.UseSeq)
                {
                    useSeenRig++;
                    if (S.UseSeq - useSeenRig < GameSim.UseHistory) Uses.Add(S.UseAt(useSeenRig));
                }
            }

            public int CountUse(UseKind k)
            {
                int n = 0;
                foreach (UseEvent e in Uses) if (e.Kind == k) n++;
                return n;
            }

            public Rig(string levelPath = "Assets/_Project/Data/TestRequest.asset") : this(AssetDatabase.LoadAssetAtPath<GameOff.Data.LevelData>(levelPath))
            {
            }

            /// <summary>W-038: 화면(GameView)이 쓰는 GameSim을 그대로 감싼다(자동 경로를 화면 위에서 돌려 캡처하기 위함).</summary>
            public Rig(GameSim existing)
            {
                S = existing;
            }

            /// <summary>지금까지 진행한 1/60초 단계 수(W-038: 소요 시간).</summary>
            public int Steps;
            /// <summary>W-038: 경로 단계마다 지난 최대 추적도·Shift 초를 모은 기록(표에 붙임).</summary>
            public string Stages = "";
            public void Stage(string name) { Stages += (Stages.Length > 0 ? " > " : "") + name + " " + Peak.ToString("0") + "/" + ShiftSeconds.ToString("0.0") + "s"; }

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
                bool ok = S.TryPush(dx, dy);
                Collect();
                return ok;
            }

            /// <summary>Shift를 누르고(없으면 시작) 숫자 N 한 번: 조준한 방의 박자 앞당기기 시도(W-035). 성공하면 true.</summary>
            public bool Phase(int n)
            {
                Shift = true;
                S.ShiftHeld = true;
                bool ok = S.TrySetPhase(n);
                Collect();
                return ok;
            }

            /// <summary>Shift를 뗀다(밀린 방이 있으면 제자리로 돌아온다).</summary>
            public void Release()
            {
                Shift = false;
                S.ShiftHeld = false;
                Collect();
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
                    Steps++;
                    Collect();
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
                    Steps++;
                    Collect();
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

        /// <summary>화면 없이: 배치가 쓰는 그림 키가 모두 반입된 PNG로 있는가(W-038).</summary>
        static bool LayoutFilesOk(SceneLayout lay)
        {
            foreach (string k in lay.AllKeys)
                if (AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/" + k + ".png") == null) return false;
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
            int[] floorsOf = { 3, 4 };   // W-031: 0-2는 4줄(3F·2F·1F·B1)
            for (int i = 0; i < 2; i++)
            {
                var level = AssetDatabase.LoadAssetAtPath<GameOff.Data.LevelData>(paths[i]);
                var b = new Board(level);
                string n = "0-" + (i + 1);
                SceneLayout L = b.Layout;
                int rooms = 0, tiles = 0, missing = 0;
                if (L != null)
                    for (int f = 0; f < floorsOf[i]; f++)
                        for (int c = 0; c < 4; c++)
                        {
                            var v = L.Sector(f, c);
                            if (v == null) { missing++; continue; }
                            if (v.Room != null) { rooms++; if (string.IsNullOrEmpty(v.Room.L1) || string.IsNullOrEmpty(v.Room.L2) || string.IsNullOrEmpty(v.Room.Dark)) missing++; }
                            else { tiles++; if (string.IsNullOrEmpty(v.Tile)) missing++; }
                        }
                Check(n + " layout read: layoutKey " + keys[i] + ", " + (floorsOf[i] * 4) + " sectors (" + rooms + " rooms + " + tiles + " earth tiles), none missing", L != null && b.LayoutKey == keys[i] && rooms + tiles == floorsOf[i] * 4 && missing == 0);
                Check(n + " layout sizes match the level: sector 640x384 px, roof 96, ground 128, " + floorsOf[i] + " floors x 4 cols; 1 door prop (home L4_door at floor 0, col 0, x 568)",
                    L != null && L.SectorPxW == 640 && L.SectorPxH == 384 && L.RoofPx == 96 && L.GroundPx == 128 && L.FloorYPx == 352 && L.Floors == floorsOf[i] && L.Cols == 4
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
            r.Jump(0, 1);                                   // W-031: 레이저 방 바로 아래 1F 복도(해치)
            r.Until(() => s.TargetFloor == 2 && s.TargetX <= 420f, 30f);   // 계단으로 1F에 내려와 해치(맵 x400) 앞
            r.Shift = true;
            r.Until(() => s.Outcome != Outcome.Running, 5f);
            r.Shift = false;
        }

        static void Home2PureChecks()
        {
            var r = new Rig(Home2Path);
            GameSim s = r.S;
            Board b = s.Board;
            Check("0-2 map (W-031): 4 floors x 4 cols (3F, 2F, 1F, B1), 640x440, tutorial level", b.Floors == 4 && b.Cols == 4 && b.MapW == 640 && b.MapH == 440 && b.Tutorial && b.BuildingName == "HOME BLDG");
            bool setOk = true;
            int operable = 0;
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++)
                {
                    bool op = b.IsOperable(f, c);
                    if (op) operable++;
                    if (op != ((f == 1 && (c == 1 || c == 2)) || (f == 2 && (c == 1 || c == 2)))) setOk = false;
                }
            Check("0-2 operable sectors (W-031) are exactly 2F elevator (1,1), laser (2,1), 1F elevator (1,2), hatch (2,2) (col, floor); the 2F stairs are always open", setOk && operable == 4);
            Check("0-2 laser sector is whole-type and on in reality, off in blueprint", b.IsWhole(1, 2) && b.Blocked(1, 2, false) && !b.Blocked(1, 2, true));
            Check("0-2 start: floor 1, x 30, facing right, aim = elevator sector (1,1)", s.TargetFloor == 1 && Mathf.Approximately(s.TargetX, 30f) && s.TargetFacingRight && s.Aim == P(1, 1));
            Check("0-2 missions: only the final ENTER_SECTOR (floor 3 = B1, col 2) (W-008, W-031)",
                s.MissionCount == 1 && s.Missions[0].kind == GameOff.Data.MissionKind.EnterSector && s.Missions[0].floor == 3 && s.Missions[0].col == 2);

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
            r.Jump(0, 1);
            Check("0-2 aim jump ↓ from the laser sector to the 1F hatch sector (2,2)", s.Aim == P(2, 2));
            bool downStairs = r.Until(() => s.TargetFloor == 2, 30f);
            Check("0-2 stairs 2F -> 1F (open): runner arrives in the 1F stairwell col 3 x60 (map x540) facing left", downStairs && Mathf.Approximately(s.TargetX, 540f) && !s.TargetFacingRight);
            r.Until(() => s.TargetX <= 420f, 10f);
            r.Shift = true;
            r.Until(() => s.Outcome != Outcome.Running, 5f);
            r.Shift = false;
            Check("0-2 hatch opened on 1F: falls to B1 col 2 (boiler room) x80 facing right -> Complete", s.Outcome == Outcome.Complete && s.TargetFloor == 3 && Mathf.Approximately(s.TargetX, 400f) && s.TargetFacingRight);
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

        /// <summary>W-033: 지금 떠 있는(멈춘) 단계가 index번이고 문구·키 배지가 같은지.</summary>
        static bool TutIs(int index, string keys, string text) =>
            view.Tut != null && view.Tut.Frozen && view.Tut.Index == index && view.Tut.StepText == text && string.Join(" ", Tutorial.KeysOf(view.Tut.StepAt(index))) == keys;

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
            yield return Press("Esc");   // W-032: 스테이지 포커스 -> 챕터 포커스
            yield return Press("Esc");   // 팝업 닫기
            yield return Press("Esc");   // 메인
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
                Check("W-014 1-1 map (W-031): 3 floors x 5 cols (map 800x344), not tutorial, rails, 3 missions (final, terminal, TRACE<=55 since W-031b), guide steps on (showGuide)",
                    b.Floors == 3 && b.Cols == 5 && b.MapW == 800 && b.MapH == 344 && !b.Tutorial && b.HasRails && b.Missions.Length == 3
                    && b.Missions[2].kind == Dt.MissionKind.TraceLimit && Near(b.Missions[2].limit, 55f) && b.BuildingName == "OLD TOWN A/B");
                Check("W-014 1-1 empty cells are exactly col 2 on 3F, 2F and 1F (W-031); rail only on the A end room (3F col 1)",
                    b.IsEmpty(0, 2) && b.IsEmpty(1, 2) && b.IsEmpty(2, 2) && !b.IsEmpty(0, 1) && !b.IsEmpty(1, 1) && !b.IsEmpty(2, 1) && b.HasRail(0, 1) && !b.HasRail(0, 4) && !b.HasRail(1, 0));
                Check("W-014 1-1 operable (aimable) sectors: A end room (rail) and B stairs room", b.IsOperable(0, 1) && b.IsOperable(0, 4) && !b.IsOperable(1, 1));
                Check("W-014 1-1 start (W-031): 1F (floor 2) col 0 x20 facing right, first aim by data = start room (col 0, floor 2)", s.TargetFloor == 2 && Near(s.TargetX, 20f) && s.TargetFacingRight && s.Aim == P(0, 2));
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
                r2.Shift = true; r2.Run(8f);   // W-031: 챕터 1은 초당 14라 100에 닿으려면 7.2초 이상
                Check("W-014 1-1 at TRACE MAX shows no SANDBOX warning (guide-only level; W-033: steps are not in the small box)", r2.S.TraceMaxed && tut.Text(r2.S) == "" && !tut.Warning(r2.S));
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
                Check("W-014 R8/R9: tracking after a blocked push + 0.5 s Shift = 7.0 (Shift time only, no extra cost; chapter 1 rate 14 per second since W-031); aim still locked in Shift (R7)", Near(s.Tracking, 7f, 0.01f) && !s.MoveAim(-1, 0));
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

                // R10: W-036(D-126 [제안])으로 규칙이 바뀌었다 - 밀린 방 안의 계단도 작동한다(W036PureChecks가 새 규칙을 확인). 여기서는 문 확인을 위해 계단 연결을 뗀 같은 레벨을 쓴다
                Dt.LevelData noLink = MakePushLevel(1, 1, 20f, true);
                noLink.objects[0].hasLink = false;
                r = new Rig(noLink); s = r.S;
                door = r.Obj(Dt.ObjectKind.Door, 1, 1);
                r.Push(0, -1);
                bool past = r.Until(() => s.TargetX >= 230f, 15f);
                Check("W-014 R10 (W-036: link removed for this check): a displaced room's stairs without a link do nothing: the runner walks past S1 (x220) on the same floor", past && s.TargetFloor == 0 && s.TargetX >= 230f && s.TargetX < 320f);
                bool wait = r.Until(() => s.TargetWaiting, 15f);
                Check("W-014 R6/R10: pushed room is in reality: the closed door (x150 of the room, map x310) holds the runner at x301", wait && Near(s.TargetX, 301f) && s.TargetFloor == 0);
                var c = new Rig(MakePushLevel(1, 1, 20f, true));
                bool climbed = c.Until(() => c.S.TargetFloor == 0, 20f);
                Check("W-014 R10 control: same room at home, S1 works - the runner climbs to 2F col 0 x60 facing left", climbed && Near(c.S.TargetX, 60f) && !c.S.TargetFacingRight);

                // R10: 도착 방이 제자리를 벗어나면 연결이 작동하지 않는다
                r = new Rig(MakePushLevel(1, 0, 20f, true)); s = r.S;
                r.Push(0, -1);
                bool passed = r.Until(() => s.TargetX >= 100f, 15f);
                Check("W-014 R10 (W-036: arrival cell empty): a link whose arrival room was pushed away (empty cell) does nothing: runner passes S2 (x60) and continues on the same floor, no jump", passed && s.TargetFloor == 1 && s.TargetX >= 100f && s.TargetX < 155f);
                c = new Rig(MakePushLevel(1, 0, 20f, true));
                c.Until(() => c.S.TargetX >= 190f, 15f);
                Check("W-014 R10 control: arrival room at home, S2 works - the runner jumps to B1 x40 (map x200)", c.S.TargetX >= 190f && c.S.TargetX < 205f);

                // R11: 층 금지는 방의 원래 자리 기준
                Dt.LevelData noLinkBan = MakePushLevel(1, 1, 100f, false);
                noLinkBan.objects[0].hasLink = false;   // W-036: 밀린 방의 계단이 이제 작동하므로, 층 금지 확인만 보려고 계단 연결을 뗀다
                r = new Rig(noLinkBan); s = r.S;
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

        // ----- W-031: 라이트 유저 모델 [제안] — 같은 경로에서 Shift를 0.4초 일찍 누르고 0.4초 늦게 뗀다(대상 48px/초 = 19.2px) -----
        const float LightLead = 19.2f;
        const float LightLag = 0.4f;
        static float Lead(bool light) => light ? LightLead : 0f;
        /// <summary>라이트 유저는 0.4초 더 누른 채로 있다가 뗀다(일찍 떼면 결과가 바뀌는 곳도 늦게 떼기만 한다).</summary>
        static void RelLate(Rig r, bool light)
        {
            if (light) r.Run(LightLag);
            r.Release();
        }

        static Rig Old1Run(bool closeStairs, bool light, out bool ok)
        {
            var r = new Rig(Old1LevelPath);
            GameSim s = r.S;
            float lead = Lead(light);
            ok = true;
            r.Jump(1, 0);
            ok &= s.Aim == P(1, 0);
            ok &= r.Until(() => s.TargetFloor == 0 && s.TargetX >= 277f - lead && s.TargetFacingRight, 90f);   // 계단 둘로 3F A 끝 방에 도착한 뒤
            ok &= r.Push(1, 0);                                   // 나르기: 조력자가 A 끝 방에서 오른쪽으로 걸을 때 Shift + →
            ok &= r.Until(() => s.TargetX >= 490f, 10f);          // B 복도에 넘어갈 때까지 누름
            RelLate(r, light);                                     // 넘어간 뒤 뗌(방은 제자리로)
            ok &= !s.IsDisplaced(0, 1);
            if (closeStairs)
            {
                r.Jump(1, 0);
                ok &= s.Aim == P(4, 0);
                ok &= r.Until(() => s.TargetX >= 671f - lead, 20f);
                r.Shift = true; s.ShiftHeld = true;               // B 계단(3F -> 2F) 닫기(설계도 상태)
                ok &= r.Until(() => s.TargetX >= 700f, 5f);
                RelLate(r, light);
                ok &= r.Until(() => s.MissionDone(1), 20f);       // 단말
            }
            ok &= r.Until(() => s.Outcome != Outcome.Running, 120f);
            return r;
        }

        /// <summary>1-1 경로: A 끝 방(3F)을 →로 밀어 대상을 B로 나르고(나르기), 필요하면 B 계단(3F -> 2F)을 닫아 단말을 찍고(탐색), 계단 둘로 1F 로비 출구까지. 별·추적도를 로그에 남긴다.</summary>
        /// <summary>W-030: 자동 경로가 읽은 장치 이용 사건(키 = "1-1 " + 경로 이름 등). 경로를 다시 돌리지 않고 W030PureChecks가 센다.</summary>
        static readonly Dictionary<string, List<UseEvent>> routeUses = new Dictionary<string, List<UseEvent>>();

        static void Old1Route(string label, bool closeStairs, int expectStars, string estimate)
        {
            Rig r = Old1Run(closeStairs, false, out bool ok);
            GameSim s = r.S;
            routeUses["1-1 " + label] = r.Uses;
            bool traceOk = !s.MissionFailed(2);
            Check("W-014 1-1 " + label + ": cleared by the automatic route (Complete, " + expectStars + " stars, final mission done, terminal " + (closeStairs ? "done" : "skipped") + ", TRACE<=55 " + (traceOk ? "kept" : "broken") + ")",
                ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.Stars == expectStars && s.MissionDone(1) == closeStairs && traceOk);
            Note("1-1 " + label + ": Shift total " + r.ShiftSeconds.ToString("0.00") + " s, final tracking " + s.Tracking.ToString("0.0") + ", peak " + r.Peak.ToString("0.0") + ", stars " + s.Stars + " (03-01 §2-3 estimate: " + estimate + ")");
        }

        /// <summary>
        /// 1-2 경로. terminal이면 창고 나르기 2번(되돌리기 포함)으로 단말을 찍는다.
        /// 기본 흐름(W-016): 빈 점포째 위로(올리기) -> 레이저 -> 중계기 -> 계단실 카메라 끄기(조력자가 경계 48px 앞에서 돌아서기 전에 계단실을 조준해 Shift, 계단을 오를 때까지 누름) -> 계단 -> 옥상 출구 방 끌어오기.
        /// 발각 0회로 UNSEEN을 지켜야 한다.
        /// </summary>
        static Rig Old2Run(bool terminal, bool light, out bool ok, out string stop)
        {
            var r = new Rig(Old2LevelPath);
            GameSim s = r.S;
            float lead = Lead(light);
            ok = true;
            stop = "";
            if (terminal)
            {
                ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX >= 277f - lead && s.TargetFacingRight, 90f);
                ok &= r.Push(1, 0);                                // 나르기 1: 골목 위에 놓아 창고로 건너보냄
                ok &= r.Until(() => s.TargetX >= 483f, 10f);
                RelLate(r, light);
                if (!ok) stop = "carry1";
                ok &= r.Until(() => s.MissionDone(1), 30f);        // 단말
                ok &= r.Until(() => !s.TargetFacingRight, 30f);    // 창고 끝(빈 칸)에서 돌아섬
                ok &= r.Until(() => s.TargetX <= 515f + lead, 30f);
                ok &= r.Push(1, 0);                                // 나르기 2: 창고 왼쪽 끝에서 다시 →(방을 골목 위로)
                ok &= r.Until(() => s.TargetX <= 470f, 10f);       // 조력자가 방 안에 들어오면
                RelLate(r, light);                                 // 방 안에 있는 채로 뗌 -> 방과 함께 제자리로(되돌리기)
                if (stop == "" && !ok) stop = "carry2";
                ok &= s.TargetFloor == 2 && s.TargetX < 320f;
            }
            ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX >= 277f - lead && s.TargetX < 310f && s.TargetFacingRight, 90f);
            ok &= r.Push(0, -1);                                   // 올리기
            ok &= s.TargetFloor == 1;
            ok &= r.Until(() => s.TargetX >= 330f, 10f);
            RelLate(r, light);
            if (stop == "" && !ok) stop = "lift";
            r.Jump(1, 0);
            ok &= s.Aim == P(3, 1);                                // 레이저 복도
            ok &= r.Until(() => s.TargetWaiting || (light && s.TargetFloor == 1 && s.TargetX >= 471f - lead), 30f);
            r.Shift = true; s.ShiftHeld = true;
            ok &= r.Until(() => s.TargetX > 490f, 10f);
            RelLate(r, light);
            if (stop == "" && !ok) stop = "laser";
            ok &= r.Until(() => s.RelayUsed, 10f);
            r.Jump(1, 0);
            ok &= s.Aim == P(4, 1);                                // 계단실(카메라·계단)
            ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 585f - lead, 20f);   // 경계(640) 48px 앞(592)에서 돌아서기 전에
            ok &= s.AlertSeq == 0;
            r.Shift = true; s.ShiftHeld = true;                    // 카메라 끄기(설계도 상태)
            ok &= r.Until(() => s.TargetFloor == 0, 10f);          // 구역을 지나 계단 2F -> 3F
            RelLate(r, light);
            if (stop == "" && !ok) stop = "camera/stairs";
            r.Jump(-1, 0); r.Jump(-1, 0);                          // (4,1) -> (3,1) -> 옥상 출구 방
            ok &= s.Aim == P(1, 0);
            ok &= r.Until(() => s.TargetFloor == 0 && !s.TargetFacingRight && s.TargetX <= 503f + lead && s.TargetX >= 489f, 60f);
            ok &= r.Push(1, 0);                                    // 끌어오기: 출구 방을 빈 칸 (3F, 열 2)로
            ok &= s.CellOf(0, 1) == P(2, 0);
            ok &= r.Until(() => s.Outcome != Outcome.Running, 15f);
            if (stop == "" && !ok) stop = "pull";
            return r;
        }

        /// <summary>
        /// 1-2 경로. terminal이면 창고 나르기 2번(되돌리기 포함)으로 단말을 찍는다.
        /// 기본 흐름(W-016): 빈 점포째 위로(올리기) -> 레이저 -> 중계기 -> 계단실 카메라 끄기(조력자가 경계 48px 앞에서 돌아서기 전에 계단실을 조준해 Shift, 계단을 오를 때까지 누름) -> 계단 -> 옥상 출구 방 끌어오기.
        /// 발각 0회로 UNSEEN을 지켜야 한다.
        /// </summary>
        static void Old2Route(string label, bool terminal, int expectStars, string estimate)
        {
            Rig r = Old2Run(terminal, false, out bool ok, out string stop);
            GameSim s = r.S;
            routeUses["1-2 " + label] = r.Uses;
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
                Check("R-C4 control: Shift on then released after the wait ended (camera cooldown 0) detects once more (count " + s.DetectCount + ", tracking " + s.Tracking.ToString("0.0") + " = min(max, " + before.ToString("0.0") + " + 80 + 0.5 s of Shift at the level rate))",
                    off && s.DetectCount == 2 && Near(s.Tracking, Mathf.Min(s.TrackingMax, before + 80f + 10f * s.Board.TrackingRateScale), 0.01f));
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
            yield return Press("Enter");   // CONNECT
            // W-028: 1-1·1-2에 임시 대사가 생겨 브리핑이 먼저 뜬다. 뜨면 Enter로 넘기고 Playing까지 간다
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing || view.CurrentPhase == GameView.Phase.Briefing, "connect -> Briefing or Playing");
            for (int i = 0; i < 4 && view.CurrentPhase == GameView.Phase.Briefing; i++) yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing, "connect -> Playing (chapter 1)");
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
            Check("W-014 chapter 1 folder is open after chapter 0 is cleared (CH1 open; W-038: CH2 stays locked until chapter 1 is cleared)", view.Desk.ChapterOpen(1) && !view.Desk.ChapterOpen(2) && !view.InFolder);
            yield return Press("Enter");   // W-032: 일반 모드 팝업 열기(챕터 포커스)
            yield return Press("Down");
            Check("W-014 Down selects the CH1 folder", view.FolderIndex == 1);
            yield return Press("Enter");
            Check("W-014 Enter on CH1 -> request list with REQ_1-1 selected; list has 12 requests (0-1, 0-2, 1-1 .. 1-6, hidden TEST_2-0, 2-1 .. 2-3; W-038)",
                view.InFolder && view.FolderIndex == 1 && view.DeskIndex == 2 && view.Desk.Count == 12 && view.Desk.RequestName(2) == "REQ_1-1" && view.Desk.RequestName(3) == "REQ_1-2" && view.Desk.RequestName(4) == "REQ_1-3" && view.Desk.RequestName(5) == "REQ_1-4"
                && view.Desk.RequestName(6) == "REQ_1-5" && view.Desk.RequestName(7) == "REQ_1-6");
            Check("W-014 REQ_1-1 playable (NEW), REQ_1-2 locked until 1-1 clears", view.Desk.State(2) == RequestState.New && view.Desk.State(3) == RequestState.Locked && !view.Desk.IsTutorial(2));
            yield return OpenAndConnectNoBriefing();
            yield return Frames(3);

            // ===== 의뢰 1-1 =====
            Check("W-014 1-1 starts (W-028: after the 3-line temporary briefing): Playing on the MISSION tab, header OLD TOWN A/B", view.CurrentTab == GameView.Tab.Mission && view.Shown.Count == 3 && Sim.Board.BuildingName == "OLD TOWN A/B" && view.Desk.Entry(2).missionNames.Length == 3);
            Check("W-014 1-1 draws with the room layers (layout loaded, every room/tile texture in the art set)", Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch1_old1/layout" && LayoutArtOk(Sim.Board.Layout));
            Check("W-014/W-033 1-1 has 5 tutorial steps on a non-tutorial request (auto-pass on in this flow)", view.Tut != null && !Sim.Board.Tutorial && view.Tut.StepCount == 5 && !view.Tut.Frozen);
            Check("W-014 1-1 is not a tutorial: 3 missions listed, star rating shown (RATING row), guide box on", Sim.MissionCount == 3 && !Sim.Board.Tutorial && view.Tut != null);
            Check("W-015 1-1 layout uses old-town rooms only (every room key starts with ot_) and the rail texture is in the art set", OldTownRoomsOnly(Sim.Board.Layout) && view.Canvas.Has("rooms128/common/rail"));
            CaptureW14("01-1-1-start");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            yield return Jump(Dir.Right, 1, 0);   // 시작 방 -> A 끝 방(레일)
            yield return Frames(3);
            Check("W-033 1-1 step 3 data: ShiftArrow, keys SHIFT →, rail room + empty cell", view.Tut.StepAt(2).input == Dt.TutorialInput.ShiftArrow && view.Tut.StepAt(2).key == "SHIFT →" && view.Tut.StepAt(2).focus2 == Dt.TutorialFocus.Cell);
            Advance(() => Sim.TargetFloor == 0 && Sim.TargetX >= 277f && Sim.TargetFacingRight, 40f);
            float tx0 = Sim.TargetX;
            shift = true;
            yield return Frames(2);
            next.Right = true;
            yield return Frames(2);
            Check("W-014 1-1 Shift + →: the A end room slides to the empty cell (col 2) and the runner rides along (x + 160); guide step 3; command log 'PUSH RIGHT'; tracking rising",
                Sim.Pushed && Sim.CellOf(0, 1) == P(2, 0) && Sim.TargetFloor == 0 && Sim.TargetX > tx0 + 150f && LogHas("> PUSH RIGHT") && Sim.Tracking > 0f);
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
            yield return ToEnded();   // W-028: 1-1에 임시 종료 대화(3줄)가 생겨 Enter로 넘긴다
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "Ended 1-1");
            Check("W-014 1-1 result window (after the W-028 ending dialogue); 3 stars saved, cleared flag set", view.Desk.Best(2) == 3 && view.Desk.IsCleared(2));
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
            yield return ToEnded();   // W-028: 1-2에 임시 종료 대화(3줄)가 생겨 Enter로 넘긴다
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
                    case GameView.Phase.Ended:
                    case GameView.Phase.Preview: yield return Press("Enter"); break;   // W-047: 챕터 끝 예고 화면도 Enter로 넘긴다
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
            Check("W-034 Down selects the LANGUAGE row (index 4, between GLITCH and CONTROLS)", view.SettingsIndex == 4);
            yield return Press("Down");
            Check("W-019 Down selects CONTROLS row", view.SettingsIndex == 5);
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
            yield return Press("Down"); yield return Press("Down"); yield return Press("Down"); yield return Press("Down"); yield return Press("Down"); yield return Press("Down");
            Check("W-019 6 Downs reach BACK (index 6; W-034 added LANGUAGE)", view.SettingsIndex == 6);
            yield return Press("Enter");
            Check("W-019 BACK Enter -> main with SETTINGS selected", view.CurrentPhase == GameView.Phase.Main && view.MainItems[view.MainIndex] == "SETTINGS");

            // ----- 일시정지에서 설정 -----
            view.Progress.BgmVolume = 7; view.Progress.SfxVolume = 7;
            yield return Press("Up");
            Check("W-019 Up selects NEW GAME again", view.MainItems[view.MainIndex] == "NEW GAME");
            yield return Press("Enter");   // 기록 없음 -> 바로 시작
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop (W-019 pause leg)");
            yield return Press("Enter");   // W-032: 일반 모드 팝업 열기
            yield return Press("Enter");   // 챕터 0 -> 스테이지 목록
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
            yield return Press("Down"); yield return Press("Down"); yield return Press("Down");   // ENV FX(2) -> GLITCH(3) -> LANGUAGE(4) -> CONTROLS(5)
            yield return Press("Enter");   // CONTROLS
            Check("W-019 CONTROLS opens inside pause settings", view.ControlsOpen);
            yield return Press("Esc");
            Check("W-019 Esc closes controls (still settings)", !view.ControlsOpen && view.CurrentPhase == GameView.Phase.Settings);
            yield return Press("Down");
            Check("W-019 Down selects BACK", view.SettingsIndex == 6);
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

        // ----- W-020: 소리 연결 틀(SfxSet·SfxPlayer·BgmPlayer·Import Audio) -----

        static int logProblems, logExceptions;

        static void OnW020Log(string condition, string stack, LogType type)
        {
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Assert || type == LogType.Exception) logProblems++;
            if (type == LogType.Exception) logExceptions++;
        }

        static int Req(string id) => view.Sfx.RequestCount(id);

        static AudioClip W020TestClip(float seconds)
        {
            int n = (int)(44100 * seconds);
            var c = AudioClip.Create("w020test", n, 1, 44100, false);
            c.SetData(new float[n], 0);
            return c;
        }

        static void WriteTestWav(string path)
        {
            const int n = 2205;   // 0.05초
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE")); w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(44100); w.Write(44100 * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
                for (int i = 0; i < n; i++) w.Write((short)(Mathf.Sin(i * 0.1f) * 8000));
            }
        }

        /// <summary>자동 확인 전용: 비공개 값(추적도·판정 상태)을 reflection으로 바꾼다. 게임 코드는 건드리지 않는다.</summary>
        static void SetSimTracking(float v) => typeof(GameSim).GetProperty("Tracking").GetSetMethod(true).Invoke(Sim, new object[] { v });

        static void ForceSimComplete()
        {
            var md = (bool[])typeof(GameSim).GetField("missionDone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(Sim);
            md[0] = true;
            typeof(GameSim).GetProperty("Outcome").GetSetMethod(true).Invoke(Sim, new object[] { Outcome.Complete });
        }

        static IEnumerator W020Checks()
        {
            logProblems = logExceptions = 0;
            Application.logMessageReceived += OnW020Log;
            var ids = AllIds();
            view.Progress.BgmVolume = 7; view.Progress.SfxVolume = 7; view.Progress.EnvFxReduced = false;
            SfxPlayer sfx = view.Sfx;
            GameOff.Audio.SfxSet set = sfx.Set;

            // ----- 소리 목록 -----
            var missing = new List<string>();
            foreach (string id in GameOff.Audio.SfxSet.AllIds) if (set == null || set.Find(id) == null) missing.Add(id);
            Check("W-020 SfxSet has every sfx-plan ID: " + (GameOff.Audio.SfxSet.AllIds.Length - missing.Count) + " / " + GameOff.Audio.SfxSet.AllIds.Length + (missing.Count > 0 ? " - missing: " + string.Join(",", missing) : ""),
                set != null && missing.Count == 0 && GameOff.Audio.SfxSet.AllIds.Length == 48);
            int loopEntries = 0;
            foreach (string id in GameOff.Audio.SfxSet.AllIds) if (set != null && set.Find(id) != null && set.Find(id).loop) loopEntries++;
            Check("W-020 SfxSet first values: only G04 and E01 loop, U01 pitch +-1 semitone and -8 dB, G08 and A05 0 dB",
                set != null && loopEntries == 2 && set.Find("G04").loop && set.Find("E01").loop && set.Find("U01").pitchRandomSemitones == 1f
                && set.Find("U01").gainDb == -8f && set.Find("G08").gainDb == 0f && set.Find("A05").gainDb == 0f);
            var bgmSet = (GameOff.Audio.BgmSet)typeof(GameView).GetField("bgmSet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(view);
            int slotsFound = 0;
            foreach (string slot in GameOff.Audio.BgmSet.Slots) if (bgmSet != null && bgmSet.Find(slot) != null) slotsFound++;
            Check("W-020 BgmSet has the 8 song slots (" + slotsFound + " / 8)", slotsFound == 8 && GameOff.Audio.BgmSet.Slots.Length == 8);

            // ----- 재생 규칙 -----
            yield return Frames(2);
            logProblems = 0;
            var u04Entry = set.Find("U04");
            bool u04Empty = u04Entry.clips == null || u04Entry.clips.Length == 0;
            int rq = Req("U04"), pl = sfx.PlayedCount("U04");
            sfx.Play("U04");
            yield return Frames(1);
            Check("W-020 an ID without a clip: request count rises (+1), nothing plays, no warning or error log (" + logProblems + " problem logs)",
                Req("U04") == rq + 1 && (!u04Empty || sfx.PlayedCount("U04") == pl) && logProblems == 0);

            yield return Frames(2);
            foreach (string id in new[] { "G08", "A05" })
            {
                var e = set.Find(id);
                bool empty = e.clips == null || e.clips.Length == 0;
                int r0 = Req(id), p0 = sfx.PlayedCount(id);
                sfx.Play(id);
                float expect = 0.35f * Mathf.Pow(10f, e.gainDb / 20f);
                Check("W-020 " + id + " with no clip plays the old code-made sound (request +1, played +1, gain " + sfx.LastGain.ToString("0.000") + " = old 0.35)",
                    Req(id) == r0 + 1 && (!empty || sfx.PlayedCount(id) == p0 + 1) && Mathf.Approximately(sfx.LastGain, expect));
                yield return Frames(2);
            }

            sfx.DebugSetClip("U01", W020TestClip(1f));
            int rr = Req("U01"), pp = sfx.PlayedCount("U01");
            sfx.Play("U01"); sfx.Play("U01"); sfx.Play("U01");
            Check("W-020 the same ID 3 times in one frame: 3 requests, only 1 real playback", Req("U01") == rr + 3 && sfx.PlayedCount("U01") == pp + 1);
            yield return Frames(1);
            sfx.Play("U01");
            Check("W-020 the same ID in the next frame plays again", sfx.PlayedCount("U01") == pp + 2);
            sfx.DebugSetClip("U01", null);
            yield return Frames(2);

            var six = new[] { "U01", "U02", "U03", "U04", "D01", "G01" };
            var before = new int[six.Length];
            for (int i = 0; i < six.Length; i++) { sfx.DebugSetClip(six[i], W020TestClip(1f)); before[i] = sfx.PlayedCount(six[i]); }
            foreach (string id in six) sfx.Play(id);
            int started = 0;
            for (int i = 0; i < six.Length; i++) started += sfx.PlayedCount(six[i]) - before[i];
            Check("W-020 6 different IDs at once: all 6 start but at most 4 sound at the same time (active " + sfx.ActiveVoices + ", oldest cut)", started == 6 && sfx.ActiveVoices <= 4 && sfx.ActiveVoices == 4);
            foreach (string id in six) sfx.DebugSetClip(id, null);
            yield return Frames(2);

            view.Progress.SfxVolume = 0;
            int pg = sfx.PlayedCount("G08"), rg = Req("G08");
            sfx.Play("G08");
            Check("W-020 SfxVolume 0: request counted, gain 0, playback skipped", Req("G08") == rg + 1 && sfx.LastGain == 0f && sfx.PlayedCount("G08") == pg);
            view.Progress.SfxVolume = 7;
            yield return Frames(2);
            sfx.Play("G08");
            Check("W-020 SfxVolume 7: same gain as before (0.35 for a 0 dB sound)", Mathf.Approximately(sfx.LastGain, 0.35f * Mathf.Pow(10f, set.Find("G08").gainDb / 20f)));
            yield return Frames(2);

            // ----- 메인·바탕화면·대화 소리와 곡 -----
            yield return ToMain();
            view.Progress.Clear(ids);
            yield return Frames(4);
            Check("W-020 BGM: main = bgm_title", view.CurrentPhase == GameView.Phase.Main && view.Bgm.CurrentTrack == "bgm_title");
            for (int i = 0; i < 6 && view.MainItems[view.MainIndex] != "NEW GAME"; i++) yield return Press("Up");
            int u01 = Req("U01"), u02 = Req("U02"), u03 = Req("U03"), u04 = Req("U04"), u05 = Req("U05"), u06 = Req("U06"), u07 = Req("U07"), d01 = Req("D01");
            yield return Press("Down");
            Check("W-020 menu cursor move -> U01 (+1)", Req("U01") == u01 + 1);
            yield return Press("Enter");
            Check("W-020 menu confirm (SETTINGS) -> U02 (+1), main/settings keep bgm_title", Req("U02") == u02 + 1 && view.CurrentPhase == GameView.Phase.Settings && view.Bgm.CurrentTrack == "bgm_title");
            yield return Press("Esc");
            Check("W-020 back -> U03 (+1)", Req("U03") == u03 + 1 && view.CurrentPhase == GameView.Phase.Main);
            yield return Press("Up");
            yield return Press("Enter");
            Check("W-020 NEW GAME -> Booting: U02 +1 more and U06 (monitor on) +1, BGM stops (no song during boot)", view.CurrentPhase == GameView.Phase.Booting && Req("U02") == u02 + 2 && Req("U06") == u06 + 1 && view.Bgm.CurrentTrack == "");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop (W-020)");
            yield return Frames(2);
            Check("W-020 BGM: desktop = bgm_desk", view.Bgm.CurrentTrack == "bgm_desk");
            yield return Press("Enter");   // W-032: 일반 모드 팝업 열기
            yield return Press("Enter");   // CH0 -> 스테이지 목록
            Check("W-020 open the popup and CH0 -> U02 (+4 total)", view.InFolder && Req("U02") == u02 + 4 && view.Bgm.CurrentTrack == "bgm_desk");
            yield return Press("Down");
            yield return Press("Enter");   // 잠긴 0-2
            Check("W-020 locked request -> U04 (+1), no connect", Req("U04") == u04 + 1 && !view.Desk.IsPlayable(view.DeskIndex) && view.CurrentPhase == GameView.Phase.Desktop);
            yield return Press("Up");
            yield return Press("Enter");   // 열린 0-1: 바로 접속
            Check("W-020 Enter on 0-1 connects at once (no detail panel step, U05 +0)", Req("U05") == u05 && view.CurrentPhase == GameView.Phase.Connecting);
            Check("W-020 connect -> U07 (+1) and no song while connecting", view.CurrentPhase == GameView.Phase.Connecting && Req("U07") == u07 + 1 && view.Bgm.CurrentTrack == "");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "Briefing (W-020)");
            yield return Frames(2);
            Check("W-020 briefing: first bubble -> D01 (+1), BGM = bgm_briefing", Req("D01") == d01 + 1 && view.Bgm.CurrentTrack == "bgm_briefing");
            int uBefore = Req("U01");
            for (int i = 0; i < 4 && view.CurrentPhase == GameView.Phase.Briefing; i++) yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing, "Playing (W-020)");
            yield return Frames(3);
            Check("W-020 each new bubble -> D01, the last Enter closes the dialogue -> D03 (= U01 +1)", Req("D01") >= d01 + 2 && Req("U01") == uBefore + 1);
            Check("W-020 BGM: chapter 0 request = bgm_ch0", view.CurrentPhase == GameView.Phase.Playing && view.Bgm.CurrentTrack == "bgm_ch0");
            Check("W-020 E01 rain loop is on in a rainy request (0-1 night rain)", view.Sfx.IsLooping("E01"));
            view.Progress.EnvFxReduced = true;
            yield return Frames(2);
            Check("W-020 E01 stops when 'reduce environment effects' is ON", !view.Sfx.IsLooping("E01"));
            view.Progress.EnvFxReduced = false;
            yield return Frames(2);
            Check("W-020 E01 comes back when it is OFF again", view.Sfx.IsLooping("E01"));

            // ----- 조작 소리: 조준·Shift -----
            int g01 = Req("G01"), g02 = Req("G02");
            Dir[] tries = { Dir.Right, Dir.Left, Dir.Up, Dir.Down };
            Dir used = Dir.Right;
            for (int t = 0; t < tries.Length && Req("G01") == g01; t++)
            {
                used = tries[t];
                if (used == Dir.Right) next.Right = true; else if (used == Dir.Left) next.Left = true; else if (used == Dir.Up) next.Up = true; else next.Down = true;
                yield return Frames(2);
            }
            Check("W-020 aim jump -> G01 (+1)", Req("G01") == g01 + 1);
            for (int t = 0; t < 10 && Req("G02") == g02; t++)
            {
                if (used == Dir.Right) next.Right = true; else if (used == Dir.Left) next.Left = true; else if (used == Dir.Up) next.Up = true; else next.Down = true;
                yield return Frames(2);
            }
            Check("W-020 aim jump with no candidate -> G02 (P2, request count up)", Req("G02") > g02);

            int g03 = Req("G03"), g04 = Req("G04"), g05 = Req("G05");
            shift = true;
            yield return Frames(3);
            Check("W-020 Shift down -> G03 (+1), G04 loop switched on (request +1)", Req("G03") == g03 + 1 && Req("G04") == g04 + 1 && view.Sfx.IsLooping("G04"));
            shift = false;
            yield return Frames(3);
            Check("W-020 Shift up -> G05 (+1), G04 loop off", Req("G05") == g05 + 1 && !view.Sfx.IsLooping("G04"));
            shift = true;
            yield return Frames(3);
            Check("W-020 G04 on again while Shift is held", view.Sfx.IsLooping("G04"));
            int pu = Req("U02");
            yield return Press("Esc");
            Check("W-020 pause opens (U08 = U02 +1) and the G04 loop is stopped", view.CurrentPhase == GameView.Phase.Paused && Req("U02") == pu + 1 && !view.Sfx.IsLooping("G04"));
            shift = false;
            float normalTarget = view.Bgm.ComputeTarget(false);
            Check("W-020 BGM paused: still bgm_ch0, target volume is -10 dB (" + view.Bgm.TargetVolume.ToString("0.0000") + " = " + normalTarget.ToString("0.0000") + " x 0.3162)",
                view.Bgm.CurrentTrack == "bgm_ch0" && normalTarget > 0f && Mathf.Abs(view.Bgm.TargetVolume - normalTarget * Mathf.Pow(10f, -10f / 20f)) < 0.0001f);
            int pu3 = Req("U03");
            yield return Press("Esc");
            Check("W-020 pause closes (U08 = U03 +1), BGM target back to normal", view.CurrentPhase == GameView.Phase.Playing && Req("U03") == pu3 + 1 && Mathf.Abs(view.Bgm.TargetVolume - normalTarget) < 0.0001f);
            view.Progress.BgmVolume = 0;
            yield return Frames(2);
            Check("W-020 BgmVolume 0 -> target volume 0", view.Bgm.TargetVolume == 0f);
            view.Progress.BgmVolume = 7;
            yield return Frames(2);

            int a01 = Req("A01");
            yield return WaitUntil(() => Req("A01") > a01, "A01 footsteps (the helper starts walking after 3 s)", 6000);
            Check("W-020 helper walking -> A01 footsteps (P2, request count up)", Req("A01") > a01);

            // 챕터 0 가두기 성공(A10): 결과 전에 종료 대화, 결과 창에서 sting_clear, 튜토리얼이라 별 소리 없음
            int a10 = Req("A10"), t07 = Req("T07");
            ForceSimComplete();
            yield return ToEnded();
            yield return Frames(3);
            Check("W-020 chapter 0 trap succeeded -> A10 (+1), no star sound for a tutorial request, BGM = sting_clear on the result window",
                Req("A10") == a10 + 1 && Req("T07") == t07 && view.CurrentPhase == GameView.Phase.Ended && view.Bgm.CurrentTrack == "sting_clear");

            // ----- 챕터 1: 방 밀기·막힘·카운트다운·게임오버·성공 -----
            Dt.LevelData old1 = LoadLevel("Ch1Old_1.asset");
            view.DebugStartLevel(old1);
            yield return Frames(3);
            Check("W-020 BGM: chapter 1 request = bgm_ch1", view.CurrentPhase == GameView.Phase.Playing && view.Bgm.CurrentTrack == "bgm_ch1");
            yield return Jump(Dir.Right, 1, 0);   // 레일이 있는 방
            yield return Frames(3);
            int g06 = Req("G06"), g07 = Req("G07"), g08 = Req("G08");
            shift = true;
            yield return Frames(2);
            next.Right = true;
            yield return Frames(2);
            Check("W-020 push the room (Shift + arrow) -> G06 (+1)", Sim.Pushed && Req("G06") == g06 + 1);
            next.Right = true;
            yield return Frames(2);
            Check("W-020 push while pushed is blocked -> G08 (+1, the old code-made sound)", Req("G08") == g08 + 1);
            shift = false;
            yield return Frames(3);
            Check("W-020 Shift up returns the room -> G07 (+1)", !Sim.Pushed && Req("G07") == g07 + 1);

            int t01 = Req("T01"), t02 = Req("T02"), t03 = Req("T03"), t04 = Req("T04");
            SetSimTracking(75f);
            yield return Frames(4);
            Check("W-020 tracking over 70 -> T01 (+1) once only", Req("T01") == t01 + 1);
            yield return Frames(6);
            Check("W-020 T01 does not repeat while it stays high", Req("T01") == t01 + 1);
            SetSimTracking(Sim.TrackingMax);
            yield return Frames(4);
            Check("W-020 countdown starts -> T02 (+1), BGM = bgm_alert", Sim.CountdownActive && Req("T02") == t02 + 1 && view.Bgm.CurrentTrack == "bgm_alert");
            yield return WaitUntil(() => Req("T03") >= t03 + 2, "two countdown ticks", 6000);
            Check("W-020 countdown ticks every second -> T03 (" + (Req("T03") - t03) + " so far)", Req("T03") >= t03 + 2 && Sim.CountdownActive && Sim.Outcome == Outcome.Running);
            SetSimTracking(50f);
            yield return Frames(4);
            Check("W-020 countdown released -> T04 (+1), BGM back to the request song bgm_ch1", !Sim.CountdownActive && Req("T04") == t04 + 1 && view.Bgm.CurrentTrack == "bgm_ch1");

            // 사건: 다시 시작 뒤에도 소리 비교 값이 맞춰진다(재시작 직후 가짜 소리 없음)
            int a07 = Req("A07"), a09 = Req("A09");
            view.DebugStartLevel(old1);
            yield return Frames(4);
            Check("W-020 restart: no false sounds from the reset (A07 / A09 unchanged)", Req("A07") == a07 && Req("A09") == a09);

            // 게임오버
            int u02End = Req("U02");
            SetSimTracking(Sim.TrackingMax);
            yield return WaitUntil(() => Sim.Outcome == Outcome.GameOver, "game over (countdown ran out)", 8000);
            yield return Frames(2);
            Check("W-020 game over: BGM = sting_gameover", view.Bgm.CurrentTrack == "sting_gameover");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "Ended after game over (W-020)", 3000);
            Check("W-020 game over window keeps sting_gameover", view.Bgm.CurrentTrack == "sting_gameover");
            yield return Press("Enter");
            Check("W-020 restart after game over: U02 +1, BGM = bgm_ch1 again", Req("U02") == u02End + 1 && view.CurrentPhase == GameView.Phase.Playing && view.Bgm.CurrentTrack == "bgm_ch1");

            // 성공 + 별
            int a12 = Req("A12"), tStars = Req("T07");
            ForceSimComplete();
            yield return ToEnded();
            int stars = Sim.Stars;
            yield return WaitUntil(() => Req("T07") >= tStars + stars, "star sounds", 3000);
            yield return Frames(30);
            Check("W-020 request cleared: A12 (+1), result window BGM = sting_clear, one T07 per filled star (" + (Req("T07") - tStars) + " for " + stars + " stars)",
                Req("A12") == a12 + 1 && view.Bgm.CurrentTrack == "sting_clear" && stars >= 1 && Req("T07") == tStars + stars);
            yield return Press("Enter");
            yield return Frames(2);
            Check("W-020 result Enter -> desktop, BGM = bgm_desk", view.CurrentPhase == GameView.Phase.Desktop && view.Bgm.CurrentTrack == "bgm_desk");

            // ----- 반입 메뉴(Import Audio) -----
            Check("W-020 Import Audio accepts only relative paths under the repository root (absolute, drive and parent paths are refused)",
                AudioSetup.IsSafeRelative("Audio/Export/a.wav") && !AudioSetup.IsSafeRelative("C:/x.wav") && !AudioSetup.IsSafeRelative("/x.wav") && !AudioSetup.IsSafeRelative("../x.wav") && !AudioSetup.IsSafeRelative("a/../../x.wav"));
            string tmp = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/w020-import"));
            Directory.CreateDirectory(Path.Combine(tmp, "Audio"));
            WriteTestWav(Path.Combine(tmp, "Audio/w020test.wav"));
            int g08Clips = set.Find("G08").clips != null ? set.Find("G08").clips.Length : 0;
            var rows = AudioSetup.Import("{\"sfx\": {\"U01\": [\"Audio/w020test.wav\"], \"G08\": []}, \"bgm\": {}}", tmp);
            Debug.Log(AudioSetup.FormatTable(rows));
            var u01Entry = set.Find("U01");
            string clipPath = u01Entry.clips != null && u01Entry.clips.Length > 0 && u01Entry.clips[0] != null ? AssetDatabase.GetAssetPath(u01Entry.clips[0]) : "";
            Check("W-020 Import Audio: a test WAV imported for U01 is linked in SfxSet (" + clipPath + "), the empty G08 array is untouched",
                rows.Count == 1 && rows[0].Ok && u01Entry.clips.Length == 1 && clipPath.StartsWith(AudioSetup.SfxFolder + "/U01_") && (set.Find("G08").clips == null ? 0 : set.Find("G08").clips.Length) == g08Clips);
            var importer = AssetImporter.GetAtPath(clipPath) as AudioImporter;
            Check("W-020 Import Audio: sfx import settings are Decompress On Load and Force To Mono off",
                importer != null && importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad && !importer.forceToMono);
            // 정리: 테스트 파일과 연결 삭제
            u01Entry.clips = new AudioClip[0];
            if (clipPath.Length > 0) AssetDatabase.DeleteAsset(clipPath);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Directory.Delete(tmp, true);
            Check("W-020 Import Audio test cleanup: the test file and the U01 link are gone", set.Find("U01").clips.Length == 0 && clipPath.Length > 0 && !File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", clipPath))));

            Check("W-020 no exception was logged during the W-020 checks (" + logExceptions + ")", logExceptions == 0);
            Application.logMessageReceived -= OnW020Log;
            view.Progress.BgmVolume = 7; view.Progress.SfxVolume = 7; view.Progress.EnvFxReduced = false;
        }

        // ----- W-028: P1 코드 연출 · 챕터 1 임시 대사 · 게임 시작 CRT 켜짐 -----

        static string CaptureDir28 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-028"));

        static void CaptureW28(string name)
        {
            Directory.CreateDirectory(CaptureDir28);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir28, name + "_1920x1080.png"), 1920, 1080);
        }

        static string CaptureDir45 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-045"));

        static void CaptureW45(string name)
        {
            Directory.CreateDirectory(CaptureDir45);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir45, name + "_1920x1080.png"), 1920, 1080);
        }

        static double Now => EditorApplication.timeSinceStartup;

        /// <summary>자동 확인 전용: 판정 결과를 reflection으로 바꾼다(게임 코드는 건드리지 않는다).</summary>
        static void ForceSimOutcome(Outcome o) => typeof(GameSim).GetProperty("Outcome").GetSetMethod(true).Invoke(Sim, new object[] { o });

        static bool[] SimMissionDone() =>
            (bool[])typeof(GameSim).GetField("missionDone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(Sim);

        /// <summary>모든 미션을 완료로 만들고 의뢰를 끝낸다(별 3개).</summary>
        static void ForceSimCompleteAll()
        {
            bool[] md = SimMissionDone();
            for (int i = 0; i < md.Length; i++) md[i] = true;
            ForceSimOutcome(Outcome.Complete);
        }

        static bool LineIs(int shownIndex, string name, string text) =>
            view.Shown.Count > shownIndex && view.Shown[shownIndex].speakerName == name && view.Shown[shownIndex].text == text;

        static IEnumerator W028Checks()
        {
            var ids = AllIds();
            view.Progress.EnvFxReduced = false;
            view.Progress.Clear(ids);
            yield return ToMain();
            yield return Frames(4);
            Check("W-028 start: Main, no record -> NEW GAME first", MainIs("NEW GAME", "SETTINGS", "CREDITS") && view.MainIndex == 0);

            // ===== 3. 게임 시작 CRT 켜짐: 페이드 아웃(1) -> 가로선(2) -> 열림(3) -> 부팅 글자(B) =====
            yield return Press("Enter");
            string seq = "";
            double tBoot = Now, tCrtEnd = -1;
            bool cap1 = false, cap2 = false, cap3 = false, cap6 = false, cap7 = false, tracking = false;   // W-045: cap6·cap7 = 부팅 화면 캡처
            for (int i = 0; i < 1800 && view.CurrentPhase == GameView.Phase.Booting; i++)
            {
                int st = view.CrtStage;
                if (st >= 2 && !tracking) { tracking = true; view.Canvas.TrackKeys = true; view.Canvas.DrawnKeys.Clear(); }   // W-045: 가로선 단계부터 그린 그림 키를 모은다
                if (st == 0 && !cap6 && view.BootSeconds > 0.5f) { cap6 = true; CaptureW45("06-boot-text"); }
                if (st == 0 && !cap7 && view.BootSeconds > 1.3f) { cap7 = true; CaptureW45("07-boot-logo"); }
                char c = st == 0 ? 'B' : (char)('0' + st);
                if (seq.Length == 0 || seq[seq.Length - 1] != c) { seq += c; if (st == 0 && tCrtEnd < 0) tCrtEnd = Now; }
                if (st == 1 && !cap1 && view.CrtStageSeconds > 0.2f) { cap1 = true; CaptureW28("03-crt-fade-out"); }
                if (st == 2 && !cap2 && view.CrtStageSeconds > 0.07f) { cap2 = true; CaptureW28("04-crt-line"); }
                if (st == 3 && !cap3 && view.CrtStageSeconds > 0.12f) { cap3 = true; CaptureW28("05-crt-open"); }
                if (st != 0 && i == 20) next.Enter = true;   // 켜짐 도중 Enter: 무시되어야 한다(단계가 그대로 이어짐)
                yield return null;
            }
            double tBootEnd = Now;
            view.Canvas.TrackKeys = false;
            Check("W-028 3: NEW GAME -> CRT stages in order fade(1) -> line(2) -> open(3) -> boot text (sequence '" + seq + "')", seq.StartsWith("123B"));
            // W-045: 부팅은 화면 전체 연출이다. 이전 방 그림 키를 CRT 열림 단계부터 부팅 끝까지 한 번도 그리지 않고, 부팅 합계는 약 2.4초
            string oldKeys = "";
            foreach (string k in view.Canvas.DrawnKeys)
                if (k.StartsWith("scenes/room_layer") || k.StartsWith("scenes/room_screen_boot") || k == "scenes/room_layer4_hacker_lit") oldKeys += k + " ";
            Check("W-045 boot: no old room picture (scenes/room_layer*, room_screen_boot_*, room_layer4_hacker_lit) is drawn from the CRT line stage to the end of the boot (drawn keys " + view.Canvas.DrawnKeys.Count + ", old: '" + oldKeys.Trim() + "')", tracking && oldKeys.Length == 0);
            double bootSeconds = tBootEnd - tCrtEnd;
            Check("W-045 boot: setting 0.6 s x 4 = 2.4 s (BootTotalSeconds " + view.BootTotalSeconds.ToString("0.00") + "), measured boot screen " + bootSeconds.ToString("0.00") + " s, captures 06-boot-text / 07-boot-logo taken",
                Mathf.Approximately(view.BootTotalSeconds, 2.4f) && tCrtEnd > 0 && bootSeconds > 2.2 && bootSeconds < 3.2 && cap6 && cap7);
            double crtSeconds = tCrtEnd - tBoot;
            Check("W-028 3: CRT-on takes about 0.85 s before the boot text (measured " + crtSeconds.ToString("0.00") + " s)", tCrtEnd > 0 && crtSeconds > 0.7 && crtSeconds < 1.3);
            Check("W-028 3: durations 0.4 / 0.15 / 0.3 s (CrtTotal " + Fx.CrtTotalSeconds.ToString("0.00") + ")", Mathf.Approximately(Fx.CrtFadeSeconds, 0.4f) && Mathf.Approximately(Fx.CrtLineSeconds, 0.15f) && Mathf.Approximately(Fx.CrtOpenSeconds, 0.3f));
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "W-028 Desktop after CRT + boot");
            Check("W-028 3: boot text -> desktop (chapter folder screen); an Enter pressed during the CRT-on was ignored", view.CurrentPhase == GameView.Phase.Desktop && !view.InFolder);

            // ===== 0-1 시작(챕터 0) =====
            yield return Press("Enter");   // W-032: 일반 모드 팝업 열기
            yield return Press("Enter");   // CH0 -> 스테이지 목록
            Check("W-028 CH0 folder opened, 0-1 selected", view.InFolder && view.FolderIndex == 0 && view.DeskIndex == 0);
            yield return OpenAndConnect();
            Check("W-028 0-1 playing", view.CurrentPhase == GameView.Phase.Playing && Sim.Board.Tutorial);

            // ===== 1-9 실패 지점 -> 빨간 사각형 2회 깜빡 -> 재시작(0.4초) =====
            int failFrames0 = view.Renderer.FailRectFrames;
            ForceSimOutcome(Outcome.Failed);
            double tFail = Now;
            yield return null;
            bool blinkStarted = view.FailElapsed >= 0f && view.CurrentPhase == GameView.Phase.Playing && Sim.Outcome == Outcome.Failed;
            int rising = 0;
            bool prevOn = false, restarted = false;
            for (int i = 0; i < 300; i++)
            {
                bool on = view.Renderer.FailRectOn;
                if (on && !prevOn) rising++;
                prevOn = on;
                if (view.FailElapsed < 0f) { restarted = true; break; }
                yield return null;
            }
            double failSeconds = Now - tFail;
            Check("W-028 1-9: fail point -> blink starts first (Playing, Outcome Failed, no restart yet), red rect on/off x2 (" + rising + " on-periods), restart after " + failSeconds.ToString("0.00") + " s",
                blinkStarted && rising == 2 && restarted && failSeconds > 0.35 && failSeconds < 0.65 && view.Renderer.FailRectFrames > failFrames0);
            yield return Frames(3);
            Check("W-028 1-9: after the blink the request restarted as before (Playing, Running, fail notice in the guide box)", view.CurrentPhase == GameView.Phase.Playing && Sim.Outcome == Outcome.Running && view.Tut != null && view.Tut.NoticeActive);

            // ===== 1-10 챕터 0 가두기 성공 -> TRAPPED 0.3초 -> 결과 창 =====
            ForceSimComplete();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending, "W-028 0-1 ending");
            yield return Press("Enter");
            next.Enter = true;   // 마지막 대사 -> 결과
            yield return null;
            bool sawTrapped = false, trappedCap = false;
            float lastTrapped = 0f;
            for (int i = 0; i < 200 && view.CurrentPhase == GameView.Phase.Ended; i++)
            {
                if (view.Renderer.TrappedDrawn)
                {
                    sawTrapped = true; lastTrapped = view.EndedSeconds;
                    if (!trappedCap && view.EndedSeconds > 0.12f) { trappedCap = true; CaptureW28("07-trapped"); }
                }
                else if (sawTrapped) break;
                yield return null;
            }
            Check("W-028 1-10: chapter 0 trap -> TRAPPED stamp shown first (last stamp frame at " + lastTrapped.ToString("0.000") + " s), then the result window", sawTrapped && lastTrapped > 0.2f && lastTrapped < 0.3f + 0.001f && view.CurrentPhase == GameView.Phase.Ended && !view.Renderer.TrappedDrawn);
            yield return WaitUntil(() => view.Renderer.ResultOffsetY == 0 && !view.Renderer.TrappedDrawn, "W-028 tutorial result window settled");
            yield return Frames(3);
            Check("W-028 1-7: tutorial result window settled (no stars)", view.CurrentPhase == GameView.Phase.Ended && view.Sim.Board.Tutorial && view.StarsShown == 0);

            // ===== 챕터 1 열기(0-2 기록) =====
            yield return Press("Enter");   // Ended -> 바탕화면(CH0 목록)
            Check("W-028 result Enter -> request list", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder);
            view.Progress.SetCleared(ids[1]);
            yield return Frames(2);
            yield return Press("Esc");
            yield return Press("Down");
            Check("W-028 CH1 folder is selectable and open", view.FolderIndex == 1 && view.Desk.ChapterOpen(1));
            yield return Press("Enter");
            Check("W-028 CH1 folder opened, 1-1 selected", view.InFolder && view.DeskIndex == 2);

            // ===== 2. 1-1 브리핑·종료 대화(3·3줄) =====
            yield return Press("Enter");   // CONNECT
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "W-028 1-1 briefing");
            Dt.DialogueData dlg11 = view.Desk.Entry(2).dialogue;
            Check("W-028 2: 1-1 briefing starts with the dialogue (3 lines), line 1 BROKER", dlg11 != null && dlg11.briefing.Length == 3 && dlg11.ending.Length == 3 && LineIs(0, "BROKER", "TWO BUILDINGS. NO BRIDGE. GET YOUR RUNNER ACROSS."));
            yield return Press("Enter");
            bool l2 = LineIs(1, "RUNNER", "NO BRIDGE? FINE. I'LL JUMP.");
            yield return Press("Enter");
            bool l3 = view.Shown.Count == 3 && view.Shown[2].speaker == Dt.Speaker.Me && view.Shown[2].text == "DON'T. THE ROOM IS THE BRIDGE." && view.Shown[2].hackerFace == "disgust";
            yield return Press("Enter");
            Check("W-028 2: 1-1 briefing lines 2 (RUNNER) and 3 (ME, disgust), then Playing", l2 && l3 && view.CurrentPhase == GameView.Phase.Playing && view.Shown.Count == 3);
            ForceSimCompleteAll();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending, "W-028 1-1 ending");
            bool e1 = LineIs(3, "RUNNER", "I JUST CROSSED AN ALLEY INSIDE A ROOM.");
            yield return Press("Enter");
            bool e2 = LineIs(4, "BROKER", "PAYMENT SENT.");
            yield return Press("Enter");
            bool e3 = view.Shown.Count == 6 && view.Shown[5].speaker == Dt.Speaker.Me && view.Shown[5].text == "ONE LINE OFF YOUR RECORD.";
            Check("W-028 2: 1-1 ending shows 3 lines (RUNNER, BROKER, ME), still Ending before the last Enter", e1 && e2 && e3 && view.CurrentPhase == GameView.Phase.Ending);

            // ===== 1-7 결과 창(성공): 아래 8px에서 올라옴, 별 하나씩 0.15초 간격 =====
            next.Enter = true;
            yield return null;
            double tEnd = Now;
            int offFirst = -1;
            double offZeroT = -1;
            var starT = new double[4];
            int lastStars = 0;
            bool trappedOnCh1 = false;
            for (int i = 0; i < 400 && view.CurrentPhase == GameView.Phase.Ended; i++)
            {
                if (offFirst < 0) offFirst = view.Renderer.ResultOffsetY;
                if (offZeroT < 0 && view.Renderer.ResultOffsetY == 0) offZeroT = Now;
                if (view.Renderer.TrappedDrawn) trappedOnCh1 = true;
                int ss = Mathf.Min(3, view.StarsShown);
                while (lastStars < ss) { lastStars++; starT[lastStars] = Now; }
                if (lastStars >= 3 && offZeroT > 0) break;
                yield return null;
            }
            Check("W-028 1-7: result window starts 8 px low (" + offFirst + ") and is in place within 0.1 s (+ frame: " + (offZeroT - tEnd).ToString("0.000") + " s)", view.CurrentPhase == GameView.Phase.Ended && offFirst == 8 && offZeroT > 0 && offZeroT - tEnd < 0.2);
            double gap1 = starT[2] - starT[1], gap2 = starT[3] - starT[2];
            Check("W-028 1-7: stars appear one at a time every 0.15 s (3 stars, gaps " + gap1.ToString("0.000") + " / " + gap2.ToString("0.000") + " s)",
                view.Sim.Stars == 3 && lastStars == 3 && Mathf.Abs((float)gap1 - 0.15f) < 0.06f && Mathf.Abs((float)gap2 - 0.15f) < 0.06f);
            Check("W-028 1-10: no TRAPPED stamp for chapter 1 results", !trappedOnCh1);
            yield return Frames(4);
            CaptureW28("06-result-stars");
            yield return Frames(1);

            // ===== 2. 1-2 브리핑·종료 대화(3·3줄) =====
            yield return Press("Enter");   // Ended -> 1-1 목록
            Check("W-028 result Enter -> CH1 list with 1-1 selected", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder && view.DeskIndex == 2 && view.Desk.IsPlayable(3));
            yield return Press("Down");
            Check("W-028 Down selects 1-2", view.DeskIndex == 3);
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "W-028 1-2 briefing");
            Dt.DialogueData dlg12 = view.Desk.Entry(3).dialogue;
            Check("W-028 2: 1-2 briefing starts with the dialogue (3 lines), line 1 BROKER", dlg12 != null && dlg12.briefing.Length == 3 && dlg12.ending.Length == 3 && LineIs(0, "BROKER", "ROOFTOP PICKUP. 2F STAIRWELL HAS A CAMERA."));
            yield return Press("Enter");
            bool m2 = LineIs(1, "RUNNER", "CAMERAS LOVE ME.") && view.Shown[1].hackerFace == "joy";
            yield return Press("Enter");
            bool m3 = view.Shown.Count == 3 && view.Shown[2].speaker == Dt.Speaker.Me && view.Shown[2].text == "THIS ONE DOESN'T. WALK WHEN I SAY." && view.Shown[2].hackerFace == "anger";
            yield return Press("Enter");
            Check("W-028 2: 1-2 briefing lines 2 (RUNNER, joy) and 3 (ME, anger), then Playing", m2 && m3 && view.CurrentPhase == GameView.Phase.Playing);
            ForceSimComplete();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending, "W-028 1-2 ending");
            bool f1 = LineIs(3, "RUNNER", "ROOF! NICE VIEW. NEXT TIME, A REAL DOOR?");
            yield return Press("Enter");
            bool f2 = LineIs(4, "BROKER", "CLEAN. NEXT JOB WITHIN THE HOUR.");   // W-038: 07-01 v0.5.0 교체
            yield return Press("Enter");
            bool f3 = view.Shown.Count == 6 && view.Shown[5].speaker == Dt.Speaker.Me && view.Shown[5].text == "CATCH YOUR BREATH.";   // W-038: 1-2 마지막 줄 교체(LOGGING OFF.는 1-6에만)
            yield return Press("Enter");
            Check("W-028 2: 1-2 ending shows 3 lines (RUNNER, BROKER, ME), then the result window", f1 && f2 && f3 && view.CurrentPhase == GameView.Phase.Ended);
            yield return Frames(20);

            // ===== 1-7 게임오버 창도 올라옴 =====
            view.DebugStartLevel(LoadLevel("Ch1Old_1.asset"));
            yield return Frames(3);
            ForceSimOutcome(Outcome.GameOver);
            int goFirst = -1;
            for (int i = 0; i < 400; i++)
            {
                if (view.CurrentPhase == GameView.Phase.Ended) { goFirst = view.Renderer.ResultOffsetY; break; }
                yield return null;
            }
            yield return WaitUntil(() => view.Renderer.ResultOffsetY == 0, "W-028 game over window in place", 120);
            Check("W-028 1-7: game over window also rises (8 -> 0)", goFirst == 8 && view.Sim.Outcome == Outcome.GameOver && view.Renderer.ResultOffsetY == 0);
            yield return Press("Enter");   // RESTART
            yield return Frames(3);
            Check("W-028 game over Enter restarts as before", view.CurrentPhase == GameView.Phase.Playing && Sim.Outcome == Outcome.Running);

            // ===== 1-1 조준 점프: 조준틀 2틱 보간 + 도착 틱 모서리 반짝 =====
            Vector2Int aim0 = Sim.Aim;
            Vector2Int oldPos = view.Renderer.DebugRoomPos(Sim, aim0.y, aim0.x);
            int flash0 = view.Renderer.CursorFlashFrames;
            double tJump = Now;
            next.Right = true;
            yield return null;
            Vector2Int newPos = view.Renderer.DebugRoomPos(Sim, Sim.Aim.y, Sim.Aim.x);
            Vector2Int mid = view.Renderer.CursorMapPos;
            bool movedAim = Sim.Aim != aim0;
            bool inBetween = mid != newPos && Mathf.Min(oldPos.x, newPos.x) <= mid.x && mid.x <= Mathf.Max(oldPos.x, newPos.x);
            for (int i = 0; i < 40 && view.Renderer.CursorMapPos != view.Renderer.CursorTargetPos; i++) yield return null;
            double jumpSeconds = Now - tJump;
            yield return Frames(2);
            Check("W-028 1-1: aim jump -> cursor draws between the old and new sector first, arrives within 0.1 s (" + jumpSeconds.ToString("0.000") + " s), corner flash on the arrival frame",
                movedAim && inBetween && view.Renderer.CursorMapPos == newPos && jumpSeconds < 0.12 && view.Renderer.CursorFlashFrames > flash0);

            // ===== 1-2·1-3 Shift 누름·뗌: 겹침 +4 / -4 에서 0.1초 안에 끝남, 1-4 게이지 끝 칸 깜빡임 =====
            double tShift = Now;
            shift = true;
            yield return null;
            int inStart = view.Renderer.BlueprintOffsetX;
            for (int i = 0; i < 40 && view.Renderer.BlueprintOffsetX != 0; i++) yield return null;
            double inSeconds = Now - tShift;
            Check("W-028 1-2: Shift down -> blueprint overlay starts at +4 and settles within 0.1 s (" + inSeconds.ToString("0.000") + " s)", inStart == Fx.BlueprintSlidePx && view.Renderer.BlueprintOffsetX == 0 && inSeconds < 0.12 && Sim.IsBlueprint(Sim.Aim.y, Sim.Aim.x));
            bool sawOn = false, sawOff = false;
            double tBlink = Now;
            while (Now - tBlink < 0.8)
            {
                if (view.Renderer.GaugeBlinkActive) { if (view.Renderer.GaugeBlinkOff) sawOff = true; else sawOn = true; }
                yield return null;
            }
            Check("W-028 1-4: Shift held -> the last filled gauge cell blinks (on and off both seen)", sawOn && sawOff && Sim.Tracking > 0f);
            double tRel = Now;
            shift = false;
            yield return null;
            int outStart = view.Renderer.BlueprintOffsetX;
            bool ghost = view.Renderer.BlueprintGhost;
            for (int i = 0; i < 40 && view.Renderer.BlueprintOffsetX != 0; i++) yield return null;
            double outSeconds = Now - tRel;
            Check("W-028 1-3: Shift up -> overlay slides to -4 for 2 more ticks then ends within 0.1 s (" + outSeconds.ToString("0.000") + " s)",
                outStart == -Fx.BlueprintSlidePx && ghost && !view.Renderer.BlueprintGhost && outSeconds < 0.12);
            Check("W-028 1-4: no blink after Shift is released", !view.Renderer.GaugeBlinkActive);

            // ===== 1-8 미션 완료 체크 아이콘 =====
            int pops0 = view.Renderer.CheckPops;
            double tCheck = Now;
            SimMissionDone()[1] = true;
            yield return Frames(2);
            bool popStarted = view.Renderer.CheckPops == pops0 + 1 && view.Renderer.CheckActiveCount == 1;
            yield return WaitUntil(() => view.Renderer.CheckActiveCount == 0, "W-028 check icon ends", 200);
            double checkSeconds = Now - tCheck;
            Check("W-028 1-8: mission 1 done while running -> check icon pops at once and ends after about 0.4 s (" + checkSeconds.ToString("0.00") + " s)", popStarted && checkSeconds > 0.3 && checkSeconds < 0.7);

            // ===== 1-5·1-6 띠와 카운트다운 숫자 =====
            view.DebugStartLevel(LoadLevel("Ch1Old_1.asset"));
            yield return Frames(3);
            Check("W-028 1-5: tracking low -> no edge band", view.Renderer.EdgeBand == 0);
            SetSimTracking(75f);
            yield return Frames(3);
            Check("W-028 1-5: tracking >= 70% -> red dither 1 px band", view.Renderer.EdgeBand == 1 && !Sim.CountdownActive);
            CaptureW28("08-edge-band-70");
            yield return Frames(1);
            SetSimTracking(Sim.TrackingMax);
            for (int i = 0; i < 60 && !Sim.CountdownActive; i++) yield return null;
            yield return null;
            bool band2 = view.Renderer.EdgeBand == 2;
            bool pop1 = view.Renderer.CountdownScale == Fx.CountdownPopScale;
            CaptureW28("09-countdown-pop");
            yield return Frames(1);
            Check("W-028 1-6: countdown -> red solid 2 px band (replaces the dither) and the number is size 4 in its first ticks", Sim.CountdownActive && band2 && pop1);
            yield return WaitGameSeconds(0.25f);
            Check("W-028 1-6: the number is back to size 3 after 3 ticks", view.Renderer.CountdownScale == Fx.CountdownBaseScale && view.Renderer.EdgeBand == 2);
            int cd0 = Mathf.CeilToInt(Sim.Countdown);
            for (int i = 0; i < 400 && Mathf.CeilToInt(Sim.Countdown) == cd0 && Sim.CountdownActive; i++) yield return null;
            yield return null;
            bool pop2 = view.Renderer.CountdownScale == Fx.CountdownPopScale && Mathf.CeilToInt(Sim.Countdown) == cd0 - 1;
            yield return WaitGameSeconds(0.2f);
            Check("W-028 1-6: when the number changes (1 s tick) it pops to size 4 then back to 3 (" + cd0 + " -> " + (cd0 - 1) + ")", pop2 && view.Renderer.CountdownScale == Fx.CountdownBaseScale);
            SetSimTracking(50f);
            yield return Frames(3);
            Check("W-028 1-6: countdown released -> band falls back to none (tracking 50)", !Sim.CountdownActive && view.Renderer.EdgeBand == 0);
        }

        // ===================================================================================================
        // W-029: 챕터 1 의뢰 1-3·1-4(D-111). 03-01 v0.7.0 §2-5·§2-6. 화면 없이 GameSim을 1/60초씩 직접 진행하는 자동 경로 + 화면 흐름(목록·대화·안내·캡처).
        // ===================================================================================================

        const string Old3LevelPath = "Assets/_Project/Data/Ch1Old_3.asset";
        const string Old4LevelPath = "Assets/_Project/Data/Ch1Old_4.asset";

        /// <summary>조준을 (열, 층) 방으로 옮긴다(방향키 점프를 가까운 쪽부터 최대 8번). 성공하면 true. Shift를 뗀 상태에서만 부른다.</summary>
        static bool AimTo(Rig r, int col, int floor)
        {
            r.Shift = false;
            r.S.ShiftHeld = false;
            return AimToSim(r.S, col, floor);
        }

        static bool AimToSim(GameSim sim, int col, int floor)
        {
            for (int i = 0; i < 8 && sim.Aim != P(col, floor); i++)
            {
                Vector2Int a = sim.Aim;
                int dx = col > a.x ? 1 : col < a.x ? -1 : 0;
                int dy = floor > a.y ? 1 : floor < a.y ? -1 : 0;
                Vector2Int before = a;
                if (dx != 0) sim.MoveAim(dx, 0);
                if (sim.Aim == before && dy != 0) sim.MoveAim(0, dy);
                if (sim.Aim == before && dx != 0 && dy != 0) sim.MoveAim(dx, 0);
                if (sim.Aim == before) sim.MoveAim(-dx, -dy);
            }
            return sim.Aim == P(col, floor);
        }

        static string RouteSummary(Rig r)
        {
            GameSim s = r.S;
            return "Shift total " + r.ShiftSeconds.ToString("0.00") + " s, final tracking " + s.Tracking.ToString("0.0") + ", peak " + r.Peak.ToString("0.0")
                + ", stars " + s.Stars + ", detections " + s.DetectCount + ", outcome " + s.Outcome + ", terminal " + (s.MissionDone(1) ? "done" : "skipped")
                + ", TRACE/UNSEEN mission " + (s.MissionFailed(2) ? "failed" : "kept");
        }

        /// <summary>
        /// 1-3 경로. A = 맞바꿈으로 레이저 건너뛰기(빈 점포째 Shift + →), B = 레이저를 Shift로 끄고 걸어가기, C = 탐색(Shift + ↑ 맞바꿈 -> 단말 -> 되돌리기 -> A).
        /// 결과(★·추적도)는 Note로 남기고, 클리어 여부만 PASS/FAIL로 확인한다.
        /// </summary>
        static Rig Old3Route(char mode, out bool ok, out string stop, bool light = false)
        {
            var r = new Rig(Old3LevelPath);
            GameSim s = r.S;
            float lead = Lead(light);
            ok = true;
            stop = "";
            if (mode == 'B')
            {
                ok &= AimTo(r, 2, 1);                               // 레이저 복도
                ok &= r.Until(() => s.TargetWaiting || (light && s.TargetFloor == 1 && s.TargetX >= 311f - lead), 40f);   // 켜진 레이저 앞에서 기다림
                r.Shift = true; s.ShiftHeld = true;                 // 레이저를 설계도(꺼짐)로
                ok &= r.Until(() => s.TargetX > 490f, 15f);         // 복도 한 섹터를 다 걸어 복도(계단)에 들어갈 때까지 누름
                RelLate(r, light);
                if (!ok) stop = "laser";
            }
            else
            {
                ok &= AimTo(r, 1, 1);                               // 빈 점포(레일)
                if (mode == 'c')
                {
                    // 03-01 §2-5 그대로: Shift를 단말과 되돌리기까지 계속 누른다(추적도 100에 닿아 게임오버가 되는지 확인용, 참고 기록).
                    ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 277f && s.TargetFacingRight, 60f);
                    ok &= r.Push(0, -1);                            // 맞바꿈: 빈 점포 <-> 3F 복도(조력자는 방과 함께 3F로)
                    ok &= s.TargetFloor == 0;
                    ok &= r.Until(() => s.TargetX < 150f, 40f);     // 3F 셋방으로 나감
                    ok &= r.Until(() => s.MissionDone(1), 40f);     // 단말
                    ok &= r.Until(() => s.TargetFacingRight && s.TargetX >= 165f, 40f);   // 끝 벽에서 돌아서 빈 점포(3F 위치)로 다시 들어옴
                    r.Release();                                    // 방 안에 있는 채로 뗌 -> 방과 함께 2F로(되돌리기)
                    ok &= s.TargetFloor == 1;
                    if (!ok) stop = "upstairs";
                }
                else if (mode == 'C' || mode == 'G')
                {
                    // (G = 탐색 뒤 레이저를 끄고 걸어 건너기. 레이저를 먼저 건너면 단말로 돌아갈 수 없어 순서는 C 다음 B) 같은 동사로 Shift를 두 번 나눠 누르는 변형: 올라가서 셋방으로 나가면 뗌 -> 단말 -> 3F 복도에서 왼쪽으로 걸을 때 다시 Shift + ↑(복도째 2F로) -> 상점으로 나가면 뗌 -> A
                    ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 305f - lead && s.TargetFacingRight, 60f);
                    ok &= r.Push(0, -1);
                    ok &= s.TargetFloor == 0;
                    ok &= r.Until(() => s.TargetX < 158f, 40f);     // 3F 셋방으로 나감
                    RelLate(r, light);
                    ok &= r.Until(() => s.MissionDone(1), 40f);     // 단말
                    ok &= r.Until(() => s.TargetFacingRight, 40f);  // 왼쪽 끝 벽에서 돌아섬
                    ok &= r.Until(() => s.TargetFacingRight && s.TargetX >= 200f, 40f);   // 3F 복도 안
                    ok &= r.Until(() => !s.TargetFacingRight && s.TargetX <= 175f + lead, 40f);  // 복도 오른쪽 벽에서 돌아선 뒤 왼쪽 끝 가까이(짧게 누르기 위해)
                    ok &= AimTo(r, 1, 1);
                    ok &= r.Push(0, -1);                            // 맞바꿈: 빈 점포(2F 자리) <-> 복도, 조력자는 복도와 함께 2F로
                    ok &= s.TargetFloor == 1;
                    ok &= r.Until(() => s.TargetX < 158f, 40f);     // 2F 상점으로 나감
                    RelLate(r, light);
                    if (!ok) stop = "upstairs";
                }
                if (mode == 'G')
                {
                    ok &= AimTo(r, 2, 1);                           // 레이저 복도
                    ok &= r.Until(() => s.TargetFloor == 1 && (s.TargetWaiting || (light && s.TargetX >= 311f - lead)), 60f);
                    r.Shift = true; s.ShiftHeld = true;
                    ok &= r.Until(() => s.TargetX > 490f, 15f);
                    RelLate(r, light);
                    if (stop == "" && !ok) stop = "laser";
                }
                else
                {
                ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 277f - lead && s.TargetFacingRight, 60f);
                ok &= r.Push(1, 0);                                 // 맞바꿈: 빈 점포(조력자 포함) <-> 레이저 복도
                ok &= r.Until(() => s.TargetX >= 490f, 10f);        // 복도(계단)에 걸어 나갈 때까지 누름
                RelLate(r, light);
                if (stop == "" && !ok) stop = "swap";
                }
            }
            ok &= r.Until(() => s.Outcome != Outcome.Running, 120f);   // 복도 계단으로 1F 로비에 내려가 출구까지
            if (stop == "" && !ok) stop = "exit";
            if (!light) routeUses["1-3 " + mode] = r.Uses;
            return r;
        }

        /// <summary>
        /// 1-4 경로. D = 기본(레이저 -> 중계기 -> 카메라 끄기·계단 -> 옥상 출구 방 끌어오기), E = 탐색(Shift + ↑로 복도째 1F -> 단말 -> 되돌리기 -> D).
        /// </summary>
        static Rig Old4Route(char mode, out bool ok, out string stop, bool light = false)
        {
            var r = new Rig(Old4LevelPath);
            GameSim s = r.S;
            float lead = Lead(light);
            ok = true;
            stop = "";
            if (mode == 'e')
            {
                // 03-01 §2-6 그대로: Shift를 단말과 되돌리기까지 계속 누른다(게임오버가 되는지 확인용, 참고 기록).
                ok &= AimTo(r, 1, 3);                               // 1F 빈 점포
                ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX >= 290f && s.TargetX < 315f && s.TargetFacingRight, 60f);   // 2F 복도 오른쪽 끝
                ok &= r.Push(0, -1);                                // 맞바꿈: 빈 점포 <-> 2F 복도, 조력자는 복도와 함께 1F로
                ok &= s.TargetFloor == 3;
                ok &= r.Until(() => s.MissionDone(1), 40f);         // 창고 단말
                ok &= r.Until(() => !s.TargetFacingRight, 40f);     // 끝 벽(빈 칸)에서 돌아섬
                ok &= r.Until(() => s.TargetX <= 315f && s.TargetFloor == 3, 40f);   // 1F 복도(맞바꿈 위치)로 다시 들어옴
                r.Release();                                        // 복도째 2F로 되돌아옴
                ok &= s.TargetFloor == 2;
                if (!ok) stop = "cellar";
            }
            else if (mode == 'E')
            {
                // 같은 동사로 Shift를 두 번 나눠 누르는 변형: 복도째 1F로 내려가 창고로 나가면 뗌 -> 단말 -> 1F 빈 점포 안에서 왼쪽으로 걸을 때 Shift + ↑(빈 점포째 2F로) -> 2F 셋방으로 나가면 뗌 -> D
                ok &= AimTo(r, 1, 3);                               // 1F 빈 점포
                ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX >= 290f - lead && s.TargetX < 315f && s.TargetFacingRight, 60f);
                ok &= r.Push(0, -1);
                ok &= s.TargetFloor == 3;
                ok &= r.Until(() => s.TargetX >= 330f, 10f);        // 창고로 나감
                RelLate(r, light);
                ok &= r.Until(() => s.MissionDone(1), 40f);         // 창고 단말
                ok &= r.Until(() => !s.TargetFacingRight, 40f);     // 끝 벽(빈 칸)에서 돌아섬
                ok &= r.Until(() => s.TargetX <= 175f + lead && s.TargetFloor == 3, 40f);   // 1F 빈 점포(제자리) 안, 왼쪽 끝 가까이(짧게 누르기 위해)
                ok &= AimTo(r, 1, 3);
                ok &= r.Push(0, -1);                                // 맞바꿈: 빈 점포(조력자 포함) <-> 2F 복도, 조력자는 2F로
                ok &= s.TargetFloor == 2;
                ok &= r.Until(() => s.TargetX < 158f, 40f);         // 2F 셋방으로 나감
                RelLate(r, light);
                if (!ok) stop = "cellar";
            }
            ok &= AimTo(r, 2, 2);                                   // 레이저 복도
            ok &= r.Until(() => s.TargetFloor == 2 && (s.TargetWaiting || (light && s.TargetX >= 311f - lead)), 90f);
            r.Shift = true; s.ShiftHeld = true;
            ok &= r.Until(() => s.TargetX > 330f, 10f);
            RelLate(r, light);
            if (stop == "" && !ok) stop = "laser";
            ok &= r.Until(() => s.RelayUsed, 10f);
            ok &= AimTo(r, 3, 2);                                   // 계단실(카메라·계단)
            ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX >= 425f - lead, 20f);   // 경계(480) 48px 앞(432)에서 돌아서기 전에
            ok &= s.AlertSeq == 0;
            r.Shift = true; s.ShiftHeld = true;                     // 카메라 끄기(설계도 상태)
            ok &= r.Until(() => s.TargetFloor == 1, 10f);           // 구역을 지나 계단 2F -> 3F
            RelLate(r, light);
            if (stop == "" && !ok) stop = "camera/stairs";
            ok &= AimTo(r, 2, 1);                                   // 3F 복도(셔터)
            ok &= r.Until(() => s.TargetFloor == 1 && (s.TargetWaiting || (light && s.TargetX <= 429f + lead)), 30f);   // 닫힌 셔터(맵 x420) 앞에서 기다림(x429)
            r.Shift = true; s.ShiftHeld = true;                     // 셔터 열기(설계도 상태)
            ok &= r.Until(() => s.TargetX <= 410f, 10f);
            RelLate(r, light);
            if (stop == "" && !ok) stop = "shutter";
            ok &= AimTo(r, 0, 0);                                   // 옥상 출구 방
            ok &= r.Until(() => s.TargetFloor == 0 && !s.TargetFacingRight && s.TargetX <= 343f + lead && s.TargetX >= 329f, 60f);   // 계단 3F -> 4F 뒤 옥상 복도 왼쪽 끝 벽 앞
            ok &= r.Push(1, 0);                                     // 끌어오기: 출구 방을 빈 칸 (4F, 열 1)로
            ok &= s.CellOf(0, 0) == P(1, 0);
            ok &= r.Until(() => s.Outcome != Outcome.Running, 15f);
            if (stop == "" && !ok) stop = "pull";
            if (!light) routeUses["1-4 " + mode] = r.Uses;
            return r;
        }

        // ----- W-031: 0-2 경로(해치 한 번에 / 해치를 놓쳐 1F 엘리베이터를 멈추고 두 번째 기회) -----

        /// <summary>
        /// 0-2 경로. miss가 false면 2F 엘리베이터 멈춤 -> 레이저 끄기·중계기 -> 계단 2F -> 1F -> 해치 열기(Shift).
        /// miss가 true면 해치를 그냥 지나가 1F 엘리베이터를 멈추고(Shift) 로비 끝 벽에서 돌아선 뒤 다시 엘리베이터를 멈추고 해치 두 번째 기회로 연다.
        /// </summary>
        static Rig Home2Route(bool miss, bool light, out bool ok, out string stop)
        {
            var r = new Rig(Home2Path);
            GameSim s = r.S;
            float lead = Lead(light);
            ok = true;
            stop = "";
            ok &= r.Until(() => s.TargetX >= 215f - lead, 60f);     // 2F 엘리베이터(맵 x240) 멈추기
            r.Shift = true; s.ShiftHeld = true;
            ok &= r.Until(() => s.TargetX >= 260f, 10f);
            RelLate(r, light);
            ok &= s.TargetFloor == 1;
            if (!ok) stop = "elevator2F";
            r.Jump(1, 0);                                           // 레이저 방
            ok &= s.Aim == P(2, 1);
            ok &= r.Until(() => s.TargetWaiting || s.TargetX >= 311f - lead, 30f);
            r.Shift = true; s.ShiftHeld = true;
            ok &= r.Until(() => s.TargetX > 330f, 10f);
            RelLate(r, light);
            if (stop == "" && !ok) stop = "laser";
            ok &= r.Until(() => s.RelayUsed, 10f);
            r.Jump(0, 1);                                           // 바로 아래 1F 복도(해치)
            ok &= s.Aim == P(2, 2);
            if (!miss)
            {
                ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX <= 420f + lead && !s.TargetFacingRight, 30f);   // 계단으로 1F에 내려와 왼쪽으로 걸어 해치(맵 x400) 앞
                r.Shift = true; s.ShiftHeld = true;
                ok &= r.Until(() => s.Outcome != Outcome.Running, 5f);
                if (stop == "" && !ok) stop = "hatch";
            }
            else
            {
                ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX <= 385f, 30f);   // 해치를 그냥 지나감
                r.Jump(-1, 0);                                       // 1F 엘리베이터 방
                ok &= s.Aim == P(1, 2);
                ok &= r.Until(() => s.TargetX <= 265f + lead, 20f);  // 엘리베이터(맵 x240) 앞
                r.Shift = true; s.ShiftHeld = true;
                ok &= r.Until(() => s.TargetX <= 220f, 10f);
                RelLate(r, light);
                ok &= s.TargetFloor == 2;                            // 3F로 올라가지 않았다
                if (stop == "" && !ok) stop = "elevator1F-1";
                ok &= r.Until(() => s.TargetFacingRight && s.TargetX <= 100f, 30f);   // 로비 끝 벽에서 돌아섬
                ok &= r.Until(() => s.TargetX >= 215f - lead && s.TargetFloor == 2, 30f);
                r.Shift = true; s.ShiftHeld = true;                  // 다시 엘리베이터 멈춤(복습)
                ok &= r.Until(() => s.TargetX >= 260f, 10f);
                RelLate(r, light);
                ok &= s.TargetFloor == 2;
                if (stop == "" && !ok) stop = "elevator1F-2";
                r.Jump(1, 0);                                        // 해치 방
                ok &= s.Aim == P(2, 2);
                ok &= r.Until(() => s.TargetX >= 380f - lead, 20f);
                r.Shift = true; s.ShiftHeld = true;
                ok &= r.Until(() => s.Outcome != Outcome.Running, 5f);
                if (stop == "" && !ok) stop = "hatch2";
            }
            r.Release();
            return r;
        }

        static void W029RouteChecks()
        {
            float peakA = 0f;
            {
                // 03-01 §2-5 그대로의 C(Shift 계속 누름): 결과만 남긴다(통과 조건 아님)
                Rig rc = Old3Route('c', out bool okc, out string stopc);
                Note("1-3 route C as written in 03-01 (hold Shift through terminal and return) [simulation]: " + RouteSummary(rc) + (okc ? "" : " - stopped at " + stopc));
            }
            foreach (char m in new[] { 'A', 'B', 'C' })
            {
                Rig r = Old3Route(m, out bool ok, out string stop);
                GameSim s = r.S;
                string name = m == 'A' ? "A swap over the laser (Shift + right)" : m == 'B' ? "B laser off, walk through" : "C terminal upstairs (Shift + up swap, Shift released and pressed again) then A";
                Check("W-029 1-3 route " + name + ": cleared by the automatic route (Complete, final mission done)" + (ok ? "" : " - stopped at " + stop),
                    ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.MissionDone(1) == (m == 'C'));
                Note("1-3 route " + m + " [simulation]: " + RouteSummary(r));
                if (m == 'A')
                {
                    peakA = r.Peak;
                    Check("W-029 1-3 route A: TRACE <= 80 kept (peak " + r.Peak.ToString("0.0") + "), 2 stars (final + TRACE, terminal skipped)", !s.MissionFailed(2) && r.Peak <= 80f && s.Stars == 2);
                }
                else if (m == 'B')
                    // W-031: 챕터 1 추적도 상승이 초당 14로 느려져 B의 최대치가 70 아래로 내려왔다. TRACE 기준 값은 메인이 정한다(지금은 70 유지) — 측정값만 확인한다.
                    Check("W-031 1-3 route B (laser off, walk): peak " + r.Peak.ToString("0.0") + " > route A's " + peakA.ToString("0.0") + ", below game over, does not break TRACE <= 80 at the 0.7 scale (measurement for the main session)", r.Peak > peakA && r.Peak < s.TrackingMax && !s.MissionFailed(2) && s.Stars == 2);
                else
                    Check("W-029 1-3 route C: terminal done, no game over (peak " + r.Peak.ToString("0.0") + " < 100), TRACE <= 80 kept (D-115, W-031b), 3 stars", s.MissionDone(1) && r.Peak < s.TrackingMax && !s.MissionFailed(2) && s.Stars == 3);
            }
            {
                // 03-01 §2-6 그대로의 E(Shift 계속 누름): 결과만 남긴다(통과 조건 아님)
                Rig re = Old4Route('e', out bool oke, out string stope);
                Note("1-4 route E as written in 03-01 (hold Shift through terminal and return) [simulation]: " + RouteSummary(re) + (oke ? "" : " - stopped at " + stope));
            }
            foreach (char m in new[] { 'D', 'E' })
            {
                Rig r = Old4Route(m, out bool ok, out string stop);
                GameSim s = r.S;
                string name = m == 'D' ? "D basic (laser, relay, camera, pull)" : "E terminal cellar (Shift + up swap down, Shift released and pressed again to go back up) then D";
                Check("W-029 1-4 route " + name + ": cleared by the automatic route (Complete, final mission done, detections 0)" + (ok ? "" : " - stopped at " + stop),
                    ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.DetectCount == 0 && s.MissionDone(1) == (m == 'E'));
                Note("1-4 route " + m + " [simulation]: " + RouteSummary(r));
                if (m == 'D')
                    Check("W-029 1-4 route D: UNSEEN kept (detections 0), peak " + r.Peak.ToString("0.0") + " < 100, 2 stars (final + UNSEEN, terminal skipped)", !s.MissionFailed(2) && s.DetectCount == 0 && r.Peak < s.TrackingMax && s.Stars == 2);
                else
                    Check("W-029 1-4 route E: no game over (peak " + r.Peak.ToString("0.0") + " < 100), terminal done, UNSEEN kept, 3 stars", r.Peak < s.TrackingMax && s.MissionDone(1) && !s.MissionFailed(2) && s.Stars == 3);
            }
        }

        /// <summary>경로 확인만 빠르게 돌리는 메뉴(Play 모드 없이). 결과는 Unity/Logs/w029-routes.txt.</summary>
        [MenuItem("GameOff/W-029 Route Check")]
        public static void W029RouteMenu()
        {
            results.Clear();
            W029RouteChecks();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w029-routes.txt"));
            File.WriteAllLines(path, results);
        }

        // ===================================================================================================
        // W-031: 층 구조 확장(D-119)·챕터 1 추적도 완화(D-120). 화면 없이 GameSim을 1/60초씩 직접 진행한다.
        // ===================================================================================================

        /// <summary>의뢰·경로·모델별 결과 한 줄(W-031 표). 보고서에 그대로 옮긴다.</summary>
        static readonly List<string> w031Table = new List<string>();

        static void W031Row(string request, string route, bool light, Rig r, bool ok, string stop)
        {
            GameSim s = r.S;
            string clear = s.Outcome == Outcome.Complete ? "clear" : s.Outcome == Outcome.GameOver ? "GAME OVER" : s.Outcome.ToString();
            w031Table.Add(request + " | " + route + " | " + (light ? "light" : "optimal") + " | " + clear + " | peak " + r.Peak.ToString("0.0") + " | stars " + s.Stars
                + " | detect " + s.DetectCount + " | Shift " + r.ShiftSeconds.ToString("0.00") + " s | missions final " + (s.MissionDone(0) ? "done" : "-")
                + (s.MissionCount > 1 ? ", terminal " + (s.MissionDone(1) ? "done" : "-") : "") + (s.MissionCount > 2 ? ", 3rd " + (s.MissionFailed(2) ? "failed" : "kept") : "") + (ok ? "" : " | stopped at " + stop));
        }

        /// <summary>모든 의뢰의 자동 경로를 「최적」·「라이트」 두 번 잰다. 최적 경로는 클리어를 확인하고, 라이트 결과는 표로만 남긴다(측정).</summary>
        static void W031RouteChecks()
        {
            w031Table.Clear();
            foreach (bool light in new[] { false, true })
            {
                string m = light ? "light" : "optimal";
                foreach (bool miss in new[] { false, true })
                {
                    Rig r = Home2Route(miss, light, out bool ok, out string stop);
                    GameSim s = r.S;
                    W031Row("0-2", miss ? "hatch missed -> 1F elevator stopped twice -> second chance" : "hatch first try", light, r, ok, stop);
                    if (!light)
                        Check("W-031 0-2 " + (miss ? "second-chance route (hatch missed, 1F elevator stopped twice, hatch second try)" : "first-try route (laser, relay, stairs 2F -> 1F, hatch)") + " [" + m + "]: Complete in B1 col 2" + (ok ? "" : " - stopped at " + stop),
                            ok && s.Outcome == Outcome.Complete && s.MissionDone(0) && s.TargetFloor == 3);
                }
                foreach (bool star3 in new[] { false, true })
                {
                    Rig r = Old1Run(star3, light, out bool ok);
                    GameSim s = r.S;
                    W031Row("1-1", star3 ? "star-3 (carry + close B stairs + terminal)" : "star-2 (carry + exit)", light, r, ok, "");
                    if (!light)
                        Check("W-031 1-1 " + (star3 ? "star-3" : "star-2") + " route [" + m + "]: Complete from 1F through 3F to the lobby exit (final mission done, terminal " + (star3 ? "done" : "skipped") + ")",
                            ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.MissionDone(1) == star3 && s.TargetFloor == 2);
                }
                foreach (bool terminal in new[] { false, true })
                {
                    Rig r = Old2Run(terminal, light, out bool ok, out string stop);
                    GameSim s = r.S;
                    W031Row("1-2", terminal ? "star-3 (carry x2 + lift + laser + camera + pull)" : "star-2 (lift + laser + camera + pull)", light, r, ok, stop);
                    if (!light)
                        Check("W-031 1-2 " + (terminal ? "star-3" : "star-2") + " route [" + m + "]: Complete at the 0.6 rate scale, detections 0" + (ok ? "" : " - stopped at " + stop),
                            ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.DetectCount == 0);
                }
                foreach (char mode in new[] { 'A', 'B', 'C', 'G' })
                {
                    Rig r = Old3Route(mode, out bool ok, out string stop, light);
                    GameSim s = r.S;
                    string name = mode == 'A' ? "A swap over the laser" : mode == 'B' ? "B laser off, walk" : mode == 'G' ? "G terminal upstairs (C) then laser off, walk (B)" : "C terminal upstairs then A";
                    W031Row("1-3", name, light, r, ok, stop);
                    if (!light)
                        Check("W-031 1-3 route " + name + " [" + m + "]: Complete (swap or walk, stairs 2F -> 1F, lobby exit)" + (ok ? "" : " - stopped at " + stop),
                            ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.TargetFloor == 2 && s.MissionDone(1) == (mode == 'C' || mode == 'G'));
                }
                foreach (char mode in new[] { 'D', 'E' })
                {
                    Rig r = Old4Route(mode, out bool ok, out string stop, light);
                    GameSim s = r.S;
                    string name = mode == 'D' ? "D basic (laser, relay, camera, shutter, stairs, pull)" : "E terminal cellar then D";
                    W031Row("1-4", name, light, r, ok, stop);
                    if (!light)
                        Check("W-031 1-4 route " + name + " [" + m + "]: Complete on 4F (shutter opened once), detections 0" + (ok ? "" : " - stopped at " + stop),
                            ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.DetectCount == 0 && s.MissionDone(1) == (mode == 'E'));
                }
            }
            foreach (string row in w031Table) Note("W-031 TABLE [simulation] " + row);
        }

        /// <summary>W-031: 의뢰별 층·열 수, 추적도 배율, Shift 1초 동안 상승량.</summary>
        static void W031PureChecks()
        {
            var sizes = new[]
            {
                new { name = "0-2", path = Home2Path, floors = 4, cols = 4, scale = 1f, rate = 20f },
                new { name = "1-1", path = Old1LevelPath, floors = 3, cols = 5, scale = 0.7f, rate = 14f },
                new { name = "1-2", path = Old2LevelPath, floors = 3, cols = 5, scale = 0.6f, rate = 12f },
                new { name = "1-3", path = Old3LevelPath, floors = 3, cols = 5, scale = 0.7f, rate = 14f },
                new { name = "1-4", path = Old4LevelPath, floors = 4, cols = 4, scale = 0.7f, rate = 14f },
                new { name = "0-1", path = Home1Path, floors = 3, cols = 4, scale = 1f, rate = 20f },
            };
            foreach (var z in sizes)
            {
                var r = new Rig(z.path);
                Board b = r.S.Board;
                Check("W-031 " + z.name + " map: " + z.floors + " floors x " + z.cols + " cols, layout loaded with the same size, TrackingRateScale " + z.scale.ToString("0.0"),
                    b.Floors == z.floors && b.Cols == z.cols && b.Layout != null && b.Layout.Floors == z.floors && b.Layout.Cols == z.cols && Near(b.TrackingRateScale, z.scale, 0.0001f));
                r.Shift = true;
                r.Run(1f);
                // 1초 = 60단계 x 1/60초. 시작 대기·튜토리얼 여부와 관계없이 Shift 중 상승량만 본다(추적도 최대 이전)
                Check("W-031 " + z.name + " Shift held for 1 s raises tracking by " + z.rate.ToString("0") + " (" + (z.scale < 1f ? "chapter 1 scale " + z.scale.ToString("0.0") : "scale 1.0") + "), measured " + r.S.Tracking.ToString("0.00"), Near(r.S.Tracking, z.rate, 0.05f));
            }
            {
                // 0-2 대조: 해치도 엘리베이터도 건드리지 않으면 1F 엘리베이터가 3F로 올리고, 주인공 방 문(실패 지점)에 닿아 Failed
                Rig r = new Rig(Home2Path);
                GameSim s = r.S;
                r.Until(() => s.TargetX >= 215f, 60f);
                r.Shift = true; s.ShiftHeld = true;
                r.Until(() => s.TargetX >= 260f, 10f);
                r.Release();
                r.Jump(1, 0);
                r.Until(() => s.TargetWaiting, 30f);
                r.Shift = true; s.ShiftHeld = true;
                r.Until(() => s.TargetX > 330f, 10f);
                r.Release();
                r.Until(() => s.RelayUsed, 10f);
                bool down = r.Until(() => s.TargetFloor == 2, 30f);
                bool downOk = down && Near(s.TargetX, 540f, 0.6f) && !s.TargetFacingRight;
                r.Until(() => s.TargetFloor == 0, 60f);
                Check("W-031 0-2 hatch missed and nothing pressed: the 1F elevator (open, x240) lifts the runner to 3F col 1 x80 facing left (stairs arrival was 1F col 3 x60 facing left)",
                    downOk && s.TargetFloor == 0 && Near(s.TargetX, 240f, 0.6f) && !s.TargetFacingRight);
                r.Until(() => s.Outcome != Outcome.Running, 60f);
                Check("W-031 0-2 missed and lifted: runner walks to the protagonist's door (fail point x142) -> Outcome.Failed", s.Outcome == Outcome.Failed && Near(s.TargetX, 142f, 0.6f) && s.TargetFloor == 0);
            }
            {
                // 1-4 셔터: 닫힌 채면 3F 복도 x420 앞(x429)에서 기다리고, 설계도 상태로 열면 지나 계단 3F -> 4F x40으로 오른다
                Rig r = Old4Route('D', out bool okD, out _);
                Check("W-031 1-4 route D passes the 3F shutter exactly once and reaches the roof (4F exit)", okD && r.CountUse(UseKind.DoorPassed) == 1 && r.S.Outcome == Outcome.Complete);
                // 셔터 앞에 서기까지: 레이저를 끄고 중계기·카메라·계단을 지난 뒤 (Old4Route와 같은 앞부분) 셔터를 열지 않고 그대로 둔다
                Rig q = new Rig(Old4LevelPath);
                GameSim sq = q.S;
                bool pre = AimTo(q, 2, 2);
                pre &= q.Until(() => sq.TargetFloor == 2 && sq.TargetWaiting, 90f);
                q.Shift = true; sq.ShiftHeld = true;
                pre &= q.Until(() => sq.TargetX > 330f, 10f);
                q.Release();
                pre &= q.Until(() => sq.RelayUsed, 10f);
                pre &= AimTo(q, 3, 2);
                pre &= q.Until(() => sq.TargetFloor == 2 && sq.TargetX >= 425f, 20f);
                q.Shift = true; sq.ShiftHeld = true;
                pre &= q.Until(() => sq.TargetFloor == 1, 10f);
                q.Release();
                q.Run(8f);
                Check("W-031 1-4 3F shutter closed (no Shift): runner waits in front of it at x429 on 3F and does not climb to 4F", pre && sq.TargetFloor == 1 && sq.TargetWaiting && Near(sq.TargetX, 429f, 0.6f));
            }

            // 테스트·샌드박스 의뢰도 배율 1.0(기본값)
            var t = new Rig();
            Check("W-031 test request (default) scale 1.0", Near(t.S.Board.TrackingRateScale, 1f, 0.0001f));
            // 중계기·발각·게임오버·카운트다운 값은 그대로(GameSettings)
            var set = AssetDatabase.LoadAssetAtPath<GameOff.Data.GameSettings>("Assets/_Project/Data/GameSettings.asset");
            Check("W-031 GameSettings unchanged: rate 20 per second, max 100", Near(set.trackingRatePerSecond, 20f, 0.0001f) && Near(set.trackingMax, 100f, 0.0001f));
        }

        /// <summary>W-031 경로·표만 빠르게 돌리는 메뉴(Play 모드 없이). 결과는 Unity/Logs/w031-routes.txt.</summary>
        [MenuItem("GameOff/W-031 Route Check")]
        public static void W031RouteMenu()
        {
            results.Clear();
            W031PureChecks();
            W031RouteChecks();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w031b-routes.txt"));
            File.WriteAllLines(path, results);
        }

        // ----- W-029: 화면 흐름(목록 6개, 1-3·1-4 브리핑·종료 대화, 1-3 단계 안내, 캡처) -----

        static void CaptureW32(string name)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-032"));
            Directory.CreateDirectory(dir);
            view.Canvas.SaveSizedPng(Path.Combine(dir, name + "_1920x1080.png"), 1920, 1080);
        }

        static string CaptureDir29 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-029"));

        static void CaptureW29(string name)
        {
            Directory.CreateDirectory(CaptureDir29);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir29, name + "_1920x1080.png"), 1920, 1080);
        }

        static IEnumerator W029Checks()
        {
            view.Progress.EnvFxReduced = false;
            view.Progress.Clear(AllIds());
            yield return ToMain();
            yield return Frames(4);
            view.Progress.SetCleared("home-0-1");
            view.Progress.SetCleared("home-0-2");
            view.Progress.SetCleared("old-1-1");
            view.Progress.SetCleared("old-1-2");
            if (view.MainIndex != 0) yield return Press("Up");
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "W-029 Desktop");
            yield return Press("Enter");   // W-032: 일반 모드 팝업 열기(챕터 포커스)
            if (view.FolderIndex != 1) yield return Press("Down");   // 처음 포커스는 마지막 플레이 의뢰라 CH0일 수도 CH1일 수도 있다
            yield return Press("Right");   // 스테이지 포커스
            Check("W-029 CH1 list opened", view.InFolder && view.FolderIndex == 1);
            for (int i = 0; i < 6 && view.DeskIndex != 4; i++) yield return Press("Down");
            Check("W-029 list (W-036: 6 chapter 1 requests, OLD TOWN E is now REQ_1-6 at index 7): REQ_1-1 .. REQ_1-6 in order (1-1 and 1-2 cleared, 1-3 playable NEW, 1-4 locked)",
                view.Desk.Count == 12 && view.Desk.RequestName(2) == "REQ_1-1" && view.Desk.RequestName(3) == "REQ_1-2" && view.Desk.RequestName(4) == "REQ_1-3" && view.Desk.RequestName(5) == "REQ_1-4" && view.Desk.RequestName(6) == "REQ_1-5" && view.Desk.RequestName(7) == "REQ_1-6"
                && view.Desk.State(4) == RequestState.New && view.Desk.State(5) == RequestState.Locked && view.DeskIndex == 4);
            Dt.RequestEntry e3 = view.Desk.Entry(4), e4 = view.Desk.Entry(7);
            Check("W-029 data: 1-3 id old-1-3, title SWAP SHOP, client ???, missions REACH THE EXIT / UPSTAIRS TERMINAL / TRACE <= 80",
                e3.id == "old-1-3" && e3.title == "SWAP SHOP" && e3.client == "???" && e3.missionNames.Length == 3 && e3.missionNames[0] == "REACH THE EXIT" && e3.missionNames[1] == "UPSTAIRS TERMINAL" && e3.missionNames[2] == "TRACE <= 80"
                && e3.summary == "A LASER HALL BLOCKS THE WAY.\nSWAP THE SHOP PAST IT.\nREACH THE EXIT.");
            Check("W-029 data (W-036: now 1-6): id old-1-4, title ROOFTOP RUN, client ???, missions REACH THE ROOF / CELLAR TERMINAL / UNSEEN",
                e4.id == "old-1-4" && e4.title == "ROOFTOP RUN" && e4.client == "???" && e4.missionNames.Length == 3 && e4.missionNames[0] == "REACH THE ROOF" && e4.missionNames[1] == "CELLAR TERMINAL" && e4.missionNames[2] == "UNSEEN"
                && e4.summary == "LASERS, A CAMERA, A ROOF.\nONE THING AT A TIME.\nREACH THE ROOF.");
            Check("W-029 data: environment 1-3 Night/Cloudy, 1-4 Night/Rain",
                e3.level.timeOfDay == Dt.TimeOfDay.Night && e3.level.weather == Dt.Weather.Cloudy && e4.level.timeOfDay == Dt.TimeOfDay.Night && e4.level.weather == Dt.Weather.Rain);
            yield return Frames(3);
            CaptureW29("01-ch1-list-1-3-selected");
            yield return Frames(1);

            // ===== 1-3 =====
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "W-029 1-3 briefing");
            Dt.DialogueData d3 = e3.dialogue;
            Check("W-029 1-3 briefing starts with the dialogue (3 lines), line 1 BROKER", d3 != null && d3.briefing.Length == 3 && d3.ending.Length == 3 && LineIs(0, "BROKER", "A LASER HALL BLOCKS THE WAY OUT."));
            yield return Press("Enter");
            bool b2 = LineIs(1, "RUNNER", "I CAN DUCK. PROBABLY.") && view.Shown[1].hackerFace == "joy";
            yield return Press("Enter");
            bool b3 = view.Shown.Count == 3 && view.Shown[2].speaker == Dt.Speaker.Me && view.Shown[2].text == "NO NEED. I'LL SWAP THE ROOMS." && view.Shown[2].hackerFace == "neutral";
            yield return Press("Enter");
            Check("W-029 1-3 briefing lines 2 (RUNNER, joy) and 3 (ME, neutral), then Playing", b2 && b3 && view.CurrentPhase == GameView.Phase.Playing);
            yield return Frames(3);
            Check("W-029 1-3 map (W-031: 3 floors): 3 floors x 5 cols, OLD TOWN D, layout ch1_old3 loaded with every room texture in the art set, 3 missions",
                Sim.Board.BuildingName == "OLD TOWN D" && Sim.Board.Floors == 3 && Sim.Board.Cols == 5 && Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch1_old3/layout" && LayoutArtOk(Sim.Board.Layout) && Sim.MissionCount == 3);
            Check("W-033 1-3 has no tutorial step (the swap notice was removed)",
                e3.level.tutorialSteps.Length == 0 && view.Tut == null && !Sim.Board.Tutorial);
            yield return Frames(2);
            CaptureW29("02-1-3-start");
            yield return Frames(1);
            Check("W-029 1-3 first aim = the empty store (rail room, col 1 floor 1)", Sim.Aim == P(1, 1));
            Advance(() => Sim.TargetFloor == 1 && Sim.TargetX >= 277f && Sim.TargetFacingRight, 60f);
            shift = true;
            next.Right = true;
            yield return null;
            yield return Frames(40);
            Check("W-029 1-3 Shift + right: the empty store (runner inside) swaps with the laser corridor (both change cells), runner carried",
                Sim.Pushed && Sim.CellOf(1, 1) == P(2, 1) && Sim.CellOf(1, 2) == P(1, 1) && Sim.TargetFloor == 1);
            CaptureW29("03-1-3-swapped");
            yield return Frames(1);
            shift = false;
            yield return Frames(3);
            ForceSimCompleteAll();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending, "W-029 1-3 ending");
            bool g1 = LineIs(3, "RUNNER", "THE WHOLE SHOP MOVED. WITH ME IN IT.") && view.Shown[3].hackerFace == "surprise";
            yield return Press("Enter");
            bool g2 = LineIs(4, "BROKER", "DONE. THE NIGHT ISN'T OVER.");   // W-038: 07-01 v0.5.0 교체
            yield return Press("Enter");
            bool g3 = view.Shown.Count == 6 && view.Shown[5].speaker == Dt.Speaker.Me && view.Shown[5].text == "KEEP YOUR SHOES ON.";
            yield return Press("Enter");
            Check("W-029 1-3 ending shows 3 lines (RUNNER surprise, BROKER, ME), then the result window", g1 && g2 && g3 && view.CurrentPhase == GameView.Phase.Ended);
            yield return Frames(20);

            // ===== 1-6(예전 1-4 OLD TOWN E, W-036: 목록 일곱 번째 자리) =====
            for (int i = 0; i < 4 && view.CurrentPhase == GameView.Phase.Ended; i++) { yield return Press("Enter"); yield return Frames(10); }
            Check("W-029 result Enter -> CH1 list, 1-3 cleared and the next request (new 1-4) now playable (W-036)", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder && view.Desk.IsCleared(4) && view.Desk.IsPlayable(5) && !view.Desk.IsPlayable(7));
            view.Progress.SetCleared("old-1-f");   // W-036: 새 1-4·1-5를 클리어로 두어 1-6(OLD TOWN E)이 열리게 한다
            view.Progress.SetCleared("old-1-g");
            for (int i = 0; i < 6 && view.DeskIndex < 7; i++) yield return Press("Down");
            Check("W-029 Down selects REQ_1-6 (OLD TOWN E, W-036)", view.DeskIndex == 7 && view.Desk.IsPlayable(7));
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "W-029 1-4 briefing");
            Dt.DialogueData d4 = e4.dialogue;
            Check("W-029 1-4 briefing starts with the dialogue (3 lines), line 1 BROKER", d4 != null && d4.briefing.Length == 3 && d4.ending.Length == 3 && LineIs(0, "BROKER", "LAST JOB. ROOFTOP. LASERS, A CAMERA, NO STAIRS TO THE EXIT."));
            yield return Press("Enter");
            bool c2 = LineIs(1, "RUNNER", "SO... EVERYTHING AT ONCE.") && view.Shown[1].hackerFace == "fear";
            yield return Press("Enter");
            bool c3 = view.Shown.Count == 3 && view.Shown[2].speaker == Dt.Speaker.Me && view.Shown[2].text == "ONE THING AT A TIME. LISTEN." && view.Shown[2].hackerFace == "neutral";
            yield return Press("Enter");
            Check("W-029 1-4 briefing lines 2 (RUNNER, fear) and 3 (ME, neutral), then Playing", c2 && c3 && view.CurrentPhase == GameView.Phase.Playing);
            yield return Frames(3);
            Check("W-029 1-4 map (W-031: 4 floors): 4 floors x 4 cols, OLD TOWN E, layout ch1_old4 loaded with every room texture in the art set, no guide box, camera in the 2F stairwell, 3 missions (UNSEEN)",
                Sim.Board.BuildingName == "OLD TOWN E" && Sim.Board.Floors == 4 && Sim.Board.Cols == 4 && Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch1_old4/layout" && LayoutArtOk(Sim.Board.Layout)
                && view.Tut == null && e4.level.tutorialSteps.Length == 0 && Sim.MissionCount == 3 && FindObj(Sim.Board, Dt.ObjectKind.Camera, 2, 3) != null);
            yield return Frames(2);
            CaptureW29("04-1-4-start");
            yield return Frames(1);
            Advance(() => Sim.TargetFloor == 2 && Sim.TargetX >= 290f && Sim.TargetX < 315f && Sim.TargetFacingRight, 60f);
            AimToSim(Sim, 1, 3);
            Check("W-029 1-4 aim on the empty store (1F = floor 3, col 1)", Sim.Aim == P(1, 3));
            shift = true;
            next.Up = true;
            yield return null;
            yield return Frames(40);
            Check("W-029 1-4 Shift + up: the empty store swaps with the 2F corridor, the runner goes down to 1F with the corridor",
                Sim.Pushed && Sim.CellOf(2, 1) == P(1, 3) && Sim.CellOf(3, 1) == P(1, 2) && Sim.TargetFloor == 3);
            CaptureW29("05-1-4-swapped-up");
            yield return Frames(1);
            shift = false;
            yield return Frames(3);
            ForceSimCompleteAll();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending, "W-029 1-4 ending");
            bool h1 = LineIs(3, "RUNNER", "WE DID IT! THE WHOLE TOWN, ROOM BY ROOM.") && view.Shown[3].hackerFace == "joy";
            yield return Press("Enter");
            bool h2 = LineIs(4, "BROKER", "CLEAN WORK. NEXT TIME, OUT OF TOWN.");   // W-038: 07-01 v0.5.0 교체
            yield return Press("Enter");
            bool h3 = view.Shown.Count == 6 && view.Shown[5].speaker == Dt.Speaker.Me && view.Shown[5].text == "LOGGING OFF.";
            yield return Press("Enter");
            Check("W-029 1-4 ending shows 3 lines (RUNNER joy, BROKER, ME), then the result window", h1 && h2 && h3 && view.CurrentPhase == GameView.Phase.Ended);
            yield return Frames(20);
            yield return Press("Enter");   // 목록으로
            yield return Frames(10);
            Check("W-029 result Enter -> CH1 list, 1-3 and 1-6 (OLD TOWN E) cleared", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder && view.Desk.IsCleared(4) && view.Desk.IsCleared(7));
            yield return Frames(3);
            CaptureW29("06-ch1-list-all-cleared");
            yield return Frames(1);
        }

        // ===================================================================================================
        // W-030: 조작 피드백(D-118) — F1 상태 표지, F5 효과 없음, F3 작동 순간(장치 이용 사건·링·기록).
        // 사건 판정은 화면 없이 GameSim을 1/60초씩 직접 진행하고(W030PureChecks), 표지·링·기록은 화면(W030Checks)에서 본다.
        // ===================================================================================================

        static string CaptureDir30 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-030"));

        static void CaptureW30(string name)
        {
            Directory.CreateDirectory(CaptureDir30);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir30, name + "_1920x1080.png"), 1920, 1080);
        }

        /// <summary>규칙 확인용 레벨 L: 2층 x 2열. 층 1·열 0에 연결 장치 하나(x80, 층 0·열 0 x80으로 이동). 시작 = 층 1·열 0 x20 오른쪽, 조준 = 그 방.</summary>
        static Dt.LevelData MakeLinkLevel(Dt.ObjectKind kind, bool real, bool bp)
        {
            var l = ScriptableObject.CreateInstance<Dt.LevelData>();
            l.floors = 2; l.cols = 2; l.sectorWidth = 160; l.sectorHeight = 96; l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.sectors = new Dt.SectorDef[0];
            Dt.ObjectDef d = PushObj(kind, 1, 0, 80, real, bp);
            d.hasLink = true; d.linkFloor = 0; d.linkCol = 0; d.linkX = 80; d.linkFacingRight = false;
            l.objects = new[] { d };
            l.startFloor = 1; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 1; l.startAimCol = 0;
            l.missions = new[] { new Dt.MissionDef { kind = Dt.MissionKind.PassObject } };
            return l;
        }

        /// <summary>규칙 확인용 레벨 M: 1층 x 2열, 열 0에 문(x140 = 맵 x140, 현실 닫힘 또는 열림 -> 설계도 열림). 시작 x20 오른쪽, 조준 = 문 방.</summary>
        static Dt.LevelData MakeDoorLevel(bool realOpen)
        {
            var l = ScriptableObject.CreateInstance<Dt.LevelData>();
            l.floors = 1; l.cols = 2; l.sectorWidth = 160; l.sectorHeight = 96; l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.sectors = new Dt.SectorDef[0];
            l.objects = new[] { PushObj(Dt.ObjectKind.Door, 0, 0, 140, realOpen, true) };
            l.startFloor = 0; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 0; l.startAimCol = 0;
            l.missions = new[] { new Dt.MissionDef { kind = Dt.MissionKind.PassObject } };
            return l;
        }

        static int SumRoutes(string prefix, UseKind k)
        {
            int n = 0;
            foreach (var kv in routeUses)
                if (kv.Key.StartsWith(prefix))
                    foreach (UseEvent e in kv.Value) if (e.Kind == k) n++;
            return n;
        }

        static string UseCounts(List<UseEvent> list)
        {
            var parts = new List<string>();
            foreach (UseKind k in Enum.GetValues(typeof(UseKind)))
            {
                int n = 0;
                foreach (UseEvent e in list) if (e.Kind == k) n++;
                if (n > 0) parts.Add(k + " x" + n);
            }
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        static void W030PureChecks()
        {
            // ----- 합성 레벨 L: 연결 장치 5가지(사건 종류별 판정) -----
            var cases = new[]
            {
                new { kind = Dt.ObjectKind.Stairs, real = false, bp = true, use = UseKind.ShutterPassed },
                new { kind = Dt.ObjectKind.Hatch, real = false, bp = true, use = UseKind.HatchPassed },
                new { kind = Dt.ObjectKind.Elevator, real = false, bp = true, use = UseKind.ElevatorUsed },
                new { kind = Dt.ObjectKind.Stairs, real = true, bp = false, use = UseKind.StairsSkipped },
                new { kind = Dt.ObjectKind.Elevator, real = true, bp = false, use = UseKind.ElevatorSkipped },
            };
            foreach (var c in cases)
            {
                var r = new Rig(MakeLinkLevel(c.kind, c.real, c.bp));
                r.Shift = true; r.S.ShiftHeld = true;
                r.Until(() => r.S.UseSeq > 0, 20f);
                r.Release();
                Check("W-030 F3 " + c.kind + " reality " + (c.real ? "on" : "off") + " / blueprint " + (c.bp ? "on" : "off") + " with Shift on the room: one " + c.use + " event at the device (floor 1, x 80), no other event (" + UseCounts(r.Uses) + ")",
                    r.Uses.Count == 1 && r.Uses[0].Kind == c.use && r.Uses[0].Floor == 1 && r.Uses[0].Col == 0 && Near(r.Uses[0].X, 80f, 0.6f));
            }
            {
                // 대조: Shift 없이 현실 그대로 열린 계단을 이용하면 사건이 없다
                var r = new Rig(MakeLinkLevel(Dt.ObjectKind.Stairs, true, false));
                r.Until(() => r.S.TargetFloor == 0, 20f);
                Check("W-030 F3 control: an open staircase used as it is in reality (no Shift) leaves no event (UseSeq " + r.S.UseSeq + ", floor " + r.S.TargetFloor + ")", r.S.TargetFloor == 0 && r.S.UseSeq == 0);
            }
            {
                // 문: 현실 닫힘 -> 설계도 열림(레벨 M: 열 0 문 x140, 시작 x20)
                var r = new Rig(MakeDoorLevel(false));
                r.Shift = true; r.S.ShiftHeld = true;
                bool ok = r.Until(() => r.S.TargetX > 150f, 20f);
                r.Release();
                Check("W-030 F3 closed door opened by Shift and walked through: one DOOR PASSED at the door (col 0, x 140), nothing else (" + UseCounts(r.Uses) + ")",
                    ok && r.Uses.Count == 1 && r.Uses[0].Kind == UseKind.DoorPassed && r.Uses[0].Col == 0 && Near(r.Uses[0].X, 140f, 0.6f));
                var r2 = new Rig(MakeDoorLevel(true));
                bool ok2 = r2.Until(() => r2.S.TargetX > 150f, 20f);
                Check("W-030 F3 control: a door that is already open in reality leaves no DOOR PASSED (" + UseCounts(r2.Uses) + ")", ok2 && r2.CountUse(UseKind.DoorPassed) == 0);
                var r3 = new Rig(MakeDoorLevel(false));
                r3.Until(() => r3.S.TargetWaiting, 20f);
                Check("W-030 F3 control: a closed door the helper only waits at (no Shift) leaves no event (waiting " + r3.S.TargetWaiting + ", " + UseCounts(r3.Uses) + ")", r3.S.TargetWaiting && r3.Uses.Count == 0);
            }
            {
                // 실제 의뢰 0-1·0-2 자동 경로
                var r = new Rig(Home1Path);
                GameSim s = r.S;
                bool ok = r.Until(() => s.TargetFloor == 1 && s.TargetX >= 520f, 40f);
                r.Shift = true; s.ShiftHeld = true;                  // 계단(맵 x540)을 닫는다: 지나친다
                ok &= r.Until(() => s.TargetX >= 560f, 10f);
                r.Release();
                Check("W-030 F3 0-1 stairs sealed by Shift and passed by: one STAIRS SKIPPED (" + UseCounts(r.Uses) + ")", ok && r.CountUse(UseKind.StairsSkipped) == 1 && r.Uses.Count == 1);
                var rh = new Rig(Home1Path);
                GameSim sh = rh.S;
                ok = AimTo(rh, 2, 1);
                ok &= rh.Until(() => sh.TargetFloor == 1 && sh.TargetX >= 385f, 40f);
                rh.Shift = true; sh.ShiftHeld = true;                // 해치(맵 x400)를 연다: 떨어진다
                ok &= rh.Until(() => sh.Outcome != Outcome.Running, 5f);
                Check("W-030 F3 0-1 hatch opened by Shift: one HATCH PASSED (" + UseCounts(rh.Uses) + ")", ok && rh.CountUse(UseKind.HatchPassed) == 1 && rh.Uses.Count == 1);
                var re = new Rig(Home2Path);
                GameSim se = re.S;
                ok = re.Until(() => se.TargetX >= 215f, 30f);
                re.Shift = true; se.ShiftHeld = true;                // 엘리베이터(맵 x240)를 멈춘다: 지나친다
                ok &= re.Until(() => se.TargetX >= 260f, 10f);
                re.Release();
                Check("W-030 F3 0-2 elevator stopped by Shift and passed by: one ELEVATOR SKIPPED (" + UseCounts(re.Uses) + ")", ok && re.CountUse(UseKind.ElevatorSkipped) == 1 && re.Uses.Count == 1);
            }

            // ----- 챕터 1 자동 경로(W-014·W-016·W-029 경로를 다시 돌리지 않고 그때 모은 사건을 센다) -----
            int c11a = SumRoutes("1-1 star-2", UseKind.Carried), b11a = SumRoutes("1-1 star-2", UseKind.RoomBridged), s11a = SumRoutes("1-1 star-2", UseKind.StairsSkipped);
            int c11b = SumRoutes("1-1 star-3", UseKind.Carried), b11b = SumRoutes("1-1 star-3", UseKind.RoomBridged), s11b = SumRoutes("1-1 star-3", UseKind.StairsSkipped);
            Check("W-030 F3 1-1 carry route (star-2): CARRIED x" + c11a + " and ROOM BRIDGED x" + b11a + " seen (the helper rides the slid room, then walks out to another room), no STAIRS SKIPPED (x" + s11a + ")", c11a >= 1 && b11a >= 1 && s11a == 0);
            Check("W-030 F3 1-1 terminal route (star-3): CARRIED x" + c11b + ", ROOM BRIDGED x" + b11b + " and STAIRS SKIPPED x" + s11b + " (B stairs sealed by Shift) seen", c11b >= 1 && b11b >= 1 && s11b >= 1);
            int l12 = SumRoutes("1-2 star-2", UseKind.LaserPassed), e12 = SumRoutes("1-2 star-2", UseKind.CameraEvaded), c12 = SumRoutes("1-2 star-2", UseKind.Carried);
            Check("W-030 F3 1-2 basic route (star-2): LASER PASSED x" + l12 + " (laser room turned off) and CAMERA EVADED x" + e12 + " (camera off, left by the stairs) and CARRIED x" + c12 + " (lift) seen, with 0 detections",
                l12 >= 1 && e12 >= 1 && c12 >= 1);
            Check("W-030 F3 1-3 route A (swap over the laser): CARRIED x" + SumRoutes("1-3 A", UseKind.Carried) + ", ROOM BRIDGED x" + SumRoutes("1-3 A", UseKind.RoomBridged) + ", no LASER PASSED (the laser room is never walked)",
                SumRoutes("1-3 A", UseKind.Carried) >= 1 && SumRoutes("1-3 A", UseKind.RoomBridged) >= 1 && SumRoutes("1-3 A", UseKind.LaserPassed) == 0);
            Check("W-030 F3 1-3 route B (laser off, walk): LASER PASSED x" + SumRoutes("1-3 B", UseKind.LaserPassed) + ", no CARRIED / ROOM BRIDGED", SumRoutes("1-3 B", UseKind.LaserPassed) >= 1 && SumRoutes("1-3 B", UseKind.Carried) == 0 && SumRoutes("1-3 B", UseKind.RoomBridged) == 0);
            Check("W-030 F3 1-4 route D: LASER PASSED x" + SumRoutes("1-4 D", UseKind.LaserPassed) + ", CAMERA EVADED x" + SumRoutes("1-4 D", UseKind.CameraEvaded) + " seen", SumRoutes("1-4 D", UseKind.LaserPassed) >= 1 && SumRoutes("1-4 D", UseKind.CameraEvaded) >= 1);
            foreach (var kv in routeUses) Note("W-030 events per route [simulation] " + kv.Key + ": " + UseCounts(kv.Value));

            // 사건을 읽어도 판정이 바뀌지 않음: 1-3 경로 A를 한 번 더 돌려도 결과가 W-029와 같다(UseSeq 읽기만 추가)
            Rig ra = Old3Route('A', out bool oka, out _);
            Check("W-030 judgement unchanged: 1-3 route A still clears with 2 stars and TRACE <= 80 kept after the use records were added", oka && ra.S.Outcome == Outcome.Complete && ra.S.Stars == 2 && !ra.S.MissionFailed(2));
        }

        static IEnumerator WaitReal(double seconds)
        {
            double t = Now;
            while (Now - t < seconds) yield return null;
        }

        static void SetActiveRequest(int index) => typeof(GameView).GetProperty("ActiveIndex").GetSetMethod(true).Invoke(view, new object[] { index });

        static IEnumerator StartLevelFor(int requestIndex, string asset)
        {
            shift = false;
            SetActiveRequest(requestIndex);
            view.DebugStartLevel(LoadLevel(asset));
            yield return Frames(3);
        }

        static IEnumerator W030Checks()
        {
            GameOff.Game.GameRenderer rd = view.Renderer;
            string note = "";

            // ===== F1 즉시 확인: 0-1 계단(CLOSED)·해치(OPEN) =====
            yield return StartLevelFor(0, "Ch0Home_1.asset");
            Check("W-030 F1 0-1 start: aim = stairs room (3,1), Shift not pressed -> no marker", Sim.Aim == P(3, 1) && rd.MarkerCount == 0);
            int appeared0 = rd.MarkersAppeared;
            shift = true;
            yield return null;
            Check("W-030 F1 0-1 Shift on the stairs room: one marker 'CLOSED' appears at once (the open stairs become closed), flashing in its first ticks",
                rd.MarkerCount == 1 && rd.MarkerText(0) == "CLOSED" && rd.MarkerFlashing(0) && !rd.MarkerGray(0) && rd.MarkersAppeared == appeared0 + 1);
            CaptureW30("01-f1-0-1-stairs-closed-flash");
            yield return WaitReal(0.25);
            Check("W-030 F1 the marker stays while Shift is held (after 0.25 s: still 'CLOSED', border no longer flashing, no second marker)", rd.MarkerCount == 1 && rd.MarkerText(0) == "CLOSED" && !rd.MarkerFlashing(0) && rd.MarkersAppeared == appeared0 + 1);
            CaptureW30("02-f1-0-1-stairs-closed");
            shift = false;
            yield return Frames(2);
            Check("W-030 F1 Shift released -> the marker disappears at once", rd.MarkerCount == 0);
            yield return Jump(Dir.Left, 2, 1);   // 해치 섹터
            shift = true;
            yield return Frames(2);
            Check("W-030 F1 0-1 Shift on the hatch room: marker 'OPEN' (the closed hatch becomes open)", rd.MarkerCount == 1 && rd.MarkerText(0) == "OPEN");
            yield return Frames(2);
            CaptureW30("03-f1-0-1-hatch-open");
            shift = false;
            yield return Frames(2);

            // ===== F1 방 밀기 MOVED: 1-1(빈 칸 1개) =====
            yield return StartLevelFor(2, "Ch1Old_1.asset");
            Check("W-030 F1 1-1 aim to the rail room (1,0)", AimToSim(Sim, 1, 0));
            shift = true;
            yield return Frames(2);
            // ===== F5: 레일 방에서 장치 변화가 없으면 PRESS ← ↑ → ↓ =====
            double tPress = Now;
            Check("W-030 F5 1-1 rail room (no device) Shift: grey hint '" + Fx.RailHintText + "' (not NO EFFECT), no state marker", rd.MarkerCount == 1 && rd.MarkerGray(0) && rd.MarkerText(0) == Fx.RailHintText);
            CaptureW30("04-f5-1-1-rail-hint");
            yield return WaitUntil(() => rd.MarkerCount == 0, "W-030 hint ends", 600);
            double hintSeconds = Now - tPress;
            Check("W-030 F5 the hint lasts 0.6 s (measured " + hintSeconds.ToString("0.00") + " s) and is not shown again while Shift is held", hintSeconds > 0.5 && hintSeconds < 0.85);
            yield return WaitReal(0.3);
            Check("W-030 F5 still no hint 0.3 s later with Shift held", rd.MarkerCount == 0);
            next.Right = true;
            yield return Frames(2);
            Check("W-030 F1 1-1 Shift + right slides the room into the empty cell: one 'MOVED' marker (no swapped room), pushed", Sim.Pushed && Sim.PushedRoom >= 0 && Sim.SwappedRoom < 0 && rd.MarkerCount == 1 && rd.MarkerText(0) == "MOVED" && !rd.MarkerGray(0));
            yield return Frames(16);
            rd.FinishSlides();
            yield return Frames(2);
            CaptureW30("05-f1-1-1-moved-x1");
            shift = false;
            yield return Frames(2);
            Check("W-030 F1 Shift released -> room returns, MOVED disappears", !Sim.Pushed && rd.MarkerCount == 0);

            // ===== F1 방 밀기 MOVED: 1-3 맞바꿈(2개) =====
            yield return StartLevelFor(4, "Ch1Old_3.asset");
            Check("W-030 F1 1-3 aim to the shop (1,1)", AimToSim(Sim, 1, 1));
            shift = true;
            yield return Frames(2);
            next.Up = true;
            yield return Frames(2);
            Check("W-030 F1 1-3 Shift + up swaps the shop with the hallway above: two 'MOVED' markers (pushed + swapped room)", Sim.Pushed && Sim.PushedRoom >= 0 && Sim.SwappedRoom >= 0 && rd.MarkerCount == 2 && rd.MarkerText(0) == "MOVED" && rd.MarkerText(1) == "MOVED");
            CaptureW30("06-f1-1-3-moved-x2-flash");
            yield return Frames(16);
            rd.FinishSlides();
            yield return Frames(2);
            Check("W-030 F1 the two MOVED markers stay while Shift is held and sit on different rooms", rd.MarkerCount == 2 && rd.MarkerPos(0) != rd.MarkerPos(1));
            CaptureW30("07-f1-1-3-moved-x2");
            shift = false;
            yield return Frames(2);
            Check("W-030 F1 1-3 released: markers gone", rd.MarkerCount == 0);

            // ===== F5: 바뀔 것이 없는 방 -> NO EFFECT 0.6초 =====
            Dt.LevelData ne = UnityEngine.Object.Instantiate(LoadLevel("TestRequest.asset"));
            ne.hasStartAim = true; ne.startAimFloor = 1; ne.startAimCol = 2;   // 문 (1,2): 현실 열림 -> 설계도 열림(바뀌지 않는 방)
            shift = false;
            SetActiveRequest(0);
            view.DebugStartLevel(ne);
            yield return Frames(3);
            Check("W-030 F5 test level: aim = the door room (1,2) whose door does not change", Sim.Aim == P(2, 1));
            shift = true;
            tPress = Now;
            yield return Frames(2);
            Check("W-030 F5 Shift on a room where nothing changes: grey 'NO EFFECT' marker above the aim frame (no state marker)", rd.MarkerCount == 1 && rd.MarkerGray(0) && rd.MarkerText(0) == Fx.NoEffectText);
            CaptureW30("08-f5-no-effect");
            yield return WaitUntil(() => rd.MarkerCount == 0, "W-030 NO EFFECT ends", 600);
            hintSeconds = Now - tPress;
            Check("W-030 F5 NO EFFECT lasts 0.6 s (measured " + hintSeconds.ToString("0.00") + " s)", hintSeconds > 0.5 && hintSeconds < 0.85);
            yield return WaitReal(0.3);
            Check("W-030 F5 not shown again while Shift stays held", rd.MarkerCount == 0);
            Check("W-030 F5 tracking still rises (judgement unchanged: tracking " + Sim.Tracking.ToString("0.0") + " after about " + (hintSeconds + 0.3).ToString("0.0") + " s)", Sim.Tracking > 0f);
            shift = false;
            yield return Frames(2);
            UnityEngine.Object.DestroyImmediate(ne);

            // ===== F3 작동 순간(화면): 0-1에서 계단을 닫아 조력자가 그냥 지나치는 순간 =====
            yield return StartLevelFor(0, "Ch0Home_1.asset");
            Advance(() => Sim.TargetFloor == 1 && Sim.TargetX >= 515f, 40f);
            shift = true;
            yield return Frames(2);
            int logBefore = view.CommandLog.Count, ringsBefore = rd.UseRingsStarted;
            Advance(() => Sim.UseSeq > 0, 5f);
            yield return null;
            int lastLog = view.CommandLog.Count - 1;
            Check("W-030 F3 0-1 runner passes the stairs sealed by Shift: command log gets '> STAIRS SKIPPED' marked as a use line (white, not the green command color)",
                lastLog >= 0 && view.CommandLog[lastLog] == "> STAIRS SKIPPED" && view.CommandLogIsUse(lastLog) && view.CommandLog.Count <= logBefore + 1);
            Check("W-030 F3 a ring started on that event (kind STAIRS SKIPPED), radius 2 px at its first tick", rd.UseRingsStarted == ringsBefore + 1 && rd.LastRingKind == UseKind.StairsSkipped && rd.UseRingActive == 1 && rd.LastRingHalf == Fx.UseRingStartPx);
            double tRing = Now;
            bool captured = false;
            int maxHalf = rd.LastRingHalf;
            for (int i = 0; i < 400 && rd.UseRingActive > 0; i++)
            {
                maxHalf = Mathf.Max(maxHalf, rd.LastRingHalf);
                if (!captured && rd.LastRingHalf >= 6) { captured = true; CaptureW30("09-f3-ring-stairs-skipped"); }
                yield return null;
            }
            double ringSeconds = Now - tRing;
            Check("W-030 F3 the ring grows 2 -> 10 px (max seen " + maxHalf + ") and ends after about 0.3 s (measured " + ringSeconds.ToString("0.00") + " s)", maxHalf >= Fx.UseRingEndPx - 1 && ringSeconds > 0.2 && ringSeconds < 0.5 && rd.UseRingActive == 0);
            Check("W-030 F3 the ring has a 9-tick (0.3 s) length and a 2 -> 10 px range in Fx", Fx.UseRingTicks == 9 && Fx.UseRingStartPx == 2 && Fx.UseRingEndPx == 10);
            shift = false;
            yield return Frames(2);

            // 1-3: 맞바꿈으로 실려 감 + 방 이음(링과 기록 줄)
            yield return StartLevelFor(4, "Ch1Old_3.asset");
            Check("W-030 F3 1-3 aim to the shop", AimToSim(Sim, 1, 1));
            Advance(() => Sim.TargetFloor == 1 && Sim.TargetX >= 277f && Sim.TargetFacingRight, 60f);
            shift = true;
            yield return Frames(2);
            next.Right = true;
            yield return Frames(2);
            Check("W-030 F3 1-3 Shift + right with the helper inside: '> CARRIED' on the command log and a ring at the helper", LogHas("> CARRIED") && Sim.UseSeq >= 1 && rd.UseRingsStarted >= 1);
            Advance(() => Sim.TargetX >= 490f, 10f);
            yield return Frames(2);
            Check("W-030 F3 1-3 the helper walks out of the slid room into the lobby: '> ROOM BRIDGED' on the command log", LogHas("> ROOM BRIDGED"));
            CaptureW30("10-f3-1-3-carried-bridged-log");
            shift = false;
            yield return Frames(2);
            note = "";
            foreach (string l in view.CommandLog) note += l + " | ";
            Note("W-030 1-3 command log at the end [simulation]: " + note);
        }

        // ----- W-031: 화면 확인(시작 화면·맨 위층 캡처, 새 맵이 화면 그림 모음으로 그려지는지) -----

        static string CaptureDir31 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-031"));

        static void CaptureW31(string name)
        {
            Directory.CreateDirectory(CaptureDir31);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir31, name + "_1920x1080.png"), 1920, 1080);
        }

        static IEnumerator W031Checks()
        {
            // ===== 0-2: 4줄(3F·2F·1F·B1) =====
            yield return StartLevelFor(1, "Ch0Home_2.asset");
            Check("W-031 0-2 on screen: 4 floors x 4 cols, layout ch0_home2 loaded with every room texture in the art set, rate scale 1.0",
                Sim.Board.Floors == 4 && Sim.Board.Cols == 4 && Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch0_home2/layout" && LayoutArtOk(Sim.Board.Layout) && Near(Sim.Board.TrackingRateScale, 1f, 0.0001f));
            yield return Frames(2);
            CaptureW31("01-0-2-start");
            yield return Frames(1);
            Advance(() => Sim.TargetFloor == 0, 30f);     // 아무것도 누르지 않으면 2F 엘리베이터가 3F로 올림(맨 위층 장면)
            yield return Frames(3);
            Check("W-031 0-2 top floor: the 2F elevator lifts the runner to 3F col 1 (floor 0)", Sim.TargetFloor == 0);
            CaptureW31("02-0-2-top-floor-3F");
            yield return Frames(1);

            // ===== 1-1: 3F·2F·1F x 5열 =====
            yield return StartLevelFor(2, "Ch1Old_1.asset");
            Check("W-031 1-1 on screen: 3 floors x 5 cols, layout ch1_old1 loaded with every room texture in the art set, rate scale 0.7, start 1F col 0",
                Sim.Board.Floors == 3 && Sim.Board.Cols == 5 && Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch1_old1/layout" && LayoutArtOk(Sim.Board.Layout) && Near(Sim.Board.TrackingRateScale, 0.7f, 0.0001f)
                && Sim.TargetFloor == 2 && Sim.TargetX <= 40f);
            yield return Frames(2);
            CaptureW31("03-1-1-start");
            yield return Frames(1);
            Advance(() => Sim.TargetFloor == 0 && Sim.TargetX >= 250f, 90f);   // 계단 둘로 3F A 끝 방까지
            Sim.ShiftHeld = false; AimToSim(Sim, 1, 0);
            yield return Frames(30);
            Check("W-031 1-1 top floor: two open staircases carry the runner from 1F to the 3F end room", Sim.TargetFloor == 0 && Sim.Aim == P(1, 0));
            CaptureW31("04-1-1-top-floor-3F");
            yield return Frames(1);

            // ===== 1-3: 3F·2F·1F x 5열 =====
            yield return StartLevelFor(4, "Ch1Old_3.asset");
            Check("W-031 1-3 on screen: 3 floors x 5 cols, layout ch1_old3 loaded with every room texture in the art set, rate scale 0.7, start 2F col 0",
                Sim.Board.Floors == 3 && Sim.Board.Cols == 5 && Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch1_old3/layout" && LayoutArtOk(Sim.Board.Layout) && Near(Sim.Board.TrackingRateScale, 0.7f, 0.0001f)
                && Sim.TargetFloor == 1 && Sim.Aim == P(1, 1));
            yield return Frames(2);
            CaptureW31("05-1-3-start");
            yield return Frames(1);
            Advance(() => Sim.TargetFloor == 1 && Sim.TargetX >= 277f && Sim.TargetFacingRight, 60f);
            shift = true;
            next.Up = true;                                 // 맞바꿈: 빈 점포(조력자 포함) <-> 3F 복도
            yield return null;
            yield return Frames(40);
            Check("W-031 1-3 top floor: Shift + up swaps the empty store with the 3F corridor, the runner is carried to 3F", Sim.Pushed && Sim.CellOf(1, 1) == P(1, 0) && Sim.CellOf(0, 1) == P(1, 1) && Sim.TargetFloor == 0);
            CaptureW31("06-1-3-top-floor-3F");
            yield return Frames(1);
            shift = false;
            yield return Frames(3);

            // ===== 1-4: 4F·3F·2F·1F x 4열 =====
            yield return StartLevelFor(7, "Ch1Old_4.asset");
            Check("W-031 1-4 on screen: 4 floors x 4 cols, layout ch1_old4 loaded with every room texture in the art set, rate scale 0.7, start 2F col 0",
                Sim.Board.Floors == 4 && Sim.Board.Cols == 4 && Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch1_old4/layout" && LayoutArtOk(Sim.Board.Layout) && Near(Sim.Board.TrackingRateScale, 0.7f, 0.0001f)
                && Sim.TargetFloor == 2 && FindObj(Sim.Board, Dt.ObjectKind.Door, 1, 2) != null);
            yield return Frames(2);
            CaptureW31("07-1-4-start");
            yield return Frames(1);
            Sim.ShiftHeld = false; AimToSim(Sim, 2, 2);
            Advance(() => Sim.TargetFloor == 2 && Sim.TargetWaiting, 90f);
            shift = true; Advance(() => Sim.TargetX > 330f, 10f); shift = false;
            Advance(() => Sim.RelayUsed, 10f);
            Sim.ShiftHeld = false; AimToSim(Sim, 3, 2);
            Advance(() => Sim.TargetFloor == 2 && Sim.TargetX >= 425f, 20f);
            shift = true; Advance(() => Sim.TargetFloor == 1, 10f); shift = false;
            Sim.ShiftHeld = false; AimToSim(Sim, 2, 1);
            Advance(() => Sim.TargetFloor == 1 && Sim.TargetWaiting, 30f);
            yield return Frames(3);
            Check("W-031 1-4 3F shutter closed: the runner waits in front of it (3F, x429) and the shutter sector is the aim", Sim.TargetFloor == 1 && Sim.TargetWaiting && Near(Sim.TargetX, 429f, 0.6f) && Sim.Aim == P(2, 1));
            CaptureW31("08-1-4-shutter-wait-3F");
            yield return Frames(1);
            shift = true; Advance(() => Sim.TargetX <= 410f, 10f); shift = false;
            Sim.ShiftHeld = false; AimToSim(Sim, 0, 0);
            Advance(() => Sim.TargetFloor == 0 && Sim.TargetX <= 343f, 60f);
            yield return Frames(30);
            Check("W-031 1-4 top floor: the shutter opens, the stairs 3F -> 4F lead to the roof hall, the runner walks toward the roof exit room (4F)", Sim.TargetFloor == 0 && Sim.Aim == P(0, 0));
            CaptureW31("09-1-4-top-floor-4F");
            yield return Frames(1);
        }

        // ----- W-033: 장치 그림 보강(D-123 ①) · 조작 튜토리얼(D-122) -----

        static string CaptureDir33 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-033"));

        static void CaptureW33(string name)
        {
            Directory.CreateDirectory(CaptureDir33);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir33, name + "_1920x1080.png"), 1920, 1080);
        }

        static IEnumerator WaitFrozen(int index, string what, int maxFrames = 9000)
        {
            yield return WaitUntil(() => view.Tut != null && view.Tut.Frozen && view.Tut.Index == index, what, maxFrames);
            yield return Frames(3);   // 그림이 단계를 그릴 시간
        }

        /// <summary>한 프레임 키 입력(방향키·Enter). 다음 프레임에 들어가고 한 프레임을 더 기다린다.</summary>
        static IEnumerator Tap(string which)
        {
            if (which == "Enter") next.Enter = true;
            else if (which == "Bracket") next.Bracket = true;
            else if (which == "Up") next.Up = true;
            else if (which == "Down") next.Down = true;
            else if (which == "Left") next.Left = true;
            else if (which == "Right") next.Right = true;
            yield return null;
            yield return null;
        }

        /// <summary>멈춘 단계에서: 시간이 안 흐르고(대상 x·추적도·카운트다운 그대로), 틀린 키는 무시되며 배지가 흔들린다.</summary>
        static IEnumerator FrozenAndWrongKeys(string label, int index, params string[] wrongKeys)
        {
            float x0 = Sim.TargetX, tr0 = Sim.Tracking;
            int floor0 = Sim.TargetFloor;
            Vector2Int aim0 = Sim.Aim;
            bool shift0 = Sim.ShiftHeld;
            yield return Frames(30);
            Check("W-033 " + label + ": game time stopped (target x, floor, tracking unchanged over 30 frames)",
                view.Tut.Frozen && Near(Sim.TargetX, x0, 0.0001f) && Sim.TargetFloor == floor0 && Near(Sim.Tracking, tr0, 0.0001f));
            int wrong0 = view.Tut.WrongSeq;
            foreach (string k in wrongKeys) yield return Tap(k);
            Check("W-033 " + label + ": wrong keys (" + string.Join(",", wrongKeys) + ") ignored - same step, aim and Shift unchanged, badge shook once per wrong key",
                view.Tut.Frozen && view.Tut.Index == index && Sim.Aim == aim0 && Sim.ShiftHeld == shift0 && view.Tut.WrongSeq == wrong0 + wrongKeys.Length);
        }

        static bool HoleIs(int i, RectInt r)
        {
            var h = view.Renderer.TutorialHoles;
            return h.Count > i && h[i].x == r.x && h[i].y == r.y && h[i].width == r.width && h[i].height == r.height;
        }

        static IEnumerator W033Checks()
        {
            Tutorial.AutoPass = false;

            // ===== 그림: 1-4 셔터 전용 그림, 1-3 계단실, 전환 프레임 목록, 튜토리얼 UI =====
            var sizes = new Dictionary<string, Vector2Int>
            {
                { "rollshutter_closed", P(40, 81) }, { "rollshutter_open", P(40, 81) }, { "bp_rollshutter_closed", P(42, 83) }, { "bp_rollshutter_open", P(42, 83) },
                { "bp_rollshutter_t1", P(42, 83) }, { "bp_rollshutter_t2", P(42, 83) }, { "bp_shutter_t1", P(42, 72) }, { "bp_shutter_t2", P(42, 72) },
                { "bp_hatch_t1", P(42, 12) }, { "bp_hatch_t2", P(42, 12) }, { "blocker_t1", P(160, 96) }, { "blocker_t2", P(160, 96) },
            };
            string bad = "";
            foreach (var kv in sizes)
            {
                string key = "objects128/" + kv.Key;
                if (!(view.Canvas.Has(key) && view.Canvas.WidthOf(key) == kv.Value.x && view.Canvas.HeightOf(key) == kv.Value.y)) bad += " " + kv.Key;
            }
            Check("W-033 objects128: 12 new pictures (rollshutter closed/open + blueprint, 6 blueprint transition frames, 2 laser frames) in the art set at 4x size" + (bad.Length > 0 ? " - bad:" + bad : ""), bad.Length == 0);
            string badUi = "";
            foreach (string k in new[] { "tut_key_shift", "tut_key_left", "tut_key_up", "tut_key_right", "tut_key_down", "tut_key_enter", "tut_bubble", "tut_tail_up", "tut_tail_down", "tut_tail_left", "tut_tail_right" })
                if (!view.Canvas.Has("ui/" + k)) badUi += " " + k;
            Check("W-033 tutorial UI art: 6 key badges (11 high like badge_enter), bubble 9-slice, 4 tails in the art set" + (badUi.Length > 0 ? " - missing:" + badUi : ""),
                badUi.Length == 0 && view.Canvas.HeightOf("ui/tut_key_enter") == view.Canvas.HeightOf("ui/badge_enter") && view.Canvas.WidthOf("ui/tut_key_enter") == view.Canvas.WidthOf("ui/badge_enter"));
            {
                var l4 = LoadLevel("Ch1Old_4.asset");
                var l3 = LoadLevel("Ch1Old_3.asset");
                bool doorArt = false;
                foreach (Dt.ObjectDef od in l4.objects) if (od.kind == Dt.ObjectKind.Door && od.artKey == "rollshutter" && od.floor == 1 && od.col == 2 && !od.realityOn && od.blueprintOn) doorArt = true;
                Check("W-033 1-4: the 3F shutter keeps kind Door (closed -> open) and uses the rollshutter art (artKey)", doorArt);
                Check("W-033 1-2/1-3/1-4 have no tutorial steps (guide off)", LoadLevel("Ch1Old_2.asset").tutorialSteps.Length == 0 && l3.tutorialSteps.Length == 0 && l4.tutorialSteps.Length == 0 && !l3.showGuide && !l4.showGuide);
            }

            // ===== 1-3: 2F 계단이 계단실 방 그림 위에 =====
            yield return StartLevelFor(4, "Ch1Old_3.asset");
            {
                SceneLayout.SectorView sv = Sim.Board.Layout.Sector(1, 3);
                SectorObject st = FindObj(Sim.Board, Dt.ObjectKind.Stairs, 1, 3);
                Check("W-033 1-3 2F stairs: the room under the stairs (2F col 3) is ot_stairwell (not the corridor), stairs x 80 / draw x 60 unchanged, no tutorial box",
                    sv != null && sv.Room != null && sv.Room.Name == "ot_stairwell" && st != null && Near(st.Def.x, 80f) && Near(st.Def.drawX, 60f) && view.Tut == null);
            }
            for (int i = 0; i < 6 && Sim.Aim != P(2, 1); i++) yield return Tap("Right");   // 실제 방향키로 조준(시작 방 -> 레일 방 -> 레이저 복도). 계단(열림 -> 열림)은 조작 대상이 아니라 계단실에는 조준이 가지 않는다 — 카메라가 레이저 복도 옆 계단실을 보이게 한다
            Check("W-033 1-3: the right arrow aims at the laser corridor (2F col 2), the stairwell is on screen next to it", Sim.Aim == P(2, 1));
            yield return Frames(150);
            CaptureW33("02-1-3-stairwell");
            yield return Frames(1);

            // ===== 1-4 셔터: 닫힘 · 전환 · 열림 + 전환 프레임 순서 =====
            Tutorial.AutoPass = true;
            yield return StartLevelFor(7, "Ch1Old_4.asset");
            {
                SectorObject door = FindObj(Sim.Board, Dt.ObjectKind.Door, 1, 2);
                Check("W-033 1-4 shutter picture keys: reality closed -> objects128/rollshutter_closed, blueprint -> objects128/bp_rollshutter_open",
                    door != null && view.Renderer.ResolveObjectKey(door) == "objects128/rollshutter_closed" && view.Renderer.ResolveBlueprintKey(door) == "objects128/bp_rollshutter_open");
                shift = false;
                AimToSim(Sim, 2, 1);
                yield return Frames(150);
                CaptureW33("01a-1-4-shutter-closed");
                yield return Frames(1);
                var seen = new List<string>();
                int draws0 = view.Renderer.TransitionDraws;
                shift = true;
                for (int i = 0; i < 40; i++)
                {
                    yield return null;
                    string k = view.Renderer.LastTransitionKey;
                    if (k != null && (seen.Count == 0 || seen[seen.Count - 1] != k)) seen.Add(k);
                }
                Check("W-033 press: the shutter blueprint plays 2 transition frames in order t1 then t2 (closed -> open), then the open blueprint stays (frames seen: " + string.Join(",", seen) + ")",
                    seen.Count >= 2 && seen[0] == "objects128/bp_rollshutter_t1" && seen[1] == "objects128/bp_rollshutter_t2" && view.Renderer.TransitionDraws > draws0);
                yield return Frames(5);
                CaptureW33("01c-1-4-shutter-open");
                yield return Frames(1);
                view.Renderer.LastTransitionKeyReset();
                shift = false;
                var seen2 = new List<string>();
                for (int i = 0; i < 40; i++)
                {
                    yield return null;
                    string k = view.Renderer.LastTransitionKey;
                    if (k != null && (seen2.Count == 0 || seen2[seen2.Count - 1] != k)) seen2.Add(k);
                }
                Check("W-033 release: the ghost plays the frames reversed (blueprint open -> reality closed: t2 then t1; frames seen: " + string.Join(",", seen2) + ")",
                    seen2.Count >= 2 && seen2[0] == "objects128/bp_rollshutter_t2" && seen2[1] == "objects128/bp_rollshutter_t1");
                // 전환 캡처: 저장이 오래 걸려 틱을 건너뛰지 않도록 순서 확인과 따로, 첫 전환 프레임(t1)이 그려진 직후 한 장만 저장한다
                yield return Frames(30);
                view.Renderer.LastTransitionKeyReset();
                shift = true;
                for (int i = 0; i < 40 && view.Renderer.LastTransitionKey != "objects128/bp_rollshutter_t1"; i++) yield return null;
                CaptureW33("01b-1-4-shutter-transition");
                yield return Frames(1);
                shift = false;
                yield return Frames(30);
            }
            // 레이저(섹터 전체): 켜짐 -> 꺼짐 t1, t2 / 꺼짐 -> 켜짐 t2, t1
            AimToSim(Sim, 2, 2);
            yield return Frames(40);
            {
                var seen = new List<string>();
                view.Renderer.LastTransitionKeyReset();
                shift = true;
                for (int i = 0; i < 40; i++)
                {
                    yield return null;
                    string k = view.Renderer.LastTransitionKey;
                    if (k != null && (seen.Count == 0 || seen[seen.Count - 1] != k)) seen.Add(k);
                }
                Check("W-033 laser press: the sector-wide laser plays blocker_t1 then blocker_t2 (3/4 beams, 1/4 beams) before it is gone (frames seen: " + string.Join(",", seen) + ")",
                    seen.Count >= 2 && seen[0] == "objects128/blocker_t1" && seen[1] == "objects128/blocker_t2");
                view.Renderer.LastTransitionKeyReset();
                shift = false;
                var seen2 = new List<string>();
                for (int i = 0; i < 40; i++)
                {
                    yield return null;
                    string k = view.Renderer.LastTransitionKey;
                    if (k != null && (seen2.Count == 0 || seen2[seen2.Count - 1] != k)) seen2.Add(k);
                }
                Check("W-033 laser release: blocker_t2 then blocker_t1 before the solid laser returns (frames seen: " + string.Join(",", seen2) + ")",
                    seen2.Count >= 2 && seen2[0] == "objects128/blocker_t2" && seen2[1] == "objects128/blocker_t1");
            }

            // ===== 0-1 튜토리얼(실제 입력·실시간) =====
            Tutorial.AutoPass = false;
            yield return StartLevelFor(0, "Ch0Home_1.asset");
            Check("W-033 0-1 has 6 steps, none passed yet", view.Tut != null && view.Tut.StepCount == 6);
            yield return WaitFrozen(0, "0-1 step 1");
            Check("W-033 0-1 step 1 (screen intro): frozen at once, Enter-type, ENTER badge, highlight = scene view only (1 hole)",
                view.Tut.StepText == "AN INTRUDER. KEEP THEM AWAY FROM YOUR ROOM." && string.Join(" ", Tutorial.KeysOf(view.Tut.StepAt(0))) == "ENTER" && view.Renderer.TutorialHoles.Count == 1
                && HoleIs(0, new RectInt(GameRenderer.VX, GameRenderer.VY, GameRenderer.VW, GameRenderer.VH)));
            yield return FrozenAndWrongKeys("0-1 step 1", 0, "Left", "Bracket");
            yield return Tap("Enter");
            yield return WaitFrozen(1, "0-1 step 2");
            Check("W-033 0-1 step 2 (TRACE gauge): order 1 -> 2, highlight = TRACE gauge, text 'SHIFT RAISES YOUR TRACE...'",
                view.Tut.StepText == "SHIFT RAISES YOUR TRACE. AT 100 YOU ARE FOUND." && view.Renderer.TutorialHoles.Count == 1 && HoleIs(0, new RectInt(62, 315, 152, 17)));
            CaptureW33("00-0-1-step2-trace");
            yield return Tap("Enter");
            yield return WaitFrozen(2, "0-1 step 3 (runner reaches the stairs)");
            Check("W-033 0-1 step 3 (seal the stairs): Hold-type, SHIFT badge, highlight = the stairs sector, runner 1F x >= 480",
                view.Tut.StepText == "HOLD SHIFT TO SEAL THE STAIRS." && TutIs(2, "SHIFT", "HOLD SHIFT TO SEAL THE STAIRS.") && HoleIs(0, view.CellRect(P(3, 1))) && Sim.TargetFloor == 1 && Sim.TargetX >= 480f);
            yield return Frames(2);
            CaptureW33("03-0-1-step3-seal-stairs");
            yield return Frames(1);
            yield return FrozenAndWrongKeys("0-1 step 3", 2, "Enter", "Left");
            shift = true;
            yield return Frames(3);
            Check("W-033 0-1 step 3: pressing Shift advances (step 4 next), the game runs again, Shift is held", view.Tut.Index == 3 && !view.Tut.Frozen && Sim.ShiftHeld);
            yield return WaitFrozen(3, "0-1 step 4 (runner passed the stairs)");
            Check("W-033 0-1 step 4 (let go): Enter-type, highlight = TRACE gauge, the sealed stairs held the runner on 1F past x 565, Shift still held in the sim (frozen state kept)",
                view.Tut.StepText == "IT ONLY HOLDS WHILE YOU PRESS." && HoleIs(0, new RectInt(62, 315, 152, 17)) && Sim.TargetFloor == 1 && Sim.TargetX >= 565f && Sim.ShiftHeld);
            yield return Tap("Enter");
            yield return WaitFrozen(4, "0-1 step 5 (runner passed the stairs again, heading left)");
            shift = false;
            Check("W-033 0-1 step 5 (aim): Press-type with the left arrow badge, highlight = the hatch sector (1F col 2), runner on 1F facing left",
                TutIs(4, "←", "PRESS ← TO AIM AT THE HATCH.") && HoleIs(0, view.CellRect(P(2, 1))) && Sim.TargetFloor == 1 && !Sim.TargetFacingRight && Sim.Aim == P(3, 1));
            yield return Frames(2);
            CaptureW33("04-0-1-step5-aim-hatch");
            yield return Frames(1);
            yield return FrozenAndWrongKeys("0-1 step 5", 4, "Right", "Down", "Enter");
            yield return Tap("Left");
            yield return Frames(3);
            Check("W-033 0-1 step 5: the left arrow aims at the hatch sector (2,1) and advances to step 6, the game runs again", Sim.Aim == P(2, 1) && view.Tut.Index == 5 && !view.Tut.Frozen);
            yield return WaitFrozen(5, "0-1 step 6 (runner heads for the hatch)");
            Check("W-033 0-1 step 6 (drop them): Hold-type, SHIFT badge, highlight = the hatch sector, runner approaching from the left (x >= 340, facing right)",
                TutIs(5, "SHIFT", "HOLD SHIFT TO DROP THEM.") && HoleIs(0, view.CellRect(P(2, 1))) && Sim.TargetX >= 340f && Sim.TargetFacingRight);
            yield return FrozenAndWrongKeys("0-1 step 6", 5, "Left");
            shift = true;
            yield return WaitUntil(() => Sim.Outcome != Outcome.Running, "0-1 hatch drop after the tutorial", 2000);
            shift = false;
            Check("W-033 0-1 after all 6 steps: Shift held at the hatch drops them -> Complete, B1 col 2; the judgement is the normal one", Sim.Outcome == Outcome.Complete && Sim.TargetFloor == 2 && view.Tut.Index == 6);

            // ===== 0-2 튜토리얼 =====
            yield return StartLevelFor(1, "Ch0Home_2.asset");
            Check("W-033 0-2 has 5 steps", view.Tut != null && view.Tut.StepCount == 5);
            yield return WaitFrozen(0, "0-2 step 1 (runner near the elevator)");
            Check("W-033 0-2 step 1 (elevator): Hold-type, SHIFT badge, highlight = the elevator sector (2F col 1), runner x >= 190 facing right",
                TutIs(0, "SHIFT", "THE ELEVATOR GOES UP TO YOU. HOLD SHIFT TO STOP IT.") && HoleIs(0, view.CellRect(P(1, 1))) && Sim.TargetX >= 190f && Sim.TargetFloor == 1);
            yield return FrozenAndWrongKeys("0-2 step 1", 0, "Right");
            shift = true;
            yield return Frames(3);
            Check("W-033 0-2 step 1: Shift advances", view.Tut.Index == 1 && !view.Tut.Frozen);
            yield return WaitUntil(() => Sim.TargetX >= 262f, "0-2 runner passes the stopped elevator");
            shift = false;
            yield return WaitFrozen(1, "0-2 step 2 (runner waits before the laser)");
            Check("W-033 0-2 step 2 (whole room): Press-type with 4 arrow badges, highlight = the laser sector (2F col 2), runner waiting on 2F",
                TutIs(1, "← ↑ → ↓", "SOME ROOMS SHIFT AS A WHOLE. AIM AT THE ROOM.") && HoleIs(0, view.CellRect(P(2, 1))) && Sim.TargetWaiting && Sim.TargetFloor == 1);
            yield return FrozenAndWrongKeys("0-2 step 2", 1, "Enter", "Bracket");
            yield return Tap("Right");
            yield return Frames(3);
            Check("W-033 0-2 step 2: an arrow key aims at the laser sector (2,1) and advances (the step stays until the aim is there)", Sim.Aim == P(2, 1) && view.Tut.Index == 2);
            shift = true;
            yield return WaitFrozen(2, "0-2 step 3 (relay used)");
            Check("W-033 0-2 step 3 (relay): Enter-type, two highlights = the laser room + the TRACE gauge",
                TutIs(2, "ENTER", "RELAYS LOWER YOUR TRACE.") && view.Renderer.TutorialHoles.Count == 2 && HoleIs(0, view.CellRect(P(2, 1))) && HoleIs(1, new RectInt(62, 315, 152, 17)));
            yield return Tap("Enter");
            shift = false;
            yield return WaitFrozen(3, "0-2 step 4 (runner reaches 1F)");
            Check("W-033 0-2 step 4 (hatch aim): Press-type with the down arrow badge, highlight = the 1F hatch room (col 2), runner on 1F",
                TutIs(3, "↓", "PRESS ↓ TO AIM AT THE HATCH.") && HoleIs(0, view.CellRect(P(2, 2))) && Sim.TargetFloor == 2);
            yield return Frames(2);
            CaptureW33("05-0-2-step4-aim-hatch");
            yield return Frames(1);
            yield return FrozenAndWrongKeys("0-2 step 4", 3, "Up", "Left");
            yield return Tap("Down");
            yield return Frames(3);
            Check("W-033 0-2 step 4: the down arrow aims at the hatch room (2,2) and advances", Sim.Aim == P(2, 2) && view.Tut.Index == 4 && !view.Tut.Frozen);
            yield return WaitFrozen(4, "0-2 step 5 (runner nears the hatch)");
            Check("W-033 0-2 step 5 (drop them): Hold-type, SHIFT badge", TutIs(4, "SHIFT", "NOW HOLD SHIFT TO DROP THEM.") && HoleIs(0, view.CellRect(P(2, 2))));
            shift = true;
            yield return WaitUntil(() => Sim.Outcome != Outcome.Running, "0-2 hatch drop", 2000);
            shift = false;
            Check("W-033 0-2 after all 5 steps: hatch opened -> B1 boiler room -> Complete", Sim.Outcome == Outcome.Complete && Sim.TargetFloor == 3 && view.Tut.Index == 5);

            // ===== 0-1 실패 재시작: 지난 단계는 다시 띄우지 않는다 / 처음부터 다시 하면 다시 띄운다 =====
            yield return StartLevelFor(0, "Ch0Home_1.asset");
            yield return WaitFrozen(0, "0-1 again step 1");
            yield return Tap("Enter");
            yield return WaitFrozen(1, "0-1 again step 2");
            yield return Tap("Enter");
            yield return WaitFrozen(2, "0-1 again step 3");
            yield return Tap("Enter");   // 틀린 키
            shift = true;
            yield return Frames(3);
            shift = false;               // 계단을 닫지 않고 바로 뗌 -> 2층으로 올라가 실패
            yield return WaitUntil(() => view.FailElapsed >= 0f, "0-1 stairs left open -> fail point");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing && Sim.Outcome == Outcome.Running && view.FailElapsed < 0f, "restart after the fail blink", 600);
            yield return Frames(5);
            Check("W-033 after a fail restart the steps already passed (1-3) are not shown again: index kept at 3, no step frozen, fail notice shown",
                view.Tut.Index == 3 && !view.Tut.Frozen && view.Tut.NoticeActive);
            yield return Frames(30);
            Check("W-033 after the fail restart the game keeps running (the runner restarted, time moves)", Sim.TargetX > 190f - 0.01f && !view.Tut.Frozen);
            view.SendMessage("OnApplicationFocus", false);   // 일시정지 -> RESTART: 의뢰를 처음부터 다시
            yield return null;
            Check("W-033 pause while playing", view.CurrentPhase == GameView.Phase.Paused);
            yield return Press("Down");
            yield return Press("Enter");
            yield return Frames(5);
            Check("W-033 pause Restart replays the whole request: step index back to 0 and step 1 shown again", view.CurrentPhase == GameView.Phase.Playing && view.Tut.Frozen && view.Tut.Index == 0);

            // ===== 1-1 튜토리얼 =====
            yield return StartLevelFor(2, "Ch1Old_1.asset");
            Check("W-033 1-1 has 5 steps (guide only, not a tutorial request)", view.Tut != null && view.Tut.StepCount == 5 && !Sim.Board.Tutorial);
            yield return WaitFrozen(0, "1-1 step 1");
            Check("W-033 1-1 step 1 (missions): Enter-type, highlight = the MISSION panel",
                TutIs(0, "ENTER", "EXTRA MISSIONS EARN STARS. THE EXIT ENDS THE JOB.") && HoleIs(0, new RectInt(436, 32, 190, 145)));
            yield return Tap("Enter");
            yield return WaitFrozen(1, "1-1 step 2 (runner reaches 3F)", 14000);
            Check("W-033 1-1 step 2 (rail room): Press-type with 4 arrow badges, highlight = the rail room (3F col 1), runner on 3F",
                TutIs(1, "← ↑ → ↓", "SOME ROOMS SIT ON RAILS. AIM AT ONE.") && HoleIs(0, view.CellRect(P(1, 0))) && Sim.TargetFloor == 0);
            yield return FrozenAndWrongKeys("1-1 step 2", 1, "Enter");
            for (int i = 0; i < 8 && view.Tut.Index == 1; i++)
            {
                Vector2Int a = Sim.Aim;
                if (a.y > 0) yield return Tap("Up");
                else if (a.x < 1) yield return Tap("Right");
                else yield return Tap("Left");
                if (view.Tut.Index == 1) Check("W-033 1-1 step 2: while the aim is not on the rail room the step stays (aim " + Sim.Aim + ")", Sim.Aim != P(1, 0) && view.Tut.Frozen);
            }
            yield return Frames(3);
            Check("W-033 1-1 step 2: the aim reaches the rail room (1,0) -> step 3 starts at once", Sim.Aim == P(1, 0) && view.Tut.Index == 2 && view.Tut.Frozen);
            yield return Frames(3);
            Check("W-033 1-1 step 3 (slide): ShiftArrow-type, badges SHIFT + right arrow, two highlights = the rail room + the empty cell next to it",
                TutIs(2, "SHIFT →", "HOLD SHIFT, THEN PRESS → TO SLIDE IT.") && view.Renderer.TutorialHoles.Count == 2 && HoleIs(0, view.CellRect(P(1, 0))) && HoleIs(1, view.CellRect(P(2, 0))));
            CaptureW33("06-1-1-step3-slide");
            yield return Frames(1);
            yield return FrozenAndWrongKeys("1-1 step 3", 2, "Right", "Enter");
            shift = true;
            yield return Frames(3);
            Check("W-033 1-1 step 3: Shift alone does not finish the step (the room is not pushed yet)", view.Tut.Index == 2 && view.Tut.Frozen && Sim.ShiftHeld && !Sim.Pushed);
            yield return Tap("Left");
            Check("W-033 1-1 step 3: the wrong direction with Shift (left arrow) is ignored", view.Tut.Index == 2 && !Sim.Pushed);
            yield return Tap("Right");
            yield return Frames(3);
            Check("W-033 1-1 step 3: Shift + right arrow slides the rail room (2,0) -> step 4 (FIRST PUSHED) follows", Sim.Pushed && Sim.CellOf(0, 1) == P(2, 0) && view.Tut.Index == 3);
            yield return WaitFrozen(3, "1-1 step 4");
            Check("W-033 1-1 step 4 (let go): the highlight follows the pushed room to its new cell (2,0); the sim keeps the pushed state while frozen",
                TutIs(3, "ENTER", "LET GO ONLY AFTER SHE STEPS OUT.") && HoleIs(0, view.CellRect(P(2, 0))) && Sim.Pushed && Sim.ShiftHeld);
            yield return Tap("Enter");
            yield return WaitFrozen(4, "1-1 step 5 (runner steps out into B)", 6000);
            Check("W-033 1-1 step 5 (swap notice): Enter-type, one highlight = the pushed rail room (now at col 2; the off-screen neighbor room is not drawn, visible width < 16), runner on 3F col 3",
                TutIs(4, "ENTER", "IF A ROOM IS NEXT TO IT, THEY SWAP PLACES.") && view.Renderer.TutorialHoles.Count == 1 && HoleIs(0, view.CellRect(P(2, 0))) && Sim.TargetFloor == 0 && Sim.TargetX >= 500f);
            yield return Frames(2);
            CaptureW33("07-1-1-step5-swap");
            yield return Frames(1);
            yield return Tap("Enter");
            shift = false;
            yield return Frames(3);
            Check("W-033 1-1 all 5 steps passed in order (1 -> 5), game running, Shift released after the crossing returns the room", view.Tut.Index == 5 && !view.Tut.Frozen);
            Tutorial.AutoPass = true;
        }

        // ----- W-034: 한국어 버전 기반(언어 설정 · 문구 표 · 저장) -----

        /// <summary>문구 표 전체를 CSV 글로 되돌린다. ko가 null이 아니면 모든 행의 한국어 칸을 그 함수 결과로 채운다(자동 확인에서 한국어 모드를 흉내 낸다).</summary>
        static string RowsToCsv(Func<Loc.Row, string> ko)
        {
            var sb = new System.Text.StringBuilder("id,en,ko\n");
            foreach (var row in Loc.Rows)
                sb.Append('"').Append(row.id).Append("\",\"").Append(row.en.Replace("\"", "\"\"")).Append("\",\"").Append((ko != null ? ko(row) : row.ko).Replace("\"", "\"\"")).Append("\"\n");
            return sb.ToString();
        }

        /// <summary>코드가 Loc.T("id")로 부르는 id 모두(Editor·Loc.cs 제외).</summary>
        static HashSet<string> LocIdsUsedInCode()
        {
            var used = new HashSet<string>();
            var rx = new System.Text.RegularExpressions.Regex("Loc\\.T\\(\"([^\"]+)\"");
            string root = Path.Combine(Application.dataPath, "_Project/Scripts");
            foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(f);
                if (name == "Loc.cs" || name == "Mvp1SmokeCheck.cs") continue;
                foreach (System.Text.RegularExpressions.Match m in rx.Matches(File.ReadAllText(f))) used.Add(m.Groups[1].Value);
            }
            return used;
        }

        /// <summary>데이터 에셋·명령 기록에서 영문 글자로 나오는 문구 모두(Loc.Tr로 한국어를 찾는 것들).</summary>
        static HashSet<string> LocDataStrings()
        {
            var set = new HashSet<string>();
            void A(string s) { if (!string.IsNullOrEmpty(s)) set.Add(s); }
            for (int i = 0; i < view.Desk.Count; i++)
            {
                var e = view.Desk.Entry(i);
                A(e.client); A(e.title);
                foreach (string m in e.missionNames) A(m);
                if (e.level != null)
                {
                    A(e.level.buildingName); A(e.level.starTwoHint);
                    if (e.level.tutorialSteps != null) foreach (var st in e.level.tutorialSteps) A(st.text);
                }
                if (e.dialogue != null)
                {
                    if (e.dialogue.briefing != null) foreach (var l in e.dialogue.briefing) { A(l.text); A(l.speakerName); }
                    if (e.dialogue.ending != null) foreach (var l in e.dialogue.ending) { A(l.text); A(l.speakerName); }
                }
            }
            foreach (UseKind k in Enum.GetValues(typeof(UseKind))) A(Fx.UseText(k));
            A("CAMERA: SPOTTED");
            return set;
        }

        /// <summary>W-035: 한국어 칸을 일부러 비워 둔 행(사용자 번역 대기). 영어 모드와 한국어 모드 모두 영문이 나온다.</summary>
        static readonly HashSet<string> W035KoPending = new HashSet<string> { "d.title.test-2-0", "d.building.test_2_0" };

        static IEnumerator W034Checks()
        {
            var ids = AllIds();
            string originalCsv = RowsToCsv(null);

            // ----- 문구 표: 모든 id가 표에 있고, 표의 모든 행이 쓰이는가 -----
            var used = LocIdsUsedInCode();
            var missingId = new List<string>();
            foreach (string id in used) if (!Loc.Has(id)) missingId.Add(id);
            var unusedUi = new List<string>();
            int uiRows = 0, dataRows = 0;
            foreach (var row in Loc.Rows)
            {
                if (row.id.StartsWith("d.")) { dataRows++; continue; }
                uiRows++;
                if (!used.Contains(row.id)) unusedUi.Add(row.id);
            }
            var data = LocDataStrings();
            var missingData = new List<string>();
            foreach (string s in data) if (!Loc.HasEn(s)) missingData.Add(s);
            var unusedData = new List<string>();
            foreach (var row in Loc.Rows) if (row.id.StartsWith("d.") && !data.Contains(row.en)) unusedData.Add(row.id);
            Note("W-034 table: " + Loc.Rows.Count + " rows (UI " + uiRows + ", data/event " + dataRows + "), code uses " + used.Count + " ids, assets use " + data.Count + " English strings");
            Check("W-034 every Loc.T id used in code exists in the table" + (missingId.Count > 0 ? " - missing: " + string.Join(", ", missingId) : ""), missingId.Count == 0);
            Check("W-034 no unused UI row in the table" + (unusedUi.Count > 0 ? " - unused: " + string.Join(", ", unusedUi) : ""), unusedUi.Count == 0);
            Check("W-034 every English string in the request/dialogue/tutorial assets and use log has a table row" + (missingData.Count > 0 ? " - missing: " + string.Join(" | ", missingData) : ""), missingData.Count == 0);
            Check("W-034 no unused data row in the table" + (unusedData.Count > 0 ? " - unused: " + string.Join(", ", unusedData) : ""), unusedData.Count == 0);

            // ----- 영어 기본: 표의 영문이 그대로 나온다(한국어 칸이 비어 있으면 한국어 모드도 영문) -----
            Loc.Current = Language.English;
            Check("W-034 English: Loc.T and Loc.Tr return the on-screen English", Loc.T("menu.settings") == "SETTINGS" && Loc.T("result.stars", 2) == "2 / 3 STARS"
                && Loc.Tr("TRAP THE INTRUDER") == "TRAP THE INTRUDER" && Loc.Tr("NOT IN THE TABLE") == "NOT IN THE TABLE" && Loc.T("game.header", "0-1") == "SHIFT://REMOTE  -  REQUEST 0-1");
            int emptyKo = 0, pendingKo = 0;
            foreach (var row in Loc.Rows)
                if (string.IsNullOrWhiteSpace(row.ko)) { if (W035KoPending.Contains(row.id)) pendingKo++; else emptyKo++; }
            Check("W-034 every Korean cell of the table is filled (" + Loc.Rows.Count + " rows; W-035: the " + pendingKo + " TEST_2-0 / PRESS PASSED rows are left empty on purpose, not counted)", emptyKo == 0 && pendingKo == W035KoPending.Count);
            Loc.Current = Language.Korean;
            Loc.Row sRow = null;
            foreach (var row in Loc.Rows) if (row.id == "menu.settings") sRow = row;
            Check("W-034 Korean mode returns the Korean cell (id and English-key lookups)", sRow != null && Loc.T("menu.settings") == sRow.ko && Loc.T("menu.settings") != "SETTINGS" && Loc.Tr("TRAP THE INTRUDER") != "TRAP THE INTRUDER");
            Loc.LoadFrom("id,en,ko\nx,HELLO,\nd.y,BYE,\n");
            Check("W-034 an empty Korean cell falls back to English", Loc.T("x") == "HELLO" && Loc.Tr("BYE") == "BYE");
            Loc.LoadFrom(originalCsv);
            Loc.Current = Language.English;

            // ----- 설정에서 언어 바꾸기: 즉시 바뀜, 저장, 다시 읽으면 유지, Clear Save 뒤 영어 -----
            yield return ToMain();
            yield return Frames(3);
            for (int i = 0; i < 4 && view.MainItems[view.MainIndex] != "SETTINGS"; i++) yield return Press("Down");
            yield return Press("Enter");
            Check("W-034 SETTINGS opened from main", view.CurrentPhase == GameView.Phase.Settings && !view.SettingsFromPause);
            for (int i = 0; i < 4; i++) yield return Press("Down");
            Check("W-034 4 Downs reach the LANGUAGE row (index 4)", view.SettingsIndex == 4);
            yield return Frames(3);
            int before = view.Canvas.RegionHash(16, 60, MenuRenderer.SettingsW, MenuRenderer.SettingsH);
            yield return Press("Left");
            Check("W-034 LANGUAGE Left at ENGLISH stops (no change, nothing saved)", view.Progress.Language == Language.English && Loc.Current == Language.English);

            // 한국어 칸을 "KO:" + 영문으로 채운 시험용 표로 바꿔 화면이 즉시 달라지는지 본다
            Loc.LoadFrom(RowsToCsv(row => "KO:" + row.en));
            yield return Press("Right");
            yield return Frames(3);
            Check("W-034 LANGUAGE Right -> Korean at once: current language, saved value, and a fresh store all say Korean",
                Loc.Current == Language.Korean && view.Progress.Language == Language.Korean && new ProgressStore().Language == Language.Korean);
            int after = view.Canvas.RegionHash(16, 60, MenuRenderer.SettingsW, MenuRenderer.SettingsH);
            Check("W-034 the settings window is redrawn right after the change (picture differs)", before != after);
            Check("W-034 Korean: representative lines change (id and English-key lookups)",
                Loc.T("settings.title") == "KO:SETTINGS" && Loc.T("result.stars", 3) == "KO:3 / 3 STARS" && Loc.Tr("NIGHT INTRUDER") == "KO:NIGHT INTRUDER" && Loc.Tr("DOOR PASSED") == "KO:DOOR PASSED"
                && Loc.T("value.korean") == "KO:한국어" && Loc.Tr("LSHIFT") == "LSHIFT");
            yield return Press("Right");
            Check("W-034 LANGUAGE Right at the last value stops", view.Progress.Language == Language.Korean);
            yield return Press("Left");
            Check("W-034 LANGUAGE Left -> English at once, saved", Loc.Current == Language.English && new ProgressStore().Language == Language.English && Loc.T("settings.title") == "SETTINGS");
            yield return Press("Right");
            Check("W-034 Korean again, then it is kept after the settings window closes (saved value read by a new store)", new ProgressStore().Language == Language.Korean);

            // 게임 도중 일시정지 설정에서도 같은 값을 바꾼다(진행 중인 의뢰를 건드리지 않는다)
            yield return Press("Esc");
            Check("W-034 Esc leaves the settings, language stays Korean", view.CurrentPhase == GameView.Phase.Main && Loc.Current == Language.Korean);
            yield return Press("Enter");   // NEW GAME 이 아니라 SETTINGS 가 선택돼 있다 -> 설정으로 다시
            Check("W-034 settings reopened", view.CurrentPhase == GameView.Phase.Settings);
            yield return Press("Esc");

            // Clear Save: 기록·설정·언어를 지워 첫 실행 상태로
            view.Progress.SetCleared(ids[0]);
            view.Progress.BgmVolume = 2;
            view.Progress.ClearAll(ids);
            Loc.Current = view.Progress.Language;
            Check("W-034 Clear Save: record, settings and language go back to the first-run state (ENGLISH, defaults)",
                !view.Progress.HasAnyProgress(ids) && Loc.Current == Language.English && view.Progress.Language == Language.English && view.Progress.BgmVolume == ProgressStore.VolumeDefault
                && new ProgressStore().Language == Language.English && !new ProgressStore().HasAnyProgress(ids) && new ProgressStore().BgmVolume == ProgressStore.VolumeDefault);

            // 시험용 표를 원래 표로 되돌린다
            Loc.LoadFrom(originalCsv);
            Loc.Current = Language.English;
            Check("W-034 the original table is back (English)", Loc.T("settings.title") == "SETTINGS");
            yield return ToMain();
            yield return W034KoChecks();
        }

        static string CaptureDir34 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-034"));

        static void CaptureW34(string name)
        {
            Directory.CreateDirectory(CaptureDir34);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir34, name + "_1920x1080.png"), 1920, 1080);
        }

        static readonly List<string> koOver = new List<string>();

        static void Fit(string label, string text, int max)
        {
            int w = PixelCanvas.TextWidth(text);
            if (w > max) koOver.Add(label + " [" + text + "] " + w + " > " + max);
        }

        /// <summary>한국어 모드(실제 표): 글자 폭이 칸을 넘는 문구 목록, 줄바꿈, 영어 모드의 한글 글자, 화면 캡처.</summary>
        static IEnumerator W034KoChecks()
        {
            var ids = AllIds();
            Loc.Current = Language.English;
            Check("W-034 English mode: a Hangul string is drawn with Galmuri (wide), Latin text keeps the 5x7 font",
                PixelCanvas.TextWidth("한국어") > 17 && PixelCanvas.TextWidth("SETTINGS") == 47 && PixelCanvas.TextWidth("21:04") == 29);
            Loc.Current = Language.Korean;
            Check("W-034 Korean mode: Latin text uses Galmuri too (different width), pure numbers stay 5x7, key badge text stays 5x7",
                PixelCanvas.TextWidth("SETTINGS") != 47 && PixelCanvas.TextWidth("21:04") == 29 && PixelCanvas.TextWidth5x7("SHIFT") == 29);

            // ----- 폭 확인: 칸을 넘는 문구 목록 -----
            koOver.Clear();
            string[] menuIds = { "menu.continue", "menu.new_game", "menu.settings", "menu.credits", "menu.quit" };
            foreach (string id in menuIds) Fit(id, Loc.T(id), 76);
            Fit("menu.erase_q", Loc.T("menu.erase_q"), 180); Fit("menu.no", Loc.T("menu.no"), 52); Fit("menu.yes", Loc.T("menu.yes"), 52);
            foreach (string id in new[] { "settings.bgm", "settings.sfx", "settings.env_fx", "settings.glitch", "settings.language" }) Fit(id, Loc.T(id), 90);
            foreach (string id in new[] { "settings.controls", "settings.back" }) Fit(id, Loc.T(id), 170);
            foreach (string id in new[] { "value.on", "value.off", "value.low", "value.normal", "value.english", "value.korean" }) Fit(id, "< " + Loc.T(id) + " >", 80);
            foreach (string id in new[] { "controls.aim", "controls.hold", "controls.push", "controls.tab", "controls.pause", "controls.ok" }) Fit(id, Loc.T(id), 160);
            Fit("desk.normal_mode", Loc.T("desk.normal_mode"), 70); Fit("desk.locked(icon)", Loc.T("desk.locked"), 70);
            Fit("desk.connecting_cam", Loc.T("desk.connecting_cam"), 250); Fit("desk.connecting", Loc.T("desk.connecting"), 260);
            foreach (string id in new[] { "credits.l1", "credits.l2", "credits.l3", "credits.l4", "credits.l5" }) Fit(id, Loc.T(id), 495);
            Fit("game.header", Loc.T("game.header", "1-4"), 560); Fit("game.floor_view", Loc.T("game.floor_view", Loc.Tr("OLD TOWN A/B")), 390);
            Fit("game.tab_mission", Loc.T("game.tab_mission"), 54); Fit("game.tab_log", Loc.T("game.tab_log"), 30); Fit("game.rating", Loc.T("game.rating"), 42);
            Fit("game.trace", Loc.T("game.trace"), 42); Fit("game.hacker", Loc.T("game.hacker"), 100); Fit("dlg.next", Loc.T("dlg.next"), 30);
            foreach (string id in new[] { "pause.continue", "pause.restart", "pause.settings", "pause.desktop", "result.btn_desktop", "result.btn_restart" }) Fit(id, Loc.T(id), 100);
            foreach (string id in new[] { "result.complete", "result.game_over", "result.failed", "result.traced", "result.trapped" }) Fit(id, Loc.T(id), 180);
            Fit("result.stars", Loc.T("result.stars", 3), 180);
            Fit("result.traced x2", Loc.T("result.traced"), 90);   // 2배 크기(TRACED.)
            Fit("result.clear x2", Loc.T("result.clear"), 90);
            for (int i = 0; i < view.Desk.Count; i++)
            {
                var e = view.Desk.Entry(i);
                Fit("title " + e.id, Loc.Tr(e.title), 180);
                if (e.level != null && !string.IsNullOrEmpty(e.level.buildingName))
                {
                    Fit("building " + e.id + " (detail)", Loc.Tr(e.level.buildingName), 180);
                    Fit("building " + e.id + " (floor view)", Loc.T("game.floor_view", Loc.Tr(e.level.buildingName)), 390);
                }
                foreach (string m in e.missionNames)
                {
                    string mk = Loc.Tr(m);
                    Fit("mission " + e.id + " (detail)", "- " + mk, 172);
                    Fit("mission " + e.id + " (game panel)", mk + (m.StartsWith("TRACE") ? " (100)" : ""), 158);
                }
            }
            foreach (UseKind k in Enum.GetValues(typeof(UseKind))) Fit("use " + k, "> " + Loc.Tr(Fx.UseText(k)), 180);
            foreach (string n in new[] { "CH0_HOME", "CH1_???", "CH2_???" }) Fit("chapter " + n, n, 76);
            for (int i = 0; i < view.Desk.Count; i++) Fit("stage " + view.Desk.RequestName(i), view.Desk.RequestName(i), 60);
            Fit("tut.trace_max", Loc.T("tut.trace_max"), 500); Fit("tut.fail_notice", Loc.T("tut.fail_notice"), 500);

            // 마커(장면 위 표지): 섹터 폭 160 안
            foreach (string id in new[] { "marker.run", "marker.stop", "marker.on", "marker.off", "marker.open", "marker.closed", "marker.moved", "marker.no_effect", "marker.rail_hint" }) Fit(id, Loc.T(id), 150);

            // 키 안내 띠(하단): 한 줄 전체 폭
            string[][] bands =
            {
                new[] { "hint.select", "hint.change", "hint.ok", "hint.back" },
                new[] { "hint.select", "hint.connect", "hint.back" }, new[] { "hint.select", "hint.open", "hint.close" }, new[] { "hint.select", "hint.open", "hint.log_off" },
            };
            foreach (string[] band in bands)
            {
                int total = 0;
                foreach (string id in band) total += PixelCanvas.TextWidth(Loc.T(id)) + 10 + 40;   // 배지 폭은 넉넉히 40으로 본다
                if (total > 610) koOver.Add("hint band [" + string.Join(",", band) + "] " + total + " > 610");
            }

            // ----- 줄바꿈: 대화·튜토리얼 -----
            int maxBubbleRows = 0, maxTutLines = 0;
            var wrapBad = new List<string>();
            for (int i = 0; i < view.Desk.Count; i++)
            {
                var e = view.Desk.Entry(i);
                if (e.dialogue != null)
                {
                    foreach (var arr in new[] { e.dialogue.briefing, e.dialogue.ending })
                        if (arr != null)
                            foreach (var l in arr)
                            {
                                string t = Loc.Tr(l.text);
                                var rows = PixelCanvas.WrapPx(t, 26 * 6 - 1);
                                maxBubbleRows = Mathf.Max(maxBubbleRows, rows.Count);
                                foreach (string rw in rows) if (PixelCanvas.TextWidth(rw) > 26 * 6 - 1) wrapBad.Add(rw);
                                if (string.Join(" ", rows) != t) wrapBad.Add("lost text: " + t);
                            }
                }
                if (e.level != null && e.level.tutorialSteps != null)
                    foreach (var st in e.level.tutorialSteps)
                    {
                        string t = Loc.Tr(st.text);
                        var rows = PixelCanvas.WrapPx(t, 30 * 6 - 1);
                        maxTutLines = Mathf.Max(maxTutLines, rows.Count);
                        foreach (string rw in rows) if (PixelCanvas.TextWidth(rw) > 30 * 6 - 1) wrapBad.Add(rw);
                        if (string.Join(" ", rows) != t) wrapBad.Add("lost text: " + t);
                    }
            }
            var longWord = PixelCanvas.WrapPx("가나다라마바사아자차카타파하가나다라마바사아자차카타파하", 100);
            Note("W-034 Korean wrap: dialogue bubble max " + maxBubbleRows + " rows (line step 12), tutorial bubble max " + maxTutLines + " lines; a 28-syllable word splits into " + longWord.Count + " rows");
            Check("W-034 Korean wrap: no row wider than its limit, no text lost" + (wrapBad.Count > 0 ? " - " + string.Join(" | ", wrapBad) : ""), wrapBad.Count == 0);
            bool longOk = longWord.Count > 1 && PixelCanvas.TextWidth(longWord[0]) <= 100 && string.Join("", longWord) == "가나다라마바사아자차카타파하가나다라마바사아자차카타파하";
            Check("W-034 a word longer than the box splits by character", longOk);
            Check("W-034 Korean wrap keeps words whole (space-separated), line step is 12 in Korean and 9 in English", PixelCanvas.LineStep == 12 && PixelCanvas.WrapPx("가나다 라마바", 35)[0] == "가나다");
            Check("W-034 Korean dialogue bubbles fit the log panel (<= 5 rows each)", maxBubbleRows <= 5);
            Note("W-034 Korean overflow list (" + koOver.Count + "): " + (koOver.Count == 0 ? "none" : string.Join(" ; ", koOver)));
            Check("W-034 Korean: no text is wider than its box (width calculation)", koOver.Count == 0);

            // ----- 한국어 모드 화면 캡처 6장 -----
            view.Progress.ClearAll(ids);
            view.Progress.Language = Language.Korean;
            Loc.Current = Language.Korean;
            Tutorial.AutoPass = false;
            yield return ToMain();
            yield return Frames(4);
            CaptureW34("01-main");
            yield return Frames(1);
            for (int i = 0; i < 4 && view.MainItems[view.MainIndex] != "SETTINGS"; i++) yield return Press("Down");
            yield return Press("Enter");
            for (int i = 0; i < 4; i++) yield return Press("Down");
            yield return Frames(4);
            CaptureW34("02-settings");
            yield return Frames(1);
            Check("W-034 capture: settings open on the LANGUAGE row in Korean", view.CurrentPhase == GameView.Phase.Settings && view.SettingsIndex == 4);
            yield return Press("Esc");
            for (int i = 0; i < 4 && view.MainItems[view.MainIndex] != "NEW GAME"; i++) yield return Press("Down");
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop (W-034 capture)");
            yield return Press("Enter");   // 일반 모드 팝업
            yield return WaitUntil(() => view.PopupSettled, "popup settled (W-034)");
            yield return Press("Enter");   // 챕터 0 -> 스테이지 목록
            yield return Frames(30);
            CaptureW34("03-desktop-popup");
            yield return Frames(1);
            yield return Press("Enter");   // CONNECT
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "Briefing (W-034)");
            yield return Press("Enter");
            yield return Frames(40);
            CaptureW34("04-briefing");
            yield return Frames(1);
            for (int i = 0; i < 4 && view.CurrentPhase == GameView.Phase.Briefing; i++) yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing, "Playing (W-034)");
            yield return WaitFrozen(0, "0-1 step 1 (Korean)");
            yield return Frames(20);
            CaptureW34("05-game-tutorial");
            yield return Frames(1);
            Tutorial.AutoPass = true;
            yield return Frames(5);
            ForceSimComplete();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ending || view.CurrentPhase == GameView.Phase.Ended, "Ending or Ended (W-034)");
            for (int i = 0; i < 4 && view.CurrentPhase == GameView.Phase.Ending; i++) yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended && !view.TrappedShowing, "result window (W-034)", 600);
            yield return Frames(40);
            CaptureW34("06-result");
            yield return Frames(1);

            // 정리
            view.Progress.ClearAll(ids);
            Loc.Current = Language.English;
            Tutorial.AutoPass = true;
            yield return ToMain();
        }

        // ===================================================================================================
        // W-035: 챕터 2 시간 위상 어긋남 시제품(TEST_2-0, D-112·D-126). 박자 시계·Shift + 숫자·프레스·미리보기·추적도 배율.
        // 규칙은 화면 없이 GameSim을 1/60초씩 직접 진행(W035PureChecks·W035RouteChecks), 표시·실제 키 입력·일시정지는 화면(W035Checks)에서 본다.
        // ===================================================================================================

        const string Test20LevelPath = "Assets/_Project/Data/Ch2Test_0.asset";

        /// <summary>프레스 방 A(열 1, x40 -> 맵 200)와 B(열 2, x13 -> 맵 333)의 가운데. 대기선 = 가운데 - 폭 절반 - 몸 반폭(A는 179, B는 312).</summary>
        const float PressACenter = 200f, PressBCenter = 333f;

        /// <summary>앞으로 프레스가 올라가 있을 시간(초). 주기 0·1박이 올라감이라 0박이면 2박에서 지난 만큼, 1박이면 1박에서 지난 만큼 뺀 값.</summary>
        static float UpSeconds(int slot, float frac) => slot == 0 ? (2f - frac) * BeatClock.BeatSeconds : slot == 1 ? (1f - frac) * BeatClock.BeatSeconds : 0f;

        /// <summary>1~3 중 N박 앞당겼을 때 올라가 있는 시간이 가장 긴 N.</summary>
        static int BestUpPhase(GameSim s, out float seconds)
        {
            int best = 1;
            seconds = -1f;
            for (int n = 1; n <= BeatClock.MaxPhase; n++)
            {
                float t = UpSeconds(BeatClock.Slot(s.Beat + n), s.BeatFraction);
                if (t > seconds) { seconds = t; best = n; }
            }
            return best;
        }

        /// <summary>지금부터 t0~t1초 사이 내내 프레스가 올라가 있는가(오프셋 n). 0.05초 간격으로 보고 앞뒤 여유를 둔다.</summary>
        static bool FitsUp(GameSim s, int n, float t0, float t1)
        {
            float pos = s.Beat + s.BeatFraction;
            for (float t = Mathf.Max(0f, t0 - 0.03f); t <= t1 + 0.05f; t += 0.05f)
                if (BeatClock.PressDownAt(Mathf.FloorToInt(pos + t / BeatClock.BeatSeconds) + n)) return false;
            return true;
        }

        /// <summary>1~3 중 N박 앞당기면 프레스가 내려와 있는 가장 작은 N(없으면 0).</summary>
        static int DownPhase(GameSim s)
        {
            for (int n = 1; n <= BeatClock.MaxPhase; n++) if (s.PressDownAhead(n)) return n;
            return 0;
        }

        /// <summary>
        /// 시험 맵 자동 경로: 'N' = 아무것도 안 함, 'S' = 숫자 맞춤(프레스 앞에서 올라가 있는 시간이 가장 긴 N을 Shift + N으로 맞추고, 지나간 뒤 뗌).
        /// 통과 시간(초, 시작 대기 3초 포함)은 seconds에 돌려준다. 제한 300초.
        /// </summary>
        static Rig Test20Route(char mode, out float seconds, out string nLog)
        {
            var r = new Rig(Test20LevelPath);
            GameSim s = r.S;
            SectorObject[] pr = { r.Obj(Dt.ObjectKind.Press, 0, 1), r.Obj(Dt.ObjectKind.Press, 0, 2) };
            int stage = 0;
            bool holding = false;
            var ns = new List<string>();
            int steps = 0;
            for (; steps < 60 * 300 && s.Outcome == Outcome.Running; steps++)
            {
                if (mode == 'S' && stage < 2)
                {
                    float c = stage == 0 ? PressACenter : PressBCenter;
                    float L = c - BeatClock.PressHalfWidth, R = c + BeatClock.PressHalfWidth;
                    if (holding)
                    {
                        if (s.TargetX > R + 2f || !s.TargetFacingRight) { if (s.TargetX > R + 2f) stage++; r.Release(); holding = false; }
                    }
                    else if (s.TargetFacingRight && s.TargetX >= L - Board.BodyHalf - 6f && s.TargetX < R)
                    {
                        // 대기선에 닿는 때부터 몸이 폭을 빠져나갈 때까지 프레스가 올라가 있게 하는 가장 작은 N(0 = 아무것도 안 해도 됨)
                        float x = s.TargetX;
                        float t0 = Mathf.Max(0f, (L - Board.BodyHalf - x) / 48f), t1 = (R - x) / 48f;
                        int pick = -1;
                        for (int n = 0; n <= BeatClock.MaxPhase && pick < 0; n++) if (FitsUp(s, n, t0, t1)) pick = n;
                        if (pick == 0) { if (x > R - 2f) stage++; }
                        else if (pick > 0 && AimTo(r, stage + 1, 0) && r.Phase(pick)) { holding = true; ns.Add("+" + pick); }
                    }
                    else if (s.TargetX >= R && !holding) stage++;
                }
                s.ShiftHeld = r.Shift;
                s.Step(1f / 60f);
                r.Collect();
                if (r.Shift) r.ShiftSteps++;
                if (s.Tracking > r.Peak) r.Peak = s.Tracking;
            }
            seconds = steps / 60f;
            nLog = ns.Count == 0 ? "-" : string.Join(" ", ns);
            return r;
        }

        static readonly List<string> w035Table = new List<string>();

        static void W035RouteChecks()
        {
            w035Table.Clear();
            foreach (char mode in new[] { 'N', 'S' })
            {
                Rig r = Test20Route(mode, out float sec, out string ns);
                GameSim s = r.S;
                int passed = r.CountUse(UseKind.PressPassed);
                string name = mode == 'N' ? "do nothing" : "match the beat (Shift + N before each press)";
                w035Table.Add("TEST_2-0 | " + name + " | " + (s.Outcome == Outcome.Complete ? "clear" : s.Outcome.ToString()) + " | time " + sec.ToString("0.0") + " s | peak " + r.Peak.ToString("0.0")
                    + " | Shift " + r.ShiftSeconds.ToString("0.00") + " s | numbers " + ns + " | PRESS PASSED " + passed + " | retreats " + s.RetreatSeq);
                Check("W-035 TEST_2-0 route " + name + " [simulation]: Complete (final mission done) in " + sec.ToString("0.0") + " s, no game over, PRESS PASSED x" + passed + " (2 presses)",
                    s.Outcome == Outcome.Complete && s.MissionFinal && r.Peak < s.TrackingMax && passed == 2);
                if (mode == 'N') Check("W-035 do-nothing route raises tracking not at all (no Shift) and takes time instead: peak 0, time " + sec.ToString("0.0") + " s (waits at press A for its next up beat), no retreat", Near(r.Peak, 0f, 0.001f) && s.RetreatSeq == 0 && sec > 17f);
                else Check("W-035 number route keeps tracking low: peak " + r.Peak.ToString("0.0") + " <= 60 and at most 2 presses (" + ns + ")", r.Peak <= 60f);
            }
            foreach (string row in w035Table) Note("W-035 TABLE [simulation] " + row);
        }

        /// <summary>화면 없이 규칙만: 박자 시계, Shift + 숫자, 시계 없는 방 막힘, 프레스 기다림·물러남, 추적도 배율, 미리보기 값, 챕터 0·1 불변.</summary>
        static void W035PureChecks()
        {
            // ----- 맵·데이터 -----
            var t = new Rig(Test20LevelPath);
            Board b = t.S.Board;
            int clocks = 0;
            for (int f = 0; f < b.Floors; f++) for (int c = 0; c < b.Cols; c++) if (b.HasClock(f, c)) clocks++;
            int pressN = 0;
            foreach (SectorObject o in b.Objects) if (o.Def.kind == Dt.ObjectKind.Press) pressN++;
            Check("W-035 TEST_2-0 map: 1 floor x 5 cols, layout loaded with the same size, clock rooms exactly (0,1)(0,2), 2 presses, scale 0.7, one mission",
                b.Floors == 1 && b.Cols == 5 && b.Layout != null && b.Layout.Floors == 1 && b.Layout.Cols == 5 && b.HasClocks && clocks == 2 && b.HasClock(0, 1) && b.HasClock(0, 2)
                && pressN == 2 && Near(b.TrackingRateScale, 0.7f, 0.0001f) && b.Missions.Length == 1 && b.IsOperable(0, 1) && b.IsOperable(0, 2) && !b.IsOperable(0, 0) && t.S.Aim == P(1, 0));

            // ----- 박자 시계: 1박 0.6초, 4박 주기, 게임 시간(Step)으로만 흐름 -----
            var r = new Rig(Test20LevelPath);
            GameSim s = r.S;
            r.Run(0.55f);
            bool b0 = s.Beat == 0;
            r.Run(0.1f);
            bool b1 = s.Beat == 1;
            r.Run(1.9f);   // 합계 2.55초
            Check("W-035 beat clock: 1 beat = 0.6 s (beat 0 at 0.55 s, beat 1 at 0.65 s), the 4-beat cycle returns at 2.4 s (beat 4 at 2.55 s)", b0 && b1 && s.Beat == 4 && BeatClock.Slot(s.Beat) == 0
                && Near(BeatClock.BeatSeconds, 0.6f, 0.0001f) && BeatClock.Beats == 4);
            var pt = new Rig(Test20LevelPath);
            pt.Run(0.1f);
            Check("W-035 press cycle table [up, up, down, down]: +1 beat up, +2 and +3 beats down (beat 0)", !pt.S.PressDownAhead(0) && !pt.S.PressDownAhead(1) && pt.S.PressDownAhead(2) && pt.S.PressDownAhead(3)
                && !BeatClock.PressDownAt(4) && BeatClock.PressDownAt(-1) && BeatClock.PressDownAt(-2) && !BeatClock.PressDownAt(-3));

            // ----- Shift + 숫자: 오프셋 N, 다른 숫자는 그 값으로, 떼면 0, 조준한 방만 -----
            r = new Rig(Test20LevelPath);
            s = r.S;
            SectorObject pa = r.Obj(Dt.ObjectKind.Press, 0, 1), pb = r.Obj(Dt.ObjectKind.Press, 0, 2);
            r.Run(0.1f);
            bool noShift = !s.TrySetPhase(2) && s.PhaseN == 0 && s.BlockSeq == 0;   // Shift 없이 숫자: 아무 일도 없다(막힘 피드백도 없음)
            s.ShiftHeld = true; r.Shift = true;
            bool set2 = s.TrySetPhase(2);
            Check("W-035 Shift + 2 on a clock room: offset 2 for that room only (press A follows beat+2, press B stays on beat), no number without Shift (no feedback)",
                noShift && set2 && s.PhaseN == 2 && s.RoomPhase(0, 1) == 2 && s.RoomPhase(0, 2) == 0 && s.PhaseRoom == 1 && s.PressDown(pa) == BeatClock.PressDownAt(s.Beat + 2) && s.PressDown(pb) == BeatClock.PressDownAt(s.Beat));
            bool set3 = s.TrySetPhase(3);
            Check("W-035 pressing another number while Shift is held switches to that value (+3)", set3 && s.PhaseN == 3 && s.RoomPhase(0, 1) == 3 && s.PhaseSeq == 2);
            r.Release();
            Check("W-035 releasing Shift returns the offset to 0 (press follows the plain beat again)", s.PhaseN == 0 && s.RoomPhase(0, 1) == 0 && s.PressDown(pa) == BeatClock.PressDownAt(s.Beat) && s.PhaseReleaseSeq == 1);
            bool bad0 = false;
            s.ShiftHeld = true;
            bad0 |= s.TrySetPhase(0) || s.TrySetPhase(4);
            s.ShiftHeld = false;
            Check("W-035 only 1, 2 and 3 are numbers (0 and 4 are ignored)", !bad0 && s.PhaseN == 0);

            // ----- 시계 표시 없는 방: 막힘, 추적도는 Shift 단독과 같다 -----
            Dt.LevelData lvBlock = UnityEngine.Object.Instantiate(LoadLevel("Ch2Test_0.asset"));
            lvBlock.sectors = new[]
            {
                new Dt.SectorDef { floor = 0, col = 1, target = Dt.SectorTarget.Objects, clock = true },
                new Dt.SectorDef { floor = 0, col = 2, target = Dt.SectorTarget.Objects, clock = true },
                new Dt.SectorDef { floor = 0, col = 3, target = Dt.SectorTarget.Objects, rail = true },   // 시계가 없는 조준 가능한 방(레일)
            };
            r = new Rig(lvBlock);
            s = r.S;
            bool aimed = AimTo(r, 3, 0);
            s.ShiftHeld = true; r.Shift = true;
            int blocks = s.BlockSeq;
            bool okBlocked = !s.TrySetPhase(2);
            r.Run(1f);
            Check("W-035 Shift + 2 on a room without a clock: blocked (BlockSeq +1, no offset, same block feedback as the room push), tracking only the plain Shift 14.0 per second (measured " + s.Tracking.ToString("0.00") + ")",
                aimed && okBlocked && s.BlockSeq == blocks + 1 && s.PhaseN == 0 && Near(s.LastBlockCell.x, 3f, 0.01f) && Near(s.Tracking, 14f, 0.05f));

            // ----- 한 번 누름 동안 방 밀기와 숫자는 하나만 -----
            lvBlock.sectors = new[]
            {
                new Dt.SectorDef { floor = 0, col = 1, target = Dt.SectorTarget.Objects, clock = true, rail = true },
                new Dt.SectorDef { floor = 0, col = 2, target = Dt.SectorTarget.Objects, clock = true },
            };
            r = new Rig(lvBlock);
            s = r.S;
            s.ShiftHeld = true;
            bool pushed = s.TryPush(1, 0);
            int bk = s.BlockSeq;
            bool numAfterPush = s.TrySetPhase(2);
            bool pushFirst = pushed && !numAfterPush && s.PhaseN == 0 && s.BlockSeq == bk + 1;
            s.ShiftHeld = false;
            s.ShiftHeld = true;
            bool num = s.TrySetPhase(2);
            bk = s.BlockSeq;
            bool pushAfterNum = s.TryPush(1, 0);
            Check("W-035 one press, one action: a pushed room refuses the number and a number refuses the push (first one wins, the other is blocked)",
                pushFirst && num && !pushAfterNum && s.PhaseN == 2 && !s.Pushed && s.BlockSeq == bk + 1);
            UnityEngine.Object.DestroyImmediate(lvBlock);

            // ----- 챕터 0·1 레벨에서는 숫자도 시계도 아무 일 없음 -----
            r = new Rig(Old1LevelPath);
            s = r.S;
            s.ShiftHeld = true;
            bool noClock = !s.TrySetPhase(2) && s.BlockSeq == 0 && s.PhaseN == 0 && !s.Board.HasClocks;
            s.ShiftHeld = false;
            r.Run(1f);
            Check("W-035 chapter 1 level (1-1): Shift + number does nothing (no clock, no block), beat clock stays 0", noClock && s.Beat == 0);

            // ----- 추적도: Shift 중 상승 = 기본 x 배율 x (1 + 0.25 N). N = 0이면 Shift 단독과 같다 -----
            var rates = new float[4];
            for (int n = 0; n <= 3; n++)
            {
                var q = new Rig(Test20LevelPath);
                q.S.ShiftHeld = true; q.Shift = true;
                if (n > 0) q.S.TrySetPhase(n);
                q.Run(1f);
                rates[n] = q.S.Tracking;
            }
            Check("W-035 tracking while Shift is held: base 20 x 0.7 x (1 + 0.25 N) = 14.0 / 17.5 / 21.0 / 24.5 per second for N 0..3 (measured " + rates[0].ToString("0.00") + " / " + rates[1].ToString("0.00") + " / " + rates[2].ToString("0.00") + " / " + rates[3].ToString("0.00") + ")",
                Near(rates[0], 14f, 0.05f) && Near(rates[1], 17.5f, 0.05f) && Near(rates[2], 21f, 0.05f) && Near(rates[3], 24.5f, 0.05f));

            // ----- 프레스: 기다림, 숫자로 올림, 물러남(다치지 않음), 지나감 -----
            float wait = BeatClock.PressHalfWidth;
            r = new Rig(Test20LevelPath);
            s = r.S;
            bool hasWait = r.Until(() => s.TargetWaiting, 30f);
            float waitX = s.TargetX;
            bool downNow = s.PressDown(r.Obj(Dt.ObjectKind.Press, 0, 1));
            Check("W-035 press down in front of the runner (do nothing): it waits at the line in front of it (x = 200 - 12 - 9 = 179, measured " + waitX.ToString("0.0") + ") and does not walk in, facing the press, not hurt",
                hasWait && downNow && Near(waitX, PressACenter - wait - Board.BodyHalf, 0.6f) && s.TargetFacingRight && s.Outcome == Outcome.Running && s.RetreatSeq == 0);
            int upN = BestUpPhase(s, out float upSec);
            r.Shift = true; s.ShiftHeld = true;
            s.TrySetPhase(upN);
            bool resumes = r.Until(() => !s.TargetWaiting, 1f);
            r.Until(() => s.TargetX > PressACenter + wait + 2f, 3f);
            Check("W-035 Shift + " + upN + " while waiting: the press goes up (offset " + upN + ", up for " + upSec.ToString("0.00") + " s), the runner walks in at once and passes under it, no retreat, PRESS PASSED recorded (x " + s.TargetX.ToString("0.0") + ")",
                resumes && s.PhaseN == upN && s.TargetX > PressACenter + wait && s.RetreatSeq == 0 && s.Outcome == Outcome.Running && r.CountUse(UseKind.PressPassed) == 1);
            r.Release();

            r = new Rig(Test20LevelPath);
            s = r.S;
            r.Until(() => s.TargetWaiting, 30f);
            bool resumeNat = r.Until(() => !s.TargetWaiting, 5f);
            r.Until(() => s.TargetX > PressACenter + wait + 2f, 3f);
            Check("W-035 do nothing: the press goes up by itself (beat " + s.Beat + " >= 12) and the runner passes press A late but safely (no Shift, tracking 0, no retreat)",
                resumeNat && s.Beat >= 12 && s.TargetX > PressACenter + wait && s.RetreatSeq == 0 && Near(s.Tracking, 0f, 0.0001f) && r.CountUse(UseKind.PressPassed) == 1);

            r = new Rig(Test20LevelPath);
            s = r.S;
            r.Until(() => s.TargetWaiting, 30f);
            int upM = BestUpPhase(s, out _);
            r.Shift = true; s.ShiftHeld = true;
            s.TrySetPhase(upM);
            r.Until(() => s.TargetX > PressACenter - wait + 3f, 3f);   // 프레스 폭 안으로 들어감
            bool inside = s.TargetX > PressACenter - wait && s.RetreatSeq == 0;
            r.Release();                                                // Shift를 너무 일찍 뗌: 프레스가 원래 박자(내려옴)로 돌아와 머리 위로 내려온다
            r.Run(0.05f);
            float rx = s.TargetX;
            Check("W-035 Shift released too early under press A: the press snaps back to its plain beat (down), the runner steps back the way it came (faces left, not hurt, tracking only the Shift time, x " + rx.ToString("0.0") + " inside the press width)",
                inside && s.RetreatSeq == 1 && s.Outcome == Outcome.Running && !s.TargetFacingRight && s.PhaseN == 0 && !s.TargetWaiting && rx > PressACenter - wait && rx < PressACenter + wait);
            r.Run(1.5f);
            Check("W-035 after stepping back it walks away from press A (x decreasing, game still running)", s.TargetX < PressACenter - wait && s.Outcome == Outcome.Running);

            r = new Rig(Test20LevelPath);
            s = r.S;
            r.Until(() => s.TargetX >= 195f, 30f);
            int dn = DownPhase(s);
            bool retreat = false;
            if (dn > 0) { r.Shift = true; s.ShiftHeld = true; s.TrySetPhase(dn); r.Run(0.05f); retreat = s.RetreatSeq == 1; }
            Check("W-035 Shift + " + dn + " while the runner is under press A: it brings the press down on top of it -> the runner steps back at once (RetreatSeq 1, faces left, game still running, no failure)",
                retreat && !s.TargetFacingRight && s.Outcome == Outcome.Running && s.TargetX > PressACenter - wait - 3f);

            // ----- 챕터 1 결과 불변: 1-3 경로 A는 W-030과 같은 결과(추적도 80 이하 유지, 별 2개) -----
            Rig ra = Old3Route('A', out bool oka, out _);
            Check("W-035 chapter 1 unchanged: 1-3 route A still clears with 2 stars and TRACE <= 80 kept (clock, press and number code do not touch it)", oka && ra.S.Outcome == Outcome.Complete && ra.S.Stars == 2 && !ra.S.MissionFailed(2) && ra.S.Beat == 0);
        }

        [MenuItem("GameOff/W-035 Route Check")]
        public static void W035RouteMenu()
        {
            results.Clear();
            W035PureChecks();
            W035RouteChecks();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w035-routes.txt"));
            File.WriteAllLines(path, results);
        }

        static string CaptureDir35 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-035"));

        static void CaptureW35(string name)
        {
            Directory.CreateDirectory(CaptureDir35);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir35, name + "_1920x1080.png"), 1920, 1080);
        }

        static IEnumerator W035Checks()
        {
            GameOff.Game.GameRenderer rd = view.Renderer;
            view.Progress.EnvFxReduced = false;
            view.Progress.Clear(AllIds());

            // ----- 바탕화면 목록: TEST_2-0은 챕터 2 아래에 잠금 없이 -----
            Check("W-035 list (W-038: 12 requests): index 8 is the hidden TEST_2-0 entry in chapter 2 (number 2-0, not in the chapter list), chapter 2 stays locked with nothing cleared, regular chapters still locked",
                view.Desk.Count == 12 && view.Desk.RequestName(8) == "TEST_2-0" && view.Desk.Number(8) == "2-0" && view.Desk.Entry(8).chapter == 2 && view.Desk.Entry(8).testEntry
                && !view.Desk.ChapterOpen(2) && !view.Desk.InChapter(2).Contains(8) && !view.Desk.ChapterOpen(1) && !view.Desk.IsTutorial(8)
                && view.Desk.Entry(8).missionNames.Length == 1 && view.Desk.Entry(8).missionNames[0] == "REACH THE EXIT" && view.Desk.Entry(8).dialogue == null);

            // ----- 시작: 박자 표시·톱니·프레스가 그려진다 -----
            yield return StartLevelFor(8, "Ch2Test_0.asset");
            int bar0 = rd.BeatBarFrames, gear0 = rd.GearFrames, press0 = rd.PressFrames;
            yield return Frames(4);
            Check("W-035 TEST_2-0 starts at once in play (no briefing, MISSION tab); the beat bar, two gears and two presses are drawn every frame",
                view.CurrentPhase == GameView.Phase.Playing && Sim.Board.HasClocks && rd.BeatBarFrames > bar0 && rd.GearFrames >= gear0 + 2 && rd.PressFrames >= press0 + 2 && view.CurrentTab == GameView.Tab.Mission);
            yield return WaitReal(0.3);
            CaptureW35("01-test-start");

            // ----- 설계도 미리보기: Shift만 누르면 +1·+2·+3박 3칸, 「효과 없음」이 아님 -----
            int pv0 = rd.PreviewFrames, ne0 = rd.NoEffectFrames;
            shift = true;
            yield return Frames(4);
            Check("W-035 Shift alone on the clock room: the +1 +2 +3 beat preview (3 cells) is drawn, no NO EFFECT marker, no +N marker yet, offset still 0",
                Sim.ShiftHeld && rd.PreviewFrames >= pv0 + 3 && rd.NoEffectFrames == ne0 && rd.MarkerCount == 0 && Sim.PhaseN == 0);
            yield return WaitReal(0.15);
            CaptureW35("02-shift-preview");
            shift = false;
            yield return Frames(3);

            // ----- 실제 키보드: Shift + 숫자는 문자(! @ #)가 아니라 물리 키로 들어온다 -----
            useReal = true;
            var kb = InputSystem.AddDevice<Keyboard>("SmokeKeyboardW35");
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.LeftShift));
            InputSystem.Update();
            yield return Frames(3);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.LeftShift, Key.Digit2));
            InputSystem.Update();
            yield return Frames(3);
            bool d2 = Sim.PhaseN == 2 && Sim.PhaseRoom == 1 && rd.MarkerCount >= 1;
            string mk = "";
            for (int i = 0; i < rd.MarkerCount; i++) mk += rd.MarkerText(i) + " ";
            Check("W-035 [real input] Left Shift + the digit 2 key (physical key, not a typed character): offset 2, marker +2 on the room (markers: " + mk.Trim() + ")", d2 && mk.Contains("+2"));
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.LeftShift, Key.Digit3));
            InputSystem.Update();
            yield return Frames(3);
            Check("W-035 [real input] another number while Shift is held: Digit3 switches to +3", Sim.PhaseN == 3);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.LeftShift, Key.Numpad1));
            InputSystem.Update();
            yield return Frames(3);
            Check("W-035 [real input] number pad 1 works too: +1", Sim.PhaseN == 1);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.LeftShift));
            InputSystem.Update();
            yield return Frames(3);
            Check("W-035 [real input] the digit key lifted but Shift still held: the offset stays (+1)", Sim.PhaseN == 1 && Sim.ShiftHeld);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Digit2));
            InputSystem.Update();
            yield return Frames(3);
            Check("W-035 [real input] the digit key without Shift is ignored and Shift released returns the offset to 0", Sim.PhaseN == 0 && !Sim.ShiftHeld && Sim.PhaseReleaseSeq >= 1);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            InputSystem.Update();
            yield return Frames(2);
            InputSystem.RemoveDevice(kb);
            useReal = false;

            // ----- 일시정지 중에는 박자 시계가 멈춘다 -----
            yield return StartLevelFor(8, "Ch2Test_0.asset");
            yield return WaitReal(0.8);
            int beatRun = Sim.Beat;
            Check("W-035 the beat clock runs while playing (beat " + beatRun + " after about 0.8 s of play)", beatRun >= 1);
            view.SendMessage("OnApplicationFocus", false);
            yield return Frames(2);
            Check("W-035 focus lost -> Paused", view.CurrentPhase == GameView.Phase.Paused);
            int beatP = Sim.Beat;
            float fracP = Sim.BeatFraction;
            yield return WaitReal(1.3);
            Check("W-035 paused for 1.3 s: the beat clock did not move (beat " + beatP + ", fraction " + fracP.ToString("0.000") + " -> " + Sim.BeatFraction.ToString("0.000") + ")", Sim.Beat == beatP && Mathf.Approximately(Sim.BeatFraction, fracP));
            yield return Press("Esc");
            Check("W-035 Esc resumes play", view.CurrentPhase == GameView.Phase.Playing);
            yield return WaitReal(0.8);
            Check("W-035 after resuming the clock moves again (beat " + Sim.Beat + " > " + beatP + ")", Sim.Beat > beatP);

            // ----- 캡처: Shift + N 통과 직전, 물러남 -----
            yield return StartLevelFor(8, "Ch2Test_0.asset");
            Advance(() => Sim.TargetX >= 172f, 30f);
            yield return Frames(1);
            int bestN = BestUpPhase(Sim, out float upSec);
            shift = true;
            next.Digit = bestN;
            yield return Frames(3);
            Check("W-035 just before passing: Shift + " + bestN + " held in front of press A -> offset " + bestN + ", press up for " + upSec.ToString("0.00") + " s, the runner still walks in (not waiting)",
                Sim.PhaseN == bestN && Sim.TargetFacingRight && !Sim.PressDown(Sim.Board.Objects[1]) && !Sim.TargetWaiting && Sim.Outcome == Outcome.Running);
            yield return WaitReal(0.12);
            CaptureW35("03-shift-n-before-passing");
            shift = false;
            yield return Frames(3);

            yield return StartLevelFor(8, "Ch2Test_0.asset");
            Advance(() => Sim.TargetX >= 195f, 30f);
            yield return Frames(1);
            int dnN = DownPhase(Sim);
            shift = true;
            next.Digit = dnN;
            yield return Frames(3);
            Check("W-035 under the press: Shift + " + dnN + " drops the press on the runner -> it steps back (faces left, game still running, the retreat counted once)",
                dnN > 0 && Sim.RetreatSeq == 1 && !Sim.TargetFacingRight && Sim.Outcome == Outcome.Running);
            yield return WaitReal(0.1);
            CaptureW35("04-retreat");
            shift = false;
            yield return Frames(3);
            yield return StartLevelFor(8, "Ch2Test_0.asset");   // 다음 확인을 위해 의뢰를 처음 상태로
            shift = false;
        }

        // ===================================================================================================
        // W-036: 챕터 1 6스테이지(D-125) — 새 1-4 OLD TOWN F·새 1-5 OLD TOWN G, 예전 1-4(OLD TOWN E)는 1-6. §2 규칙: 밀린·맞바뀐 방의 연결 오브젝트도 작동(D-126 [제안]).
        // 화면 없이 GameSim을 1/60초씩 직접 진행하는 자동 경로·규칙 확인 + 화면 흐름(목록·잠금·시작·캡처).
        // ===================================================================================================

        const string Old5LevelPath = "Assets/_Project/Data/Ch1Old_5.asset";   // 1-4 OLD TOWN F
        const string Old6LevelPath = "Assets/_Project/Data/Ch1Old_6.asset";   // 1-5 OLD TOWN G

        /// <summary>
        /// 1-4 OLD TOWN F 경로. 'a' = ★2 두 번 나르기만(A 끝 방 → 골목 위로 건넘, B 끝 방 → 3F 빈 칸으로 올림), 'b' = ★3 해치 탐색 포함(B 복도 해치 → 1F 로비 → 단말 → 계단으로 B 끝 방 → 나르기).
        /// 두 번째 나르기는 대상이 B 끝 방 안에서 왼쪽으로 돌아선 뒤 방 왼쪽 가까이 올 때 누른다(오래 누르지 않기 위해).
        /// </summary>
        static Rig Old5Route(char mode, out bool ok, out string stop, bool light = false)
        {
            var r = new Rig(Old5LevelPath);
            GameSim s = r.S;
            float lead = Lead(light);
            ok = true;
            stop = "";
            ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 277f - lead && s.TargetFacingRight, 60f);   // 2F A 끝 방(열 1) 오른쪽 끝 가까이
            ok &= r.Push(1, 0);                                     // 나르기 1: A 끝 방을 골목 위(열 2)로
            ok &= r.Until(() => s.TargetX >= 490f, 10f);            // B 복도로 나갈 때까지 누름
            RelLate(r, light);                                       // 나간 뒤 뗌(방은 제자리로)
            ok &= !s.IsDisplaced(1, 1);
            if (!ok) stop = "carry1";
            if (mode == 'b')
            {
                ok &= AimTo(r, 3, 1);                               // B 복도(해치)
                ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 540f - lead && s.TargetFacingRight, 20f);   // 해치(맵 x560) 앞
                r.Shift = true; s.ShiftHeld = true;                 // 해치 열기(설계도 상태)
                ok &= r.Until(() => s.TargetFloor == 2, 10f);       // 1F B 로비로 떨어짐
                RelLate(r, light);
                ok &= r.Until(() => s.MissionDone(1), 20f);         // B 창고 단말
                ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX >= 640f, 20f);   // 계단으로 2F B 끝 방
                if (stop == "" && !ok) stop = "hatch/cellar";
            }
            ok &= AimTo(r, 4, 1);                                   // B 끝 방
            ok &= r.Until(() => s.TargetFloor == 1 && !s.TargetFacingRight && s.TargetX >= 649f && s.TargetX <= 690f + lead, 90f);   // 방 안에서 돌아선 뒤 왼쪽 가까이
            ok &= r.Push(0, -1);                                    // 나르기 2: B 끝 방을 3F 빈 칸(열 4)으로
            ok &= s.CellOf(1, 4) == P(4, 0) && s.TargetFloor == 0;
            if (stop == "" && !ok) stop = "carry2";
            ok &= r.Until(() => s.TargetX < 640f, 15f);             // 3F 옥탑으로 나감
            RelLate(r, light);
            ok &= r.Until(() => s.Outcome != Outcome.Running, 15f);
            if (stop == "" && !ok) stop = "roof";
            if (!light) routeUses["1-4(F) " + mode] = r.Uses;
            return r;
        }

        /// <summary>
        /// 1-5 OLD TOWN G 경로. 'A' = 카메라 끄고 지나감(감시 복도를 조준해 Shift, 구역을 벗어날 때까지), 'B' = 감시 복도를 계단 방과 맞바꿈(Shift + ←, 맞바뀐 계단 방의 계단으로 4F),
        /// 'C' = 해치 탐색(시작 직후 2F 복도 해치 → 1F 창고 단말 → 계단으로 2F 상점) + B. 모두 레이저 끄기·중계기 뒤 4F에서 옥상 출구 방을 끌어옴.
        /// </summary>
        static Rig Old6Route(char mode, out bool ok, out string stop, bool light = false)
        {
            var r = new Rig(Old6LevelPath);
            GameSim s = r.S;
            float lead = Lead(light);
            ok = true;
            stop = "";
            if (mode == 'C')
            {
                ok &= AimTo(r, 1, 2);                               // 2F 복도(해치)
                ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX >= 225f - lead && s.TargetFacingRight, 30f);   // 해치(맵 x240) 앞
                r.Shift = true; s.ShiftHeld = true;
                ok &= r.Until(() => s.TargetFloor == 3, 10f);       // 1F 창고로 떨어짐
                RelLate(r, light);
                ok &= r.Until(() => s.MissionDone(1), 20f);         // 창고 단말
                ok &= r.Until(() => s.TargetFloor == 2 && s.TargetX < 160f, 30f);   // 계단으로 2F 상점
                if (!ok) stop = "hatch/store";
            }
            ok &= AimTo(r, 2, 2);                                   // 레이저 복도
            ok &= r.Until(() => s.TargetFloor == 2 && (s.TargetWaiting || (light && s.TargetX >= 311f - lead)), 90f);
            r.Shift = true; s.ShiftHeld = true;
            ok &= r.Until(() => s.TargetX > 330f, 10f);
            RelLate(r, light);
            if (stop == "" && !ok) stop = "laser";
            ok &= r.Until(() => s.RelayUsed, 10f);
            ok &= AimTo(r, 2, 1);                                   // 감시 복도(3F 열 2)
            ok &= r.Until(() => s.TargetFloor == 1 && s.TargetX <= 545f + lead, 40f);   // 계단으로 3F 열 3에 도착한 직후, 구역 경계(480) 48px 앞(528)에서 돌아서기 전
            ok &= s.AlertSeq == 0;
            if (mode == 'A')
            {
                r.Shift = true; s.ShiftHeld = true;                 // 카메라 끄기(설계도 상태)
                ok &= r.Until(() => s.TargetX < 320f, 20f);         // 구역(열 2)을 다 지나 계단 방(열 1)으로 들어올 때까지
                RelLate(r, light);
                ok &= r.Until(() => s.TargetFloor == 0, 10f);       // 계단 3F -> 4F
                if (stop == "" && !ok) stop = "camera off";
            }
            else
            {
                ok &= r.Push(-1, 0);                                // 맞바꿈: 감시 복도(열 2) <-> 계단 방(열 1)
                ok &= s.CellOf(1, 2) == P(1, 1) && s.CellOf(1, 1) == P(2, 1);
                ok &= r.Until(() => s.TargetFloor == 0, 20f);       // 계단 방이 열 2로 와 있다: 맞바뀐 방의 계단으로 4F
                RelLate(r, light);
                ok &= !s.IsDisplaced(1, 2) && !s.IsDisplaced(1, 1);
                if (stop == "" && !ok) stop = "swap";
            }
            ok &= AimTo(r, 3, 0);                                   // 옥상 출구 방
            ok &= r.Until(() => s.TargetFloor == 0 && s.TargetFacingRight && s.TargetX >= 305f - lead, 40f);   // 옥상 복도 오른쪽 끝 벽(x311) 앞
            ok &= r.Push(-1, 0);                                    // 끌어오기: 출구 방을 빈 칸 (4F, 열 2)로
            ok &= s.CellOf(0, 3) == P(2, 0);
            ok &= r.Until(() => s.Outcome != Outcome.Running, 15f);
            if (stop == "" && !ok) stop = "pull";
            if (!light) routeUses["1-5(G) " + mode] = r.Uses;
            return r;
        }

        static readonly List<string> w036Table = new List<string>();

        static void W036Row(string request, string route, bool light, Rig r, bool ok, string stop)
        {
            GameSim s = r.S;
            string clear = s.Outcome == Outcome.Complete ? "clear" : s.Outcome == Outcome.GameOver ? "GAME OVER" : s.Outcome.ToString();
            w036Table.Add(request + " | " + route + " | " + (light ? "light" : "optimal") + " | " + clear + " | peak " + r.Peak.ToString("0.0") + " | stars " + s.Stars
                + " | detect " + s.DetectCount + " | Shift " + r.ShiftSeconds.ToString("0.00") + " s | missions final " + (s.MissionDone(0) ? "done" : "-")
                + ", terminal " + (s.MissionDone(1) ? "done" : "-") + ", 3rd " + (s.MissionFailed(2) ? "failed" : "kept") + (ok ? "" : " | stopped at " + stop));
        }

        /// <summary>새 의뢰 1-4·1-5와 1-6(OLD TOWN E)의 자동 경로를 「최적」·「라이트」 두 번 잰다. 최적 경로는 클리어를 확인하고, 라이트 결과는 표로만 남긴다(측정).</summary>
        static void W036RouteChecks()
        {
            w036Table.Clear();
            var peakA = new Dictionary<bool, float>();
            foreach (bool light in new[] { false, true })
            {
                string m = light ? "light" : "optimal";
                foreach (char mode in new[] { 'a', 'b' })
                {
                    Rig r = Old5Route(mode, out bool ok, out string stop, light);
                    GameSim s = r.S;
                    string name = mode == 'a' ? "star-2 (double carry only)" : "star-3 (double carry + hatch + terminal)";
                    W036Row("1-4 OLD TOWN F", name, light, r, ok, stop);
                    if (!light)
                        Check("W-036 1-4 route " + name + " [" + m + "]: Complete on 3F (carry A end room over the alley, carry B end room up), terminal " + (mode == 'b' ? "done" : "skipped") + ", peak " + r.Peak.ToString("0.0") + " < 100" + (ok ? "" : " - stopped at " + stop),
                            ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.MissionDone(1) == (mode == 'b') && r.Peak < s.TrackingMax && r.CountUse(UseKind.Carried) == 2);
                }
                foreach (char mode in new[] { 'A', 'B', 'C' })
                {
                    Rig r = Old6Route(mode, out bool ok, out string stop, light);
                    GameSim s = r.S;
                    string name = mode == 'A' ? "A camera off, walk past" : mode == 'B' ? "B swap the camera room away (Shift + left)" : "C hatch terminal + B";
                    W036Row("1-5 OLD TOWN G", name, light, r, ok, stop);
                    if (mode == 'A') peakA[light] = r.Peak;
                    if (!light)
                        Check("W-036 1-5 route " + name + " [" + m + "]: Complete on 4F, detections 0, UNSEEN kept, terminal " + (mode == 'C' ? "done" : "skipped") + ", peak " + r.Peak.ToString("0.0") + " < 100" + (ok ? "" : " - stopped at " + stop),
                            ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.DetectCount == 0 && !s.MissionFailed(2) && s.MissionDone(1) == (mode == 'C') && r.Peak < s.TrackingMax);
                    if (mode == 'B')
                        Check("W-036 1-5 [" + m + "] B (swap) peak " + r.Peak.ToString("0.0") + " is lower than A (camera off) peak " + peakA[light].ToString("0.0") + " (design intent: swapping needs a shorter Shift hold)", r.Peak < peakA[light]);
                }
                foreach (char mode in new[] { 'D', 'E' })
                {
                    Rig r = Old4Route(mode, out bool ok, out string stop, light);
                    W036Row("1-6 OLD TOWN E (was 1-4)", mode == 'D' ? "D basic" : "E terminal cellar then D", light, r, ok, stop);
                }
            }
            foreach (string row in w036Table) Note("W-036 TABLE [simulation] " + row);
        }

        /// <summary>규칙 확인용 레벨 R: 2층 x 2열, 빈 칸 없음. 레일 방 X(0,0)와 Y(0,1), 1F 방 W(1,1)에 계단(x60, 연결 = (0,0) x60 왼쪽). 시작 = W 안 x20 오른쪽.</summary>
        static Dt.LevelData MakeArrivalSwapLevel()
        {
            var l = ScriptableObject.CreateInstance<Dt.LevelData>();
            l.floors = 2; l.cols = 2; l.sectorWidth = 160; l.sectorHeight = 96; l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.sectors = new[] { new Dt.SectorDef { floor = 0, col = 0, target = Dt.SectorTarget.Objects, rail = true } };
            Dt.ObjectDef st = PushObj(Dt.ObjectKind.Stairs, 1, 1, 60, true, true);
            st.hasLink = true; st.linkFloor = 0; st.linkCol = 0; st.linkX = 60; st.linkFacingRight = false;
            l.objects = new[] { st };
            l.startFloor = 1; l.startCol = 1; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 0; l.startAimCol = 0;
            l.missions = new[] { new Dt.MissionDef { kind = Dt.MissionKind.PassObject } };
            return l;
        }

        /// <summary>W-036: 새 의뢰 데이터·크기, §2 규칙(밀린·맞바뀐 방의 연결 작동, 도착 칸이 비면 미작동).</summary>
        static void W036PureChecks()
        {
            // ----- 두 새 의뢰의 크기·배율·방 배치 -----
            foreach (var z in new[]
            {
                new { name = "1-4 OLD TOWN F", path = Old5LevelPath, floors = 3, cols = 5, key = "scenes128/ch1_old5/layout", rails = new[] { P(1, 1), P(4, 1) } },
                new { name = "1-5 OLD TOWN G", path = Old6LevelPath, floors = 4, cols = 4, key = "scenes128/ch1_old6/layout", rails = new[] { P(2, 1), P(3, 0) } },
            })
            {
                var r = new Rig(z.path);
                Board b = r.S.Board;
                bool railsOk = true;
                foreach (Vector2Int rl in z.rails) railsOk &= b.HasRail(rl.y, rl.x);
                Check("W-036 " + z.name + " map: " + z.floors + " floors x " + z.cols + " cols, layout " + z.key + " loaded (room textures are checked on screen), rate scale 0.7, rails on the planned rooms, no tutorial / guide, 3 missions",
                    b.Floors == z.floors && b.Cols == z.cols && b.Layout != null && b.LayoutKey == z.key && Near(b.TrackingRateScale, 0.7f, 0.0001f) && railsOk && b.HasRails
                    && !b.Tutorial && b.Missions.Length == 3 && AssetDatabase.LoadAssetAtPath<Dt.LevelData>(z.path).tutorialSteps.Length == 0 && !AssetDatabase.LoadAssetAtPath<Dt.LevelData>(z.path).showGuide);
                r.Shift = true;
                r.Run(1f);
                Check("W-036 " + z.name + " Shift held for 1 s raises tracking by 14 (chapter 1 scale 0.7), measured " + r.S.Tracking.ToString("0.00"), Near(r.S.Tracking, 14f, 0.05f));
            }
            {
                var b5 = new Rig(Old5LevelPath).S.Board;
                var b6 = new Rig(Old6LevelPath).S.Board;
                Check("W-036 1-4 data: missions final / terminal / TRACE limit 60 (provisional), start 2F col 0 aim A end room, exit on 3F col 3, hatch B corridor -> 1F lobby, terminal + stairs in B storehouse",
                    b5.Missions[2].kind == Dt.MissionKind.TraceLimit && Near(b5.Missions[2].limit, 60f) && b5.StartFloor == 1 && b5.StartAim == P(1, 1)
                    && FindObj(b5, Dt.ObjectKind.Exit, 0, 3) != null && FindObj(b5, Dt.ObjectKind.Hatch, 1, 3) != null && FindObj(b5, Dt.ObjectKind.Terminal, 2, 4) != null && FindObj(b5, Dt.ObjectKind.Stairs, 2, 4) != null
                    && b5.IsEmpty(0, 4) && b5.IsEmpty(1, 2) && b5.IsEmpty(2, 2) && !b5.IsEmpty(0, 3));
                Check("W-036 1-5 data: missions final / terminal / UNSEEN, camera only in the 3F col 2 rail room, laser corridor 2F col 2 (whole sector) with the relay, stairs 3F col 1 (to 4F col 1), exit in the 4F col 3 rail room",
                    b6.Missions[2].kind == Dt.MissionKind.Unseen && FindObj(b6, Dt.ObjectKind.Camera, 1, 2) != null && b6.IsWhole(2, 2) && FindObj(b6, Dt.ObjectKind.Relay, 2, 2) != null
                    && FindObj(b6, Dt.ObjectKind.Stairs, 1, 1) != null && FindObj(b6, Dt.ObjectKind.Stairs, 1, 1).Def.linkFloor == 0 && FindObj(b6, Dt.ObjectKind.Stairs, 1, 1).Def.linkCol == 1
                    && FindObj(b6, Dt.ObjectKind.Exit, 0, 3) != null && FindObj(b6, Dt.ObjectKind.Hatch, 2, 1) != null && FindObj(b6, Dt.ObjectKind.Terminal, 3, 1) != null);
            }

            // ----- §2 규칙: 밀린·맞바뀐 방의 연결 오브젝트도 작동 -----
            {
                // (1) 밀린 방 안의 계단 S1(방 x60, 열 1로 올라간 칸의 맵 x220)이 작동한다: 도착은 원래 연결된 칸 (0,0) x60, 왼쪽
                var r = new Rig(MakePushLevel(1, 1, 20f, true));
                GameSim s = r.S;
                r.Push(0, -1);
                bool jumped = r.Until(() => s.TargetFloor == 0 && s.TargetX < 150f, 15f);
                Check("W-036 §2 rule: stairs inside a displaced room work - the runner carried up in the pushed room reaches S1 (map x220) and arrives at the original linked cell (floor 0 col 0, x60) facing left, room still pushed",
                    jumped && Near(s.TargetX, 60f, 2f) && !s.TargetFacingRight && s.IsDisplaced(1, 1) && s.Pushed);

                // (2) 그 사이 닫힌 문(현실)은 그대로 막는다: 계단 연결을 뗀 같은 레벨에서 밀린 방은 현실 상태라 문 앞(맵 x301)에서 기다린다
                Dt.LevelData noLink = MakePushLevel(1, 1, 20f, true);
                noLink.objects[0].hasLink = false;
                r = new Rig(noLink); s = r.S;
                r.Push(0, -1);
                bool wait = r.Until(() => s.TargetWaiting, 15f);
                Check("W-036 §2 rule: the pushed room is still reality (S1 without a link: the runner walks past it and the closed door holds him at x301)", wait && Near(s.TargetX, 301f) && s.TargetFloor == 0);

                // (3) 도착 방이 밀려 나가 도착 칸이 빈 칸이면 작동하지 않는다(떨어짐 금지)
                r = new Rig(MakePushLevel(1, 0, 20f, true)); s = r.S;
                r.Push(0, -1);
                bool pass = r.Until(() => s.TargetX >= 100f, 15f);
                Check("W-036 §2 rule: arrival cell empty (the arrival room was pushed away) - S2 does nothing, the runner passes it on the same floor (no fall)", pass && s.TargetFloor == 1 && s.TargetX >= 100f && s.TargetX < 155f && s.RoomIndexAt(1, 1) < 0);

                // (4) 도착 방이 밀려 있어도 도착 칸에 다른 방이 있으면 원래 연결된 칸으로 간다
                r = new Rig(MakeArrivalSwapLevel()); s = r.S;
                bool pushed = r.Push(1, 0);
                bool arrived = r.Until(() => s.TargetFloor == 0, 20f);
                Check("W-036 §2 rule: arrival room displaced but its cell is taken by the swapped room - the link works and arrives at the original cell (floor 0 col 0, x60)",
                    pushed && arrived && Near(s.TargetX, 60f, 2f) && !s.TargetFacingRight && s.IsDisplaced(0, 0) && s.RoomIndexAt(0, 0) == 1);
                // 같은 레벨, 아무것도 밀지 않음: 같은 도착(대조)
                var c = new Rig(MakeArrivalSwapLevel());
                bool arrivedHome = c.Until(() => c.S.TargetFloor == 0, 20f);
                Check("W-036 §2 rule control: nothing pushed, the same stairs arrive at floor 0 col 0 x60", arrivedHome && Near(c.S.TargetX, 60f, 2f));
            }
        }

        /// <summary>W-036 경로·표만 빠르게 돌리는 메뉴(Play 모드 없이). 결과는 Unity/Logs/w036-routes.txt.</summary>
        [MenuItem("GameOff/W-036 Route Check")]
        public static void W036RouteMenu()
        {
            results.Clear();
            W031PureChecks();
            W031RouteChecks();
            W036PureChecks();
            W036RouteChecks();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w036-routes.txt"));
            File.WriteAllLines(path, results);
        }

        static string CaptureDir36 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-036"));

        static void CaptureW36(string name)
        {
            Directory.CreateDirectory(CaptureDir36);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir36, name + "_1920x1080.png"), 1920, 1080);
        }

        /// <summary>W-036 화면 흐름: 목록 순서·번호·잠금·저장 키, 새 의뢰 데이터, 1-4·1-5 시작·핵심 장면·캡처.</summary>
        static IEnumerator W036Checks()
        {
            view.Progress.EnvFxReduced = false;
            view.Progress.Clear(AllIds());

            // ----- 목록: 1-1 .. 1-6 순서·번호, 시험 의뢰는 그대로 -----
            RequestBoard d = view.Desk;
            string[] ids = { "old-1-1", "old-1-2", "old-1-3", "old-1-f", "old-1-g", "old-1-4" };
            bool idsOk = d.Count == 12;
            for (int i = 0; i < 6; i++) idsOk &= d.Entry(2 + i).id == ids[i] && d.Number(2 + i) == "1-" + (i + 1) && d.RequestName(2 + i) == "REQ_1-" + (i + 1) && d.ChapterOf(2 + i) == 1 && d.IndexInChapter(2 + i) == i;
            Check("W-036 list (W-038: 12 requests); chapter 1 is REQ_1-1 .. REQ_1-6 in order (ids old-1-1, old-1-2, old-1-3, old-1-f, old-1-g, old-1-4 - the old OLD TOWN E keeps its id), numbers come from the list order, the chapter 1 group has 6 entries",
                idsOk && d.InChapter(1).Count == 6);
            Check("W-036 list (W-038): TEST_2-0 stays at index 8 (number 2-0, chapter 2) but is not counted in any chapter list, so chapter 2 numbers are 2-1 .. 2-3",
                d.Entry(8).testEntry && d.RequestName(8) == "TEST_2-0" && d.Number(8) == "2-0" && d.ChapterOf(8) == 2 && d.InChapter(2).Count == 3 && d.IndexInChapter(8) == -1);

            // ----- 해금 순서: 1-3 -> 1-4(새) -> 1-5(새) -> 1-6(OLD TOWN E) -----
            view.Progress.SetCleared("home-0-1");
            view.Progress.SetCleared("home-0-2");
            bool g0 = d.IsUnlocked(2) && !d.IsUnlocked(3) && !d.IsUnlocked(7);
            view.Progress.SetCleared("old-1-1");
            view.Progress.SetCleared("old-1-2");
            bool g1 = d.IsUnlocked(4) && !d.IsUnlocked(5) && !d.IsUnlocked(6) && !d.IsUnlocked(7);
            view.Progress.SetCleared("old-1-3");
            bool g2 = d.IsPlayable(5) && d.State(5) == RequestState.New && !d.IsUnlocked(6) && !d.IsUnlocked(7);
            view.Progress.SetCleared("old-1-f");
            bool g3 = d.IsPlayable(6) && d.State(6) == RequestState.New && !d.IsUnlocked(7) && d.State(7) == RequestState.Locked;
            view.Progress.SetCleared("old-1-g");
            bool g4 = d.IsPlayable(7) && d.State(7) == RequestState.New && !d.IsCleared(7);
            Check("W-036 unlock order: 1-1 open first; 1-3 clear opens 1-4 (OLD TOWN F); 1-4 clear opens 1-5 (OLD TOWN G); 1-5 clear opens 1-6 (OLD TOWN E); each stays locked until the one before it clears",
                g0 && g1 && g2 && g3 && g4);

            // ----- 새 의뢰 데이터 -----
            Dt.RequestEntry e4 = d.Entry(5), e5 = d.Entry(6), e6 = d.Entry(7);
            Check("W-036 data: 1-4 id old-1-f, title DOUBLE HAUL, client ???, missions REACH THE ROOF / B CELLAR TERMINAL / TRACE <= 60, 3-line briefing and ending (W-038 dialogue draft)",
                e4.id == "old-1-f" && e4.title == "DOUBLE HAUL" && e4.client == "???" && e4.chapter == 1 && e4.missionNames.Length == 3 && e4.missionNames[0] == "REACH THE ROOF" && e4.missionNames[1] == "B CELLAR TERMINAL" && e4.missionNames[2] == "TRACE <= 60"
                && e4.dialogue != null && e4.dialogue.briefing.Length == 3 && e4.dialogue.ending.Length == 3 && !e4.testEntry && e4.level != null && e4.level.buildingName == "OLD TOWN F");
            Check("W-036 data: 1-5 id old-1-g, title CAMERA SHUFFLE, client ???, missions REACH THE ROOF / STORE TERMINAL / UNSEEN, 3-line briefing and ending (W-038 dialogue draft)",
                e5.id == "old-1-g" && e5.title == "CAMERA SHUFFLE" && e5.client == "???" && e5.chapter == 1 && e5.missionNames.Length == 3 && e5.missionNames[0] == "REACH THE ROOF" && e5.missionNames[1] == "STORE TERMINAL" && e5.missionNames[2] == "UNSEEN"
                && e5.dialogue != null && e5.dialogue.briefing.Length == 3 && e5.dialogue.ending.Length == 3 && !e5.testEntry && e5.level != null && e5.level.buildingName == "OLD TOWN G");
            Check("W-036 data: 1-6 is the old OLD TOWN E request unchanged (id old-1-4, title ROOFTOP RUN, missions REACH THE ROOF / CELLAR TERMINAL / UNSEEN, 3-line briefing and ending, level Ch1Old_4)",
                e6.id == "old-1-4" && e6.title == "ROOFTOP RUN" && e6.missionNames.Length == 3 && e6.missionNames[1] == "CELLAR TERMINAL" && e6.dialogue != null && e6.dialogue.briefing.Length == 3 && e6.dialogue.ending.Length == 3
                && e6.level != null && e6.level.name == "Ch1Old_4" && e6.level.buildingName == "OLD TOWN E");
            Check("W-036 data: environment 1-4 Night/Cloudy, 1-5 Night/Fog (provisional)",
                e4.level.timeOfDay == Dt.TimeOfDay.Night && e4.level.weather == Dt.Weather.Cloudy && e5.level.timeOfDay == Dt.TimeOfDay.Night && e5.level.weather == Dt.Weather.Fog);
            Check("W-036 Korean names: the table has 1-4/1-5 title, mission and building rows (두 번 나르기, 카메라 비켜 세우기, B동 지하 단말, 가게 단말, 구시가지 F, 구시가지 G)",
                Loc.HasEn("DOUBLE HAUL") && Loc.HasEn("CAMERA SHUFFLE") && Loc.HasEn("B CELLAR TERMINAL") && Loc.HasEn("STORE TERMINAL") && Loc.HasEn("OLD TOWN F") && Loc.HasEn("OLD TOWN G") && Loc.HasEn("TRACE <= 60"));

            // ----- 바탕화면: 챕터 1 목록 6개(캡처) -----
            yield return ToMain();
            yield return Frames(4);
            if (view.MainIndex != 0) yield return Press("Up");
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "W-036 Desktop");
            yield return Press("Enter");   // 일반 모드 팝업(챕터 포커스)
            if (view.FolderIndex != 1) yield return Press("Down");
            yield return Press("Right");   // 스테이지 포커스
            Check("W-036 CH1 list opened with its 6 requests (REQ_1-1 .. REQ_1-6)", view.InFolder && view.FolderIndex == 1 && view.Desk.InChapter(1).Count == 6);
            for (int i = 0; i < 6 && view.DeskIndex != 6; i++) yield return Press("Down");
            yield return Frames(3);
            CaptureW36("01-ch1-list-six");
            yield return Frames(1);
            yield return ToMain();
            yield return Frames(2);

            // ===== 1-4 OLD TOWN F =====
            yield return StartLevelFor(5, "Ch1Old_5.asset");
            Check("W-036 1-4 on screen: Playing at once (empty briefing), header OLD TOWN F, 3 floors x 5 cols, layout ch1_old5 loaded with every room texture in the art set, no guide box, 3 missions",
                view.CurrentPhase == GameView.Phase.Playing && Sim.Board.BuildingName == "OLD TOWN F" && Sim.Board.Floors == 3 && Sim.Board.Cols == 5 && Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch1_old5/layout"
                && LayoutArtOk(Sim.Board.Layout) && view.Tut == null && Sim.MissionCount == 3 && Sim.Aim == P(1, 1));
            yield return Frames(2);
            CaptureW36("02-1-4-start");
            yield return Frames(1);
            Advance(() => Sim.TargetFloor == 1 && Sim.TargetX >= 277f && Sim.TargetFacingRight, 60f);
            shift = true;
            next.Right = true;
            yield return null;
            yield return Frames(30);
            Check("W-036 1-4 first carry: Shift + right slides the A end room (2F col 1) over the alley cell with the runner inside (the shop under it stays)", Sim.Pushed && Sim.CellOf(1, 1) == P(2, 1) && Sim.TargetFloor == 1);
            Advance(() => Sim.TargetX >= 490f, 10f);
            shift = false;
            Sim.ShiftHeld = false;
            Check("W-036 1-4 first carry done: Shift released after the runner stepped into the B corridor, the A end room is home", !Sim.ShiftHeld && Sim.TargetX >= 490f);
            Sim.ShiftHeld = false; AimToSim(Sim, 4, 1);
            Advance(() => Sim.TargetFloor == 1 && !Sim.TargetFacingRight && Sim.TargetX >= 649f && Sim.TargetX <= 690f, 90f);
            yield return Frames(3);
            Check("W-036 1-4 before the second carry: the runner is inside the B end room (2F col 4) walking left, the aim is on it again (one press, one room)", Sim.Aim == P(4, 1) && !Sim.Pushed && Sim.TargetFloor == 1 && Sim.TargetX >= 640f);
            CaptureW36("03-1-4-before-second-carry");
            yield return Frames(1);
            shift = true;
            next.Up = true;
            yield return null;
            yield return Frames(30);
            Check("W-036 1-4 second carry: Shift + up lifts the B end room into the empty 3F col 4 with the runner (floor 0)", Sim.Pushed && Sim.CellOf(1, 4) == P(4, 0) && Sim.TargetFloor == 0);
            CaptureW36("04-1-4-second-carry-up");
            yield return Frames(1);
            shift = false;
            yield return Frames(3);

            // ===== 1-5 OLD TOWN G =====
            yield return StartLevelFor(6, "Ch1Old_6.asset");
            Check("W-036 1-5 on screen: Playing at once (empty briefing), header OLD TOWN G, 4 floors x 4 cols, layout ch1_old6 loaded with every room texture in the art set, no guide box, camera in the 3F col 2 rail room, 3 missions (UNSEEN)",
                view.CurrentPhase == GameView.Phase.Playing && Sim.Board.BuildingName == "OLD TOWN G" && Sim.Board.Floors == 4 && Sim.Board.Cols == 4 && Sim.Board.Layout != null && Sim.Board.LayoutKey == "scenes128/ch1_old6/layout"
                && LayoutArtOk(Sim.Board.Layout) && view.Tut == null && Sim.MissionCount == 3 && FindObj(Sim.Board, Dt.ObjectKind.Camera, 1, 2) != null && Sim.Board.HasRail(1, 2));
            yield return Frames(2);
            CaptureW36("05-1-5-start");
            yield return Frames(1);
            Sim.ShiftHeld = false; AimToSim(Sim, 2, 2);
            Advance(() => Sim.TargetFloor == 2 && Sim.TargetWaiting, 90f);
            shift = true; Advance(() => Sim.TargetX > 330f, 10f); shift = false;
            Advance(() => Sim.RelayUsed, 10f);
            Sim.ShiftHeld = false; AimToSim(Sim, 2, 1);
            Advance(() => Sim.TargetFloor == 1 && Sim.TargetX <= 545f, 40f);
            yield return Frames(3);
            Check("W-036 1-5 before the swap: the runner stands in the 3F col 3 stairwell facing left, the camera zone (3F col 2) is lit, the aim is the camera room, nothing detected", Sim.TargetFloor == 1 && Sim.TargetX > 480f && Sim.AimCell == P(2, 1) && Sim.DetectCount == 0 && !Sim.Pushed);
            CaptureW36("06-1-5-before-swap");
            yield return Frames(1);
            shift = true;
            next.Left = true;
            yield return null;
            yield return Frames(20);
            Check("W-036 1-5 swap: Shift + left sends the camera room to 3F col 1 and brings the stairs room to col 2 (camera moved aside, the zone moved with its room), nothing detected",
                Sim.Pushed && Sim.CellOf(1, 2) == P(1, 1) && Sim.CellOf(1, 1) == P(2, 1) && Sim.DetectCount == 0 && Sim.TargetFloor == 1);
            CaptureW36("07-1-5-camera-moved-aside");
            yield return Frames(1);
            Advance(() => Sim.TargetFloor == 0, 20f);
            Check("W-036 1-5 swapped stairs work: the runner climbs 3F -> 4F with the stairs room still pushed (arrival 4F col 1), still unseen", Sim.TargetFloor == 0 && Sim.Pushed && Sim.DetectCount == 0 && !Sim.MissionFailed(2));
            shift = false;
            yield return Frames(3);
            CaptureW36("08-1-5-roof-after-swap");
            yield return Frames(1);
        }

        // ===================================================================================================
        // W-038: 챕터 2 정식 의뢰 2-1~2-3(D-112·D-126) — 주기 레이저, 대사 초안(07-01 v0.5.0), 시험 의뢰를 목록에서 뺌.
        // 화면 없이 GameSim을 1/60초씩 진행하는 자동 경로·규칙 확인 + 화면 흐름(목록·잠금·튜토리얼·대화·캡처).
        // ===================================================================================================

        const string Fac1Path = "Assets/_Project/Data/Ch2Fac_1.asset";   // 2-1 FACTORY A
        const string Fac2Path = "Assets/_Project/Data/Ch2Fac_2.asset";   // 2-2 FACTORY B
        const string Fac3Path = "Assets/_Project/Data/Ch2Fac_3.asset";   // 2-3 FACTORY C

        /// <summary>지금부터 t0~t1초 사이 내내 주기 레이저가 꺼져 있는가(오프셋 n).</summary>
        static bool FitsOff(GameSim s, int n, float t0, float t1)
        {
            float pos = s.Beat + s.BeatFraction;
            for (float t = Mathf.Max(0f, t0 - 0.03f); t <= t1 + 0.05f; t += 0.05f)
                if (BeatClock.LaserOnAt(Mathf.FloorToInt(pos + t / BeatClock.BeatSeconds) + n)) return false;
            return true;
        }

        /// <summary>
        /// 시계 방(프레스 또는 주기 레이저) 한 곳을 지나는 자동 입력: 앞에 다가오면 지나는 동안 열려 있게 하는 가장 작은 N(0 = 그냥 지남)을 골라 Shift + N을 누르고,
        /// 지난 뒤 뗀다(라이트 유저는 0.4초 일찍 판단하고 0.4초 늦게 뗌). dir = 1 오른쪽 / -1 왼쪽. 지나갔으면 true, 시간 안에 못 지났거나 의뢰가 끝났으면 false.
        /// </summary>
        static bool Cross(Rig r, bool light, int rf, int rc, bool laser, int dir, float maxSec = 90f)
        {
            GameSim s = r.S;
            float W = s.Board.SectorW;
            float L, R;
            if (laser) { L = rc * W; R = L + W; }
            else { float c = r.Obj(Dt.ObjectKind.Press, rf, rc).AbsX; L = c - BeatClock.PressHalfWidth; R = c + BeatClock.PressHalfWidth; }
            float edge = dir > 0 ? L : R;
            float wait = edge - dir * Board.BodyHalf;
            float clear = laser ? edge + dir * (BeatClock.LaserGateDepth + 2f) : (dir > 0 ? R + 2f : L - 2f);
            float lead = Lead(light) + 6f;
            bool holding = false;
            int max = (int)(maxSec * 60f);
            for (int i = 0; i < max && s.Outcome == Outcome.Running; i++)
            {
                float x = s.TargetX;
                bool facing = (s.TargetFacingRight ? 1 : -1) == dir;
                bool beyond = s.TargetFloor == rf && (x - clear) * dir >= 0f;
                if (holding)
                {
                    if (beyond) { RelLate(r, light); return true; }
                    if (!facing) { r.Release(); holding = false; }   // 물러남: 다시 처음부터
                }
                else if (beyond && facing) return true;
                else if (s.TargetFloor == rf && facing && (x - wait) * dir >= -lead)
                {
                    float t0 = Mathf.Max(0f, (wait - x) * dir / 48f), t1 = Mathf.Max(0.05f, (clear - x) * dir / 48f);
                    int pick = -1;
                    for (int n = 0; n <= BeatClock.MaxPhase && pick < 0; n++)
                        if (laser ? FitsOff(s, n, t0, t1) : FitsUp(s, n, t0, t1)) pick = n;
                    if (pick > 0 && AimTo(r, rc, rf) && r.Phase(pick)) holding = true;
                }
                s.ShiftHeld = r.Shift;
                s.Step(1f / 60f);
                r.Steps++;
                r.Collect();
                if (r.Shift) r.ShiftSteps++;
                if (s.Tracking > r.Peak) r.Peak = s.Tracking;
            }
            return false;
        }

        /// <summary>
        /// 2-1 경로: 'S' = 박자 맞춤(프레스·주기 레이저 앞에서 Shift + N), 'N' = 아무것도 안 함(기다리기만).
        /// </summary>
        static Rig Fac1Route(char mode, out bool ok, out string stop, bool light = false)
        {
            var r = new Rig(Fac1Path);
            GameSim s = r.S;
            ok = true; stop = "";
            if (mode == 'S')
            {
                ok &= Cross(r, light, 0, 1, false, 1);
                if (!ok) stop = "press";
                ok &= ok && Cross(r, light, 1, 2, true, 1);
                if (stop == "" && !ok) stop = "laser";
            }
            ok &= r.Until(() => s.Outcome != Outcome.Running, mode == 'S' ? 40f : 120f);
            if (stop == "" && !ok) stop = "exit";
            return r;
        }

        /// <summary>2-2 경로: 'S' = 프레스 A·B를 N으로 어긋나게 맞춤 -> 카메라 방 Shift(끄고 지나감) -> 레이저 -> 출구. 'N' = 아무것도 안 함.</summary>
        static Rig Fac2Route(char mode, out bool ok, out string stop, bool light = false)
        {
            var r = new Rig(Fac2Path);
            GameSim s = r.S;
            ok = true; stop = "";
            if (mode == 'S')
            {
                ok &= Cross(r, light, 0, 1, false, 1);
                ok &= ok && Cross(r, light, 0, 2, false, 1);
                if (!ok) stop = "presses";
                if (ok)
                {
                    r.Stage("presses");
                    ok &= AimTo(r, 3, 0);                            // 감시 통로를 조준해 두고, 구역 경계 48px 앞(맵 x432)에 닿기 전에 카메라를 끈다
                    ok &= r.Until(() => s.TargetX >= 425f - Lead(light), 10f);
                    r.Shift = true; s.ShiftHeld = true;
                    ok &= r.Until(() => s.TargetX >= 645f, 30f);     // 구역(열 3, 맵 x480~640)을 다 지날 때까지
                    RelLate(r, light);
                    r.Stage("camera");
                    if (stop == "" && !ok) stop = "camera";
                }
                ok &= ok && Cross(r, light, 1, 2, true, -1);
                if (stop == "" && !ok) stop = "laser";
            }
            ok &= r.Until(() => s.Outcome != Outcome.Running, mode == 'S' ? 60f : 120f);
            if (stop == "" && !ok) stop = "exit";
            return r;
        }

        /// <summary>2-3 경로: 'S' = 레이저 -> 단말 -> 계단 -> 프레스(왼쪽 · 벽에서 돌아 오른쪽) -> 계단 -> 승강 칸을 Shift + ↑(3F 빈 칸) -> 관제실. 'N' 없음(승강 칸을 올려야 함).</summary>
        static Rig Fac3Route(out bool ok, out string stop, bool light = false)
        {
            var r = new Rig(Fac3Path);
            GameSim s = r.S;
            ok = true; stop = "";
            ok &= Cross(r, light, 2, 1, true, 1);
            r.Stage("laser");
            if (!ok) stop = "laser";
            ok &= ok && r.Until(() => s.TargetFloor == 1, 40f);                 // 계단 1F -> 2F(단말은 가는 길에 지남)
            ok &= ok && Cross(r, light, 1, 2, false, -1);                       // 프레스를 왼쪽으로 지나 승강 칸 · 왼쪽 벽에서 돌아옴
            r.Stage("press<-");
            ok &= ok && Cross(r, light, 1, 2, false, 1);                        // 프레스를 오른쪽으로 다시
            r.Stage("press->");
            if (stop == "" && !ok) stop = "press";
            ok &= ok && r.Until(() => s.TargetFloor == 0, 40f);                 // 계단 2F -> 3F(옥상 배관, 왼쪽을 봄)
            if (stop == "" && !ok) stop = "stairs";
            ok &= ok && AimTo(r, 1, 1);                                          // 화물 승강 칸(레일)
            ok &= ok && r.Push(0, -1);                                           // 3F 빈 칸(열 1)으로 올림
            ok &= ok && s.CellOf(1, 1) == P(1, 0);
            if (stop == "" && !ok) stop = "lift";
            ok &= ok && r.Until(() => s.TargetX < 150f, 30f);                   // 승강 칸을 다 건너 관제실로 들어올 때까지 누름
            RelLate(r, light);
            r.Stage("lift");
            ok &= r.Until(() => s.Outcome != Outcome.Running, 20f);
            if (stop == "" && !ok) stop = "control";
            return r;
        }

        static readonly List<string> w038Table = new List<string>();

        static void W038Row(string request, string route, bool light, Rig r, bool ok, string stop)
        {
            GameSim s = r.S;
            string clear = s.Outcome == Outcome.Complete ? "clear" : s.Outcome == Outcome.GameOver ? "GAME OVER" : s.Outcome.ToString();
            float t = r.Steps / 60f;
            w038Table.Add(request + " | " + route + " | " + (light ? "light" : "optimal") + " | " + clear + " | time " + t.ToString("0.0") + " s | peak " + r.Peak.ToString("0.0") + " | stars " + s.Stars
                + " | detect " + s.DetectCount + " | Shift " + r.ShiftSeconds.ToString("0.00") + " s | terminal " + (s.MissionDone(1) ? "done" : "-") + ", 3rd " + (s.MissionFailed(2) ? "failed" : "kept")
                + " | stages " + r.Stages + " | PRESS " + r.CountUse(UseKind.PressPassed) + " LASER " + r.CountUse(UseKind.LaserPassed) + " retreats " + s.RetreatSeq + (ok ? "" : " | stopped at " + stop));
        }

        static void W038RouteChecks()
        {
            w038Table.Clear();
            foreach (bool light in new[] { false, true })
            {
                string m = light ? "light" : "optimal";
                foreach (char mode in new[] { 'S', 'N' })
                {
                    Rig r = Fac1Route(mode, out bool ok, out string stop, light);
                    GameSim s = r.S;
                    string name = mode == 'S' ? "match the beat (Shift + N at the press and the laser)" : "do nothing (wait)";
                    W038Row("2-1 FACTORY A", name, light, r, ok, stop);
                    if (!light)
                        Check("W-038 2-1 route " + name + " [" + m + "]: Complete (final done), peak " + r.Peak.ToString("0.0") + " < 100, PRESS PASSED x" + r.CountUse(UseKind.PressPassed) + (ok ? "" : " - stopped at " + stop),
                            ok && s.Outcome == Outcome.Complete && s.MissionFinal && r.Peak < s.TrackingMax && r.CountUse(UseKind.PressPassed) >= 1 && (mode == 'N' || r.CountUse(UseKind.LaserPassed) >= 1));
                }
                foreach (char mode in new[] { 'S', 'N' })
                {
                    Rig r = Fac2Route(mode, out bool ok, out string stop, light);
                    GameSim s = r.S;
                    string name = mode == 'S' ? "match two presses with different N, camera room Shift, laser Shift + N" : "do nothing (wait)";
                    W038Row("2-2 FACTORY B", name, light, r, ok, stop);
                    if (!light && mode == 'S')
                        Check("W-038 2-2 route " + name + " [" + m + "]: Complete, UNSEEN kept (detections 0), relay used, peak " + r.Peak.ToString("0.0") + " < 100" + (ok ? "" : " - stopped at " + stop),
                            ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.DetectCount == 0 && !s.MissionFailed(2) && s.RelayUsed && r.Peak < s.TrackingMax && r.CountUse(UseKind.PressPassed) >= 2);
                    if (!light && mode == 'N')
                        Check("W-038 2-2 do nothing [" + m + "]: the runner never clears the level (camera zone turns it back / stays Running, not a game over), time " + (r.Steps / 60f).ToString("0") + " s", s.Outcome == Outcome.Running && r.Peak < 0.001f);
                }
                {
                    Rig r = Fac3Route(out bool ok, out string stop, light);
                    GameSim s = r.S;
                    W038Row("2-3 FACTORY C", "laser, presses, lift up (Shift + up), control room", light, r, ok, stop);
                    if (!light)
                        Check("W-038 2-3 route [" + m + "]: Complete in the control room (lift pushed to 3F col 1), terminal done, relay used, peak " + r.Peak.ToString("0.0") + " < 100" + (ok ? "" : " - stopped at " + stop),
                            ok && s.Outcome == Outcome.Complete && s.MissionFinal && s.MissionDone(1) && s.RelayUsed && r.Peak < s.TrackingMax && r.CountUse(UseKind.Carried) == 0);
                }
            }
            foreach (string row in w038Table) Note("W-038 TABLE [simulation] " + row);
        }

        /// <summary>규칙 확인용 레벨: 1층 x 3열, 열 1이 주기 레이저. 시작 = 열 0 x20 오른쪽, 출구 = 열 2 x40.</summary>
        static Dt.LevelData MakeLaserLevel()
        {
            var l = ScriptableObject.CreateInstance<Dt.LevelData>();
            l.floors = 1; l.cols = 3; l.sectorWidth = 160; l.sectorHeight = 96; l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.sectors = new[] { new Dt.SectorDef { floor = 0, col = 1, target = Dt.SectorTarget.Whole, periodicLaser = true } };
            l.objects = new[]
            {
                new Dt.ObjectDef { kind = Dt.ObjectKind.Exit, floor = 0, col = 2, x = 40, drawX = 26, drawSprite = true, mission = 0, detectTrackingOverride = -1f },
            };
            l.startFloor = 0; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 0; l.startAimCol = 1;
            l.missions = new[] { new Dt.MissionDef { kind = Dt.MissionKind.PassObject } };
            l.trackingRateScale = 0.7f;
            return l;
        }

        /// <summary>W-038: 새 의뢰 데이터·크기, 주기 레이저 규칙(주기·오프셋·기다림·꺼진 박에 지나감·물러남·Shift 단독 변화 없음), 챕터 0·1 불변.</summary>
        static void W038PureChecks()
        {
            // ----- 세 의뢰의 크기·방 배치 -----
            foreach (var z in new[]
            {
                new { name = "2-1 FACTORY A", path = Fac1Path, floors = 2, cols = 4, key = "scenes128/ch2_fac1/layout", clocks = new[] { P(1, 0) }, lasers = new[] { P(2, 1) }, presses = 1 },
                new { name = "2-2 FACTORY B", path = Fac2Path, floors = 2, cols = 5, key = "scenes128/ch2_fac2/layout", clocks = new[] { P(1, 0), P(2, 0) }, lasers = new[] { P(2, 1) }, presses = 2 },
                new { name = "2-3 FACTORY C", path = Fac3Path, floors = 3, cols = 4, key = "scenes128/ch2_fac3/layout", clocks = new[] { P(1, 1), P(2, 1) }, lasers = new[] { P(1, 2) }, presses = 1 },
            })
            {
                var r = new Rig(z.path);
                Board b = r.S.Board;
                bool clocksOk = true, lasersOk = true;
                foreach (Vector2Int c in z.clocks) clocksOk &= b.HasClock(c.y, c.x) && b.IsOperable(c.y, c.x);
                foreach (Vector2Int c in z.lasers) lasersOk &= b.IsPeriodicLaser(c.y, c.x) && b.IsWhole(c.y, c.x) && b.HasClock(c.y, c.x) && b.IsOperable(c.y, c.x);
                int pn = 0;
                foreach (SectorObject o in b.Objects) if (o.Def.kind == Dt.ObjectKind.Press) pn++;
                Check("W-038 " + z.name + " map: " + z.floors + " floors x " + z.cols + " cols, layout " + z.key + " loaded with the same size and every room texture in the art set, rate scale 0.7, clock rooms and the periodic laser room, " + z.presses + " press(es), 3 missions",
                    b.Floors == z.floors && b.Cols == z.cols && b.Layout != null && b.LayoutKey == z.key && b.Layout.Floors == z.floors && b.Layout.Cols == z.cols && LayoutFilesOk(b.Layout)
                    && Near(b.TrackingRateScale, 0.7f, 0.0001f) && b.HasClocks && b.HasPeriodicLasers && clocksOk && lasersOk && pn == z.presses && b.Missions.Length == 3);
            }
            {
                var b3 = new Rig(Fac3Path).S.Board;
                var l1 = AssetDatabase.LoadAssetAtPath<Dt.LevelData>(Fac1Path);
                var l2 = AssetDatabase.LoadAssetAtPath<Dt.LevelData>(Fac2Path);
                var l3 = AssetDatabase.LoadAssetAtPath<Dt.LevelData>(Fac3Path);
                Check("W-038 2-3: the cargo lift room (2F col 1) is a rail room with a clock-less rail, empty cells (3F col 1, 3F col 3, 2F col 0), exit in the 3F col 0 control room, no stairs to the exit room",
                    b3.HasRail(1, 1) && b3.IsEmpty(0, 1) && b3.IsEmpty(0, 3) && b3.IsEmpty(1, 0) && FindObj(b3, Dt.ObjectKind.Exit, 0, 0) != null && FindObj(b3, Dt.ObjectKind.Stairs, 0, 0) == null);
                Check("W-038 data: 2-1 has 4 tutorial steps (beat bar Enter / Shift / Shift + digits / laser), 2-2 and 2-3 have none, 2-1 guide on (not a tutorial level, no game-over lock), env 2-1 Night/Cloudy, 2-2 Night/Clear, 2-3 Night/Rain",
                    l1.tutorialSteps.Length == 4 && l1.showGuide && !l1.tutorial && l2.tutorialSteps.Length == 0 && !l2.showGuide && l3.tutorialSteps.Length == 0 && !l3.showGuide
                    && l1.tutorialSteps[0].focus == Dt.TutorialFocus.BeatBar && l1.tutorialSteps[2].input == Dt.TutorialInput.ShiftDigit && l1.tutorialSteps[3].cond == Dt.TutorialCond.TargetWaiting
                    && l1.weather == Dt.Weather.Cloudy && l2.weather == Dt.Weather.Clear && l3.weather == Dt.Weather.Rain && l1.timeOfDay == Dt.TimeOfDay.Night && l2.timeOfDay == Dt.TimeOfDay.Night && l3.timeOfDay == Dt.TimeOfDay.Night);
                Check("W-038 data: 2-2 mission 3 is UNSEEN (camera in the 2F col 3 room, relay in the 2F col 2 room after press B), 2-1 / 2-3 TRACE limits " + Mvp1Setup.Fac1Trace + " / " + Mvp1Setup.Fac3Trace,
                    l2.missions[2].kind == Dt.MissionKind.Unseen && FindObj(new Rig(Fac2Path).S.Board, Dt.ObjectKind.Camera, 0, 3) != null && FindObj(new Rig(Fac2Path).S.Board, Dt.ObjectKind.Relay, 0, 2) != null
                    && l1.missions[2].kind == Dt.MissionKind.TraceLimit && Near(l1.missions[2].limit, Mvp1Setup.Fac1Trace) && l3.missions[2].kind == Dt.MissionKind.TraceLimit && Near(l3.missions[2].limit, Mvp1Setup.Fac3Trace));
            }

            // ----- 주기: 켜짐·켜짐·켜짐·꺼짐, 박 0.6초, 오프셋 N, Shift 단독은 상태를 바꾸지 않음 -----
            {
                var r = new Rig(MakeLaserLevel());
                GameSim s = r.S;
                var seq = new List<bool>();
                for (int beat = 0; beat < 8; beat++) seq.Add(BeatClock.LaserOnAt(beat));
                bool table = seq[0] && seq[1] && seq[2] && !seq[3] && seq[4] && seq[5] && seq[6] && !seq[7] && BeatClock.LaserOnAt(-1) == false && BeatClock.LaserOnAt(-4) && BeatClock.LaserOnAt(-5) == false;
                r.Run(0.1f);   // 박 0: 켜짐
                bool on0 = s.SectorBlocked(0, 1) && s.LaserOnAhead(0) && s.LaserOnAhead(1) && s.LaserOnAhead(2) && !s.LaserOnAhead(3) && s.LaserOnAhead(4);
                s.ShiftHeld = true; r.Shift = true;
                bool shiftAlone = s.SectorBlocked(0, 1) && s.IsBlueprint(0, 1) && new Rig(MakeLaserLevel()).S.Board.Blocked(0, 1, false) == new Rig(MakeLaserLevel()).S.Board.Blocked(0, 1, true);
                bool set3 = s.TrySetPhase(3);
                bool off = !s.SectorBlocked(0, 1) && s.RoomPhase(0, 1) == 3 && s.PhaseN == 3;
                r.Release();
                bool back = s.SectorBlocked(0, 1) && s.PhaseN == 0;
                Check("W-038 periodic laser cycle [on, on, on, off]: beat 0..7 table, 4-beat period, LaserOnAhead(+1..+3) = on, on, off at beat 0; Shift alone does not open it (reality = blueprint), Shift + 3 turns it off, releasing Shift turns it back on",
                    table && on0 && shiftAlone && set3 && off && back);
                var q = new Rig(MakeLaserLevel());
                q.Run(0.1f);
                bool ph = true;
                for (int n = 1; n <= 3; n++)
                {
                    var t = new Rig(MakeLaserLevel());
                    t.Run(0.1f);
                    t.S.ShiftHeld = true; t.Shift = true; t.S.TrySetPhase(n);
                    ph &= t.S.LaserOn(0, 1) == BeatClock.LaserOnAt(t.S.Beat + n);
                }
                var u = new Rig(MakeLaserLevel());
                u.Shift = true; u.S.ShiftHeld = true; u.S.TrySetPhase(1);
                u.Run(1f);
                Check("W-038 laser offset N: LaserOn = table[beat + N] for N 1..3; tracking while Shift + 1 is the same 14 x (1 + 0.25) = 17.5 per second (measured " + u.S.Tracking.ToString("0.00") + ")", ph && Near(u.S.Tracking, 17.5f, 0.05f));
            }

            // ----- 기다림: 켜져 있으면 앞 가장자리에서 기다리고, 꺼진 박에 들어가 지남(물러나지 않음), 지나가는 사건 기록 -----
            {
                var r = new Rig(MakeLaserLevel());
                GameSim s = r.S;
                bool wait = r.Until(() => s.TargetWaiting, 30f);
                float wx = s.TargetX;
                int beatWait = s.Beat;
                bool onWhileWaiting = s.SectorBlocked(0, 1);
                bool resumes = r.Until(() => !s.TargetWaiting, 5f);
                int beatGo = s.Beat;
                r.Until(() => s.TargetX > 160f + 25f, 5f);
                bool noRetreat = s.RetreatSeq == 0;
                r.Until(() => s.Outcome != Outcome.Running, 20f);
                Check("W-038 periodic laser, do nothing: the runner waits in front of the sector (x = 160 - 9 = 151, measured " + wx.ToString("0.0") + ") while the laser is on (beat " + beatWait + "), walks in when the off beat comes (beat " + beatGo + ", slot 3), is deeper than "
                    + BeatClock.LaserGateDepth + " px when it turns on again so it does not step back, passes (LASER PASSED x" + r.CountUse(UseKind.LaserPassed) + ") and clears",
                    wait && onWhileWaiting && Near(wx, 151f, 0.6f) && resumes && BeatClock.Slot(beatGo) == 3 && noRetreat && s.Outcome == Outcome.Complete && r.CountUse(UseKind.LaserPassed) == 1 && s.Tracking < 0.001f);
            }

            // ----- Shift + N으로 앞당겨 바로 들어감, 너무 일찍 떼면 들어온 쪽으로 물러남(다치지 않음) -----
            {
                var r = new Rig(MakeLaserLevel());
                GameSim s = r.S;
                r.Until(() => s.TargetWaiting, 30f);
                int pick = -1;
                for (int n = 1; n <= 3 && pick < 0; n++) if (FitsOff(s, n, 0f, 0.4f)) pick = n;
                if (pick < 0) { r.Run(0.3f); for (int n = 1; n <= 3 && pick < 0; n++) if (FitsOff(s, n, 0f, 0.4f)) pick = n; }
                r.Shift = true; s.ShiftHeld = true;
                bool setOk = pick > 0 && s.TrySetPhase(pick);
                bool resumes = r.Until(() => !s.TargetWaiting, 1f);
                r.Until(() => s.TargetX > 160f + BeatClock.LaserGateDepth + 3f, 2f);
                r.Release();   // 더 들어간 뒤에 뗌: 이미 지났으므로 물러나지 않는다
                r.Run(0.2f);
                Check("W-038 Shift + " + pick + " in front of the laser: it opens at once (the runner resumes), the runner is deeper than " + BeatClock.LaserGateDepth + " px when Shift is released so it keeps going (no retreat, no hurt, still running or cleared)",
                    setOk && resumes && s.RetreatSeq == 0 && s.Outcome != Outcome.GameOver && s.TargetX > 160f + BeatClock.LaserGateDepth);

                var q = new Rig(MakeLaserLevel());
                GameSim sq = q.S;
                q.Until(() => sq.TargetWaiting, 30f);
                int pk = -1;
                for (int n = 1; n <= 3 && pk < 0; n++) if (FitsOff(sq, n, 0f, 0.4f)) pk = n;
                if (pk < 0) { q.Run(0.3f); for (int n = 1; n <= 3 && pk < 0; n++) if (FitsOff(sq, n, 0f, 0.4f)) pk = n; }
                q.Shift = true; sq.ShiftHeld = true;
                sq.TrySetPhase(pk);
                q.Run(0.25f);                // 0.25초 걸으면 약 12px = 방 안 3px: 너무 얕다
                bool shallow = sq.TargetX > 160f && sq.TargetX < 160f + BeatClock.LaserGateDepth;
                q.Release();                 // 얕게 들어간 채 떼면 레이저가 다시 켜진다
                q.Run(0.1f);
                bool stepped = sq.RetreatSeq == 1 && !sq.TargetFacingRight && sq.Outcome == Outcome.Running && !sq.TargetWaiting;
                q.Run(1.5f);
                Check("W-038 releasing Shift while the runner is inside the first " + BeatClock.LaserGateDepth + " px of the sector: the laser comes back on, the runner steps back the way it came (faces left, RetreatSeq 1, not hurt, game still running) and leaves the sector",
                    shallow && stepped && sq.TargetX < 160f && sq.Outcome == Outcome.Running && sq.Tracking < sq.TrackingMax);
            }

            // ----- 챕터 0·1·시험 맵 불변 -----
            {
                Rig ra = Old3Route('A', out bool oka, out _);
                var t = new Rig(Test20LevelPath);
                Check("W-038 chapter 1 / TEST_2-0 unchanged: 1-3 route A still clears with 2 stars and TRACE <= 80 kept; TEST_2-0 has no periodic laser (HasPeriodicLasers false, presses unchanged)",
                    oka && ra.S.Outcome == Outcome.Complete && ra.S.Stars == 2 && !ra.S.MissionFailed(2) && !t.S.Board.HasPeriodicLasers && !ra.S.Board.HasPeriodicLasers);
            }
        }

        [MenuItem("GameOff/W-038 Route Check")]
        public static void W038RouteMenu()
        {
            results.Clear();
            W035PureChecks();
            W035RouteChecks();
            W036PureChecks();
            W036RouteChecks();
            W038PureChecks();
            W038RouteChecks();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/w038-routes.txt"));
            File.WriteAllLines(path, results);
        }

        static string CaptureDir38 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-038"));

        static void CaptureW38(string name)
        {
            Directory.CreateDirectory(CaptureDir38);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir38, name + "_1920x1080.png"), 1920, 1080);
        }

        /// <summary>W-038 화면 흐름: 목록·잠금·번호, 시험 의뢰가 팝업에 없음, 2-1 튜토리얼 4단계(실제 입력), 2-2·2-3 장면, 새 대사 줄 수, 한국어 대화.</summary>
        static IEnumerator W038Checks()
        {
            view.Progress.EnvFxReduced = false;
            view.Progress.Clear(AllIds());
            Tutorial.AutoPass = false;
            RequestBoard d = view.Desk;

            // ----- 목록·번호·잠금 -----
            string[] fids = { "fac-2-1", "fac-2-2", "fac-2-3" };
            bool ok = d.Count == 12 && d.InChapter(2).Count == 3 && !d.InChapter(2).Contains(8);
            for (int i = 0; i < 3; i++) ok &= d.Entry(9 + i).id == fids[i] && d.Number(9 + i) == "2-" + (i + 1) && d.RequestName(9 + i) == "REQ_2-" + (i + 1) && d.ChapterOf(9 + i) == 2 && d.IndexInChapter(9 + i) == i && !d.Entry(9 + i).testEntry;
            Check("W-038 list: 12 requests; chapter 2 shows exactly REQ_2-1 .. REQ_2-3 (indexes 9..11, ids fac-2-1 .. fac-2-3), TEST_2-0 (index 8) is not in the chapter list and does not count", ok);
            Check("W-038 chapter 2 is locked while chapter 1 is not cleared, TEST_2-0 can no longer open it", !d.ChapterOpen(2) && !d.IsUnlocked(9) && d.State(9) == RequestState.Locked);
            foreach (string id in new[] { "home-0-1", "home-0-2", "old-1-1", "old-1-2", "old-1-3", "old-1-f", "old-1-g" }) view.Progress.SetCleared(id);
            bool still = !d.ChapterOpen(2);
            view.Progress.SetCleared("old-1-4");
            bool open1 = d.ChapterOpen(2) && d.IsUnlocked(9) && d.IsPlayable(9) && d.State(9) == RequestState.New && !d.IsUnlocked(10) && !d.IsUnlocked(11);
            view.Progress.SetCleared("fac-2-1");
            bool open2 = d.IsPlayable(10) && d.State(10) == RequestState.New && !d.IsUnlocked(11);
            view.Progress.SetCleared("fac-2-2");
            bool open3 = d.IsPlayable(11) && d.State(11) == RequestState.New;
            Check("W-038 unlock order: chapter 2 stays locked until 1-6 (old-1-4) clears; then 2-1 opens, 2-1 clear opens 2-2, 2-2 clear opens 2-3", still && open1 && open2 && open3);

            // ----- 데이터 -----
            Dt.RequestEntry e1 = d.Entry(9), e2 = d.Entry(10), e3 = d.Entry(11);
            Check("W-038 data: 2-1 ON THE BEAT (REACH THE EXIT / STOCK TERMINAL / TRACE <= " + Mvp1Setup.Fac1Trace + "), 2-2 OFFBEAT (REACH THE EXIT / PACKING TERMINAL / UNSEEN), 2-3 CONTROL ROOM (REACH CONTROL / STOCK TERMINAL / TRACE <= " + Mvp1Setup.Fac3Trace + "); buildings FACTORY A/B/C",
                e1.title == "ON THE BEAT" && e1.missionNames.Length == 3 && e1.missionNames[1] == "STOCK TERMINAL" && e1.missionNames[2] == "TRACE <= " + Mvp1Setup.Fac1Trace && e1.level.buildingName == "FACTORY A"
                && e2.title == "OFFBEAT" && e2.missionNames[0] == "REACH THE EXIT" && e2.missionNames[1] == "PACKING TERMINAL" && e2.missionNames[2] == "UNSEEN" && e2.level.buildingName == "FACTORY B"
                && e3.title == "CONTROL ROOM" && e3.missionNames[0] == "REACH CONTROL" && e3.missionNames[2] == "TRACE <= " + Mvp1Setup.Fac3Trace && e3.level.buildingName == "FACTORY C");
            Check("W-038 dialogue: 2-1 / 2-2 / 2-3 have 3 briefing + 3 ending lines each (first lines EMPTY FACTORY... / PACKING LINE... / CONTROL ROOM. TOP FLOOR...), 1-4 and 1-5 now have 3 + 3 lines (first ROOFTOP AGAIN... / FOUR FLOORS...)",
                e1.dialogue.briefing.Length == 3 && e1.dialogue.ending.Length == 3 && e1.dialogue.briefing[0].text == "EMPTY FACTORY. THE MACHINES STILL RUN ON A TIMER." && e1.dialogue.briefing[0].speakerName == "BROKER"
                && e2.dialogue.briefing.Length == 3 && e2.dialogue.ending.Length == 3 && e2.dialogue.briefing[0].text == "PACKING LINE. TWO PRESSES. ONE CAMERA." && e2.dialogue.ending[1].speaker == Dt.Speaker.Me
                && e3.dialogue.briefing.Length == 3 && e3.dialogue.ending.Length == 3 && e3.dialogue.briefing[0].text == "CONTROL ROOM. TOP FLOOR. NO STAIRS LEAD THERE." && e3.dialogue.ending[2].speakerName == "BROKER"
                && d.Entry(5).dialogue.briefing.Length == 3 && d.Entry(5).dialogue.ending.Length == 3 && d.Entry(5).dialogue.briefing[0].text == "ROOFTOP AGAIN. TWO GAPS THIS TIME."
                && d.Entry(6).dialogue.briefing.Length == 3 && d.Entry(6).dialogue.ending.Length == 3 && d.Entry(6).dialogue.briefing[0].text == "FOUR FLOORS. A CAMERA WATCHES THE ONLY STAIRS.");
            Check("W-038 replaced lines (07-01 v0.5.0): 1-2 end e2/e3 = NEXT JOB WITHIN THE HOUR / CATCH YOUR BREATH, 1-3 end e2 = THE NIGHT ISN'T OVER, 1-6 times 01:40..01:58 and end e2 = NEXT TIME, OUT OF TOWN, other lines kept",
                d.Entry(3).dialogue.ending[1].text == "CLEAN. NEXT JOB WITHIN THE HOUR." && d.Entry(3).dialogue.ending[2].text == "CATCH YOUR BREATH." && d.Entry(3).dialogue.ending[0].text == "ROOF! NICE VIEW. NEXT TIME, A REAL DOOR?"
                && d.Entry(4).dialogue.ending[1].text == "DONE. THE NIGHT ISN'T OVER." && d.Entry(4).dialogue.ending[2].text == "KEEP YOUR SHOES ON."
                && d.Entry(7).dialogue.briefing[0].time == "01:40" && d.Entry(7).dialogue.briefing[2].time == "01:41" && d.Entry(7).dialogue.ending[0].time == "01:57" && d.Entry(7).dialogue.ending[1].time == "01:57" && d.Entry(7).dialogue.ending[2].time == "01:58"
                && d.Entry(7).dialogue.ending[1].text == "CLEAN WORK. NEXT TIME, OUT OF TOWN." && d.Entry(7).dialogue.ending[2].text == "LOGGING OFF.");
            {
                bool all = true; string miss = "";
                foreach (Dt.RequestEntry e in new[] { e1, e2, e3, d.Entry(5), d.Entry(6) })
                {
                    foreach (var l in e.dialogue.briefing) { if (!Loc.HasEn(l.text)) { all = false; miss += " | " + l.text; } }
                    foreach (var l in e.dialogue.ending) { if (!Loc.HasEn(l.text)) { all = false; miss += " | " + l.text; } }
                }
                foreach (var st in e1.level.tutorialSteps) if (!Loc.HasEn(st.text)) { all = false; miss += " | " + st.text; }
                Loc.Current = Language.Korean;
                string k1 = Loc.Tr(e1.dialogue.briefing[0].text), k2 = Loc.Tr(e3.dialogue.ending[1].text), k3 = Loc.Tr(e1.level.tutorialSteps[2].text), k4 = Loc.Tr("LASER PASSED");
                Loc.Current = Language.English;
                Check("W-038 Korean table: every new dialogue line and 2-1 guide step has a row and shows Korean (no empty cell), LASER PASSED = " + k4 + (miss.Length > 0 ? " - missing:" + miss : ""),
                    all && k1 == "빈 공장이다. 기계는 아직 타이머대로 돈다." && k2 == "그럼 계속 찾아보지." && k3 == "1~3을 눌러 박자를 앞당겨. 열린 박자에 맞춰." && k4 == "레이저 통과");
            }

            // ----- 바탕화면 챕터 2 목록(캡처): TEST_2-0 없음 -----
            yield return ToMain();
            yield return Frames(4);
            if (view.MainIndex != 0) yield return Press("Up");
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "W-038 Desktop");
            yield return Press("Enter");
            for (int i = 0; i < 3 && view.FolderIndex != 2; i++) yield return Press("Down");
            yield return Press("Right");
            Check("W-038 CH2 list opened: REQ_2-1 selected, 3 requests, TEST_2-0 absent", view.InFolder && view.FolderIndex == 2 && view.DeskIndex == 9 && view.Desk.InChapter(2).Count == 3 && !view.Desk.InChapter(2).Contains(8));
            yield return Press("Down");
            yield return Press("Down");
            yield return Frames(3);
            CaptureW38("01-ch2-list");
            yield return Frames(1);
            yield return ToMain();
            yield return Frames(2);

            // ----- 2-1 튜토리얼 4단계(실제 입력 경로): 박자 표시 -> Shift -> Shift + 숫자 -> 주기 레이저 앞 -----
            yield return StartLevelFor(9, "Ch2Fac_1.asset");
            Check("W-038 2-1 on screen: Playing at once, header FACTORY A, 2 floors x 4 cols, layout ch2_fac1 loaded, tutorial step 1 is frozen on the beat bar (ENTER badge)",
                view.CurrentPhase == GameView.Phase.Playing && Sim.Board.BuildingName == "FACTORY A" && Sim.Board.Floors == 2 && Sim.Board.Cols == 4 && Sim.Board.LayoutKey == "scenes128/ch2_fac1/layout" && LayoutArtOk(Sim.Board.Layout)
                && TutIs(0, "ENTER", "MACHINES MOVE TO A SHARED BEAT.") && view.Tut.StepCount == 4);
            int beat0 = Sim.Beat;
            yield return Frames(20);
            Check("W-038 2-1 step 1 freezes game time (beat clock still " + beat0 + " after 20 frames)", Sim.Beat == beat0 && view.Tut.Frozen);
            yield return WaitReal(0.3);
            CaptureW38("02-2-1-start-beat-bar");
            next.Digit = 2;   // 틀린 키(숫자)는 무시된다
            yield return Frames(2);
            Check("W-038 2-1 step 1: a digit key is ignored while the step waits for ENTER", view.Tut.Index == 0 && Sim.PhaseN == 0);
            yield return Press("Enter");
            Check("W-038 2-1 step 2 (SHIFT): text SHIFT SHOWS THE NEXT BEATS., frozen, aim on the press room", TutIs(1, "SHIFT", "SHIFT SHOWS THE NEXT BEATS.") && Sim.Aim == P(1, 0));
            shift = true;
            yield return Frames(3);
            Check("W-038 2-1 step 3 (SHIFT 1 2 3): Shift pressed -> the digit step is frozen, the preview appears, arrows do not work", TutIs(2, "SHIFT 1 2 3", "PRESS 1-3 TO SKIP AHEAD. MATCH AN OPEN BEAT.") && Sim.ShiftHeld);
            yield return WaitReal(0.3);
            CaptureW38("03-2-1-digit-step");
            next.Right = true;
            yield return Frames(2);
            Check("W-038 2-1 step 3: an arrow key is ignored (aim unchanged, step stays)", view.Tut.Index == 2 && Sim.Aim == P(1, 0));
            next.Digit = 2;
            yield return Frames(3);
            Check("W-038 2-1 step 3 done by Shift + 2: offset 2 on the press room, step advanced (4th step waits for the laser), time runs again", Sim.PhaseN == 2 && Sim.PhaseRoom == 1 && view.Tut.Index == 3 && !view.Tut.Frozen);
            shift = false;
            yield return Frames(3);
            yield return WaitUntil(() => view.Tut.Active, "W-038 2-1 step 4 (waiting at the laser)", 12000);
            Check("W-038 2-1 step 4: the runner waits in front of the 1F periodic laser -> ONE BEAT OFF. PULL IT CLOSER. (frozen, ENTER)", TutIs(3, "ENTER", "ONE BEAT OFF. PULL IT CLOSER.") && Sim.TargetFloor == 1 && Sim.TargetWaiting);
            yield return WaitReal(0.3);
            CaptureW38("04-2-1-laser-step");
            yield return Press("Enter");
            Check("W-038 2-1: all 4 steps done, play continues", view.Tut.Index == 4 && !view.Tut.Frozen);
            Tutorial.AutoPass = true;

            // ----- 2-2: 프레스 둘 -----
            yield return StartLevelFor(10, "Ch2Fac_2.asset");
            Check("W-038 2-2 on screen: header FACTORY B, 2 floors x 5 cols, layout ch2_fac2 loaded, no guide box, two clock rooms side by side (2F col 1 and 2) and the camera room (2F col 3)",
                view.CurrentPhase == GameView.Phase.Playing && Sim.Board.BuildingName == "FACTORY B" && Sim.Board.Floors == 2 && Sim.Board.Cols == 5 && LayoutArtOk(Sim.Board.Layout) && view.Tut == null
                && Sim.Board.HasClock(0, 1) && Sim.Board.HasClock(0, 2) && FindObj(Sim.Board, Dt.ObjectKind.Camera, 0, 3) != null);
            Sim.ShiftHeld = false; AimToSim(Sim, 2, 0);
            yield return Frames(30);
            CaptureW38("05-2-2-two-presses");
            yield return Frames(1);

            // ----- 2-3: 승강 칸을 올린 뒤(sim 직접 진행) -----
            yield return StartLevelFor(11, "Ch2Fac_3.asset");
            Check("W-038 2-3 on screen: header FACTORY C, 3 floors x 4 cols, layout ch2_fac3 loaded, no guide box, the lift room (2F col 1) is a rail room",
                view.CurrentPhase == GameView.Phase.Playing && Sim.Board.BuildingName == "FACTORY C" && Sim.Board.Floors == 3 && Sim.Board.Cols == 4 && LayoutArtOk(Sim.Board.Layout) && view.Tut == null && Sim.Board.HasRail(1, 1));
            {
                var rg = new Rig(Sim);
                bool c1 = Cross(rg, false, 2, 1, true, 1);
                bool up = rg.Until(() => Sim.TargetFloor == 1, 40f);
                bool c2 = Cross(rg, false, 1, 2, false, -1);
                bool c3 = Cross(rg, false, 1, 2, false, 1);
                bool up2 = rg.Until(() => Sim.TargetFloor == 0, 40f);
                AimTo(rg, 1, 1);
                bool pushed = rg.Push(0, -1);
                shift = true;   // 화면 쪽 Shift 값도 맞춰 두어 다음 프레임에 떼어지지 않게 한다
                rg.Until(() => Sim.TargetX < 330f, 10f);
                yield return Frames(3);
                Check("W-038 2-3 on screen: after the laser, the stairs, the press (both ways) and the stairs to 3F, Shift + up lifts the cargo lift room into the empty 3F col 1 (the runner stands in the 3F roof pipe room)",
                    c1 && up && c2 && c3 && up2 && pushed && Sim.CellOf(1, 1) == P(1, 0) && Sim.Pushed);
                yield return WaitReal(0.2);
                CaptureW38("06-2-3-lift-up");
                yield return Frames(1);
                shift = false;
                yield return Frames(3);
            }

            // ----- 한국어 대화 1장(2-1 브리핑) -----
            Loc.Current = Language.Korean;
            view.Progress.Language = Language.Korean;
            yield return ToMain();
            yield return Frames(4);
            if (view.MainIndex != 0) yield return Press("Up");
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "W-038 Desktop (Korean)");
            yield return Press("Enter");
            for (int i = 0; i < 3 && view.FolderIndex != 2; i++) yield return Press("Down");
            yield return Press("Right");
            yield return Press("Enter");   // 2-1 접속
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Briefing, "W-038 briefing (Korean)", 3000);
            yield return WaitReal(0.8);
            Check("W-038 Korean briefing on screen: 2-1 starts a 3-line briefing, the first line shows the Korean text, no empty cell", view.CurrentPhase == GameView.Phase.Briefing && Loc.Current == Language.Korean && Loc.Tr(view.Desk.Entry(9).dialogue.briefing[0].text).Length > 0 && Loc.Tr(view.Desk.Entry(9).dialogue.briefing[0].text) != view.Desk.Entry(9).dialogue.briefing[0].text);
            CaptureW38("07-ko-briefing");
            yield return Frames(1);
            Loc.Current = Language.English;
            view.Progress.Language = Language.English;
            yield return ToMain();
            yield return Frames(2);
            view.Progress.Clear(AllIds());
        }

        // ===================================================================================================
        // W-047: S5 예고 — 바탕화면 챕터 2(2-4~2-6 잠금 줄)·챕터 3·4 예고 폴더, 2-3 첫 클리어 뒤 챕터 끝 예고 화면
        // ===================================================================================================

        static string CaptureDir47 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-047"));

        static void CaptureW47(string name)
        {
            Directory.CreateDirectory(CaptureDir47);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir47, name + "_1920x1080.png"), 1920, 1080);
        }

        /// <summary>자동 확인 전용: 의뢰를 시작해 모든 미션을 끝낸 판정으로 만들고 결과 창(Ended)까지 간다(종료 대화는 Enter로 넘긴다).</summary>
        static IEnumerator W047ClearToEnded(int requestIndex, string asset)
        {
            yield return StartLevelFor(requestIndex, asset);
            ForceSimCompleteAll();
            yield return ToEnded();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "W-047 Ended");
            yield return Frames(2);
        }

        /// <summary>자동 확인 전용: 기록을 지운 뒤 지정한 의뢰만 빼고 모두 클리어한 기록으로 만든다.</summary>
        static void W047ClearAllExcept(int keep)
        {
            view.Progress.Clear(AllIds());
            for (int i = 0; i < view.Desk.Count; i++) if (i != keep) view.Progress.SetCleared(view.Desk.Entry(i).id);
        }

        static IEnumerator W047Checks()
        {
            Loc.Current = Language.English;
            view.Progress.Language = Language.English;
            view.Progress.Clear(AllIds());
            Check("W-047 data: 5 chapter folders (CH3_??? / CH4_???), planned counts 2 / 6 / 6 / 6 / 0, chapters 3 and 4 have no requests and stay locked",
                RequestBoard.ChapterCount == 5 && RequestBoard.ChapterName(3) == "CH3_???" && RequestBoard.ChapterName(4) == "CH4_???"
                && RequestBoard.PlannedCount(0) == 2 && RequestBoard.PlannedCount(1) == 6 && RequestBoard.PlannedCount(2) == 6 && RequestBoard.PlannedCount(3) == 6 && RequestBoard.PlannedCount(4) == 0
                && view.Desk.InChapter(3).Count == 0 && view.Desk.InChapter(4).Count == 0 && view.Desk.IsTeaserChapter(3) && view.Desk.IsTeaserChapter(4) && !view.Desk.IsTeaserChapter(2)
                && !view.Desk.ChapterOpen(3) && !view.Desk.ChapterOpen(4));
            Check("W-047 data: no fake requests were added (list still 12); only 2-3 (index 11) leads to the chapter 3 preview",
                view.Desk.Count == 12 && view.Desk.PreviewChapterAfter(11) == 3 && view.Desk.PreviewChapterAfter(10) == -1 && view.Desk.PreviewChapterAfter(9) == -1 && view.Desk.PreviewChapterAfter(8) == -1 && view.Desk.PreviewChapterAfter(7) == -1);
            string[] keys = { "desk.coming_soon", "desk.teaser_ch3_a", "desk.teaser_ch3_b", "desk.teaser_ch4_a", "desk.teaser_ch4_b", "preview.incoming", "preview.next", "preview.broker", "preview.coming", "preview.thanks", "hint.continue" };
            string missKey = "";
            foreach (string k in keys) { if (!Loc.Has(k)) missKey += " " + k; }
            Check("W-047 strings: all 11 keys exist in the table" + missKey, missKey.Length == 0);
            Loc.Current = Language.Korean;
            string ko1 = Loc.T("desk.teaser_ch3_a"), ko2 = Loc.T("desk.teaser_ch4_b"), ko3 = Loc.T("preview.broker");
            Loc.Current = Language.English;
            Check("W-047 strings: Korean rows show Korean (" + ko1 + " / " + ko2 + " / " + ko3 + "), English stays English",
                ko1 == "도시 관리 타워" && ko2 == "기록이 지워진 다음." && ko3 == "브로커: 네 러너를 쫓는 기록이 거기 있다." && Loc.T("desk.teaser_ch3_a") == "CITY ADMIN TOWER");

            // ----- 바탕화면: 챕터 3·4 예고 칸 -----
            yield return ToMain();
            yield return Frames(4);
            if (view.MainIndex != 0) yield return Press("Up");
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "W-047 Desktop");
            yield return Press("Enter");
            yield return WaitUntil(() => view.PopupSettled, "W-047 popup settled");
            int t0 = view.Menus.TeaserDraws;   // 예고 칸은 Down을 누를 때 다시 그려지므로 이동 전에 센다
            for (int i = 0; i < 6 && view.FolderIndex != 3; i++) yield return Press("Down");
            yield return Frames(3);
            Check("W-047 desktop: CH3 teaser folder selected, teaser panel drawn (COMING SOON + 2 lines), no placeholder rows",
                view.FolderIndex == 3 && view.Menus.TeaserDraws > t0 && view.Menus.LastTeaserChapter == 3 && view.Menus.PlaceholderRows == 0 && !view.Desk.ChapterOpen(3));
            yield return Press("Enter");
            yield return Press("Right");
            Check("W-047 desktop: Enter and Right on the CH3 teaser folder are ignored (still chapter focus)", !view.InFolder && view.FolderIndex == 3 && view.CurrentPhase == GameView.Phase.Desktop);
            yield return Frames(2);
            CaptureW47("02-desk-ch3-teaser");
            yield return Frames(1);
            yield return Press("Down");
            yield return Frames(3);
            Check("W-047 desktop: CH4 teaser panel drawn (chapter 4)", view.FolderIndex == 4 && view.Menus.LastTeaserChapter == 4);
            yield return Press("Enter");
            Check("W-047 desktop: Enter on the CH4 teaser folder is ignored", !view.InFolder && view.FolderIndex == 4);

            // 한국어 예고 칸(캡처)
            Loc.Current = Language.Korean;
            view.Progress.Language = Language.Korean;
            yield return Press("Up");
            yield return Frames(4);
            CaptureW47("04-desk-ch3-teaser-ko");
            yield return Frames(1);
            Check("W-047 desktop (Korean): CH3 teaser panel drawn", view.FolderIndex == 3 && view.Menus.LastTeaserChapter == 3);
            Loc.Current = Language.English;
            view.Progress.Language = Language.English;

            // ----- 챕터 2: 2-4~2-6 예정 줄(챕터 2 의뢰 전까지 클리어한 기록으로 챕터 2를 연다) -----
            W047ClearAllExcept(-1);
            yield return ToMain();
            yield return Frames(4);
            if (view.MainIndex != 0) yield return Press("Up");
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "W-047 Desktop (ch2 open)");
            yield return Press("Enter");
            yield return WaitUntil(() => view.PopupSettled, "W-047 popup settled (ch2)");
            for (int i = 0; i < 6 && view.FolderIndex != 2; i++) yield return Press("Down");
            Check("W-047 ch2 is open when everything before it is cleared", view.FolderIndex == 2 && view.Desk.ChapterOpen(2));
            yield return Press("Right");
            Check("W-047 ch2 list opened in stage focus", view.InFolder && view.FolderIndex == 2);
            yield return Frames(3);
            Check("W-047 ch2 list: 3 placeholder rows drawn (REQ_2-4 .. REQ_2-6, dim + lock)", view.Menus.PlaceholderRows == 3);
            for (int i = 0; i < 8; i++) yield return Press("Down");
            Check("W-047 ch2 list: Down x8 never leaves the real requests (cursor only on 2-1 .. 2-3, never on 2-4 .. 2-6)", view.DeskIndex >= 9 && view.DeskIndex <= 11 && view.Desk.InChapter(2).Contains(view.DeskIndex));
            for (int i = 0; i < 3 && view.DeskIndex != 11; i++) yield return Press("Down");
            yield return WaitUntil(() => view.SlideSettled, "W-047 slide settled");
            yield return Frames(3);
            CaptureW47("01-desk-ch2-coming");
            yield return Frames(1);
            Check("W-047 ch2 list: the cursor is on 2-3 for the capture", view.DeskIndex == 11 && view.Menus.PlaceholderRows == 3);

            // ----- 2-3 첫 클리어 -> 결과 Enter -> 예고 화면 -> Enter x2 -> 바탕화면 -----
            W047ClearAllExcept(11);
            Check("W-047 flag: the chapter 3 preview was not seen yet after the record reset", !view.Progress.ChapterPreviewSeen(3));
            yield return W047ClearToEnded(11, "Ch2Fac_3.asset");
            Check("W-047 first clear of 2-3: result window (Ended, Complete)", view.CurrentPhase == GameView.Phase.Ended && Sim.Outcome == Outcome.Complete && view.Desk.IsCleared(11));
            yield return Press("Enter");
            Check("W-047 result Enter -> preview screen (not the desktop yet)", view.CurrentPhase == GameView.Phase.Preview);
            int pd0 = view.Menus.PreviewDraws;
            yield return Frames(3);
            Check("W-047 preview: drawn every frame, lines appear one by one (0.1 s each, 8 lines), not all shown at the start",
                view.Menus.PreviewDraws > pd0 && !view.PreviewRevealed);
            yield return WaitUntil(() => view.PreviewRevealed, "W-047 preview revealed", 600);
            yield return Frames(2);
            CaptureW47("03-preview-ch3");
            yield return Frames(1);
            Check("W-047 preview: all lines shown after 0.8 s, phase stays Preview until Enter (seen flag not saved yet)", view.CurrentPhase == GameView.Phase.Preview && !view.Progress.ChapterPreviewSeen(3));
            yield return Press("Enter");
            Check("W-047 preview: Enter -> desktop, seen flag saved, ch2 list (stage focus) as after any result", view.CurrentPhase == GameView.Phase.Desktop && view.Progress.ChapterPreviewSeen(3) && view.InFolder && view.FolderIndex == 2);

            // 첫 Enter는 모든 줄을 한 번에 보여 준다(기록을 다시 지우고 같은 흐름)
            W047ClearAllExcept(11);
            yield return W047ClearToEnded(11, "Ch2Fac_3.asset");
            yield return Press("Enter");
            Check("W-047 preview again after a record reset: Preview phase", view.CurrentPhase == GameView.Phase.Preview);
            yield return Press("Enter");
            Check("W-047 preview: the first Enter shows every line at once and stays on the preview", view.CurrentPhase == GameView.Phase.Preview && view.PreviewRevealed);
            yield return Press("Enter");
            Check("W-047 preview: the second Enter goes to the desktop", view.CurrentPhase == GameView.Phase.Desktop && view.Progress.ChapterPreviewSeen(3));

            // 두 번째 클리어에서는 예고 없이 바탕화면
            yield return W047ClearToEnded(11, "Ch2Fac_3.asset");
            yield return Press("Enter");
            Check("W-047 second clear of 2-3: no preview, Enter goes straight to the desktop", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder);

            // 다른 의뢰(2-2) 클리어는 예고를 띄우지 않는다
            view.Progress.Clear(AllIds());
            yield return W047ClearToEnded(10, "Ch2Fac_2.asset");
            yield return Press("Enter");
            Check("W-047 clearing 2-2 does not show the preview", view.CurrentPhase == GameView.Phase.Desktop);

            view.Progress.Clear(AllIds());
            yield return ToMain();
            yield return Frames(2);
        }

        // ===================================================================================================
        // W-041: 인물 그림 반입(D-127·D-128) — 해커 초상 v2, 러너 초상(대화 중 화자 전환), 러너 128px 시트 v2, 메인 화면 v4
        // ===================================================================================================

        static string CaptureDir41 => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/W-041"));

        static void CaptureW41(string name)
        {
            Directory.CreateDirectory(CaptureDir41);
            view.Canvas.SaveSizedPng(Path.Combine(CaptureDir41, name + "_1920x1080.png"), 1920, 1080);
        }

        /// <summary>대화를 직접 시작한다(바탕화면 경로 없이 화면 확인): 지금 sim이 있는 상태에서 BeginDialogue를 부른다.</summary>
        static void StartDialogue(Dt.DialogueLine[] lines)
        {
            typeof(GameView).GetMethod("BeginDialogue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(view, new object[] { GameView.Phase.Briefing, lines });
        }

        static IEnumerator W041Checks()
        {
            Tutorial.AutoPass = true;
            view.Progress.EnvFxReduced = false;
            string[] faces = { "neutral", "disgust", "joy", "surprise", "fear", "sadness", "anger" };
            string missH = "", missR = "";
            foreach (string f in faces)
            {
                if (!view.Canvas.Has("portraits/hacker_v4_x6_" + f)) missH += " " + f;
                if (!view.Canvas.Has("portraits/runner_v4_x6_" + f)) missR += " " + f;
            }
            Check("W-041 art set: hacker v4_x6 portraits (7 faces) and runner_v4_x6 portraits (7 faces) are imported, the old hacker_* pictures are still there (reference swap only)" + (missH + missR),
                missH.Length == 0 && missR.Length == 0 && view.Canvas.Has("portraits/hacker_neutral"));
            Check("W-041 portrait size is the same for v3, runner and the old hacker portrait",
                view.Canvas.WidthOf("portraits/hacker_v4_x6_joy") == view.Canvas.WidthOf("portraits/hacker_joy") && view.Canvas.HeightOf("portraits/hacker_v4_x6_joy") == view.Canvas.HeightOf("portraits/hacker_joy")
                && view.Canvas.WidthOf("portraits/runner_v4_x6_joy") == view.Canvas.WidthOf("portraits/hacker_joy") && view.Canvas.HeightOf("portraits/runner_v4_x6_joy") == view.Canvas.HeightOf("portraits/hacker_joy"));
            Check("W-041 runner v4 (W-044 redraw) sheets present with the same frame counts as the old ones (W-044 PixelLab sheets runner64b_x2: walk 8 / wait 8 / fall 8 frames, right and left; holds keep the 30 FPS period: 16 / 24 / 10 ticks)",
                view.Canvas.FrameCount("anim/runner64b_x2_walk_sheet") == 8 && view.Canvas.FrameCount("anim/runner64b_x2_wait_sheet") == 8 && view.Canvas.FrameCount("anim/runner64b_x2_fall_sheet") == 8
                && view.Canvas.FrameCount("anim/runner64b_x2_walk_left_sheet") == 8 && view.Canvas.FrameCount("anim/runner64b_x2_wait_left_sheet") == 8 && view.Canvas.FrameCount("anim/runner64b_x2_fall_left_sheet") == 8 && Mathf.Abs(view.Canvas.SheetSeconds("anim/runner64b_x2_walk_sheet") - 16f / 30f) < 0.001f && Mathf.Abs(view.Canvas.SheetSeconds("anim/runner64b_x2_wait_sheet") - 24f / 30f) < 0.001f && Mathf.Abs(view.Canvas.SheetSeconds("anim/runner64b_x2_fall_sheet") - 10f / 30f) < 0.001f);
            Check("W-041 main background art: scenes/main_lobby_v6_x8 is imported at full canvas size (" + view.Canvas.WidthOf("scenes/main_lobby_v6_x8") + "x" + view.Canvas.HeightOf("scenes/main_lobby_v6_x8") + " logical = 640x360 drawn 8x from 320x180)",
                view.Canvas.Has("scenes/main_lobby_v6_x8") && view.Canvas.WidthOf("scenes/main_lobby_v6_x8") == PixelCanvas.W && view.Canvas.HeightOf("scenes/main_lobby_v6_x8") == PixelCanvas.H);

            // ----- 메인 화면(캡처) -----
            yield return ToMain();
            yield return Frames(6);
            Check("W-041 main screen is shown (v5 background, logo and menu drawn on top)", view.CurrentPhase == GameView.Phase.Main && view.MainItems.Count >= 3);
            CaptureW41("01-main");
            yield return Frames(1);

            // ----- 1-1 브리핑: BROKER(해커 초상) -> RUNNER(러너 초상) -> ME(해커 초상) -----
            yield return StartLevelFor(2, "Ch1Old_1.asset");
            Check("W-041 outside a dialogue the portrait is the hacker (FaceIsRunner false)", !view.FaceIsRunner);
            StartDialogue(view.Desk.Entry(2).dialogue.briefing);
            yield return Frames(4);
            Check("W-041 1-1 line 1 (BROKER): hacker portrait, no switch", view.CurrentPhase == GameView.Phase.Briefing && !view.FaceIsRunner && view.FaceKey == "neutral");
            yield return Press("Enter");
            yield return Frames(30);
            Check("W-041 1-1 line 2 (RUNNER): the face panel switches to the runner portrait (runner_v4_x6_" + view.FaceKey + ") and the RUNNER name tag", view.FaceIsRunner && view.FaceKey == "neutral");
            CaptureW41("02-1-1-runner-line");
            yield return Frames(1);
            yield return Press("Enter");
            yield return Frames(30);
            Check("W-041 1-1 line 3 (ME): back to the hacker portrait with its own face (disgust)", !view.FaceIsRunner && view.FaceKey == "disgust");
            CaptureW41("03-1-1-hacker-line");
            yield return Frames(1);

            // 챕터 0의 SYSTEM(해커 유지)과 ???(침입자 = 러너)
            yield return StartLevelFor(0, "Ch0Home_1.asset");
            StartDialogue(view.Desk.Entry(0).dialogue.briefing);
            yield return Frames(3);
            bool sys = !view.FaceIsRunner;
            yield return Press("Enter");
            yield return Frames(3);
            Check("W-041 chapter 0: SYSTEM and ME lines keep the hacker portrait (no switch)", sys && !view.FaceIsRunner);
            StartDialogue(view.Desk.Entry(0).dialogue.ending);
            yield return Frames(3);
            Check("W-041 chapter 0: the ??? line (the intruder = the runner) switches to the runner portrait", view.FaceIsRunner);

            // ----- 2-1 러너 걷기(v2 시트) -----
            yield return StartLevelFor(9, "Ch2Fac_1.asset");
            yield return WaitUntil(() => Sim.TargetMoving, "W-041 2-1 runner walking", 1500);
            yield return Frames(20);
            Check("W-041 2-1 runner walks with the runner64b_x2 sheet (anim/runner64b_x2_walk_sheet)", view.Renderer.RunnerSheet == "anim/runner64b_x2_walk_sheet" && view.Renderer.RunnerAnim == "walk");
            CaptureW41("04-2-1-runner-walk");
            yield return Frames(1);

            // ----- 한국어 대화 1장(러너 줄) -----
            Loc.Current = Language.Korean;
            view.Progress.Language = Language.Korean;
            yield return StartLevelFor(2, "Ch1Old_1.asset");
            StartDialogue(view.Desk.Entry(2).dialogue.briefing);
            yield return Press("Enter");
            yield return Frames(30);
            Check("W-041 Korean dialogue: the runner line shows the runner portrait; the name tag is the Korean runner name " + Loc.Tr("RUNNER"), view.FaceIsRunner && Loc.Tr("RUNNER") != "RUNNER");
            CaptureW41("05-ko-runner-line");
            yield return Frames(1);
            Loc.Current = Language.English;
            view.Progress.Language = Language.English;
            yield return ToMain();
            yield return Frames(2);
        }

        static IEnumerator Run()
        {
            yield return null;
            // W-034(D-124): 저장 데이터가 하나도 없는 첫 실행 = 진행 0, 설정 기본값, 영어. 테스트 전용 저장 공간이 비어 있는 상태로 시작한다
            Check("W-034 first run: the smoke uses the test-only store (not the user's real save)", view.Progress.IsTestStore && EditorPrefs.GetBool(ProgressStore.TestStoreFlag, false));
            Check("W-034 first run: no record (0-1 only open), CONTINUE hidden, language ENGLISH, settings at defaults",
                !view.Progress.HasAnyProgress(AllIds()) && Loc.Current == Language.English && view.Progress.Language == Language.English
                && view.Progress.BgmVolume == ProgressStore.VolumeDefault && view.Progress.SfxVolume == ProgressStore.VolumeDefault
                && !view.Progress.EnvFxReduced && view.Progress.GlitchSetting == GlitchStrength.Normal
                && view.Desk.IsUnlocked(0) && !view.Desk.IsUnlocked(1) && !view.Desk.ChapterOpen(1) && view.MainItems.Count == 3 && view.MainItems[0] == "NEW GAME");
            SnapshotPrefs();   // W-019: 끝날 때 되돌릴 사용자 값을 먼저 저장
            Tutorial.AutoPass = true;   // W-033: 튜토리얼 멈춤 없이 기존 확인을 진행한다(튜토리얼 자체는 W033Checks가 실제 입력으로 확인)
            ClearProgress();
            view.Progress.BgmVolume = 7; view.Progress.SfxVolume = 7; view.Progress.EnvFxReduced = false;
            view.Progress.GlitchSetting = GlitchStrength.Normal; Glitch.Strength = GlitchStrength.Normal;   // 기본 설정에서 시작
            PureChecks();
            Ch0PureChecks();
            W14PureChecks();
            W16PureChecks();
            W14RouteChecks();
            W029RouteChecks();   // W-029: 1-3·1-4 자동 경로
            W030PureChecks();    // W-030: 장치 이용 사건(F3) 판정
            W031PureChecks();    // W-031: 층·열 수, 추적도 배율(D-120)
            W031RouteChecks();   // W-031: 새 배치의 자동 경로(최적·라이트)
            W035PureChecks();    // W-035: 박자 시계·Shift + 숫자·프레스(시간 위상 어긋남)
            W035RouteChecks();   // W-035: TEST_2-0 자동 경로(숫자 맞춤·아무것도 안 함)
            W036PureChecks();    // W-036: 새 1-4·1-5 데이터·§2 규칙(밀린·맞바뀐 방의 연결 작동)
            W036RouteChecks();   // W-036: 새 1-4·1-5와 1-6의 자동 경로(최적·라이트)
            W038PureChecks();    // W-038: 챕터 2 정식 의뢰 데이터·주기 레이저 규칙
            W038RouteChecks();   // W-038: 2-1~2-3 자동 경로(최적·라이트)
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
            Check("Booting -> Desktop shows the icon screen (popup closed, NORMAL MODE selected, CH0 first)",
                view.CurrentPhase == GameView.Phase.Desktop && !view.PopupOpen && !view.InFolder && view.DeskIcon == 0 && view.FolderIndex == 0 && view.DeskIndex == 0);
            Check("3 chapter folders: CH0 open, CH1 locked; W-038: CH2 locked too (opens after chapter 1)", view.Desk.ChapterOpen(0) && !view.Desk.ChapterOpen(1) && !view.Desk.ChapterOpen(2));
            Check("folder names CH0_HOME / CH1_??? / CH2_???", RequestBoard.ChapterName(0) == "CH0_HOME" && RequestBoard.ChapterName(1) == "CH1_???" && RequestBoard.ChapterName(2) == "CH2_???");
            Check("request list: chapter 0 requests 0-1 and 0-2 first (test level removed); W-014 adds chapter 1 requests 1-1 and 1-2, W-029 adds 1-3 after them; W-036 adds 1-4, 1-5 and moves OLD TOWN E to 1-6; W-038: hidden TEST_2-0 at index 8, then 2-1 .. 2-3", view.Desk.Count == 12 && view.Desk.Number(0) == "0-1" && view.Desk.Number(1) == "0-2"
                && view.Desk.Entry(0).chapter == 0 && view.Desk.Entry(1).chapter == 0 && view.Desk.Number(2) == "1-1" && view.Desk.Number(3) == "1-2" && view.Desk.Entry(2).chapter == 1 && view.Desk.Entry(3).chapter == 1
                && view.Desk.Number(4) == "1-3" && view.Desk.Number(5) == "1-4" && view.Desk.Number(6) == "1-5" && view.Desk.Number(7) == "1-6" && view.Desk.Entry(4).chapter == 1 && view.Desk.Entry(5).chapter == 1 && view.Desk.Entry(6).chapter == 1 && view.Desk.Entry(7).chapter == 1);
            yield return Frames(3);
            CaptureW32("01-desktop-icons");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            // W-032: 개발 모드 자리 = LOCKED만, 아무것도 열지 않음
            int u04d = Req("U04");
            yield return Press("Down");
            Check("W-032 Down selects the dev mode icon (2 icons)", view.DeskIcon == 1);
            yield return Press("Enter");
            Check("W-032 Enter on dev mode icon -> LOCKED shown only (no popup, phase unchanged, U04 +1)", view.DevLockedShown && !view.PopupOpen && view.CurrentPhase == GameView.Phase.Desktop && Req("U04") == u04d + 1);
            yield return Frames(3);
            CaptureW32("02-dev-locked");
            yield return Frames(1);
            yield return WaitUntil(() => !view.DevLockedShown, "dev LOCKED text gone (0.6 s)");
            yield return Press("Down");
            Check("W-032 Down wraps back to NORMAL MODE", view.DeskIcon == 0);

            // 일반 모드 팝업: 챕터 포커스, 잠긴 챕터
            yield return Press("Enter");
            Check("W-032 Enter on NORMAL MODE -> popup opens with chapter focus (CH0, first stage selected)", view.PopupOpen && !view.InFolder && view.FolderIndex == 0 && view.DeskIndex == 0 && view.CurrentPhase == GameView.Phase.Desktop);
            yield return WaitUntil(() => view.PopupSettled, "popup settled");
            yield return Frames(2);
            CaptureW32("03-popup-chapter0");
            yield return Frames(1);
            yield return Press("Down");
            Check("W-032 Down on chapter list selects CH1 (right list switches to CH1 requests, 1-1 selected)", view.FolderIndex == 1 && !view.InFolder && view.DeskIndex == 2);
            yield return Press("Enter");
            Check("W-032 Enter on a locked chapter is ignored (focus stays on chapters)", !view.InFolder && view.FolderIndex == 1);
            yield return Press("Right");
            Check("W-032 Right on a locked chapter is ignored", !view.InFolder && view.FolderIndex == 1);
            yield return Frames(2);
            CaptureW32("04-popup-locked-chapter");
            yield return Frames(1);
            yield return Press("Down");
            Check("W-032 Down to CH2 (W-038: locked until chapter 1 is cleared)", view.FolderIndex == 2 && !view.Desk.ChapterOpen(2));
            yield return Press("Enter");
            Check("W-038 Enter on locked CH2 is ignored (TEST_2-0 is not in the popup list)", !view.InFolder && view.FolderIndex == 2 && view.CurrentPhase == GameView.Phase.Desktop);
            yield return Press("Down");
            Check("W-047 Down to CH3 (teaser folder, no requests, locked), Enter is ignored", view.FolderIndex == 3 && !view.Desk.ChapterOpen(3) && !view.InFolder);
            yield return Press("Enter");
            Check("W-047 Enter on the CH3 teaser folder stays on the chapter list", view.FolderIndex == 3 && !view.InFolder && view.CurrentPhase == GameView.Phase.Desktop);
            yield return Press("Down");
            Check("W-047 Down to CH4 (teaser folder)", view.FolderIndex == 4 && !view.Desk.ChapterOpen(4));
            yield return Press("Down");
            Check("W-032 Down wraps to CH0 (W-047: after CH4)", view.FolderIndex == 0 && view.DeskIndex == 0);
            yield return Press("Esc");
            Check("W-032 Esc on chapter list closes the popup (icon screen)", !view.PopupOpen && view.CurrentPhase == GameView.Phase.Desktop);
            yield return Press("Enter");
            Check("W-032 popup reopens at the last opened chapter (CH0)", view.PopupOpen && view.FolderIndex == 0);
            yield return Press("Esc");
            yield return Press("Esc");
            Check("W-032 Esc on the icon screen -> Main", view.CurrentPhase == GameView.Phase.Main);
            yield return Press("Enter");
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "Desktop again");
            Check("W-032 back on the icon screen with the popup closed", !view.PopupOpen && !view.InFolder && view.DeskIcon == 0 && view.FolderIndex == 0);

            // 스테이지 포커스: [스테이지 | 상세] 두 열
            yield return Press("Enter");
            yield return Press("Right");
            Check("W-032 Right on CH0 -> stage focus (REQ_0-1 selected)", view.InFolder && view.FolderIndex == 0 && view.DeskIndex == 0);
            Check("list names REQ_0-1 / REQ_0-2", view.Desk.RequestName(0) == "REQ_0-1" && view.Desk.RequestName(1) == "REQ_0-2");
            Check("REQ_0-1 playable (NEW), REQ_0-2 locked until 0-1 clears", view.Desk.State(0) == RequestState.New && view.Desk.State(1) == RequestState.Locked);
            yield return WaitUntil(() => view.SlideSettled, "column slide settled");
            yield return Frames(2);
            CaptureW32("05-popup-stage-focus-detail");
            yield return Frames(1);
            yield return Press("Down");
            Check("W-032 Down selects REQ_0-2 (locked: detail shows LOCKED only)", view.DeskIndex == 1 && view.Desk.State(1) == RequestState.Locked);
            yield return Frames(2);
            CaptureW32("06-popup-locked-stage-detail");
            yield return Frames(1);
            yield return Press("Enter");
            Check("W-032 Enter on a locked stage does not connect", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder);
            yield return Press("Left");
            Check("W-032 Left -> back to [chapters | stages] (chapter focus)", view.PopupOpen && !view.InFolder);
            yield return Press("Enter");
            Check("W-032 Enter on the chapter -> stage focus again", view.InFolder);
            yield return Press("Esc");
            Check("W-032 Esc in stage focus -> chapter focus (popup still open)", view.PopupOpen && !view.InFolder);
            yield return Press("Enter");
            yield return Press("Up");
            Check("W-032 Up selects REQ_0-1", view.DeskIndex == 0);

            // Enter 한 번 = 접속, 접속 중 Esc = 취소
            yield return Press("Enter");
            Check("W-032 Enter on a playable stage -> Connecting at once (no detail panel step)", view.CurrentPhase == GameView.Phase.Connecting);
            yield return Frames(4);
            CaptureW32("07-connecting");
            yield return Frames(1);
            yield return Press("Esc");
            Check("W-032 Esc while connecting cancels -> popup with stage focus, REQ_0-1 still selected", view.CurrentPhase == GameView.Phase.Desktop && view.PopupOpen && view.InFolder && view.DeskIndex == 0);

            // ===== 의뢰 0-1 NIGHT INTRUDER =====
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
            Check("W-033 0-1 has 6 tutorial steps (auto-pass on in this flow, steps are played in W033Checks)", view.Tut.StepCount == 6 && !view.Tut.Frozen);
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
                !Sim.TargetMoving && view.Renderer.RunnerAnim == "wait" && view.Renderer.RunnerSheet == "anim/runner64b_x2_wait_sheet");
            yield return WaitUntil(() => Sim.TargetMoving, "runner starts walking (start delay 3 s)", 3000);   // W-028: 프레임이 빠른 기기(약 4 ms)에서 600프레임은 3초보다 짧아 늘림
            yield return Jump(Dir.Left, 2, 1);   // 카메라가 조준한 섹터를 따라가므로, 걷는 모습이 장면에 들어오게 해치 섹터로 조준을 옮긴다
            yield return WaitUntil(() => Sim.TargetX >= 300f, "runner walks into the camera view", 3000);
            yield return Frames(3);
            Check("W-009 0-1 runner walks: walk animation, right-facing sheet, wait -> walk switched without delaying the judgement",
                Sim.TargetMoving && view.Renderer.RunnerAnim == "walk" && view.Renderer.RunnerSheet == "anim/runner64b_x2_walk_sheet");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            // 계단을 열어 둔 채 두면: 2층 진입 -> 실패 지점 -> 바로 다시 시작
            Advance(() => Sim.TargetFloor == 0, 40f);
            yield return Frames(3);
            Check("0-1 runner climbed the open stairs to 2F: no mission failed, still running", Sim.TargetFloor == 0 && !Sim.MissionFailed(0) && !Sim.MissionDone(0) && Sim.Outcome == Outcome.Running);
            Capture("21-0-1-2f-climbed");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            Advance(() => Sim.Outcome == Outcome.Failed, 30f);
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Playing && Sim.Outcome == Outcome.Running, "W-028 fail blink (0.4 s) then restart", 300);   // 1-9: 재시작 전에 빨간 사각형 깜빡임
            yield return Frames(3);
            double tNotice = EditorApplication.timeSinceStartup;
            Check("0-1 fail point: restarts at once - Playing, no result window, no briefing (log kept), floor 1 x190, aim (3,1), missions reset",
                view.CurrentPhase == GameView.Phase.Playing && Sim.Outcome == Outcome.Running && Sim.TargetFloor == 1 && Mathf.Approximately(Sim.TargetX, 190f) && Sim.Aim == P(3, 1)
                && !Sim.MissionFailed(0) && view.Shown.Count == 2 && view.CurrentTab == GameView.Tab.Mission);
            Check("0-1 restart notice in the guide box: 'THEY REACHED YOU. TRY AGAIN.' (no badge)", view.Tut.NoticeActive && view.Tut.Text(Sim) == "THEY REACHED YOU. TRY AGAIN.");
            Capture("22-0-1-restart-notice");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            yield return WaitUntil(() => !view.Tut.NoticeActive, "restart notice ends", 900);
            double noticeSeconds = EditorApplication.timeSinceStartup - tNotice;
            Check("0-1 restart notice lasts about 2 s (measured from 3 frames after the restart: " + noticeSeconds.ToString("0.00") + " s) and then step 1 returns",
                noticeSeconds > 1.3 && noticeSeconds < 3.5);

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
            // W-033: 이 단계 확인은 W033Checks(실제 멈춤·입력)로 옮겼다
            shift = false;
            yield return Frames(3);
            // W-033: 이 단계 확인은 W033Checks(실제 멈춤·입력)로 옮겼다
            Capture("23-0-1-step2");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다
            Advance(() => !Sim.TargetFacingRight, 30f);
            yield return Frames(3);
            // W-033: 이 단계 확인은 W033Checks(실제 멈춤·입력)로 옮겼다
            Check("W-009 left-facing runner uses the walk_left sheet", Sim.TargetMoving && view.Renderer.RunnerAnim == "walk" && view.Renderer.RunnerSheet == "anim/runner64b_x2_walk_left_sheet");
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
            // W-033: 이 단계 확인은 W033Checks(실제 멈춤·입력)로 옮겼다
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
            Check("result Enter -> request list of CH0 (folder open, REQ_0-1 selected)", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder && view.FolderIndex == 0 && view.DeskIndex == 0);
            Check("0-1 cleared: no stars saved (best 0), cleared flag set, REQ_0-1 CLEAR", view.Desk.Best(0) == 0 && view.Desk.IsCleared(0) && view.Desk.IsTutorial(0) && view.Desk.State(0) == RequestState.Clear);
            Check("CH1 stays locked while REQ_0-2 is not cleared", !view.Desk.ChapterOpen(1));
            Check("0-1 cleared -> REQ_0-2 opens (playable, NEW)", view.Desk.IsUnlocked(1) && view.Desk.IsPlayable(1) && view.Desk.State(1) == RequestState.New);
            Check("CH1 still locked; CH2 locked too (W-038)", !view.Desk.ChapterOpen(1) && !view.Desk.ChapterOpen(2));
            yield return Frames(3);
            Capture("28-request-list-after-clear");
            yield return Frames(1);   // 스크린샷은 프레임 끝에 찍히므로 그 사이 sim을 더 진행하지 않는다

            // ===== 의뢰 0-2 LOCKDOWN =====
            yield return Press("Down");
            Check("Down selects REQ_0-2", view.DeskIndex == 1);
            yield return OpenAndConnect();
            Check("0-2 briefing log: ??? line + ME line", view.Shown.Count == 2 && view.Shown[0].speakerName == "???" && view.Shown[0].text == "I'M OUT. WHOEVER YOU ARE, I'M NOT STAYING."
                && view.Shown[1].speaker == GameOff.Data.Speaker.Me && view.Shown[1].text == "WE'LL SEE.");
            Check("0-2 playing, tutorial step 1 (no badge): 'THE ELEVATOR GOES UP TO YOU...'", view.CurrentPhase == GameView.Phase.Playing && view.Tut.StepCount == 5);
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
            Check("0-2 waits before the laser (x~311); step 2 'SOME ROOMS SHIFT AS A WHOLE...' (no badge)", Sim.TargetWaiting && Mathf.Abs(Sim.TargetX - 311f) < 0.5f);
            Check("W-009 0-2 waiting before the laser: wait animation (not walk)", Sim.TargetWaiting && !Sim.TargetMoving && view.Renderer.RunnerAnim == "wait" && view.Renderer.RunnerSheet == "anim/runner64b_x2_wait_sheet");
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
            Check("0-2 relay used: tracking lowered; step 3 'RELAYS LOWER YOUR TRACE.'", Sim.RelayUsed && Sim.Tracking < trBefore);
            yield return Jump(Dir.Down, 2, 2);                                       // W-031: 레이저 방 바로 아래 1F 복도(해치)
            Advance(() => Sim.TargetFloor == 2 && Sim.TargetX <= 420f, 30f);          // 계단 2F -> 1F 뒤 왼쪽으로 걸어 해치(맵 x400) 앞
            shift = true;
            Advance(() => Sim.Outcome != Outcome.Running, 5f);
            shift = false;
            Check("0-2 hatch opened (1F) -> B1 boiler room -> Complete",
                Sim.Outcome == Outcome.Complete && Sim.TargetFloor == 3 && Sim.MissionFinal);
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
            Check("0-1 replay -> Playing, tracking 0, step 1", view.CurrentPhase == GameView.Phase.Playing && Sim.Tracking == 0f);
            shift = true;
            Advance(() => Sim.TraceMaxed, 20f);
            Check("tracking reaches max", Sim.TraceMaxed && Sim.Tracking >= 100f);
            yield return Frames(3);
            Check("tracking max: warning text replaces the step text, no badge", view.Tut.Text(Sim) == "TRACE MAX - SANDBOX ONLY.");
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
                && Mathf.Approximately(Sim.TargetX, 190f) && !Sim.ShiftReleasedOnce);
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
            Check("pause DESKTOP -> request list of CH0 (REQ_0-1)", view.CurrentPhase == GameView.Phase.Desktop && view.InFolder && view.FolderIndex == 0 && view.DeskIndex == 0);
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
            yield return W020Checks();   // W-020: 소리 연결 틀
            yield return W028Checks();   // W-028: P1 코드 연출·챕터 1 대사·게임 시작 CRT 켜짐
            yield return W029Checks();   // W-029: 챕터 1 의뢰 1-3·1-4 목록·대화·안내·캡처
            yield return W030Checks();   // W-030: 조작 피드백(표지·효과 없음·작동 링·기록)
            yield return W031Checks();   // W-031: 새 층 구조 화면 확인·캡처
            yield return W033Checks();   // W-033: 장치 그림 보강·조작 튜토리얼
            yield return W034Checks();   // W-034: 언어 설정·문구 표·저장
            yield return W035Checks();   // W-035: 챕터 2 시험 맵 화면 확인·캡처
            yield return W036Checks();   // W-036: 챕터 1 6스테이지 목록·잠금·새 의뢰 화면 확인·캡처
            yield return W038Checks();   // W-038: 챕터 2 정식 의뢰 목록·튜토리얼·대화·캡처
            yield return W041Checks();   // W-041: 인물 그림(해커 v2·러너 초상·러너 시트 v2·메인 v4)
            yield return W047Checks();   // W-047: 챕터 3·4 예고 폴더·챕터 2 예정 줄·챕터 끝 예고 화면

            Finish();
        }
    }
}
