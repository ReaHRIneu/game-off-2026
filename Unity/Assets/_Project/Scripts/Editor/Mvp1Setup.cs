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

            // 의뢰 목록(W-002): #01 = 테스트 의뢰, #02~#04 = 레벨 없음(잠긴 빈 자리)
            var requests = LoadOrCreate<RequestList>(DataDir + "/RequestList.asset");
            requests.entries = new[]
            {
                new RequestEntry
                {
                    id = "req01", client = "COURIER GUILD", title = "LATE PACKAGE",
                    summary = "A COURIER IS STUCK IN A\nLOCKED FLOOR. GET THEM\nOUT WITHOUT A TRACE.",
                    missionNames = new[] { "REACH THE EXIT (FINAL)", "PASS TERMINAL A", "PASS TERMINAL B" },
                    level = level,
                    dialogue = dialogue,
                },
                new RequestEntry { id = "req02", client = "???", title = "???", summary = "", missionNames = new string[0] },
                new RequestEntry { id = "req03", client = "???", title = "???", summary = "", missionNames = new string[0] },
                new RequestEntry { id = "req04", client = "???", title = "???", summary = "", missionNames = new string[0] },
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
                list.Add(new ArtEntry
                {
                    key = rel.Substring(0, rel.Length - 4),
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                    nl = b[0], nt = b[1], nr = b[2], nb = b[3],
                });
            }
            art.entries = list.ToArray();
            art.glyphs = ArtManifest.Glyphs().ToArray();
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene가 메모리의 미사용 에셋을 해제할 수 있어 다시 불러온다.
            settings = AssetDatabase.LoadAssetAtPath<GameSettings>(DataDir + "/GameSettings.asset");
            level = AssetDatabase.LoadAssetAtPath<LevelData>(DataDir + "/TestRequest.asset");
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

        static DialogueLine Line(Speaker who, string time, string text, string face) =>
            new DialogueLine { speaker = who, time = time, text = text, hackerFace = face };

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
