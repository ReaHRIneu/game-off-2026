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
        public const int VX = 16, VY = 48, VW = GameSim.ViewW, VH = GameSim.ViewH;
        static readonly Color32 Gd = new Color32(36, 128, 92, 255);   // 출구 문(팔레트 g)
        const int MaxBubbleChars = 26;   // 말풍선 한 줄 최대 글자 수

        readonly PixelCanvas cv;
        Board layerBoard;        // 고정 층을 만든 격자(의뢰가 바뀌면 다시 만든다)

        // ---------- 방 밀기 연출(W-014) ----------
        // 규칙의 위치는 누르는 순간 바뀐다(GameSim). 여기서는 그림이 옛 위치에서 새 위치로 따라가는 것만 30 FPS 틱 단위로 보여 준다.
        /// <summary>방 미끄러짐 틱 수(30 FPS 5틱 = 0.167초) [제안].</summary>
        public const int SlideTicks = 5;
        /// <summary>막힘 흔들림: 틱마다 좌우 논리 px(4틱 = 0.13초) [제안].</summary>
        static readonly int[] ShakeOffsets = { 2, -2, 2, -2 };
        /// <summary>자동 확인용: 0 이상이면 흔들림을 이 틱의 모양으로 멈춰 보여 준다.</summary>
        public int FreezeShakeTick = -1;

        GameSim slideSim;
        int slideReset;
        float[] roomOffX, roomOffY, roomStart;   // 방(정체 번호)마다: 옛 위치에서 남은 어긋남(논리 px)과 시작 시각
        int[] roomCell;                          // 방이 마지막으로 있던 칸
        float runOffX, runOffY, runStart;        // 방과 함께 옮겨진 대상의 어긋남
        int runSeen;
        int shakeRoom = -1;
        float shakeStart, frameNow;

        /// <summary>미끄러짐·흔들림을 모두 끝난 상태로 만든다(자동 확인 캡처용).</summary>
        public void FinishSlides()
        {
            if (roomStart != null) for (int i = 0; i < roomStart.Length; i++) roomStart[i] = -999f;
            runStart = -999f;
            shakeRoom = -1;
        }

        /// <summary>막힘 흔들림 시작: 흔들릴 방의 정체 번호(조준한 방).</summary>
        public void StartShake(int roomIndex)
        {
            shakeRoom = roomIndex;
            shakeStart = Time.realtimeSinceStartup;
        }

        float Progress(float start) => Mathf.Clamp01(Mathf.FloorToInt((frameNow - start) * 30f) / (float)SlideTicks);

        void UpdateSlides(GameSim sim)
        {
            Board b = sim.Board;
            int n = b.Floors * b.Cols;
            frameNow = Time.realtimeSinceStartup;
            if (slideSim != sim || slideReset != sim.ResetSeq || roomOffX == null || roomOffX.Length != n)
            {
                if (roomOffX == null || roomOffX.Length != n)
                {
                    roomOffX = new float[n]; roomOffY = new float[n]; roomStart = new float[n]; roomCell = new int[n];
                }
                for (int i = 0; i < n; i++) { roomOffX[i] = roomOffY[i] = 0f; roomStart[i] = -999f; roomCell[i] = sim.CellIndexOf(i); }
                runOffX = runOffY = 0f; runStart = -999f; runSeen = sim.TargetShiftSeq;
                slideSim = sim; slideReset = sim.ResetSeq;
                shakeRoom = -1;
                return;
            }
            int cols = b.Cols;
            for (int i = 0; i < n; i++)
            {
                int ci = sim.CellIndexOf(i);
                if (ci == roomCell[i]) continue;
                float k = 1f - Progress(roomStart[i]);
                roomOffX[i] = roomOffX[i] * k + (roomCell[i] % cols - ci % cols) * b.SectorW;
                roomOffY[i] = roomOffY[i] * k + (roomCell[i] / cols - ci / cols) * b.SectorH;
                roomStart[i] = frameNow;
                roomCell[i] = ci;
            }
            if (sim.TargetShiftSeq != runSeen)
            {
                float k = 1f - Progress(runStart);
                runOffX = runOffX * k - sim.TargetShiftDX;
                runOffY = runOffY * k - sim.TargetShiftDY;
                runStart = frameNow;
                runSeen = sim.TargetShiftSeq;
            }
        }

        int ShakeX(int roomIndex)
        {
            if (roomIndex != shakeRoom) return 0;
            int k = FreezeShakeTick >= 0 ? FreezeShakeTick : Mathf.FloorToInt((frameNow - shakeStart) * 30f);
            return k >= 0 && k < ShakeOffsets.Length ? ShakeOffsets[k] : 0;
        }

        /// <summary>그 방(원래 자리)을 지금 그릴 맵 x·y(논리 px, 칸 왼쪽 위 기준 + 미끄러짐 + 흔들림).</summary>
        int RoomX(GameSim sim, int floor, int col)
        {
            int i = floor * sim.Board.Cols + col;
            return sim.CellIndexOf(i) % sim.Board.Cols * sim.Board.SectorW + Mathf.RoundToInt(roomOffX[i] * (1f - Progress(roomStart[i]))) + ShakeX(i);
        }

        int RoomY(GameSim sim, int floor, int col)
        {
            int i = floor * sim.Board.Cols + col;
            return sim.Board.SectorTop(sim.CellIndexOf(i) / sim.Board.Cols) + Mathf.RoundToInt(roomOffY[i] * (1f - Progress(roomStart[i])));
        }

        /// <summary>자동 확인용: 이 방을 지금 그릴 맵 x·y(논리 px).</summary>
        public Vector2Int DebugRoomPos(GameSim sim, int floor, int col) => new Vector2Int(RoomX(sim, floor, col), RoomY(sim, floor, col));

        public GameRenderer(PixelCanvas canvas)
        {
            cv = canvas;
            cv.EnsureFxTextures();   // W-028: 점선·디더 무늬(코드 생성)
        }

        // ---------- P1 코드 연출(W-028, vfx-direction §2) ----------
        // 판정은 GameSim이 즉시 하고, 여기서는 값 변화(조준 칸·Shift·미션 완료·카운트다운 숫자)를 읽어 30 FPS 틱 단위 연출만 입힌다. 길이·거리는 Fx에 모았다.

        /// <summary>조준틀이 지금 그려지는 맵 위치와 도착할 위치(자동 확인용).</summary>
        public Vector2Int CursorMapPos { get; private set; }
        public Vector2Int CursorTargetPos { get; private set; }
        /// <summary>도착 틱에 모서리 4개를 반짝이는 중인지, 지금까지 반짝인 프레임 수(누계).</summary>
        public bool CursorFlash { get; private set; }
        public int CursorFlashFrames { get; private set; }
        /// <summary>설계도 겹침의 옆 어긋남(+4 누름, -4 뗌, 0 제자리)과 뗀 뒤 잔상을 그리는 중인지.</summary>
        public int BlueprintOffsetX { get; private set; }
        public bool BlueprintGhost { get; private set; }
        /// <summary>추적도 게이지 끝 칸 깜빡임이 적용 중인지(Shift를 누르는 동안)와 이번 프레임이 꺼진 쪽인지.</summary>
        public bool GaugeBlinkActive { get; private set; }
        public bool GaugeBlinkOff { get; private set; }
        /// <summary>장면 뷰 테두리 띠: 0 없음, 1 빨간 디더 1px(추적도 70% 이상), 2 빨간 실선 2px(카운트다운).</summary>
        public int EdgeBand { get; private set; }
        /// <summary>카운트다운 숫자 글자 배율(숫자가 바뀐 앞 3틱 4, 그 뒤 3).</summary>
        public int CountdownScale { get; private set; } = Fx.CountdownBaseScale;
        /// <summary>미션 체크 아이콘 연출이 시작된 횟수(누계)와 지금 그리는 중인 수.</summary>
        public int CheckPops { get; private set; }
        public int CheckActiveCount { get; private set; }
        /// <summary>실패 지점 빨간 사각형: 지금 켜진 쪽인지, 그린 프레임 수(누계).</summary>
        public bool FailRectOn { get; private set; }
        public int FailRectFrames { get; private set; }

        GameSim fxSim;
        int fxReset;
        Vector2Int aimSeen;
        Vector2 aimFrom, cursorPos;
        float aimStart = -999f;
        bool bpPrev;
        Vector2Int bpPrevCell, bpGhostCell;
        float bpInStart = -999f, bpOutStart = -999f;
        int cdSec;
        float cdStart = -999f;
        bool[] chkPrev;
        float[] chkStart;

        int FxTick(float start) => Fx.TickOf(frameNow - start);

        void UpdateFx(GameSim sim)
        {
            int nm = sim.MissionCount;
            Vector2Int aim = sim.Aim;
            var target = new Vector2Int(RoomX(sim, aim.y, aim.x), RoomY(sim, aim.y, aim.x));
            bool bp = sim.IsBlueprint(aim.y, aim.x);
            int cd = sim.CountdownActive ? Mathf.CeilToInt(sim.Countdown) : 0;
            if (fxSim != sim || fxReset != sim.ResetSeq || chkPrev == null || chkPrev.Length != nm)
            {
                fxSim = sim; fxReset = sim.ResetSeq;
                aimSeen = aim; aimStart = -999f; cursorPos = target;
                bpPrev = bp; bpPrevCell = aim; bpInStart = bpOutStart = -999f;
                cdSec = cd; cdStart = -999f;
                chkPrev = new bool[nm]; chkStart = new float[nm];
                for (int i = 0; i < nm; i++) { chkPrev[i] = sim.MissionDone(i); chkStart[i] = -999f; }
                // W-030: 표지·링·효과 없음 상태를 처음부터 시작한다
                useSeen = sim.UseSeq; ringCount = 0; mkPrevCount = 0; noEffStart = -999f;
            }
            else
            {
                if (bp && !bpPrev) StartNoEffect(sim);                                                             // W-030 F5
                UpdateUse(sim);                                                                                    // W-030 F3
                if (aim != aimSeen) { aimFrom = cursorPos; aimStart = frameNow; aimSeen = aim; }                     // 1-1
                if (bp && !bpPrev) bpInStart = frameNow;                                                           // 1-2
                else if (!bp && bpPrev) { bpOutStart = frameNow; bpGhostCell = bpPrevCell; }                       // 1-3
                if (cd != cdSec) { cdSec = cd; if (cd > 0) cdStart = frameNow; }                                   // 1-6
                for (int i = 0; i < nm; i++)                                                                       // 1-8: 진행 중에 완료된 미션만(최종 완료는 의뢰가 끝난다)
                {
                    bool d = sim.MissionDone(i);
                    if (d && !chkPrev[i] && sim.Outcome == Outcome.Running) { chkStart[i] = frameNow; CheckPops++; }
                    chkPrev[i] = d;
                }
            }
            bpPrev = bp; bpPrevCell = aim;

            // 1-1: 틱 0·1은 이전 위치에서 1/3·2/3 지점, 틱 2에 도착(도착 틱에 모서리 반짝)
            int k = aimStart > -900f ? FxTick(aimStart) : 99;
            cursorPos = k < Fx.AimMoveTicks ? Vector2.Lerp(aimFrom, target, (k + 1) / (float)(Fx.AimMoveTicks + 1)) : (Vector2)target;
            CursorMapPos = new Vector2Int(Mathf.RoundToInt(cursorPos.x), Mathf.RoundToInt(cursorPos.y));
            CursorTargetPos = target;
            CursorFlash = k >= Fx.AimMoveTicks && k < Fx.AimMoveTicks + Fx.AimFlashTicks;
            if (CursorFlash) CursorFlashFrames++;

            // 1-2·1-3: 누르면 +x에서 제자리로, 떼면 -x로 밀리며 2틱 더 그려진 뒤 사라짐
            BlueprintOffsetX = 0; BlueprintGhost = false;
            if (bp) { if (bpInStart > -900f && FxTick(bpInStart) < Fx.BlueprintSlideTicks) BlueprintOffsetX = Fx.BlueprintSlidePx; }
            else if (bpOutStart > -900f && FxTick(bpOutStart) < Fx.BlueprintSlideTicks) { BlueprintOffsetX = -Fx.BlueprintSlidePx; BlueprintGhost = true; }

            // 1-5·1-6
            EdgeBand = sim.Outcome != Outcome.Running ? 0 : (sim.CountdownActive ? 2 : (sim.Tracking >= sim.TrackingMax * Fx.WarnRatio ? 1 : 0));
            CountdownScale = cd > 0 && cdStart > -900f && FxTick(cdStart) < Fx.CountdownPopTicks ? Fx.CountdownPopScale : Fx.CountdownBaseScale;

            // 1-4: Shift를 누르는 동안(추적도가 오르는 동안) 게이지 끝 칸 깜빡임. 켜짐 8틱·꺼짐 7틱(초당 2회)
            GaugeBlinkActive = sim.ShiftHeld && sim.Outcome == Outcome.Running && sim.Tracking > 0f && sim.Tracking < sim.TrackingMax;
            GaugeBlinkOff = GaugeBlinkActive && Fx.TickOf(frameNow) % Fx.GaugeBlinkPeriodTicks >= Fx.GaugeBlinkOnTicks;
        }

        // ---------- 조작 피드백(W-030, D-118) ----------
        // 판정은 GameSim이 즉시 한다. 여기서는 (F1) 누른 순간 바뀐 장치·밀린 방에 상태 표지, (F5) 바뀔 것이 없으면 회색 표지, (F3) 조력자가 바뀐 장치를 실제로
        // 이용한 순간(GameSim.UseSeq) 민트 링을 30 FPS 틱 단위로 그린다. 그리는 층은 설계도 겹침 위(L8 표시). 표지 배열은 미리 만들어 프레임마다 할당하지 않는다.

        static readonly Color32 BpBack = new Color32(18, 54, 118, 255);   // 팔레트 B(청사진 바탕)

        struct Marker
        {
            public int Key;
            public string Text;
            public int Cx, Bottom, W;          // 가운데 x, 표지 아래 y(스크린 논리 좌표), 폭
            public bool Gray;
            public int Rx, Ry, Rw, Rh;         // 같이 반짝일 장치 그림·섹터 사각형(Rw 0이면 없음)
            public float Start;
            public int X, Y;                   // 겹침을 풀고 난 왼쪽 위
        }

        const int MaxMarkers = 24;
        readonly Marker[] mk = new Marker[MaxMarkers], mkPrev = new Marker[MaxMarkers];
        int mkCount, mkPrevCount;
        float noEffStart = -999f;
        bool noEffRail;
        int useSeen, ringCount;
        readonly float[] ringStart = new float[Fx.UseRingMax];
        readonly UseEvent[] ringEv = new UseEvent[Fx.UseRingMax];

        /// <summary>자동 확인용: 지금 그리는 표지 수와 i번째 표지의 글자·왼쪽 위·반짝임(나타난 뒤 2틱 안)·회색 여부.</summary>
        public int MarkerCount => mkCount;
        public string MarkerText(int i) => mk[i].Text;
        public Vector2Int MarkerPos(int i) => new Vector2Int(mk[i].X, mk[i].Y);
        public bool MarkerFlashing(int i) => FxTick(mk[i].Start) < Fx.MarkerFlashTicks;
        public bool MarkerGray(int i) => mk[i].Gray;
        /// <summary>자동 확인용: 표지가 새로 나타난 횟수(누계), 회색 표지(효과 없음·밀기 안내)를 그린 프레임 수(누계).</summary>
        public int MarkersAppeared { get; private set; }
        public int NoEffectFrames { get; private set; }
        /// <summary>자동 확인용: 링이 시작된 횟수(누계), 지금 그리는 링 수, 마지막 링 반지름(px)과 마지막 시작 사건.</summary>
        public int UseRingsStarted { get; private set; }
        public int UseRingActive { get; private set; }
        public int LastRingHalf { get; private set; }
        public UseKind LastRingKind { get; private set; }

        /// <summary>그 장치가 표지·반짝임 대상 종류인가.</summary>
        static bool Markable(ObjectKind k) => k == ObjectKind.Door || k == ObjectKind.Stairs || k == ObjectKind.Hatch || k == ObjectKind.Elevator || k == ObjectKind.Camera;

        /// <summary>Shift를 눌러 설계도 상태가 되었을 때, 현실과 다른 유효 상태의 장치(또는 섹터 전체 차단막)의 수.</summary>
        int ChangedCount(GameSim sim)
        {
            Board b = sim.Board;
            int n = 0;
            foreach (SectorObject o in b.Objects)
                if (Markable(o.Def.kind) && sim.ObjectOn(o) != o.Def.realityOn) n++;
            Vector2Int aim = sim.Aim;
            if (b.IsWhole(aim.y, aim.x) && sim.SectorBlocked(aim.y, aim.x) != b.Blocked(aim.y, aim.x, false)) n++;
            return n;
        }

        /// <summary>F5: Shift를 눌렀는데 바뀌는 장치가 하나도 없으면 회색 표지를 0.6초 띄운다(레일 방이면 방 밀기 안내). 누른 순간 한 번만 판단한다.</summary>
        void StartNoEffect(GameSim sim)
        {
            if (sim.Outcome != Outcome.Running || ChangedCount(sim) > 0) return;
            noEffStart = frameNow;
            noEffRail = sim.Board.HasRails && sim.Board.HasRail(sim.Aim.y, sim.Aim.x);
        }

        /// <summary>F3: GameSim이 남긴 새 사건마다 링을 시작한다. 같은 층·같은 자리의 이전 링은 끝낸다(새 연출이 이전 연출을 끝냄).</summary>
        void UpdateUse(GameSim sim)
        {
            if (sim.UseSeq < useSeen) useSeen = sim.UseSeq;
            while (useSeen < sim.UseSeq)
            {
                useSeen++;
                if (sim.UseSeq - useSeen >= GameSim.UseHistory) continue;
                UseEvent e = sim.UseAt(useSeen);
                for (int i = 0; i < ringCount; i++)
                    if (ringEv[i].Floor == e.Floor && Mathf.Abs(ringEv[i].X - e.X) <= Fx.UseRingSamePlacePx) ringStart[i] = -999f;
                int slot = -1;
                for (int i = 0; i < ringCount; i++) if (FxTick(ringStart[i]) >= Fx.UseRingTicks) { slot = i; break; }
                if (slot < 0)
                {
                    if (ringCount < Fx.UseRingMax) slot = ringCount++;
                    else slot = 0;
                }
                ringEv[slot] = e; ringStart[slot] = frameNow;
                UseRingsStarted++;
                LastRingKind = e.Kind;
            }
        }

        static int RingCenterY(Board b, UseEvent e)
        {
            switch (e.Kind)
            {
                case UseKind.LaserPassed:
                case UseKind.RoomBridged: return b.SectorTop(e.Floor) + b.SectorH / 2;
                case UseKind.CameraEvaded: return b.SectorTop(e.Floor) + 14;
                default: return b.FloorLineY(e.Floor) - 18;
            }
        }

        string MarkerTextOf(SectorObject o, bool on)
        {
            switch (o.Def.kind)
            {
                case ObjectKind.Elevator: return on ? "RUN" : "STOP";
                case ObjectKind.Camera: return on ? "ON" : "OFF";
                default: return on ? "OPEN" : "CLOSED";
            }
        }

        void AddMarker(int key, string text, int cx, int bottom, bool gray, int rx, int ry, int rw, int rh)
        {
            if (mkCount >= MaxMarkers) return;
            mk[mkCount++] = new Marker { Key = key, Text = text, Cx = cx, Bottom = bottom, W = PixelCanvas.TextWidth(text) + 2 * Fx.MarkerPadX + 2, Gray = gray, Rx = rx, Ry = ry, Rw = rw, Rh = rh };
        }

        /// <summary>모인 표지를 놓을 자리로 정하고(가운데 정렬, 장면 뷰 안으로 당김, 겹치면 위로 한 줄씩) 시작 시각을 이어 붙인다.</summary>
        void LayoutMarkers()
        {
            for (int i = 0; i < mkCount; i++)
            {
                Marker m = mk[i];
                m.Start = frameNow;
                bool isNew = true;
                for (int j = 0; j < mkPrevCount; j++) if (mkPrev[j].Key == m.Key) { m.Start = mkPrev[j].Start; isNew = false; break; }
                if (isNew) MarkersAppeared++;
                m.X = Mathf.Clamp(m.Cx - m.W / 2, VX + 1, VX + VW - m.W - 1);
                m.Y = m.Bottom - Fx.MarkerHeightPx;
                bool moved = true;
                for (int guard = 0; moved && guard < MaxMarkers; guard++)
                {
                    moved = false;
                    for (int j = 0; j < i; j++)
                    {
                        Marker o = mk[j];
                        if (m.X < o.X + o.W && o.X < m.X + m.W && m.Y < o.Y + Fx.MarkerHeightPx && o.Y < m.Y + Fx.MarkerHeightPx) { m.Y -= Fx.MarkerStepPx; moved = true; break; }
                    }
                }
                m.Y = Mathf.Max(m.Y, VY + 1);
                mk[i] = m;
            }
        }

        void DrawFeedback(GameSim sim, Board b, int cx, int cy, int cux, int cuy, int cuw)
        {
            mkCount = 0;
            Vector2Int aim = sim.Aim;
            if (sim.ShiftHeld && sim.Outcome == Outcome.Running)
            {
                if (sim.Pushed)
                {
                    // F1: 밀린 방(맞바꾼 방 포함)마다 위쪽 가운데에 MOVED
                    for (int n = 0; n < 2; n++)
                    {
                        int id = n == 0 ? sim.PushedRoom : sim.SwappedRoom;
                        if (id < 0) continue;
                        int rf = id / b.Cols, rc = id % b.Cols;
                        int sx = VX + RoomX(sim, rf, rc) - cx, sy = VY + RoomY(sim, rf, rc) - cy;
                        AddMarker(2000 + id, "MOVED", sx + b.SectorW / 2, sy + 4 + Fx.MarkerHeightPx, false, sx, sy, b.SectorW, b.SectorH);
                    }
                }
                else if (sim.IsBlueprint(aim.y, aim.x))
                {
                    // F1: 현실과 유효 상태가 달라진 장치마다 그림 위쪽 4px 위에 표지
                    for (int i = 0; i < b.Objects.Count; i++)
                    {
                        SectorObject o = b.Objects[i];
                        if (!Markable(o.Def.kind) || !o.Def.drawSprite) continue;
                        bool on = sim.ObjectOn(o);
                        if (on == o.Def.realityOn) continue;
                        string key = Hi(ObjectKey(o));
                        if (key == null) continue;
                        int w = cv.WidthOf(key), h = cv.HeightOf(key);
                        int rx = RoomX(sim, o.Floor, o.Col) - b.SectorLeft(o.Col), ry = RoomY(sim, o.Floor, o.Col) - b.SectorTop(o.Floor);
                        int sx = VX + o.DrawAbsX - cx + rx;
                        int sy = o.Def.kind == ObjectKind.Camera ? VY + b.SectorTop(o.Floor) + ry + CeilLogic - cy : VY + b.FloorLineY(o.Floor) + ry - h - cy;
                        int bottom = sy - Fx.MarkerGapPx;
                        int roomTop = VY + RoomY(sim, o.Floor, o.Col) - cy;
                        if (bottom - Fx.MarkerHeightPx < roomTop) bottom = sy + h + Fx.MarkerGapPx + Fx.MarkerHeightPx;   // 방 위로 벗어나면(천장 카메라) 그림 아래에 둔다
                        AddMarker(i, MarkerTextOf(o, on), sx + w / 2, bottom, false, sx, sy, w, h);
                    }
                    if (b.IsWhole(aim.y, aim.x) && sim.SectorBlocked(aim.y, aim.x) != b.Blocked(aim.y, aim.x, false))
                    {
                        int sx = VX + RoomX(sim, aim.y, aim.x) - cx, sy = VY + RoomY(sim, aim.y, aim.x) - cy;
                        AddMarker(1000 + aim.y * b.Cols + aim.x, sim.SectorBlocked(aim.y, aim.x) ? "ON" : "OFF", sx + b.SectorW / 2, sy + 4 + Fx.MarkerHeightPx, false, sx, sy, b.SectorW, b.SectorH);
                    }
                }
            }
            // F5: 효과 없음 / 레일 방 안내(조준틀 위쪽 가운데, 0.6초). 방이 밀리면 안내가 필요 없어 끝낸다
            if (sim.Pushed) noEffStart = -999f;   // 방을 밀면 안내는 끝(떼어 제자리로 돌아와도 다시 띄우지 않음)
            bool noEff = noEffStart > -900f && frameNow - noEffStart < Fx.NoEffectSeconds && sim.Outcome == Outcome.Running && !sim.Pushed;
            if (noEff) AddMarker(3000, noEffRail ? Fx.RailHintText : Fx.NoEffectText, cux + cuw / 2, cuy - 2, true, 0, 0, 0, 0);

            LayoutMarkers();
            for (int i = 0; i < mkCount; i++)
            {
                Marker m = mk[i];
                bool flash = FxTick(m.Start) < Fx.MarkerFlashTicks;
                if (flash && m.Rw > 0) cv.Box(m.Rx - 1, m.Ry - 1, m.Rx + m.Rw, m.Ry + m.Rh, Wh);   // 장치 그림 테두리 1회 반짝
                cv.Fill(m.X, m.Y, m.W, Fx.MarkerHeightPx, m.Gray ? B4 : BpBack);
                cv.Box(m.X, m.Y, m.X + m.W - 1, m.Y + Fx.MarkerHeightPx - 1, flash ? Wh : (m.Gray ? B3 : Cy));
                cv.Text(m.X + 1 + Fx.MarkerPadX, m.Y + 2, m.Text, m.Gray ? Cy : Wh);
                if (m.Gray) NoEffectFrames++;
            }
            for (int i = 0; i < mkCount; i++) mkPrev[i] = mk[i];
            mkPrevCount = mkCount;

            // F3: 민트 사각 링 — 장치(또는 섹터 가운데)에서 반지름 2px -> 10px로 퍼지며 0.3초 뒤 사라짐
            UseRingActive = 0;
            for (int i = 0; i < ringCount; i++)
            {
                int k = FxTick(ringStart[i]);
                if (k < 0 || k >= Fx.UseRingTicks) continue;
                int half = Fx.UseRingStartPx + k * (Fx.UseRingEndPx - Fx.UseRingStartPx) / (Fx.UseRingTicks - 1);
                int rx = VX + Mathf.RoundToInt(ringEv[i].X) - cx, ry = VY + RingCenterY(b, ringEv[i]) - cy;
                cv.Box(rx - half, ry - half, rx + half, ry + half, Gn);
                UseRingActive++;
                LastRingHalf = half;
            }
        }

        // ---------- 바깥 환경(W-011, D-103) ----------

        /// <summary>환경 이펙트 값(프리셋 10개). GameView가 넣는다. 없으면 환경 이펙트 없이 예전 배경 그림으로 그린다.</summary>
        public EnvironmentSet EnvSet;
        EnvironmentFx env;
        GameSim envSim;
        int envReset;
        bool forceEnv;
        TimeOfDay forceTime;
        Weather forceWeather;
        int envStep;
        /// <summary>지금 의뢰의 환경 이펙트(없으면 null). 자동 확인이 입자 배열·위치를 읽는다.</summary>
        public EnvironmentFx Env => env;
        /// <summary>이번 프레임 그리기 순서 번호(자동 확인용): 환경 입자(LE) 첫 그리기, 옥상 두 번째 입자, 오브젝트, 조력자. 안 그렸으면 0.</summary>
        public int StepLe, StepSecond, StepObjects, StepRunner;

        /// <summary>자동 확인용: 지금 의뢰의 시간대·날씨를 덮어쓴다(프리셋별 캡처·성능 측정). 입자 배열도 다시 만든다.</summary>
        public void DebugForceEnv(TimeOfDay time, Weather weather)
        {
            forceEnv = true; forceTime = time; forceWeather = weather;
            envSim = null;   // 다음 Draw에서 다시 만든다
        }

        /// <summary>자동 확인용: 덮어쓰기를 풀고 의뢰 데이터의 값으로 되돌린다.</summary>
        public void DebugClearForceEnv()
        {
            forceEnv = false;
            envSim = null;
        }

        EnvironmentFx BuildEnv(Board b)
        {
            if (EnvSet == null || b.Layout == null) return null;
            TimeOfDay t = forceEnv ? forceTime : b.TimeOfDay;
            Weather w = forceEnv ? forceWeather : b.Weather;
            EnvPreset p = EnvSet.Find(t, w);
            if (p == null) { Debug.LogError("[GameRenderer] 환경 프리셋이 없습니다: " + t + " " + w); return null; }
            return new EnvironmentFx(cv, b, p, EnvSet);
        }

        /// <summary>
        /// 환경 이펙트 갱신: 의뢰가 시작될 때 한 번 만들고(입자 배열 한 번 할당), 처음부터 다시 하면 같은 배열의 값만 되돌린다.
        /// 시간은 일시정지 중에만 멈춘다. 브리핑·종료 대화·결과 창 중에는 계속 흐른다(조력자 애니메이션과 같은 규칙).
        /// </summary>
        void UpdateEnv(GameView v, GameSim sim)
        {
            StepLe = StepSecond = StepObjects = StepRunner = 0;
            envStep = 0;
            if (envSim != sim)
            {
                envSim = sim;
                envReset = sim.ResetSeq;
                env = BuildEnv(sim.Board);
            }
            else if (envReset != sim.ResetSeq)
            {
                envReset = sim.ResetSeq;
                if (env != null) env.Reseed();
            }
            if (env == null) return;
            float dt = !v.GameFrozen ? Mathf.Min(Time.deltaTime, 0.1f) : 0f;
            env.Advance(dt);
            env.BeginFrame(sim.ShiftHeld, v.Progress.EnvFxReduced, VX * PixelCanvas.Z, VY * PixelCanvas.Z, (VX + VW) * PixelCanvas.Z, (VY + VH) * PixelCanvas.Z);
        }

        // ---------- 그리기 ----------

        public void Draw(GameView v)
        {
            GameSim sim = v.Sim;
            if (layerBoard != sim.Board || !cv.HasLayer) BuildLayer(v);
            cv.RestoreLayer();
            UpdateSlides(sim);
            UpdateFx(sim);
            UpdateEnv(v, sim);
            UpdateRunnerAnim(v, sim);

            DrawScene(v);
            if (v.Tut != null && (v.CurrentPhase == GameView.Phase.Playing || v.GameFrozen)) DrawTutorial(v);
            DrawRightPanel(v);
            DrawFace(v);
            DrawBottom(v);
            // 게임오버 창 뒤에 0.0 이 남지 않도록 진행 중일 때만 카운트다운 숫자를 그린다
            if (sim.CountdownActive && sim.Outcome == Outcome.Running) DrawCountdown(sim);

            if (v.CurrentPhase == GameView.Phase.Paused) DrawPause(v);
            else if (v.GameFrozen) v.DrawSettingsInGame(222, 171);   // W-019: 일시정지에서 연 설정 화면(일시정지 창 자리)
            else if (v.CurrentPhase == GameView.Phase.Ended) DrawResult(v);
        }

        /// <summary>바뀌지 않는 부분을 그려 고정 층으로 저장한다.</summary>
        void BuildLayer(GameView v)
        {
            Board b = v.Sim.Board;
            cv.Blit("backgrounds/city_night_composite", 0, 0);
            cv.Nine("ui/window_frame", 8, 8, 624, 344);
            cv.Text(16, 15, "SHIFT://REMOTE  -  REQUEST " + v.Desk.Number(v.ActiveIndex), Wh);
            cv.Blit("ui/icon_pause", 596, 11);
            cv.Blit("ui/badge_esc", 602, 17);

            // 장면 패널(안쪽 뷰는 매 프레임 DrawScene이 그린다)
            cv.Nine("ui/panel", 14, 32, 416, 278);
            cv.Text(22, 38, "FLOOR VIEW / " + b.BuildingName, Cy);

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

        // ---------- 조력자 애니메이션(W-009) ----------
        // 판정은 GameSim이 즉시 하고, 여기서는 그 결과(이동·기다림·해치 낙하)를 따라가며 그림만 고른다(animation-audit §3-1).

        public const string AnimWalk = "walk", AnimWait = "wait", AnimFall = "fall";
        /// <summary>지금 재생 중인 조력자 동작(walk·wait·fall). 자동 확인용.</summary>
        public string RunnerAnim { get; private set; } = AnimWait;
        /// <summary>지금 동작이 시작된 뒤 지난 시간(초). 자동 확인용.</summary>
        public float RunnerAnimTime { get; private set; }
        /// <summary>떨어짐을 끝까지 재생한 횟수(자동 확인용).</summary>
        public int FallsPlayed { get; private set; }
        /// <summary>마지막으로 그린 조력자 시트 이름(자동 확인용).</summary>
        public string RunnerSheet { get; private set; }
        GameSim animSim;
        int seenFalls;
        float fallTime = -1f;

        static string SheetKey(string state, bool right) => "anim/runner128_" + state + (right ? "" : "_left") + "_sheet";

        void UpdateRunnerAnim(GameView v, GameSim sim)
        {
            if (animSim != sim) { animSim = sim; seenFalls = sim.FallCount; fallTime = -1f; RunnerAnim = AnimWait; RunnerAnimTime = 0f; }
            if (sim.FallCount < seenFalls) { seenFalls = sim.FallCount; fallTime = -1f; }   // Reset으로 줄었음
            if (sim.FallCount > seenFalls) { seenFalls = sim.FallCount; fallTime = 0f; RunnerAnim = AnimFall; RunnerAnimTime = 0f; }

            // 일시정지 중에는 시간이 흐르지 않는다(그림이 멈춤). 브리핑·종료 대화 중에는 흐른다(떨어짐이 끝까지 재생됨)
            float dt = !v.GameFrozen ? Mathf.Min(Time.deltaTime, 0.1f) : 0f;
            string want;
            if (fallTime >= 0f)
            {
                fallTime += dt;
                string key = SheetKey(AnimFall, sim.TargetFacingRight);
                if (cv.IsSheet(key) && fallTime >= cv.SheetSeconds(key)) { fallTime = -1f; FallsPlayed++; want = null; }
                else want = AnimFall;
            }
            else want = null;
            if (want == null) want = sim.TargetMoving ? AnimWalk : AnimWait;
            if (want != RunnerAnim) { RunnerAnim = want; RunnerAnimTime = 0f; }
            else RunnerAnimTime += dt;
        }

        void DrawRunner(GameSim sim, int tx, int ty)
        {
            string key = SheetKey(RunnerAnim, sim.TargetFacingRight);
            RunnerSheet = key;
            if (!cv.IsSheet(key))
            {
                cv.Blit(sim.TargetFacingRight ? "sprites/runner_right" : "sprites/runner_left", tx, ty);   // 시트가 없으면 이전 32px 그림
                return;
            }
            // 128px 프레임 = 논리 32x32. 이전 32px 그림과 같은 자리(몸 중심 x·바닥선)에 그린다.
            cv.BlitFrame(key, cv.FrameAt(key, RunnerAnimTime), tx, ty);
        }

        // ---------- 장면 ----------

        void DrawScene(GameView v)
        {
            GameSim sim = v.Sim;
            Board b = sim.Board;
            int cx = sim.CamX, cy = sim.CamY;

            cv.Fill(VX, VY, VW, VH, Pa);
            cv.SetClip(VX, VY, VW, VH);
            if (b.Layout != null) DrawLayout(sim, b, b.Layout, cx, cy);   // W-010: 방 레이어(L0~L3)
            else cv.BlitPart(b.BackgroundKey, cx, cy, VW, VH, VX, VY);

            // 현실 오브젝트(그림 아래 끝이 바닥선에 닿음). 방이 옮겨졌으면 오브젝트도 그 방과 함께(W-014)
            if (StepObjects == 0) StepObjects = ++envStep;
            foreach (SectorObject o in b.Objects)
            {
                if (!o.Def.drawSprite) continue;
                DrawObject(sim, b, o, cx, cy, RoomX(sim, o.Floor, o.Col) - b.SectorLeft(o.Col), RoomY(sim, o.Floor, o.Col) - b.SectorTop(o.Floor));
            }
            // 섹터 전체형: 지금 유효 상태에서 차단막이 켜져 있을 때만 그린다(꺼짐은 그리지 않음)
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++)
                    if (b.IsWhole(f, c) && sim.SectorBlocked(f, c))
                        cv.Blit(Hi("objects/blocker_on"), VX + RoomX(sim, f, c) - cx, VY + RoomY(sim, f, c) - cy);
            // W-010: 레이아웃의 L4 그림(주인공 방 문). 그림 아래 끝이 바닥선에 닿는다
            if (b.Layout != null)
            {
                SceneLayout.Prop[] props = b.Layout.Props;
                for (int i = 0; i < props.Length; i++)
                {
                    string pk = props[i].Key;
                    int dx = RoomX(sim, props[i].Floor, props[i].Col) - b.SectorLeft(props[i].Col), dy = RoomY(sim, props[i].Floor, props[i].Col) - b.SectorTop(props[i].Floor);
                    cv.Blit(pk, VX + b.SectorLeft(props[i].Col) + props[i].XPx / PixelCanvas.Z - cx + dx, VY + b.FloorLineY(props[i].Floor) - cv.HeightOf(pk) - cy + dy);
                }
            }

            // 대상: 몸 중심 x를 가운데로, 그림 아래 끝이 바닥선에 닿음. 방과 함께 옮겨졌으면 방과 같이 미끄러진다(W-014)
            float runK = 1f - Progress(runStart);
            int tx = VX + Mathf.RoundToInt(sim.TargetX) - 16 - cx + Mathf.RoundToInt(runOffX * runK);
            int ty = VY + b.FloorLineY(sim.TargetFloor) - 32 - cy + Mathf.RoundToInt(runOffY * runK);
            if (StepRunner == 0) StepRunner = ++envStep;
            DrawRunner(sim, tx, ty);
            if (sim.AlertMarkLeft > 0f) DrawAlertMark(tx + 16, ty);   // W-016: 위험을 보고 돌아설 때·발각 때 머리 위 ! 0.5초

            // 설계도 겹침(D-015): Shift를 누르는 동안 조준한 섹터 위에. 현실 그림은 아래에 그대로 보인다. 밀린 방은 현실 상태라 겹치지 않는다(W-014 R6).
            // W-028: Shift 누름은 4px 옆(+x)에서 들어오고, 뗌은 반대쪽(-x)으로 밀리며 2틱 더 그려진 뒤 사라진다
            Vector2Int aim = sim.Aim;
            int ax = RoomX(sim, aim.y, aim.x), ay = RoomY(sim, aim.y, aim.x);
            if (sim.IsBlueprint(aim.y, aim.x)) DrawBlueprint(b, aim, ax, ay, cx, cy, BlueprintOffsetX);
            else if (BlueprintGhost) DrawBlueprint(b, bpGhostCell, RoomX(sim, bpGhostCell.y, bpGhostCell.x), RoomY(sim, bpGhostCell.y, bpGhostCell.x), cx, cy, BlueprintOffsetX);
            // W-028 1-1: 조준틀은 이전 섹터에서 새 섹터로 보간 이동, 도착 틱에 모서리 4개 흰색 1px
            int cux = VX + CursorMapPos.x - cx, cuy = VY + CursorMapPos.y - cy;
            cv.Blit("objects/sector_cursor", cux, cuy);
            if (CursorFlash)
            {
                int cw = cv.WidthOf("objects/sector_cursor"), ch = cv.HeightOf("objects/sector_cursor");
                cv.Fill(cux, cuy, 1, 1, Wh); cv.Fill(cux + cw - 1, cuy, 1, 1, Wh);
                cv.Fill(cux, cuy + ch - 1, 1, 1, Wh); cv.Fill(cux + cw - 1, cuy + ch - 1, 1, 1, Wh);
            }
            DrawFeedback(sim, b, cx, cy, cux, cuy, cv.WidthOf("objects/sector_cursor"));   // W-030: 상태 표지·효과 없음·작동 링(설계도 겹침·조준틀 위)
            DrawChecks(sim, b, cx, cy, tx, ty);
            DrawFailRect(v, sim, b, cx, cy);
            cv.ClearClip();

            cv.Box(15, 47, 428, 308, Bb);
            DrawEdgeBand();
            DrawTargetMarker(sim, tx, ty);
        }

        void DrawBlueprint(Board b, Vector2Int cell, int ax, int ay, int cx, int cy, int off)
        {
            cv.Blit(Hi("objects/bp_sector"), VX + ax - cx + off, VY + ay - cy);
            if (b.IsWhole(cell.y, cell.x)) return;
            foreach (SectorObject o in b.Objects)
                if (o.Floor == cell.y && o.Col == cell.x) DrawBlueprintObject(b, o, cx, cy, ax - b.SectorLeft(cell.x) + off, ay - b.SectorTop(cell.y));
        }

        /// <summary>W-028 1-5·1-6: 장면 뷰 테두리 안쪽 띠. 1 = 빨간 디더 1px(한 칸 걸러 한 칸), 2 = 빨간 실선 2px.</summary>
        void DrawEdgeBand()
        {
            if (EdgeBand == 1)
            {
                const int z = PixelCanvas.Z;
                cv.TilePx("fx/dash_h_rd", VX * z, VY * z, VW * z, z, VX * z, VY * z);
                cv.TilePx("fx/dash_h_rd", VX * z, (VY + VH - 1) * z, VW * z, z, VX * z, (VY + VH - 1) * z);
                cv.TilePx("fx/dash_v_rd", VX * z, VY * z, z, VH * z, VX * z, VY * z);
                cv.TilePx("fx/dash_v_rd", (VX + VW - 1) * z, VY * z, z, VH * z, (VX + VW - 1) * z, VY * z);
            }
            else if (EdgeBand == 2)
            {
                int t = Fx.CountdownBandPx;
                cv.Fill(VX, VY, VW, t, Rd);
                cv.Fill(VX, VY + VH - t, VW, t, Rd);
                cv.Fill(VX, VY + t, t, VH - 2 * t, Rd);
                cv.Fill(VX + VW - t, VY + t, t, VH - 2 * t, Rd);
            }
        }

        /// <summary>W-028 1-8: 진행 중 완료된 미션의 지점(단말 등, 없으면 입장 섹터, 그것도 없으면 대상 머리 위)에 체크 아이콘이 4px 튀어 올랐다 사라진다.</summary>
        void DrawChecks(GameSim sim, Board b, int cx, int cy, int tx, int ty)
        {
            CheckActiveCount = 0;
            if (chkStart == null) return;
            for (int i = 0; i < chkStart.Length; i++)
            {
                if (chkStart[i] < -900f) continue;
                int k = FxTick(chkStart[i]);
                if (k < 0 || k >= Fx.CheckTicks) continue;
                MissionPoint(sim, b, i, cx, cy, tx, ty, out int x, out int y);
                int rise = Mathf.Min(Fx.CheckRisePx, k * Fx.CheckRisePx / Fx.CheckRiseTicks);
                cv.Blit("ui/checkbox_done", x, y - rise);
                CheckActiveCount++;
            }
        }

        void MissionPoint(GameSim sim, Board b, int i, int cx, int cy, int tx, int ty, out int x, out int y)
        {
            foreach (SectorObject o in b.Objects)
            {
                if (o.Def.mission != i || o.Def.kind == ObjectKind.Exit) continue;
                string key = Hi(ObjectKey(o));
                int w = key != null ? cv.WidthOf(key) : 9, h = key != null ? cv.HeightOf(key) : 0;
                int rx = RoomX(sim, o.Floor, o.Col) - b.SectorLeft(o.Col), ry = RoomY(sim, o.Floor, o.Col) - b.SectorTop(o.Floor);
                x = VX + o.DrawAbsX - cx + rx + w / 2 - 4;
                y = VY + b.FloorLineY(o.Floor) + ry - h - 11 - cy;
                return;
            }
            MissionDef m = sim.Missions[i];
            if (m.kind == MissionKind.EnterSector)
            {
                x = VX + RoomX(sim, m.floor, m.col) + b.SectorW / 2 - 4 - cx;
                y = VY + RoomY(sim, m.floor, m.col) + b.SectorH / 2 - 4 - cy;
                return;
            }
            x = tx + 16 - 4;   // 지점을 알 수 없으면 대상 머리 위
            y = ty - 12;
        }

        /// <summary>W-028 1-9: 실패 지점에 닿은 섹터 위에 빨간 반투명 사각형이 0.1초 켜짐·0.1초 꺼짐을 2번 깜빡인다(그 뒤 재시작은 GameView).</summary>
        void DrawFailRect(GameView v, GameSim sim, Board b, int cx, int cy)
        {
            FailRectOn = false;
            float fe = v.FailElapsed;
            if (fe < 0f) return;
            int k = Fx.TickOf(fe);
            FailRectOn = k < Fx.FailTotalTicks && k % (Fx.FailOnTicks + Fx.FailOffTicks) < Fx.FailOnTicks;
            if (!FailRectOn) return;
            FailRectFrames++;
            Vector2Int c = v.FailCell;
            cv.FillAlpha(VX + RoomX(sim, c.y, c.x) - cx, VY + RoomY(sim, c.y, c.x) - cy, b.SectorW, b.SectorH, Rd, Fx.FailAlpha);
        }

        const string CityKey = "backgrounds/city_night_composite";

        /// <summary>
        /// 방 레이어 그리기(W-010, 클립 안). 순서: L0 도시(시차 1/2) -> 지붕 띠 -> 땅 -> 섹터마다 L1 -> L2 -> 켜진 L3 전부(없으면 어둠).
        /// 보이는 섹터만 그리고, 그림 키는 SceneLayout이 미리 만든 문자열을 쓴다(프레임마다 문자열을 만들지 않음).
        /// </summary>
        void DrawLayout(GameSim sim, Board b, SceneLayout lay, int cx, int cy)
        {
            // L0: 도시 배경. 카메라 이동의 절반 속도. 640x360 그림 안에 머물게 잘라 경계에 빈칸이 생기지 않는다
            int px = Mathf.Clamp(cx / 2, 0, Mathf.Max(0, cv.WidthOf(CityKey) - VW));
            int py = Mathf.Clamp(cy / 2, 0, Mathf.Max(0, cv.HeightOf(CityKey) - VH));
            if (env != null)
            {
                // W-011: 하늘색(시간대·날씨별 위→아래 띠 디더) 위에 도시 건물 층(밤: 불 켜진 창 / 낮: 밝기 3단 색). 그리는 순서는 그대로 L0 단계
                env.DrawSky(cv, VX, VY, VW, VH);
                for (int ci = 0; ci < env.CityKeys.Length; ci++) cv.BlitPart(env.CityKeys[ci], px, py, VW, VH, VX, VY);
            }
            else cv.BlitPart(CityKey, px, py, VW, VH, VX, VY);

            // 지붕 띠(열마다)와 맵 아래 땅(earth 위쪽 groundHeight만큼, 열마다)
            int groundY = b.SectorTop(b.Floors);
            int groundH = lay.GroundPx / PixelCanvas.Z;
            for (int c = 0; c < b.Cols; c++)
            {
                int sx = VX + b.SectorLeft(c) - cx;
                if (sx >= VX + VW || sx + b.SectorW <= VX) continue;
                cv.Blit(lay.RoofKey, sx, VY - cy);
                cv.BlitPart(lay.EarthKey, 0, 0, b.SectorW, groundH, sx, VY + groundY - cy);
            }

            // W-014: 방이 없는 칸(원래 빈 칸, 또는 방이 밀려 나가 비어 있는 칸)은 어두운 바깥 배경 단색(팔레트 K)
            for (int f = 0; f < b.Floors; f++)
            {
                int sy = VY + b.SectorTop(f) - cy;
                if (sy >= VY + VH || sy + b.SectorH <= VY) continue;
                for (int c = 0; c < b.Cols; c++)
                {
                    int sx = VX + b.SectorLeft(c) - cx;
                    if (sx >= VX + VW || sx + b.SectorW <= VX) continue;
                    if (b.IsEmpty(f, c) || sim.RoomIndexAt(f, c) < 0) cv.Fill(sx, sy, b.SectorW, b.SectorH, K);
                }
            }

            // 섹터(방): 정체(원래 자리)마다 지금 칸 위치에 그린다. 화면에 보이는 것만
            for (int f = 0; f < b.Floors; f++)
            {
                for (int c = 0; c < b.Cols; c++)
                {
                    SceneLayout.SectorView v = lay.Sector(f, c);
                    if (v == null) continue;
                    int sx = VX + RoomX(sim, f, c) - cx, sy = VY + RoomY(sim, f, c) - cy;
                    if (sy >= VY + VH || sy + b.SectorH <= VY || sx >= VX + VW || sx + b.SectorW <= VX) continue;
                    if (v.Room == null) { cv.Blit(v.Tile, sx, sy); continue; }
                    // W-011: 창 구멍이 있는 방은 L0 위·L1 아래에 환경 입자(LE), L1 위에 유리 물방울, 옥상(지붕 없는 방)은 L2 위에 입자 한 번 더.
                    // 환경 밝기(실내 어둠·창 빛)는 L2 위·L3 조명 아래(lighting-shading §4: 조명·어둠은 L4 아래).
                    int slot0 = 0, slotN = 0, ox = sx * PixelCanvas.Z, oy = sy * PixelCanvas.Z;
                    if (env != null) env.SlotRange(f, c, out slot0, out slotN);
                    for (int si = slot0; si < slot0 + slotN; si++)
                        if (env.DrawLe(cv, si, ox, oy) > 0 && StepLe == 0) StepLe = ++envStep;
                    cv.Blit(v.Room.L1, sx, sy);
                    for (int si = slot0; si < slot0 + slotN; si++) env.DrawGlass(cv, si, ox, oy);
                    cv.Blit(v.Room.L2, sx, sy);
                    for (int si = slot0; si < slot0 + slotN; si++)
                        if (env.DrawSecond(cv, si, ox, oy) > 0 && StepSecond == 0) StepSecond = ++envStep;
                    if (env != null) env.DrawRoomLight(cv, v.Room, ox, oy);
                    for (int i = 0; i < v.LightKeys.Length; i++)
                        if (v.LightOn[i]) cv.Blit(v.LightKeys[i], sx, sy);
                    if (v.OnCount == 0) cv.Blit(v.Room.Dark, sx, sy);
                    if (b.HasRail(f, c)) DrawRail(b, sx, sy);
                }
            }

            // W-016: 켜진 감시 카메라의 구역마다 감시 빛(빨간 줄무늬·스캔라인). 방 레이어(L3·어둠·레일) 위, L4 오브젝트 아래. 구역은 카메라 방과 함께 옮겨진다
            if (cv.Has(ZoneKey))
                for (int i = 0; i < sim.CameraCount; i++)
                {
                    SectorObject cam = sim.CameraAt(i);
                    if (!sim.CameraOn(cam)) continue;
                    int zf = sim.CameraZone(cam, out float zl, out float zr);
                    int n = Mathf.RoundToInt((zr - zl) / b.SectorW);
                    int sy = VY + RoomY(sim, cam.Floor, cam.Col) - cy;
                    if (sy >= VY + VH || sy + b.SectorH <= VY) continue;
                    for (int k = 0; k < n; k++)
                    {
                        int sx = VX + RoomX(sim, cam.Floor, cam.Col) + k * b.SectorW - cx;
                        if (sx >= VX + VW || sx + b.SectorW <= VX) continue;
                        cv.Blit(ZoneKey, sx, sy);
                    }
                }
        }

        const string ZoneKey = "objects128/surveil_zone";
        static readonly Color32 Yl = new Color32(240, 210, 90, 255);   // 팔레트 Y

        /// <summary>조력자 머리 위 ! 표시(W-016): 어두운 상자 + 빨간 테두리 + 노란 ! 글자(기존 5x7 글꼴). cx는 조력자 몸 중심 x, top은 그림 위 끝.</summary>
        void DrawAlertMark(int cx, int top)
        {
            cv.Fill(cx - 4, top - 11, 9, 11, K);
            cv.Box(cx - 4, top - 11, cx + 4, top - 1, Rd);
            cv.Text(cx - 2, top - 9, "!", Yl);
        }

        const string RailKey = "rooms128/common/rail";

        /// <summary>
        /// 밀기 표시(레일) 최종 그림(W-015): 천장·바닥 슬래브 구역만 그린 640x384 그림을 섹터 위에 한 번 그린다.
        /// </summary>
        void DrawRail(Board b, int sx, int sy)
        {
            cv.Blit(RailKey, sx, sy);
        }

        // ---------- 튜토리얼 안내 상자(W-005) ----------

        /// <summary>
        /// 장면 패널 위쪽 안쪽에 안내 상자(ui/panel 9-slice + 글자 + 키 배지)를 띄운다. LOG 탭 대화가 아니다(D-045).
        /// 추적도가 최대치면 TRACE MAX 경고가 단계 문구를 대신한다.
        /// </summary>
        void DrawTutorial(GameView v)
        {
            GameSim sim = v.Sim;
            string text = v.Tut.Text(sim), key = v.Tut.Key(sim), key2 = v.Tut.Key2(sim);
            if (string.IsNullOrEmpty(text)) return;
            int kw = key == null ? 0 : PixelCanvas.TextWidth(key) + 6;
            int kw2 = key2 == null ? 0 : PixelCanvas.TextWidth(key2) + 6;
            int w = 8 + (kw > 0 ? kw + 6 : 0) + (kw2 > 0 ? kw2 + 6 : 0) + PixelCanvas.TextWidth(text) + 8, h = 21;
            int x = VX + (VW - w) / 2, y = VY + 6;
            cv.Nine("ui/panel", x, y, w, h);
            int cx = x + 8;
            if (key != null)
            {
                cv.Fill(cx, y + 5, kw, 11, K);   // 키 배지: Hint()의 그림 없는 키와 같은 규격
                cv.Box(cx, y + 5, cx + kw - 1, y + 15, B3);
                cv.Text(cx + 3, y + 7, key, Wh);
                cx += kw + 6;
            }
            if (key2 != null)
            {
                cv.Fill(cx, y + 5, kw2, 11, K);
                cv.Box(cx, y + 5, cx + kw2 - 1, y + 15, B3);
                cv.Text(cx + 3, y + 7, key2, Wh);
                cx += kw2 + 6;
            }
            cv.Text(cx, y + 7, text, v.Tut.Warning(sim) ? Rd : Wh);
        }

        // W-012: 오브젝트 그림은 objects128/(4배 그림)이 있으면 그것을, 없으면 예전 objects/(32px 확대)를 쓴다.
        // 키 문자열은 처음 한 번만 만들어 캐시한다(프레임마다 문자열을 만들지 않음).
        readonly Dictionary<string, string> hiKeys = new Dictionary<string, string>();

        string Hi(string key)
        {
            if (key == null) return null;
            if (!hiKeys.TryGetValue(key, out string r))
            {
                string hi = "objects128/" + key.Substring("objects/".Length);
                r = cv.Has(hi) ? hi : key;
                hiKeys[key] = r;
            }
            return r;
        }

        /// <summary>자동 확인용: 이 오브젝트의 현실 그림 키(objects128/ 우선).</summary>
        public string ResolveObjectKey(SectorObject o) => Hi(ObjectKey(o));
        /// <summary>자동 확인용: 이 오브젝트의 설계도 겹침 그림 키(없으면 null).</summary>
        public string ResolveBlueprintKey(SectorObject o) => Hi(BlueprintObjectKey(o));
        /// <summary>자동 확인용: 섹터 전체형 차단막 그림 키.</summary>
        public string ResolveBlockerKey() => Hi("objects/blocker_on");
        /// <summary>자동 확인용: 설계도 섹터 겹침 그림 키.</summary>
        public string ResolveBpSectorKey() => Hi("objects/bp_sector");

        static string ObjectKey(SectorObject o)
        {
            switch (o.Def.kind)
            {
                case ObjectKind.Door: return o.Def.realityOn ? "objects/door_open" : "objects/door_closed";
                case ObjectKind.Stairs: return o.Def.realityOn ? "objects/shutter_open" : "objects/shutter_closed";
                case ObjectKind.Hatch: return o.Def.realityOn ? "objects/hatch_open" : "objects/hatch_closed";
                case ObjectKind.Elevator: return o.Def.realityOn ? "objects/elevator_running" : "objects/elevator_stopped";
                case ObjectKind.Terminal: return "objects/terminal";
                case ObjectKind.Relay: return "objects/relay_box";
                case ObjectKind.Exit: return "objects/exit_sign";
                case ObjectKind.Camera: return CamKey(o, o.Def.realityOn);
            }
            return null;
        }

        /// <summary>감시 카메라 그림 키: objects/ + 종류의 artKey + _on·_off(W-016). 처음 한 번만 만들어 캐시한다.</summary>
        static readonly Dictionary<string, string[]> camKeys = new Dictionary<string, string[]>();

        static string CamKey(SectorObject o, bool on)
        {
            string art = o.Def.surveillance != null && !string.IsNullOrEmpty(o.Def.surveillance.artKey) ? o.Def.surveillance.artKey : "camera";
            if (!camKeys.TryGetValue(art, out string[] k))
            {
                k = new[] { "objects/" + art + "_off", "objects/" + art + "_on" };
                camKeys[art] = k;
            }
            return on ? k[1] : k[0];
        }

        /// <summary>천장선(방 그림 y 28px = 논리 7)에서 카메라 그림 위 끝까지의 위치.</summary>
        const int CeilLogic = 7;

        /// <summary>설계도 상태 그림(있는 것만): 문·계단·엘리베이터. 현실과 다른 상태일 때 겹쳐 그린다.</summary>
        static string BlueprintObjectKey(SectorObject o)
        {
            if (o.Def.realityOn == o.Def.blueprintOn) return null;
            switch (o.Def.kind)
            {
                // W-008: 설계도 상태(blueprintOn)에 맞춤. 열림·작동이면 *_open·running, 닫힘·멈춤이면 *_closed·stopped
                case ObjectKind.Door: return o.Def.blueprintOn ? "objects/bp_door_open" : "objects/bp_door_closed";
                case ObjectKind.Stairs: return o.Def.blueprintOn ? "objects/bp_shutter_open" : "objects/bp_shutter_closed";
                case ObjectKind.Hatch: return o.Def.blueprintOn ? "objects/bp_hatch_open" : "objects/bp_hatch_closed";
                case ObjectKind.Elevator: return o.Def.blueprintOn ? "objects/bp_elevator_running" : "objects/bp_elevator_stopped";
            }
            return null;
        }

        void DrawObject(GameSim sim, Board b, SectorObject o, int cx, int cy, int roomDx, int roomDy)
        {
            if (o.Def.kind == ObjectKind.Camera)
            {
                // W-016: 천장에 달린 카메라. 켜짐·꺼짐은 지금 유효 상태(Shift로 조준한 방은 꺼짐). 위 끝이 천장선에 닿는다
                string ck = Hi(CamKey(o, sim.CameraOn(o)));
                cv.Blit(ck, VX + o.DrawAbsX - cx + roomDx, VY + b.SectorTop(o.Floor) + roomDy + CeilLogic - cy);
                return;
            }
            string key = Hi(ObjectKey(o));
            if (key == null) return;
            int fy = b.FloorLineY(o.Floor) + roomDy;
            int x = VX + o.DrawAbsX - cx + roomDx;
            if (o.Def.kind == ObjectKind.Exit)
            {
                // 초록 문(시안 scene_building.py: 섹터 x 14~30, 높이 40) + 표지(문 위 58px)
                cv.Fill(x + 6, VY + fy - 40 - cy, 17, 40, Gd);
                cv.Blit(key, x, VY + fy - 58 - cy);
                return;
            }
            cv.Blit(key, x, VY + fy - cv.HeightOf(key) - cy);
        }

        void DrawBlueprintObject(Board b, SectorObject o, int cx, int cy, int roomDx, int roomDy)
        {
            string key = Hi(BlueprintObjectKey(o));
            if (key == null) return;
            // 설계도 그림은 실루엣보다 사방 1px 크다
            cv.Blit(key, VX + o.DrawAbsX - 1 - cx + roomDx, VY + b.FloorLineY(o.Floor) + roomDy + 1 - cv.HeightOf(key) - cy);
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
            int scale = CountdownScale;   // W-028 1-6: 숫자가 바뀐 앞 3틱은 4배, 그 뒤 3배
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
            // 미션 목록 순서: 0번 = 최종 미션(RequestEntry.missionNames와 같은 순서). 실패 확정 미션은 빨간 X + 흐린 글자(W-007).
            int n = Mathf.Min(sim.MissionCount, e.missionNames.Length);
            for (int i = 0; i < n; i++)
            {
                int y = PY + 24 + i * 16;
                bool done = sim.MissionDone(i), failed = sim.MissionFailed(i);
                cv.Blit(done ? "ui/checkbox_done" : "ui/checkbox_empty", PX + 8, y);
                if (failed) DrawRedX(PX + 8, y);
                string name = e.missionNames[i];
                if (sim.Missions[i].kind == MissionKind.TraceLimit) name += " (" + Mathf.CeilToInt(sim.Tracking) + ")";   // 현재 추적도
                cv.Text(PX + 22, y + 1, name, failed ? B4 : (done ? B3 : Wh));
            }
            if (sim.Board.Tutorial) return;   // W-008: 튜토리얼 의뢰는 별(RATING)이 없다
            cv.Text(PX + 8, PY + 82, "RATING", Cy);
            // 게임오버면 예상 별 대신 빈 별(실패한 의뢰에 별이 보이지 않게)
            int stars = sim.Outcome == Outcome.GameOver ? 0 : sim.ProjectedStars;
            for (int i = 0; i < 3; i++)
                cv.Blit(i < stars ? "ui/star_full" : "ui/star_empty", PX + 52 + i * 12, PY + 81);
        }

        /// <summary>체크 상자(9x9) 위에 빨간 X를 코드로 그린다(팔레트 R).</summary>
        void DrawRedX(int x, int y)
        {
            for (int i = 1; i <= 7; i++)
            {
                cv.Fill(x + i, y + i, 1, 1, Rd);
                cv.Fill(x + 8 - i, y + i, 1, 1, Rd);
            }
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
                string who = me ? "ME" : (string.IsNullOrEmpty(bb.line.speakerName) ? ClientName(v) : bb.line.speakerName.ToUpperInvariant());
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

        /// <summary>의뢰인 자리 화자 이름(의뢰 목록의 client). 비어 있으면 CLIENT.</summary>
        static string ClientName(GameView v)
        {
            string c = v.Desk.Entry(v.ActiveIndex).client;
            return string.IsNullOrEmpty(c) ? "CLIENT" : c.ToUpperInvariant();
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
            if (GaugeBlinkOff && fill > 0)
            {
                // W-028 1-4: 채워진 끝 칸 1개(4px 묶음)가 깜빡인다 — 꺼진 쪽에서는 빈 바탕 색으로 덮는다
                int g0 = (fill - 1) / Fx.GaugeCellPx * Fx.GaugeCellPx;
                cv.Fill(gx + 2 + g0, gy + 2, fill - g0, 5, Pa);
            }

            // 명령 기록 줄: 칸에 들어가는 만큼 최근 것만
            const int maxWidth = 406 - 16;
            IReadOnlyList<string> log = v.CommandLog;
            int start = 0;
            while (start < log.Count - 1 && JoinedWidth(log, start) > maxWidth) start++;
            // W-030: 장치 이용 사건 줄(CARRIED 등)은 흰색, 조작 줄은 지금처럼 민트(G). 한 줄로 이어 쓰는 간격은 그대로(항목 사이 공백 3칸)
            int lx = 228;
            for (int i = start; i < log.Count; i++)
            {
                cv.Text(lx, 325, log[i], v.CommandLogIsUse(i) ? Wh : Gn);
                lx += (log[i].Length + 3) * 6;
            }
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

        static readonly string[] PauseItems = { "CONTINUE", "RESTART", "SETTINGS", "DESKTOP" };

        void DrawPause(GameView v)
        {
            const int w = 160, h = 130;   // 항목 4개
            int x = 222 - w / 2, y = 171 - h / 2;
            cv.Nine("ui/window_frame", x, y, w, h);
            cv.Text(x + 8, y + 7, "PAUSED", Wh);
            for (int i = 0; i < PauseItems.Length; i++)
                Button(x + (w - 110) / 2, y + 30 + i * 18, 110, PauseItems[i], i == v.PauseIndex);
            Hint(x + 10, y + h - 17, "↑↓", "SELECT", "ENTER", "OK");
        }

        /// <summary>결과 창이 올라오는 중의 아래 어긋남(px, 자동 확인용)과 TRAPPED 도장을 그리는 중인지.</summary>
        public int ResultOffsetY { get; private set; }
        public bool TrappedDrawn { get; private set; }

        /// <summary>W-028 1-10: 챕터 0 가두기 성공, 결과 창 직전에 장면 가운데에 TRAPPED 글자 도장(2배, 초록, 1px 상자) 0.3초.</summary>
        void DrawTrapped()
        {
            const string text = "TRAPPED";
            int sc = Fx.TrappedScale, pad = Fx.TrappedPadding;
            int w = PixelCanvas.TextWidth(text, sc) + pad * 2, h = 7 * sc + pad * 2;
            int x = VX + (VW - w) / 2, y = VY + (VH - h) / 2;
            cv.Fill(x, y, w, h, Pa);
            cv.Box(x, y, x + w - 1, y + h - 1, Gn);
            cv.Text(x + pad, y + pad, text, Gn, sc);
        }

        void DrawResult(GameView v)
        {
            GameSim sim = v.Sim;
            TrappedDrawn = v.TrappedShowing;
            if (TrappedDrawn) { ResultOffsetY = Fx.ResultRisePx; DrawTrapped(); return; }
            float ws = v.ResultSeconds;
            int off = Fx.ResultOffset(ws);   // W-028 1-7: 아래 8px에서 0.1초 동안 제자리로 올라온다(성공·게임오버 모두)
            ResultOffsetY = off;
            string hint = v.ResultHint;   // 튜토리얼 의뢰를 ★★로 끝냈을 때만 한 줄 덧붙임(글자 폭 때문에 창을 넓힘)
            int w = hint != null ? 272 : 200;
            const int h = 112;
            int x = 222 - w / 2, y = 171 - h / 2 + off;
            bool clear = sim.Outcome == Outcome.Complete;
            cv.Nine("ui/window_frame", x, y, w, h);
            if (clear)
            {
                cv.Text(x + 8, y + 7, "REQUEST COMPLETE", Wh);
                if (sim.Board.Tutorial)
                {
                    // W-008: 튜토리얼 의뢰는 별 대신 CLEAR 글자
                    const string clearText = "CLEAR";
                    if (Fx.TickOf(ws) >= Fx.ResultRiseTicks) cv.Text(x + (w - PixelCanvas.TextWidth(clearText, 2)) / 2, y + 40, clearText, Wh, 2);   // 1-7: 올라온 뒤 표시
                }
                else
                {
                    int stars = Mathf.Min(sim.Stars, v.StarsShown);   // 1-7: 별은 하나씩 0.15초 간격으로 찍힌다(아직 안 찍힌 별은 빈 별)
                    int sx = x + (w - (3 * 18 + 2 * 4)) / 2;
                    for (int i = 0; i < 3; i++)
                        cv.BlitScaled(i < stars ? "ui/star_full" : "ui/star_empty", sx + i * 22, y + 32, 2);
                    string s = sim.Stars + " / 3 STARS";
                    cv.Text(x + (w - PixelCanvas.TextWidth(s)) / 2, y + 58, s, Wh);
                }
                if (hint != null) cv.Text(x + (w - PixelCanvas.TextWidth(hint)) / 2, y + 70, hint, Cy);
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
