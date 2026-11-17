using System;
using UnityEngine;

namespace GameOff.Data
{
    [Serializable]
    public struct ArtEntry
    {
        public string key;          // 예: "ui/button"
        public Texture2D texture;
        public int nl, nt, nr, nb;  // 9-slice 테두리(왼·위·오른·아래, px). 0이면 없음
    }

    [Serializable]
    public struct GlyphEntry
    {
        public int code;            // 글자 코드(대문자 기준)
        public int x, y, w, h;      // 아틀라스 안 좌표(왼쪽 위 기준)
    }

    /// <summary>화면 그리기에 쓰는 반입 PNG와 글자 좌표 모음. GameOff/Import Art(Setup)가 채운다.</summary>
    [CreateAssetMenu(fileName = "ArtSet", menuName = "GameOff/Art Set")]
    public sealed class ArtSet : ScriptableObject
    {
        public ArtEntry[] entries;
        public GlyphEntry[] glyphs;
    }
}
