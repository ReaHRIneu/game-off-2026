using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using GameOff.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Dt = GameOff.Data;

namespace GameOff.EditorTools
{
    /// <summary>
    /// W-048 S7 통합 QA 중 Editor에서 자동으로 확인할 수 있는 항목(test-backlog의 T 항목). 메뉴: GameOff/Run QA Checks (S7).
    /// 스모크(Mvp1SmokeCheck)와 같은 실행 틀(코루틴 실행기·Check·Note·Play 모드 진입·종료·테스트 전용 저장 공간·입력 포커스 설정)을 그대로 쓰려고
    /// 같은 클래스의 두 번째 파일(partial)로 둔다. 결과는 Unity/Logs/qa-s7.txt, 한 줄에 PASS|FAIL|INFO T-xxx 설명. 캡처는 Docs/QA/captures/S7/.
    /// 게임 코드(판정·화면)는 건드리지 않는다. 비공개 값은 스모크와 같은 방식(reflection)으로 읽고 바꾼다.
    /// </summary>
    public static partial class Mvp1SmokeCheck
    {
        internal const string QaPrefKey = "GameOff.QaS7";
        const string QaInputSnapKey = "GameOff.QaS7.InputSnap";
        const string QaT076PendingKey = "GameOff.QaS7.T076Pending";
        static bool qaMode;

        [MenuItem("GameOff/Run QA Checks (S7)")]
        public static void QaStart()
        {
            // T-076: 실행 전 입력 설정을 저장해 둔다(Play 모드 진입·종료로 도메인이 다시 로드되므로 EditorPrefs에 둔다)
            EditorPrefs.SetString(QaInputSnapKey, QaInputSnapshot());
            EditorPrefs.SetBool(QaT076PendingKey, true);
            EditorPrefs.SetBool(QaPrefKey, true);
            Start();   // 스모크와 같은 진입: 테스트 전용 저장 공간 켜기, 씬 열기, Play 모드 진입
        }

        [MenuItem("GameOff/Check Input Settings Restored (T-076)")]
        public static void QaCheckInputRestored()
        {
            QaReportT076(" (manual re-check, e.g. after the smoke)");
        }

        // ----- T-076: Editor 입력 설정 -----

        static string QaInputSnapshot()
        {
            InputSettings s = InputSystem.settings;
            if (s == null) return "none";
            string path = AssetDatabase.GetAssetPath(s);
            string hash = "-";
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    using (var md5 = MD5.Create())
                        hash = BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "../" + path)))));
                }
                catch (Exception) { hash = "unreadable"; }
            }
            return s.backgroundBehavior + "|" + s.editorInputBehaviorInPlayMode + "|" + path + "|" + hash;
        }

        internal static void QaReportT076(string suffix)
        {
            string before = EditorPrefs.GetString(QaInputSnapKey, "");
            string now = QaInputSnapshot();
            string line;
            if (before.Length == 0) line = "INFO T-076 no snapshot from before the QA run (run GameOff/Run QA Checks (S7) first)" + suffix;
            else if (before == now) line = "PASS T-076 Editor input settings (background behavior | editor input in play mode | asset path | asset file hash) are the same as before the run: " + now + suffix;
            else line = "FAIL T-076 Editor input settings changed. before: " + before + " | now: " + now + suffix;
            Debug.Log("[QA] " + line);
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/qa-s7.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception e) { Debug.LogWarning("[QA] could not append T-076 line: " + e.Message); }
        }

        // ----- 도우미 -----

        static void Q(string id, bool ok, string text) => Check(id + " " + text, ok);
        static void QNote(string id, string text) => Note(id + " " + text);

        static string QaCaptureDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/QA/captures/S7"));
        const string QaRoot = "Assets/_Project/Data/";

        static void QaSetProp(GameSim s, string name, object v) => typeof(GameSim).GetProperty(name).GetSetMethod(true).Invoke(s, new[] { v });

        static Dt.GameSettings QaSettings => AssetDatabase.LoadAssetAtPath<Dt.GameSettings>(QaRoot + "GameSettings.asset");

        /// <summary>일정한 간격 dt로 sim을 진행하는 도우미(T-023·T-024).</summary>
        sealed class QaDrive
        {
            public GameSim S;
            public float Dt = 1f / 60f;
            public bool Shift;
            public float Time;

            public bool Until(Func<bool> cond, float maxSec)
            {
                float t = 0f;
                while (t < maxSec)
                {
                    if (cond()) return true;
                    S.ShiftHeld = Shift;
                    S.Step(Dt);
                    t += Dt;
                    Time += Dt;
                    if (S.Outcome != Outcome.Running) break;
                }
                return cond();
            }

            public void Once()
            {
                S.ShiftHeld = Shift;
                S.Step(Dt);
                Time += Dt;
            }
        }

        static Dt.LevelData QaBaseLevel(int floors, int cols)
        {
            var l = ScriptableObject.CreateInstance<Dt.LevelData>();
            l.floors = floors; l.cols = cols; l.sectorWidth = 160; l.sectorHeight = 96; l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.sectors = new Dt.SectorDef[0];
            l.startFloor = 0; l.startCol = 0; l.startX = 40; l.startFacingRight = true;
            l.missions = new[] { new Dt.MissionDef { kind = Dt.MissionKind.PassObject } };
            return l;
        }

        // ===================================================================================================
        // 실행 순서
        // ===================================================================================================

        static IEnumerator QaRun()
        {
            yield return null;
            Check("T-000 QA run uses the test-only store (not the user's real save)", view.Progress.IsTestStore && EditorPrefs.GetBool(ProgressStore.TestStoreFlag, false));
            SnapshotPrefs();
            Tutorial.AutoPass = true;
            ClearProgress();
            view.Progress.ClearAll(AllIds());
            Loc.Current = Language.English;
            view.Progress.BgmVolume = 7; view.Progress.SfxVolume = 7; view.Progress.EnvFxReduced = false;
            view.Progress.GlitchSetting = GlitchStrength.Normal; Glitch.Strength = GlitchStrength.Normal;

            // 화면 없이 규칙만(프레임을 쓰지 않음)
            QaAimChecks();
            QaAimTieSynthetic();
            QaCountdownSameTick();
            QaRelayOnce();
            QaBigFrameStep();
            QaFontChecks();
            QaTextureChecks();
            QaT015();

            // 화면이 필요한 것
            yield return QaCaptures();
            yield return QaBestStarsKept();
            yield return QaDarkSector();

            QNote("T-076", "the input settings are compared after the Play mode exits (a line is appended to this file); run GameOff/Check Input Settings Restored (T-076) again after the smoke");
            Finish();
        }

        // ===================================================================================================
        // T-008 · T-022 조준 점프 규칙
        // ===================================================================================================

        sealed class QaAimStat { public int Cases, Multi, ScoreTie, AxisTie, None, Bad; public string FirstBad = ""; }

        /// <summary>
        /// 규칙을 GameSim과 따로 계산한다: 누른 방향으로 진행 축 거리가 양수인 조작 가능한 섹터를 모두 모아
        /// (점수 = 진행 축 + 가중치 x 옆 축)를 만든 뒤, 최소 점수 -> 같은 점수 안에서 최소 진행 축 거리 순으로 고른다. 후보가 없으면 빈 목록.
        /// </summary>
        static List<Vector2Int> QaExpectedAim(Board b, float weight, Vector2Int aim, int dx, int dy, out int candidates, out bool scoreTie, out bool axisTie)
        {
            var cand = new List<(Vector2Int cell, float score, int axis)>();
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++)
                {
                    if (!b.IsOperable(f, c)) continue;
                    int axis = dx != 0 ? (c - aim.x) * dx : (f - aim.y) * dy;
                    if (axis <= 0) continue;
                    int side = dx != 0 ? Mathf.Abs(f - aim.y) : Mathf.Abs(c - aim.x);
                    cand.Add((new Vector2Int(c, f), axis + weight * side, axis));
                }
            candidates = cand.Count;
            scoreTie = axisTie = false;
            var result = new List<Vector2Int>();
            if (cand.Count == 0) return result;
            float minScore = float.MaxValue;
            foreach (var t in cand) if (t.score < minScore) minScore = t.score;
            var atMin = cand.FindAll(t => Mathf.Abs(t.score - minScore) < 1e-4f);
            int minAxis = int.MaxValue;
            foreach (var t in atMin) if (t.axis < minAxis) minAxis = t.axis;
            foreach (var t in atMin)
                if (t.axis == minAxis) result.Add(t.cell);
            scoreTie = atMin.Count >= 2 && atMin.Exists(t => t.axis != minAxis);
            axisTie = result.Count >= 2;
            return result;
        }

        static void QaAimOnBoard(Rig rig, float weight, QaAimStat st)
        {
            GameSim s = rig.S;
            Board b = s.Board;
            int[] dxs = { 1, 0, -1, 0 }, dys = { 0, 1, 0, -1 };
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++)
                {
                    if (!b.IsOperable(f, c)) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        Vector2Int start = new Vector2Int(c, f);
                        QaSetProp(s, "Aim", start);
                        List<Vector2Int> exp = QaExpectedAim(b, weight, start, dxs[d], dys[d], out int cands, out bool scoreTie, out bool axisTie);
                        bool moved = s.MoveAim(dxs[d], dys[d]);
                        Vector2Int got = s.Aim;
                        st.Cases++;
                        if (cands >= 2) st.Multi++;
                        if (scoreTie) st.ScoreTie++;
                        if (axisTie) st.AxisTie++;
                        if (cands == 0) st.None++;
                        bool ok = cands == 0 ? (!moved && got == start) : (moved && exp.Contains(got));
                        if (!ok)
                        {
                            st.Bad++;
                            if (st.FirstBad.Length == 0) st.FirstBad = "from (col " + c + ", floor " + f + ") dir (" + dxs[d] + "," + dys[d] + ") got " + got + " moved " + moved + " expected " + (exp.Count > 0 ? exp[0].ToString() : "stay");
                        }
                    }
                }
        }

        static void QaAimChecks()
        {
            var list = AssetDatabase.LoadAssetAtPath<Dt.RequestList>(QaRoot + "RequestList.asset");
            float weight = QaSettings.aimSideWeight;
            var seen = new HashSet<Dt.LevelData>();
            var total = new QaAimStat();
            int maps = 0;
            for (int i = 0; i < list.entries.Length; i++)
            {
                Dt.LevelData lv = list.entries[i].level;
                if (lv == null || !seen.Add(lv)) continue;
                var st = new QaAimStat();
                QaAimOnBoard(new Rig(lv), weight, st);
                maps++;
                total.Cases += st.Cases; total.Multi += st.Multi; total.ScoreTie += st.ScoreTie; total.AxisTie += st.AxisTie; total.None += st.None; total.Bad += st.Bad;
                if (total.FirstBad.Length == 0 && st.FirstBad.Length > 0) total.FirstBad = lv.name + " " + st.FirstBad;
                string num = view.Desk.Number(i);
                if (num == "0-2" || num == "1-4")
                    QNote("T-008", "map " + num + " (" + lv.name + "): " + st.Cases + " cases (every operable sector x 4 arrow keys), " + st.Multi + " with 2+ candidates, " + st.ScoreTie + " with a score tie (shorter axis rule), " + st.None + " with no candidate (stays), mismatches " + st.Bad);
            }
            Q("T-008", total.Bad == 0 && total.Cases > 0,
                "aim jump = rule computed independently (min score axis + " + weight + " x side; tie -> shorter axis; no candidate -> stay) on " + maps + " real maps: " + total.Cases + " cases, " + total.Multi + " with 2+ candidates, mismatches " + total.Bad + (total.Bad > 0 ? " - first: " + total.FirstBad : ""));
            if (total.ScoreTie > 0)
                Q("T-022", total.Bad == 0, "tie rule on real maps: " + total.ScoreTie + " cases have equal scores with different axis distances and all pick the shorter axis (equal score and equal axis cases: " + total.AxisTie + ", any of them accepted)");
            else
                QNote("T-022", "no real map has a score tie, so the tie rule is checked only on the synthetic map below");
        }

        /// <summary>점수가 같은 두 후보(진행 축 3·옆 0 = 3, 진행 축 1·옆 1 = 1 + 2 = 3)에서 진행 축이 짧은 쪽을 고르는지, 후보 없음은 유지하는지(합성 맵).</summary>
        static void QaAimTieSynthetic()
        {
            Dt.LevelData l = QaBaseLevel(3, 4);
            l.objects = new[]
            {
                PushObj(Dt.ObjectKind.Stairs, 1, 0, 80, true, false),   // 시작 조준 (col 0, floor 1)
                PushObj(Dt.ObjectKind.Stairs, 1, 3, 80, true, false),   // A: 오른쪽 진행 축 3, 옆 0 -> 점수 3
                PushObj(Dt.ObjectKind.Stairs, 0, 1, 80, true, false),   // B: 오른쪽 진행 축 1, 옆 1 -> 점수 1 + 2 x 1 = 3 (A와 같은 점수, 진행 축은 더 짧음)
            };
            l.startFloor = 1; l.hasStartAim = true; l.startAimFloor = 1; l.startAimCol = 0;
            var rig = new Rig(l);
            GameSim s = rig.S;
            float weight = QaSettings.aimSideWeight;
            Vector2Int a0 = s.Aim;
            bool moved = s.MoveAim(1, 0);
            if (!Mathf.Approximately(weight, 2f)) QNote("T-022", "synthetic tie map assumes aimSideWeight 2, the asset has " + weight + ": tie check skipped");
            else Q("T-022", a0 == new Vector2Int(0, 1) && moved && s.Aim == new Vector2Int(1, 0),
                "synthetic tie: from (col 0, floor 1) press -> with equal score 3 (A: axis 3 side 0, B: axis 1 side 1, weight " + weight + ") the shorter axis wins = B (col 1, floor 0), got " + s.Aim);
            bool left = s.MoveAim(-1, 0);
            Q("T-008", left && s.Aim == new Vector2Int(0, 1), "synthetic: press <- from B returns to (col 0, floor 1), got " + s.Aim);
            bool none = s.MoveAim(-1, 0);
            Q("T-008", !none && s.Aim == new Vector2Int(0, 1), "synthetic: no candidate on the left keeps the aim and returns false");
            UnityEngine.Object.DestroyImmediate(l);
        }

        // ===================================================================================================
        // T-011 추적도 100과 미션 완료가 같은 틱
        // ===================================================================================================

        static Dt.LevelData QaExitLevel()
        {
            Dt.LevelData l = QaBaseLevel(1, 2);
            l.objects = new[] { PushObj(Dt.ObjectKind.Exit, 0, 0, 100, true, true) };
            l.startX = 90;
            return l;
        }

        static void QaCountdownSameTick()
        {
            Dt.GameSettings st = QaSettings;
            Dt.LevelData lv = QaExitLevel();

            // A: 추적도가 이 틱에 100(최대)이 되고, 같은 틱에 최종 미션 출구를 지난다
            var d = new QaDrive { S = new Rig(lv).S };
            GameSim s = d.S;
            d.Until(() => s.TargetMoving, 10f);   // 시작 대기가 끝나 움직이기 시작할 때까지
            QaSetProp(s, "TargetX", 99.7f);
            QaSetProp(s, "Tracking", st.trackingMax - 0.05f);
            d.Shift = true;
            d.Once();
            bool a = s.Outcome == Outcome.Complete && s.MissionFinal && s.Tracking >= st.trackingMax && !s.CountdownActive;
            Q("T-011", a, "tracking reaches " + st.trackingMax + " in the same tick as the final mission (exit): outcome " + s.Outcome + ", final mission " + (s.MissionFinal ? "done" : "not done") + ", tracking " + s.Tracking.ToString("0.00") + ", countdown " + (s.CountdownActive ? "started" : "not started"));

            // B: 카운트다운이 이미 돌고 있고 이 틱에 0이 되는데, 같은 틱에 최종 미션 출구를 지난다
            var d2 = new QaDrive { S = new Rig(lv).S };
            GameSim s2 = d2.S;
            d2.Until(() => s2.TargetMoving, 10f);
            QaSetProp(s2, "TargetX", 99.7f);
            QaSetProp(s2, "Tracking", st.trackingMax);
            QaSetProp(s2, "CountdownActive", true);
            QaSetProp(s2, "Countdown", 0.001f);
            d2.Shift = true;
            d2.Once();
            bool b = s2.Outcome == Outcome.Complete && s2.MissionFinal;
            Q("T-011", b, "countdown (0.001 s left) would expire in the same tick as the final mission: outcome " + s2.Outcome + " (GameOver would mean the countdown wins), final mission " + (s2.MissionFinal ? "done" : "not done"));
            QNote("T-011", "criterion: decisions.md D-025 (countdown at max tracking, game over if not lowered before it ends) with D-027 (the stage ends when the final mission is done; no game over after that, written in D-027 and the D-025 contradiction note); in code GameSim.Step returns after MoveTarget completes the final mission, before UpdateCountdown runs, so the mission wins a same-tick tie");
            UnityEngine.Object.DestroyImmediate(lv);
        }

        // ===================================================================================================
        // T-023 중계기 의뢰당 1회
        // ===================================================================================================

        static void QaRelayOnceOn(Dt.LevelData lv, string label)
        {
            Dt.GameSettings st = QaSettings;
            var d = new QaDrive { S = new Rig(lv).S };
            GameSim s = d.S;
            SectorObject relay = null;
            bool hazard = false;
            foreach (SectorObject o in s.Board.Objects)
            {
                if (o.Def.kind == Dt.ObjectKind.Relay && relay == null) relay = o;
                if (o.Def.kind == Dt.ObjectKind.Camera) hazard = true;
            }
            if (relay == null) return;
            if (hazard) { QNote("T-023", label + ": skipped (the level has a surveillance camera that would change tracking by itself)"); return; }
            d.Until(() => s.TargetMoving, 10f);
            float start = relay.AbsX - 3f;
            QaSetProp(s, "TargetFloor", relay.Floor);
            QaSetProp(s, "TargetX", start);
            QaSetProp(s, "TargetFacingRight", true);
            QaSetProp(s, "Tracking", 70f);
            d.Shift = false;
            bool first = d.Until(() => s.RelayUsed, 5f);
            float after1 = s.Tracking;
            float expect1 = Mathf.Max(0f, 70f - st.relayRelief);
            // 두 번째: 같은 중계기를 다시 지나간다
            QaSetProp(s, "TargetFloor", relay.Floor);
            QaSetProp(s, "TargetX", start);
            QaSetProp(s, "TargetFacingRight", true);
            QaSetProp(s, "Tracking", 70f);
            bool second = d.Until(() => s.TargetX > relay.AbsX + 3f, 5f);
            float after2 = s.Tracking;
            Q("T-023", first && second && Mathf.Abs(after1 - expect1) < 0.01f && Mathf.Abs(after2 - 70f) < 0.01f && s.RelayUsed,
                label + ": first pass lowers tracking 70 -> " + after1.ToString("0.0") + " (expected " + expect1.ToString("0.0") + "), second pass over the same relay leaves 70 -> " + after2.ToString("0.0") + " (first reached " + first + ", second reached " + second + ")");
        }

        static void QaRelayOnce()
        {
            var list = AssetDatabase.LoadAssetAtPath<Dt.RequestList>(QaRoot + "RequestList.asset");
            var seen = new HashSet<Dt.LevelData>();
            int found = 0;
            for (int i = 0; i < list.entries.Length; i++)
            {
                Dt.LevelData lv = list.entries[i].level;
                if (lv == null || !seen.Add(lv)) continue;
                bool has = false;
                foreach (Dt.ObjectDef o in lv.objects ?? new Dt.ObjectDef[0]) if (o.kind == Dt.ObjectKind.Relay) has = true;
                if (!has) continue;
                found++;
                QaRelayOnceOn(lv, "request " + view.Desk.Number(i) + " (" + lv.name + ")");
            }
            if (found == 0) QNote("T-023", "no real request has a relay");
            // 합성 의뢰: 중계기 하나, 시작에서 오른쪽
            Dt.LevelData sy = QaBaseLevel(1, 2);
            sy.objects = new[] { PushObj(Dt.ObjectKind.Relay, 0, 0, 100, true, true), PushObj(Dt.ObjectKind.Exit, 0, 1, 120, true, true) };
            QaRelayOnceOn(sy, "synthetic one-relay level");
            UnityEngine.Object.DestroyImmediate(sy);
        }

        // ===================================================================================================
        // T-024 큰 프레임 간격
        // ===================================================================================================

        sealed class QaOutcome
        {
            public Outcome Out; public int Floor; public int Stars; public bool Final; public float X, Tracking; public float Seconds;
            public string Text => Out + ", floor " + Floor + ", stars " + Stars + ", final " + Final + ", x " + X.ToString("0.0") + ", tracking " + Tracking.ToString("0.0") + ", " + Seconds.ToString("0.00") + " s";
        }

        static QaOutcome QaSnap(QaDrive d) => new QaOutcome { Out = d.S.Outcome, Floor = d.S.TargetFloor, Stars = d.S.Stars, Final = d.S.MissionFinal, X = d.S.TargetX, Tracking = d.S.Tracking, Seconds = d.Time };

        /// <summary>0-1: 해치 조준 + 처음부터 Shift를 누른 채 끝까지.</summary>
        static QaOutcome QaHome1RouteA(float dt)
        {
            var d = new QaDrive { S = new Rig(Home1Path).S, Dt = dt };
            d.S.MoveAim(-1, 0);
            d.Shift = true;
            d.Until(() => d.S.Outcome != Outcome.Running, 40f);
            return QaSnap(d);
        }

        /// <summary>0-1: 해치 조준, 해치 앞(x >= 380)까지 걸은 뒤 Shift를 눌러 해치를 연다(스모크의 해치 먼저 경로와 같음).</summary>
        static QaOutcome QaHome1RouteB(float dt)
        {
            var d = new QaDrive { S = new Rig(Home1Path).S, Dt = dt };
            d.S.MoveAim(-1, 0);
            d.Until(() => d.S.TargetX >= 380f, 60f);
            d.Shift = true;
            d.Until(() => d.S.Outcome != Outcome.Running, 5f);
            d.Shift = false;
            return QaSnap(d);
        }

        static void QaBigFrameStep()
        {
            Dt.GameSettings st = QaSettings;
            float[] dts = { 1f / 60f, 0.1f, 0.25f };
            QaOutcome refA = QaHome1RouteA(dts[0]), refB = QaHome1RouteB(dts[0]);
            QNote("T-024", "0-1 route A (hatch aimed, Shift held from the start) at 1/60 s: " + refA.Text);
            QNote("T-024", "0-1 route B (walk to x 380, then Shift) at 1/60 s: " + refB.Text);
            for (int i = 1; i < dts.Length; i++)
            {
                QaOutcome a = QaHome1RouteA(dts[i]), b = QaHome1RouteB(dts[i]);
                // 결과(완료·층·별·최종 미션·도착 x)는 같아야 한다. 추적도는 Shift를 누른 시간에 비례해 한 걸음(dt)만큼 어긋날 수 있어 한 걸음 몫의 두 배까지 허용한다.
                float tol = st.trackingRatePerSecond * dts[i] * 2f + 0.01f;
                bool sameA = a.Out == refA.Out && a.Floor == refA.Floor && a.Stars == refA.Stars && a.Final == refA.Final && Mathf.Abs(a.X - refA.X) < 0.5f && Mathf.Abs(a.Tracking - refA.Tracking) <= tol;
                bool sameB = b.Out == refB.Out && b.Floor == refB.Floor && b.Stars == refB.Stars && b.Final == refB.Final && Mathf.Abs(b.X - refB.X) < 0.5f && Mathf.Abs(b.Tracking - refB.Tracking) <= tol;
                Q("T-024", sameA && sameB, "0-1 at frame step " + dts[i] + " s gives the same result as 1/60 s (outcome, floor, stars, final mission, arrival x; tracking within " + tol.ToString("0.0") + "): route A " + a.Text + " | route B " + b.Text);
            }
            // 한 걸음에 중계기·단말·출구가 모두 들어오는 경우: 판정이 간격과 상관없이 같은지
            QaOutcome m1 = QaMultiPass(dts[0]);
            QaOutcome m3 = QaMultiPass(0.25f);
            float expMulti = Mathf.Max(0f, Mathf.Max(0f, 90f - st.relayRelief) - st.missionRelief);
            Q("T-024", m1.Out == Outcome.Complete && m3.Out == m1.Out && Mathf.Abs(m1.Tracking - m3.Tracking) < 0.01f && Mathf.Abs(m1.Tracking - expMulti) < 0.01f,
                "relay + terminal + exit inside one step: 1/60 s -> " + m1.Text + " | 0.25 s -> " + m3.Text + " (expected tracking 90 - relay " + st.relayRelief + " - terminal " + st.missionRelief + " = " + expMulti.ToString("0") + ")");
        }

        /// <summary>합성 맵: 중계기 x60 · 단말(미션 1) x64 · 출구 x68이 12 px 안에 있다. 추적도 90에서 시작해 그 구간을 지난다.</summary>
        static QaOutcome QaMultiPass(float dt)
        {
            Dt.LevelData l = QaBaseLevel(1, 2);
            Dt.ObjectDef term = PushObj(Dt.ObjectKind.Terminal, 0, 0, 64, true, true);
            term.mission = 1;
            l.objects = new[] { PushObj(Dt.ObjectKind.Relay, 0, 0, 60, true, true), term, PushObj(Dt.ObjectKind.Exit, 0, 0, 68, true, true) };
            l.missions = new[] { new Dt.MissionDef { kind = Dt.MissionKind.PassObject }, new Dt.MissionDef { kind = Dt.MissionKind.PassObject } };
            var d = new QaDrive { S = new Rig(l).S, Dt = dt };
            d.Until(() => d.S.TargetMoving, 10f);
            QaSetProp(d.S, "TargetX", 58f);
            QaSetProp(d.S, "Tracking", 90f);
            d.Shift = false;
            d.Until(() => d.S.Outcome != Outcome.Running, 5f);
            QaOutcome o = QaSnap(d);
            UnityEngine.Object.DestroyImmediate(l);
            return o;
        }

        // ===================================================================================================
        // T-017 글꼴 아틀라스 '_'
        // ===================================================================================================

        static void QaFontChecks()
        {
            var art = AssetDatabase.LoadAssetAtPath<Dt.ArtSet>(QaRoot + "ArtSet.asset");
            bool has = false;
            string size = "";
            foreach (Dt.GlyphEntry g in art.glyphs)
                if (g.code == '_') { has = g.w > 0 && g.h > 0; size = g.w + "x" + g.h; }
            int w = PixelCanvas.TextWidth5x7("REQ_0-1");
            int wl = PixelCanvas.TextWidth("REQ_0-1");
            Q("T-017", has && w > 0 && wl > 0, "font atlas glyph '_' present in the art set" + (has ? " (" + size + " px)" : " (missing; the code draws a one-line underline fallback)") + ", width of REQ_0-1 = " + w + " px (5x7), " + wl + " px (current language)");
        }

        // ===================================================================================================
        // T-036 · T-040 텍스처
        // ===================================================================================================

        static bool QaPngSize(string assetPath, out int w, out int h)
        {
            w = h = 0;
            try
            {
                if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return false;
                using (var fs = File.OpenRead(Path.GetFullPath(Path.Combine(Application.dataPath, "../" + assetPath))))
                {
                    var b = new byte[24];
                    if (fs.Read(b, 0, 24) < 24) return false;
                    w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                    h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                    return w > 0 && h > 0;
                }
            }
            catch (Exception) { return false; }
        }

        static void QaTextureChecks()
        {
            var art = AssetDatabase.LoadAssetAtPath<Dt.ArtSet>(QaRoot + "ArtSet.asset");
            var seen = new HashSet<string>();
            int sheets = 0, sheetBig = 0, unique = 0, noSource = 0, compressed = 0, roomCount = 0, maxDim = 0;
            long bytes = 0, roomBytes = 0;
            var shrunk = new List<string>();
            var bigSheets = new List<string>();
            foreach (Dt.ArtEntry e in art.entries)
            {
                if (e.texture == null) continue;
                string path = AssetDatabase.GetAssetPath(e.texture);
                int tw = e.texture.width, th = e.texture.height;
                if (e.frames > 0)
                {
                    sheets++;
                    if (tw > 2048 || th > 2048) { sheetBig++; bigSheets.Add(e.key + " " + tw + "x" + th); }
                }
                if (!seen.Add(path)) continue;
                unique++;
                maxDim = Mathf.Max(maxDim, tw, th);
                bytes += (long)tw * th * 4;
                if (e.key.StartsWith("rooms128/")) { roomCount++; roomBytes += (long)tw * th * 4; }
                int sw = tw, sh = th;
                if (!QaPngSize(path, out sw, out sh)) { sw = tw; sh = th; noSource++; }
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                int defMax = ti != null ? ti.maxTextureSize : 0;
                int webMax = defMax;
                if (ti != null)
                {
                    TextureImporterPlatformSettings web = ti.GetPlatformTextureSettings("WebGL");
                    if (web != null && web.overridden) webMax = web.maxTextureSize;
                    if (ti.textureCompression != TextureImporterCompression.Uncompressed) compressed++;
                }
                int srcMax = Mathf.Max(sw, sh);
                if (tw < sw || th < sh || (ti != null && (defMax < srcMax || webMax < srcMax)))
                    shrunk.Add(e.key + " (source " + sw + "x" + sh + ", imported " + tw + "x" + th + ", maxTextureSize " + defMax + (webMax != defMax ? ", WebGL " + webMax : "") + ")");
            }
            string sample = shrunk.Count > 0 ? " - e.g. " + shrunk[0] + (shrunk.Count > 1 ? " (+" + (shrunk.Count - 1) + " more)" : "") : "";
            Q("T-036", sheets > 0 && sheetBig == 0, "animation sheet textures actual size <= 2048: " + sheets + " sheets, " + sheetBig + " above 2048" + (sheetBig > 0 ? " - " + string.Join(", ", bigSheets) : ""));
            Q("T-036", shrunk.Count == 0, "no texture is shrunk by the import settings (source size <= imported size and maxTextureSize, incl. the WebGL override, >= source size): " + unique + " unique textures, largest side " + maxDim + " px, shrunk " + shrunk.Count + sample + (noSource > 0 ? " (" + noSource + " not PNG: imported size used as the source)" : ""));
            QNote("T-040", "ArtSet texture memory (width x height x 4 bytes): " + unique + " unique textures = " + (bytes / 1048576.0).ToString("0.0") + " MB; room layer textures (rooms128/): " + roomCount + " = " + (roomBytes / 1048576.0).ToString("0.0") + " MB (" + (roomCount > 0 ? (roomBytes / 1048576.0 / roomCount).ToString("0.00") : "0") + " MB each); " + compressed + " textures have a compression setting other than Uncompressed (not counted here). Build Report check and atlas / code-light comparison are not done here");
        }

        // ===================================================================================================
        // T-015 저장 실패
        // ===================================================================================================

        static void QaT015()
        {
            QNote("T-015", "manual check needed: ProgressStore calls the static PlayerPrefs directly and has no failure injection point; every read and write is inside try/catch with a memory cache (code check only, not a run). Block the browser storage in a Web build to confirm");
        }

        // ===================================================================================================
        // T-009 · T-013 · T-025 · T-038 · T-043 해상도별 캡처
        // ===================================================================================================

        static readonly Vector2Int[] QaScreens =
        {
            new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1366, 768), new Vector2Int(1080, 1350),
        };

        /// <summary>
        /// 화면 출력(GameView.DrawCanvas)과 같은 계산으로 sw x sh 화면에 16:9 캔버스를 가운데에 놓고 남는 곳은 검게 채운다(Bilinear).
        /// 캔버스는 PixelCanvas.SaveSizedPng로 먼저 같은 방식으로 줄여 저장한 뒤 검은 바탕에 얹는다.
        /// </summary>
        static string QaCaptureScreen(string name, int sw, int sh)
        {
            float k = Mathf.Min(sw / (float)PixelCanvas.RW, sh / (float)PixelCanvas.RH);
            int w = Mathf.Max(1, Mathf.RoundToInt(PixelCanvas.RW * k)), h = Mathf.Max(1, Mathf.RoundToInt(PixelCanvas.RH * k));
            int x0 = (sw - w) / 2, y0 = (sh - h) / 2;
            string tmp = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/qa_s7_tmp.png"));
            view.Canvas.SaveSizedPng(tmp, w, h);
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(tmp));
            var dst = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
            var black = new Color32[sw * sh];
            for (int i = 0; i < black.Length; i++) black[i] = new Color32(0, 0, 0, 255);
            dst.SetPixels32(black);
            dst.SetPixels(x0, y0, Mathf.Min(w, src.width), Mathf.Min(h, src.height), src.GetPixels());
            dst.Apply(false);
            Directory.CreateDirectory(QaCaptureDir);
            File.WriteAllBytes(Path.Combine(QaCaptureDir, name + "_" + sw + "x" + sh + ".png"), dst.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(src);
            UnityEngine.Object.DestroyImmediate(dst);
            float logical = w / (float)PixelCanvas.W;
            return sw + "x" + sh + " -> canvas " + w + "x" + h + " at (" + x0 + "," + y0 + "), bars L/R " + x0 + " px, T/B " + y0 + " px, logical 640x360 scale " + logical.ToString("0.000") + (Mathf.Approximately(logical, Mathf.Round(logical)) ? " (integer)" : " (not an integer)");
        }

        static IEnumerator QaCaptureAll(string name, string where)
        {
            yield return Frames(3);   // 스크린샷은 프레임 끝에 찍히므로 그리기가 끝난 프레임에서 한다
            string lines = "";
            foreach (Vector2Int sz in QaScreens)
                lines += (lines.Length > 0 ? "; " : "") + QaCaptureScreen(name, sz.x, sz.y);
            yield return Frames(1);
            QNote("T-009 T-013 T-025 T-038 T-043", where + " captured in Docs/QA/captures/S7/ (" + name + "_WxH.png): " + lines + ". A person checks letterbox, cut-off text and legibility in the PNGs");
        }

        static IEnumerator QaCaptures()
        {
            yield return Frames(3);
            Check("T-009 captures start from the main screen", view.CurrentPhase == GameView.Phase.Main);
            yield return QaCaptureAll("main", "main screen");
            yield return Press("Enter");   // NEW GAME -> 부팅 -> 바탕화면
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Desktop, "QA Desktop");
            yield return Press("Enter");   // NORMAL MODE: 의뢰 팝업
            yield return WaitUntil(() => view.PopupOpen && view.PopupSettled, "QA popup settled");
            yield return QaCaptureAll("desktop-requests", "request desktop (popup open)");
            QaStartLevel(0);
            yield return Frames(30);
            yield return QaCaptureAll("playing-0-1", "request screen in play (0-1)");
        }

        static void QaStartLevel(int index)
        {
            shift = false;
            SetActiveRequest(index);
            view.DebugStartLevel(view.Desk.Entry(index).level);
        }

        // ===================================================================================================
        // T-016 낮은 별로 다시 클리어해도 최고 별 유지
        // ===================================================================================================

        static IEnumerator QaBestStarsKept()
        {
            int idx = -1;
            for (int i = 0; i < view.Desk.Count && idx < 0; i++)
            {
                Dt.RequestEntry e = view.Desk.Entry(i);
                if (e.level != null && !e.level.tutorial && !e.testEntry && e.level.missions != null && e.level.missions.Length >= 2) idx = i;
            }
            if (idx < 0) { QNote("T-016", "no request with 2+ missions found"); yield break; }
            string id = view.Desk.Entry(idx).id;
            view.Progress.Clear(AllIds());
            QaStartLevel(idx);
            yield return Frames(3);
            ForceSimCompleteAll();
            yield return ToEnded();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "QA first clear Ended");
            yield return Frames(2);
            int best1 = view.Progress.GetBest(id);
            QaStartLevel(idx);
            yield return Frames(3);
            ForceSimComplete();   // 최종 미션만 완료 = 별 1개
            int stars2 = Sim.Stars;
            yield return ToEnded();
            yield return WaitUntil(() => view.CurrentPhase == GameView.Phase.Ended, "QA second clear Ended");
            yield return Frames(2);
            int best2 = view.Progress.GetBest(id);
            Q("T-016", best1 == 3 && stars2 < 3 && best2 == 3,   /* 솜씨 미션은 시작부터 완료로 셀 수 있어(T-033) 다시 깬 별이 1~2개 */ "request " + view.Desk.Number(idx) + " (" + id + "): clear with 3 stars -> best " + best1 + "; clear again with " + stars2 + " star -> best " + best2 + " (expected 3 -> fewer than 3 -> best stays 3)");
        }

        // ===================================================================================================
        // T-039 조명을 모두 끈 섹터의 어둠 그림
        // ===================================================================================================

        static IEnumerator QaDarkSector()
        {
            QaStartLevel(0);
            yield return Frames(5);
            Board b = Sim.Board;
            SceneLayout lay = b.Layout;
            if (lay == null) { QNote("T-039", "0-1 has no room layer layout"); yield break; }
            Vector2Int a = Sim.Aim;
            SceneLayout.SectorView v = lay.Sector(a.y, a.x);
            if (v == null || v.Room == null || v.LightNames.Length == 0) { QNote("T-039", "aimed sector (col " + a.x + ", floor " + a.y + ") has no room or lights"); yield break; }
            foreach (string ln in v.LightNames) lay.SetLight(a.y, a.x, ln, false);
            bool dark = lay.IsDark(a.y, a.x);
            string key = v.Room.Dark;
            view.Canvas.TrackKeys = true;
            view.Canvas.DrawnKeys.Clear();
            yield return Frames(4);
            bool drawn = view.Canvas.DrawnKeys.Contains(key);
            view.Canvas.TrackKeys = false;
            int kw = view.Canvas.WidthOf(key), kh = view.Canvas.HeightOf(key);
            Q("T-039", dark && drawn && kw == b.SectorW && kh == b.SectorH,
                "0-1 sector (col " + a.x + ", floor " + a.y + ") with all " + v.LightNames.Length + " lights off: dark state " + dark + ", dark picture '" + key + "' drawn " + drawn + ", picture size " + kw + "x" + kh + " vs sector " + b.SectorW + "x" + b.SectorH);
            yield return Frames(1);
            QaCaptureScreen("dark-sector", 1920, 1080);
            yield return Frames(1);
            QNote("T-039", "capture Docs/QA/captures/S7/dark-sector_1920x1080.png: a person checks that the darkness covers exactly the sector");
        }
    }

    /// <summary>T-076: Play 모드가 끝나 Editor 모드로 돌아온 뒤 입력 설정을 실행 전과 비교한다(도메인 다시 로드 뒤에도 동작하도록 InitializeOnLoad).</summary>
    [InitializeOnLoad]
    static class QaEditModeHook
    {
        const string PendingKey = "GameOff.QaS7.T076Pending";

        static QaEditModeHook()
        {
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.delayCall += TryReport;
        }

        static void OnState(PlayModeStateChange c)
        {
            if (c == PlayModeStateChange.EnteredEditMode) TryReport();
        }

        static void TryReport()
        {
            if (!EditorPrefs.GetBool(PendingKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorPrefs.SetBool(PendingKey, false);
            Mvp1SmokeCheck.QaReportT076("");
        }
    }
}
