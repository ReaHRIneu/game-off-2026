using System.Collections.Generic;
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
            int kw = PixelCanvas.TextWidth5x7(key) + 6;   // 키 이름은 두 언어 모두 5x7
            cv.Fill(x, y, kw, 11, K);
            cv.Box(x, y, x + kw - 1, y + 10, B3);
            cv.Text5x7(x + 3, y + 2, key, Wh);
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
                Button(bx, by, 84, MainLabel(items[i]), i == sel && !confirm);
                if (i == sel && !confirm) cv.Text(bx - 10, by + 3, ">", Rd);
            }
            if (confirm)
            {
                const int w = 190, h = 66;
                int x = (PixelCanvas.W - w) / 2, y = 124;
                cv.Nine("ui/window_frame", x, y, w, h);
                string q = Loc.T("menu.erase_q");
                cv.Text(x + (w - PixelCanvas.TextWidth(q)) / 2, y + 12, q, Wh);
                Button(x + 24, y + 38, 60, Loc.T("menu.no"), confirmSel == 0);
                Button(x + w - 24 - 60, y + 38, 60, Loc.T("menu.yes"), confirmSel == 1);
            }
            DrawBand(confirm ? new[] { "←→", Loc.T("hint.select"), "ENTER", Loc.T("hint.ok"), "ESC", Loc.T("hint.back") } : new[] { "↑↓", Loc.T("hint.select"), "ENTER", Loc.T("hint.ok") });
        }

        /// <summary>메인 메뉴 항목의 화면 글자(항목 자체는 GameView의 영문 키로 구분한다).</summary>
        static string MainLabel(string item)
        {
            switch (item)
            {
                case "CONTINUE": return Loc.T("menu.continue");
                case "NEW GAME": return Loc.T("menu.new_game");
                case "SETTINGS": return Loc.T("menu.settings");
                case "CREDITS": return Loc.T("menu.credits");
                case "QUIT": return Loc.T("menu.quit");
            }
            return item;
        }

        // ---------- W-050: 메인 화면 움직임(눈 깜빡임·해커 손·모니터 빛·창밖 불빛) ----------
        // 덮개 그림 `scenes/main_lobby_v6_x8_<이름>`은 Art/recipes/main_lobby_v6_x8.py 가 만든다. 위치는 그 그림의 manifest "pos"(장면 좌표 320x180,
        // 화면 논리 좌표로는 x2)와 같은 값이다. 장면 그림 자체는 바뀌지 않고, 바뀐 픽셀만 있는 덮개 그림을 상태에 맞게 얹는다.

        public const float HandStepSeconds = 0.25f, MonitorStepSeconds = 0.7f;
        static readonly int[] MonitorLevels = { 0, 1, 2, 1 };   // 0 = 본그림(가장 밝음), 1 = 중간, 2 = 꺼짐. 한 바퀴 2.8초

        struct CityLight { public int x, y; public bool baseOn; public float period, offset, flipFrac; }
        // 창밖 불빛(2x1 장면 픽셀): baseOn = 본그림에서 켜져 있음. 각 주기의 앞 flipFrac 동안 반대로 바뀐다(꺼졌다/켜졌다). 다섯 점의 주기·시작이 서로 다르다
        static readonly CityLight[] CityLights =
        {
            new CityLight { x = 128, y = 69, baseOn = true,  period = 2.6f, offset = 0.0f, flipFrac = 0.35f },
            new CityLight { x = 123, y = 75, baseOn = true,  period = 3.7f, offset = 1.1f, flipFrac = 0.30f },
            new CityLight { x = 138, y = 73, baseOn = true,  period = 3.1f, offset = 2.0f, flipFrac = 0.40f },
            new CityLight { x = 131, y = 75, baseOn = false, period = 4.3f, offset = 0.7f, flipFrac = 0.35f },
            new CityLight { x = 128, y = 77, baseOn = false, period = 2.9f, offset = 1.6f, flipFrac = 0.30f },
        };
        static readonly Color32 CityOn = new Color32(228, 158, 44, 255), CityOff = new Color32(10, 30, 76, 255);   // 본그림의 켜진 창·건물 어둠 색

        readonly BlinkClock mainHackerBlink = new BlinkClock(5101, 3f, 5f, 0.1f);
        readonly BlinkClock mainRunnerBlink = new BlinkClock(5102, 4f, 6.5f, 0.1f);
        float mainAnimT;

        /// <summary>
        /// 지금 얹을 움직임 상태를 한 숫자로 묶은 값(비트 0 해커 눈 감김, 1 러너 눈 감김, 2 손 프레임(0 왼손 위·1 오른손 위), 3~4 모니터 빛 단계, 5~9 창밖 불빛이 본그림과 반대).
        /// 바뀐 프레임에만 메인 화면을 다시 그린다.
        /// </summary>
        public int MainAnimState { get; private set; }
        public int MainAnimHackerBlinks => mainHackerBlink.BlinkCount;
        public int MainAnimRunnerBlinks => mainRunnerBlink.BlinkCount;

        /// <summary>메인 화면이 보이는 동안만 GameView가 매 프레임 부른다. 화면에 보이는 상태가 바뀌면 true.</summary>
        public bool AdvanceMainAnim(float dt)
        {
            mainAnimT += dt;
            mainHackerBlink.Advance(dt);
            mainRunnerBlink.Advance(dt);
            int st = (mainHackerBlink.Closed ? 1 : 0) | (mainRunnerBlink.Closed ? 2 : 0);
            st |= ((int)(mainAnimT / HandStepSeconds) % 2) << 2;
            st |= MonitorLevels[(int)(mainAnimT / MonitorStepSeconds) % MonitorLevels.Length] << 3;
            for (int i = 0; i < CityLights.Length; i++)
                if (CityLightFlipped(CityLights[i], mainAnimT)) st |= 1 << (5 + i);
            bool changed = st != MainAnimState;
            MainAnimState = st;
            return changed;
        }

        static bool CityLightFlipped(CityLight l, float t) => (t + l.offset) % l.period < l.period * l.flipFrac;

        void Overlay(string name, int sceneX, int sceneY)
        {
            string key = "scenes/main_lobby_v6_x8_" + name;
            if (cv.Has(key)) cv.Blit(key, sceneX * 2, sceneY * 2);
        }

        void DrawMainAnim()
        {
            int st = MainAnimState;
            if ((st & 1) != 0) Overlay("blink_hacker", 149, 74);
            if ((st & 2) != 0) Overlay("blink_runner", 105, 95);
            if ((st & 4) == 0) Overlay("hands_l", 148, 106); else Overlay("hands_r", 159, 107);
            int mon = (st >> 3) & 3;
            if (mon == 1) Overlay("mon_l1", 180, 81);
            else if (mon == 2) Overlay("mon_l2", 180, 81);
            for (int i = 0; i < CityLights.Length; i++)
                if ((st & (1 << (5 + i))) != 0)
                    cv.Fill(CityLights[i].x * 2, CityLights[i].y * 2, 4, 2, CityLights[i].baseOn ? CityOff : CityOn);
        }

        void DrawMainBase()
        {
            // W-041(D-127·D-128): 메인 화면은 합성 그림 한 장(640x360, 4배 점 확대). 없으면 이전 방 레이어
            if (cv.Has("scenes/main_lobby_v6_x8"))   // W-043: PixelLab 가공판 320x180(8배, 없으면 v5)
            {
                cv.Blit("scenes/main_lobby_v6_x8", 0, 0);
                DrawMainAnim();   // W-050: 눈 깜빡임·해커 손·모니터 빛·창밖 불빛(작은 덮개 그림과 점)
            }
            else if (cv.Has("scenes/main_lobby_v5")) cv.Blit("scenes/main_lobby_v5", 0, 0);   // W-042: pixelstudio 인물판(없으면 v4)
            else if (cv.Has("scenes/main_lobby_v4")) cv.Blit("scenes/main_lobby_v4", 0, 0);
            else { Room(false); cv.Blit("scenes/room_layer4_hacker", 0, 0); }
            cv.BlitHalf("ui/logo", 20, 18);   // 로고 정수 1/2 축소
        }

        void DrawBand(string[] hint)
        {
            cv.Fill(0, 340, PixelCanvas.W, 20, Pa);
            Hint(12, 344, hint);
        }

        // ---------- 설정(W-019, D-105): 메인·일시정지 공용 ----------

        public const int SettingsRows = 7;   // BGM, SFX, ENV FX, GLITCH, LANGUAGE, CONTROLS, BACK (W-034: LANGUAGE 추가)
        static string SettingsLabel(int i)
        {
            switch (i)
            {
                case 0: return Loc.T("settings.bgm");
                case 1: return Loc.T("settings.sfx");
                case 2: return Loc.T("settings.env_fx");
                case 3: return Loc.T("settings.glitch");
                case 4: return Loc.T("settings.language");
                case 5: return Loc.T("settings.controls");
                default: return Loc.T("settings.back");
            }
        }

        public const int SettingsW = 212, SettingsH = 156;

        /// <summary>메인에서 연 설정 화면: 방 일러스트 위 왼쪽 창 + 아래 키 안내 띠.</summary>
        public void DrawSettingsMain(GameView v)
        {
            DrawMainBase();
            DrawSettings(v, 16, 60);
            if (v.ControlsOpen) DrawControls(PixelCanvas.W / 2, 170);
            DrawBand(v.ControlsOpen ? new[] { "ESC", Loc.T("hint.back") } : new[] { "↑↓", Loc.T("hint.select"), "←→", Loc.T("hint.change"), "ENTER", Loc.T("hint.ok"), "ESC", Loc.T("hint.back") });
        }

        /// <summary>설정 창(왼쪽 위 x, y). 일시정지에서는 GameRenderer가 일시정지 창 자리에 부른다.</summary>
        public void DrawSettings(GameView v, int x, int y)
        {
            const int w = SettingsW, h = SettingsH;
            cv.Nine("ui/window_frame", x, y, w, h);
            cv.Text(x + 8, y + 7, Loc.T("settings.title"), Wh);
            for (int i = 0; i < SettingsRows; i++)
            {
                bool sel = i == v.SettingsIndex && !v.ControlsOpen;
                int ry = y + 24 + i * 16;
                if (i >= 5) { Button(x + 10, ry, w - 20, SettingsLabel(i), sel); continue; }
                cv.Nine(sel ? "ui/button_selected" : "ui/button", x + 10, ry, w - 20, 13);
                cv.Text(x + 16, ry + 3, SettingsLabel(i), Wh);
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
                    string val;
                    if (i == 2) val = v.Progress.EnvFxReduced ? Loc.T("value.off") : Loc.T("value.on");
                    else if (i == 4) val = v.Progress.Language == Language.Korean ? Loc.T("value.korean") : Loc.T("value.english");
                    else val = v.Progress.GlitchSetting == GlitchStrength.Off ? Loc.T("value.off") : v.Progress.GlitchSetting == GlitchStrength.Weak ? Loc.T("value.low") : Loc.T("value.normal");
                    if (sel) val = "< " + val + " >";
                    cv.Text(right - PixelCanvas.TextWidth(val), ry + 3, val, sel ? Cy : Wh);
                }
            }
            if (!v.ControlsOpen && v.SettingsFromPause)
                Hint(x + 10, y + h - 17, "↑↓", Loc.T("hint.select"), "←→", Loc.T("hint.change"), "ESC", Loc.T("hint.back"));
        }

        /// <summary>조작법 창: 중심 (cx, cy). 실제 입력 처리(GameInput)와 같은 키만 적는다.</summary>
        public void DrawControls(int cx, int cy)
        {
            const int w = 284, h = 166;   // W-049: 줄 7개 -> W-052: W A S D 줄 추가로 8개
            int x = cx - w / 2, y = cy - h / 2;
            cv.Nine("ui/window_frame", x, y, w, h);
            cv.Text(x + 8, y + 7, Loc.T("controls.title"), Wh);
            string[] keys = { "ARROWS", "W A S D", "L-SHIFT", "L-SHIFT + ARROW", "L-SHIFT + 1-3", "]", "ESC", "ENTER" };
            string[] labels = { Loc.T("controls.aim"), Loc.T("controls.look"), Loc.T("controls.hold"), Loc.T("controls.push"), Loc.T("controls.skip"), Loc.T("controls.tab"), Loc.T("controls.pause"), Loc.T("controls.ok") };
            for (int i = 0; i < keys.Length; i++)
            {
                int ry = y + 24 + i * 14;
                Badge(x + 10, ry, keys[i]);
                cv.Text(x + 10 + 104, ry + 2, labels[i], Wh);
            }
            Hint(x + 10, y + h - 17, "ESC", Loc.T("hint.back"));
        }

        // ---------- 도움말(GUIDE) 패널(W-049) ----------
        // 일시정지 메뉴(GameRenderer가 일시정지 창 자리에 부른다)와 바탕화면(GameView.RedrawMenu)에서 쓴다. 새 그림 파일 없이 기존 창 틀·색·글꼴·키 배지로만 그린다.

        public const int GuideW = 480, GuideH = 260, GuideX = (PixelCanvas.W - GuideW) / 2, GuideY = 40;
        public const int GuideTextW = 262;
        const int GuideTextX = 22, GuideDiagX = 300, GuideDiagY = 30, GuideDiagW = 168, GuideDiagH = 160;
        static readonly Color32 Yl = new Color32(240, 210, 90, 255);   // 팔레트 Y(GameRenderer와 같은 값)

        // 문구 id는 W-034 미사용 검사가 Loc.T 호출 안의 글자를 찾으므로 하나씩 적는다.
        static string GuideTitle(int page)
        {
            switch (page)
            {
                case 0: return Loc.T("guide.p0.title");
                case 1: return Loc.T("guide.p1.title");
                case 2: return Loc.T("guide.p2.title");
                default: return Loc.T("guide.p3.title");
            }
        }

        /// <summary>쪽 page의 n번째(1부터) 본문 항목. 더 없으면 null.</summary>
        static string GuideItem(int page, int n)
        {
            switch (page * 10 + n)
            {
                case 1: return Loc.T("guide.p0.1");
                case 2: return Loc.T("guide.p0.2");
                case 3: return Loc.T("guide.p0.3");
                case 4: return Loc.T("guide.p0.4");
                case 5: return Loc.T("guide.p0.5");
                case 11: return Loc.T("guide.p1.1");
                case 12: return Loc.T("guide.p1.2");
                case 13: return Loc.T("guide.p1.3");
                case 14: return Loc.T("guide.p1.4");
                case 21: return Loc.T("guide.p2.1");
                case 22: return Loc.T("guide.p2.2");
                case 23: return Loc.T("guide.p2.3");
                case 24: return Loc.T("guide.p2.4");
                case 31: return Loc.T("guide.p3.1");
                case 32: return Loc.T("guide.p3.2");
                case 33: return Loc.T("guide.p3.3");
            }
            return null;
        }

        /// <summary>도움말 패널. pages는 지금 볼 수 있는 쪽 번호 목록, index는 그중 지금 쪽.</summary>
        public void DrawGuide(IReadOnlyList<int> pages, int index)
        {
            index = Mathf.Clamp(index, 0, pages.Count - 1);
            int page = pages[index];
            int x = GuideX, y = GuideY;
            cv.Nine("ui/window_frame", x, y, GuideW, GuideH);
            string title = Loc.T("guide.title");
            cv.Text(x + 8, y + 7, title, Wh);
            cv.Text(x + 8 + PixelCanvas.TextWidth(title) + 12, y + 7, GuideTitle(page), Cy);
            cv.Fill(x + 8, y + 20, GuideW - 16, 1, Bb);

            // 본문: 항목마다 작은 점 + 줄바꿈한 글. 한국어는 줄 간격이 더 넓다
            int ty = y + GuideDiagY;
            int step = PixelCanvas.LineStep;
            for (int n = 1; GuideItem(page, n) != null; n++)
            {
                List<string> rows = PixelCanvas.WrapPx(GuideItem(page, n), GuideTextW);
                cv.Fill(x + 14, ty + (step > 9 ? 4 : 3), 3, 3, Cy);
                for (int r = 0; r < rows.Count; r++) cv.Text(x + GuideTextX, ty + r * step, rows[r], Wh);
                ty += rows.Count * step + 6;
            }

            // 도식: 오른쪽 상자 안(코드로 그린 간단한 그림)
            int dx = x + GuideDiagX, dy = y + GuideDiagY;
            cv.Fill(dx, dy, GuideDiagW, GuideDiagH, Pa);
            cv.Box(dx, dy, dx + GuideDiagW - 1, dy + GuideDiagH - 1, B3);
            switch (page)
            {
                case 0: DrawGuideBasics(dx, dy); break;
                case 1: DrawGuideRooms(dx, dy); break;
                case 2: DrawGuideBeats(dx, dy); break;
                default: DrawGuideSigns(dx, dy); break;
            }

            // 쪽 점과 키 안내
            int dotsW = pages.Count * 10 - 5;
            int px = x + (GuideW - dotsW) / 2, py = y + GuideH - 36;
            for (int i = 0; i < pages.Count; i++)
            {
                if (i == index) cv.Fill(px + i * 10, py, 5, 5, Cy);
                else { cv.Fill(px + i * 10, py, 5, 5, K); cv.Box(px + i * 10, py, px + i * 10 + 4, py + 4, B3); }
            }
            Hint(x + 10, y + GuideH - 17, "←→", Loc.T("guide.page"), "ESC", Loc.T("hint.back"));
        }

        /// <summary>쪽 1(BASICS) 도식: 방향키, 조준한 섹터, Shift로 설계도, 추적도 막대.</summary>
        void DrawGuideBasics(int dx, int dy)
        {
            Badge(dx + 36, dy + 10, "↑");
            Badge(dx + 22, dy + 23, "←"); Badge(dx + 36, dy + 23, "↓"); Badge(dx + 50, dy + 23, "→");
            cv.Box(dx + 92, dy + 10, dx + 150, dy + 44, Cy);
            cv.Fill(dx + 98, dy + 30, 46, 1, B3);
            int sw = Badge(dx + 10, dy + 66, "SHIFT");
            cv.Text5x7(dx + 10 + sw + 4, dy + 68, "→", B3);
            cv.Fill(dx + 92, dy + 58, 59, 35, Bp);
            cv.Box(dx + 92, dy + 58, dx + 150, dy + 92, Cy);
            cv.Fill(dx + 98, dy + 80, 46, 1, Bb);
            cv.Fill(dx + 98, dy + 66, 1, 14, Bb);
            cv.Fill(dx + 122, dy + 66, 22, 1, Bb);
            cv.Text(dx + 10, dy + 112, Loc.T("game.trace"), B3);
            int bx = dx + 10, by = dy + 126, bw = GuideDiagW - 20;
            cv.Fill(bx, by, bw, 7, Bp);
            cv.Fill(bx, by, bw * 7 / 10, 7, Rd);
            cv.Box(bx, by, bx + bw - 1, by + 6, B3);
            cv.Text5x7(dx + GuideDiagW - 10 - PixelCanvas.TextWidth5x7("100"), dy + 112, "100", Wh);
        }

        /// <summary>쪽 2(ROOMS) 도식: 레일 위 방 두 칸이 자리를 바꾼다, Shift + 방향키, 카메라 빛.</summary>
        void DrawGuideRooms(int dx, int dy)
        {
            cv.Fill(dx + 12, dy + 10, 8, 5, Rd);
            for (int r = 0; r < 14; r++) cv.FillAlpha(dx + 14 - r / 2, dy + 15 + r, 5 + r, 1, Yl, 0.45f);
            cv.Fill(dx + 15, dy + 36, 3, 6, Wh);
            cv.Box(dx + 30, dy + 56, dx + 70, dy + 96, Cy);
            cv.Box(dx + 98, dy + 56, dx + 138, dy + 96, B3);
            cv.Text5x7(dx + 47, dy + 72, "A", Cy);
            cv.Text5x7(dx + 115, dy + 72, "B", B3);
            cv.Text5x7(dx + 79, dy + 66, "→", Cy);
            cv.Text5x7(dx + 79, dy + 78, "←", Cy);
            cv.Fill(dx + 12, dy + 98, GuideDiagW - 24, 2, Bb);
            int x = dx + 20, y = dy + 118;
            x += Badge(x, y, "SHIFT") + 4;
            cv.Text5x7(x, y + 2, "+", Wh); x += 10;
            Badge(x, y, "→");
        }

        /// <summary>쪽 3(BEATS) 도식: 박자 칸(켜짐·꺼짐)과 Shift + 1~3으로 앞당긴 모습.</summary>
        void DrawGuideBeats(int dx, int dy)
        {
            bool[] on = { true, true, false, true, true, true };
            for (int i = 0; i < on.Length; i++)
            {
                int cx = dx + 12 + i * 25;
                cv.Fill(cx, dy + 20, 19, 19, on[i] ? Bp : K);
                cv.Box(cx, dy + 20, cx + 18, dy + 38, on[i] ? Cy : Rd);
            }
            cv.Text5x7(dx + 12, dy + 50, "↓", Cy);
            for (int i = 0; i < on.Length; i++)
            {
                int cx = dx + 12 + i * 25;
                bool o = on[(i + 2) % on.Length];
                cv.Fill(cx, dy + 72, 19, 19, o ? Bp : K);
                cv.Box(cx, dy + 72, cx + 18, dy + 90, o ? Cy : Rd);
            }
            int x = dx + 8, y = dy + 118;
            x += Badge(x, y, "SHIFT") + 4;
            cv.Text5x7(x, y + 2, "+", Wh); x += 10;
            Badge(x, y, "1-3");
        }

        /// <summary>쪽 4(SIGNS) 도식: 표지 글자 4개, 민트 링, 노란 ! 표시.</summary>
        void DrawGuideSigns(int dx, int dy)
        {
            string[] signs = { Loc.T("marker.open"), Loc.T("marker.closed"), Loc.T("marker.moved"), Loc.T("marker.no_effect") };
            for (int i = 0; i < signs.Length; i++)
            {
                int sy = dy + 12 + i * 22;
                int w = PixelCanvas.TextWidth(signs[i]) + 8;
                cv.Fill(dx + 12, sy, w, 13, K);
                cv.Box(dx + 12, sy, dx + 12 + w - 1, sy + 12, i == 3 ? B3 : Cy);
                cv.Text(dx + 16, sy + 3, signs[i], i == 3 ? Cy : Wh);
            }
            int rx = dx + 128, ry = dy + 44;
            cv.Box(rx - 3, ry - 3, rx + 3, ry + 3, Gn);
            cv.Box(rx - 9, ry - 9, rx + 9, ry + 9, Gn);
            int ax = dx + 128, ay = dy + 104;
            cv.Fill(ax - 4, ay - 11, 9, 11, K);
            cv.Box(ax - 4, ay - 11, ax + 4, ay - 1, Rd);
            cv.Text5x7(ax - 2, ay - 9, "!", Yl);
            cv.Fill(ax - 1, ay + 4, 3, 7, Wh);
        }

        // ---------- 부팅 화면(W-045) ----------
        // 화면 전체가 컴퓨터 부팅 화면이다(이전 방 그림 room_layer*·room_screen_boot_*·room_layer4_hacker_lit는 쓰지 않는다).
        // 기준 눈금 2.4초(bootStageSeconds 0.6 x 4): 0~0.9 자체 점검 글, 0.9~1.8 로고와 진행 막대, 1.8~2.4 완료(마지막 0.2초 바탕화면 색으로 밝아짐).
        public const float BootRefSeconds = 2.4f;
        const float BootTextEnd = 0.9f, BootLogoEnd = 1.8f, BootFadeSeconds = 0.2f;
        const float BootLineGap = 0.12f;        // 점검 글 한 줄이 나타나는 간격
        const float BootCursorHalf = 0.125f;    // 커서 깜빡임 반주기(주기 0.25초)
        const int BootBarX = 200, BootBarY = 190, BootBarW = 240, BootBarH = 8;
        static readonly string[] BootNames = { "CPU", "MEMORY  65536K", "DISPLAY  CRT-640", "BLUEPRINT LINK", "NETWORK" };

        /// <summary>
        /// 부팅 화면. seconds = 부팅이 시작된 뒤 지난 시간(CRT 켜짐이 끝난 뒤부터, GameView.BootSeconds), total = 부팅 합계 시간.
        /// stage(0~3)는 이전 호출 호환용이며 그리기에는 쓰지 않는다. 글은 언어와 무관한 고정 영어(시계처럼).
        /// </summary>
        public void DrawBoot(int stage, float seconds = 0f, float total = BootRefSeconds)
        {
            const int W = PixelCanvas.W, H = PixelCanvas.H;
            cv.Fill(0, 0, W, H, Pa);
            float s = Mathf.Max(0f, seconds) * BootRefSeconds / Mathf.Max(0.01f, total);   // 기준 눈금으로 환산
            if (s < BootTextEnd) DrawBootText(s);
            else DrawBootLogo(s < BootLogoEnd ? (s - BootTextEnd) / (BootLogoEnd - BootTextEnd) : 1f, s >= BootLogoEnd);
            for (int y = 1; y < H; y += 2) cv.FillAlpha(0, y, W, 1, new Color32(0, 0, 0, 255), 0.15f);   // 주사선
            float fade = (s - (BootRefSeconds - BootFadeSeconds)) / BootFadeSeconds;
            if (fade > 0f) cv.FillAlpha(0, 0, W, H, Bp, Mathf.Clamp01(fade));   // 마지막 0.2초: 바탕화면 쪽 색으로 밝아짐
        }

        /// <summary>1단계: 왼쪽 위부터 점검 글이 한 줄씩 나타난다. 마지막 줄 끝에 커서가 깜빡인다.</summary>
        void DrawBootText(float s)
        {
            const int X = 16, Y = 16, Step = 12, OkX = X + 6 * 30;
            int shown = Mathf.Min(7, Mathf.FloorToInt(s / BootLineGap + 0.0001f) + 1);
            for (int i = 0; i < shown; i++)
            {
                int y = Y + i * Step;
                if (i == 0) { cv.Text5x7(X, y, "SHIFT//REMOTE BIOS  v0.9", Wh); continue; }
                if (i == 6)
                {
                    cv.Text5x7(X, y, "LOADING SHELL", Wh);
                    if (Mathf.FloorToInt(s / BootCursorHalf) % 2 == 0) cv.Fill(X + 13 * 6, y, 5, 7, Cy);   // 깜빡이는 커서
                    continue;
                }
                string left = BootNames[i - 1] + "  ";
                while (left.Length < 28) left += ".";
                cv.Text5x7(X, y, left, Cy);
                cv.Text5x7(OkX, y, "OK", Gn);
            }
        }

        /// <summary>2~3단계: 가운데 로고와 진행 막대. done이면 막대 100%와 WELCOME, OPERATOR.</summary>
        void DrawBootLogo(float p, bool done)
        {
            const int W = PixelCanvas.W;
            if (cv.Has("ui/logo")) cv.Blit("ui/logo", (W - cv.WidthOf("ui/logo")) / 2, 128);
            cv.Box(BootBarX, BootBarY, BootBarX + BootBarW - 1, BootBarY + BootBarH - 1, Cy);
            int inner = BootBarW - 4, filled = Mathf.RoundToInt(inner * Mathf.Clamp01(p));
            for (int x = 0; x < filled; x += 4)
                cv.Fill(BootBarX + 2 + x, BootBarY + 2, Mathf.Min(4, filled - x), BootBarH - 4, Color32.Lerp(Bb, Cy, x / (float)inner));   // 파랑 -> 하늘색
            if (done)
            {
                const string msg = "WELCOME, OPERATOR";
                cv.Text5x7((W - PixelCanvas.TextWidth5x7(msg)) / 2, BootBarY + 16, msg, Wh);
                return;
            }
            string pct = "BOOTING... " + Mathf.RoundToInt(Mathf.Clamp01(p) * 100f) + "%";
            cv.Text5x7((W - PixelCanvas.TextWidth5x7("BOOTING... 100%")) / 2, BootBarY + 16, pct, Cy);   // 퍼센트 글자 자릿수가 바뀌어도 시작 위치 고정
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
            DrawBoot(0, 0f);   // W-045: 열리는 안쪽은 새 부팅 화면 첫 장면
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
            cv.Text5x7(13, 347, "SHIFT", Wh);
            if (connect >= 0f) Hint(62, 344, "ESC", Loc.T("hint.cancel"));
            else if (popup && stageFocus) Hint(62, 344, "↑↓", Loc.T("hint.select"), "ENTER", Loc.T("hint.connect"), "←", Loc.T("hint.back"));
            else if (popup) Hint(62, 344, "↑↓", Loc.T("hint.select"), "→", Loc.T("hint.open"), "ESC", Loc.T("hint.close"));
            else Hint(62, 344, "↑↓", Loc.T("hint.select"), "ENTER", Loc.T("hint.open"), "G", Loc.T("hint.guide"), "ESC", Loc.T("hint.log_off"));   // W-049: G = 도움말
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
                string name = locked ? Loc.T("desk.unknown") : Loc.T("desk.normal_mode");
                cv.Text(x + 22 - PixelCanvas.TextWidth(name) / 2, y + 36, name, locked ? B3 : Wh);
                if (locked && devLocked)
                {
                    string lk = Loc.T("desk.locked");
                    cv.Text(x + 22 - PixelCanvas.TextWidth(lk) / 2, y + 46, lk, Rd);
                }
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
            PlaceholderRows = 0;
            int step = Mathf.Clamp(tick + 1, 1, Fx.PopupOpenTicks);
            int w = Mathf.Max(24, PopupW * step / Fx.PopupOpenTicks);
            cv.Nine("ui/window_frame", PopupX, PopupY, w, PopupH);
            if (step < Fx.PopupOpenTicks) return;
            cv.Text(PopupX + 8, PopupY + 7, stageFocus ? Loc.T("desk.normal_mode") + " > " + RequestBoard.ChapterName(folder) : Loc.T("desk.normal_mode"), Wh);

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
            if (board.IsTeaserChapter(folder))
            {
                DrawTeaser(ix, top, iw, ih, sx, folder);   // W-047: 의뢰가 없는 챕터는 LOCKED 대신 예고 칸
                return;
            }
            if (!board.ChapterOpen(folder))
            {
                cv.Text(sx + 4, top + 8, Loc.T("desk.locked"), B3, 2);
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
                int nameW = Mathf.Max(42, PixelCanvas.TextWidth(board.RequestName(i)));   // 한국어 모드(Galmuri)에서 이름이 더 길면 그만큼 비킨다
                if (locked) { Lock(sx + 4 + nameW + 6, ry + 2, B3); continue; }
                if (board.IsTutorial(i))
                {
                    if (st == RequestState.Clear) cv.Text(sx + 4 + nameW + 6, ry + 3, Loc.T("desk.clear"), Gn);   // W-008: 튜토리얼 의뢰는 별 없음
                }
                else Stars(sx + 4 + nameW + 6, ry + 2, board.Best(i));
                if (st == RequestState.Clear) Check(sx + PopupStageW - 12, ry + 3, Gn);
            }
            // W-047: 데이터에 아직 없는 예정 의뢰(예: 2-4~2-6)는 흐린 글자 + 자물쇠 줄로만 그린다. 커서·상세·접속 없음
            int planned = RequestBoard.PlannedCount(folder);
            PlaceholderRows = 0;
            for (int pos = items.Count; pos < planned; pos++)
            {
                int ry = top + 2 + pos * (PopupRow + 2);
                string name = "REQ_" + folder + "-" + (pos + 1);
                cv.Text(sx + 4, ry + 3, name, B3);
                Lock(sx + 4 + Mathf.Max(42, PixelCanvas.TextWidth(name)) + 6, ry + 2, B3);
                PlaceholderRows++;
            }
            if (PlaceholderRows > 0) cv.Text(sx + 4, top + ih - 12, Loc.T("desk.coming_soon"), B3);
            cv.Fill(x0 + PopupDetailX - 6, top, 1, ih, Bb);

            // 상세(선택한 스테이지, ↑↓마다 바로 바뀜)
            DrawDetail(x0 + PopupDetailX, top + 2, sel);
            cv.ClearClip();
        }

        /// <summary>자동 확인용(W-047): 마지막 팝업 그리기에서 그린 예정 의뢰 줄 수, 예고 칸을 그린 횟수·챕터.</summary>
        public int PlaceholderRows { get; private set; }
        public int TeaserDraws { get; private set; }
        public int LastTeaserChapter { get; private set; } = -1;
        public int PreviewDraws { get; private set; }

        /// <summary>
        /// 예고 칸(W-047, roadmap A3): 스테이지 열 자리에 COMING SOON(2배) + 한 줄 소개 2줄. 챕터 3·4만 글이 있다.
        /// 챕터 포커스에서는 상세 열이 비어 있으므로, 클립을 팝업 안쪽 전체로 넓혀 그린다.
        /// </summary>
        void DrawTeaser(int ix, int top, int iw, int ih, int sx, int chapter)
        {
            cv.SetClip(ix, top, iw, ih);
            TeaserDraws++;
            LastTeaserChapter = chapter;
            cv.Text(sx + 4, top + 8, Loc.T("desk.coming_soon"), Cy, 2);
            // 키를 Loc.T 호출 안에 글자 그대로 적는다(W-034 미사용 문구 검사가 그 형태로 키를 찾는다)
            string a = chapter == 3 ? Loc.T("desk.teaser_ch3_a") : chapter == 4 ? Loc.T("desk.teaser_ch4_a") : "";
            string b = chapter == 3 ? Loc.T("desk.teaser_ch3_b") : chapter == 4 ? Loc.T("desk.teaser_ch4_b") : "";
            if (a.Length > 0) cv.Text(sx + 4, top + 34, a, B3);
            if (b.Length > 0) cv.Text(sx + 4, top + 48, b, Wh);
            cv.ClearClip();
        }

        /// <summary>상세 열: 건물 이름, 의뢰 제목, 미션, 최고 별. 잠긴 스테이지는 LOCKED만.</summary>
        void DrawDetail(int x, int y, int sel)
        {
            if (board.State(sel) == RequestState.Locked)
            {
                cv.Text(x + 4, y + 6, Loc.T("desk.locked"), B3, 2);
                return;
            }
            var e = board.Entry(sel);
            string building = e.level != null && !string.IsNullOrEmpty(e.level.buildingName) ? Loc.Tr(e.level.buildingName) : "";
            if (building.Length > 0) cv.Text(x + 4, y + 2, building, Cy);
            cv.Text(x + 4, y + 14, Loc.Tr(e.title), Wh);
            cv.Fill(x + 4, y + 26, PopupDetailW - 12, 1, Bb);
            cv.Text(x + 4, y + 32, Loc.T("desk.missions"), Cy);
            for (int i = 0; i < e.missionNames.Length; i++) cv.Text(x + 8, y + 44 + i * 12, "- " + Loc.Tr(e.missionNames[i]), B3);
            int by = y + 44 + e.missionNames.Length * 12 + 6;
            if (board.IsTutorial(sel))
            {
                if (board.State(sel) == RequestState.Clear) cv.Text(x + 4, by, Loc.T("desk.clear"), Gn);   // W-008: 튜토리얼 의뢰는 별 없음
            }
            else
            {
                cv.Text(x + 4, by, Loc.T("desk.best"), Cy);
                Stars(x + 36, by - 1, board.Best(sel));
            }
        }

        void DrawConnect(float ratio)
        {
            const int x0 = 180, y0 = 140;
            cv.Nine("ui/window_frame", x0, y0, 280, 70);
            cv.Text(x0 + 8, y0 + 7, Loc.T("desk.connecting"), Wh);
            cv.Text(x0 + 12, y0 + 30, Loc.T("desk.connecting_cam"), Gn);
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

        // ---------- 챕터 끝 예고 화면(W-047) ----------
        // 2-3을 처음 끝낸 뒤 결과 창 Enter -> 이 화면 -> 바탕화면. 부팅(W-045)과 같은 주사선. 글은 0.1초 간격으로 줄마다 나타난다.
        public const float PreviewStageSeconds = 0.1f;
        public const int PreviewStages = 8;   // 0 위 제목, 1 NEXT, 2 이름, 3 타워 그림, 4 브로커, 5 COMING SOON, 6 감사, 7 키 안내
        const float PreviewDotHalf = 0.35f;   // 점 3개 깜빡임 반주기

        /// <summary>예고 화면. seconds = 화면이 시작된 뒤 지난 시간(Enter로 건너뛰면 아주 큰 값). 글은 언어에 따라 Loc.T, 줄 효과·그림은 코드로 그린다.</summary>
        public void DrawPreview(float seconds)
        {
            const int W = PixelCanvas.W, H = PixelCanvas.H;
            PreviewDraws++;
            cv.Fill(0, 0, W, H, Pa);
            int shown = Mathf.Min(PreviewStages, Mathf.FloorToInt(Mathf.Max(0f, seconds) / PreviewStageSeconds + 0.0001f) + 1);

            if (shown > 0)
            {
                string head = Loc.T("preview.incoming");
                int hw = PixelCanvas.TextWidth(head), dotsW = 3 * 5 + 2 * 3;
                int hx = (W - (hw + 8 + dotsW)) / 2;
                cv.Text(hx, 22, head, Cy);
                if (Mathf.FloorToInt(seconds / PreviewDotHalf) % 2 == 0)
                    for (int d = 0; d < 3; d++) cv.Fill(hx + hw + 8 + d * 8, 25, 5, 5, Cy);   // 깜빡이는 점 3개
            }
            if (shown > 1)
            {
                string nx = Loc.T("preview.next");
                cv.Text((W - PixelCanvas.TextWidth(nx, 2)) / 2, 54, nx, Wh, 2);
            }
            if (shown > 2)
            {
                string nm = Loc.T("desk.teaser_ch3_a");
                cv.Text((W - PixelCanvas.TextWidth(nm)) / 2, 78, nm, Cy);
            }
            if (shown > 3) DrawTowerSketch(W / 2, 236);
            if (shown > 4)
            {
                string br = Loc.T("preview.broker");
                cv.Text((W - PixelCanvas.TextWidth(br)) / 2, 256, br, Wh);
            }
            if (shown > 5)
            {
                string cs = Loc.T("preview.coming");
                cv.Text((W - PixelCanvas.TextWidth(cs)) / 2, 280, cs, Cy);
            }
            if (shown > 6)
            {
                string th = Loc.T("preview.thanks");
                cv.Text((W - PixelCanvas.TextWidth(th)) / 2, 296, th, B3);
            }
            if (shown > 7) Hint(W / 2 - 40, 320, "ENTER", Loc.T("hint.continue"));
            for (int y = 1; y < H; y += 2) cv.FillAlpha(0, y, W, 1, new Color32(0, 0, 0, 255), 0.15f);   // 주사선(부팅과 같은 15%)
        }

        /// <summary>도시 관리 타워 설계도 느낌의 선 그림(코드로 그림): 아래에서 위로 좁아지는 층 5개 + 안테나. 중심 cx, 바닥 y = baseY.</summary>
        void DrawTowerSketch(int cx, int baseY)
        {
            int[] widths = { 96, 80, 64, 48, 32 };
            const int floorH = 22;
            int y = baseY;
            for (int i = 0; i < widths.Length; i++)
            {
                int w = widths[i], x = cx - w / 2;
                y -= floorH;
                cv.Box(x, y, x + w - 1, y + floorH - 1, i == 0 ? Cy : Bb);
                for (int wx = x + 8; wx + 4 < x + w - 6; wx += 12) cv.Fill(wx, y + 8, 4, 6, Bp);   // 창 칸
            }
            cv.Fill(cx, y - 18, 1, 18, Cy);                    // 안테나
            cv.Fill(cx - 3, y - 18, 7, 1, Cy);
            cv.Fill(cx - 1, y - 21, 3, 3, Rd);                 // 끝 표시등
            cv.Fill(cx - 66, baseY, 133, 1, Bb);               // 바닥선
            for (int t = 0; t < 6; t++) cv.Fill(cx + 62, baseY - t * 22, 6, 1, Bb);   // 높이 눈금
            cv.Fill(cx + 64, baseY - 5 * 22, 1, 5 * 22, Bb);
        }

        // ---------- 크레딧 ----------

        public void DrawCredits()
        {
            cv.Blit("scenes/desktop_wallpaper", 0, 0);
            cv.Nine("ui/window_frame", 60, 60, 520, 200);
            cv.Text(68, 67, Loc.T("credits.title"), Wh);
            string[] lines =
            {
                Loc.T("credits.l1"),
                Loc.T("credits.l2"),
                Loc.T("credits.l3"),
                Loc.T("credits.l4"),
                Loc.T("credits.l5"),
            };
            // 마지막 줄(제작자 이름 ReaHRineu, D-136)은 대소문자를 살려 Galmuri로 그린다. 5x7 글꼴은 대문자만 있다
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == lines.Length - 1) cv.TextExact(76, 100 + i * 16, lines[i], Wh);
                else cv.Text(76, 100 + i * 16, lines[i], Wh);
            }
            cv.Fill(0, 340, PixelCanvas.W, 20, Pa);
            Hint(12, 344, "ESC", Loc.T("hint.back"));
        }
    }
}
