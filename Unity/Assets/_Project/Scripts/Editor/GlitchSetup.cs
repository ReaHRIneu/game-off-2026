using GameOff.Data;
using GameOff.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameOff.EditorTools
{
    /// <summary>
    /// W-013: 글리치 프리셋 에셋(Data/GlitchSet.asset)을 만들고 Main 씬의 GameView에 연결한다.
    /// 시작값은 vfx-direction §7-3 표의 역할별 프리셋을 따른 [제안]이며, 이후 조정은 에셋에서 직접 한다
    /// (이미 있는 에셋은 덮어쓰지 않는다. 처음 값으로 되돌리려면 에셋을 지우고 다시 실행).
    /// 메뉴: GameOff/Setup Glitch (W-013)
    /// </summary>
    public static class GlitchSetup
    {
        public const string AssetPath = "Assets/_Project/Data/GlitchSet.asset";
        const string ShaderPath = "Assets/_Project/Shaders/GlitchPost.shader";

        [MenuItem("GameOff/Setup Glitch (W-013)")]
        public static void Run()
        {
            var set = EnsureAsset();
            var scene = EditorSceneManager.OpenScene(Mvp1Setup.ScenePath);
            var view = Object.FindAnyObjectByType<GameView>();
            if (view == null) { Debug.LogError("[GlitchSetup] GameView가 씬에 없습니다."); return; }
            var so = new SerializedObject(view);
            so.FindProperty("glitch").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GlitchSet>(AssetPath);   // 씬을 열면 메모리의 에셋이 해제될 수 있어 다시 불러온다
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[GlitchSetup] 완료: " + AssetPath);
        }

        [MenuItem("GameOff/Reset Glitch Presets to Starting Values (W-013)")]
        public static void ResetValues()
        {
            var set = EnsureAsset();
            Fill(set);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("[GlitchSetup] 프리셋을 시작값으로 되돌렸습니다.");
        }

        /// <summary>에셋이 없으면 시작값으로 만든다. Mvp1Setup도 이 메서드로 GameView에 연결한다.</summary>
        public static GlitchSet EnsureAsset()
        {
            var set = AssetDatabase.LoadAssetAtPath<GlitchSet>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<GlitchSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
                Fill(set);
                EditorUtility.SetDirty(set);
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (set.shader != shader) { set.shader = shader; EditorUtility.SetDirty(set); }
            AssetDatabase.SaveAssets();
            return set;
        }

        // 단계 한 칸을 짧게 쓰기 위한 도우미(틱, 띠 높이, 밀림 폭, 밀림 비율, 색 복사, 어긋남, 밀도, 스캔, 노이즈, 블록 크기, 블록 수, 치우침)
        static GlitchStage S(int ticks, int band, int shift, float ratio, GlitchCopy copy, int off, float dens, float scan = 0f, float noise = 0f, int blockSize = 8, int blocks = 0, float bias = 0f) =>
            new GlitchStage
            {
                ticks = ticks, bandHeight = band, maxShift = shift, shiftRatio = ratio, copy = copy, copyOffset = off, copyDensity = dens,
                scan = scan, noise = noise, blockSize = blockSize, blockCount = blocks, shiftBias = bias,
            };

        static void Fill(GlitchSet set)
        {
            set.dangerTrackingRatio = 0.7f;
            set.presets = new[]
            {
                // shift_in: 3프레임(0.1초), 줄 밀림 강 + 복사 C + 스캔라인 약. 들어올 때는 밀림이 줄어들며 제자리로
                new GlitchPreset { id = "shift_in", stages = new[] { S(1, 4, 6, 0.6f, GlitchCopy.C, 2, 0.8f, 0.2f), S(1, 4, 4, 0.5f, GlitchCopy.C, 2, 0.7f, 0.2f), S(1, 4, 2, 0.3f, GlitchCopy.C, 1, 0.5f, 0.2f) } },
                // shift_out: 반대로 제자리에서 밀림이 커지며 흩어진다
                new GlitchPreset { id = "shift_out", stages = new[] { S(1, 4, 2, 0.3f, GlitchCopy.C, 1, 0.5f, 0.2f), S(1, 4, 4, 0.5f, GlitchCopy.C, 2, 0.7f, 0.2f), S(1, 4, 6, 0.6f, GlitchCopy.C, 2, 0.8f, 0.2f) } },
                // room_push: 4프레임, 줄 밀림(미는 방향 쪽으로 치우침) + 복사 C·R + 노이즈 약
                new GlitchPreset { id = "room_push", stages = new[] { S(1, 4, 4, 0.5f, GlitchCopy.RC, 2, 0.6f, 0f, 0.15f, 8, 0, 0.8f), S(1, 4, 6, 0.6f, GlitchCopy.RC, 3, 0.7f, 0f, 0.15f, 8, 0, 0.8f),
                    S(1, 4, 4, 0.5f, GlitchCopy.RC, 2, 0.6f, 0f, 0.15f, 8, 0, 0.8f), S(1, 4, 2, 0.3f, GlitchCopy.RC, 1, 0.4f, 0f, 0.1f, 8, 0, 0.8f) } },
                // danger: 장면 뷰, 2프레임, 2~3초마다 1회(카운트다운 중 1초마다). 복사 R + 스캔라인 + 줄 밀림 약
                new GlitchPreset { id = "danger", loop = true, intervalMin = 2f, intervalMax = 3f, fastInterval = 1f,
                    stages = new[] { S(1, 6, 2, 0.25f, GlitchCopy.R, 2, 0.8f, 0.3f), S(1, 6, 1, 0.15f, GlitchCopy.R, 2, 0.6f, 0.2f) } },
                // gameover: 화면 전체, 0.5초(15틱), 3단계로 세짐. 줄 밀림 강 + 복사 R + 노이즈 강 + 블록 깨짐 약
                new GlitchPreset { id = "gameover", stages = new[] { S(5, 6, 4, 0.5f, GlitchCopy.R, 2, 0.6f, 0.2f, 0.25f), S(5, 6, 6, 0.65f, GlitchCopy.R, 3, 0.8f, 0.3f, 0.5f, 8, 2), S(5, 6, 8, 0.8f, GlitchCopy.R, 4, 1f, 0.4f, 0.8f, 8, 4) } },
                // transition: 화면 전체, 4프레임, 줄 밀림 + 복사 C + 스캔라인
                new GlitchPreset { id = "transition", stages = new[] { S(1, 6, 5, 0.5f, GlitchCopy.C, 2, 0.7f, 0.25f), S(1, 6, 6, 0.55f, GlitchCopy.C, 2, 0.7f, 0.25f), S(1, 6, 4, 0.45f, GlitchCopy.C, 2, 0.6f, 0.2f), S(1, 6, 2, 0.3f, GlitchCopy.C, 1, 0.5f, 0.15f) } },
                // logo: 로고 영역, 2프레임, 3~4초마다. 복사 R·C + 줄 밀림 약
                new GlitchPreset { id = "logo", loop = true, intervalMin = 3f, intervalMax = 4f, fastInterval = 3f,
                    stages = new[] { S(1, 3, 2, 0.3f, GlitchCopy.RC, 2, 0.8f), S(1, 3, 1, 0.2f, GlitchCopy.RC, 2, 0.6f) } },
            };
        }
    }
}
