using System;
using UnityEngine;

namespace GameOff.Data
{
    /// <summary>바깥 시간대(W-011, D-103). 직렬화 값이 바뀌지 않도록 순서를 지킨다.</summary>
    public enum TimeOfDay { Day = 0, Night = 1 }

    /// <summary>바깥 날씨(W-011, D-103).</summary>
    public enum Weather { Clear = 0, Cloudy = 1, Rain = 2, Snow = 3, Fog = 4 }

    /// <summary>한 프리셋이 쓰는 입자 종류(environment-fx §2 표의 「입자」 칸).</summary>
    [Flags]
    public enum EnvParticleKinds { None = 0, Clouds = 1, Rain = 2, Drips = 4, Snow = 8, Fog = 16 }

    /// <summary>
    /// 시간대·날씨 한 조합의 값(environment-fx §2). 기호(Sym)는 아트 팔레트(tools/art/artkit.py PIX)의 한 글자이고, 색은 그 기호의 실제 값이다.
    /// 밀도는 분모로 적는다(0 = 없음, 4 = 1/4, 8 = 1/8, 16 = 1/16, lighting-shading §3-2).
    /// </summary>
    [Serializable]
    public sealed class EnvPreset
    {
        public TimeOfDay time;
        public Weather weather;
        [Header("하늘(위 -> 아래)")]
        public string skyTopSym = "a";
        public Color32 skyTop;
        public string skyBottomSym = "e";
        public Color32 skyBottom;
        [Header("실내 밝기(L3)")]
        [Tooltip("실내 어둠 밀도 분모. 0 = 없음")]
        public int ambientDenominator;
        public string ambientSym = "a";
        public Color32 ambientColor;
        [Tooltip("창 빛 밀도 분모. 0 = 없음")]
        public int beamDenominator;
        public string beamSym = "";
        public Color32 beamColor;
        [Header("입자(LE)")]
        public EnvParticleKinds particles;
        [Tooltip("비·눈·안개 입자 색 기호")]
        public string particleSym = "";
        public Color32 particleColor;
    }

    /// <summary>
    /// 환경 이펙트 값 모음(W-011, D-103). 프리셋 10개와 입자 수·옥상 방 목록을 이 에셋 하나에서 조정한다.
    /// 값은 Docs/Art/environment-fx.md §2·§3 표 그대로이며 Setup(EnvironmentSetup)이 처음 값을 넣는다.
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentSet", menuName = "GameOff/Environment Set")]
    public sealed class EnvironmentSet : ScriptableObject
    {
        public EnvPreset[] presets = new EnvPreset[0];

        [Header("입자 수(창 1개 208x144 기준, environment-fx §3). 보이는 창 면적에 비례한다")]
        [Min(1)] public int rainPerWindow = 70;
        [Min(1)] public int dripsPerWindow = 8;
        [Min(1)] public int snowPerWindow = 60;
        [Min(1)] public int cloudsPerWindow = 6;
        [Min(1)] public int fogBandsPerWindow = 3;
        [Tooltip("한 화면 입자 상한(성능 규칙, environment-fx §4). 이 수가 곧 입자 배열 크기의 상한이다")]
        [Min(1)] public int maxParticles = 600;

        [Header("지붕 없는 방(옥상)")]
        [Tooltip("이 이름의 방은 LE 입자를 L2 위·L4 아래에 한 번 더 그린다(D-103 ①)")]
        public string[] openAirRooms = new string[0];

        /// <summary>시간대·날씨에 맞는 프리셋. 없으면 null.</summary>
        public EnvPreset Find(TimeOfDay time, Weather weather)
        {
            if (presets == null) return null;
            for (int i = 0; i < presets.Length; i++)
                if (presets[i].time == time && presets[i].weather == weather) return presets[i];
            return null;
        }

        public bool IsOpenAir(string roomName)
        {
            if (openAirRooms == null) return false;
            for (int i = 0; i < openAirRooms.Length; i++)
                if (openAirRooms[i] == roomName) return true;
            return false;
        }

        /// <summary>
        /// 아트 팔레트 기호의 실제 색(tools/art/artkit.py PIX와 같은 값, 환경 이펙트가 쓰는 기호만).
        /// 모르는 기호는 false.
        /// </summary>
        public static bool TryPalette(string sym, out Color32 color)
        {
            switch (sym)
            {
                case "0": color = new Color32(30, 34, 48, 255); return true;
                case "1": color = new Color32(42, 48, 64, 255); return true;
                case "2": color = new Color32(68, 76, 98, 255); return true;
                case "3": color = new Color32(104, 114, 140, 255); return true;
                case "4": color = new Color32(50, 56, 74, 255); return true;
                case "b": color = new Color32(40, 96, 176, 255); return true;
                case "C": color = new Color32(140, 210, 255, 255); return true;
                case "W": color = new Color32(236, 246, 255, 255); return true;
                case "a": color = new Color32(10, 10, 24, 255); return true;
                case "e": color = new Color32(22, 16, 44, 255); return true;
                case "H": color = new Color32(64, 58, 92, 255); return true;
                case "J": color = new Color32(38, 34, 60, 255); return true;
                case "U": color = new Color32(110, 104, 146, 255); return true;
            }
            color = default;
            return false;
        }
    }
}
