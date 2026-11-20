using System.Collections.Generic;
using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 메인 컨트롤러: 입력 처리, 화면 단계(메인·바탕화면·대화·플레이·일시정지·결과). 모든 화면은 640x360 논리 좌표의 도트 캔버스(내부 2560x1440 GPU)로 그린다.
    /// 규칙은 GameSim이 맡고, 여기서는 입력을 넘기고 결과를 그린다.
    /// </summary>
    public sealed class GameView : MonoBehaviour
    {
        /// <summary>
        /// 화면 상태(D-056~D-059): 메인 / 크레딧 / 부팅(모니터 켜짐·로딩) / 바탕화면(의뢰 선택, 상세 패널 포함) /
        /// 접속 로딩 / 브리핑 대화 / 게임 / 일시정지 / 종료 대화 / 결과 / 설정(W-019: 메인·일시정지 공용, 기존 값 순서를 지키려 맨 끝). Connecting까지는 MenuRenderer, 그 뒤는 GameRenderer가 그린다.
        /// </summary>
        public enum Phase { Main, Credits, Booting, Desktop, Connecting, Briefing, Playing, Paused, Ending, Ended, Settings }

        /// <summary>오른쪽 위 공용 패널의 탭(D-043).</summary>
        public enum Tab { Mission, Log }

        [SerializeField] GameSettings settings;
        [SerializeField] RequestList requests;
        [SerializeField] ArtSet art;
        [SerializeField] GlitchSet glitch;   // W-013: 글리치 셰이더·프리셋(없으면 글리치 없음)
        [SerializeField] EnvironmentSet environment;   // W-011: 바깥 환경 프리셋 10개(없으면 환경 이펙트 없음)

        public Phase CurrentPhase { get; private set; } = Phase.Main;
        public GameSim Sim { get; private set; }
        /// <summary>챕터 0 튜토리얼 안내 상태. 튜토리얼 의뢰가 아니면 null(W-005).</summary>
        public Tutorial Tut { get; private set; }
        public PixelCanvas Canvas { get; private set; }
        public RequestBoard Desk { get; private set; }
        public ProgressStore Progress { get; private set; }
        public int MainIndex { get; private set; }
        /// <summary>메인 메뉴 글자(위에서 아래). 기록이 없으면 NEW GAME 시작, 있으면 CONTINUE 시작(W-019, D-105).</summary>
        public IReadOnlyList<string> MainItems => mainItems;
        /// <summary>NEW GAME 초기화 확인 창이 열려 있는지, 그 선택(0 = NO, 1 = YES).</summary>
        public bool EraseConfirm { get; private set; }
        public int EraseIndex { get; private set; }
        /// <summary>설정 화면의 선택 줄(0 BGM, 1 SFX, 2 ENV FX, 3 GLITCH, 4 CONTROLS, 5 BACK)과 조작법 창 열림.</summary>
        public int SettingsIndex { get; private set; }
        public bool ControlsOpen { get; private set; }
        /// <summary>설정을 일시정지에서 열었는지(false면 메인에서 열었다).</summary>
        public bool SettingsFromPause { get; private set; }
        /// <summary>게임 시간이 멈춘 상태: 일시정지, 또는 일시정지에서 연 설정 화면.</summary>
        public bool GameFrozen => CurrentPhase == Phase.Paused || (CurrentPhase == Phase.Settings && SettingsFromPause);
        public int DeskIndex { get; private set; }
        /// <summary>바탕화면이 챕터 폴더 안(의뢰 목록)인지. false면 챕터 폴더 화면(W-005).</summary>
        public bool InFolder { get; private set; }
        /// <summary>선택한 챕터 폴더 번호(0부터).</summary>
        public int FolderIndex { get; private set; }
        public int ActiveIndex { get; private set; }
        /// <summary>바탕화면 상세 패널이 열려 있는지(닫는 중·여는 중 포함 아님).</summary>
        public bool PanelOpen { get; private set; }
        /// <summary>상세 패널이 열려 있고 등장 연출이 끝났는지.</summary>
        public bool PanelSettled => PanelOpen && panelElapsed >= settings.panelSlideSeconds;
        public int BootStage => Mathf.Min(3, (int)(loadElapsed / settings.bootStageSeconds));
        public Tab CurrentTab { get; private set; }
        /// <summary>LOG 탭에 보이는 대화(브리핑부터 지금까지). 의뢰를 시작할 때 비워진다.</summary>
        public List<DialogueLine> Shown { get; } = new List<DialogueLine>();
        public bool InDialogue => CurrentPhase == Phase.Briefing || CurrentPhase == Phase.Ending;
        public int PauseIndex => pauseIndex;
        public IReadOnlyList<string> CommandLog => commandLog;

        /// <summary>결과 창에 덧붙는 한 줄(튜토리얼 의뢰를 ★★로 끝냈을 때). 없으면 null.</summary>
        public string ResultHint
        {
            get
            {
                if (Sim == null || Sim.Outcome != Outcome.Complete || Sim.Board.Tutorial || Sim.Stars != 2) return null;
                LevelData level = Desk.Entry(ActiveIndex).level;
                return level != null && level.tutorial && !string.IsNullOrEmpty(level.starTwoHint) ? level.starTwoHint : null;
            }
        }

        /// <summary>해커 초상 표정(D-049 [제안] 규칙). 표정은 즉시 바뀐다.</summary>
        public string FaceKey
        {
            get
            {
                if (InDialogue && activeLines != null) return activeLines[Mathf.Clamp(lineIndex, 0, activeLines.Length - 1)].hackerFace;
                if (Sim == null) return "neutral";
                if (CurrentPhase == Phase.Ended)
                    return Sim.Outcome == Outcome.GameOver ? "sadness" : (Sim.Stars == 3 ? "joy" : "neutral");
                if (Sim.CountdownActive) return "fear";
                return Sim.Tracking >= settings.surpriseTracking ? "surprise" : "neutral";
            }
        }

        MenuRenderer menus;
        GameRenderer game;
        /// <summary>의뢰 화면 그리기(자동 확인이 조력자 애니메이션 상태를 읽는 데 쓴다).</summary>
        public GameRenderer Renderer => game;
        DialogueLine[] activeLines;
        int lineIndex;
        bool dirty = true;
        float loadElapsed;     // 현재 단계(부팅·접속)의 경과 시간
        float panelElapsed;    // 패널 등장 경과 시간
        // 일시정지 메뉴(W-019): 0 = Continue, 1 = Restart, 2 = Settings, 3 = Desktop
        int pauseIndex;
        bool prevShift;
        int seenDetect;              // 이미 처리한 발각 횟수(W-016): 늘어나면 글리치·로그·경보음을 낸다
        float gameOverDelay = -1f;   // 게임오버 글리치(0.5초)가 끝날 때까지 남은 시간. 0 미만이면 대기 중이 아님
        RectInt logoRect = new RectInt(20, 18, 160, 40);
        readonly List<string> commandLog = new List<string>();

        const int MaxLog = 9;

        string[] mainItems = { "NEW GAME", "SETTINGS", "CREDITS" };
        bool mainHasProgress;
        bool mainItemsReady;

        // ---------- 수명 ----------

        void Awake()
        {
            Progress = new ProgressStore();
            Desk = new RequestBoard(requests, Progress);
            Glitch.Init(glitch);
            Canvas = new PixelCanvas(art, Glitch.Shader);
            menus = new MenuRenderer(Canvas, Desk);
            game = new GameRenderer(Canvas) { EnvSet = environment };
            Glitch.Strength = Progress.GlitchSetting;   // W-019: 저장된 글리치 세기 적용
            if (Canvas.Has("ui/logo_temp")) logoRect = new RectInt(20, 18, Canvas.WidthOf("ui/logo_temp") / 2, Canvas.HeightOf("ui/logo_temp") / 2);
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

        bool IsMenuPhase => CurrentPhase <= Phase.Connecting || (CurrentPhase == Phase.Settings && !SettingsFromPause);

        void Go(Phase p)
        {
            CurrentPhase = p;
            loadElapsed = 0f;
            if (p == Phase.Connecting) Glitch.PlayFull(Glitch.Transition);   // 의뢰 접속 화면 전환
            if (p == Phase.Desktop && !PanelOpen) panelElapsed = 0f;
            dirty = true;
        }

        void StartRequest(int index)
        {
            ActiveIndex = index;
            LevelData level = Desk.Entry(index).level;
            Sim = new GameSim(settings, new Board(level));
            Tut = level.tutorial || level.showGuide ? new Tutorial(level) : null;
            Shown.Clear();
            RestartGame();
            Glitch.PlayFull(Glitch.Transition);   // 접속 화면 -> 의뢰 화면 전환
            DialogueData d = Desk.Entry(index).dialogue;
            if (d != null && d.briefing != null && d.briefing.Length > 0) BeginDialogue(Phase.Briefing, d.briefing);
        }

        /// <summary>처음부터 다시(D-031). 브리핑 대화는 건너뛰고 LOG 탭의 기록은 유지한다 [제안].</summary>
        void RestartGame(bool afterFail = false)
        {
            gameOverDelay = -1f;
            Glitch.Cancel();
            Sim.Reset();
            seenDetect = Sim.DetectCount;
            Tut?.Reset();
            if (afterFail) Tut?.ShowNotice(Tutorial.FailNoticeSeconds);   // 실패 지점에 닿아 다시 시작: 안내 상자에 2초(W-007)
            CurrentPhase = Phase.Playing;
            CurrentTab = Tab.Mission;
            prevShift = false;
            commandLog.Clear();
        }

        // ---------- 대화(D-045, D-046) ----------

        void BeginDialogue(Phase p, DialogueLine[] lines)
        {
            activeLines = lines;
            lineIndex = 0;
            Shown.Add(lines[0]);
            CurrentTab = Tab.Log;
            CurrentPhase = p;
            Sim.ShiftHeld = false;
        }

        /// <summary>Enter: 다음 대사. 마지막 대사 뒤에는 브리핑이면 플레이, 종료 대화면 결과 창.</summary>
        void AdvanceDialogue()
        {
            lineIndex++;
            if (lineIndex < activeLines.Length)
            {
                Shown.Add(activeLines[lineIndex]);
                return;
            }
            if (CurrentPhase == Phase.Briefing)
            {
                CurrentTab = Tab.Mission;
                CurrentPhase = Phase.Playing;
                prevShift = false;
            }
            else
            {
                CurrentPhase = Phase.Ended;
                Glitch.PlayFull(Glitch.Transition);   // 결과 화면 전환
            }
        }

        // ---------- 입력·진행 ----------

        void Update()
        {
            if (Canvas.CheckLost()) dirty = true;   // RenderTexture가 사라졌으면 다시 만들고 메뉴를 다시 그린다
            Glitch.Update(Mathf.Min(Time.deltaTime, 0.1f));
            InputFrame inp = GameInput.Read();

            switch (CurrentPhase)
            {
                case Phase.Main:
                    UpdateMain(inp);
                    break;

                case Phase.Settings:
                    UpdateSettings(inp);
                    break;

                case Phase.Credits:
                    if (inp.Esc) Go(Phase.Main);
                    break;

                case Phase.Booting:
                {
                    int before = BootStage;
                    loadElapsed += Mathf.Min(Time.deltaTime, 0.1f);
                    if (BootStage != before) dirty = true;
                    if (loadElapsed >= settings.bootStageSeconds * 4) OpenDesktop(false);
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

                case Phase.Briefing:
                case Phase.Ending:
                    if (inp.Enter) AdvanceDialogue();   // 대화 중에는 게임이 멈춰 있고 Esc·탭 전환은 받지 않는다
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
                        if (Sim.Outcome == Outcome.Complete) OpenDesktop(true);
                        else RestartGame();
                    }
                    break;
            }

            UpdateGlitchLoops();
            if (IsMenuPhase)
            {
                if (dirty)
                {
                    dirty = false;
                    RedrawMenu();   // 끝에서 Apply(글리치 포함)
                }
                else Canvas.Apply();   // 메뉴는 다시 그리지 않아도 글리치가 입혀진 출력을 갱신한다
            }
            else if (Sim != null)
            {
                game.Draw(this);   // 의뢰 화면은 매 프레임 다시 그린다
                Canvas.Apply();
            }
        }

        /// <summary>반복 글리치 켜기·끄기: 메인 로고, 추적도가 높을 때의 danger(카운트다운 중 빠른 주기).</summary>
        void UpdateGlitchLoops()
        {
            Glitch.SetLoop(Glitch.Logo, CurrentPhase == Phase.Main || (CurrentPhase == Phase.Settings && !SettingsFromPause), logoRect, false);
            bool danger = CurrentPhase == Phase.Playing && Sim != null && Sim.Outcome == Outcome.Running && gameOverDelay < 0f
                && Sim.Tracking >= Sim.TrackingMax * Glitch.DangerRatio;
            Glitch.SetLoop(Glitch.Danger, danger, SceneViewRect, danger && Sim.CountdownActive);
        }

        /// <summary>장면 뷰 사각형(논리 좌표).</summary>
        static RectInt SceneViewRect => new RectInt(GameRenderer.VX, GameRenderer.VY, GameRenderer.VW, GameRenderer.VH);

        /// <summary>조준한 방이 화면에서 차지하는 사각형(장면 뷰 안으로 자름, 논리 좌표). 밀린 방은 옮겨진 칸 기준(W-014).</summary>
        public RectInt AimSectorRect() => CellRect(Sim.AimCell);

        /// <summary>칸(x = 열, y = 층)이 화면에서 차지하는 사각형(장면 뷰 안으로 자름, 논리 좌표).</summary>
        public RectInt CellRect(Vector2Int cell)
        {
            Board b = Sim.Board;
            int x0 = GameRenderer.VX + b.SectorLeft(cell.x) - Sim.CamX, y0 = GameRenderer.VY + b.SectorTop(cell.y) - Sim.CamY;
            int x1 = x0 + b.SectorW, y1 = y0 + b.SectorH;
            x0 = Mathf.Max(x0, GameRenderer.VX); y0 = Mathf.Max(y0, GameRenderer.VY);
            x1 = Mathf.Min(x1, GameRenderer.VX + GameRenderer.VW); y1 = Mathf.Min(y1, GameRenderer.VY + GameRenderer.VH);
            return new RectInt(x0, y0, Mathf.Max(1, x1 - x0), Mathf.Max(1, y1 - y0));
        }

        /// <summary>자동 확인용: 지정한 의뢰 데이터로 바로 시작한다(챕터 0에는 게임오버가 없어 TestRequest로 게임오버를 확인한다).</summary>
        public void DebugStartLevel(LevelData level)
        {
            Sim = new GameSim(settings, new Board(level));
            Tut = level.tutorial || level.showGuide ? new Tutorial(level) : null;
            Shown.Clear();
            RestartGame();
        }

        /// <summary>
        /// 바탕화면으로 간다(상세 패널은 닫힌 상태). toList가 true면 방금 하던 의뢰의 챕터 폴더 안(의뢰 목록)으로,
        /// false면 챕터 폴더 화면으로 간다(W-005).
        /// </summary>
        void OpenDesktop(bool toList)
        {
            PanelOpen = false;
            panelElapsed = 0f;
            if (toList)
            {
                FolderIndex = Desk.ChapterOf(ActiveIndex);
                DeskIndex = ActiveIndex;
                InFolder = true;
            }
            else
            {
                FolderIndex = 0;
                InFolder = false;
            }
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
            if (PanelOpen && panelElapsed < settings.panelSlideSeconds)
            {
                panelElapsed += Mathf.Min(Time.deltaTime, 0.1f);
                dirty = true;
            }
            if (InFolder) UpdateRequestList(inp);
            else UpdateFolders(inp);
        }

        /// <summary>챕터 폴더 화면: 위아래로 폴더 선택, Enter로 열기(잠긴 폴더는 LOCKED 패널), Esc는 메인(W-005).</summary>
        void UpdateFolders(InputFrame inp)
        {
            int n = RequestBoard.ChapterCount;
            if (inp.Down) { FolderIndex = (FolderIndex + 1) % n; FolderMoved(); }
            if (inp.Up) { FolderIndex = (FolderIndex + n - 1) % n; FolderMoved(); }
            if (inp.Esc)
            {
                if (PanelOpen) { PanelOpen = false; dirty = true; }   // 패널 닫기
                else Go(Phase.Main);                                    // 모니터 꺼짐 -> 메인
                return;
            }
            if (inp.Enter)
            {
                if (Desk.ChapterOpen(FolderIndex))
                {
                    InFolder = true;
                    DeskIndex = Desk.InChapter(FolderIndex)[0];
                    PanelOpen = false;
                    panelElapsed = 0f;
                    dirty = true;
                }
                else if (!PanelOpen) { PanelOpen = true; panelElapsed = 0f; dirty = true; }   // 잠긴 폴더: LOCKED 패널
            }
        }

        void FolderMoved()
        {
            if (PanelOpen && Desk.ChapterOpen(FolderIndex)) PanelOpen = false;   // 열린 폴더에는 패널이 없다
            dirty = true;
        }

        /// <summary>챕터 폴더 안(의뢰 목록): 위아래로 의뢰 선택, Enter로 패널·접속, Esc는 패널 닫기 → 폴더 화면.</summary>
        void UpdateRequestList(InputFrame inp)
        {
            List<int> items = Desk.InChapter(FolderIndex);
            int pos = Mathf.Max(0, items.IndexOf(DeskIndex));
            if (inp.Down) { DeskIndex = items[(pos + 1) % items.Count]; dirty = true; }
            if (inp.Up) { DeskIndex = items[(pos + items.Count - 1) % items.Count]; dirty = true; }
            if (inp.Esc)
            {
                if (PanelOpen) PanelOpen = false;   // 패널 닫기
                else InFolder = false;              // 폴더 화면으로
                dirty = true;
                return;
            }
            if (inp.Enter)
            {
                if (!PanelOpen) { PanelOpen = true; panelElapsed = 0f; dirty = true; }
                else if (Desk.IsPlayable(DeskIndex)) Go(Phase.Connecting);   // 잠긴 의뢰는 무시
            }
        }

        // ---------- 메인 메뉴·설정(W-019, D-105) ----------

        /// <summary>QUIT 항목은 PC 단독 실행(Windows·Mac·Linux Player)에서만 보인다. Web·Editor에서는 숨긴다.</summary>
        public static bool QuitAvailable =>
            Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.LinuxPlayer;

        List<string> AllRequestIds()
        {
            var ids = new List<string>();
            for (int i = 0; i < Desk.Count; i++) ids.Add(Desk.Entry(i).id);
            return ids;
        }

        /// <summary>기록 유무가 바뀌면 메뉴 글자를 다시 만든다(선택 칸은 범위 안으로).</summary>
        void RefreshMainItems()
        {
            bool has = Progress.HasAnyProgress(AllRequestIds());
            if (mainItemsReady && has == mainHasProgress) return;
            mainItemsReady = true;
            mainHasProgress = has;
            var list = new List<string>();
            if (has) list.Add("CONTINUE");
            list.Add("NEW GAME");
            list.Add("SETTINGS");
            list.Add("CREDITS");
            if (QuitAvailable) list.Add("QUIT");
            mainItems = list.ToArray();
            MainIndex = Mathf.Clamp(MainIndex, 0, mainItems.Length - 1);
            dirty = true;
        }

        void UpdateMain(InputFrame inp)
        {
            RefreshMainItems();
            if (EraseConfirm)
            {
                if (inp.Left || inp.Right || inp.Up || inp.Down) { EraseIndex = 1 - EraseIndex; dirty = true; }
                if (inp.Esc) { EraseConfirm = false; dirty = true; }
                else if (inp.Enter)
                {
                    EraseConfirm = false;
                    dirty = true;
                    if (EraseIndex == 1)
                    {
                        Progress.Clear(AllRequestIds());   // 의뢰 기록(별·클리어)만 지운다. 설정 값은 유지
                        mainItemsReady = false;
                        RefreshMainItems();
                        MainIndex = 0;
                        Go(Phase.Booting);
                    }
                }
                return;
            }
            int n = mainItems.Length;
            if (inp.Down) { MainIndex = (MainIndex + 1) % n; dirty = true; }
            if (inp.Up) { MainIndex = (MainIndex + n - 1) % n; dirty = true; }
            if (!inp.Enter) return;
            switch (mainItems[MainIndex])
            {
                case "CONTINUE": Go(Phase.Booting); break;
                case "NEW GAME":
                    if (mainHasProgress) { EraseConfirm = true; EraseIndex = 0; dirty = true; }   // 처음 선택은 NO
                    else Go(Phase.Booting);
                    break;
                case "SETTINGS": OpenSettings(false); break;
                case "CREDITS": Go(Phase.Credits); break;
                case "QUIT": Application.Quit(); break;
            }
        }

        void OpenSettings(bool fromPause)
        {
            SettingsFromPause = fromPause;
            SettingsIndex = 0;
            ControlsOpen = false;
            CurrentPhase = Phase.Settings;
            dirty = true;
        }

        /// <summary>설정에서 나간다: 메인이면 메인(SETTINGS 선택), 일시정지면 일시정지 메뉴(SETTINGS 선택).</summary>
        void CloseSettings()
        {
            ControlsOpen = false;
            CurrentPhase = SettingsFromPause ? Phase.Paused : Phase.Main;
            dirty = true;
        }

        void UpdateSettings(InputFrame inp)
        {
            if (ControlsOpen)
            {
                if (inp.Esc) { ControlsOpen = false; dirty = true; }
                return;
            }
            if (inp.Esc) { CloseSettings(); return; }
            if (inp.Down) { SettingsIndex = (SettingsIndex + 1) % MenuRenderer.SettingsRows; dirty = true; }
            if (inp.Up) { SettingsIndex = (SettingsIndex + MenuRenderer.SettingsRows - 1) % MenuRenderer.SettingsRows; dirty = true; }
            if (inp.Left) ChangeSetting(-1);
            if (inp.Right) ChangeSetting(1);
            if (inp.Enter)
            {
                if (SettingsIndex == 2) ChangeSetting(1);
                else if (SettingsIndex == 4) { ControlsOpen = true; dirty = true; }
                else if (SettingsIndex == 5) CloseSettings();
            }
        }

        /// <summary>선택한 줄의 값을 dir(-1, +1)만큼 바꾸고 바로 저장한다.</summary>
        void ChangeSetting(int dir)
        {
            switch (SettingsIndex)
            {
                case 0: Progress.BgmVolume += dir; break;
                case 1: Progress.SfxVolume += dir; break;
                case 2: Progress.EnvFxReduced = !Progress.EnvFxReduced; break;
                case 3:
                    Progress.GlitchSetting = (GlitchStrength)Mathf.Clamp((int)Progress.GlitchSetting + dir, 0, 2);
                    Glitch.Strength = Progress.GlitchSetting;
                    if (Glitch.Strength == GlitchStrength.Off) Glitch.Cancel();
                    break;
                default: return;
            }
            dirty = true;
        }

        /// <summary>일시정지에서 연 설정 창 그리기(GameRenderer가 부른다). (cx, cy)는 창 중심.</summary>
        public void DrawSettingsInGame(int cx, int cy)
        {
            menus.DrawSettings(this, cx - MenuRenderer.SettingsW / 2, cy - MenuRenderer.SettingsH / 2);
            if (ControlsOpen) menus.DrawControls(cx, cy);
        }

        /// <summary>메뉴 화면을 캔버스에 다시 그린다. 자동 확인·캡처가 직접 부를 수도 있다.</summary>
        public void RedrawMenu()
        {
            switch (CurrentPhase)
            {
                case Phase.Main: menus.DrawMain(MainIndex, mainItems, EraseConfirm, EraseIndex); break;
                case Phase.Settings: menus.DrawSettingsMain(this); break;
                case Phase.Credits: menus.DrawCredits(); break;
                case Phase.Booting: menus.DrawBoot(BootStage); break;
                case Phase.Desktop: menus.DrawDesktop(InFolder, FolderIndex, DeskIndex, PanelOpen ? PanelX() : -1, -1f); break;
                case Phase.Connecting: menus.DrawDesktop(true, FolderIndex, DeskIndex, MenuRenderer.PanelTargetX, loadElapsed / settings.connectSeconds); break;
            }
            Canvas.Apply();
        }

        void UpdatePlaying(InputFrame inp)
        {
            if (gameOverDelay >= 0f)
            {
                // 게임오버 글리치 재생 중: 판정은 이미 끝났고 입력은 받지 않는다. 시간이 다 되면 게임오버 창(D-087, 타이밍만 늦춤)
                Sim.ShiftHeld = false;
                gameOverDelay -= Mathf.Min(Time.deltaTime, 0.1f);
                if (gameOverDelay <= 0f) { gameOverDelay = -1f; CurrentPhase = Phase.Ended; }
                return;
            }
            if (inp.Esc)
            {
                PauseIfPlaying();
                return;
            }
            if (inp.Bracket) CurrentTab = CurrentTab == Tab.Mission ? Tab.Log : Tab.Mission;   // D-050

            int returns = Sim.ReturnSeq;
            Sim.ShiftHeld = inp.LeftShift;   // 밀린 방이 있으면 Shift를 떼는 순간 제자리로 돌아온다(W-014 R4)
            bool returned = Sim.ReturnSeq != returns;
            if (inp.LeftShift && !prevShift) { Log("LShift"); Glitch.Play(Glitch.ShiftIn, AimSectorRect()); }   // 설계도 진입 글리치(조준 섹터)
            else if (!inp.LeftShift && prevShift)
            {
                if (returned) Glitch.Play(Glitch.RoomPush, CellRect(Sim.LastMoveFrom), CellRect(Sim.LastMoveTo), PushDirOf(Sim.LastMoveFrom, Sim.LastMoveTo));   // 방이 제자리로 돌아옴
                else Glitch.Play(Glitch.ShiftOut, AimSectorRect());
            }
            prevShift = inp.LeftShift;

            // Shift를 누른 동안 방향키는 방 밀기(W-014, 레일이 있는 레벨만). 조준 이동은 하지 않는다(R7)
            if (inp.LeftShift)
            {
                if (inp.Left) TryPush(-1, 0, "Left");
                if (inp.Right) TryPush(1, 0, "Right");
                if (inp.Up) TryPush(0, -1, "Up");
                if (inp.Down) TryPush(0, 1, "Down");
            }
            else
            {
                if (inp.Left && Sim.MoveAim(-1, 0)) Log("Left");
                if (inp.Right && Sim.MoveAim(1, 0)) Log("Right");
                if (inp.Up && Sim.MoveAim(0, -1)) Log("Up");
                if (inp.Down && Sim.MoveAim(0, 1)) Log("Down");
            }

            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            Sim.Step(dt);
            if (Sim.DetectCount != seenDetect)
            {
                // 발각(W-016, 02-06 §5): 위험 프리셋 글리치 1회(감시 구역 방) + 경보음 짧게 + 로그 한 줄 [제안]
                seenDetect = Sim.DetectCount;
                Log("CAMERA: SPOTTED");
                Glitch.Play(Glitch.Danger, CellRect(Sim.LastDetectCell));
                PlayAlertSound();
            }
            if (Sim.Outcome == Outcome.Failed)
            {
                RestartGame(true);   // 실패 지점(W-007): 결과 창 없이 바로 처음부터 다시(브리핑 건너뜀, 기존 재시작과 같음)
                return;
            }
            Tut?.Update(Sim);
            Tut?.Tick(dt);

            if (Sim.Outcome != Outcome.Running)
            {
                Sim.ShiftHeld = false;
                if (Sim.Outcome == Outcome.Complete)
                {
                    string reqId = Desk.Entry(ActiveIndex).id;
                    Progress.SetCleared(reqId);                                  // W-008: 별이 없는 튜토리얼 의뢰도 클리어 여부를 기록
                    if (!Sim.Board.Tutorial) Progress.SetBest(reqId, Sim.Stars);
                    DialogueData d = Desk.Entry(ActiveIndex).dialogue;
                    if (d != null && d.ending != null && d.ending.Length > 0) BeginDialogue(Phase.Ending, d.ending);
                    else { CurrentPhase = Phase.Ended; Glitch.PlayFull(Glitch.Transition); }   // 결과 화면 전환
                }
                else if (Glitch.Strength != GlitchStrength.Off && Glitch.GameOver != null)
                {
                    // 게임오버(추적됨): 화면 전체 글리치 0.5초 뒤에 게임오버 창. 판정은 그대로(D-087)
                    Glitch.PlayFull(Glitch.GameOver);
                    gameOverDelay = Glitch.GameOver.TotalSeconds;
                }
                else
                {
                    CurrentPhase = Phase.Ended;   // 게임오버는 종료 대화 없이 바로 게임오버 창
                }
            }
        }

        /// <summary>미는 방향의 글리치 치우침 부호(가로 이동이면 그 방향, 세로 이동이면 +1).</summary>
        static int PushDirOf(Vector2Int from, Vector2Int to) => to.x != from.x ? (to.x > from.x ? 1 : -1) : 1;

        /// <summary>Shift + 방향키 한 번(W-014). 성공하면 로그·글리치, 막히면 방 흔들림과 짧은 소리(R8).</summary>
        void TryPush(int dx, int dy, string name)
        {
            int blocks = Sim.BlockSeq;
            if (Sim.TryPush(dx, dy))
            {
                Log("Push " + name);
                Glitch.Play(Glitch.RoomPush, CellRect(Sim.LastMoveFrom), CellRect(Sim.LastMoveTo), PushDirOf(Sim.LastMoveFrom, Sim.LastMoveTo));
            }
            else if (Sim.BlockSeq != blocks)
            {
                game.StartShake(Sim.Aim.y * Sim.Board.Cols + Sim.Aim.x);
                PlayBlockSound();
            }
        }

        // ---------- 막힘 소리(W-014): 코드 합성 짧은 음 1개 [제안] ----------
        // 이 프로젝트에는 아직 재생 중인 소리가 없다(BGM 파일만 Audio/Export에 있음). 짧은 효과음은 샘플 계산으로 바로 만든다.
        AudioSource sfx;
        AudioClip blockClip;
        /// <summary>막힘 소리를 재생한 횟수(자동 확인용).</summary>
        public int BlockSoundCount { get; private set; }

        void PlayBlockSound()
        {
            BlockSoundCount++;
            if (sfx == null)
            {
                if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
                sfx = gameObject.AddComponent<AudioSource>();
                sfx.playOnAwake = false;
                sfx.spatialBlend = 0f;
                blockClip = MakeBlockClip();
            }
            PlaySfx(blockClip);
        }

        /// <summary>마지막으로 재생을 요청한 효과음의 음량(0이면 소리를 내지 않았다, W-019 자동 확인용).</summary>
        public float LastSfxGain { get; private set; } = -1f;

        /// <summary>효과음 재생: 기본 0.5에 설정의 SFX 음량(0~10)을 곱한다. 0이면 재생을 생략한다(W-019).</summary>
        void PlaySfx(AudioClip clip)
        {
            LastSfxGain = 0.5f * Progress.SfxVolume / ProgressStore.VolumeMax;
            if (LastSfxGain > 0f) sfx.PlayOneShot(clip, LastSfxGain);
        }

        // ---------- 경보음(W-016): 막힘 소리와 같은 방식의 코드 합성 짧은 음 [제안] ----------
        AudioClip alertClip;
        /// <summary>경보음을 재생한 횟수(자동 확인용).</summary>
        public int AlertSoundCount { get; private set; }

        void PlayAlertSound()
        {
            AlertSoundCount++;
            if (sfx == null)
            {
                if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
                sfx = gameObject.AddComponent<AudioSource>();
                sfx.playOnAwake = false;
                sfx.spatialBlend = 0f;
            }
            if (alertClip == null) alertClip = MakeAlertClip();
            PlaySfx(alertClip);
        }

        /// <summary>높은 사각파 두 번(880 Hz, 660 Hz) 짧게 0.16초.</summary>
        static AudioClip MakeAlertClip()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.16f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float freq = t < 0.5f ? 880f : 660f;
                phase += freq / rate;
                float local = t < 0.5f ? t * 2f : (t - 0.5f) * 2f;
                float square = (phase % 1f) < 0.5f ? 1f : -1f;
                data[i] = square * 0.3f * (1f - local * 0.6f);
            }
            var clip = AudioClip.Create("alert", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>낮은 사각파가 220 Hz에서 150 Hz로 내려가는 0.09초 음(빠른 감쇠).</summary>
        static AudioClip MakeBlockClip()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.09f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float freq = Mathf.Lerp(220f, 150f, t);
                phase += freq / rate;
                float square = (phase % 1f) < 0.5f ? 1f : -1f;
                data[i] = square * 0.35f * (1f - t) * (1f - t);
            }
            var clip = AudioClip.Create("block", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void UpdatePaused(InputFrame inp)
        {
            if (inp.Esc)
            {
                CurrentPhase = Phase.Playing;
                return;
            }
            if (inp.Down) pauseIndex = (pauseIndex + 1) % 4;
            if (inp.Up) pauseIndex = (pauseIndex + 3) % 4;
            if (inp.Enter)
            {
                if (pauseIndex == 0) CurrentPhase = Phase.Playing;
                else if (pauseIndex == 1) RestartGame();
                else if (pauseIndex == 2) OpenSettings(true);   // W-019: 환경 효과 줄이기 등은 설정 화면 안에 있다
                else OpenDesktop(true);
            }
        }

        void Log(string cmd)
        {
            commandLog.Add("> " + cmd.ToUpperInvariant());
            if (commandLog.Count > MaxLog) commandLog.RemoveAt(0);
        }

        // ---------- 화면 ----------

        void OnGUI()
        {
            DrawCanvas();
        }

        /// <summary>
        /// 2560x1440 캔버스를 화면에 맞춰 그린다(D-076): 16:9 비율을 지키고 남는 부분은 검은 레터박스, 필터는 Bilinear.
        /// 화면 크기가 정수 배율이 아니어도 그대로 부드럽게 늘리거나 줄인다.
        /// </summary>
        void DrawCanvas()
        {
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float k = Mathf.Min(Screen.width / (float)PixelCanvas.RW, Screen.height / (float)PixelCanvas.RH);
            int w = Mathf.Max(1, Mathf.RoundToInt(PixelCanvas.RW * k)), h = Mathf.Max(1, Mathf.RoundToInt(PixelCanvas.RH * k));
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.DrawTexture(r, Canvas.Output, ScaleMode.StretchToFill, false);
        }
    }
}
