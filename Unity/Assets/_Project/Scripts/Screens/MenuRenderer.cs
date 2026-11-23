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


        readonly PixelCanvas cv;
        readonly RequestBoard board;

        public MenuRenderer(PixelCanvas canvas, RequestBoard board)
        {
            cv = canvas;
            cv.EnsureFxTextures();   // W-028: CRT 켜짐 가장자리 디더 무늬(코드 생성)
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

        /// <summary>
        /// 게임 시작 CRT 켜짐(W-028, D-109 ⑤). stage 1: 메인 그림이 검게 페이드 아웃, 2: 검은 화면 가운데 가로선이 좌우로 펼쳐짐,
        /// 3: 가로선에서 위아래로 빛이 번지며 화면 높이까지 열림(가장자리는 디더). stageSeconds는 그 단계가 시작된 뒤 지난 시간. 모두 30 FPS 틱 단위.
        /// </summary>
        public void DrawCrt(int stage, float stageSeconds, int sel, string[] items)
        {
            const int W = PixelCanvas.W, H = PixelCanvas.H, Z = PixelCanvas.Z;
            int t = Fx.TickOf(stageSeconds);
            if (stage == 1)
            {
                DrawMain(sel, items);
                cv.FillAlpha(0, 0, W, H, Pa, Mathf.Clamp01((t + 1) / (float)Fx.CrtFadeTicks));
                return;
            }
            if (stage == 2)
            {
                cv.Fill(0, 0, W, H, Pa);
                float p = Mathf.Clamp01((t + 1) / (float)Fx.CrtLineTicks);
                int w = Mathf.Max(2, Mathf.RoundToInt(W * p)) & ~1;
                cv.Fill((W - w) / 2, H / 2, w, 1, Cy);                  // 가장자리는 밝은 청사진 파랑
                int core = Mathf.Max(2, w * 6 / 10) & ~1;
                cv.Fill((W - core) / 2, H / 2, core, 1, Wh);            // 가운데는 흰색
                return;
            }
            DrawBoot(0);
            float q = Mathf.Clamp01((t + 1) / (float)Fx.CrtOpenTicks);
            int hgt = Mathf.Clamp(Mathf.RoundToInt(H * q), 2, H) & ~1;
            int y0 = (H - hgt) / 2, y1 = y0 + hgt;
            if (y0 <= 0) return;
            cv.Fill(0, 0, W, y0, Pa);
            cv.Fill(0, y1, W, H - y1, Pa);
            cv.Fill(0, y0, W, 1, Wh);                                   // 빛이 번지는 가장자리: 밝은 선 1줄 + 디더 3줄(75·50·25%)
            cv.Fill(0, y1 - 1, W, 1, Wh);
            if (hgt < 2 * Fx.CrtEdgeRows + 2) return;
            string[] keys = { "fx/dith75_cy", "fx/dith50_cy", "fx/dith25_cy" };
            for (int i = 0; i < keys.Length; i++)
            {
                cv.TilePx(keys[i], 0, (y0 + 1 + i) * Z, W * Z, Z, 0, 0);
                cv.TilePx(keys[i], 0, (y1 - 2 - i) * Z, W * Z, Z, 0, 0);
            }
        }

        // ---------- 바탕화면 ----------

        static readonly string[] IconKey = { "ui/icon_file_clear", "ui/icon_file_new", "ui/icon_file_locked" };

        // W-032 일반 모드 팝업(D-121): 아이콘 오른쪽 창. 항상 2열만 보인다. 열 띠 = [챕터 | 스테이지 | 상세], 스테이지 포커스에서는 ColumnStep만큼 왼쪽으로 밀린다
        public const int PopupX = 90, PopupY = 16, PopupW = 330, PopupH = 150;
        public const int ColumnStep = 96;
        const int PopupRow = 16, PopupChapterW = 84, PopupStageX = ColumnStep, PopupStageW = 110, PopupDetailX = ColumnStep + PopupStageW + 12, PopupDetailW = 192;

        /// <summary>
        /// 바탕화면 그리기(W-032). icon 0 = NORMAL MODE, 1 = 개발 모드 자리(잠김). popup이 true면 일반 모드 팝업(popupTick = 열리는 연출 틱,
        /// shift = 열 밀림 px, stageFocus = 포커스가 스테이지 목록, folder = 선택한 챕터, sel = 선택한 의뢰).
        /// connect가 0 이상이면 접속 창과 진행 막대(0~1). devLocked가 true면 개발 모드 자리 아래에 LOCKED 글자.
        /// </summary>
        public void DrawDesktop(int icon, bool popup, int popupTick, int shift, bool stageFocus, int folder, int sel, float connect, bool devLocked)
        {
            cv.Blit("scenes/desktop_wallpaper", 0, 0);
            DrawModeIcons(icon, devLocked);
            if (popup) DrawPopup(popupTick, shift, stageFocus, folder, sel);

            // 작업 표시줄
            cv.Fill(0, 340, PixelCanvas.W, 20, Pa);
            cv.Fill(0, 340, PixelCanvas.W, 1, Cy);
            cv.Fill(4, 343, 49, 14, Bp);
            cv.Box(4, 343, 52, 356, Cy);
            cv.Text(13, 347, "SHIFT", Wh);
            if (connect >= 0f) Hint(62, 344, "ESC", "CANCEL");
            else if (popup && stageFocus) Hint(62, 344, "↑↓", "SELECT", "ENTER", "CONNECT", "←", "BACK");
            else if (popup) Hint(62, 344, "↑↓", "SELECT", "→", "OPEN", "ESC", "CLOSE");
            else Hint(62, 344, "↑↓", "SELECT", "ENTER", "OPEN", "ESC", "LOG OFF");
            string clock = "21:04";   // 고정 글자
            cv.Text(PixelCanvas.W - 8 - PixelCanvas.TextWidth(clock), 347, clock, Wh);

            if (connect >= 0f) DrawConnect(connect);
        }

        /// <summary>바탕화면 아이콘 2개(왼쪽 위부터 세로): NORMAL MODE, 개발 모드 자리(흐린 색 + 자물쇠, 이름 ???).</summary>
        void DrawModeIcons(int sel, bool devLocked)
        {
            for (int i = 0; i < 2; i++)
            {
                bool locked = i == 1;
                int x = 24, y = 20 + i * 72;
                if (i == sel)
                {
                    cv.Fill(x - 14, y - 4, 73, 53, Pa);
                    cv.Box(x - 14, y - 4, x + 58, y + 48, Cy);
                }
                DrawModeIcon(x + 6, y, locked);
                string name = locked ? "???" : "NORMAL MODE";
                cv.Text(x + 22 - PixelCanvas.TextWidth(name) / 2, y + 36, name, locked ? B3 : Wh);
                if (locked && devLocked) cv.Text(x + 22 - PixelCanvas.TextWidth("LOCKED") / 2, y + 46, "LOCKED", Rd);
            }
        }

        /// <summary>32x32 모드 아이콘(코드로 그림): 모니터 + 화면 속 글줄. locked면 흐린 색 + 앞쪽 자물쇠.</summary>
        void DrawModeIcon(int x, int y, bool locked)
        {
            Color32 frame = locked ? B3 : Wh, screen = locked ? Bp : Bb, line = locked ? B3 : Cy;
            cv.Fill(x + 3, y + 3, 26, 19, K);                       // 바깥 테두리
            cv.Fill(x + 4, y + 4, 24, 17, frame);
            cv.Fill(x + 6, y + 6, 20, 13, screen);
            cv.Fill(x + 8, y + 8, 12, 1, line);
            cv.Fill(x + 8, y + 11, 16, 1, line);
            cv.Fill(x + 8, y + 14, 9, 1, line);
            if (!locked) cv.Fill(x + 21, y + 14, 3, 3, Gn);          // 클리어 표시 점
            cv.Fill(x + 13, y + 22, 6, 3, K);                       // 받침대
            cv.Fill(x + 14, y + 22, 4, 2, frame);
            cv.Fill(x + 9, y + 25, 14, 3, K);
            cv.Fill(x + 10, y + 25, 12, 2, frame);
            if (locked) Lock(x + 11, y + 13, B3, true);
        }

        /// <summary>작은 자물쇠(7x8). big이면 아이콘용으로 한 칸 크게 그린다(9x11).</summary>
        void Lock(int x, int y, Color32 body, bool big = false)
        {
            if (big)
            {
                cv.Fill(x + 1, y, 7, 6, K); cv.Fill(x + 3, y + 1, 3, 4, Pa);   // 고리
                cv.Fill(x, y + 5, 9, 7, K); cv.Fill(x + 1, y + 6, 7, 5, body);
                cv.Fill(x + 4, y + 8, 1, 2, K);
                return;
            }
            cv.Fill(x + 1, y, 5, 4, body); cv.Fill(x + 2, y + 1, 3, 3, Pa);
            cv.Fill(x, y + 3, 7, 5, body);
            cv.Fill(x + 3, y + 5, 1, 2, K);
        }

        /// <summary>작은 체크 표시(7x5).</summary>
        void Check(int x, int y, Color32 c)
        {
            int[] ys = { 2, 3, 4, 3, 2, 1, 0 };
            for (int i = 0; i < ys.Length; i++) cv.Fill(x + i, y + ys[i], 1, 2, c);
        }

        /// <summary>일반 모드 팝업: 열리는 동안은 창틀만 가로로 커지고, 다 열리면 열 띠(챕터 | 스테이지 | 상세)를 shift만큼 밀어 두 열만 보여 준다.</summary>
        void DrawPopup(int tick, int shift, bool stageFocus, int folder, int sel)
        {
            int step = Mathf.Clamp(tick + 1, 1, Fx.PopupOpenTicks);
            int w = Mathf.Max(24, PopupW * step / Fx.PopupOpenTicks);
            cv.Nine("ui/window_frame", PopupX, PopupY, w, PopupH);
            if (step < Fx.PopupOpenTicks) return;
            cv.Text(PopupX + 8, PopupY + 7, stageFocus ? "NORMAL MODE > " + RequestBoard.ChapterName(folder) : "NORMAL MODE", Wh);

            int ix = PopupX + 8, iw = PopupW - 16, top = PopupY + 22, ih = PopupH - 30;
            cv.SetClip(ix, top, Mathf.Min(iw, PopupDetailX - 10 + shift), ih);   // 챕터 포커스(shift 0)에서는 상세 열이 보이지 않는다
            int x0 = ix - shift;

            // 챕터 목록
            for (int i = 0; i < RequestBoard.ChapterCount; i++)
            {
                bool locked = !board.ChapterOpen(i);
                int ry = top + 2 + i * (PopupRow + 2);
                if (i == folder)
                {
                    cv.Fill(x0, ry, PopupChapterW, PopupRow - 2, Pa);
                    cv.Box(x0, ry, x0 + PopupChapterW - 1, ry + PopupRow - 3, stageFocus ? B3 : Cy);
                }
                cv.Text(x0 + 4, ry + 3, RequestBoard.ChapterName(i), locked ? B3 : Wh);
                if (locked) Lock(x0 + PopupChapterW - 11, ry + 2, B3);
            }
            cv.Fill(x0 + PopupStageX - 6, top, 1, ih, Bb);

            // 스테이지 목록
            int sx = x0 + PopupStageX;
            if (!board.ChapterOpen(folder))
            {
                cv.Text(sx + 4, top + 8, "LOCKED", B3, 2);
                cv.ClearClip();
                return;
            }
            var items = board.InChapter(folder);
            for (int pos = 0; pos < items.Count; pos++)
            {
                int i = items[pos];
                var st = board.State(i);
                bool locked = st == RequestState.Locked;
                int ry = top + 2 + pos * (PopupRow + 2);
                if (i == sel)
                {
                    cv.Fill(sx, ry, PopupStageW, PopupRow - 2, Pa);
                    cv.Box(sx, ry, sx + PopupStageW - 1, ry + PopupRow - 3, stageFocus ? Cy : B3);
                }
                cv.Text(sx + 4, ry + 3, board.RequestName(i), locked ? B3 : Wh);
                if (locked) { Lock(sx + 4 + 42 + 6, ry + 2, B3); continue; }
                if (board.IsTutorial(i))
                {
                    if (st == RequestState.Clear) cv.Text(sx + 4 + 42 + 6, ry + 3, "CLEAR", Gn);   // W-008: 튜토리얼 의뢰는 별 없음
                }
                else Stars(sx + 4 + 42 + 6, ry + 2, board.Best(i));
                if (st == RequestState.Clear) Check(sx + PopupStageW - 12, ry + 3, Gn);
            }
            cv.Fill(x0 + PopupDetailX - 6, top, 1, ih, Bb);

            // 상세(선택한 스테이지, ↑↓마다 바로 바뀜)
            DrawDetail(x0 + PopupDetailX, top + 2, sel);
            cv.ClearClip();
        }

        /// <summary>상세 열: 건물 이름, 의뢰 제목, 미션, 최고 별. 잠긴 스테이지는 LOCKED만.</summary>
        void DrawDetail(int x, int y, int sel)
        {
            if (board.State(sel) == RequestState.Locked)
            {
                cv.Text(x + 4, y + 6, "LOCKED", B3, 2);
                return;
            }
            var e = board.Entry(sel);
            string building = e.level != null && !string.IsNullOrEmpty(e.level.buildingName) ? e.level.buildingName : "";
            if (building.Length > 0) cv.Text(x + 4, y + 2, building, Cy);
            cv.Text(x + 4, y + 14, e.title, Wh);
            cv.Fill(x + 4, y + 26, PopupDetailW - 12, 1, Bb);
            cv.Text(x + 4, y + 32, "MISSIONS", Cy);
            for (int i = 0; i < e.missionNames.Length; i++) cv.Text(x + 8, y + 44 + i * 12, "- " + e.missionNames[i], B3);
            int by = y + 44 + e.missionNames.Length * 12 + 6;
            if (board.IsTutorial(sel))
            {
                if (board.State(sel) == RequestState.Clear) cv.Text(x + 4, by, "CLEAR", Gn);   // W-008: 튜토리얼 의뢰는 별 없음
            }
            else
            {
                cv.Text(x + 4, by, "BEST", Cy);
                Stars(x + 36, by - 1, board.Best(sel));
            }
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
