using System.IO;
using GameOff.Data;
using GameOff.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

            // 바깥 환경 기본값(W-011, D-103 ⑤ [제안]): 챕터 0 두 의뢰 밤·비, 1-1 밤·흐림, 1-2 밤·안개. 그 밖의 에셋(샌드박스·테스트 의뢰)은 기본값 밤·맑음.
            SetEnv(level, TimeOfDay.Night, Weather.Clear);
            SetEnv(ch01, TimeOfDay.Night, Weather.Clear);
            SetEnv(ch02, TimeOfDay.Night, Weather.Clear);
            SetEnv(home1, TimeOfDay.Night, Weather.Rain);
            SetEnv(home2, TimeOfDay.Night, Weather.Rain);
            SetEnv(old1, TimeOfDay.Night, Weather.Cloudy);
            SetEnv(old2, TimeOfDay.Night, Weather.Fog);
            SetEnv(old3, TimeOfDay.Night, Weather.Cloudy);   // W-029 [제안] 1-3 밤·흐림
            SetEnv(old4, TimeOfDay.Night, Weather.Rain);     // W-029 [제안] 1-4 밤·비
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

            // 챕터 1 의뢰 1-1·1-2 대사(W-028): [임시] 07-01 v0.2.0 §6 초안 — 사용자 원고로 교체. 화자 BROKER·RUNNER는 의뢰인 자리(왼쪽 말풍선, speakerName), ME는 오른쪽
            var odlg1 = LoadOrCreate<DialogueData>(DataDir + "/Ch1Old_1Dialogue.asset");
            odlg1.briefing = new[]
            {
                Say("BROKER", "22:10", "TWO BUILDINGS. NO BRIDGE. GET YOUR RUNNER ACROSS.", "neutral"),
                Say("RUNNER", "22:10", "NO BRIDGE? FINE. I'LL JUMP.", "neutral"),
                Line(Speaker.Me, "22:11", "DON'T. THE ROOM IS THE BRIDGE.", "disgust"),
            };
            odlg1.ending = new[]
            {
                Say("RUNNER", "22:24", "I JUST CROSSED AN ALLEY INSIDE A ROOM.", "joy"),
                Say("BROKER", "22:24", "PAYMENT SENT.", "neutral"),
                Line(Speaker.Me, "22:25", "ONE LINE OFF YOUR RECORD.", "neutral"),
            };
            EditorUtility.SetDirty(odlg1);
            var odlg2 = LoadOrCreate<DialogueData>(DataDir + "/Ch1Old_2Dialogue.asset");
            odlg2.briefing = new[]
            {
                Say("BROKER", "23:02", "ROOFTOP PICKUP. 2F STAIRWELL HAS A CAMERA.", "neutral"),
                Say("RUNNER", "23:02", "CAMERAS LOVE ME.", "joy"),
                Line(Speaker.Me, "23:03", "THIS ONE DOESN'T. WALK WHEN I SAY.", "anger"),
            };
            odlg2.ending = new[]
            {
                Say("RUNNER", "23:19", "ROOF! NICE VIEW. NEXT TIME, A REAL DOOR?", "joy"),
                Say("BROKER", "23:19", "CLEAN. MORE WORK SOON.", "neutral"),
                Line(Speaker.Me, "23:20", "LOGGING OFF.", "neutral"),
            };
            EditorUtility.SetDirty(odlg2);
            // 1-3·1-4 대사(W-029): [임시] 07-01 v0.3.0 §6 초안
            var odlg3 = LoadOrCreate<DialogueData>(DataDir + "/Ch1Old_3Dialogue.asset");
            odlg3.briefing = new[]
            {
                Say("BROKER", "00:05", "A LASER HALL BLOCKS THE WAY OUT.", "neutral"),
                Say("RUNNER", "00:05", "I CAN DUCK. PROBABLY.", "joy"),
                Line(Speaker.Me, "00:06", "NO NEED. I'LL SWAP THE ROOMS.", "neutral"),
            };
            odlg3.ending = new[]
            {
                Say("RUNNER", "00:18", "THE WHOLE SHOP MOVED. WITH ME IN IT.", "surprise"),
                Say("BROKER", "00:18", "DONE. ONE LEFT TONIGHT.", "neutral"),
                Line(Speaker.Me, "00:19", "KEEP YOUR SHOES ON.", "neutral"),
            };
            EditorUtility.SetDirty(odlg3);
            var odlg4 = LoadOrCreate<DialogueData>(DataDir + "/Ch1Old_4Dialogue.asset");
            odlg4.briefing = new[]
            {
                Say("BROKER", "00:40", "LAST JOB. ROOFTOP. LASERS, A CAMERA, NO STAIRS TO THE EXIT.", "neutral"),
                Say("RUNNER", "00:40", "SO... EVERYTHING AT ONCE.", "fear"),
                Line(Speaker.Me, "00:41", "ONE THING AT A TIME. LISTEN.", "neutral"),
            };
            odlg4.ending = new[]
            {
                Say("RUNNER", "00:57", "WE DID IT! THE WHOLE TOWN, ROOM BY ROOM.", "joy"),
                Say("BROKER", "00:57", "CLEAN WORK. MORE SOON.", "neutral"),
                Line(Speaker.Me, "00:58", "LOGGING OFF.", "neutral"),
            };
            EditorUtility.SetDirty(odlg4);

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
                // 1-3·1-4(W-029, D-111): 제목·요약은 [임시]
                new RequestEntry
                {
                    id = "old-1-3", chapter = 1, client = "???", title = "SWAP SHOP", summary = "A LASER HALL BLOCKS THE WAY.\nSWAP THE SHOP PAST IT.\nREACH THE EXIT.",
                    missionNames = new[] { "REACH THE EXIT", "UPSTAIRS TERMINAL", "TRACE <= 80" },
                    level = old3,
                    dialogue = odlg3,
                },
                new RequestEntry
                {
                    id = "old-1-4", chapter = 1, client = "???", title = "ROOFTOP RUN", summary = "LASERS, A CAMERA, A ROOF.\nONE THING AT A TIME.\nREACH THE ROOF.",
                    missionNames = new[] { "REACH THE ROOF", "CELLAR TERMINAL", "UNSEEN" },
                    level = old4,
                    dialogue = odlg4,
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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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

            EditorSceneManager.SaveScene(scene, ScenePath);
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
                new TutorialStep { cond = TutorialCond.Start, key = "SHIFT", text = "THE INTRUDER HEADS FOR THE STAIRS. HOLD SHIFT TO SEAL THEM." },
                new TutorialStep { cond = TutorialCond.ShiftReleased, text = "IT ONLY HOLDS WHILE YOU PRESS." },
                new TutorialStep { cond = TutorialCond.TargetTurnedBack, floor = 1, key = "\u2190", text = "PRESS \u2190 TO AIM AT THE HATCH." },
                new TutorialStep { cond = TutorialCond.AimAt, floor = 1, col = 2, key = "SHIFT", text = "HOLD SHIFT TO DROP THEM." },
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
                new TutorialStep { cond = TutorialCond.Start, text = "THE ELEVATOR GOES UP TO YOU. HOLD SHIFT TO STOP IT." },
                new TutorialStep { cond = TutorialCond.TargetWaiting, floor = 1, col = -1, text = "SOME ROOMS SHIFT AS A WHOLE. AIM AT THE ROOM." },
                new TutorialStep { cond = TutorialCond.RelayUsed, text = "RELAYS LOWER YOUR TRACE." },
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
                new TutorialStep { cond = TutorialCond.Start, text = "SOME ROOMS SIT ON RAILS. AIM AT ONE." },
                new TutorialStep { cond = TutorialCond.AimAt, floor = 0, col = 1, key = "SHIFT", key2 = "\u2192", text = "HOLD SHIFT, THEN PRESS \u2192 TO SLIDE IT." },
                new TutorialStep { cond = TutorialCond.FirstPushed, text = "LET GO ONLY AFTER SHE STEPS OUT." },
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
            l.showGuide = true;
            l.tutorialSteps = new[]
            {
                new TutorialStep { cond = TutorialCond.Start, text = "RAILED ROOMS SWAP PLACES WITH THEIR NEIGHBOR." },
            };
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
                Obj(ObjectKind.Door, 1, 2, 100, 100, false, true),                       // 3F 셔터 (1,2) x100: 닫힘 -> 열림 [W-031 제안]
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
