using System.Collections.Generic;
using GameOff.Audio;
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
        /// 접속 로딩 / 브리핑 대화 / 게임 / 일시정지 / 종료 대화 / 결과 / 설정(W-019: 메인·일시정지 공용, 기존 값 순서를 지키려 맨 끝) / 챕터 끝 예고(W-047, 2-3 첫 클리어 뒤 한 번, 그 뒤 맨 끝). Connecting까지는 MenuRenderer, 그 뒤는 GameRenderer가 그린다.
        /// </summary>
        public enum Phase { Main, Credits, Booting, Desktop, Connecting, Briefing, Playing, Paused, Ending, Ended, Settings, Preview }

        /// <summary>오른쪽 위 공용 패널의 탭(D-043).</summary>
        public enum Tab { Mission, Log }

        [SerializeField] GameSettings settings;
        [SerializeField] RequestList requests;
        [SerializeField] ArtSet art;
        [SerializeField] GlitchSet glitch;   // W-013: 글리치 셰이더·프리셋(없으면 글리치 없음)
        [SerializeField] EnvironmentSet environment;   // W-011: 바깥 환경 프리셋 10개(없으면 환경 이펙트 없음)
        [SerializeField] SfxSet sfxSet;                // W-020: 효과음 목록(없으면 G08·A05의 코드 합성 소리만 난다)
        [SerializeField] BgmSet bgmSet;                // W-020: BGM 곡 슬롯 8개(없으면 무음)

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
        /// <summary>챕터 끝 예고 화면(W-047)이 시작된 뒤 지난 시간, 모든 줄이 나타났는지. 자동 확인·그리기용.</summary>
        float previewElapsed;
        public float PreviewSeconds => previewElapsed;
        public bool PreviewRevealed => previewElapsed >= MenuRenderer.PreviewStages * MenuRenderer.PreviewStageSeconds;
        /// <summary>화면 그리기 담당(자동 확인이 그린 횟수를 읽는다, W-047).</summary>
        public MenuRenderer Menus => menus;
        /// <summary>바탕화면 아이콘 선택(W-032): 0 NORMAL MODE, 1 개발 모드 자리(잠김).</summary>
        public int DeskIcon { get; private set; }
        /// <summary>일반 모드 팝업(챕터 목록 | 스테이지 목록)이 열려 있는지(W-032, D-121).</summary>
        public bool PopupOpen { get; private set; }
        bool stageFocus;
        /// <summary>팝업에서 포커스가 스테이지 목록에 있는지(false면 챕터 목록).</summary>
        public bool InFolder => PopupOpen && stageFocus;
        /// <summary>팝업이 열리는 연출(Fx.PopupOpenTicks)이 끝났는지.</summary>
        public bool PopupSettled => PopupOpen && popupElapsed >= Fx.PopupOpenTicks * Fx.TickSeconds;
        /// <summary>개발 모드 자리를 골라 LOCKED 글자가 보이는 중인지(Fx.DevLockedSeconds).</summary>
        public bool DevLockedShown => devLockedLeft > 0f;
        float popupElapsed, devLockedLeft;
        int lastPlayed = -1;   // 마지막으로 플레이한 의뢰(저장하지 않는 세션 값). 팝업 처음 포커스에 쓴다
        /// <summary>선택한 챕터 번호(0부터).</summary>
        public int FolderIndex { get; private set; }
        public int ActiveIndex { get; private set; }
        /// <summary>열 밀기 연출(Fx.ColumnSlideTicks)이 끝났는지(W-032 변경: 스테이지 포커스는 [스테이지 | 상세] 두 열).</summary>
        public bool SlideSettled => slideElapsed >= Fx.ColumnSlideTicks * Fx.TickSeconds;
        /// <summary>부팅 글자 단계(0~3). 앞의 CRT 켜짐(Fx.CrtTotalSeconds, W-028)이 끝난 뒤부터 센다.</summary>
        public int BootStage => Mathf.Clamp(Mathf.FloorToInt((loadElapsed - Fx.CrtTotalSeconds) / settings.bootStageSeconds), 0, 3);
        /// <summary>부팅 화면이 시작된 뒤 지난 시간(초, CRT 켜짐이 끝난 뒤부터, W-045). 부팅 단계가 아니면 0.</summary>
        public float BootSeconds => CurrentPhase == Phase.Booting ? Mathf.Max(0f, loadElapsed - Fx.CrtTotalSeconds) : 0f;
        /// <summary>부팅 화면 합계 시간(초).</summary>
        public float BootTotalSeconds => settings.bootStageSeconds * 4;
        /// <summary>게임 시작 CRT 켜짐이 진행 중인지(부팅 단계 앞부분).</summary>
        public bool CrtActive => CurrentPhase == Phase.Booting && loadElapsed < Fx.CrtTotalSeconds;
        /// <summary>CRT 켜짐 단계: 0 없음, 1 검게 페이드 아웃, 2 가로선 펼침, 3 위아래로 열림. CrtStageSeconds는 그 단계가 시작된 뒤 지난 시간.</summary>
        public int CrtStage => CrtStageOf(out _);
        public float CrtStageSeconds { get { CrtStageOf(out float t); return t; } }

        int CrtStageOf(out float within)
        {
            within = 0f;
            if (CurrentPhase != Phase.Booting) return 0;
            float t = loadElapsed;
            if (t < Fx.CrtFadeSeconds) { within = t; return 1; }
            t -= Fx.CrtFadeSeconds;
            if (t < Fx.CrtLineSeconds) { within = t; return 2; }
            t -= Fx.CrtLineSeconds;
            if (t < Fx.CrtOpenSeconds) { within = t; return 3; }
            return 0;
        }

        // ---------- P1 연출 상태(W-028) ----------
        float failElapsed = -1f;      // 실패 지점에 닿은 뒤 지난 시간(깜빡임 중). 0 미만이면 아님
        float endedElapsed;           // 결과 단계에 들어온 뒤 지난 시간
        /// <summary>1-9 실패 지점 깜빡임 경과 시간(-1이면 깜빡이는 중이 아님)과 깜빡일 섹터(x = 열, y = 층).</summary>
        public float FailElapsed => failElapsed;
        public Vector2Int FailCell { get; private set; }
        /// <summary>결과 단계 경과 시간, 챕터 0 가두기 성공 TRAPPED 도장이 보이는 중인지, 결과 창이 시작된 뒤 지난 시간, 결과 창에서 지금까지 찍힌 별 수(1-7, 1-10).</summary>
        public float EndedSeconds => endedElapsed;
        public bool TrappedStamp => Sim != null && Sim.Outcome == Outcome.Complete && Sim.Board.Tutorial;
        public bool TrappedShowing => CurrentPhase == Phase.Ended && TrappedStamp && endedElapsed < Fx.TrappedSeconds;
        public float ResultSeconds => endedElapsed - (TrappedStamp ? Fx.TrappedSeconds : 0f);
        public int StarsShown => CurrentPhase == Phase.Ended ? starsTotal - starsLeft : 0;
        /// <summary>W-052: 층 보기 영역 CRT 꺼짐의 틱(0 이상, Fx.ViewOffTicks 이상이면 완전히 꺼진 검은 화면). 게임오버가 아니거나 꺼짐 전이면 -1.</summary>
        public int ViewOffTick
        {
            get
            {
                if (Sim == null || Sim.Outcome != Outcome.GameOver) return -1;
                if (goElapsed >= 0f) { float t = goElapsed - goGlitchSeconds; return t < 0f ? -1 : Fx.TickOf(t); }
                return CurrentPhase == Phase.Ended || scrStage != 0 ? Fx.ViewOffTicks : -1;
            }
        }
        /// <summary>W-052: 게임오버 1~2단계(치지직·꺼짐)가 진행 중인지. 1단계만 보려면 GameOverGlitchPhase.</summary>
        public bool GameOverSequence => goElapsed >= 0f;
        public bool GameOverGlitchPhase => goElapsed >= 0f && goElapsed < goGlitchSeconds;
        /// <summary>W-052 4단계: 화면 전체 연출 단계(0 없음, 1 전체 꺼짐, 2 검정, 3 켜짐)와 그 단계가 시작된 뒤 지난 시간.</summary>
        public int ScreenFxStage => scrStage;
        /// <summary>W-052: 게임오버 창의 선택(0 RESTART, 1 DESKTOP).</summary>
        public int GameOverChoice => goChoice;
        public float ScreenFxSeconds => scrElapsed;
        public Tab CurrentTab { get; private set; }
        /// <summary>LOG 탭에 보이는 대화(브리핑부터 지금까지). 의뢰를 시작할 때 비워진다.</summary>
        public List<DialogueLine> Shown { get; } = new List<DialogueLine>();
        public bool InDialogue => CurrentPhase == Phase.Briefing || CurrentPhase == Phase.Ending;
        public int PauseIndex => pauseIndex;
        /// <summary>일시정지 메뉴 항목 수(W-049: CONTINUE, RESTART, SETTINGS, GUIDE, DESKTOP).</summary>
        public const int PauseItemCount = 5;

        // ---------- 도움말(GUIDE) 패널(W-049) ----------
        /// <summary>쪽 번호: 0 BASICS(항상), 1 ROOMS(챕터 1 열림), 2 BEATS(챕터 2 열림), 3 SIGNS(항상). 아직 열리지 않은 챕터의 쪽은 목록에서 빠진다(스포일러 방지).</summary>
        public const int GuideBasics = 0, GuideRooms = 1, GuideBeats = 2, GuideSigns = 3;
        /// <summary>도움말 패널이 열려 있는지. 일시정지 메뉴(게임은 멈춘 그대로) 또는 바탕화면(팝업이 닫힌 상태)에서 연다.</summary>
        public bool GuideOpen { get; private set; }
        /// <summary>지금 보이는 쪽의 목록 안 위치(GuidePageIds 기준).</summary>
        public int GuidePage { get; private set; }

        /// <summary>지금 볼 수 있는 쪽 번호 목록(순서대로).</summary>
        public List<int> GuidePageIds
        {
            get
            {
                var ids = new List<int> { GuideBasics };
                if (Desk != null && Desk.ChapterOpen(1)) ids.Add(GuideRooms);
                if (Desk != null && Desk.ChapterOpen(2)) ids.Add(GuideBeats);
                ids.Add(GuideSigns);
                return ids;
            }
        }

        void OpenGuide()
        {
            GuideOpen = true;
            GuidePage = 0;
            dirty = true;
        }

        void CloseGuide()
        {
            GuideOpen = false;
            Sfx.Play("U03");
            dirty = true;
        }

        /// <summary>도움말이 열려 있는 동안의 입력: ←→ 쪽 이동(양끝에서 멈춤), Esc 닫기. 나머지 입력은 받지 않는다.</summary>
        void UpdateGuide(InputFrame inp)
        {
            if (inp.Esc) { CloseGuide(); return; }
            int n = GuidePageIds.Count;
            int before = GuidePage;
            if (inp.Left) GuidePage = Mathf.Max(0, GuidePage - 1);
            if (inp.Right) GuidePage = Mathf.Min(n - 1, GuidePage + 1);
            if (GuidePage != before) { Sfx.Play("U01"); dirty = true; }
        }
        public IReadOnlyList<string> CommandLog => commandLog;
        /// <summary>명령 기록 i번째 줄이 장치 이용 사건(W-030 F3)인지. 화면이 다른 글자색으로 그린다.</summary>
        public bool CommandLogIsUse(int i) => commandLogUse[i];
        /// <summary>명령 기록 i번째 줄의 화면 글자(W-034). 사건 줄은 지금 언어로 바꾸고, 입력 기록(LSHIFT·PUSH RIGHT 등)은 영문 그대로다.</summary>
        public string CommandLogText(int i) => "> " + Loc.Tr(commandLog[i].Substring(2));

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

        /// <summary>
        /// W-041: 지금 대화 줄의 화자가 러너(DASH, 챕터 0의 ???)인가. 그러면 오른쪽 아래 초상이 러너로 바뀐다(표정은 그 줄의 hackerFace = 「화자 표정」).
        /// SYSTEM·X·CLIENT·ME 줄과 대화 밖에서는 false(해커 초상).
        /// </summary>
        public bool FaceIsRunner
        {
            get
            {
                if (!InDialogue || activeLines == null || activeLines.Length == 0) return false;
                DialogueLine l = activeLines[Mathf.Clamp(lineIndex, 0, activeLines.Length - 1)];
                return l.speaker == Speaker.Client && (l.speakerName == "DASH" || l.speakerName == "???");
            }
        }

        /// <summary>
        /// W-050: 초상이 "말하는 순간"을 알리는 값(0이 아님). 초상 인물(러너 줄이면 러너, 아니면 해커)·표정이 바뀌거나,
        /// 그 인물의 새 대사 줄(러너 = DASH·???, 해커 = ME 줄)이 나오면 값이 바뀐다. 다른 화자(SYSTEM·X·CLIENT) 줄에서는 초상이 그대로라 값도 그대로다.
        /// </summary>
        public int FaceCue
        {
            get
            {
                bool speaking = false;
                if (InDialogue && activeLines != null && activeLines.Length > 0)
                    speaking = FaceIsRunner || activeLines[Mathf.Clamp(lineIndex, 0, activeLines.Length - 1)].speaker == Speaker.Me;
                unchecked
                {
                    int h = 17;
                    h = h * 31 + (FaceIsRunner ? 1 : 0);
                    h = h * 31 + (FaceKey ?? "").GetHashCode();
                    h = h * 31 + (speaking ? dialogueSerial * 1000 + lineIndex + 1 : 0);
                    return h == 0 ? 1 : h;
                }
            }
        }

        MenuRenderer menus;
        GameRenderer game;
        /// <summary>의뢰 화면 그리기(자동 확인이 조력자 애니메이션 상태를 읽는 데 쓴다).</summary>
        public GameRenderer Renderer => game;
        DialogueLine[] activeLines;
        int lineIndex;
        bool dirty = true;
        int dialogueSerial;          // W-050: 대화를 시작할 때마다 1씩 늘어(FaceCue가 같은 번호의 줄을 구분)
        float loadElapsed;     // 현재 단계(부팅·접속)의 경과 시간
        float slideElapsed = 99f;   // 열 밀기 경과 시간(99 = 끝남)
        // 일시정지 메뉴(W-019): 0 = Continue, 1 = Restart, 2 = Settings, 3 = Desktop
        int pauseIndex;
        bool prevShift;
        int seenDetect;              // 이미 처리한 발각 횟수(W-016): 늘어나면 글리치·로그·경보음을 낸다
        // W-052(D-138) 게임오버 연출: 1 층 보기 영역만 치지직 -> 2 그 영역 CRT 꺼짐 -> 3 게임오버 창(Ended) -> 4 RESTART 뒤 화면 전체 꺼짐·검정·켜짐
        float goElapsed = -1f;       // 게임오버 판정 뒤 지난 시간(1·2단계). 0 미만이면 아님
        float goGlitchSeconds;       // 1단계 길이(글리치 켜짐: 프리셋 길이, 끔: 0 — 그래도 2단계 꺼짐은 그대로)
        int scrStage;                // 4단계: 0 없음, 1 전체 꺼짐, 2 짧은 검정, 3 켜짐(재시작된 의뢰가 비친다)
        float scrElapsed;            // 4단계 안에서 지난 시간
        int goChoice;                // 게임오버 창에서 고른 버튼(0 RESTART, 1 DESKTOP). 창이 뜰 때 0
        RectInt logoRect = new RectInt(20, 18, 160, 40);
        readonly List<string> commandLog = new List<string>();
        readonly List<bool> commandLogUse = new List<bool>();
        int seenUse;                 // 이미 기록에 남긴 장치 이용 사건 수(W-030 F3)

        const int MaxLog = 9;

        string[] mainItems = { "NEW GAME", "SETTINGS", "CREDITS" };
        bool mainHasProgress;
        bool mainItemsReady;

        // ---------- 수명 ----------

        void Awake()
        {
            Progress = new ProgressStore();
            Sfx = new SfxPlayer(gameObject, sfxSet, Progress);   // W-020
            Bgm = new BgmPlayer(gameObject, bgmSet, Progress);
            Desk = new RequestBoard(requests, Progress);
            Glitch.Init(glitch);
            Canvas = new PixelCanvas(art, Glitch.Shader);
            menus = new MenuRenderer(Canvas, Desk);
            game = new GameRenderer(Canvas) { EnvSet = environment };
            Glitch.Strength = Progress.GlitchSetting;   // W-019: 저장된 글리치 세기 적용
            Loc.Current = Progress.Language;            // W-034(D-124): 저장된 언어(처음 실행은 영어)
            if (Canvas.Has("ui/logo")) logoRect = new RectInt(20, 18, Canvas.WidthOf("ui/logo") / 2, Canvas.HeightOf("ui/logo") / 2);
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
            if (CurrentPhase != Phase.Playing || scrStage != 0) return;
            CurrentPhase = Phase.Paused;
            pauseIndex = 0;
            Sim.ShiftHeld = false;
        }

        bool IsMenuPhase => CurrentPhase <= Phase.Connecting || (CurrentPhase == Phase.Settings && !SettingsFromPause) || CurrentPhase == Phase.Preview;

        void Go(Phase p)
        {
            CurrentPhase = p;
            loadElapsed = 0f;
            GuideOpen = false;
            if (p == Phase.Booting) Sfx.Play("U06");   // 모니터 켜짐·부팅(W-020)
            if (p == Phase.Connecting) { Glitch.PlayFull(Glitch.Transition); Sfx.Play("U07"); }   // 의뢰 접속 화면 전환
            dirty = true;
        }

        void StartRequest(int index)
        {
            ActiveIndex = index;
            lastPlayed = index;
            playChapter = Desk.ChapterOf(index);
            LevelData level = Desk.Entry(index).level;
            Sim = new GameSim(settings, new Board(level));
            Tut = level.tutorial || level.showGuide ? new Tutorial(level) : null;
            Shown.Clear();
            RestartGame();
            Glitch.PlayFull(Glitch.Transition);   // 접속 화면 -> 의뢰 화면 전환
            Sfx.Play("T08");
            DialogueData d = Desk.Entry(index).dialogue;
            if (d != null && d.briefing != null && d.briefing.Length > 0) BeginDialogue(Phase.Briefing, d.briefing);
        }

        /// <summary>처음부터 다시(D-031). 브리핑 대화는 건너뛰고 LOG 탭의 기록은 유지한다 [제안].</summary>
        void RestartGame(bool afterFail = false)
        {
            goElapsed = -1f;
            scrStage = 0;
            failElapsed = -1f;
            Glitch.Cancel();
            Sim.Reset();
            seenDetect = Sim.DetectCount;
            seenUse = Sim.UseSeq;
            sfxSynced = false;
            starsLeft = 0;
            if (afterFail) { Tut?.RestartAfterFail(); Tut?.ShowNotice(Tutorial.FailNoticeSeconds); }   // 실패 지점에 닿아 다시 시작: 지난 단계는 다시 띄우지 않고 안내 상자에 2초(W-007, W-033)
            else Tut?.Reset();                                                                          // 의뢰를 처음부터 다시: 단계도 처음부터
            CurrentPhase = Phase.Playing;
            CurrentTab = Tab.Mission;
            prevShift = false;
            commandLog.Clear();
            commandLogUse.Clear();
        }

        // ---------- 대화(D-045, D-046) ----------

        void BeginDialogue(Phase p, DialogueLine[] lines)
        {
            activeLines = lines;
            dialogueSerial++;
            lineIndex = 0;
            Shown.Add(lines[0]);
            Sfx.Play("D01");   // 말풍선 등장
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
                Sfx.Play("D01");   // 새 말풍선 등장
                return;
            }
            Sfx.Play("U01");   // D03: 마지막 대사를 넘겨 대화가 닫힘(U01 재사용)
            if (CurrentPhase == Phase.Briefing)
            {
                CurrentTab = Tab.Mission;
                CurrentPhase = Phase.Playing;
                prevShift = false;
            }
            else
            {
                EnterEnded();
                Glitch.PlayFull(Glitch.Transition);   // 결과 화면 전환
                Sfx.Play("T08");
            }
        }

        // ---------- 입력·진행 ----------

        void Update()
        {
            if (Canvas.CheckLost()) dirty = true;   // RenderTexture가 사라졌으면 다시 만들고 메뉴를 다시 그린다
            Glitch.Update(Mathf.Min(Time.deltaTime, 0.1f));
            InputFrame inp = GameInput.Read();
            if (scrStage == 3 && CurrentPhase == Phase.Desktop) { UpdateScreenFx(); dirty = true; }   // W-052: DESKTOP을 고른 켜짐 동안 바탕화면을 매 프레임 다시 그린다

            switch (CurrentPhase)
            {
                case Phase.Main:
                    UpdateMain(inp);
                    break;

                case Phase.Settings:
                    UpdateSettings(inp);
                    break;

                case Phase.Credits:
                    if (inp.Esc) { Sfx.Play("U03"); Go(Phase.Main); }
                    break;

                case Phase.Booting:
                {
                    loadElapsed += Mathf.Min(Time.deltaTime, 0.1f);
                    dirty = true;   // CRT 켜짐·부팅 화면(점검 글·진행 막대·커서)은 매 프레임 다시 그린다(W-045)
                    if (loadElapsed >= Fx.CrtTotalSeconds + settings.bootStageSeconds * 4) OpenDesktop(false);
                    break;
                }

                case Phase.Connecting:
                    if (inp.Esc) { Sfx.Play("U03"); Go(Phase.Desktop); break; }   // W-032: 접속 취소 -> 팝업(스테이지 포커스)
                    loadElapsed += Mathf.Min(Time.deltaTime, 0.1f);
                    dirty = true;
                    if (loadElapsed >= settings.connectSeconds) StartRequest(DeskIndex);
                    break;

                case Phase.Desktop:
                    if (scrStage != 0) break;   // W-052: 게임오버에서 DESKTOP을 고른 켜짐 동안 입력은 무시
                    UpdateDesktop(inp);
                    break;

                case Phase.Briefing:
                case Phase.Ending:
                    if (inp.Enter) AdvanceDialogue();   // (소리는 AdvanceDialogue 안) 대화 중에는 게임이 멈춰 있고 Esc·탭 전환은 받지 않는다
                    break;

                case Phase.Playing:
                    UpdatePlaying(inp);
                    break;

                case Phase.Paused:
                    UpdatePaused(inp);
                    break;

                case Phase.Ended:
                    if (scrStage != 0) { UpdateScreenFx(); break; }   // W-052 4단계: 화면 전체 꺼짐·검정 동안 입력은 무시
                    if (Sim.Outcome == Outcome.GameOver && (inp.Left || inp.Right || inp.Up || inp.Down)) { goChoice = 1 - goChoice; Sfx.Play("U01"); }   // W-052: RESTART / DESKTOP 선택
                    if (inp.Enter)
                    {
                        Sfx.Play("U02");
                        // 클리어 -> 의뢰 선택(바탕화면), 게임오버 -> 처음부터 다시(D-031)
                        // W-047: 2-3(챕터 2 끝)을 클리어했고 챕터 3 예고를 본 적이 없으면 바탕화면 전에 예고 화면을 한 번 보여 준다
                        if (Sim.Outcome == Outcome.Complete)
                        {
                            if (Desk.PreviewChapterAfter(ActiveIndex) >= 0 && !Progress.ChapterPreviewSeen(Desk.PreviewChapterAfter(ActiveIndex))) { previewElapsed = 0f; Go(Phase.Preview); }
                            else OpenDesktop(true);
                        }
                        else BeginScreenFx();   // W-052: 게임오버 -> 화면 전체 CRT 꺼짐 -> 검정 -> 켜짐 뒤 처음부터 다시
                    }
                    break;

                case Phase.Preview:
                    previewElapsed += Mathf.Min(Time.deltaTime, 0.1f);
                    dirty = true;   // 점 깜빡임·줄 나타남 때문에 매 프레임 다시 그린다
                    if (inp.Enter)
                    {
                        if (!PreviewRevealed) { previewElapsed = 99f; Sfx.Play("U01"); }   // 첫 Enter: 모든 글을 바로 표시
                        else
                        {
                            Sfx.Play("U02");
                            Progress.SetChapterPreviewSeen(Desk.PreviewChapterAfter(ActiveIndex));
                            OpenDesktop(true);
                        }
                    }
                    break;
            }

            UpdateGlitchLoops();
            UpdateAudio();
            // W-050: 메인 화면(설정을 메인에서 연 경우 포함)이 보이는 동안만 움직임을 진행하고, 보이는 모양이 바뀔 때만 다시 그린다
            if (CurrentPhase == Phase.Main || (CurrentPhase == Phase.Settings && !SettingsFromPause))
                if (menus.AdvanceMainAnim(Mathf.Min(Time.deltaTime, 0.1f))) dirty = true;
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
            bool danger = CurrentPhase == Phase.Playing && Sim != null && Sim.Outcome == Outcome.Running && goElapsed < 0f
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
            int chapter = ChapterOfLevel(level);
            if (chapter >= 0) playChapter = chapter;   // W-020: BGM 곡을 고르는 챕터
            RestartGame();
        }

        /// <summary>
        /// 바탕화면으로 간다(상세 패널은 닫힌 상태). toList가 true면 방금 하던 의뢰의 팝업(챕터·스테이지 포커스)으로,
        /// false면 아이콘 화면으로 간다(W-032). 아이콘 화면에서 처음 팝업을 열 때의 포커스는 마지막으로 플레이한 의뢰, 기록이 없으면 첫 챕터·첫 스테이지.
        /// </summary>
        void OpenDesktop(bool toList)
        {
            devLockedLeft = 0f;
            slideElapsed = 99f;
            DeskIcon = 0;
            if (toList)
            {
                FolderIndex = Desk.ChapterOf(ActiveIndex);
                DeskIndex = ActiveIndex;
                PopupOpen = true;
                stageFocus = true;
                popupElapsed = Fx.PopupOpenTicks * Fx.TickSeconds;   // 돌아올 때는 이미 열려 있다
            }
            else
            {
                int focus = lastPlayed >= 0 && lastPlayed < Desk.Count && Progress.HasAnyProgress(AllRequestIds()) ? lastPlayed : 0;
                FolderIndex = Desk.ChapterOf(focus);
                DeskIndex = focus;
                PopupOpen = false;
                stageFocus = false;
            }
            Go(Phase.Desktop);
        }

        void UpdateDesktop(InputFrame inp)
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (slideElapsed < Fx.ColumnSlideTicks * Fx.TickSeconds) { slideElapsed += dt; dirty = true; }
            if (PopupOpen && popupElapsed < Fx.PopupOpenTicks * Fx.TickSeconds) { popupElapsed += dt; dirty = true; }
            if (devLockedLeft > 0f) { devLockedLeft = Mathf.Max(0f, devLockedLeft - dt); dirty = true; }
            if (GuideOpen) { UpdateGuide(inp); return; }   // W-049: 도움말이 열려 있는 동안 바탕화면 입력은 멈춘다
            if (!PopupOpen && inp.Guide) { Sfx.Play("U02"); OpenGuide(); return; }   // 팝업이 닫힌 아이콘 화면에서 G
            if (!PopupOpen) UpdateIcons(inp);
            else if (!stageFocus) UpdateChapters(inp);
            else UpdateStages(inp);
        }

        /// <summary>아이콘 화면(W-032): 위아래로 아이콘 선택, Enter로 일반 모드 팝업 열기(개발 모드 자리는 LOCKED만), Esc는 메인.</summary>
        void UpdateIcons(InputFrame inp)
        {
            if (inp.Down || inp.Up) { DeskIcon = 1 - DeskIcon; Sfx.Play("U01"); dirty = true; }
            if (inp.Esc) { Sfx.Play("U03"); Go(Phase.Main); return; }   // 모니터 꺼짐 -> 메인
            if (!inp.Enter) return;
            if (DeskIcon == 0)
            {
                Sfx.Play("U02");
                PopupOpen = true;
                stageFocus = false;
                popupElapsed = 0f;
                slideElapsed = 99f;
                dirty = true;
            }
            else
            {
                Sfx.Play("U04");   // 잠김
                devLockedLeft = Fx.DevLockedSeconds;
                dirty = true;
            }
        }

        /// <summary>팝업의 챕터 목록: 위아래로 챕터 선택(오른쪽 목록이 바로 바뀜), →·Enter로 스테이지 목록(잠긴 챕터는 막힘), Esc는 팝업 닫기.</summary>
        void UpdateChapters(InputFrame inp)
        {
            int n = RequestBoard.ChapterCount;
            if (inp.Down || inp.Up)
            {
                FolderIndex = (FolderIndex + (inp.Down ? 1 : n - 1)) % n;
                List<int> items = Desk.InChapter(FolderIndex);
                if (items.Count > 0) DeskIndex = items[0];
                Sfx.Play("U01");
                dirty = true;
            }
            if (inp.Esc)
            {
                Sfx.Play("U03");
                PopupOpen = false;
                dirty = true;
                return;
            }
            if (inp.Enter || inp.Right)
            {
                if (Desk.ChapterOpen(FolderIndex) && Desk.InChapter(FolderIndex).Count > 0)
                {
                    Sfx.Play("U02");
                    stageFocus = true;
                    slideElapsed = 0f;
                    if (!Desk.InChapter(FolderIndex).Contains(DeskIndex)) DeskIndex = Desk.InChapter(FolderIndex)[0];
                    dirty = true;
                }
                else Sfx.Play("U04");   // 잠긴 챕터는 오른쪽 목록이 LOCKED
            }
        }

        /// <summary>팝업의 스테이지 목록([스테이지 | 상세] 두 열): 위아래로 선택(상세가 바로 바뀜), Enter로 접속, ←·Esc는 챕터 목록으로.</summary>
        void UpdateStages(InputFrame inp)
        {
            List<int> items = Desk.InChapter(FolderIndex);
            int pos = Mathf.Max(0, items.IndexOf(DeskIndex));
            if (inp.Down) { DeskIndex = items[(pos + 1) % items.Count]; dirty = true; Sfx.Play("U01"); }
            if (inp.Up) { DeskIndex = items[(pos + items.Count - 1) % items.Count]; dirty = true; Sfx.Play("U01"); }
            if (inp.Esc || inp.Left)
            {
                Sfx.Play("U03");
                stageFocus = false;
                slideElapsed = 0f;
                dirty = true;
                return;
            }
            if (inp.Enter)
            {
                if (Desk.IsPlayable(DeskIndex)) { Sfx.Play("U02"); Go(Phase.Connecting); }
                else Sfx.Play("U04");   // 잠긴 의뢰는 접속하지 않는다
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
                if (inp.Left || inp.Right || inp.Up || inp.Down) { EraseIndex = 1 - EraseIndex; dirty = true; Sfx.Play("U01"); }
                if (inp.Esc) { EraseConfirm = false; dirty = true; Sfx.Play("U03"); }
                else if (inp.Enter)
                {
                    EraseConfirm = false;
                    dirty = true;
                    Sfx.Play(EraseIndex == 1 ? "U02" : "U03");   // YES: 확인 / NO: 닫기
                    if (EraseIndex == 1)
                    {
                        Progress.Clear(AllRequestIds());   // 의뢰 기록(별·클리어)만 지운다. 설정 값은 유지
                        lastPlayed = -1;
                        mainItemsReady = false;
                        RefreshMainItems();
                        MainIndex = 0;
                        Go(Phase.Booting);
                    }
                }
                return;
            }
            int n = mainItems.Length;
            if (inp.Down) { MainIndex = (MainIndex + 1) % n; dirty = true; Sfx.Play("U01"); }
            if (inp.Up) { MainIndex = (MainIndex + n - 1) % n; dirty = true; Sfx.Play("U01"); }
            if (!inp.Enter) return;
            Sfx.Play("U02");
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
                if (inp.Esc) { ControlsOpen = false; dirty = true; Sfx.Play("U03"); }
                return;
            }
            if (inp.Esc) { Sfx.Play("U03"); CloseSettings(); return; }
            if (inp.Down) { SettingsIndex = (SettingsIndex + 1) % MenuRenderer.SettingsRows; dirty = true; Sfx.Play("U01"); }
            if (inp.Up) { SettingsIndex = (SettingsIndex + MenuRenderer.SettingsRows - 1) % MenuRenderer.SettingsRows; dirty = true; Sfx.Play("U01"); }
            if (inp.Left) ChangeSetting(-1);
            if (inp.Right) ChangeSetting(1);
            if (inp.Enter)
            {
                if (SettingsIndex == 2) ChangeSetting(1);
                else if (SettingsIndex == 5) { ControlsOpen = true; dirty = true; Sfx.Play("U02"); }
                else if (SettingsIndex == 6) { Sfx.Play("U03"); CloseSettings(); }
            }
        }

        /// <summary>선택한 줄의 값을 dir(-1, +1)만큼 바꾸고 바로 저장한다.</summary>
        void ChangeSetting(int dir)
        {
            string tick = "U09";   // 토글·단계 값: U01보다 낮은 틱. 음량 줄은 U01(바뀐 음량으로 들려 줌)
            bool changed = true;
            switch (SettingsIndex)
            {
                case 0: { int v = Progress.BgmVolume; Progress.BgmVolume += dir; changed = Progress.BgmVolume != v; tick = "U01"; break; }
                case 1: { int v = Progress.SfxVolume; Progress.SfxVolume += dir; changed = Progress.SfxVolume != v; tick = "U01"; break; }
                case 2: Progress.EnvFxReduced = !Progress.EnvFxReduced; break;
                case 3:
                {
                    GlitchStrength before = Progress.GlitchSetting;
                    Progress.GlitchSetting = (GlitchStrength)Mathf.Clamp((int)Progress.GlitchSetting + dir, 0, 2);
                    changed = Progress.GlitchSetting != before;
                    Glitch.Strength = Progress.GlitchSetting;
                    if (Glitch.Strength == GlitchStrength.Off) Glitch.Cancel();
                    break;
                }
                case 4:
                {
                    // W-034: 언어는 양끝에서 멈춘다(ENGLISH <-> 한국어). 바뀌면 다음 그리기부터 바뀐 언어
                    Language before = Progress.Language;
                    Progress.Language = (Language)Mathf.Clamp((int)Progress.Language + dir, 0, 1);
                    changed = Progress.Language != before;
                    Loc.Current = Progress.Language;
                    break;
                }
                default: return;
            }
            if (changed) Sfx.Play(tick);
            dirty = true;
        }

        /// <summary>일시정지에서 연 설정 창 그리기(GameRenderer가 부른다). (cx, cy)는 창 중심.</summary>
        public void DrawSettingsInGame(int cx, int cy)
        {
            menus.DrawSettings(this, cx - MenuRenderer.SettingsW / 2, cy - MenuRenderer.SettingsH / 2);
            if (ControlsOpen) menus.DrawControls(cx, cy);
        }

        /// <summary>열 밀기: 팝업 안 열 띠의 왼쪽 이동량(px). 챕터 포커스 0, 스테이지 포커스 MenuRenderer.ColumnStep, 사이는 정수 px로 보간.</summary>
        int ColumnShift()
        {
            float p = Mathf.Clamp01((Fx.TickOf(slideElapsed) + 1) / (float)Fx.ColumnSlideTicks);
            if (slideElapsed >= Fx.ColumnSlideTicks * Fx.TickSeconds) p = 1f;
            return Mathf.RoundToInt(MenuRenderer.ColumnStep * (stageFocus ? p : 1f - p));
        }

        /// <summary>메뉴 화면을 캔버스에 다시 그린다. 자동 확인·캡처가 직접 부를 수도 있다.</summary>
        public void RedrawMenu()
        {
            switch (CurrentPhase)
            {
                case Phase.Main: menus.DrawMain(MainIndex, mainItems, EraseConfirm, EraseIndex); break;
                case Phase.Settings: menus.DrawSettingsMain(this); break;
                case Phase.Credits: menus.DrawCredits(); break;
                case Phase.Booting:
                    if (CrtActive) menus.DrawCrt(CrtStage, CrtStageSeconds, MainIndex, mainItems);   // W-028(D-109 ⑤): 게임 시작 CRT 켜짐
                    else menus.DrawBoot(BootStage, BootSeconds, BootTotalSeconds);
                    break;
                case Phase.Desktop:
                    menus.DrawDesktop(DeskIcon, PopupOpen, Fx.TickOf(popupElapsed), ColumnShift(), InFolder, FolderIndex, DeskIndex, -1f, DevLockedShown);
                    if (GuideOpen) menus.DrawGuide(GuidePageIds, GuidePage);   // W-049
                    break;
                case Phase.Preview: menus.DrawPreview(previewElapsed); break;
                case Phase.Connecting: menus.DrawDesktop(0, true, Fx.PopupOpenTicks, MenuRenderer.ColumnStep, true, FolderIndex, DeskIndex, loadElapsed / settings.connectSeconds, false); break;
            }
            if (scrStage == 3 && CurrentPhase == Phase.Desktop) game.DrawScreenFx(scrStage, scrElapsed);   // W-052: 바탕화면 위 켜짐 덮개
            Canvas.Apply();
        }

        bool rawShift;   // W-033: 지난 프레임의 실제 Shift(누른 순간 판단용)

        void UpdatePlaying(InputFrame inp)
        {
            if (goElapsed >= 0f)
            {
                // W-052 게임오버 1·2단계(치지직 -> 층 보기 꺼짐): 판정은 이미 끝났고 입력은 받지 않는다. 시간이 다 되면 게임오버 창(D-087, 타이밍만 늦춤)
                Sim.ShiftHeld = false;
                goElapsed += Mathf.Min(Time.deltaTime, 0.1f);
                if (goElapsed >= goGlitchSeconds + Fx.GameOverViewOffSeconds) { goElapsed = -1f; EnterEnded(); }
                return;
            }
            if (failElapsed >= 0f)
            {
                // W-028 1-9: 실패 지점 깜빡임 중. 판정은 이미 끝났고 입력은 받지 않는다. 0.4초 뒤 기존대로 처음부터 다시 시작
                Sim.ShiftHeld = false;
                failElapsed += Mathf.Min(Time.deltaTime, 0.1f);
                if (failElapsed >= Fx.FailSeconds) RestartGame(true);
                return;
            }
            if (!sfxSynced) SfxSync();
            if (scrStage == 3) { Sim.ShiftHeld = false; UpdateScreenFx(); return; }   // W-052 4단계 켜짐: 재시작된 의뢰가 비치는 동안 시간·입력은 멈춘다
            if (inp.Esc)
            {
                Sfx.Play("U02");   // U08: 일시정지 열기(U02 재사용). 닫기는 U03
                PauseIfPlaying();
                return;
            }
            // W-033: 튜토리얼 단계가 떠 있으면 시간이 멈추고, 받는 키만 평소 입력 처리에 넣는다(틀린 키는 무시 + 배지 흔들림)
            InputFrame raw = inp;
            bool shiftEdge = raw.LeftShift && !rawShift;
            rawShift = raw.LeftShift;
            bool tutFrozen = Tut != null && Tut.Frozen;
            if (tutFrozen) inp = Tut.Filter(raw, shiftEdge, Sim);
            if (inp.Bracket) { CurrentTab = CurrentTab == Tab.Mission ? Tab.Log : Tab.Mission; Sfx.Play("U09"); }   // D-050

            int returns = Sim.ReturnSeq;
            Sim.ShiftHeld = inp.LeftShift;   // 밀린 방이 있으면 Shift를 떼는 순간 제자리로 돌아온다(W-014 R4)
            bool returned = Sim.ReturnSeq != returns;
            if (inp.LeftShift && !prevShift) { Log("LShift"); Glitch.Play(Glitch.ShiftIn, AimSectorRect()); Sfx.Play("G03"); }   // 설계도 진입 글리치(조준 섹터)
            else if (!inp.LeftShift && prevShift)
            {
                if (returned) { Glitch.Play(Glitch.RoomPush, CellRect(Sim.LastMoveFrom), CellRect(Sim.LastMoveTo), PushDirOf(Sim.LastMoveFrom, Sim.LastMoveTo)); Sfx.Play("G07"); }   // 방이 제자리로 돌아옴
                else { Glitch.Play(Glitch.ShiftOut, AimSectorRect()); Sfx.Play("G05"); }
            }
            prevShift = inp.LeftShift;

            // Shift를 누른 동안 방향키는 방 밀기(W-014, 레일이 있는 레벨만). 조준 이동은 하지 않는다(R7)
            if (inp.LeftShift)
            {
                if (inp.Left) TryPush(-1, 0, "Left");
                if (inp.Right) TryPush(1, 0, "Right");
                if (inp.Up) TryPush(0, -1, "Up");
                if (inp.Down) TryPush(0, 1, "Down");
                if (inp.Digit > 0) TryPhase(inp.Digit);   // W-035: Shift + 숫자 = 조준한 방의 박자를 N박 앞당김(시계 표시가 있는 방만)
            }
            else
            {
                if (inp.Left) TryAim(-1, 0, "Left");
                if (inp.Right) TryAim(1, 0, "Right");
                if (inp.Up) TryAim(0, -1, "Up");
                if (inp.Down) TryAim(0, 1, "Down");
            }

            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            // W-052(D-138): W·A·S·D를 누르는 동안 시점만 옮긴다(조준·판정은 그대로, 튜토리얼 멈춤 단계에서도). 조준이 바뀌면 Sim이 이동량을 거둔다
            Sim.PanCamera((raw.PanRight ? 1 : 0) - (raw.PanLeft ? 1 : 0), (raw.PanDown ? 1 : 0) - (raw.PanUp ? 1 : 0), dt);
            if (tutFrozen && !Tut.Evaluate(raw, shiftEdge, Sim))
            {
                Sim.AdvanceCamera(dt);   // 시간은 멈추고 카메라만 조준을 따라간다
                Tut.Tick(dt);
                return;
            }
            Sim.Step(dt);
            if (Sim.DetectCount != seenDetect)
            {
                // 발각(W-016, 02-06 §5): 위험 프리셋 글리치 1회(감시 구역 방) + 경보음 짧게 + 로그 한 줄 [제안]
                seenDetect = Sim.DetectCount;
                Log("CAMERA: SPOTTED");
                Glitch.Play(Glitch.Danger, CellRect(Sim.LastDetectCell));
                PlayAlertSound();
            }
            // W-030 F3: 조력자가 현실과 다른 유효 상태의 장치를 지나가거나 이용한 순간 명령 기록에 한 줄(링은 GameRenderer가 같은 값을 읽어 그린다)
            while (seenUse < Sim.UseSeq)
            {
                seenUse++;
                if (Sim.UseSeq - seenUse < GameSim.UseHistory) Log(Fx.UseText(Sim.UseAt(seenUse).Kind), true);
            }
            SfxWatch();
            if (Sim.Outcome == Outcome.Failed)
            {
                // 실패 지점(W-007): 결과 창 없이 처음부터 다시(브리핑 건너뜀, 기존 재시작과 같음). W-028 1-9: 그 전에 실패 섹터 위 빨간 사각형을 0.4초 깜빡인다
                failElapsed = 0f;
                FailCell = FindFailCell();
                Sim.ShiftHeld = false;
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
                    else { EnterEnded(); Glitch.PlayFull(Glitch.Transition); Sfx.Play("T08"); }   // 결과 화면 전환
                }
                else
                {
                    // 게임오버(추적됨, W-052): 1 층 보기 영역만 치지직(글리치 켜짐일 때만) -> 2 그 영역이 꺼짐 -> 게임오버 창(종료 대화 없음). 판정은 그대로(D-087)
                    bool glitchOn = Glitch.Strength != GlitchStrength.Off && Glitch.GameOver != null;
                    goGlitchSeconds = glitchOn ? Glitch.GameOver.TotalSeconds : 0f;
                    if (glitchOn)
                    {
                        Glitch.Play(Glitch.GameOver, SceneViewRect);
                        Sfx.Play("T08");
                    }
                    goElapsed = 0f;
                }
            }
        }

        /// <summary>W-052 4단계 시작: 게임오버 창에서 RESTART를 고르면 화면 전체가 CRT 꺼짐 -> 짧은 검정 -> 켜짐 뒤 처음부터 다시 시작한다.</summary>
        void BeginScreenFx()
        {
            scrStage = 1;
            scrElapsed = 0f;
            dirty = true;
        }

        /// <summary>W-052 4단계 진행. 꺼짐·검정이 끝나면 재시작(브리핑은 건너뜀, RestartGame)하고 켜짐 단계로, 켜짐이 끝나면 연출을 마친다.</summary>
        void UpdateScreenFx()
        {
            scrElapsed += Mathf.Min(Time.deltaTime, 0.1f);
            if (scrStage == 1 && scrElapsed >= Fx.GameOverScreenOffSeconds) { scrStage = 2; scrElapsed = 0f; }
            else if (scrStage == 2 && scrElapsed >= Fx.GameOverBlackSeconds) { if (goChoice == 1) OpenDesktop(true); else RestartGame(); scrStage = 3; scrElapsed = 0f; }   // RestartGame이 scrStage를 0으로 돌리므로 그 뒤에 3으로. DESKTOP은 결과 창의 DESKTOP과 같은 OpenDesktop(true)
            else if (scrStage == 3 && scrElapsed >= Fx.GameOverOnSeconds) { scrStage = 0; scrElapsed = 0f; }
        }

        /// <summary>대상이 닿은 실패 지점이 있는 섹터(x = 열, y = 층). 같은 층에서 대상 위치와 가장 가까운 실패 지점, 없으면 대상이 선 섹터.</summary>
        Vector2Int FindFailCell()
        {
            Board b = Sim.Board;
            SectorObject best = null;
            float bestD = float.MaxValue;
            foreach (SectorObject o in b.Objects)
            {
                if (o.Def.kind != ObjectKind.FailPoint || o.Floor != Sim.TargetFloor) continue;
                float d = Mathf.Abs(o.AbsX - Sim.TargetX);
                if (d < bestD) { bestD = d; best = o; }
            }
            if (best != null) return new Vector2Int(best.Col, best.Floor);
            return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(Sim.TargetX / b.SectorW), 0, b.Cols - 1), Sim.TargetFloor);
        }

        /// <summary>미는 방향의 글리치 치우침 부호(가로 이동이면 그 방향, 세로 이동이면 +1).</summary>
        static int PushDirOf(Vector2Int from, Vector2Int to) => to.x != from.x ? (to.x > from.x ? 1 : -1) : 1;

        /// <summary>조준 점프 한 번(Shift를 안 누른 상태). 옮겼으면 로그·G01, 후보가 없으면 G02(W-020).</summary>
        void TryAim(int dx, int dy, string name)
        {
            if (Sim.MoveAim(dx, dy)) { Log(name); Sfx.Play("G01"); }
            else Sfx.Play("G02");
        }

        /// <summary>Shift + 방향키 한 번(W-014). 성공하면 로그·글리치, 막히면 방 흔들림과 짧은 소리(R8).</summary>
        void TryPush(int dx, int dy, string name)
        {
            int blocks = Sim.BlockSeq;
            if (Sim.TryPush(dx, dy))
            {
                Log("Push " + name);
                Glitch.Play(Glitch.RoomPush, CellRect(Sim.LastMoveFrom), CellRect(Sim.LastMoveTo), PushDirOf(Sim.LastMoveFrom, Sim.LastMoveTo));
                Sfx.Play("G06");
            }
            else if (Sim.BlockSeq != blocks)
            {
                game.StartShake(Sim.Aim.y * Sim.Board.Cols + Sim.Aim.x);
                PlayBlockSound();
            }
        }

        /// <summary>Shift + 숫자 한 번(W-035). 성공하면 로그·소리, 시계가 없는 방이거나 방이 밀린 상태이면 방 흔들림과 짧은 소리(방 밀기 막힘과 같다).</summary>
        void TryPhase(int n)
        {
            int blocks = Sim.BlockSeq;
            if (Sim.TrySetPhase(n))
            {
                Log("Shift +" + n);
                Sfx.Play("G06");
            }
            else if (Sim.BlockSeq != blocks)
            {
                game.StartShake(Sim.Aim.y * Sim.Board.Cols + Sim.Aim.x);
                PlayBlockSound();
            }
        }

        // ---------- 효과음·BGM 연결(W-020, sfx-plan §2·§4, bgm-plan §4-3) ----------
        // 판정 코드(GameSim)는 건드리지 않는다. 화면·메뉴 입력은 그 자리에서 재생하고, 의뢰 중 사건은 지난 프레임과 Sim 값을 비교해 재생한다.
        // 소리 목록의 클립이 비어 있으면 조용히 넘어간다(G08·A05만 코드 합성 소리가 대신 난다).

        /// <summary>효과음 재생기와 BGM 재생기(자동 확인이 요청 횟수·현재 곡을 읽는다).</summary>
        public SfxPlayer Sfx { get; private set; }
        public BgmPlayer Bgm { get; private set; }

        /// <summary>막힘(G08)·경보(A05) 재생 요청 횟수(자동 확인용, 클립이 없어도 오른다)와 마지막 요청의 최종 음량(0이면 소리를 내지 않았다, W-019).</summary>
        public int BlockSoundCount => Sfx.RequestCount("G08");
        public int AlertSoundCount => Sfx.RequestCount("A05");
        public float LastSfxGain => Sfx.LastGain;

        void PlayBlockSound() => Sfx.Play("G08");
        void PlayAlertSound() => Sfx.Play("A05");

        int playChapter;                // 지금 하는 의뢰의 챕터(BGM 곡 고르기)
        int starsLeft;                  // 결과 창에서 아직 소리를 내지 않은 별 수(T07)
        int starsTotal;
        float starTimer;
        const float StarInterval = Fx.StarSeconds;
        const float FootstepPx = 12f;   // A01: 대상이 이만큼 걸을 때마다 발소리 한 번 [제안]

        // 지난 프레임 값(SfxWatch가 비교)
        bool sfxSynced;
        GameSim sfxSim;
        int sfxReset, sfxFall, sfxShiftSeq, sfxAlertSeq, sfxDetect, sfxFloor, sfxCountTick, sfxRetreat;
        float sfxTracking, sfxTargetX, stepAccum;
        bool sfxRelay, sfxCountdown, sfxOver;
        Outcome sfxOutcome;
        bool[] sfxMissionDone = new bool[0], sfxMissionFail = new bool[0], sfxObjOn = new bool[0], sfxBlocked = new bool[0];

        /// <summary>결과 창으로 간다. 성공(튜토리얼 제외)이면 별 소리(T07)를 0.15초 간격으로 예약한다.</summary>
        void EnterEnded()
        {
            CurrentPhase = Phase.Ended;
            goChoice = 0;
            starsLeft = starsTotal = Sim != null && Sim.Outcome == Outcome.Complete && !Sim.Board.Tutorial ? Sim.Stars : 0;
            starTimer = 0f;
            endedElapsed = 0f;   // W-028: 결과 창 올라옴·별 찍힘·TRAPPED 도장의 기준 시간
        }

        /// <summary>그 의뢰 데이터가 의뢰 목록의 몇 챕터인지(목록에 없으면 -1). DebugStartLevel이 BGM 곡을 고르는 데 쓴다.</summary>
        int ChapterOfLevel(LevelData level)
        {
            for (int i = 0; i < Desk.Count; i++)
                if (Desk.Entry(i).level == level) return Desk.ChapterOf(i);
            return -1;
        }

        /// <summary>
        /// 지금 화면에서 나와야 할 곡 슬롯(bgm-plan §4-3). 부팅·접속 로딩은 곡 없음, 카운트다운은 alert(해제되면 원래 의뢰 곡),
        /// 게임오버는 sting_gameover, 결과 성공은 sting_clear.
        /// </summary>
        string DesiredTrack()
        {
            switch (CurrentPhase)
            {
                case Phase.Main:
                case Phase.Credits: return BgmSet.Title;
                case Phase.Booting:
                case Phase.Connecting: return "";
                case Phase.Desktop:
                case Phase.Preview: return BgmSet.Desk;   // W-047: 예고 화면도 바탕화면 곡 그대로
                case Phase.Briefing:
                case Phase.Ending: return BgmSet.Briefing;
                case Phase.Settings:
                    if (!SettingsFromPause) return BgmSet.Title;
                    break;
            }
            if (Sim == null) return "";
            if (Sim.Outcome == Outcome.GameOver) return BgmSet.StingGameOver;
            if (CurrentPhase == Phase.Ended) return Sim.Outcome == Outcome.Complete ? BgmSet.StingClear : BgmSet.StingGameOver;
            if (Sim.Outcome == Outcome.Complete) return Bgm.CurrentTrack;   // 같은 프레임에 종료 대화·결과 창으로 넘어간다
            if (Sim.CountdownActive) return BgmSet.Alert;
            return playChapter == 0 ? BgmSet.Ch0 : BgmSet.Ch1;
        }

        /// <summary>매 프레임: 반복음 켜고 끄기, 결과 별 소리, BGM 곡·음량.</summary>
        void UpdateAudio()
        {
            if (CurrentPhase != Phase.Playing) sfxSynced = false;
            bool live = CurrentPhase == Phase.Playing && Sim != null && Sim.Outcome == Outcome.Running && goElapsed < 0f;
            // 일시정지·결과·게임오버·대화 중에는 Shift 험(G04)을 멈춘다. 빗소리(E01)는 비 오는 의뢰의 진행·브리핑 중에만, 환경 효과 줄이기가 켜지면 멈춘다
            Sfx.SetLoop("G04", live && Sim.ShiftHeld);
            Sfx.SetLoop("E01", Sim != null && Sim.Board.Weather == Weather.Rain && !Progress.EnvFxReduced && (live || CurrentPhase == Phase.Briefing));
            Sfx.Tick();

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (CurrentPhase == Phase.Ended) endedElapsed += dt;
            if (CurrentPhase == Phase.Ended && starsLeft > 0)
            {
                starTimer -= dt;
                if (starTimer <= 0f)
                {
                    Sfx.Play("T07", (starsTotal - starsLeft) * 2f);   // 별마다 음이 한 단계(온음)씩 올라간다 [제안]
                    starsLeft--;
                    starTimer = StarInterval;
                }
            }
            else if (CurrentPhase != Phase.Ended) starsLeft = 0;

            Bgm.Update(DesiredTrack(), GameFrozen, dt);
        }

        /// <summary>지난 프레임 값을 지금 값으로 맞춘다(소리 없음). 의뢰가 시작·다시 시작되거나 일시정지에서 돌아온 첫 프레임에 부른다.</summary>
        void SfxSync()
        {
            GameSim s = Sim;
            sfxSynced = true;
            sfxSim = s;
            sfxReset = s.ResetSeq;
            sfxFall = s.FallCount;
            sfxShiftSeq = s.TargetShiftSeq;
            sfxAlertSeq = s.AlertSeq;
            sfxDetect = s.DetectCount;
            sfxRetreat = s.RetreatSeq;
            sfxFloor = s.TargetFloor;
            sfxTargetX = s.TargetX;
            stepAccum = 0f;
            sfxTracking = s.Tracking;
            sfxRelay = s.RelayUsed;
            sfxCountdown = s.CountdownActive;
            sfxCountTick = Mathf.CeilToInt(s.Countdown);
            sfxOver = s.Tracking >= settings.surpriseTracking;
            sfxOutcome = s.Outcome;
            if (sfxMissionDone.Length != s.MissionCount) { sfxMissionDone = new bool[s.MissionCount]; sfxMissionFail = new bool[s.MissionCount]; }
            for (int i = 0; i < s.MissionCount; i++) { sfxMissionDone[i] = s.MissionDone(i); sfxMissionFail[i] = s.MissionFailed(i); }
            Board b = s.Board;
            if (sfxObjOn.Length != b.Objects.Count) sfxObjOn = new bool[b.Objects.Count];
            for (int i = 0; i < b.Objects.Count; i++) sfxObjOn[i] = s.ObjectOn(b.Objects[i]);
            if (sfxBlocked.Length != b.Floors * b.Cols) sfxBlocked = new bool[b.Floors * b.Cols];
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++) sfxBlocked[f * b.Cols + c] = b.IsWhole(f, c) && s.SectorBlocked(f, c);
        }

        /// <summary>의뢰 중 사건 소리(sfx-plan §2). 막힘(G08)·경보(A05)·밀기(G06)·되돌아옴(G07)·Shift·조준은 그 자리에서 이미 재생한다. 값 비교만 하고 Sim은 바꾸지 않는다.</summary>
        void SfxWatch()
        {
            GameSim s = Sim;
            if (!sfxSynced || sfxSim != s || sfxReset != s.ResetSeq) { SfxSync(); return; }

            // 장치 상태 바뀜(O01~O06): 같은 ID는 한 프레임에 1번이라 종류별로 1번만 난다. 켜짐·열림은 반음 +2, 꺼짐·닫힘은 -2 [제안]
            Board b = s.Board;
            for (int i = 0; i < b.Objects.Count; i++)
            {
                SectorObject o = b.Objects[i];
                bool on = s.ObjectOn(o);
                if (on == sfxObjOn[i]) continue;
                sfxObjOn[i] = on;
                string id = null;
                switch (o.Def.kind)
                {
                    case ObjectKind.Door: id = "O01"; break;
                    case ObjectKind.Stairs: id = "O02"; break;
                    case ObjectKind.Hatch: id = "O03"; break;
                    case ObjectKind.Elevator: id = "O05"; break;
                    case ObjectKind.Camera: id = "O06"; break;
                }
                if (id != null) Sfx.Play(id, on ? 2f : -2f);
            }
            for (int f = 0; f < b.Floors; f++)
                for (int c = 0; c < b.Cols; c++)
                {
                    if (!b.IsWhole(f, c)) continue;
                    bool blocked = s.SectorBlocked(f, c);
                    if (blocked == sfxBlocked[f * b.Cols + c]) continue;
                    sfxBlocked[f * b.Cols + c] = blocked;
                    Sfx.Play("O04", blocked ? 2f : -2f);   // 레이저(섹터 차단막) 켜짐·꺼짐
                }

            // 조력자 이동: 층 이동(A02)·떨어짐(A03)·돌아섬(A04)·발소리(A01)
            if (s.FallCount > sfxFall) Sfx.Play("A03");
            else if (s.TargetFloor != sfxFloor && s.TargetShiftSeq == sfxShiftSeq) Sfx.Play("A02");
            if ((s.AlertSeq - sfxAlertSeq) - (s.DetectCount - sfxDetect) > 0) Sfx.Play("A04");   // 발각이 아닌 위험 반응만
            if (s.RetreatSeq > sfxRetreat) Sfx.Play("A04");   // W-035: 프레스 아래에서 돌아섬(위험 반응과 같은 소리)
            if (s.TargetMoving && s.TargetFloor == sfxFloor && s.TargetShiftSeq == sfxShiftSeq)
            {
                stepAccum += Mathf.Abs(s.TargetX - sfxTargetX);
                if (stepAccum >= FootstepPx) { stepAccum %= FootstepPx; Sfx.Play("A01"); }
            }
            else if (s.TargetFloor != sfxFloor) stepAccum = 0f;

            // 미션(A06·A09)·추적도(A07·A08·T01)
            bool a06 = false, a09 = false;
            for (int i = 0; i < s.MissionCount; i++)
            {
                bool done = s.MissionDone(i), fail = s.MissionFailed(i);
                MissionKind k = s.Missions[i].kind;
                if (i > 0 && done && !sfxMissionDone[i] && (k == MissionKind.PassObject || k == MissionKind.EnterSector)) a06 = true;
                if (fail && !sfxMissionFail[i]) a09 = true;
                sfxMissionDone[i] = done; sfxMissionFail[i] = fail;
            }
            if (a06) Sfx.Play("A06");
            if (a09) Sfx.Play("A09");
            if (s.RelayUsed && !sfxRelay) Sfx.Play("A08");
            else if (s.Tracking < sfxTracking - 0.001f) Sfx.Play("A07");
            bool over = s.Tracking >= settings.surpriseTracking;
            if (over && !sfxOver) Sfx.Play("T01");   // 70% 넘음: 경고 비프 1번, 내려갔다 다시 넘으면 다시
            sfxOver = over;

            // 카운트다운: 진입 T02, 1초마다 T03(마지막 2초는 높은 음), 해제 T04
            if (s.CountdownActive)
            {
                int tick = Mathf.CeilToInt(s.Countdown);
                if (!sfxCountdown) { Sfx.Play("T02"); sfxCountTick = tick; }
                else if (tick < sfxCountTick)
                {
                    if (tick >= 1) Sfx.Play("T03", tick <= 2 ? 7f : 0f);
                    sfxCountTick = tick;
                }
            }
            else if (sfxCountdown && s.Outcome == Outcome.Running) Sfx.Play("T04");
            sfxCountdown = s.CountdownActive;

            // 결과: 챕터 0 가두기 성공 A10, 출구 도달 A12, 실패 지점 A11
            if (s.Outcome != sfxOutcome)
            {
                if (s.Outcome == Outcome.Complete) Sfx.Play(s.Board.Tutorial ? "A10" : "A12");
                else if (s.Outcome == Outcome.Failed) Sfx.Play("A11");
                sfxOutcome = s.Outcome;
            }

            sfxFall = s.FallCount; sfxShiftSeq = s.TargetShiftSeq; sfxAlertSeq = s.AlertSeq; sfxDetect = s.DetectCount; sfxRetreat = s.RetreatSeq;
            sfxFloor = s.TargetFloor; sfxTargetX = s.TargetX; sfxTracking = s.Tracking; sfxRelay = s.RelayUsed;
        }

        void UpdatePaused(InputFrame inp)
        {
            if (GuideOpen) { UpdateGuide(inp); return; }   // W-049: 도움말이 열려 있는 동안 게임은 멈춘 그대로, Esc는 일시정지 메뉴로 돌아간다
            if (inp.Esc)
            {
                Sfx.Play("U03");   // U08: 일시정지 닫기
                CurrentPhase = Phase.Playing;
                return;
            }
            if (inp.Down) { pauseIndex = (pauseIndex + 1) % PauseItemCount; Sfx.Play("U01"); }
            if (inp.Up) { pauseIndex = (pauseIndex + PauseItemCount - 1) % PauseItemCount; Sfx.Play("U01"); }
            if (inp.Enter)
            {
                Sfx.Play(pauseIndex == 0 ? "U03" : "U02");   // Continue는 닫기, 나머지는 확인
                if (pauseIndex == 0) CurrentPhase = Phase.Playing;
                else if (pauseIndex == 1) RestartGame();
                else if (pauseIndex == 2) OpenSettings(true);   // W-019: 환경 효과 줄이기 등은 설정 화면 안에 있다
                else if (pauseIndex == 3) OpenGuide();          // W-049: 도움말(GUIDE)
                else OpenDesktop(true);
            }
        }

        void Log(string cmd, bool use = false)
        {
            commandLog.Add("> " + cmd.ToUpperInvariant());
            commandLogUse.Add(use);
            if (commandLog.Count > MaxLog) { commandLog.RemoveAt(0); commandLogUse.RemoveAt(0); }
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
