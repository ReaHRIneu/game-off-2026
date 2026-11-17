using System.Collections.Generic;
using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 메인 컨트롤러: 입력 처리, 화면 단계(메인·바탕화면·대화·플레이·일시정지·결과). 모든 화면은 640x360 도트 캔버스로 그린다.
    /// 규칙은 GameSim이 맡고, 여기서는 입력을 넘기고 결과를 그린다.
    /// </summary>
    public sealed class GameView : MonoBehaviour
    {
        /// <summary>
        /// 화면 상태(D-056~D-059): 메인 / 크레딧 / 부팅(모니터 켜짐·로딩) / 바탕화면(의뢰 선택, 상세 패널 포함) /
        /// 접속 로딩 / 브리핑 대화 / 게임 / 일시정지 / 종료 대화 / 결과. Connecting까지는 MenuRenderer, 그 뒤는 GameRenderer가 그린다.
        /// </summary>
        public enum Phase { Main, Credits, Booting, Desktop, Connecting, Briefing, Playing, Paused, Ending, Ended }

        /// <summary>오른쪽 위 공용 패널의 탭(D-043).</summary>
        public enum Tab { Mission, Log }

        [SerializeField] GameSettings settings;
        [SerializeField] RequestList requests;
        [SerializeField] ArtSet art;

        public Phase CurrentPhase { get; private set; } = Phase.Main;
        public GameSim Sim { get; private set; }
        /// <summary>챕터 0 튜토리얼 안내 상태. 튜토리얼 의뢰가 아니면 null(W-005).</summary>
        public Tutorial Tut { get; private set; }
        public PixelCanvas Canvas { get; private set; }
        public RequestBoard Desk { get; private set; }
        public ProgressStore Progress { get; private set; }
        public int MainIndex { get; private set; }
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
        DialogueLine[] activeLines;
        int lineIndex;
        bool dirty = true;
        float loadElapsed;     // 현재 단계(부팅·접속)의 경과 시간
        float panelElapsed;    // 패널 등장 경과 시간
        // 일시정지 메뉴: 0 = Continue, 1 = Restart, 2 = Desktop
        int pauseIndex;
        bool prevShift;
        readonly List<string> commandLog = new List<string>();

        const int MaxLog = 9;

        // ---------- 수명 ----------

        void Awake()
        {
            Progress = new ProgressStore();
            Desk = new RequestBoard(requests, Progress);
            Canvas = new PixelCanvas(art);
            menus = new MenuRenderer(Canvas, Desk);
            game = new GameRenderer(Canvas);
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
            LevelData level = Desk.Entry(index).level;
            Sim = new GameSim(settings, new Board(level));
            Tut = level.tutorial ? new Tutorial(level) : null;
            Shown.Clear();
            RestartGame();
            DialogueData d = Desk.Entry(index).dialogue;
            if (d != null && d.briefing != null && d.briefing.Length > 0) BeginDialogue(Phase.Briefing, d.briefing);
        }

        /// <summary>처음부터 다시(D-031). 브리핑 대화는 건너뛰고 LOG 탭의 기록은 유지한다 [제안].</summary>
        void RestartGame(bool afterFail = false)
        {
            Sim.Reset();
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
            }
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

            if (IsMenuPhase)
            {
                if (dirty)
                {
                    dirty = false;
                    RedrawMenu();
                }
            }
            else if (Sim != null)
            {
                game.Draw(this);   // 의뢰 화면은 매 프레임 다시 그린다
                Canvas.Apply();
            }
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

        /// <summary>메뉴 화면을 캔버스에 다시 그린다. 자동 확인·캡처가 직접 부를 수도 있다.</summary>
        public void RedrawMenu()
        {
            switch (CurrentPhase)
            {
                case Phase.Main: menus.DrawMain(MainIndex); break;
                case Phase.Credits: menus.DrawCredits(); break;
                case Phase.Booting: menus.DrawBoot(BootStage); break;
                case Phase.Desktop: menus.DrawDesktop(InFolder, FolderIndex, DeskIndex, PanelOpen ? PanelX() : -1, -1f); break;
                case Phase.Connecting: menus.DrawDesktop(true, FolderIndex, DeskIndex, MenuRenderer.PanelTargetX, loadElapsed / settings.connectSeconds); break;
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
            if (inp.Bracket) CurrentTab = CurrentTab == Tab.Mission ? Tab.Log : Tab.Mission;   // D-050

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

            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            Sim.Step(dt);
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
                    else CurrentPhase = Phase.Ended;
                }
                else
                {
                    CurrentPhase = Phase.Ended;   // 게임오버는 종료 대화 없이 바로 게임오버 창
                }
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

        /// <summary>캔버스를 정수 배율로 확대해 그린다. 남는 부분은 검은 레터박스.</summary>
        void DrawCanvas()
        {
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            int k = Mathf.Max(1, Mathf.Min(Screen.width / PixelCanvas.W, Screen.height / PixelCanvas.H));
            int w = PixelCanvas.W * k, h = PixelCanvas.H * k;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.DrawTexture(r, Canvas.Texture, ScaleMode.StretchToFill, false);
        }
    }
}
