using System.Collections.Generic;
using System.Text;
using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 의뢰(게임) 화면 그리기. 640x360 도트 캔버스 하나로 그린다(메뉴와 같은 경로).
    /// 좌표는 Art/recipes/ui_mvp.py 의 mockup()과 scene_building.py 의 game_mockup()과 같다. 바뀌지 않는 부분(배경·창 틀·패널)은 의뢰마다 한 번 그려
    /// 고정 층으로 저장하고, 매 프레임 그 위에 장면(측면 단면도를 카메라로 잘라 보여 줌)과 움직이는 것만 그린다. 화면 상태 관리는 GameView가 맡는다.
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
        // 장면 뷰(목업 기준 16,48 ~ 427,307)
        const int VX = 16, VY = 48, VW = GameSim.ViewW, VH = GameSim.ViewH;
        static readonly Color32 Gd = new Color32(36, 128, 92, 255);   // 출구 문(팔레트 g)
        const int MaxBubbleChars = 26;   // 말풍선 한 줄 최대 글자 수

        readonly PixelCanvas cv;
        Board layerBoard;        // 고정 층을 만든 격자(의뢰가 바뀌면 다시 만든다)

        public GameRenderer(PixelCanvas canvas)
        {
            cv = canvas;
        }

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

            // 장면 패널(안쪽 뷰는 매 프레임 DrawScene이 그린다)
            cv.Nine("ui/panel", 14, 32, 416, 278);
            cv.Text(22, 38, "FLOOR VIEW / CAM 03", Cy);

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
            int cx = sim.CamX, cy = sim.CamY;

            cv.Fill(VX, VY, VW, VH, Pa);
            cv.SetClip(VX, VY, VW, VH);
            cv.BlitPart(b.BackgroundKey, cx, cy, VW, VH, VX, VY);

            // 현실 오브젝트(그림 아래 끝이 바닥선에 닿음)
            foreach (SectorObject o in b.Objects)
            {
                if (!o.Def.drawSprite) continue;
                DrawObject(b, o, cx, cy);
            }
            // 섹터 전체형: 지금 유효 상태에서 차단막이 켜져 있을 때만 그린다(꺼짐은 그리지 않음)
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++)
                    if (b.IsWhole(f, c) && sim.SectorBlocked(f, c))
                        cv.Blit("objects/blocker_on", VX + b.SectorLeft(c) - cx, VY + b.SectorTop(f) - cy);

            // 대상: 몸 중심 x를 가운데로, 그림 아래 끝이 바닥선에 닿음
            int tx = VX + Mathf.RoundToInt(sim.TargetX) - 16 - cx;
            int ty = VY + b.FloorLineY(sim.TargetFloor) - 32 - cy;
            cv.Blit(sim.TargetFacingRight ? "sprites/courier_right" : "sprites/courier_left", tx, ty);

            // 설계도 겹침(D-015): Shift를 누르는 동안 조준한 섹터 위에. 현실 그림은 아래에 그대로 보인다.
            Vector2Int aim = sim.Aim;
            if (sim.ShiftHeld)
            {
                cv.Blit("objects/bp_sector", VX + b.SectorLeft(aim.x) - cx, VY + b.SectorTop(aim.y) - cy);
                if (!b.IsWhole(aim.y, aim.x))
                    foreach (SectorObject o in b.Objects)
                        if (o.Floor == aim.y && o.Col == aim.x) DrawBlueprintObject(b, o, cx, cy);
            }
            cv.Blit("objects/sector_cursor", VX + b.SectorLeft(aim.x) - cx, VY + b.SectorTop(aim.y) - cy);
            cv.ClearClip();

            cv.Box(15, 47, 428, 308, Bb);
            DrawTargetMarker(sim, tx, ty);
        }

        static string ObjectKey(SectorObject o)
        {
            switch (o.Def.kind)
            {
                case ObjectKind.Door: return o.Def.realityOn ? "objects/door_open" : "objects/door_closed";
                case ObjectKind.Stairs: return o.Def.realityOn ? "objects/shutter_open" : "objects/shutter_closed";
                case ObjectKind.Elevator: return o.Def.realityOn ? "objects/elevator_running" : "objects/elevator_stopped";
                case ObjectKind.Terminal: return "objects/terminal";
                case ObjectKind.Relay: return "objects/relay_box";
                case ObjectKind.Exit: return "objects/exit_sign";
            }
            return null;
        }

        /// <summary>설계도 상태 그림(있는 것만): 문·계단·엘리베이터. 현실과 다른 상태일 때 겹쳐 그린다.</summary>
        static string BlueprintObjectKey(SectorObject o)
        {
            if (o.Def.realityOn == o.Def.blueprintOn) return null;
            switch (o.Def.kind)
            {
                case ObjectKind.Door: return "objects/bp_door_open";
                case ObjectKind.Stairs: return "objects/bp_shutter_open";
                case ObjectKind.Elevator: return "objects/bp_elevator_running";
            }
            return null;
        }

        void DrawObject(Board b, SectorObject o, int cx, int cy)
        {
            string key = ObjectKey(o);
            if (key == null) return;
            int fy = b.FloorLineY(o.Floor);
            int x = VX + o.DrawAbsX - cx;
            if (o.Def.kind == ObjectKind.Exit)
            {
                // 초록 문(시안 scene_building.py: 섹터 x 14~30, 높이 40) + 표지(문 위 58px)
                cv.Fill(x + 6, VY + fy - 40 - cy, 17, 40, Gd);
                cv.Blit(key, x, VY + fy - 58 - cy);
                return;
            }
            cv.Blit(key, x, VY + fy - cv.HeightOf(key) - cy);
        }

        void DrawBlueprintObject(Board b, SectorObject o, int cx, int cy)
        {
            string key = BlueprintObjectKey(o);
            if (key == null) return;
            // 설계도 그림은 실루엣보다 사방 1px 크다
            cv.Blit(key, VX + o.DrawAbsX - 1 - cx, VY + b.FloorLineY(o.Floor) + 1 - cv.HeightOf(key) - cy);
        }

        /// <summary>대상이 장면 뷰 밖이면 그 방향 가장자리에 빨간 표시(◀ TARGET 등). 가로로 벗어나면 가로를 우선한다.</summary>
        void DrawTargetMarker(GameSim sim, int tx, int ty)
        {
            const int body = 9;                       // 몸통 절반 폭(Board.BodyHalf)
            int bx0 = tx + 16 - body, bx1 = tx + 16 + body, by0 = ty, by1 = ty + 32;
            bool outLeft = bx1 < VX, outRight = bx0 >= VX + VW, outUp = by1 < VY, outDown = by0 >= VY + VH;
            if (!(outLeft || outRight || outUp || outDown)) return;

            const string label = "TARGET";
            int tw = PixelCanvas.TextWidth(label);
            int yc = Mathf.Clamp(ty + 32, VY + 8, VY + VH - 8);
            int xc = Mathf.Clamp(tx + 16, VX + 8, VX + VW - 8);
            if (outLeft || outRight)
            {
                for (int i = 0; i < 5; i++)
                {
                    int x = outLeft ? VX + 4 + i : VX + VW - 5 - i;
                    cv.Fill(x, yc - i, 1, 2 * i + 1, Rd);
                }
                if (outLeft) cv.Text(VX + 14, yc - 3, label, Rd);
                else cv.Text(VX + VW - 14 - tw, yc - 3, label, Rd);
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    int y = outUp ? VY + 4 + i : VY + VH - 5 - i;
                    cv.Fill(xc - i, y, 2 * i + 1, 1, Rd);
                }
                int tx0 = Mathf.Clamp(xc - tw / 2, VX + 2, VX + VW - tw - 2);
                if (outUp) cv.Text(tx0, VY + 12, label, Rd);
                else cv.Text(tx0, VY + VH - 20, label, Rd);
            }
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
