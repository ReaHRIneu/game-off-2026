using System.IO;
using GameOff.Data;
using GameOff.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameOff.EditorTools
{
    /// <summary>
    /// MVP-1 에셋(설정·테스트 의뢰)과 메인 씬을 만들고 Build Settings에 등록한다. 여러 번 실행해도 안전하다.
    /// 메뉴: GameOff/Setup MVP-1
    /// </summary>
    public static class Mvp1Setup
    {
        const string DataDir = "Assets/_Project/Data";
        const string SceneDir = "Assets/_Project/Scenes";
        public const string ScenePath = SceneDir + "/Main.unity";

        [MenuItem("GameOff/Setup MVP-1")]
        public static void Run()
        {
            ArtImporter.ImportArt();
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(SceneDir);

            var settings = LoadOrCreate<GameSettings>(DataDir + "/GameSettings.asset");
            settings.targetSpeed = 48f;          // [제안] W-004
            settings.aimSideWeight = 2f;
            settings.cameraSeconds = 0.2f;
            // W-016 ① 공통 규칙 값(02-06 §7-2, D-098 처음 값)
            settings.helperAlertDistance = 48f;
            settings.detectTracking = 40f;
            settings.alertMarkSeconds = 0.5f;
            EditorUtility.SetDirty(settings);
            // W-016 ② 종류 정의: cam_basic(02-06 §7-3)
            var camBasic = LoadOrCreate<SurveillanceType>(DataDir + "/Surveillance_cam_basic.asset");
            camBasic.id = "cam_basic";
            camBasic.coverCols = 1;
            camBasic.realityOn = true;
            camBasic.blueprintOn = false;
            camBasic.detectTrackingOverride = -1f;
            camBasic.redetectSeconds = 3f;
            camBasic.artKey = "camera";
            EditorUtility.SetDirty(camBasic);
            var level = LoadOrCreate<LevelData>(DataDir + "/TestRequest.asset");

            // 테스트 의뢰 v2 [임시] — Docs/Development/W-004-sector-model-brief.md §3 (측면 단면도 섹터 모델)
            level.title = "Test request";
            level.buildingName = "LOGIX CO. BLDG";
            level.floors = 3; level.cols = 5;
            level.sectorWidth = 160; level.sectorHeight = 96;
            level.roofHeight = 24; level.wallThickness = 6; level.groundHeight = 32;
            level.backgroundKey = "scenes/building_test_base";
            level.sectors = new[]
            {
                // B1 차단막: 섹터 전체형, 켜짐 -> 꺼짐
                new SectorDef { floor = 0, col = 3, target = SectorTarget.Whole, realityBlocked = true, blueprintBlocked = false },
            };
            level.objects = new[]
            {
                // 문 D1: (2,0) 오른쪽 경계, 닫힘 -> 열림
                new ObjectDef { kind = ObjectKind.Door, floor = 2, col = 0, x = 154, drawX = 154, drawSprite = true, realityOn = false, blueprintOn = true, mission = -1 },
                // 단말 A(미션): (2,2) 창고
                new ObjectDef { kind = ObjectKind.Terminal, floor = 2, col = 2, x = 120, drawX = 120, drawSprite = true, realityOn = false, blueprintOn = false, mission = 1 },
                // 계단 셔터 S1: (2,4), 닫힘 -> 열림, 연결 (1,3) x80, 도착 후 오른쪽
                new ObjectDef { kind = ObjectKind.Stairs, floor = 2, col = 4, x = 60, drawX = 60, drawSprite = true, realityOn = false, blueprintOn = true, mission = -1,
                    hasLink = true, linkFloor = 1, linkCol = 3, linkX = 80, linkFacingRight = true },
                // 계단 S2: (1,3), 열림 -> 열림(도착 지점, 조작 불가, 연결 없음)
                new ObjectDef { kind = ObjectKind.Stairs, floor = 1, col = 3, x = 60, drawX = 60, drawSprite = true, realityOn = true, blueprintOn = true, mission = -1 },
                // 엘리베이터 E: (1,4) <-> (0,4), 멈춤 -> 작동, 두 섹터 공유, 도착 후 왼쪽. 그림(차)은 3F 쪽에만 있다.
                new ObjectDef { kind = ObjectKind.Elevator, floor = 1, col = 4, x = 80, drawX = 52, drawSprite = false, realityOn = false, blueprintOn = true, mission = -1,
                    hasLink = true, linkFloor = 0, linkCol = 4, linkX = 80, linkFacingRight = false, shareWithLink = true },
                new ObjectDef { kind = ObjectKind.Elevator, floor = 0, col = 4, x = 80, drawX = 52, drawSprite = true, realityOn = false, blueprintOn = true, mission = -1,
                    hasLink = true, linkFloor = 1, linkCol = 4, linkX = 80, linkFacingRight = false, shareWithLink = true },
                // 단말 B(미션): (0,2) 서버실
                new ObjectDef { kind = ObjectKind.Terminal, floor = 0, col = 2, x = 20, drawX = 20, drawSprite = true, realityOn = false, blueprintOn = false, mission = 2 },
                // 문 D2: (1,2) 왼쪽 경계, 열림 -> 열림(조작 불가)
                new ObjectDef { kind = ObjectKind.Door, floor = 1, col = 2, x = 0, drawX = -6, drawSprite = true, realityOn = true, blueprintOn = true, mission = -1 },
                // 중계기: (1,1)
                new ObjectDef { kind = ObjectKind.Relay, floor = 1, col = 1, x = 70, drawX = 70, drawSprite = true, realityOn = false, blueprintOn = false, mission = -1 },
                // 출구(최종 미션): (1,0). drawX는 표지 왼쪽(초록 문은 표지보다 6px 오른쪽에서 시작)
                new ObjectDef { kind = ObjectKind.Exit, floor = 1, col = 0, x = 22, drawX = 8, drawSprite = true, realityOn = false, blueprintOn = false, mission = 0 },
            };
            level.startFloor = 2; level.startCol = 0; level.startX = 40; level.startFacingRight = true;
            // 미션 목록(W-007 형식으로 옮김): 최종 = 출구 통과, 나머지 = 단말 A·B 통과(오브젝트의 mission 번호 0·1·2와 같은 순서)
            level.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },
                new MissionDef { kind = MissionKind.PassObject },
                new MissionDef { kind = MissionKind.PassObject },
            };

            // W-001 타일 격자 데이터: 쓰지 않지만 지우지 않고 보존한다.
            level.realityRows = new[]
            {
                "############",
                "#S.>....D.E#",
                "###.######.#",
                "#..>A.D.BR^#",
                "############",
            };
            level.blueprintRows = new[]
            {
                "############",
                "#S.v....O.E#",
                "###.######.#",
                "#..>A.O.BR^#",
                "############",
            };
            EditorUtility.SetDirty(level);
            EditorUtility.SetDirty(settings);

            // 테스트 의뢰의 임시 대사 [임시] — 시나리오 미정, 사용자가 나중에 이 에셋을 교체한다(W-003 지시서)
            var dialogue = LoadOrCreate<DialogueData>(DataDir + "/TestRequestDialogue.asset");
            dialogue.briefing = new[]
            {
                Line(Speaker.Client, "21:04", "PACKAGE IS LATE. OUR COURIER IS STUCK.", "neutral"),
                Line(Speaker.Me, "21:04", "ON IT.", "neutral"),
                Line(Speaker.Client, "21:05", "NO TRACE. PLEASE.", "neutral"),
                Line(Speaker.Me, "21:05", "NO PROMISES.", "disgust"),
            };
            dialogue.ending = new[]
            {
                Line(Speaker.Client, "21:09", "GOT IT. PAYMENT SENT.", "neutral"),
                Line(Speaker.Me, "21:09", "LOGGING OFF.", "joy"),
            };
            EditorUtility.SetDirty(dialogue);

            // 챕터 0 의뢰 0-1·0-2(W-005): Docs/Planning/03-01-level-design.md v0.3.0 §1-1·§1-2, 지시서 W-005
            var ch01 = LoadOrCreate<LevelData>(DataDir + "/Ch0_1.asset");
            BuildCh01(ch01);
            var ch02 = LoadOrCreate<LevelData>(DataDir + "/Ch0_2.asset");
            BuildCh02(ch02);
            EditorUtility.SetDirty(ch01);
            EditorUtility.SetDirty(ch02);

            // 새 챕터 0(W-007): 주인공 건물 침입자 저지. 03-01 v0.4.1 §1-C-1·§1-C-2, 07-01 §5
            var home1 = LoadOrCreate<LevelData>(DataDir + "/Ch0Home_1.asset");
            BuildHome1(home1);
            var home2 = LoadOrCreate<LevelData>(DataDir + "/Ch0Home_2.asset");
            BuildHome2(home2);
            EditorUtility.SetDirty(home1);
            EditorUtility.SetDirty(home2);

            // 챕터 1 의뢰 1-1·1-2(W-014): 03-01 v0.6.0 §2-1·§2-2. 방 그림은 구시가지 방(scenes128/ch1_old1·ch1_old2, W-015)
            var old1 = LoadOrCreate<LevelData>(DataDir + "/Ch1Old_1.asset");
            BuildOld1(old1);
            var old2 = LoadOrCreate<LevelData>(DataDir + "/Ch1Old_2.asset");
            BuildOld2(old2, camBasic);
            EditorUtility.SetDirty(old1);
            EditorUtility.SetDirty(old2);
            // 챕터 1 의뢰 1-3·1-4(W-029, D-111): 03-01 v0.7.0 §2-5·§2-6. 방 그림은 기존 구시가지 방 재사용(scenes128/ch1_old3·ch1_old4)
            var old3 = LoadOrCreate<LevelData>(DataDir + "/Ch1Old_3.asset");
            BuildOld3(old3);
            var old4 = LoadOrCreate<LevelData>(DataDir + "/Ch1Old_4.asset");
            BuildOld4(old4, camBasic);
            EditorUtility.SetDirty(old3);
            EditorUtility.SetDirty(old4);
            // 챕터 1 새 1-4 OLD TOWN F·새 1-5 OLD TOWN G(W-036, D-125): 03-01 §2-8. 방 그림은 기존 구시가지 방 재사용(scenes128/ch1_old5·ch1_old6). 지금 old4(OLD TOWN E)는 목록에서 1-6이 된다
            var old5 = LoadOrCreate<LevelData>(DataDir + "/Ch1Old_5.asset");
            BuildOld5(old5);
            var old6 = LoadOrCreate<LevelData>(DataDir + "/Ch1Old_6.asset");
            BuildOld6(old6, camBasic);
            EditorUtility.SetDirty(old5);
            EditorUtility.SetDirty(old6);
            // 챕터 2 시험 맵 TEST_2-0(W-035, D-112·D-126 G2 점검용): 시간 위상 어긋남 시제품. 방 그림은 기존 구시가지 방 재사용(scenes128/ch2_test)
            var test20 = LoadOrCreate<LevelData>(DataDir + "/Ch2Test_0.asset");
            BuildTest20(test20);
            EditorUtility.SetDirty(test20);

            // 챕터 2 정식 의뢰 2-1~2-3(W-038, D-112·D-126): 03-01 §3. 방 그림은 공장 방(scenes128/ch2_fac1~3, W-039)
            var fac1 = LoadOrCreate<LevelData>(DataDir + "/Ch2Fac_1.asset");
            BuildFac1(fac1);
            var fac2 = LoadOrCreate<LevelData>(DataDir + "/Ch2Fac_2.asset");
            BuildFac2(fac2, camBasic);
            var fac3 = LoadOrCreate<LevelData>(DataDir + "/Ch2Fac_3.asset");
            BuildFac3(fac3);
            EditorUtility.SetDirty(fac1);
            EditorUtility.SetDirty(fac2);
            EditorUtility.SetDirty(fac3);

            // 바깥 환경 기본값(W-011, D-103 ⑤ [제안]): 챕터 0 두 의뢰 밤·비, 1-1 밤·흐림, 1-2 밤·안개. 그 밖의 에셋(샌드박스·테스트 의뢰)은 기본값 밤·맑음.
            SetEnv(level, TimeOfDay.Night, Weather.Clear);
            SetEnv(ch01, TimeOfDay.Night, Weather.Clear);
            SetEnv(ch02, TimeOfDay.Night, Weather.Clear);
            SetEnv(home1, TimeOfDay.Night, Weather.Rain);
            SetEnv(home2, TimeOfDay.Night, Weather.Rain);
            SetEnv(old1, TimeOfDay.Night, Weather.Cloudy);
            SetEnv(old2, TimeOfDay.Night, Weather.Fog);
            SetEnv(old3, TimeOfDay.Night, Weather.Cloudy);   // W-029 [제안] 1-3 밤·흐림
            SetEnv(old4, TimeOfDay.Night, Weather.Rain);     // W-029 [제안] OLD TOWN E(지금 1-6) 밤·비
            SetEnv(old5, TimeOfDay.Night, Weather.Cloudy);   // W-036 [제안] 1-4 OLD TOWN F 밤·흐림
            SetEnv(old6, TimeOfDay.Night, Weather.Fog);      // W-036 [제안] 1-5 OLD TOWN G 밤·안개
            SetEnv(test20, TimeOfDay.Night, Weather.Clear);  // W-035 [제안] TEST_2-0 밤·맑음
            SetEnv(fac1, TimeOfDay.Night, Weather.Cloudy);   // W-038 [제안] 2-1 밤·흐림
            SetEnv(fac2, TimeOfDay.Night, Weather.Clear);    // W-038 [제안] 2-2 밤·맑음
            SetEnv(fac3, TimeOfDay.Night, Weather.Rain);     // W-038 [제안] 2-3 밤·비
            EnvironmentSetup.EnsureAsset();

            // 07-01 §5 대사: 화자 SYSTEM·???는 의뢰인 자리(왼쪽 말풍선, speakerName), ME는 오른쪽. 시각 글자는 [임시].
            var hdlg1 = LoadOrCreate<DialogueData>(DataDir + "/Ch0Home_1Dialogue.asset");
            hdlg1.briefing = new[]
            {
                Say("SYSTEM", "23:41", "1F INTRUDER DETECTED.", "surprise"),
                Line(Speaker.Me, "23:41", "NOT TONIGHT.", "anger"),
            };
            hdlg1.ending = new[]
            {
                Say("???", "23:49", "HEY! WHO LOCKED THIS?!", "neutral"),
                Line(Speaker.Me, "23:49", "STAY THERE.", "neutral"),
                Say("???", "23:49", "WHO TALKS THROUGH A LOCKED DOOR?!", "anger"),   // W-049
            };
            EditorUtility.SetDirty(hdlg1);
            var hdlg2 = LoadOrCreate<DialogueData>(DataDir + "/Ch0Home_2Dialogue.asset");
            hdlg2.briefing = new[]
            {
                Say("???", "23:52", "I'M OUT. WHOEVER YOU ARE, I'M NOT STAYING.", "neutral"),
                Line(Speaker.Me, "23:52", "WE'LL SEE.", "disgust"),
            };
            hdlg2.ending = new[]
            {
                Say("???", "00:01", "OK OK. I JUST NEEDED A PLACE TO HIDE.", "sadness"),
                Line(Speaker.Me, "00:01", "YOUR FACE IS ON MY CAMERA.", "neutral"),
                Line(Speaker.Me, "00:02", "WORK FOR ME. I ERASE YOUR RECORD.", "neutral"),
                Say("???", "00:02", "... DEAL.", "joy"),
            };
            EditorUtility.SetDirty(hdlg2);

            // 챕터 1 의뢰 1-1·1-2 대사(W-028): [임시] 07-01 v0.2.0 §6 초안 — 사용자 원고로 교체. 화자 X·DASH는 의뢰인 자리(왼쪽 말풍선, speakerName), ME는 오른쪽
            var odlg1 = LoadOrCreate<DialogueData>(DataDir + "/Ch1Old_1Dialogue.asset");
            odlg1.briefing = new[]
            {
                Say("X", "22:10", "TWO BUILDINGS. NO BRIDGE. GET YOUR RUNNER ACROSS.", "neutral"),
                Say("DASH", "22:10", "NO BRIDGE? FINE. I'LL JUMP.", "neutral"),
                Line(Speaker.Me, "22:11", "DON'T. THE ROOM IS THE BRIDGE.", "disgust"),
            };
            odlg1.ending = new[]
            {
                Say("DASH", "22:24", "I JUST CROSSED AN ALLEY INSIDE A ROOM.", "joy"),
                Say("X", "22:24", "PAYMENT SENT.", "neutral"),
                Line(Speaker.Me, "22:25", "ONE LINE OFF YOUR RECORD.", "neutral"),
                Say("DASH", "22:25", "ONLY ONE?", "surprise"),   // W-049
            };
            EditorUtility.SetDirty(odlg1);
            var odlg2 = LoadOrCreate<DialogueData>(DataDir + "/Ch1Old_2Dialogue.asset");
            odlg2.briefing = new[]
            {
                Say("X", "23:02", "ROOFTOP PICKUP. 2F STAIRWELL HAS A CAMERA.", "neutral"),
                Say("DASH", "23:02", "CAMERAS LOVE ME.", "joy"),
                Line(Speaker.Me, "23:03", "THIS ONE DOESN'T. WALK WHEN I SAY.", "anger"),
            };
            odlg2.ending = new[]
            {
                Say("DASH", "23:19", "ROOF! NICE VIEW. NEXT TIME, A REAL DOOR?", "joy"),
                Say("X", "23:19", "CLEAN. NEXT JOB WITHIN THE HOUR.", "neutral"),   // W-038: 07-01 v0.5.0 교체(1-6 종료와 겹침 해소)
                Line(Speaker.Me, "23:20", "CATCH YOUR BREATH.", "neutral"),
            };
            EditorUtility.SetDirty(odlg2);
            // 1-3·1-4 대사(W-029): [임시] 07-01 v0.3.0 §6 초안
            var odlg3 = LoadOrCreate<DialogueData>(DataDir + "/Ch1Old_3Dialogue.asset");
            odlg3.briefing = new[]
            {
                Say("X", "00:05", "A LASER HALL BLOCKS THE WAY OUT.", "neutral"),
                Say("DASH", "00:05", "I CAN DUCK. PROBABLY.", "joy"),
                Line(Speaker.Me, "00:06", "NO NEED. I'LL SWAP THE ROOMS.", "neutral"),
            };
            odlg3.ending = new[]
            {
                Say("DASH", "00:18", "THE WHOLE SHOP MOVED. WITH ME IN IT.", "surprise"),
                Say("X", "00:18", "DONE. THE NIGHT ISN'T OVER.", "neutral"),   // W-038: 07-01 v0.5.0 교체
                Line(Speaker.Me, "00:19", "KEEP YOUR SHOES ON.", "neutral"),
            };
            EditorUtility.SetDirty(odlg3);
            var odlg4 = LoadOrCreate<DialogueData>(DataDir + "/Ch1Old_4Dialogue.asset");
            odlg4.briefing = new[]
            {
                Say("X", "01:40", "LAST JOB. ROOFTOP. LASERS, A CAMERA, NO STAIRS TO THE EXIT.", "neutral"),   // W-038: 07-01 v0.5.0 시각 01:40~
                Say("DASH", "01:40", "SO... EVERYTHING AT ONCE.", "fear"),
                Line(Speaker.Me, "01:41", "ONE THING AT A TIME. LISTEN.", "neutral"),
            };
            odlg4.ending = new[]
            {
                Say("DASH", "01:57", "WE DID IT! THE WHOLE TOWN, ROOM BY ROOM.", "joy"),
                Say("DASH", "01:57", "AND STILL NOT ONE REAL DOOR.", "joy"),   // W-049
                Say("X", "01:58", "CLEAN WORK. NEXT TIME, OUT OF TOWN.", "neutral"),   // W-038: 07-01 v0.5.0 교체(챕터 2 예고)
                Line(Speaker.Me, "01:58", "LOGGING OFF.", "neutral"),
            };
            EditorUtility.SetDirty(odlg4);
            // 1-4(OLD TOWN F)·1-5(OLD TOWN G) 대화(W-036): 없음(빈 대화) — 대사 원고가 오면 이 에셋에 줄을 추가한다
            var odlg5 = LoadOrCreate<DialogueData>(DataDir + "/Ch1Old_5Dialogue.asset");
            odlg5.briefing = new[]
            {
                Say("X", "00:32", "ROOFTOP AGAIN. TWO GAPS THIS TIME.", "neutral"),
                Say("DASH", "00:32", "TWO RIDES? I'M STARTING TO LIKE THIS.", "joy"),
                Line(Speaker.Me, "00:33", "ONE ROOM PER PUSH. WAIT FOR MY SIGNAL.", "neutral"),
            };
            odlg5.ending = new[]
            {
                Say("DASH", "00:51", "ACROSS, THEN UP. YOU'RE GETTING FAST.", "joy"),
                Say("X", "00:51", "PAID.", "neutral"),
                Line(Speaker.Me, "00:52", "TWO LINES DOWN.", "neutral"),   // W-049
            };
            EditorUtility.SetDirty(odlg5);
            var odlg6 = LoadOrCreate<DialogueData>(DataDir + "/Ch1Old_6Dialogue.asset");
            odlg6.briefing = new[]
            {
                Say("X", "01:05", "FOUR FLOORS. A CAMERA WATCHES THE ONLY STAIRS.", "neutral"),
                Say("DASH", "01:05", "I'LL JUST WAVE AT IT.", "joy"),
                Line(Speaker.Me, "01:06", "NO. I'LL MOVE IT OUT OF YOUR WAY.", "disgust"),
            };
            odlg6.ending = new[]
            {
                Say("DASH", "01:24", "THE CAMERA WAS THERE. THEN IT WASN'T.", "surprise"),
                Say("DASH", "01:24", "WHO ARE YOU, ANYWAY?", "neutral"),   // W-049
                Line(Speaker.Me, "01:25", "THE ONE HOLDING YOUR RECORD.", "neutral"),
                Say("X", "01:25", "NICE. ONE LAST JOB TONIGHT.", "neutral"),
            };
            EditorUtility.SetDirty(odlg6);
            // 챕터 2 대화(W-038): 07-01 v0.5.0 §6-2 [생성] 초안
            var fdlg1 = LoadOrCreate<DialogueData>(DataDir + "/Ch2Fac_1Dialogue.asset");
            fdlg1.briefing = new[]
            {
                Say("X", "22:00", "EMPTY FACTORY. THE MACHINES STILL RUN ON A TIMER.", "neutral"),
                Say("DASH", "22:00", "NO GUARDS? EASY MONEY.", "joy"),
                Line(Speaker.Me, "22:01", "THE PRESSES DON'T CARE WHO YOU ARE. WATCH THE BEAT.", "neutral"),
            };
            fdlg1.ending = new[]
            {
                Say("DASH", "22:14", "THAT WAS LIKE DANCING WITH A HAMMER.", "joy"),
                Line(Speaker.Me, "22:14", "THREE LINES DOWN.", "neutral"),   // W-049
                Say("DASH", "22:15", "HOW MANY ARE LEFT?", "neutral"),
                Line(Speaker.Me, "22:15", "ENOUGH.", "neutral"),
            };
            EditorUtility.SetDirty(fdlg1);
            var fdlg2 = LoadOrCreate<DialogueData>(DataDir + "/Ch2Fac_2Dialogue.asset");
            fdlg2.briefing = new[]
            {
                Say("X", "22:40", "PACKING LINE. TWO PRESSES. ONE CAMERA.", "neutral"),
                Say("DASH", "22:40", "TWO HAMMERS. GREAT.", "fear"),
                Line(Speaker.Me, "22:41", "DON'T STOP IN FRONT OF THE CAMERA.", "anger"),
            };
            fdlg2.ending = new[]
            {
                Say("DASH", "22:58", "WAIT. THESE CRATES HAVE A CITY PATROL MARK.", "surprise"),
                Line(Speaker.Me, "22:58", "NOT OUR JOB. MOVE.", "neutral"),
                Say("X", "22:59", "GOOD. ONE MORE IN THIS FACTORY.", "neutral"),
            };
            EditorUtility.SetDirty(fdlg2);
            var fdlg3 = LoadOrCreate<DialogueData>(DataDir + "/Ch2Fac_3Dialogue.asset");
            fdlg3.briefing = new[]
            {
                Say("X", "23:20", "CONTROL ROOM. TOP FLOOR. NO STAIRS LEAD THERE.", "neutral"),
                Say("DASH", "23:20", "LET ME GUESS. YOU MOVE A ROOM.", "joy"),
                Line(Speaker.Me, "23:21", "THE BEAT FIRST. THEN THE ROOM.", "neutral"),
            };
            fdlg3.ending = new[]
            {
                Say("DASH", "23:39", "THOSE PATROL CRATES... SAME SQUAD THAT CHASED ME.", "fear"),
                Line(Speaker.Me, "23:39", "THEN WE KEEP LOOKING.", "neutral"),
                Say("X", "23:40", "CLEAN. I'LL CALL.", "neutral"),
            };
            EditorUtility.SetDirty(fdlg3);

            // (이전) 챕터 0 SANDBOX 임시 대사 [임시] — 의뢰 목록에서는 빠졌고 에셋만 남김. 화자 SANDBOX는 의뢰인 자리(Speaker.Client)
            var dlg01 = LoadOrCreate<DialogueData>(DataDir + "/Ch0_1Dialogue.asset");
            dlg01.briefing = new[]
            {
                Line(Speaker.Client, "20:58", "TRAINING SIM A LOADED.", "neutral"),
                Line(Speaker.Me, "20:58", "LET'S GET THIS OVER WITH.", "neutral"),
            };
            dlg01.ending = new[]
            {
                Line(Speaker.Client, "21:00", "SIM A CLEAR.", "neutral"),
                Line(Speaker.Me, "21:00", "NEXT.", "neutral"),
            };
            EditorUtility.SetDirty(dlg01);
            var dlg02 = LoadOrCreate<DialogueData>(DataDir + "/Ch0_2Dialogue.asset");
            dlg02.briefing = new[]
            {
                Line(Speaker.Client, "21:01", "SIM B. STAIRS AND RELAYS.", "neutral"),
                Line(Speaker.Me, "21:01", "FINE.", "disgust"),
            };
            dlg02.ending = new[]
            {
                Line(Speaker.Client, "21:04", "SIM B CLEAR.", "neutral"),
                Line(Speaker.Me, "21:04", "OK.", "joy"),
            };
            EditorUtility.SetDirty(dlg02);

            // 의뢰 목록(W-007): 새 챕터 0(HOME BLDG)의 0-1·0-2만. W-005 SANDBOX 의뢰 2개와 W-004 테스트 의뢰(TestRequest)는 목록에서 빼고 에셋은 남긴다.
            // 제목·의뢰인·요약·미션 이름은 03-01 v0.4.1 §1-C-1·§1-C-2 표 그대로(요약은 [임시]).
            var requests = LoadOrCreate<RequestList>(DataDir + "/RequestList.asset");
            requests.entries = new[]
            {
                new RequestEntry
                {
                    id = "home-0-1", chapter = 0, client = "HOME SYSTEM", title = "NIGHT INTRUDER", summary = "SOMEONE BROKE IN.\nKEEP THEM AWAY FROM YOU.\nTRAP THEM BELOW.",
                    missionNames = new[] { "TRAP THE INTRUDER" },
                    level = home1,
                    dialogue = hdlg1,
                },
                new RequestEntry
                {
                    id = "home-0-2", chapter = 0, client = "HOME SYSTEM", title = "LOCKDOWN", summary = "THEY GOT OUT.\nSTOP THE ELEVATOR.\nSEND THEM TO THE BOILER ROOM.",
                    missionNames = new[] { "TRAP THE INTRUDER" },
                    level = home2,
                    dialogue = hdlg2,
                },
                // 챕터 1(W-014): 의뢰인·요약은 [임시]. 미션 이름은 03-01 §2-1·§2-2. 대사는 W-028의 [임시] 초안(07-01 v0.2.0 §6)
                new RequestEntry
                {
                    id = "old-1-1", chapter = 1, client = "???", title = "ACROSS THE ALLEY", summary = "GUIDE THEM ACROSS THE ALLEY.\nSLIDE A ROOM TO BRIDGE THE GAP.\nREACH THE EXIT.",
                    missionNames = new[] { "REACH THE EXIT", "B-SIDE TERMINAL", "TRACE <= 55" },
                    level = old1,
                    dialogue = odlg1,
                },
                new RequestEntry
                {
                    id = "old-1-2", chapter = 1, client = "???", title = "UP AND OVER", summary = "THEY ARE STUCK ON THE GROUND FLOOR.\nLIFT, CARRY AND PULL THE ROOMS.\nREACH THE ROOF.",
                    missionNames = new[] { "REACH THE ROOF", "WAREHOUSE TERMINAL", "UNSEEN" },   // W-016(D-099): 솜씨 미션 TRACE <= 60 -> UNSEEN
                    level = old2,
                    dialogue = odlg2,
                },
                // 1-3과 (예전) 1-4(W-029, D-111): 제목·요약은 [임시]
                new RequestEntry
                {
                    id = "old-1-3", chapter = 1, client = "???", title = "SWAP SHOP", summary = "A LASER HALL BLOCKS THE WAY.\nSWAP THE SHOP PAST IT.\nREACH THE EXIT.",
                    missionNames = new[] { "REACH THE EXIT", "UPSTAIRS TERMINAL", "TRACE <= 80" },
                    level = old3,
                    dialogue = odlg3,
                },
                // 새 1-4·1-5(W-036, D-125): id는 OLD TOWN F·G 이름을 따른다. 번호는 목록 순서에서 나온다. 제목·요약은 [임시], 대화는 빈 대화
                new RequestEntry
                {
                    id = "old-1-f", chapter = 1, client = "???", title = "DOUBLE HAUL", summary = "CARRY THEM ACROSS THE ALLEY.\nLET GO, AIM AGAIN, CARRY AGAIN.\nREACH THE ROOF.",
                    missionNames = new[] { "REACH THE ROOF", "B CELLAR TERMINAL", "TRACE <= 60" },
                    level = old5,
                    dialogue = odlg5,
                },
                new RequestEntry
                {
                    id = "old-1-g", chapter = 1, client = "???", title = "CAMERA SHUFFLE", summary = "A CAMERA GUARDS THE WAY UP.\nSWAP IT OUT OF THE WAY.\nREACH THE ROOF.",
                    missionNames = new[] { "REACH THE ROOF", "STORE TERMINAL", "UNSEEN" },
                    level = old6,
                    dialogue = odlg6,
                },
                // 1-6(예전 1-4 OLD TOWN E): id를 그대로 두어 저장 키가 이어진다. 의뢰·대사·미션은 그대로이고 목록 순서만 바뀜
                new RequestEntry
                {
                    id = "old-1-4", chapter = 1, client = "???", title = "ROOFTOP RUN", summary = "LASERS, A CAMERA, A ROOF.\nONE THING AT A TIME.\nREACH THE ROOF.",
                    missionNames = new[] { "REACH THE ROOF", "CELLAR TERMINAL", "UNSEEN" },
                    level = old4,
                    dialogue = odlg4,
                },
                // 챕터 2 시험 의뢰(W-035): 정식 의뢰와 구분되는 TEST_2-0. 잠금 없음, 대화 없음, 미션은 REACH THE EXIT 하나. 제목·요약은 [임시]
                new RequestEntry
                {
                    id = "test-2-0", chapter = 2, client = "???", title = "PRESS TEST", summary = "TEST MAP FOR BEAT SHIFTING.\nHOLD SHIFT + 1 2 3 ON A CLOCK ROOM.\nREACH THE EXIT.",
                    missionNames = new[] { "REACH THE EXIT" },
                    level = test20,
                    dialogue = null,
                    testEntry = true,
                },
                // 챕터 2 정식 의뢰(W-038): 제목 [생성] ON THE BEAT / OFFBEAT / CONTROL ROOM, 요약 [제안]. TEST_2-0(위)은 팝업 목록에서 빠지고 항목만 남는다
                new RequestEntry
                {
                    id = "fac-2-1", chapter = 2, client = "???", title = "ON THE BEAT", summary = "THE MACHINES RUN ON A BEAT.\nSHIFT SHOWS WHAT COMES NEXT.\nREACH THE EXIT.",
                    missionNames = new[] { "REACH THE EXIT", "STOCK TERMINAL", "TRACE <= " + Fac1Trace },
                    level = fac1,
                    dialogue = fdlg1,
                },
                new RequestEntry
                {
                    id = "fac-2-2", chapter = 2, client = "???", title = "OFFBEAT", summary = "TWO PRESSES MOVE AS ONE.\nPULL THEM OUT OF STEP.\nREACH THE EXIT.",
                    missionNames = new[] { "REACH THE EXIT", "PACKING TERMINAL", "UNSEEN" },
                    level = fac2,
                    dialogue = fdlg2,
                },
                new RequestEntry
                {
                    id = "fac-2-3", chapter = 2, client = "???", title = "CONTROL ROOM", summary = "THE CONTROL ROOM HAS NO STAIRS.\nLIFT A ROOM. KEEP THE BEAT.\nREACH CONTROL.",
                    missionNames = new[] { "REACH CONTROL", "STOCK TERMINAL", "TRACE <= " + Fac3Trace },
                    level = fac3,
                    dialogue = fdlg3,
                },
            };
            EditorUtility.SetDirty(requests);

            // 화면용 아트 모음: 반입한 PNG + manifest의 9-slice 테두리·글자 좌표
            var art = LoadOrCreate<ArtSet>(DataDir + "/ArtSet.asset");
            var list = new System.Collections.Generic.List<ArtEntry>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtImporter.ArtRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string rel = path.Substring(ArtImporter.ArtRoot.Length + 1);
                int[] b = ArtManifest.NineSlice(rel) ?? new int[4];
                var entry = new ArtEntry
                {
                    key = rel.Substring(0, rel.Length - 4),
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                    nl = b[0], nt = b[1], nr = b[2], nb = b[3],
                };
                if (ArtManifest.Anim(rel, out var am))   // W-009: 애니메이션 시트 메타
                {
                    entry.frameW = am.frameW; entry.frameH = am.frameH; entry.frames = am.frames;
                    entry.fps = am.fps; entry.holds = am.holds; entry.loop = am.loop;
                }
                list.Add(entry);
            }
            art.entries = list.ToArray();
            art.glyphs = ArtManifest.Glyphs().ToArray();
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();

            // W-032: 씬이 이미 있고 GameView가 있으면 새로 만들지 않고 기존 씬의 참조만 갱신한다(새로 만들면 fileID가 매번 바뀌어
            // Main.unity가 무관하게 바뀌고, 되돌릴 때 「modified externally」 창이 뜬다). 바뀐 것이 없으면 저장하지 않는다.
            Scene scene = default;
            GameView existing = null;
            if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                existing = Object.FindAnyObjectByType<GameView>();
            }
            if (existing == null)
            {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene가 메모리의 미사용 에셋을 해제할 수 있어 다시 불러온다.
            settings = AssetDatabase.LoadAssetAtPath<GameSettings>(DataDir + "/GameSettings.asset");
            requests = AssetDatabase.LoadAssetAtPath<RequestList>(DataDir + "/RequestList.asset");
            art = AssetDatabase.LoadAssetAtPath<ArtSet>(DataDir + "/ArtSet.asset");

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
            cam.orthographic = true;

            var viewGo = new GameObject("GameView");
            var view = viewGo.AddComponent<GameView>();
            var so = new SerializedObject(view);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("requests").objectReferenceValue = requests;
            so.FindProperty("art").objectReferenceValue = art;
            so.FindProperty("glitch").objectReferenceValue = GlitchSetup.EnsureAsset();   // W-013
            so.FindProperty("environment").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnvironmentSet>(EnvironmentSetup.AssetPath);   // W-011
            so.FindProperty("sfxSet").objectReferenceValue = AudioSetup.EnsureSfxSet();   // W-020: 효과음 목록(빈 클립)
            so.FindProperty("bgmSet").objectReferenceValue = AudioSetup.EnsureBgmSet();   // W-020: BGM 곡 슬롯 8개(빈 클립)
            so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
            }
            else
            {
                settings = AssetDatabase.LoadAssetAtPath<GameSettings>(DataDir + "/GameSettings.asset");
                requests = AssetDatabase.LoadAssetAtPath<RequestList>(DataDir + "/RequestList.asset");
                art = AssetDatabase.LoadAssetAtPath<ArtSet>(DataDir + "/ArtSet.asset");
                var so2 = new SerializedObject(existing);
                so2.FindProperty("settings").objectReferenceValue = settings;
                so2.FindProperty("requests").objectReferenceValue = requests;
                so2.FindProperty("art").objectReferenceValue = art;
                so2.FindProperty("glitch").objectReferenceValue = GlitchSetup.EnsureAsset();
                so2.FindProperty("environment").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnvironmentSet>(EnvironmentSetup.AssetPath);
                so2.FindProperty("sfxSet").objectReferenceValue = AudioSetup.EnsureSfxSet();
                so2.FindProperty("bgmSet").objectReferenceValue = AudioSetup.EnsureBgmSet();
                so2.ApplyModifiedPropertiesWithoutUndo();   // 값이 같으면 씬이 더러워지지 않는다
            }
            if (scene.isDirty) EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Mvp1Setup] 완료: " + ScenePath);
        }

        static ObjectDef Obj(ObjectKind kind, int floor, int col, float x, float drawX, bool realityOn, bool blueprintOn, int mission = -1) =>
            new ObjectDef { kind = kind, floor = floor, col = col, x = x, drawX = drawX, drawSprite = true, realityOn = realityOn, blueprintOn = blueprintOn, mission = mission, detectTrackingOverride = -1f };

        /// <summary>의뢰 0-1 SANDBOX BLDG A: 1층 x 4섹터, 문 D1·D2(조준 점프 연습). 배경 scenes/ch0_1_base(640x152).</summary>
        static void BuildCh01(LevelData l)
        {
            l.title = "SANDBOX BLDG A";
            l.buildingName = "SANDBOX BLDG A";
            l.floors = 1; l.cols = 4;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 6; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_1_base";
            l.sectors = new SectorDef[0];
            l.objects = new[]
            {
                Obj(ObjectKind.Door, 0, 0, 154, 154, false, true),                       // 문 D1 (0,0) 오른쪽 경계, 닫힘 -> 열림
                Obj(ObjectKind.Terminal, 0, 1, 80, 80, false, false, 1),                 // 단말 A (0,1)
                Obj(ObjectKind.Terminal, 0, 2, 80, 80, false, false, 2),                 // 단말 B (0,2)
                Obj(ObjectKind.Door, 0, 2, 154, 154, false, true),                       // 문 D2 (0,2) 오른쪽 경계, 닫힘 -> 열림
                Obj(ObjectKind.Exit, 0, 3, 138, 124, false, false, 0),                   // 출구 (0,3): 표지 x 124, 초록 문 x 130~146, 판정 x 138
            };
            l.startFloor = 0; l.startCol = 0; l.startX = 30; l.startFacingRight = true;
            l.tutorial = true;
            l.tutorialSteps = new[]
            {
                new TutorialStep { cond = TutorialCond.Start, key = "SHIFT", text = "THE DOOR IS LOCKED. HOLD SHIFT TO SHIFT IT." },
                new TutorialStep { cond = TutorialCond.ShiftReleased, text = "TRACE RISES WHILE YOU HOLD. KEEP IT SHORT." },
                new TutorialStep { cond = TutorialCond.TargetWaiting, floor = 0, col = 2, key = "\u2192", text = "PRESS \u2192 TO JUMP YOUR AIM TO THE NEXT ROOM." },
                new TutorialStep { cond = TutorialCond.AimAt, floor = 0, col = 2, key = "SHIFT", text = "NOW HOLD SHIFT." },
            };
            l.starTwoHint = "";
        }

        /// <summary>의뢰 0-2 SANDBOX BLDG B: 2층 x 4섹터, 계단 S1·섹터 전체형 차단막·중계기. 배경 scenes/ch0_2_base(640x248).</summary>
        static void BuildCh02(LevelData l)
        {
            l.title = "SANDBOX BLDG B";
            l.buildingName = "SANDBOX BLDG B";
            l.floors = 2; l.cols = 4;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 6; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_2_base";
            l.sectors = new[]
            {
                // 차단막 B1: (0,2) 섹터 전체형, 켜짐 -> 꺼짐
                new SectorDef { floor = 0, col = 2, target = SectorTarget.Whole, realityBlocked = true, blueprintBlocked = false },
            };
            ObjectDef s1 = Obj(ObjectKind.Stairs, 1, 3, 50, 50, false, true);          // 계단 셔터 S1 (1,3): 닫힘 -> 열림
            s1.hasLink = true; s1.linkFloor = 0; s1.linkCol = 3; s1.linkX = 50; s1.linkFacingRight = false;   // (0,3) x 50, 도착 후 왼쪽
            l.objects = new[]
            {
                Obj(ObjectKind.Terminal, 1, 1, 60, 60, false, false, 1),                // 단말 A (1,1)
                s1,
                Obj(ObjectKind.Terminal, 1, 3, 118, 118, false, false, 2),              // 단말 B (1,3): S1 너머 끝
                Obj(ObjectKind.Stairs, 0, 3, 50, 50, true, true),                       // 도착 계단 (0,3): 열림 -> 열림, 연결 없음(조작 불가)
                Obj(ObjectKind.Relay, 0, 1, 70, 70, false, false),                      // 중계기 (0,1)
                Obj(ObjectKind.Exit, 0, 0, 138, 124, false, false, 0),                  // 출구 (0,0)
            };
            l.startFloor = 1; l.startCol = 0; l.startX = 30; l.startFacingRight = true;
            l.tutorial = true;
            l.tutorialSteps = new[]
            {
                new TutorialStep { cond = TutorialCond.Start, text = "STAIRS WORK ONLY WHEN THE SHUTTER IS OPEN." },
                new TutorialStep { cond = TutorialCond.TargetWaiting, floor = 0, col = -1, text = "SOME ROOMS SHIFT AS A WHOLE. AIM AT THE ROOM." },
                new TutorialStep { cond = TutorialCond.RelayUsed, text = "RELAYS LOWER YOUR TRACE." },
            };
            l.starTwoHint = "TERMINAL B WAS PAST THE STAIRS. TRY AGAIN?";
        }

        /// <summary>HOME BLDG 0-1: 3층(2F·1F·B1) x 4열, 640x344. 배치는 03-01 §1-C-1 표.</summary>
        static void BuildHome1(LevelData l)
        {
            l.title = "NIGHT INTRUDER";
            l.buildingName = "HOME BLDG";
            l.floors = 3; l.cols = 4;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home1_base";       // 방 레이어 배치가 없을 때만 쓰는 예전 한 장 그림
            SetLayout(l, "scenes128/ch0_home1/layout");
            l.sectors = new SectorDef[0];
            ObjectDef stairs = Obj(ObjectKind.Stairs, 1, 3, 60, 40, true, false);        // 계단 (1,3): 열림 -> 닫힘
            stairs.hasLink = true; stairs.linkFloor = 0; stairs.linkCol = 3; stairs.linkX = 60; stairs.linkFacingRight = false;   // 2F (0,3) x 60, 도착 후 왼쪽
            ObjectDef hatch = Obj(ObjectKind.Hatch, 1, 2, 80, 54, false, true);          // 바닥 해치 (1,2): 닫힘 -> 열림. drawX 54(W-012: 복도 쓰레기통 x 94~106과 겹치지 않게 60에서 옮김, 규칙 x는 그대로)
            hatch.hasLink = true; hatch.linkFloor = 2; hatch.linkCol = 2; hatch.linkX = 80; hatch.linkFacingRight = true;         // B1 (2,2) x 80, 도착 후 오른쪽
            ObjectDef fail = Obj(ObjectKind.FailPoint, 0, 0, 142, 142, false, false);    // 실패 지점 (0,0) x 142(주인공 방 문 왼쪽 끝, W-010): 그림 없음(문 그림은 layout.objects)
            fail.drawSprite = false;
            l.objects = new[]
            {
                stairs,
                hatch,
                fail,
            };
            l.startFloor = 1; l.startCol = 1; l.startX = 30; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 1; l.startAimCol = 3;                // 처음 조준 = 계단 섹터(가까운 해치 섹터가 아님)
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.EnterSector, floor = 2, col = 2 },   // TRAP THE INTRUDER: B1 열 2 (W-008: 튜토리얼은 최종 미션 하나만)
            };
            l.tutorial = true;
            l.tutorialSteps = new[]
            {
                // W-033(D-122): 단계가 시작되면 시간이 멈추고(어둡게 + 네온 강조), 요구한 입력이 들어오면 이어진다. 문구·순서는 이 표만 고치면 된다
                TStep(TutorialCond.Start, TutorialInput.Enter, "", "AN INTRUDER. KEEP THEM AWAY FROM YOUR ROOM.", TutorialFocus.Scene, 0, 0),
                TStep(TutorialCond.Start, TutorialInput.Enter, "", "SHIFT RAISES YOUR TRACE. AT 100 YOU ARE FOUND.", TutorialFocus.Trace, 0, 0),
                // 계단 앞(절대 x 480, 계단 x 540 한 걸음 앞)에서 멈춤: Shift를 누르기 시작하면 이어진다
                TPast(TStep(TutorialCond.TargetPast, TutorialInput.Hold, "SHIFT", "HOLD SHIFT TO SEAL THE STAIRS.", TutorialFocus.Room, 1, 3), 1, 3, 0f, true),
                TPast(TStep(TutorialCond.TargetPast, TutorialInput.Enter, "", "IT ONLY HOLDS WHILE YOU PRESS.", TutorialFocus.Trace, 0, 0), 1, 3, 85f, true),
                // 오른쪽 끝에서 돌아와 계단을 다시 지난 뒤(x 495 이하, 왼쪽을 봄) 해치 조준
                TPast(TAim(TStep(TutorialCond.TargetPast, TutorialInput.Press, "\u2190", "PRESS \u2190 TO AIM AT THE HATCH.", TutorialFocus.Room, 1, 2), -1, 0, 1, 2), 1, 3, 15f, false),
                // 해치로 걸어오는 중(x 340 이상, 오른쪽을 봄)
                TPast(TStep(TutorialCond.TargetPast, TutorialInput.Hold, "SHIFT", "HOLD SHIFT TO DROP THEM.", TutorialFocus.Room, 1, 2), 1, 2, 20f, true),
            };
            l.starTwoHint = "";
        }

        /// <summary>HOME BLDG 0-2(W-031, D-119): 4줄(3F·2F·1F·B1) x 4열, 640x440. 배치는 03-01 §2-7 표. 2F 계단실 계단이 1F로 내려가고, 해치는 1F 복도 바닥에 있다. 해치를 놓치면 1F 엘리베이터가 3F로 올린다(두 번째 기회).</summary>
        static void BuildHome2(LevelData l)
        {
            l.title = "LOCKDOWN";
            l.buildingName = "HOME BLDG";
            l.floors = 4; l.cols = 4;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home2_base";       // 방 레이어 배치가 없을 때만 쓰는 예전 한 장 그림
            SetLayout(l, "scenes128/ch0_home2/layout");
            l.sectors = new[]
            {
                // 레이저 (1,2): 섹터 전체형, 켜짐 -> 꺼짐
                new SectorDef { floor = 1, col = 2, target = SectorTarget.Whole, realityBlocked = true, blueprintBlocked = false },
            };
            ObjectDef elevator = Obj(ObjectKind.Elevator, 1, 1, 80, 52, true, false);    // 2F 엘리베이터 (1,1): 작동 -> 멈춤
            elevator.hasLink = true; elevator.linkFloor = 0; elevator.linkCol = 1; elevator.linkX = 80; elevator.linkFacingRight = false;   // 3F (0,1) x80, 도착 후 왼쪽
            ObjectDef elevator1 = Obj(ObjectKind.Elevator, 2, 1, 80, 52, true, false);   // 1F 엘리베이터 (2,1): 작동 -> 멈춤. 해치를 놓치면 3F로 올림 [W-031]
            elevator1.hasLink = true; elevator1.linkFloor = 0; elevator1.linkCol = 1; elevator1.linkX = 80; elevator1.linkFacingRight = false;
            ObjectDef stairs = Obj(ObjectKind.Stairs, 1, 3, 60, 40, true, true);         // 계단 2F->1F (1,3) x60: 열림(계속). 기존 정비 통로 해치 자리 [W-031]
            stairs.hasLink = true; stairs.linkFloor = 2; stairs.linkCol = 3; stairs.linkX = 60; stairs.linkFacingRight = false;   // 1F (2,3) x60, 도착 후 왼쪽
            ObjectDef hatch = Obj(ObjectKind.Hatch, 2, 2, 80, 54, false, true);          // 바닥 해치 (2,2) 1F 복도: 닫힘 -> 열림. drawX 54(W-012, 0-1과 같은 이유)
            hatch.hasLink = true; hatch.linkFloor = 3; hatch.linkCol = 2; hatch.linkX = 80; hatch.linkFacingRight = true;          // B1 (3,2) 보일러실 x80, 도착 후 오른쪽
            ObjectDef fail = Obj(ObjectKind.FailPoint, 0, 0, 142, 142, false, false);    // 실패 지점 (0,0) x142(주인공 방 문 왼쪽 끝, W-010)
            fail.drawSprite = false;
            l.objects = new[]
            {
                elevator,
                elevator1,
                Obj(ObjectKind.Relay, 1, 2, 100, 92, false, false),                      // 중계기 (1,2) x100: 레이저 복도 안
                stairs,
                hatch,
                fail,
            };
            l.startFloor = 1; l.startCol = 0; l.startX = 30; l.startFacingRight = true;
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.EnterSector, floor = 3, col = 2 },   // TRAP THE INTRUDER: B1 열 2(보일러실) (W-008: 최종 미션 하나만, W-031: 줄 번호 3)
            };
            l.tutorial = true;
            l.tutorialSteps = new[]
            {
                // 엘리베이터(절대 x 240) 앞 x 190에서 멈춤 -> Shift
                TPast(TStep(TutorialCond.TargetPast, TutorialInput.Hold, "SHIFT", "THE ELEVATOR GOES UP TO YOU. HOLD SHIFT TO STOP IT.", TutorialFocus.Room, 1, 1), 1, 1, 30f, true),
                // 레이저 앞에서 기다릴 때: 방향키로 레이저 방(2F 열 2)에 조준
                TAim(TWait(TStep(TutorialCond.TargetWaiting, TutorialInput.Press, "\u2190 \u2191 \u2192 \u2193", "SOME ROOMS SHIFT AS A WHOLE. AIM AT THE ROOM.", TutorialFocus.Room, 1, 2), 1), 0, 0, 1, 2),
                TAlso(TStep(TutorialCond.RelayUsed, TutorialInput.Enter, "", "RELAYS LOWER YOUR TRACE.", TutorialFocus.Room, 1, 2), TutorialFocus.Trace, 0, 0),
                // 계단으로 1층에 내려오면: 해치(1F 열 2)에 아래 방향키로 조준, 해치 가까이 오면 Shift
                TAim(TFloor(TStep(TutorialCond.TargetOnFloor, TutorialInput.Press, "\u2193", "PRESS \u2193 TO AIM AT THE HATCH.", TutorialFocus.Room, 2, 2), 2), 0, 1, 2, 2),
                TPast(TStep(TutorialCond.TargetPast, TutorialInput.Hold, "SHIFT", "NOW HOLD SHIFT TO DROP THEM.", TutorialFocus.Room, 2, 2), 2, 2, 140f, false),
            };
            l.starTwoHint = "";
        }

        /// <summary>
        /// 챕터 1 의뢰 1-1 OLD TOWN A/B(W-031, D-119): 3층(3F·2F·1F) x 5열, 맵 800x344. 배치는 03-01 §2-7 표.
        /// 빈 칸 3개(열 2), 레일은 A 끝 방(3F, 열 1), 계단 4개(A 1F->2F, A 2F->3F, B 3F->2F 열림->닫힘, B 2F->1F). 튜토리얼이 아니므로 별·미션 3개·카운트다운이 있고 단계 안내만 켠다(showGuide).
        /// </summary>
        static void BuildOld1(LevelData l)
        {
            l.title = "ACROSS THE ALLEY";
            l.buildingName = "OLD TOWN A/B";
            l.floors = 3; l.cols = 5;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home1_base";       // 방 레이어 배치가 없을 때만 쓰는 한 장 그림(쓰이지 않음)
            SetLayout(l, "scenes128/ch1_old1/layout");
            l.trackingRateScale = 0.7f;                      // D-120: 챕터 1 추적도 상승 초당 14
            l.sectors = new[]
            {
                new SectorDef { floor = 0, col = 1, target = SectorTarget.Objects, rail = true },   // A 끝 방(레일)
            };
            l.emptyCells = new[]
            {
                new CellDef { floor = 0, col = 2 },   // 골목 위
                new CellDef { floor = 1, col = 2 },   // 골목
                new CellDef { floor = 2, col = 2 },   // 골목(철망)
            };
            ObjectDef stairsA1 = Obj(ObjectKind.Stairs, 2, 0, 80, 60, true, true);       // A 계단 1F->2F (2,0) x80: 열림(계속)
            stairsA1.hasLink = true; stairsA1.linkFloor = 1; stairsA1.linkCol = 0; stairsA1.linkX = 80; stairsA1.linkFacingRight = true;   // 2F (1,0) x80, 도착 후 오른쪽
            ObjectDef stairsA2 = Obj(ObjectKind.Stairs, 1, 1, 80, 60, true, true);       // A 계단 2F->3F (1,1) x80: 열림(계속)
            stairsA2.hasLink = true; stairsA2.linkFloor = 0; stairsA2.linkCol = 1; stairsA2.linkX = 80; stairsA2.linkFacingRight = true;   // 3F (0,1) 끝 방 x80, 도착 후 오른쪽
            ObjectDef stairsB3 = Obj(ObjectKind.Stairs, 0, 4, 50, 30, true, false);      // B 계단 3F->2F (0,4) x50: 열림 -> 닫힘
            stairsB3.hasLink = true; stairsB3.linkFloor = 1; stairsB3.linkCol = 4; stairsB3.linkX = 50; stairsB3.linkFacingRight = false;   // 2F (1,4) x50, 도착 후 왼쪽
            ObjectDef stairsB2 = Obj(ObjectKind.Stairs, 1, 3, 80, 60, true, true);       // B 계단 2F->1F (1,3) x80: 열림(계속)
            stairsB2.hasLink = true; stairsB2.linkFloor = 2; stairsB2.linkCol = 3; stairsB2.linkX = 80; stairsB2.linkFacingRight = false;   // 1F (2,3) 로비 x80, 도착 후 왼쪽
            l.objects = new[]
            {
                stairsA1,
                stairsA2,
                stairsB3,
                stairsB2,
                Obj(ObjectKind.Terminal, 0, 4, 130, 122, false, false, 1),               // 단말 (0,4) x130: 계단보다 오른쪽 -> Shift로 계단을 닫아야 닿음
                Obj(ObjectKind.Exit, 2, 3, 40, 26, false, false, 0),                     // 출구 (2,3) x40: 최종
            };
            l.startFloor = 2; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 2; l.startAimCol = 0;                // [제안] 처음 조준 = 시작 방(조작 방이 아님): 안내 1단계("AIM AT ONE")가 먼저 보이게
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },                         // REACH THE EXIT
                new MissionDef { kind = MissionKind.PassObject },                         // B-SIDE TERMINAL
                new MissionDef { kind = MissionKind.TraceLimit, limit = 55f },            // TRACE <= 55(W-031b: 의도 ★3 라이트 46.4)
            };
            l.tutorial = false;
            l.showGuide = true;
            l.tutorialSteps = new[]
            {
                TStep(TutorialCond.Start, TutorialInput.Enter, "", "EXTRA MISSIONS EARN STARS. THE EXIT ENDS THE JOB.", TutorialFocus.Mission, 0, 0),
                // 조력자가 3F(레일 방이 있는 층)에 올라오면: 방향키로 레일 방(3F 열 1)에 조준
                TAim(TFloor(TStep(TutorialCond.TargetOnFloor, TutorialInput.Press, "\u2190 \u2191 \u2192 \u2193", "SOME ROOMS SIT ON RAILS. AIM AT ONE.", TutorialFocus.Room, 0, 1), 0), 0, 0, 0, 1),
                TAlso(TAim(TAt(TStep(TutorialCond.AimAt, TutorialInput.ShiftArrow, "SHIFT \u2192", "HOLD SHIFT, THEN PRESS \u2192 TO SLIDE IT.", TutorialFocus.Room, 0, 1), 0, 1), 1, 0, 0, 1, false), TutorialFocus.Cell, 0, 2),
                TStep(TutorialCond.FirstPushed, TutorialInput.Enter, "", "LET GO ONLY AFTER SHE STEPS OUT.", TutorialFocus.Room, 0, 1),
                // 조력자가 B쪽(3F 열 3)에 건너가면: 맞바꿈 예고(옆 방은 A 셋방)
                TPast(TStep(TutorialCond.TargetPast, TutorialInput.Enter, "", "IF A ROOM IS NEXT TO IT, THEY SWAP PLACES.", TutorialFocus.Room, 0, 1), 0, 3, 20f, true),   // 밀린 레일 방 하나만 강조(옆 방은 화면 밖)
            };
            l.starTwoHint = "";
        }

        /// <summary>
        /// 챕터 1 의뢰 1-2 OLD TOWN C: 3층(3F·2F·1F) x 5열. 배치는 03-01 §2-2 표 그대로.
        /// 빈 칸 6개, 레일은 빈 점포(1F, 열 1)와 옥상 출구 방(3F, 열 1), 레이저 복도는 섹터 전체형. 별·미션 3개·카운트다운이 있고 안내는 없다.
        /// </summary>
        static void BuildOld2(LevelData l, SurveillanceType camBasic)
        {
            l.title = "UP AND OVER";
            l.buildingName = "OLD TOWN C";
            l.floors = 3; l.cols = 5;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home1_base";
            SetLayout(l, "scenes128/ch1_old2/layout");
            l.trackingRateScale = 0.6f;                      // D-120, W-031b: 1-2만 0.6(초당 12) — ★3 라이트 최대치를 80 근처로
            l.sectors = new[]
            {
                new SectorDef { floor = 2, col = 1, target = SectorTarget.Objects, rail = true },   // 빈 점포(레일)
                new SectorDef { floor = 0, col = 1, target = SectorTarget.Objects, rail = true },   // 옥상 출구 방(레일)
                new SectorDef { floor = 1, col = 3, target = SectorTarget.Whole, realityBlocked = true, blueprintBlocked = false },   // 레이저 복도: 켜짐 -> 꺼짐
            };
            l.emptyCells = new[]
            {
                new CellDef { floor = 0, col = 0 }, new CellDef { floor = 0, col = 2 },   // 3F
                new CellDef { floor = 1, col = 0 }, new CellDef { floor = 1, col = 1 },   // 2F(무너진 바닥)
                new CellDef { floor = 2, col = 2 }, new CellDef { floor = 2, col = 4 },   // 1F(골목)
            };
            ObjectDef stairs = Obj(ObjectKind.Stairs, 1, 4, 60, 40, true, true);         // 계단 2F->3F (1,4) x60: 열림(계속)
            stairs.hasLink = true; stairs.linkFloor = 0; stairs.linkCol = 4; stairs.linkX = 60; stairs.linkFacingRight = false;   // 3F (0,4) x60, 도착 후 왼쪽
            // 감시 카메라 (1,4): cam_basic(D-097). 그림 x 120(천장 쪽, 계단 x 60과 겹치지 않게)은 그림용 [제안], 판정은 섹터 전체. 켜짐 상태는 종류 정의가 정한다
            ObjectDef camera = Obj(ObjectKind.Camera, 1, 4, 120, 120, camBasic.realityOn, camBasic.blueprintOn);
            camera.surveillance = camBasic;
            l.objects = new[]
            {
                stairs,
                camera,
                Obj(ObjectKind.Relay, 1, 3, 100, 92, false, false),                      // 중계기 (1,3) x100: 레이저 복도 안
                Obj(ObjectKind.Terminal, 2, 3, 100, 92, false, false, 1),                // 단말 (2,3) x100: 창고
                Obj(ObjectKind.Exit, 0, 1, 140, 126, false, false, 0),                   // 출구 (0,1) x140: 방이 (0,2)로 밀리면 출구도 함께 옮겨짐
            };
            l.startFloor = 2; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },                         // REACH THE ROOF
                new MissionDef { kind = MissionKind.PassObject },                         // WAREHOUSE TERMINAL
                new MissionDef { kind = MissionKind.Unseen },                             // UNSEEN(D-099, 솜씨): 카메라에 발각되지 않기
            };
            l.tutorial = false;
            l.showGuide = false;
            l.tutorialSteps = new TutorialStep[0];
            l.starTwoHint = "";
        }

        /// <summary>
        /// 챕터 1 의뢰 1-3 OLD TOWN D(W-029, D-111, W-031 D-119): 3층(3F·2F·1F) x 5열, 맵 800x344. 배치는 03-01 §2-7 표.
        /// 빈 칸 6개, 레일은 빈 점포(2F, 열 1), 레이저 복도(2F, 열 2)는 섹터 전체형, 복도(2F, 열 3)의 계단이 1F 로비로 내려간다. 별·미션 3개·카운트다운이 있고 안내는 처음 한 번만.
        /// </summary>
        static void BuildOld3(LevelData l)
        {
            l.title = "SWAP SHOP";
            l.buildingName = "OLD TOWN D";
            l.floors = 3; l.cols = 5;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home1_base";       // 방 레이어 배치가 없을 때만 쓰는 한 장 그림(쓰이지 않음)
            SetLayout(l, "scenes128/ch1_old3/layout");
            l.trackingRateScale = 0.7f;                      // D-120: 챕터 1 추적도 상승 초당 14
            l.sectors = new[]
            {
                new SectorDef { floor = 1, col = 1, target = SectorTarget.Objects, rail = true },   // 빈 점포(레일)
                new SectorDef { floor = 1, col = 2, target = SectorTarget.Whole, realityBlocked = true, blueprintBlocked = false },   // 레이저 복도: 켜짐 -> 꺼짐
            };
            l.emptyCells = new[]
            {
                new CellDef { floor = 0, col = 2 }, new CellDef { floor = 0, col = 3 }, new CellDef { floor = 0, col = 4 },   // 3F 오른쪽
                new CellDef { floor = 1, col = 4 },                                                                          // 2F 오른쪽 끝
                new CellDef { floor = 2, col = 0 }, new CellDef { floor = 2, col = 1 },                                      // 1F 왼쪽
            };
            ObjectDef stairs = Obj(ObjectKind.Stairs, 1, 3, 80, 60, true, true);         // 계단 2F->1F (1,3) x80: 열림(계속)
            stairs.hasLink = true; stairs.linkFloor = 2; stairs.linkCol = 3; stairs.linkX = 80; stairs.linkFacingRight = false;   // 1F (2,3) 로비 x80, 도착 후 왼쪽
            l.objects = new[]
            {
                Obj(ObjectKind.Terminal, 0, 0, 60, 52, false, false, 1),                 // 단말 (0,0) x60: 3F 셋방(탐색)
                stairs,
                Obj(ObjectKind.Exit, 2, 3, 40, 26, false, false, 0),                     // 출구 (2,3) x40: 1F 로비(최종)
            };
            l.startFloor = 1; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },                         // REACH THE EXIT
                new MissionDef { kind = MissionKind.PassObject },                         // UPSTAIRS TERMINAL
                new MissionDef { kind = MissionKind.TraceLimit, limit = 80f },            // TRACE <= 80(D-115, W-031b: 탐색 C 라이트 70.6)
            };
            l.tutorial = false;
            l.showGuide = false;                              // W-033(D-122): 1-3은 단계 안내 없음
            l.tutorialSteps = new TutorialStep[0];
            l.starTwoHint = "";
        }

        /// <summary>
        /// 챕터 1 의뢰 1-4 OLD TOWN E(W-029, D-111, W-031 D-119): 4층(4F·3F·2F·1F) x 4열, 맵 640x440. 배치는 03-01 §2-7 표.
        /// 빈 칸 5개, 레일은 빈 점포(1F, 열 1)와 옥상 출구 방(4F, 열 0), 레이저 복도(2F, 열 2)는 섹터 전체형 + 중계기, 2F 계단실에 감시 카메라·계단, 3F 복도에 셔터(닫힘 -> 열림)와 4F로 가는 계단. 안내 없음.
        /// </summary>
        static void BuildOld4(LevelData l, SurveillanceType camBasic)
        {
            l.title = "ROOFTOP RUN";
            l.buildingName = "OLD TOWN E";
            l.floors = 4; l.cols = 4;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home1_base";
            SetLayout(l, "scenes128/ch1_old4/layout");
            l.trackingRateScale = 0.7f;                      // D-120: 챕터 1 추적도 상승 초당 14
            l.sectors = new[]
            {
                new SectorDef { floor = 3, col = 1, target = SectorTarget.Objects, rail = true },   // 빈 점포(레일)
                new SectorDef { floor = 0, col = 0, target = SectorTarget.Objects, rail = true },   // 옥상 출구 방(레일)
                new SectorDef { floor = 2, col = 2, target = SectorTarget.Whole, realityBlocked = true, blueprintBlocked = false },   // 레이저 복도: 켜짐 -> 꺼짐
            };
            l.emptyCells = new[]
            {
                new CellDef { floor = 0, col = 1 }, new CellDef { floor = 0, col = 3 },   // 4F
                new CellDef { floor = 1, col = 0 },                                       // 3F
                new CellDef { floor = 3, col = 0 }, new CellDef { floor = 3, col = 3 },   // 1F
            };
            ObjectDef stairs = Obj(ObjectKind.Stairs, 2, 3, 60, 40, true, true);         // 계단 2F->3F (2,3) x60: 열림(계속)
            stairs.hasLink = true; stairs.linkFloor = 1; stairs.linkCol = 3; stairs.linkX = 60; stairs.linkFacingRight = false;   // 3F (1,3) x60, 도착 후 왼쪽
            ObjectDef stairs34 = Obj(ObjectKind.Stairs, 1, 2, 40, 20, true, true);       // 계단 3F->4F (1,2) x40: 열림(계속) [W-031]
            stairs34.hasLink = true; stairs34.linkFloor = 0; stairs34.linkCol = 2; stairs34.linkX = 40; stairs34.linkFacingRight = false;   // 4F (0,2) x40, 도착 후 왼쪽
            ObjectDef camera = Obj(ObjectKind.Camera, 2, 3, 120, 120, camBasic.realityOn, camBasic.blueprintOn);   // 감시 카메라 (2,3): 1-2와 같은 cam_basic
            camera.surveillance = camBasic;
            l.objects = new[]
            {
                ShutterDoor(1, 2, 100, 80),                                              // 3F 셔터 (1,2) x100: 닫힘 -> 열림 [W-031 제안]. W-033: 셔터 전용 그림(40px 폭, 가운데가 막는 선), 종류·판정은 Door 그대로
                stairs34,
                stairs,
                camera,
                Obj(ObjectKind.Relay, 2, 2, 100, 92, false, false),                      // 중계기 (2,2) x100: 레이저 복도 안
                Obj(ObjectKind.Terminal, 3, 2, 100, 92, false, false, 1),                // 단말 (3,2) x100: 창고(탐색)
                Obj(ObjectKind.Exit, 0, 0, 140, 126, false, false, 0),                   // 출구 (0,0) x140: 방이 밀리면 출구도 함께 옮겨짐
            };
            l.startFloor = 2; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },                         // REACH THE ROOF
                new MissionDef { kind = MissionKind.PassObject },                         // CELLAR TERMINAL
                new MissionDef { kind = MissionKind.Unseen },                             // UNSEEN
            };
            l.tutorial = false;
            l.showGuide = false;
            l.tutorialSteps = new TutorialStep[0];
            l.starTwoHint = "";
        }

        /// <summary>
        /// 챕터 1 새 1-4 OLD TOWN F(W-036, D-125): 3층(3F·2F·1F) x 5열, 맵 800x344. 배치는 03-01 §2-8 표.
        /// 빈 칸 7개(3F 열 0·1·2·4, 2F 열 2, 1F 열 2), 레일은 A 끝 방(2F 열 1)과 B 끝 방(2F 열 4). B 복도(2F 열 3) 바닥 해치가 1F 로비로 떨어지고,
        /// 1F B 창고(열 4)의 단말을 지나 계단이 2F B 끝 방으로 올린다. 3F 열 3 옥탑이 출구이고 3F 열 4(빈 칸)로 B 끝 방을 올려 나른다. 단계 안내 없음(D-122).
        /// </summary>
        static void BuildOld5(LevelData l)
        {
            l.title = "DOUBLE HAUL";
            l.buildingName = "OLD TOWN F";
            l.floors = 3; l.cols = 5;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home1_base";       // 방 레이어 배치가 없을 때만 쓰는 한 장 그림(쓰이지 않음)
            SetLayout(l, "scenes128/ch1_old5/layout");
            l.trackingRateScale = 0.7f;                      // D-120: 챕터 1 추적도 상승 초당 14
            l.sectors = new[]
            {
                new SectorDef { floor = 1, col = 1, target = SectorTarget.Objects, rail = true },   // A 끝 방(레일)
                new SectorDef { floor = 1, col = 4, target = SectorTarget.Objects, rail = true },   // B 끝 방(레일)
            };
            l.emptyCells = new[]
            {
                new CellDef { floor = 0, col = 0 }, new CellDef { floor = 0, col = 1 }, new CellDef { floor = 0, col = 2 }, new CellDef { floor = 0, col = 4 },   // 3F: 옥탑 말고 전부
                new CellDef { floor = 1, col = 2 },                                                                                                           // 2F 골목 위
                new CellDef { floor = 2, col = 2 },                                                                                                           // 1F 골목
            };
            ObjectDef hatch = Obj(ObjectKind.Hatch, 1, 3, 80, 54, false, true);          // B 복도 바닥 해치 (1,3) x80: 닫힘 -> 열림. drawX 54(0-1·0-2와 같음)
            hatch.hasLink = true; hatch.linkFloor = 2; hatch.linkCol = 3; hatch.linkX = 80; hatch.linkFacingRight = true;    // 1F B 로비 (2,3) x80, 도착 후 오른쪽
            ObjectDef stairs = Obj(ObjectKind.Stairs, 2, 4, 110, 90, true, true);        // 계단 1F->2F (2,4) x110: 열림(계속). 단말(x50)보다 오른쪽이라 오른쪽으로 걸으면 단말을 지난 뒤 오른다
            stairs.hasLink = true; stairs.linkFloor = 1; stairs.linkCol = 4; stairs.linkX = 40; stairs.linkFacingRight = true;   // 2F B 끝 방 (1,4) x40, 도착 후 오른쪽
            l.objects = new[]
            {
                hatch,
                Obj(ObjectKind.Terminal, 2, 4, 50, 42, false, false, 1),                 // 단말 (2,4) x50: B 창고(탐색)
                stairs,
                Obj(ObjectKind.Exit, 0, 3, 140, 126, false, false, 0),                   // 출구 (0,3) x140: 3F 옥탑(최종)
            };
            l.startFloor = 1; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 1; l.startAimCol = 1;                // 처음 조준 = A 끝 방(레일)
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },                         // REACH THE ROOF
                new MissionDef { kind = MissionKind.PassObject },                         // B CELLAR TERMINAL
                new MissionDef { kind = MissionKind.TraceLimit, limit = 60f },            // TRACE <= 60 [임시: 메인이 측정값으로 정함]
            };
            l.tutorial = false;
            l.showGuide = false;                              // D-122: 2스테이지부터 단계 안내 없음
            l.tutorialSteps = new TutorialStep[0];
            l.starTwoHint = "";
        }

        /// <summary>
        /// 챕터 1 새 1-5 OLD TOWN G(W-036, D-125): 4층(4F·3F·2F·1F) x 4열, 맵 640x440. 배치는 03-01 §2-8 표.
        /// 빈 칸 5개, 레일은 감시 복도(3F 열 2, 카메라만)와 옥상 출구 방(4F 열 3). 2F 레이저 복도(열 2, 중계기)를 지나 계단실 계단으로 3F 열 3에 도착하면
        /// 카메라 구역을 카메라 끄기로 지나가거나, 감시 복도를 계단 방(3F 열 1)과 맞바꿔 카메라를 비켜 세운다 — 맞바뀐 방의 계단이 작동해야 한다(§2 규칙 변경). 단계 안내 없음.
        /// </summary>
        static void BuildOld6(LevelData l, SurveillanceType camBasic)
        {
            l.title = "CAMERA SHUFFLE";
            l.buildingName = "OLD TOWN G";
            l.floors = 4; l.cols = 4;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home1_base";
            SetLayout(l, "scenes128/ch1_old6/layout");
            l.trackingRateScale = 0.7f;                      // D-120: 챕터 1 추적도 상승 초당 14
            l.sectors = new[]
            {
                new SectorDef { floor = 1, col = 2, target = SectorTarget.Objects, rail = true },   // 감시 복도(레일, 카메라만)
                new SectorDef { floor = 0, col = 3, target = SectorTarget.Objects, rail = true },   // 옥상 출구 방(레일)
                new SectorDef { floor = 2, col = 2, target = SectorTarget.Whole, realityBlocked = true, blueprintBlocked = false },   // 레이저 복도: 켜짐 -> 꺼짐
            };
            l.emptyCells = new[]
            {
                new CellDef { floor = 0, col = 0 }, new CellDef { floor = 0, col = 2 },   // 4F
                new CellDef { floor = 3, col = 0 }, new CellDef { floor = 3, col = 2 }, new CellDef { floor = 3, col = 3 },   // 1F
            };
            ObjectDef hatch = Obj(ObjectKind.Hatch, 2, 1, 80, 54, false, true);          // 2F 복도 바닥 해치 (2,1) x80: 닫힘 -> 열림
            hatch.hasLink = true; hatch.linkFloor = 3; hatch.linkCol = 1; hatch.linkX = 70; hatch.linkFacingRight = true;      // 1F 창고 (3,1) x70, 도착 후 오른쪽
            ObjectDef stairs12 = Obj(ObjectKind.Stairs, 3, 1, 30, 10, true, true);       // 계단 1F->2F (3,1) x30: 열림(계속). 도착(x70) 왼쪽에 있어 오른쪽으로 걸으면 단말을 먼저 찍는다
            stairs12.hasLink = true; stairs12.linkFloor = 2; stairs12.linkCol = 0; stairs12.linkX = 100; stairs12.linkFacingRight = true;   // 2F 상점 (2,0) x100, 도착 후 오른쪽
            ObjectDef stairs23 = Obj(ObjectKind.Stairs, 2, 3, 60, 40, true, true);       // 계단 2F->3F (2,3) x60: 열림(계속)
            stairs23.hasLink = true; stairs23.linkFloor = 1; stairs23.linkCol = 3; stairs23.linkX = 80; stairs23.linkFacingRight = false;   // 3F (1,3) x80, 도착 후 왼쪽
            ObjectDef stairs34 = Obj(ObjectKind.Stairs, 1, 1, 130, 110, true, true);     // 계단 3F->4F (1,1) x130: 열림(계속). 방 오른쪽에 두어 맞바꿈 때 Shift를 짧게 누른다. 이 방이 열 2로 맞바뀌어도 작동한다(§2 규칙)
            stairs34.hasLink = true; stairs34.linkFloor = 0; stairs34.linkCol = 1; stairs34.linkX = 60; stairs34.linkFacingRight = false;   // 4F 옥상 복도 (0,1) x60, 도착 후 왼쪽
            ObjectDef camera = Obj(ObjectKind.Camera, 1, 2, 120, 120, camBasic.realityOn, camBasic.blueprintOn);   // 감시 카메라 (1,2): 1-2와 같은 cam_basic. 구역은 이 방이 있는 칸 한 열
            camera.surveillance = camBasic;
            l.objects = new[]
            {
                hatch,
                stairs12,
                Obj(ObjectKind.Relay, 2, 2, 100, 92, false, false),                      // 중계기 (2,2) x100: 레이저 복도 안
                stairs23,
                stairs34,
                camera,
                Obj(ObjectKind.Terminal, 3, 1, 110, 102, false, false, 1),               // 단말 (3,1) x110: 1F 창고(탐색)
                Obj(ObjectKind.Exit, 0, 3, 100, 86, false, false, 0),                    // 출구 (0,3) x100: 방이 밀리면 출구도 함께 옮겨짐. 방 안쪽에 두어 끌어오기 때 Shift를 짧게 누른다
            };
            l.startFloor = 2; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },                         // REACH THE ROOF
                new MissionDef { kind = MissionKind.PassObject },                         // STORE TERMINAL
                new MissionDef { kind = MissionKind.Unseen },                             // UNSEEN
            };
            l.tutorial = false;
            l.showGuide = false;                              // D-122: 단계 안내 없음
            l.tutorialSteps = new TutorialStep[0];
            l.starTwoHint = "";
        }

        /// <summary>
        /// 챕터 2 시험 맵 TEST_2-0(W-035, D-112, 02-04 §2-3): 1층 x 5열. 시작 복도(중계기) -> 프레스 방 A -> 프레스 방 B -> 복도 -> 로비(출구).
        /// 프레스 방 두 곳(열 1·2)에 시계 표시. 아무것도 안 해도 프레스가 다시 올라가면 지나가지만 시간이 걸린다(추적도는 오르지 않음). 챕터 2 추적도 배율 0.7 [제안].
        /// </summary>
        static void BuildTest20(LevelData l)
        {
            l.title = "PRESS TEST";
            l.buildingName = "FACTORY TEST";
            l.floors = 1; l.cols = 5;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home1_base";       // 방 레이어 배치가 없을 때만 쓰는 한 장 그림(쓰이지 않음)
            SetLayout(l, "scenes128/ch2_test/layout");
            l.trackingRateScale = 0.7f;                      // [제안] 챕터 2 추적도 배율(W-035 지시서 §1)
            l.sectors = new[]
            {
                new SectorDef { floor = 0, col = 1, target = SectorTarget.Objects, clock = true },   // 프레스 방 A(시계)
                new SectorDef { floor = 0, col = 2, target = SectorTarget.Objects, clock = true },   // 프레스 방 B(시계)
            };
            l.emptyCells = new CellDef[0];
            l.objects = new[]
            {
                Obj(ObjectKind.Relay, 0, 0, 110, 110, false, false),                     // 중계기 (0,0) x110
                Obj(ObjectKind.Press, 0, 1, 40, 28, false, false),                       // 프레스 A (0,1) 가운데 x40 [제안: 아무것도 안 하면 내려온 박자(약 6.3초)에 닿아 올라올 때까지 기다림 -> Shift + 2로 바로 통과]
                Obj(ObjectKind.Press, 0, 2, 13, 1, false, false),                        // 프레스 B (0,2) 가운데 x13 [제안: A를 Shift + 2로 지나면 B에 내려온 박자로 닿아 Shift + 1이 필요(서로 다른 N). 아무것도 안 하면 A에서 기다린 뒤 올라간 박자에 닿아 그대로 지남]
                Obj(ObjectKind.Exit, 0, 4, 40, 26, false, false, 0),                     // 출구 (0,4) x40: 로비(최종)
            };
            l.startFloor = 0; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 0; l.startAimCol = 1;                // 처음 조준 = 프레스 방 A
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },                         // REACH THE EXIT(최종 1개뿐, 별 없음)
            };
            l.tutorial = false;
            l.showGuide = false;
            l.tutorialSteps = new TutorialStep[0];
            l.starTwoHint = "";
        }

        // 챕터 2 솜씨 미션 TRACE 기준(W-038): 라이트 유저 모델로 잰 값에서 [제안]. 바꾸면 Strings.csv의 d.mission.trace_* 행도 함께
        public const int Fac1Trace = 40, Fac3Trace = 95;

        static SectorDef ClockRoom(int floor, int col, bool rail = false) =>
            new SectorDef { floor = floor, col = col, target = SectorTarget.Objects, clock = true, rail = rail };

        static SectorDef LaserRoom(int floor, int col) =>
            new SectorDef { floor = floor, col = col, target = SectorTarget.Whole, periodicLaser = true };

        static void Fac(LevelData l, string title, string building, int floors, int cols, string layoutKey)
        {
            l.title = title;
            l.buildingName = building;
            l.floors = floors; l.cols = cols;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home1_base";       // 방 레이어 배치가 없을 때만 쓰는 한 장 그림(쓰이지 않음)
            SetLayout(l, layoutKey);
            l.trackingRateScale = 0.7f;                      // 챕터 2 추적도 배율 [제안]
            l.tutorial = false;
            l.showGuide = false;
            l.tutorialSteps = new TutorialStep[0];
            l.starTwoHint = "";
        }

        /// <summary>
        /// 챕터 2 의뢰 2-1 FACTORY A(03-01 §3-1): 2F·1F x 4열. 2F 하역장(시작)·프레스 라인[시계]·복도(계단 2F->1F), 1F 창고(단말)·레이저 라인[시계](주기 레이저)·출하장(출구).
        /// 계단은 1F 창고 오른쪽에 내려서 왼쪽을 보며 도착(단말 쪽으로 간 뒤 돌아와 레이저로). 단계 안내 4개(D-122): 박자 표시 / Shift만 / Shift + 숫자 / 주기 레이저.
        /// </summary>
        static void BuildFac1(LevelData l)
        {
            Fac(l, "ON THE BEAT", "FACTORY A", 2, 4, "scenes128/ch2_fac1/layout");
            l.sectors = new[] { ClockRoom(0, 1), LaserRoom(1, 2) };
            l.emptyCells = new[] { new CellDef { floor = 0, col = 3 }, new CellDef { floor = 1, col = 0 } };
            ObjectDef stairs = Obj(ObjectKind.Stairs, 0, 2, 60, 40, true, true);          // 복도 계단 2F->1F (0,2) x60: 열림(계속)
            stairs.hasLink = true; stairs.linkFloor = 1; stairs.linkCol = 1; stairs.linkX = 125; stairs.linkFacingRight = false;   // 1F 창고 (1,1) x125, 도착 후 왼쪽(단말 쪽)
            l.objects = new[]
            {
                Obj(ObjectKind.Press, 0, 1, 35, 23, false, false),                        // 프레스 (0,1) x35: 아무것도 안 하면 내려온 박자에 닿아 약 1초 기다림
                stairs,
                Obj(ObjectKind.Terminal, 1, 1, 40, 32, false, false, 1),                  // 단말 (1,1) x40: 창고 왼쪽
                Obj(ObjectKind.Exit, 1, 3, 40, 26, false, false, 0),                      // 출구 (1,3) x40: 출하장
            };
            l.startFloor = 0; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 0; l.startAimCol = 1;                 // 처음 조준 = 프레스 라인
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },                          // REACH THE EXIT
                new MissionDef { kind = MissionKind.PassObject },                          // STOCK TERMINAL
                new MissionDef { kind = MissionKind.TraceLimit, limit = Fac1Trace },       // TRACE <= (라이트 유저 모델 측정, W-038)
            };
            l.showGuide = true;
            l.tutorialSteps = new[]
            {
                TStep(TutorialCond.Start, TutorialInput.Enter, "", "MACHINES MOVE TO A SHARED BEAT.", TutorialFocus.BeatBar, 0, 0),
                TStep(TutorialCond.Start, TutorialInput.Hold, "SHIFT", "SHIFT SHOWS THE NEXT BEATS.", TutorialFocus.Room, 0, 1),
                TStep(TutorialCond.Start, TutorialInput.ShiftDigit, "SHIFT 1 2 3", "PRESS 1-3 TO SKIP AHEAD. MATCH AN OPEN BEAT.", TutorialFocus.Room, 0, 1),
                TWait(TStep(TutorialCond.TargetWaiting, TutorialInput.Enter, "", "ONE BEAT OFF. PULL IT CLOSER.", TutorialFocus.Room, 1, 2), 1),   // 1F에서 기다릴 때(주기 레이저 앞)
            };
        }

        /// <summary>
        /// 챕터 2 의뢰 2-2 FACTORY B(03-01 §3-2): 2F·1F x 5열. 2F 하역장(시작)·프레스 A·프레스 B·감시 통로(카메라)·계단실, 1F 출하장(출구)·복도·레이저 라인(중계기)·창고(단말)·계단실(도착, 왼쪽으로).
        /// 프레스 A·B는 같은 박자(방 안 위치만 다름) — 서로 다른 N으로 어긋나게 맞춘다. 단계 안내 없음.
        /// </summary>
        static void BuildFac2(LevelData l, SurveillanceType camBasic)
        {
            Fac(l, "OFFBEAT", "FACTORY B", 2, 5, "scenes128/ch2_fac2/layout");
            l.sectors = new[] { ClockRoom(0, 1), ClockRoom(0, 2), LaserRoom(1, 2) };
            l.emptyCells = new CellDef[0];
            ObjectDef stairs = Obj(ObjectKind.Stairs, 0, 4, 60, 40, true, true);          // 계단 2F->1F (0,4) x60
            stairs.hasLink = true; stairs.linkFloor = 1; stairs.linkCol = 4; stairs.linkX = 100; stairs.linkFacingRight = false;   // 1F (1,4) x100, 도착 후 왼쪽
            ObjectDef camera = Obj(ObjectKind.Camera, 0, 3, 120, 120, camBasic.realityOn, camBasic.blueprintOn);   // 감시 통로 (0,3): cam_basic
            camera.surveillance = camBasic;
            l.objects = new[]
            {
                Obj(ObjectKind.Press, 0, 1, 35, 23, false, false),                        // 프레스 A (0,1) x35
                Obj(ObjectKind.Press, 0, 2, 60, 48, false, false),                        // 프레스 B (0,2) x60
                camera,
                stairs,
                Obj(ObjectKind.Relay, 0, 2, 140, 132, false, false),                      // 중계기 (0,2) x140 [제안]: 프레스 B 뒤·카메라 구역 앞(중계기는 한 번만 듣는다: 03-01 표의 레이저 라인 안 자리는 가장 쓸모 없는 때라 옮김). 카메라를 끄고 건너는 긴 구간을 앞두고 추적도를 낮춤
                Obj(ObjectKind.Terminal, 1, 3, 100, 92, false, false, 1),                 // 단말 (1,3) x100: 창고
                Obj(ObjectKind.Exit, 1, 0, 40, 26, false, false, 0),                      // 출구 (1,0) x40: 출하장
            };
            l.startFloor = 0; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 0; l.startAimCol = 1;
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },                          // REACH THE EXIT
                new MissionDef { kind = MissionKind.PassObject },                          // PACKING TERMINAL
                new MissionDef { kind = MissionKind.Unseen },                              // UNSEEN
            };
        }

        /// <summary>
        /// 챕터 2 의뢰 2-3 FACTORY C(03-01 §3-3): 3F·2F·1F x 4열. 3F 관제실(출구)·빈 칸·옥상 배관(계단 도착, 왼쪽으로)·빈 칸, 2F 빈 칸·화물 승강 칸[레일]·프레스 라인[시계]·계단실(2F->3F),
        /// 1F 하역장(시작)·레이저 라인[시계](중계기)·창고(단말)·계단실(1F->2F). 승강 칸을 Shift + ↑로 3F 빈 칸(열 1)에 올려 관제실로 건넌다. 단계 안내 없음.
        /// </summary>
        static void BuildFac3(LevelData l)
        {
            Fac(l, "CONTROL ROOM", "FACTORY C", 3, 4, "scenes128/ch2_fac3/layout");
            l.sectors = new[] { ClockRoom(1, 1, true), ClockRoom(1, 2), LaserRoom(2, 1) };
            l.emptyCells = new[] { new CellDef { floor = 0, col = 1 }, new CellDef { floor = 0, col = 3 }, new CellDef { floor = 1, col = 0 } };
            ObjectDef up12 = Obj(ObjectKind.Stairs, 2, 3, 100, 80, true, true);           // 계단 1F->2F (2,3) x100
            up12.hasLink = true; up12.linkFloor = 1; up12.linkCol = 3; up12.linkX = 60; up12.linkFacingRight = false;     // 2F (1,3) x60, 도착 후 왼쪽(프레스 쪽)
            ObjectDef up23 = Obj(ObjectKind.Stairs, 1, 3, 130, 110, true, true);          // 계단 2F->3F (1,3) x130
            up23.hasLink = true; up23.linkFloor = 0; up23.linkCol = 2; up23.linkX = 20; up23.linkFacingRight = false;     // 3F (0,2) x40, 도착 후 왼쪽(빈 칸 쪽)
            l.objects = new[]
            {
                Obj(ObjectKind.Terminal, 2, 2, 60, 52, false, false, 1),                  // 단말 (2,2) x60: 창고
                up12,
                Obj(ObjectKind.Press, 1, 2, 80, 68, false, false),                        // 프레스 (1,2)
                up23,
                Obj(ObjectKind.Relay, 0, 2, 8, 2, false, false),                          // 중계기 (0,2) x8 [제안]: 도착(x20) 바로 왼쪽. 승강 칸을 건너며 누르는 동안(가장 긴 구간)을 앞두고 추적도를 낮춤
                Obj(ObjectKind.Exit, 0, 0, 100, 86, false, false, 0),                     // 출구 (0,0) x100: 관제실
            };
            l.startFloor = 2; l.startCol = 0; l.startX = 20; l.startFacingRight = true;
            l.hasStartAim = true; l.startAimFloor = 2; l.startAimCol = 1;                 // 처음 조준 = 레이저 라인
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.PassObject },                          // REACH CONTROL
                new MissionDef { kind = MissionKind.PassObject },                          // STOCK TERMINAL
                new MissionDef { kind = MissionKind.TraceLimit, limit = Fac3Trace },       // TRACE <= (라이트 유저 모델 측정, W-038)
            };
        }

        /// <summary>W-033: 셔터 전용 그림(artKey rollshutter)을 쓰는 문(종류 Door, 닫힘 -> 열림). drawX는 그림 왼쪽(40px 폭이라 막는 선 x에서 20 뺌).</summary>
        static ObjectDef ShutterDoor(int floor, int col, float x, float drawX)
        {
            ObjectDef d = Obj(ObjectKind.Door, floor, col, x, drawX, false, true);
            d.artKey = "rollshutter";
            return d;
        }

        // ---- W-033 튜토리얼 단계 만들기(D-122). 단계 표는 BuildHome1·BuildHome2·BuildOld1에 있다 ----
        // TStep: 시작 조건, 요구 입력, 키 배지(공백으로 나열), 문구, 강조 영역(방이면 층·열). 나머지는 아래 도우미로 덧붙인다.
        static TutorialStep TStep(TutorialCond cond, TutorialInput input, string key, string text, TutorialFocus focus, int ff, int fc) =>
            new TutorialStep { cond = cond, input = input, key = key, text = text, focus = focus, focusFloor = ff, focusCol = fc };

        /// <summary>시작 조건 TargetPast: floor층 절대 x = col * 섹터 폭 + x, 오른쪽을 보며 이상(facingRight) 또는 왼쪽을 보며 이하.</summary>
        static TutorialStep TPast(TutorialStep s, int floor, int col, float x, bool facingRight) { s.floor = floor; s.col = col; s.x = x; s.facingRight = facingRight; return s; }

        /// <summary>시작 조건 TargetWaiting(층, 열 -1 = 층만 봄).</summary>
        static TutorialStep TWait(TutorialStep s, int floor) { s.floor = floor; s.col = -1; return s; }

        /// <summary>시작 조건 AimAt(층, 열).</summary>
        static TutorialStep TAt(TutorialStep s, int floor, int col) { s.floor = floor; s.col = col; return s; }

        /// <summary>시작 조건 TargetOnFloor.</summary>
        static TutorialStep TFloor(TutorialStep s, int floor) { s.floor = floor; return s; }

        /// <summary>Press·ShiftArrow의 방향(dx, dy)과 조준 목표(untilAim: 층 aimFloor, 열 aimCol). dx·dy가 0이면 방향키 아무거나.</summary>
        static TutorialStep TAim(TutorialStep s, int dx, int dy, int aimFloor, int aimCol, bool untilAim = true)
        {
            s.keyDx = dx; s.keyDy = dy; s.untilAim = untilAim; s.aimFloor = aimFloor; s.aimCol = aimCol;
            return s;
        }

        /// <summary>두 번째 강조 영역(Room은 방, Cell은 칸, Trace는 위치 인자 무시).</summary>
        static TutorialStep TAlso(TutorialStep s, TutorialFocus focus2, int f, int c) { s.focus2 = focus2; s.focus2Floor = f; s.focus2Col = c; return s; }

        /// <summary>왼쪽 말풍선 화자 이름(SYSTEM, ??? 등)을 지정한 대사. 화자 자리는 의뢰인(Speaker.Client).</summary>
        static DialogueLine Say(string name, string time, string text, string face) =>
            new DialogueLine { speaker = Speaker.Client, speakerName = name, time = time, text = text, hackerFace = face };

        /// <summary>W-011: 의뢰의 바깥 시간대·날씨(연출만, 규칙·판정에는 영향 없음).</summary>
        static void SetEnv(LevelData l, TimeOfDay time, Weather weather)
        {
            l.timeOfDay = time;
            l.weather = weather;
            EditorUtility.SetDirty(l);
        }

        static DialogueLine Line(Speaker who, string time, string text, string face) =>
            new DialogueLine { speaker = who, time = time, text = text, hackerFace = face };

        /// <summary>W-010: 방 레이어 배치(layout.json)를 레벨에 연결한다. ArtImporter가 Assets/_Project/Art/ 아래로 복사한 파일을 쓴다.</summary>
        static void SetLayout(LevelData l, string key)
        {
            l.layoutKey = key;
            l.layoutJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ArtImporter.ArtRoot + "/" + key + ".json");
            if (l.layoutJson == null) Debug.LogError("[Mvp1Setup] layout.json을 찾지 못했습니다: " + key);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
