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
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Mvp1Setup] 완료: " + ScenePath);
        }

        static ObjectDef Obj(ObjectKind kind, int floor, int col, float x, float drawX, bool realityOn, bool blueprintOn, int mission = -1) =>
            new ObjectDef { kind = kind, floor = floor, col = col, x = x, drawX = drawX, drawSprite = true, realityOn = realityOn, blueprintOn = blueprintOn, mission = mission };

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

        /// <summary>HOME BLDG 0-2: 3층(3F·2F·B1) x 4열, 640x344. 배치는 03-01 §1-C-2 표.</summary>
        static void BuildHome2(LevelData l)
        {
            l.title = "LOCKDOWN";
            l.buildingName = "HOME BLDG";
            l.floors = 3; l.cols = 4;
            l.sectorWidth = 160; l.sectorHeight = 96;
            l.roofHeight = 24; l.wallThickness = 8; l.groundHeight = 32;
            l.backgroundKey = "scenes/ch0_home2_base";       // 방 레이어 배치가 없을 때만 쓰는 예전 한 장 그림
            SetLayout(l, "scenes128/ch0_home2/layout");
            l.sectors = new[]
            {
                // 레이저 (1,2): 섹터 전체형, 켜짐 -> 꺼짐
                new SectorDef { floor = 1, col = 2, target = SectorTarget.Whole, realityBlocked = true, blueprintBlocked = false },
            };
            ObjectDef elevator = Obj(ObjectKind.Elevator, 1, 1, 80, 52, true, false);    // 엘리베이터 (1,1): 작동 -> 멈춤
            elevator.hasLink = true; elevator.linkFloor = 0; elevator.linkCol = 1; elevator.linkX = 80; elevator.linkFacingRight = false;   // 3F (0,1) x 80, 도착 후 왼쪽
            ObjectDef hatch = Obj(ObjectKind.Hatch, 1, 3, 80, 54, false, true);          // 정비 통로 해치 (1,3): 닫힘 -> 열림. drawX 54(W-012, 위와 같은 이유)
            hatch.hasLink = true; hatch.linkFloor = 2; hatch.linkCol = 3; hatch.linkX = 80; hatch.linkFacingRight = false;         // B1 (2,3) x 80, 도착 후 왼쪽
            ObjectDef fail = Obj(ObjectKind.FailPoint, 0, 0, 142, 142, false, false);    // 실패 지점 (0,0) x 142(주인공 방 문 왼쪽 끝, W-010)
            fail.drawSprite = false;
            l.objects = new[]
            {
                elevator,
                Obj(ObjectKind.Relay, 1, 2, 100, 92, false, false),                      // 중계기 (1,2) x 100: 레이저 복도 안
                hatch,
                fail,
            };
            l.startFloor = 1; l.startCol = 0; l.startX = 30; l.startFacingRight = true;
            l.missions = new[]
            {
                new MissionDef { kind = MissionKind.EnterSector, floor = 2, col = 3 },   // TRAP THE INTRUDER: B1 열 3(보일러실) (W-008: 최종 미션 하나만)
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

        /// <summary>왼쪽 말풍선 화자 이름(SYSTEM, ??? 등)을 지정한 대사. 화자 자리는 의뢰인(Speaker.Client).</summary>
        static DialogueLine Say(string name, string time, string text, string face) =>
            new DialogueLine { speaker = Speaker.Client, speakerName = name, time = time, text = text, hackerFace = face };

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
