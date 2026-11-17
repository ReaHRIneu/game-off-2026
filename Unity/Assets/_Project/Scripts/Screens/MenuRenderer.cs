using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 메인(방 일러스트)·부팅·바탕화면(의뢰 선택)·크레딧 화면 그리기(D-056~D-059).
    /// 배치는 Art/recipes/ui_scenes.py 의 목업 좌표를 따른다. 화면 상태 관리는 GameView가 맡는다.
    /// </summary>
    public sealed class MenuRenderer
    {
        // 아트 팔레트(tools/art/artkit.py PIX)와 같은 값
        static readonly Color32 K = new Color32(18, 20, 38, 255);
        static readonly Color32 Pa = new Color32(10, 10, 24, 255);
        static readonly Color32 B3 = new Color32(104, 114, 140, 255);
        static readonly Color32 Rd = new Color32(240, 72, 102, 255);
        static readonly Color32 Gn = new Color32(84, 224, 152, 255);
        static readonly Color32 Cy = new Color32(140, 210, 255, 255);
        static readonly Color32 Wh = new Color32(236, 246, 255, 255);
        static readonly Color32 Bb = new Color32(40, 96, 176, 255);
        static readonly Color32 Bp = new Color32(18, 54, 118, 255);

        public const int PanelTargetX = 352, PanelY = 16, PanelW = 280, PanelH = 318;

        readonly PixelCanvas cv;
        readonly RequestBoard board;

        public MenuRenderer(PixelCanvas canvas, RequestBoard board)
        {
            cv = canvas;
            this.board = board;
        }

        // ---------- 공통 부품 ----------

        /// <summary>시스템 키 안내(D-051): [배지] 설명을 나열한다.</summary>
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

        void Stars(int x, int y, int count)
        {
            for (int i = 0; i < 3; i++) cv.Blit(i < count ? "ui/star_full" : "ui/star_empty", x + i * 11, y);
        }

        // ---------- 메인 ----------

        void Room(bool on)
        {
            cv.Blit("scenes/room_layer0_wall", 0, 0);
            if (on) cv.Blit("scenes/room_layer3_light_on", 0, 0);
            cv.Blit("scenes/room_layer1_desk", 0, 0);
            cv.Blit(on ? "scenes/room_layer2_monitor_on" : "scenes/room_layer2_monitor_off", 0, 0);
        }

        public void DrawMain(int sel)
        {
            Room(false);
            cv.Blit("scenes/room_layer4_hacker", 0, 0);
            cv.BlitHalf("ui/logo_temp", 20, 18);   // 로고 정수 1/2 축소
            string[] items = { "START", "CREDITS" };
            for (int i = 0; i < items.Length; i++)
            {
                int bx = 32, by = 64 + i * 20;
                Button(bx, by, 84, items[i], i == sel);
                if (i == sel) cv.Text(bx - 10, by + 3, ">", Rd);
            }
            cv.Fill(0, 340, PixelCanvas.W, 20, Pa);
            Hint(12, 344, "↑↓", "SELECT", "ENTER", "OK");
        }

        /// <summary>모니터가 켜지고 화면 안에 부팅 글자가 차례로 나온다. stage 0~3.</summary>
        public void DrawBoot(int stage)
        {
            Room(true);
            cv.Blit("scenes/room_screen_boot_" + Mathf.Clamp(stage, 0, 3), 0, 0);
            cv.Blit("scenes/room_layer4_hacker_lit", 0, 0);
        }

        // ---------- 바탕화면 ----------

        static readonly string[] IconKey = { "ui/icon_file_clear", "ui/icon_file_new", "ui/icon_file_locked" };

        /// <summary>panelX가 0 미만이면 상세 패널 없음. connect가 0 이상이면 접속 창과 진행 막대(0~1).</summary>
        public void DrawDesktop(int sel, int panelX, float connect)
        {
            cv.Blit("scenes/desktop_wallpaper", 0, 0);
            for (int i = 0; i < board.Count; i++)
            {
                var st = board.State(i);
                bool locked = st == RequestState.Locked;
                int x = 24, y = 20 + i * 72;
                if (i == sel)
                {
                    cv.Fill(x - 6, y - 4, 57, 63, Pa);
                    cv.Box(x - 6, y - 4, x + 50, y + 58, Cy);
                }
                cv.Blit(IconKey[st == RequestState.Clear ? 0 : st == RequestState.New ? 1 : 2], x + 6, y);
                string name = "REQ_" + (i + 1).ToString("00");
                cv.Text(x + 22 - PixelCanvas.TextWidth(name) / 2, y + 36, name, locked ? B3 : Wh);
                Stars(x + 6, y + 46, board.Best(i));
            }

            // 작업 표시줄
            cv.Fill(0, 340, PixelCanvas.W, 20, Pa);
            cv.Fill(0, 340, PixelCanvas.W, 1, Cy);
            cv.Fill(4, 343, 49, 14, Bp);
            cv.Box(4, 343, 52, 356, Cy);
            cv.Text(13, 347, "SHIFT", Wh);
            if (panelX >= 0) Hint(62, 344, "↑↓", "SELECT", "ENTER", "CONNECT", "ESC", "CLOSE");
            else Hint(62, 344, "↑↓", "SELECT", "ENTER", "OPEN", "ESC", "LOG OFF");
            string clock = "21:04";   // 고정 글자
            cv.Text(PixelCanvas.W - 8 - PixelCanvas.TextWidth(clock), 347, clock, Wh);

            if (panelX >= 0) DrawPanel(sel, panelX);
            if (connect >= 0f) DrawConnect(connect);
        }

        void DrawPanel(int sel, int x0)
        {
            int y0 = PanelY;
            var e = board.Entry(sel);
            bool locked = board.State(sel) == RequestState.Locked;
            cv.Nine("ui/window_frame", x0, y0, PanelW, PanelH);
            cv.Text(x0 + 8, y0 + 7, "REQ_" + (sel + 1).ToString("00") + ".DAT", Wh);
            if (locked)
            {
                cv.Text(x0 + 10, y0 + 30, "LOCKED", B3, 2);
                return;
            }
            cv.Text(x0 + 10, y0 + 30, e.title, Wh, 2);
            cv.Text(x0 + 10, y0 + 50, "FROM: " + e.client, B3);
            cv.Fill(x0 + 10, y0 + 62, PanelW - 20, 1, Bb);
            string[] lines = e.summary.Split('\n');
            for (int i = 0; i < lines.Length; i++) cv.Text(x0 + 10, y0 + 70 + i * 11, lines[i], Wh);
            cv.Text(x0 + 10, y0 + 112, "MISSIONS", Cy);
            for (int i = 0; i < e.missionNames.Length; i++) cv.Text(x0 + 16, y0 + 126 + i * 12, "- " + e.missionNames[i], B3);
            cv.Text(x0 + 10, y0 + 172, "BEST", Cy);
            Stars(x0 + 42, y0 + 171, board.Best(sel));
            const int bw = 110;
            int bx = x0 + PanelW - bw - 34, by = y0 + PanelH - 36;
            Button(bx, by, bw, "CONNECT", true);
            cv.Blit("ui/badge_enter", bx + bw - 5, by + 8);
        }

        void DrawConnect(float ratio)
        {
            const int x0 = 180, y0 = 140;
            cv.Nine("ui/window_frame", x0, y0, 280, 70);
            cv.Text(x0 + 8, y0 + 7, "CONNECTING", Wh);
            cv.Text(x0 + 12, y0 + 30, "CONNECTING TO CAM 03...", Gn);
            // 진행 막대(칸 나뉜 게이지): 바깥 K, 테두리 파랑, 안쪽 어두운 바탕, 채움 분홍빨강
            int gx = x0 + 12, gy = y0 + 46, gw = 256;
            cv.Fill(gx, gy, gw, 9, K);
            cv.Box(gx + 1, gy + 1, gx + gw - 2, gy + 7, Bb);
            cv.Fill(gx + 2, gy + 2, gw - 4, 5, Pa);
            int fill = Mathf.RoundToInt((gw - 4) * Mathf.Clamp01(ratio));
            for (int i = 0; i < fill; i++)
                if (i % 4 != 3) cv.Fill(gx + 2 + i, gy + 2, 1, 5, Rd);
            if (fill > 2) cv.Fill(gx + 2, gy + 2, fill, 1, Wh);
        }

        // ---------- 크레딧 ----------

        public void DrawCredits()
        {
            cv.Blit("scenes/desktop_wallpaper", 0, 0);
            cv.Nine("ui/window_frame", 60, 60, 520, 200);
            cv.Text(68, 67, "CREDITS", Wh);
            string[] lines =
            {
                "MADE FOR GITHUB GAME OFF 2026",
                "TOOLS: UNITY 6, CLAUDE CODE",
                "AI DISCLOSURE: CODE AND PIXEL ART MADE WITH",
                "AI ASSISTANCE (CLAUDE CODE)",
                "CREATOR: TBD",
            };
            for (int i = 0; i < lines.Length; i++) cv.Text(76, 100 + i * 16, lines[i], Wh);
            cv.Fill(0, 340, PixelCanvas.W, 20, Pa);
            Hint(12, 344, "ESC", "BACK");
        }
    }
}
