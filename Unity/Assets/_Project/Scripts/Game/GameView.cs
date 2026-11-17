using System.Collections.Generic;
using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// MVP-1 메인 컨트롤러: 입력 처리, 화면 단계(첫 화면·플레이·일시정지·종료), 임시 화면(IMGUI 단순 도형).
    /// 규칙은 GameSim이 맡고, 여기서는 입력을 넘기고 결과를 그린다.
    /// </summary>
    public sealed class GameView : MonoBehaviour
    {
        /// <summary>
        /// 화면 상태(D-056~D-059): 메인 / 크레딧 / 부팅(모니터 켜짐·로딩) / 바탕화면(의뢰 선택, 상세 패널 포함) /
        /// 접속 로딩 / 게임 / 일시정지 / 결과. Credits 이하가 아닌 Connecting까지는 캔버스(도트 메뉴)로 그린다.
        /// </summary>
        public enum Phase { Main, Credits, Booting, Desktop, Connecting, Playing, Paused, Ended }

        [SerializeField] GameSettings settings;
        [SerializeField] RequestList requests;
        [SerializeField] ArtSet art;

        public Phase CurrentPhase { get; private set; } = Phase.Main;
        public GameSim Sim { get; private set; }
        public PixelCanvas Canvas { get; private set; }
        public RequestBoard Desk { get; private set; }
        public ProgressStore Progress { get; private set; }
        public int MainIndex { get; private set; }
        public int DeskIndex { get; private set; }
        public int ActiveIndex { get; private set; }
        /// <summary>바탕화면 상세 패널이 열려 있는지(닫는 중·여는 중 포함 아님).</summary>
        public bool PanelOpen { get; private set; }
        /// <summary>상세 패널이 열려 있고 등장 연출이 끝났는지.</summary>
        public bool PanelSettled => PanelOpen && panelElapsed >= settings.panelSlideSeconds;
        public int BootStage => Mathf.Min(3, (int)(loadElapsed / settings.bootStageSeconds));

        MenuRenderer menus;
        bool dirty = true;
        float loadElapsed;     // 현재 단계(부팅·접속)의 경과 시간
        float panelElapsed;    // 패널 등장 경과 시간
        // 일시정지 메뉴: 0 = Continue, 1 = Restart, 2 = Desktop
        int pauseIndex;
        bool prevShift;
        readonly List<string> commandLog = new List<string>();

        const float RefW = 960f, RefH = 540f;
        const int MaxLog = 9;

        Texture2D starTex, circleTex;
        readonly GUIStyle textStyle = new GUIStyle();

        // ---------- 수명 ----------

        void Awake()
        {
            Progress = new ProgressStore();
            Desk = new RequestBoard(requests, Progress);
            Canvas = new PixelCanvas(art);
            menus = new MenuRenderer(Canvas, Desk);
            starTex = MakeShapeTexture(true);
            circleTex = MakeShapeTexture(false);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) PauseIfPlaying();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) PauseIfPlaying();
        }

        void PauseIfPlaying()
        {
            if (CurrentPhase != Phase.Playing) return;
            CurrentPhase = Phase.Paused;
            pauseIndex = 0;
            Sim.ShiftHeld = false;
        }

        bool IsMenuPhase => CurrentPhase <= Phase.Connecting;

        void Go(Phase p)
        {
            CurrentPhase = p;
            loadElapsed = 0f;
            if (p == Phase.Desktop && !PanelOpen) panelElapsed = 0f;
            dirty = true;
        }

        void StartRequest(int index)
        {
            ActiveIndex = index;
            Sim = new GameSim(settings, new Board(Desk.Entry(index).level));
            RestartGame();
        }

        void RestartGame()
        {
            Sim.Reset();
            CurrentPhase = Phase.Playing;
            prevShift = false;
            commandLog.Clear();
        }

        // ---------- 입력·진행 ----------

        void Update()
        {
            InputFrame inp = GameInput.Read();

            switch (CurrentPhase)
            {
                case Phase.Main:
                    if (inp.Up || inp.Down) { MainIndex = 1 - MainIndex; dirty = true; }
                    if (inp.Enter) Go(MainIndex == 0 ? Phase.Booting : Phase.Credits);
                    break;

                case Phase.Credits:
                    if (inp.Esc) Go(Phase.Main);
                    break;

                case Phase.Booting:
                {
                    int before = BootStage;
                    loadElapsed += Mathf.Min(Time.deltaTime, 0.1f);
                    if (BootStage != before) dirty = true;
                    if (loadElapsed >= settings.bootStageSeconds * 4) OpenDesktop();
                    break;
                }

                case Phase.Connecting:
                    loadElapsed += Mathf.Min(Time.deltaTime, 0.1f);
                    dirty = true;
                    if (loadElapsed >= settings.connectSeconds) StartRequest(DeskIndex);
                    break;

                case Phase.Desktop:
                    UpdateDesktop(inp);
                    break;

                case Phase.Playing:
                    UpdatePlaying(inp);
                    break;

                case Phase.Paused:
                    UpdatePaused(inp);
                    break;

                case Phase.Ended:
                    if (inp.Enter)
                    {
                        // 클리어 -> 의뢰 선택(바탕화면), 게임오버 -> 처음부터 다시(D-031)
                        if (Sim.Outcome == Outcome.Complete) OpenDesktop();
                        else RestartGame();
                    }
                    break;
            }

            if (dirty && IsMenuPhase)
            {
                dirty = false;
                RedrawMenu();
            }
        }

        /// <summary>바탕화면으로 간다(상세 패널은 닫힌 상태).</summary>
        void OpenDesktop()
        {
            PanelOpen = false;
            panelElapsed = 0f;
            Go(Phase.Desktop);
        }

        /// <summary>패널 x 위치: 화면 오른쪽 밖에서 정수 픽셀 단위로 미끄러져 들어온다.</summary>
        public int PanelX()
        {
            float t = settings.panelSlideSeconds <= 0f ? 1f : Mathf.Clamp01(panelElapsed / settings.panelSlideSeconds);
            return MenuRenderer.PanelTargetX + Mathf.RoundToInt((PixelCanvas.W - MenuRenderer.PanelTargetX) * (1f - t));
        }

        void UpdateDesktop(InputFrame inp)
        {
            int n = Desk.Count;
            if (PanelOpen && panelElapsed < settings.panelSlideSeconds)
            {
                panelElapsed += Mathf.Min(Time.deltaTime, 0.1f);
                dirty = true;
            }
            if (inp.Down) { DeskIndex = (DeskIndex + 1) % n; dirty = true; }
            if (inp.Up) { DeskIndex = (DeskIndex + n - 1) % n; dirty = true; }
            if (inp.Esc)
            {
                if (PanelOpen) { PanelOpen = false; dirty = true; }   // 패널 닫기
                else Go(Phase.Main);                                    // 모니터 꺼짐 -> 메인
                return;
            }
            if (inp.Enter)
            {
                if (!PanelOpen) { PanelOpen = true; panelElapsed = 0f; dirty = true; }
                else if (Desk.IsPlayable(DeskIndex)) Go(Phase.Connecting);   // 잠긴 의뢰는 무시
            }
        }

        /// <summary>메뉴 화면을 캔버스에 다시 그린다. 자동 확인·캡처가 직접 부를 수도 있다.</summary>
        public void RedrawMenu()
        {
            switch (CurrentPhase)
            {
                case Phase.Main: menus.DrawMain(MainIndex); break;
                case Phase.Credits: menus.DrawCredits(); break;
                case Phase.Booting: menus.DrawBoot(BootStage); break;
                case Phase.Desktop: menus.DrawDesktop(DeskIndex, PanelOpen ? PanelX() : -1, -1f); break;
                case Phase.Connecting: menus.DrawDesktop(DeskIndex, MenuRenderer.PanelTargetX, loadElapsed / settings.connectSeconds); break;
            }
            Canvas.Apply();
        }

        void UpdatePlaying(InputFrame inp)
        {
            if (inp.Esc)
            {
                PauseIfPlaying();
                return;
            }

            Sim.ShiftHeld = inp.LeftShift;
            if (inp.LeftShift && !prevShift) Log("LShift");
            prevShift = inp.LeftShift;

            // Shift를 누른 동안 방향키는 무시(MoveAim도 막지만 기록도 남기지 않음)
            if (!inp.LeftShift)
            {
                if (inp.Left && Sim.MoveAim(-1, 0)) Log("Left");
                if (inp.Right && Sim.MoveAim(1, 0)) Log("Right");
                if (inp.Up && Sim.MoveAim(0, -1)) Log("Up");
                if (inp.Down && Sim.MoveAim(0, 1)) Log("Down");
            }

            Sim.Step(Mathf.Min(Time.deltaTime, 0.1f));

            if (Sim.Outcome != Outcome.Running)
            {
                Sim.ShiftHeld = false;
                if (Sim.Outcome == Outcome.Complete) Progress.SetBest(Desk.Entry(ActiveIndex).id, Sim.Stars);
                CurrentPhase = Phase.Ended;
            }
        }

        void UpdatePaused(InputFrame inp)
        {
            if (inp.Esc)
            {
                CurrentPhase = Phase.Playing;
                return;
            }
            if (inp.Down) pauseIndex = (pauseIndex + 1) % 3;
            if (inp.Up) pauseIndex = (pauseIndex + 2) % 3;
            if (inp.Enter)
            {
                if (pauseIndex == 0) CurrentPhase = Phase.Playing;
                else if (pauseIndex == 1) RestartGame();
                else OpenDesktop();
            }
        }

        void Log(string cmd)
        {
            commandLog.Add("> " + cmd);
            if (commandLog.Count > MaxLog) commandLog.RemoveAt(0);
        }

        // ---------- 화면 ----------

        static readonly Color ColBg = new Color(0.07f, 0.08f, 0.10f);
        static readonly Color ColWindow = new Color(0.14f, 0.16f, 0.20f);
        static readonly Color ColTitleBar = new Color(0.22f, 0.25f, 0.32f);
        static readonly Color ColText = new Color(0.90f, 0.92f, 0.95f);
        static readonly Color ColDim = new Color(0.55f, 0.60f, 0.68f);
        static readonly Color ColBlue = new Color(0.30f, 0.55f, 1.00f);
        static readonly Color ColRed = new Color(1.00f, 0.30f, 0.28f);
        static readonly Color ColGreen = new Color(0.30f, 0.80f, 0.42f);

        void OnGUI()
        {
            if (IsMenuPhase || CurrentPhase == Phase.Credits)
            {
                DrawCanvas();
                return;
            }
            if (Sim == null) return;

            float scale = Mathf.Min(Screen.width / RefW, Screen.height / RefH);
            Vector2 offset = new Vector2((Screen.width - RefW * scale) * 0.5f, (Screen.height - RefH * scale) * 0.5f);

            GUI.matrix = Matrix4x4.identity;
            Fill(new Rect(0, 0, Screen.width, Screen.height), ColBg);
            GUI.matrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0), Quaternion.identity, new Vector3(scale, scale, 1));

            DrawWindow();
            DrawGrid();
            DrawSidePanel();
            DrawCommandLog();

            switch (CurrentPhase)
            {
                case Phase.Paused: DrawPauseOverlay(); break;
                case Phase.Ended: DrawEndOverlay(); break;
            }

            GUI.matrix = Matrix4x4.identity;
        }

        void DrawWindow()
        {
            Fill(new Rect(20, 20, 920, 500), ColWindow);
            Fill(new Rect(20, 20, 920, 30), ColTitleBar);
            Text(new Rect(34, 20, 700, 30), "SHIFT prototype  -  " + Desk.Entry(ActiveIndex).level.title, 16, ColText, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text(new Rect(40, 76, 300, 24), "FLOOR VIEW", 13, ColDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text(new Rect(40, 330, 880, 24), "Arrows: jump aim to next operable cell   |   Hold Left Shift: reveal blueprint of aimed cell   |   Esc: pause", 14, ColDim, TextAnchor.MiddleLeft, FontStyle.Normal);
        }

        Rect GridRect()
        {
            float cell = Mathf.Min(600f / Sim.Board.Width, 200f / Sim.Board.Height);
            return new Rect(40, 110, cell * Sim.Board.Width, cell * Sim.Board.Height);
        }

        void DrawGrid()
        {
            Board b = Sim.Board;
            Rect g = GridRect();
            float cs = g.width / b.Width;

            for (int y = 0; y < b.Height; y++)
            {
                for (int x = 0; x < b.Width; x++)
                {
                    Rect r = new Rect(g.x + x * cs, g.y + y * cs, cs, cs);
                    DrawCell(r, b.Reality[x, y], 1f, false);
                    Border(r, 1, new Color(0, 0, 0, 0.35f));
                }
            }

            // 설계도 층: Shift를 누른 동안 조준한 칸만 청사진처럼 겹쳐 표시(현실 칸은 그대로 비쳐 보임)
            Vector2Int aim = Sim.Aim;
            Rect ar = new Rect(g.x + aim.x * cs, g.y + aim.y * cs, cs, cs);
            if (Sim.ShiftHeld)
            {
                DrawCell(ar, b.Blueprint[aim.x, aim.y], 0.6f, true);
                Border(ar, 3, ColBlue);
            }

            // 대상
            Vector2Int t = Sim.TargetPos;
            Rect tr = new Rect(g.x + t.x * cs + cs * 0.2f, g.y + t.y * cs + cs * 0.2f, cs * 0.6f, cs * 0.6f);
            Tinted(tr, circleTex, new Color(1f, 0.62f, 0.15f));
            Text(tr, DirGlyph(Sim.TargetDir), 18, new Color(0.15f, 0.08f, 0f), TextAnchor.MiddleCenter, FontStyle.Bold);

            // 조준틀
            Border(new Rect(ar.x - 1, ar.y - 1, ar.width + 2, ar.height + 2), 2, Color.white);
        }

        void DrawCell(Rect r, Cell c, float alpha, bool blueprint)
        {
            Color floor = new Color(0.62f, 0.65f, 0.72f);
            Color wall = new Color(0.16f, 0.17f, 0.21f);
            Color door = new Color(0.60f, 0.35f, 0.18f);
            Color glyph = new Color(0.12f, 0.13f, 0.17f);
            string label = null;
            Color labelCol = glyph;
            Color fill = floor;
            Rect inner = new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 12);
            bool innerBox = false;
            Color innerCol = floor;
            bool outlineOnly = false;

            switch (c.Kind)
            {
                case CellKind.Wall: fill = wall; break;
                case CellKind.DoorClosed: innerBox = true; innerCol = door; label = "D"; labelCol = Color.white; break;
                case CellKind.DoorOpen: outlineOnly = true; innerCol = door; label = "o"; labelCol = door; break;
                case CellKind.Arrow: label = DirGlyph(c.Dir); break;
                case CellKind.Exit: innerBox = true; innerCol = new Color(0.25f, 0.70f, 0.35f); label = "E"; labelCol = Color.white; break;
                case CellKind.CheckA: innerBox = true; innerCol = Sim.MissionA ? ColGreen : new Color(0.92f, 0.80f, 0.30f); label = "A"; break;
                case CellKind.CheckB: innerBox = true; innerCol = Sim.MissionB ? ColGreen : new Color(0.92f, 0.80f, 0.30f); label = "B"; break;
                case CellKind.Relay: innerBox = true; innerCol = Sim.RelayUsed ? new Color(0.45f, 0.38f, 0.55f) : new Color(0.62f, 0.40f, 0.85f); label = "R"; labelCol = Color.white; break;
            }

            if (blueprint)
            {
                fill = Color.Lerp(fill, ColBlue, 0.45f);
                innerCol = Color.Lerp(innerCol, ColBlue, 0.45f);
            }
            fill.a *= alpha;
            innerCol.a *= alpha;
            labelCol.a *= alpha;

            Fill(r, fill);
            if (innerBox) Fill(inner, innerCol);
            if (outlineOnly) Border(inner, 3, innerCol);
            if (label != null) Text(r, label, 24, labelCol, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void DrawSidePanel()
        {
            float x = 670;
            Text(new Rect(x, 76, 250, 24), "MISSIONS", 13, ColDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            MissionRow(x, 104, "Reach the exit (final)", Sim.MissionFinal);
            MissionRow(x, 132, "Pass checkpoint A", Sim.MissionA);
            MissionRow(x, 160, "Pass checkpoint B", Sim.MissionB);

            Text(new Rect(x, 200, 250, 24), "TRACE", 13, ColDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            Rect gauge = new Rect(x, 226, 250, 22);
            Fill(gauge, new Color(0, 0, 0, 0.5f));
            float ratio = Mathf.Clamp01(Sim.Tracking / Sim.TrackingMax);
            Fill(new Rect(gauge.x, gauge.y, gauge.width * ratio, gauge.height), Sim.CountdownActive ? ColRed : ColBlue);
            Border(gauge, 1, ColDim);
            Text(gauge, Mathf.RoundToInt(Sim.Tracking) + " / " + Mathf.RoundToInt(Sim.TrackingMax), 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            if (Sim.CountdownActive)
            {
                Text(new Rect(x, 256, 250, 20), "TRACED - COUNTDOWN", 13, ColRed, TextAnchor.MiddleLeft, FontStyle.Bold);
                Text(new Rect(x, 274, 250, 54), Sim.Countdown.ToString("0.0"), 48, ColRed, TextAnchor.MiddleLeft, FontStyle.Bold);
            }
        }

        void MissionRow(float x, float y, string label, bool done)
        {
            Rect box = new Rect(x, y + 3, 18, 18);
            Fill(box, new Color(0, 0, 0, 0.4f));
            Border(box, 1, ColDim);
            if (done) Fill(new Rect(box.x + 4, box.y + 4, 10, 10), ColGreen);
            Text(new Rect(x + 28, y, 222, 24), label, 14, done ? ColGreen : ColText, TextAnchor.MiddleLeft, FontStyle.Normal);
        }

        void DrawCommandLog()
        {
            Rect bar = new Rect(40, 440, 880, 30);
            Fill(bar, new Color(0, 0, 0, 0.45f));
            Border(bar, 1, ColDim);
            Text(new Rect(bar.x + 10, bar.y, bar.width - 20, bar.height), string.Join("   ", commandLog), 15, ColText, TextAnchor.MiddleLeft, FontStyle.Normal);
        }

        void DrawPauseOverlay()
        {
            Fill(new Rect(20, 50, 920, 470), new Color(0, 0, 0, 0.75f));
            Text(new Rect(20, 170, 920, 50), "PAUSED", 36, ColText, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuItem(new Rect(380, 250, 200, 36), "Continue", pauseIndex == 0);
            MenuItem(new Rect(380, 296, 200, 36), "Restart", pauseIndex == 1);
            MenuItem(new Rect(380, 342, 200, 36), "DESKTOP", pauseIndex == 2);
            Text(new Rect(20, 400, 920, 24), "Up / Down: select    Enter: confirm    Esc: continue", 14, ColDim, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        void MenuItem(Rect r, string label, bool selected)
        {
            Fill(r, selected ? new Color(0.30f, 0.55f, 1f, 0.35f) : new Color(1, 1, 1, 0.06f));
            if (selected) Border(r, 2, ColBlue);
            Text(r, label, 20, selected ? Color.white : ColDim, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void DrawEndOverlay()
        {
            Fill(new Rect(20, 50, 920, 470), new Color(0, 0, 0, 0.8f));
            if (Sim.Outcome == Outcome.GameOver)
            {
                Text(new Rect(20, 180, 920, 60), "GAME OVER", 44, ColRed, TextAnchor.MiddleCenter, FontStyle.Bold);
                Text(new Rect(20, 240, 920, 30), "Traced. The request failed.", 18, ColDim, TextAnchor.MiddleCenter, FontStyle.Normal);
            }
            else
            {
                Text(new Rect(20, 140, 920, 50), "REQUEST COMPLETE", 36, ColGreen, TextAnchor.MiddleCenter, FontStyle.Bold);
                int stars = Sim.Stars;
                for (int i = 0; i < 3; i++)
                {
                    Rect sr = new Rect(480 - 120 + i * 84, 200, 72, 72);
                    Tinted(sr, starTex, i < stars ? new Color(1f, 0.82f, 0.2f) : new Color(1f, 1f, 1f, 0.15f));
                }
                Text(new Rect(20, 280, 920, 30), stars + " / 3 stars", 20, ColText, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            Rect btn = new Rect(380, 360, 200, 40);
            MenuItem(btn, Sim.Outcome == Outcome.Complete ? "Desktop" : "Restart", true);
            Text(new Rect(20, 410, 920, 24), "Press Enter", 14, ColDim, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        static string DirGlyph(Dir d)
        {
            switch (d)
            {
                case Dir.Right: return ">";
                case Dir.Left: return "<";
                case Dir.Up: return "^";
                default: return "v";
            }
        }

        // ---------- 그리기 도우미 ----------

        /// <summary>메뉴 캔버스를 정수 배율로 확대해 그린다. 남는 부분은 검은 레터박스.</summary>
        void DrawCanvas()
        {
            GUI.matrix = Matrix4x4.identity;
            Fill(new Rect(0, 0, Screen.width, Screen.height), Color.black);
            int k = Mathf.Max(1, Mathf.Min(Screen.width / PixelCanvas.W, Screen.height / PixelCanvas.H));
            int w = PixelCanvas.W * k, h = PixelCanvas.H * k;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.DrawTexture(r, Canvas.Texture, ScaleMode.StretchToFill, false);
        }

        static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static void Border(Rect r, float t, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        static void Tinted(Rect r, Texture2D tex, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, tex);
            GUI.color = Color.white;
        }

        void Text(Rect r, string s, int size, Color c, TextAnchor anchor, FontStyle style)
        {
            textStyle.fontSize = size;
            textStyle.fontStyle = style;
            textStyle.alignment = anchor;
            textStyle.normal.textColor = c;
            textStyle.clipping = TextClipping.Overflow;
            GUI.Label(r, s, textStyle);
        }

        /// <summary>별(5각) 또는 원 모양의 흰색 텍스처를 코드로 만든다(외부 에셋 없음).</summary>
        static Texture2D MakeShapeTexture(bool star)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float rad = (i % 2 == 0) ? 0.48f : 0.20f;
                poly[i] = new Vector2(0.5f + Mathf.Cos(ang) * rad, 0.52f + Mathf.Sin(ang) * rad);
            }
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                    bool inside = star ? InPoly(poly, p) : (p - new Vector2(0.5f, 0.5f)).magnitude <= 0.48f;
                    tex.SetPixel(x, y, inside ? Color.white : new Color(1, 1, 1, 0));
                }
            }
            tex.Apply();
            return tex;
        }

        static bool InPoly(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
