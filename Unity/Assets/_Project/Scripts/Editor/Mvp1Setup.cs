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
            var level = LoadOrCreate<LevelData>(DataDir + "/TestRequest.asset");

            // 테스트 의뢰 [임시] — Docs/Development/W-001-mvp1-brief.md
            level.title = "Test request";
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

            // 의뢰 목록(W-002): #01 = 테스트 의뢰, #02~#04 = 레벨 없음(잠긴 빈 자리)
            var requests = LoadOrCreate<RequestList>(DataDir + "/RequestList.asset");
            requests.entries = new[]
            {
                new RequestEntry
                {
                    id = "req01", client = "COURIER GUILD", title = "LATE PACKAGE",
                    summary = "A COURIER IS STUCK IN A\nLOCKED FLOOR. GET THEM\nOUT WITHOUT A TRACE.",
                    missionNames = new[] { "REACH THE EXIT (FINAL)", "PASS POINT A", "PASS POINT B" },
                    level = level,
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
