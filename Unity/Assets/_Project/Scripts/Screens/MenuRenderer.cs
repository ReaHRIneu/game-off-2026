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

        /// <summary>키 배지 하나를 그리고 폭을 돌려준다. ENTER·ESC는 그림, 그 밖의 키(↑↓ 등)는 같은 규격으로 직접 그린다.</summary>
        int Badge(int x, int y, string key)
        {
            string img = key == "ENTER" ? "ui/badge_enter" : key == "ESC" ? "ui/badge_esc" : null;
            if (img != null) { cv.Blit(img, x, y); return cv.WidthOf(img); }
            int kw = PixelCanvas.TextWidth(key) + 6;
            cv.Fill(x, y, kw, 11, K);
            cv.Box(x, y, x + kw - 1, y + 10, B3);
            cv.Text(x + 3, y + 2, key, Wh);
            return kw;
        }

        /// <summary>시스템 키 안내(D-051): [배지] 설명을 나열한다.</summary>
        void Hint(int x, int y, params string[] items)
        {
            for (int i = 0; i < items.Length; i += 2)
            {
                x += Badge(x, y, items[i]) + 3;
                x += cv.Text(x, y + 2, items[i + 1], B3) + 10;
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

        /// <summary>메인 화면(W-019, D-105). items는 위에서 아래 순서의 메뉴 글자. confirm이 true면 초기화 확인 창(confirmSel 0 = NO, 1 = YES)을 위에 얹는다.</summary>
        public void DrawMain(int sel, string[] items, bool confirm = false, int confirmSel = 0)
        {
            DrawMainBase();
            for (int i = 0; i < items.Length; i++)
            {
                int bx = 32, by = 50 + i * 14;   // 메뉴 4~5개가 방 그림(파란 보관함, y 약 117)과 겹치지 않게
                Button(bx, by, 84, items[i], i == sel && !confirm);
                if (i == sel && !confirm) cv.Text(bx - 10, by + 3, ">", Rd);
            }
            if (confirm)
            {
                const int w = 190, h = 66;
                int x = (PixelCanvas.W - w) / 2, y = 124;
                cv.Nine("ui/window_frame", x, y, w, h);
                const string q = "ERASE ALL PROGRESS?";
                cv.Text(x + (w - PixelCanvas.TextWidth(q)) / 2, y + 12, q, Wh);
                Button(x + 24, y + 38, 60, "NO", confirmSel == 0);
                Button(x + w - 24 - 60, y + 38, 60, "YES", confirmSel == 1);
            }
            DrawBand(confirm ? new[] { "←→", "SELECT", "ENTER", "OK", "ESC", "BACK" } : new[] { "↑↓", "SELECT", "ENTER", "OK" });
        }

        void DrawMainBase()
        {
            Room(false);
            cv.Blit("scenes/room_layer4_hacker", 0, 0);
            cv.BlitHalf("ui/logo_temp", 20, 18);   // 로고 정수 1/2 축소
        }

        void DrawBand(string[] hint)
        {
            cv.Fill(0, 340, PixelCanvas.W, 20, Pa);
            Hint(12, 344, hint);
        }

        // ---------- 설정(W-019, D-105): 메인·일시정지 공용 ----------

        public const int SettingsRows = 6;
        static readonly string[] SettingsLabels = { "BGM", "SFX", "ENV FX", "GLITCH", "CONTROLS", "BACK" };
        public const int SettingsW = 212, SettingsH = 140;

        /// <summary>메인에서 연 설정 화면: 방 일러스트 위 왼쪽 창 + 아래 키 안내 띠.</summary>
        public void DrawSettingsMain(GameView v)
        {
            DrawMainBase();
            DrawSettings(v, 16, 60);
            if (v.ControlsOpen) DrawControls(PixelCanvas.W / 2, 170);
            DrawBand(v.ControlsOpen ? new[] { "ESC", "BACK" } : new[] { "↑↓", "SELECT", "←→", "CHANGE", "ENTER", "OK", "ESC", "BACK" });
        }

        /// <summary>설정 창(왼쪽 위 x, y). 일시정지에서는 GameRenderer가 일시정지 창 자리에 부른다.</summary>
        public void DrawSettings(GameView v, int x, int y)
        {
            const int w = SettingsW, h = SettingsH;
            cv.Nine("ui/window_frame", x, y, w, h);
            cv.Text(x + 8, y + 7, "SETTINGS", Wh);
            for (int i = 0; i < SettingsRows; i++)
            {
                bool sel = i == v.SettingsIndex && !v.ControlsOpen;
                int ry = y + 24 + i * 16;
                if (i >= 4) { Button(x + 10, ry, w - 20, SettingsLabels[i], sel); continue; }
                cv.Nine(sel ? "ui/button_selected" : "ui/button", x + 10, ry, w - 20, 13);
                cv.Text(x + 16, ry + 3, SettingsLabels[i], Wh);
                int right = x + w - 16;
                if (i < 2)
                {
                    int val = i == 0 ? v.Progress.BgmVolume : v.Progress.SfxVolume;
                    string num = val.ToString();
                    cv.Text(right - PixelCanvas.TextWidth(num), ry + 3, num, Wh);
                    int bx = right - PixelCanvas.TextWidth("10") - 6 - (ProgressStore.VolumeMax * 6 - 1);
                    for (int c = 0; c < ProgressStore.VolumeMax; c++)
                        cv.Fill(bx + c * 6, ry + 3, 5, 7, c < val ? Cy : Bp);
                }
                else
                {
                    string val = i == 2 ? (v.Progress.EnvFxReduced ? "OFF" : "ON")
                        : v.Progress.GlitchSetting == GlitchStrength.Off ? "OFF" : v.Progress.GlitchSetting == GlitchStrength.Weak ? "LOW" : "NORMAL";
                    if (sel) val = "< " + val + " >";
                    cv.Text(right - PixelCanvas.TextWidth(val), ry + 3, val, sel ? Cy : Wh);
                }
            }
            if (!v.ControlsOpen && v.SettingsFromPause)
                Hint(x + 10, y + h - 17, "↑↓", "SELECT", "←→", "CHANGE", "ESC", "BACK");
        }

        /// <summary>조작법 창: 중심 (cx, cy). 실제 입력 처리(GameInput)와 같은 키만 적는다.</summary>
        public void DrawControls(int cx, int cy)
        {
            const int w = 284, h = 136;
            int x = cx - w / 2, y = cy - h / 2;
            cv.Nine("ui/window_frame", x, y, w, h);
            cv.Text(x + 8, y + 7, "CONTROLS", Wh);
            string[] keys = { "ARROWS", "L-SHIFT", "L-SHIFT + ARROW", "]", "ESC", "ENTER" };
            string[] labels = { "AIM SECTOR", "HOLD TO SHIFT (BLUEPRINT)", "PUSH ROOM", "MISSION / LOG TAB", "PAUSE / BACK", "OK" };
            for (int i = 0; i < keys.Length; i++)
            {
                int ry = y + 24 + i * 14;
                Badge(x + 10, ry, keys[i]);
                cv.Text(x + 10 + 104, ry + 2, labels[i], Wh);
            }
            Hint(x + 10, y + h - 17, "ESC", "BACK");
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

        /// <summary>
        /// 바탕화면 그리기(W-005). inFolder가 false면 챕터 폴더 화면(folder = 선택한 폴더), true면 그 챕터의 의뢰 목록(sel = 선택한 의뢰).
        /// panelX가 0 미만이면 상세 패널 없음. connect가 0 이상이면 접속 창과 진행 막대(0~1).
        /// </summary>
        public void DrawDesktop(bool inFolder, int folder, int sel, int panelX, float connect)
        {
            cv.Blit("scenes/desktop_wallpaper", 0, 0);
            if (inFolder) DrawRequestIcons(folder, sel);
            else DrawFolderIcons(folder);

            // 작업 표시줄
            cv.Fill(0, 340, PixelCanvas.W, 20, Pa);
            cv.Fill(0, 340, PixelCanvas.W, 1, Cy);
            cv.Fill(4, 343, 49, 14, Bp);
            cv.Box(4, 343, 52, 356, Cy);
            cv.Text(13, 347, "SHIFT", Wh);
            if (panelX >= 0)
            {
                if (inFolder) Hint(62, 344, "↑↓", "SELECT", "ENTER", "CONNECT", "ESC", "CLOSE");
                else Hint(62, 344, "↑↓", "SELECT", "ESC", "CLOSE");   // 잠긴 폴더 패널
            }
            else if (inFolder) Hint(62, 344, "↑↓", "SELECT", "ENTER", "OPEN", "ESC", "BACK");
            else Hint(62, 344, "↑↓", "SELECT", "ENTER", "OPEN", "ESC", "LOG OFF");
            string clock = "21:04";   // 고정 글자
            cv.Text(PixelCanvas.W - 8 - PixelCanvas.TextWidth(clock), 347, clock, Wh);

            if (panelX >= 0)
            {
                if (inFolder) DrawPanel(sel, panelX);
                else DrawFolderPanel(folder, panelX);
            }
            if (connect >= 0f) DrawConnect(connect);
        }

        /// <summary>챕터 폴더 아이콘 세로 배치(열림·잠김). 경로 글자는 없다.</summary>
        void DrawFolderIcons(int sel)
        {
            for (int i = 0; i < RequestBoard.ChapterCount; i++)
            {
                bool locked = !board.ChapterOpen(i);
                int x = 24, y = 20 + i * 72;
                if (i == sel)
                {
                    cv.Fill(x - 14, y - 4, 73, 53, Pa);
                    cv.Box(x - 14, y - 4, x + 58, y + 48, Cy);
                }
                cv.Blit(locked ? "ui/icon_folder_locked" : "ui/icon_folder_open", x + 6, y);
                string name = RequestBoard.ChapterName(i);
                cv.Text(x + 22 - PixelCanvas.TextWidth(name) / 2, y + 36, name, locked ? B3 : Wh);
            }
        }

        /// <summary>챕터 폴더 안의 의뢰 아이콘 목록. 위쪽에 경로 글자(/CH0_SANDBOX/).</summary>
        void DrawRequestIcons(int folder, int sel)
        {
            cv.Text(8, 6, "/" + RequestBoard.ChapterName(folder) + "/", Cy);
            var items = board.InChapter(folder);
            for (int pos = 0; pos < items.Count; pos++)
            {
                int i = items[pos];
                var st = board.State(i);
                bool locked = st == RequestState.Locked;
                int x = 24, y = 20 + pos * 72;
                if (i == sel)
                {
                    cv.Fill(x - 6, y - 4, 57, 63, Pa);
                    cv.Box(x - 6, y - 4, x + 50, y + 58, Cy);
                }
                cv.Blit(IconKey[st == RequestState.Clear ? 0 : st == RequestState.New ? 1 : 2], x + 6, y);
                string name = board.RequestName(i);
                cv.Text(x + 22 - PixelCanvas.TextWidth(name) / 2, y + 36, name, locked ? B3 : Wh);
                if (!board.IsTutorial(i)) Stars(x + 6, y + 46, board.Best(i));   // W-008: 튜토리얼 의뢰는 별 없음
            }
        }

        /// <summary>잠긴 챕터 폴더의 상세 패널: LOCKED만 보인다(잠긴 의뢰와 같은 방식).</summary>
        void DrawFolderPanel(int folder, int x0)
        {
            int y0 = PanelY;
            cv.Nine("ui/window_frame", x0, y0, PanelW, PanelH);
            cv.Text(x0 + 8, y0 + 7, RequestBoard.ChapterName(folder), Wh);
            cv.Text(x0 + 10, y0 + 30, "LOCKED", B3, 2);
        }

        void DrawPanel(int sel, int x0)
        {
            int y0 = PanelY;
            var e = board.Entry(sel);
            bool locked = board.State(sel) == RequestState.Locked;
            cv.Nine("ui/window_frame", x0, y0, PanelW, PanelH);
            cv.Text(x0 + 8, y0 + 7, board.RequestName(sel) + ".DAT", Wh);
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
            if (!board.IsTutorial(sel))   // W-008: 튜토리얼 의뢰는 BEST 별 없음
            {
                cv.Text(x0 + 10, y0 + 172, "BEST", Cy);
                Stars(x0 + 42, y0 + 171, board.Best(sel));
            }
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
