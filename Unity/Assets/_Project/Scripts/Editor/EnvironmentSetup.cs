using GameOff.Data;
using UnityEditor;
using UnityEngine;

namespace GameOff.EditorTools
{
    /// <summary>
    /// W-011: 환경 이펙트 값 에셋(Data/EnvironmentSet.asset)을 만든다. 처음 값은 Docs/Art/environment-fx.md §2(프리셋 10가지)·§3(입자 수) 표 그대로다.
    /// 기호의 실제 색은 아트 팔레트(tools/art/artkit.py PIX)와 같은 값(EnvironmentSet.TryPalette).
    /// 이미 프리셋이 채워진 에셋은 덮어쓰지 않는다(조정한 값 보존). 처음 값으로 되돌리려면 에셋을 지우고 Setup을 다시 실행한다.
    /// Mvp1Setup이 부른다(GameView 연결 포함).
    /// </summary>
    public static class EnvironmentSetup
    {
        public const string AssetPath = "Assets/_Project/Data/EnvironmentSet.asset";

        public static EnvironmentSet EnsureAsset()
        {
            var set = AssetDatabase.LoadAssetAtPath<EnvironmentSet>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<EnvironmentSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }
            if (set.presets == null || set.presets.Length != 10) Fill(set);
            // 옥상(지붕 없는 방): 챕터 1 옥상 복도(ot_roofhall, D-103 ①)
            if (set.openAirRooms == null || set.openAirRooms.Length == 0) set.openAirRooms = new[] { "ot_roofhall" };
            EditorUtility.SetDirty(set);
            return set;
        }

        /// <summary>environment-fx §2 표를 그대로 옮긴 값. 입자 색은 비·눈·안개 입자에 쓰고 구름은 하늘색에서 정한다.</summary>
        static void Fill(EnvironmentSet set)
        {
            set.presets = new[]
            {
                // 시간대, 날씨, 하늘 위, 하늘 아래, 실내 어둠(분모), 창 빛 색, 창 빛(분모), 입자 종류, 입자 색
                P(TimeOfDay.Day, Weather.Clear, "b", "C", 0, "W", 8, EnvParticleKinds.None, ""),
                P(TimeOfDay.Day, Weather.Cloudy, "2", "3", 16, "3", 16, EnvParticleKinds.Clouds, "W"),
                P(TimeOfDay.Day, Weather.Rain, "4", "2", 8, "3", 16, EnvParticleKinds.Clouds | EnvParticleKinds.Rain | EnvParticleKinds.Drips, "C"),
                P(TimeOfDay.Day, Weather.Snow, "3", "U", 16, "W", 8, EnvParticleKinds.Snow, "W"),
                P(TimeOfDay.Day, Weather.Fog, "3", "U", 8, "W", 16, EnvParticleKinds.Fog, "W"),
                P(TimeOfDay.Night, Weather.Clear, "a", "e", 4, "b", 16, EnvParticleKinds.None, ""),
                P(TimeOfDay.Night, Weather.Cloudy, "e", "J", 4, "", 0, EnvParticleKinds.Clouds, "H"),
                P(TimeOfDay.Night, Weather.Rain, "e", "J", 4, "", 0, EnvParticleKinds.Clouds | EnvParticleKinds.Rain | EnvParticleKinds.Drips, "3"),
                P(TimeOfDay.Night, Weather.Snow, "J", "H", 4, "U", 16, EnvParticleKinds.Snow, "W"),
                P(TimeOfDay.Night, Weather.Fog, "J", "H", 4, "", 0, EnvParticleKinds.Fog, "U"),
            };
            set.rainPerWindow = 70;
            set.dripsPerWindow = 8;
            set.snowPerWindow = 60;
            set.cloudsPerWindow = 6;
            set.fogBandsPerWindow = 3;
            set.maxParticles = 600;
        }

        static EnvPreset P(TimeOfDay time, Weather weather, string skyTop, string skyBottom, int ambient, string beam, int beamDen, EnvParticleKinds kinds, string particle)
        {
            var p = new EnvPreset
            {
                time = time, weather = weather,
                skyTopSym = skyTop, skyTop = Color(skyTop),
                skyBottomSym = skyBottom, skyBottom = Color(skyBottom),
                ambientDenominator = ambient, ambientSym = "a", ambientColor = Color("a"),   // 실내 어둠은 팔레트 a(lighting-shading §3-4 dark_overlay)
                beamDenominator = beamDen, beamSym = beam, beamColor = beam.Length > 0 ? Color(beam) : default,
                particles = kinds,
                particleSym = particle, particleColor = particle.Length > 0 ? Color(particle) : default,
            };
            return p;
        }

        static Color32 Color(string sym)
        {
            if (!EnvironmentSet.TryPalette(sym, out Color32 c)) Debug.LogError("[EnvironmentSetup] 팔레트 기호를 모릅니다: " + sym);
            return c;
        }
    }
}
