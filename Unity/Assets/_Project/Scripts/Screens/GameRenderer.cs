using System.Collections.Generic;
using System.Text;
using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 의뢰(게임) 화면 그리기. 640x360 도트 캔버스 하나로 그린다(메뉴와 같은 경로).
    /// 좌표는 Art/recipes/ui_mvp.py 의 mockup()과 같다. 바뀌지 않는 부분(배경·창 틀·격자 타일)은 의뢰마다 한 번 그려
    /// 고정 층으로 저장하고, 매 프레임 그 위에 움직이는 것만 그린다. 화면 상태 관리는 GameView가 맡는다.
    /// </summary>
    public sealed class GameRenderer
    {
        // 아트 팔레트(tools/art/artkit.py PIX)와 같은 값
        static readonly Color32 K = new Color32(18, 20, 38, 255);
        static readonly Color32 Pa = new Color32(10, 10, 24, 255);
        static readonly Color32 B3 = new Color32(104, 114, 140, 255);
        static readonly Color32 B4 = new Color32(50, 56, 74, 255);
        static readonly Color32 Rd = new Color32(240, 72, 102, 255);
        static readonly Color32 Gn = new Color32(84, 224, 152, 255);
        static readonly Color32 Cy = new Color32(140, 210, 255, 255);
        static readonly Color32 Wh = new Color32(236, 246, 255, 255);
        static readonly Color32 Bb = new Color32(40, 96, 176, 255);

        // 오른쪽 패널 위치(목업의 px0, py0, pw)
        const int PX = 436, PY = 32, PW = 190;
        const int Tile = 32;
        const int MaxBubbleChars = 26;   // 말풍선 한 줄 최대 글자 수

        readonly PixelCanvas cv;
        Board layerBoard;        // 고정 층을 만든 격자(의뢰가 바뀌면 다시 만든다)
        Dir lastSide = Dir.Right; // 마지막 좌우 방향(위·아래 이동 중 유지)

        public GameRenderer(PixelCanvas canvas)
        {
            cv = canvas;
        }

        // ---------- 격자 좌표 ----------

        /// <summary>격자 왼쪽 위 x. 장면 패널 가운데(222)에 맞춘다(12칸이면 30).</summary>
        public static int GridX(Board b) => 222 - b.Width * Tile / 2;

        /// <summary>격자 왼쪽 위 y. 가운데 줄(176)에 맞춘다(5줄이면 96).</summary>
        public static int GridY(Board b) => 176 - b.Height * Tile / 2;

        // ---------- 그리기 ----------

        public void Draw(GameView v)
        {
            GameSim sim = v.Sim;
            if (layerBoard != sim.Board) BuildLayer(v);
            cv.RestoreLayer();

            DrawScene(v);
            DrawRightPanel(v);
            DrawFace(v);
            DrawBottom(v);
            // 게임오버 창 뒤에 0.0 이 남지 않도록 진행 중일 때만 카운트다운 숫자를 그린다
            if (sim.CountdownActive && sim.Outcome == Outcome.Running) DrawCountdown(sim);

            if (v.CurrentPhase == GameView.Phase.Paused) DrawPause(v);
            else if (v.CurrentPhase == GameView.Phase.Ended) DrawResult(v);
        }

        /// <summary>바뀌지 않는 부분을 그려 고정 층으로 저장한다.</summary>
        void BuildLayer(GameView v)
        {
            Board b = v.Sim.Board;
            cv.Blit("backgrounds/city_night_composite", 0, 0);
            cv.Nine("ui/window_frame", 8, 8, 624, 344);
            cv.Text(16, 15, "SHIFT://REMOTE  -  REQUEST " + (v.ActiveIndex + 1).ToString("00"), Wh);
            cv.Blit("ui/icon_pause", 596, 11);
            cv.Blit("ui/badge_esc", 602, 17);

            // 현실 장면 패널과 현실 타일
            cv.Nine("ui/panel", 14, 32, 416, 278);
            cv.Text(22, 38, "FLOOR VIEW / CAM 03", Cy);
            int ox = GridX(b), oy = GridY(b);
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                    cv.Blit(RealityKey(b.Reality[x, y]), ox + x * Tile, oy + y * Tile);

            // 얼굴 패널 틀(D-042, D-049)
            cv.Nine("ui/panel", PX, 182, PW, 128);
            Frame(PX + 6, 194, 102, 102);
            cv.Text(PX + 114, 196, "HACKER", Cy);
            cv.Text(PX + 114, 206, "???", B3);

            // 아래 줄 틀
            cv.Nine("ui/panel", 14, 314, 200, 30);
            cv.Text(22, 320, "TRACE", Cy);
            cv.Nine("ui/panel", 220, 314, 406, 30);

            cv.SaveLayer();
            layerBoard = b;
            lastSide = Dir.Right;
        }

        static string RealityKey(Cell c)
        {
            switch (c.Kind)
            {
                case CellKind.Wall: return "tiles/reality_wall";
                case CellKind.DoorClosed: return "tiles/reality_door_closed";
                case CellKind.DoorOpen: return "tiles/reality_door_open";
                case CellKind.Arrow: return "tiles/reality_arrow_" + c.Dir.ToString().ToLowerInvariant();
                case CellKind.Exit: return "tiles/reality_exit";
                case CellKind.CheckA:
                case CellKind.CheckB: return "tiles/reality_checkpoint";
                case CellKind.Relay: return "tiles/reality_relay";
                default: return "tiles/reality_floor";
            }
        }

        /// <summary>설계도 층 타일. 그림이 없는 칸 종류(출구·체크 지점·중계기)는 null.</summary>
        static string BlueprintKey(Cell c)
        {
            switch (c.Kind)
            {
                case CellKind.Floor: return "tiles/blueprint_floor";
                case CellKind.Wall: return "tiles/blueprint_wall";
                case CellKind.DoorClosed: return "tiles/blueprint_door_closed";
                case CellKind.DoorOpen: return "tiles/blueprint_door_open";
                case CellKind.Arrow: return "tiles/blueprint_arrow_" + c.Dir.ToString().ToLowerInvariant();
                default: return null;
            }
        }

        /// <summary>2px 테두리 패널(ui/panel과 같은 모양, 안쪽 바탕만 어둡게). 초상 틀에 쓴다.</summary>
        void Frame(int x, int y, int w, int h)
        {
            cv.Fill(x, y, w, h, Pa);
            cv.Box(x, y, x + w - 1, y + h - 1, K);
            cv.Box(x + 1, y + 1, x + w - 2, y + h - 2, Bb);
            cv.Fill(x + 1, y + 1, 1, 1, Cy);
            cv.Fill(x + w - 2, y + 1, 1, 1, Cy);
            cv.Fill(x + 1, y + h - 2, 1, 1, Cy);
            cv.Fill(x + w - 2, y + h - 2, 1, 1, Cy);
        }

        // ---------- 장면 ----------

        void DrawScene(GameView v)
        {
            GameSim sim = v.Sim;
            Board b = sim.Board;
            int ox = GridX(b), oy = GridY(b);

            // 설계도 겹침(D-015): Shift를 누르는 동안 조준한 칸 위에. 현실 타일은 아래에 그대로 보인다.
            Vector2Int aim = sim.Aim;
            if (sim.ShiftHeld)
            {
                string bk = BlueprintKey(b.Blueprint[aim.x, aim.y]);
                if (bk != null) cv.Blit(bk, ox + aim.x * Tile, oy + aim.y * Tile);
            }

            if (sim.TargetDir == Dir.Left || sim.TargetDir == Dir.Right) lastSide = sim.TargetDir;
            Vector2Int t = sim.TargetPos;
            cv.Blit(lastSide == Dir.Left ? "sprites/courier_left" : "sprites/courier_right", ox + t.x * Tile, oy + t.y * Tile);
            cv.Blit("sprites/cursor", ox + aim.x * Tile, oy + aim.y * Tile);
        }

        void DrawCountdown(GameSim sim)
        {
            string s = sim.Countdown.ToString("0.0");
            const int scale = 3;
            cv.Text(430 - 8 - PixelCanvas.TextWidth(s, scale), 40, s, Rd, scale);
        }

        // ---------- 오른쪽 위 패널 ----------

        void DrawRightPanel(GameView v)
        {
            bool mission = v.CurrentTab == GameView.Tab.Mission;
            // 탭을 먼저 그리고 패널 틀이 탭 아래 줄을 덮는다(목업과 같은 순서)
            cv.Nine(mission ? "ui/tab_active" : "ui/tab_inactive", PX, PY, 70, 14);
            cv.Text(PX + 8, PY + 4, "MISSION", mission ? Wh : B4);
            cv.Nine(mission ? "ui/tab_inactive" : "ui/tab_active", PX + 72, PY, 46, 14);
            cv.Text(PX + 81, PY + 4, "LOG", mission ? B4 : Wh);
            cv.Nine("ui/panel", PX, PY + 13, PW, 132);
            cv.Blit("ui/icon_tabswitch", PX + PW - 26, PY + 1);
            cv.Blit("ui/badge_bracket", PX + PW - 26 + 6, PY + 1 + 6);

            if (mission) DrawMission(v);
            else DrawLog(v);
        }

        void DrawMission(GameView v)
        {
            GameSim sim = v.Sim;
            RequestEntry e = v.Desk.Entry(v.ActiveIndex);
            // 미션 목록 순서: 최종 미션, 체크 지점 A, B (RequestEntry.missionNames와 같은 순서)
            bool[] done = { sim.MissionFinal, sim.MissionA, sim.MissionB };
            int n = Mathf.Min(done.Length, e.missionNames.Length);
            for (int i = 0; i < n; i++)
            {
                int y = PY + 24 + i * 16;
                cv.Blit(done[i] ? "ui/checkbox_done" : "ui/checkbox_empty", PX + 8, y);
                cv.Text(PX + 22, y + 1, e.missionNames[i], done[i] ? B3 : Wh);
            }
            cv.Text(PX + 8, PY + 82, "RATING", Cy);
            // 게임오버면 예상 별 대신 빈 별(실패한 의뢰에 별이 보이지 않게)
            int stars = sim.Outcome == Outcome.GameOver ? 0 : sim.ProjectedStars;
            for (int i = 0; i < 3; i++)
                cv.Blit(i < stars ? "ui/star_full" : "ui/star_empty", PX + 52 + i * 12, PY + 81);
        }

        // 말풍선 한 개의 배치 정보
        struct Bubble
        {
            public DialogueLine line;
            public List<string> rows;
            public int h => 13 + 7 + 9 * (rows.Count - 1);
        }

        void DrawLog(GameView v)
        {
            bool next = v.InDialogue;
            int top = PY + 20;
            int bottom = next ? PY + 122 : PY + 13 + 132 - 4;   // 이 선보다 아래로 내려가면 오래된 말풍선부터 밀어 올림
            var items = new List<Bubble>();
            foreach (DialogueLine line in v.Shown)
                items.Add(new Bubble { line = line, rows = Wrap(line.text, MaxBubbleChars) });

            int first = 0;
            while (first < items.Count - 1 && TotalHeight(items, first) > bottom - top) first++;

            int y = top;
            for (int i = first; i < items.Count; i++)
            {
                Bubble bb = items[i];
                bool me = bb.line.speaker == Speaker.Me;
                string who = me ? "ME" : "CLIENT";
                string label = who + "  " + bb.line.time;
                int tw = 0;
                foreach (string r in bb.rows) tw = Mathf.Max(tw, PixelCanvas.TextWidth(r));
                int bw = tw + 16;   // 말풍선 그림 폭 = 글자 폭 + 꼬리 4 + 안쪽 여백 6 + 6
                int by = y + 9;
                if (me)
                {
                    int bx = PX + PW - 6 - bw;
                    cv.Text(PX + PW - 17 - PixelCanvas.TextWidth(label), y, label, Cy);
                    cv.Nine("ui/bubble_right", bx, by, bw, bb.h);
                    for (int r = 0; r < bb.rows.Count; r++) cv.Text(bx + 6, by + 7 + r * 9, bb.rows[r], Wh);
                }
                else
                {
                    int bx = PX + 6;
                    cv.Text(PX + 8, y, label, B3);
                    cv.Nine("ui/bubble_left", bx, by, bw, bb.h);
                    for (int r = 0; r < bb.rows.Count; r++) cv.Text(bx + 10, by + 7 + r * 9, bb.rows[r], Wh);
                }
                y += 9 + bb.h + 7;
            }

            if (next)
            {
                int nx = PX + PW - 78, ny = PY + 124;
                cv.Nine("ui/button_selected", nx, ny, 44, 13);
                cv.Text(nx + 6, ny + 3, "NEXT", Wh);
                cv.Blit("ui/badge_enter", nx + 39, ny + 8);
            }
        }

        static int TotalHeight(List<Bubble> items, int from)
        {
            int total = 0;
            for (int i = from; i < items.Count; i++) total += 9 + items[i].h + (i > from ? 7 : 0);
            return total;
        }

        /// <summary>단어 단위로 maxChars 글자 안에서 줄바꿈한다.</summary>
        static List<string> Wrap(string text, int maxChars)
        {
            var rows = new List<string>();
            var sb = new StringBuilder();
            foreach (string word in (text ?? "").Split(' '))
            {
                if (sb.Length > 0 && sb.Length + 1 + word.Length > maxChars)
                {
                    rows.Add(sb.ToString());
                    sb.Clear();
                }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(word);
            }
            rows.Add(sb.ToString());
            return rows;
        }

        // ---------- 오른쪽 아래 얼굴 ----------

        void DrawFace(GameView v)
        {
            string key = "portraits/hacker_" + v.FaceKey;
            if (!cv.Has(key)) key = "portraits/hacker_neutral";
            cv.Blit(key, PX + 9, 197);
        }

        // ---------- 아래 줄 ----------

        void DrawBottom(GameView v)
        {
            GameSim sim = v.Sim;
            // 추적도 게이지: 칸 나뉜 빨간 막대(ui_mvp.py gauge()와 같은 모양)
            int gx = 66, gy = 319, gw = 140;
            cv.Fill(gx, gy, gw, 9, K);
            cv.Box(gx + 1, gy + 1, gx + gw - 2, gy + 7, Bb);
            cv.Fill(gx + 2, gy + 2, gw - 4, 5, Pa);
            int fill = Mathf.RoundToInt((gw - 4) * Mathf.Clamp01(sim.Tracking / sim.TrackingMax));
            for (int i = 0; i < fill; i++)
                if (i % 4 != 3) cv.Fill(gx + 2 + i, gy + 2, 1, 5, Rd);
            if (fill > 2) cv.Fill(gx + 2, gy + 2, fill, 1, Wh);

            // 명령 기록 줄: 칸에 들어가는 만큼 최근 것만
            const int maxWidth = 406 - 16;
            IReadOnlyList<string> log = v.CommandLog;
            int start = 0;
            while (start < log.Count - 1 && JoinedWidth(log, start) > maxWidth) start++;
            var sb = new StringBuilder();
            for (int i = start; i < log.Count; i++)
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(log[i]);
            }
            cv.Text(228, 325, sb.ToString(), Gn);
        }

        static int JoinedWidth(IReadOnlyList<string> log, int from)
        {
            int chars = 0;
            for (int i = from; i < log.Count; i++) chars += log[i].Length + (i > from ? 3 : 0);
            return chars == 0 ? 0 : chars * 6 - 1;
        }

        // ---------- 일시정지·결과 창 ----------

        void Hint(int x, int y, params string[] items)
        {
            for (int i = 0; i < items.Length; i += 2)
            {
                string key = items[i], label = items[i + 1];
                int kw;
                string img = key == "ENTER" ? "ui/badge_enter" : key == "ESC" ? "ui/badge_esc" : null;
                if (img != null) { cv.Blit(img, x, y); kw = cv.WidthOf(img); }
                else
                {
                    kw = PixelCanvas.TextWidth(key) + 6;   // 그림이 없는 키(↑↓)는 같은 규격으로 직접 그림
                    cv.Fill(x, y, kw, 11, K);
                    cv.Box(x, y, x + kw - 1, y + 10, B3);
                    cv.Text(x + 3, y + 2, key, Wh);
                }
                x += kw + 3;
                x += cv.Text(x, y + 2, label, B3) + 10;
            }
        }

        void Button(int x, int y, int w, string label, bool selected)
        {
            cv.Nine(selected ? "ui/button_selected" : "ui/button", x, y, w, 13);
            cv.Text(x + (w - PixelCanvas.TextWidth(label)) / 2, y + 3, label, Wh);
        }

        void DrawPause(GameView v)
        {
            const int w = 160, h = 112;
            int x = 222 - w / 2, y = 171 - h / 2;
            cv.Nine("ui/window_frame", x, y, w, h);
            cv.Text(x + 8, y + 7, "PAUSED", Wh);
            string[] items = { "CONTINUE", "RESTART", "DESKTOP" };
            for (int i = 0; i < items.Length; i++)
                Button(x + (w - 110) / 2, y + 30 + i * 18, 110, items[i], i == v.PauseIndex);
            Hint(x + 10, y + h - 17, "↑↓", "SELECT", "ENTER", "OK");
        }

        void DrawResult(GameView v)
        {
            GameSim sim = v.Sim;
            const int w = 200, h = 112;
            int x = 222 - w / 2, y = 171 - h / 2;
            bool clear = sim.Outcome == Outcome.Complete;
            cv.Nine("ui/window_frame", x, y, w, h);
            if (clear)
            {
                cv.Text(x + 8, y + 7, "REQUEST COMPLETE", Wh);
                int stars = sim.Stars;
                int sx = x + (w - (3 * 18 + 2 * 4)) / 2;
                for (int i = 0; i < 3; i++)
                    cv.BlitScaled(i < stars ? "ui/star_full" : "ui/star_empty", sx + i * 22, y + 32, 2);
                string s = stars + " / 3 STARS";
                cv.Text(x + (w - PixelCanvas.TextWidth(s)) / 2, y + 58, s, Wh);
            }
            else
            {
                cv.Text(x + 8, y + 7, "GAME OVER", Wh);
                string a = "TRACED.", b = "THE REQUEST FAILED.";
                cv.Text(x + (w - PixelCanvas.TextWidth(a, 2)) / 2, y + 32, a, Rd, 2);
                cv.Text(x + (w - PixelCanvas.TextWidth(b)) / 2, y + 56, b, B3);
            }
            int bw = 110, bx = x + (w - bw) / 2, by = y + h - 30;
            Button(bx, by, bw, clear ? "DESKTOP" : "RESTART", true);
            cv.Blit("ui/badge_enter", bx + bw - 5, by + 8);
        }
    }
}
