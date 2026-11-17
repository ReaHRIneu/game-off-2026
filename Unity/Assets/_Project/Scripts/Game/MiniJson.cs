using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GameOff.Game
{
    /// <summary>
    /// 아주 작은 JSON 읽기(레이아웃 파일용). 객체는 Obj(키 순서 유지), 배열은 List&lt;object&gt;, 숫자는 double, 문자열은 string, 참/거짓은 bool, null은 null.
    /// 레벨 시작 때 한 번만 쓰므로 속도보다 단순함을 택했다.
    /// </summary>
    public static class MiniJson
    {
        /// <summary>JSON 객체: 키 순서를 파일 순서대로 보관한다.</summary>
        public sealed class Obj
        {
            public readonly List<string> Keys = new List<string>();
            readonly Dictionary<string, object> map = new Dictionary<string, object>();

            public void Set(string k, object v)
            {
                if (!map.ContainsKey(k)) Keys.Add(k);
                map[k] = v;
            }

            public bool Has(string k) => map.ContainsKey(k);
            public object Get(string k) => map.TryGetValue(k, out var v) ? v : null;
            public Obj O(string k) => Get(k) as Obj;
            public List<object> A(string k) => Get(k) as List<object>;
            public string S(string k) => Get(k) as string;
            public int I(string k, int def = 0) => Get(k) is double d ? (int)System.Math.Round(d) : def;
        }

        public static object Parse(string text)
        {
            int i = 0;
            return Value(text, ref i);
        }

        static void Ws(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t' || s[i] == '﻿')) i++;
        }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new System.FormatException("JSON이 끝났습니다.");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var o = new Obj();
                Ws(s, ref i);
                if (s[i] == '}') { i++; return o; }
                while (true)
                {
                    Ws(s, ref i);
                    string k = Str(s, ref i);
                    Ws(s, ref i);
                    if (s[i++] != ':') throw new System.FormatException("':'가 필요합니다. 위치 " + i);
                    o.Set(k, Value(s, ref i));
                    Ws(s, ref i);
                    char n = s[i++];
                    if (n == '}') return o;
                    if (n != ',') throw new System.FormatException("',' 또는 '}'가 필요합니다. 위치 " + i);
                }
            }
            if (c == '[')
            {
                i++;
                var a = new List<object>();
                Ws(s, ref i);
                if (s[i] == ']') { i++; return a; }
                while (true)
                {
                    a.Add(Value(s, ref i));
                    Ws(s, ref i);
                    char n = s[i++];
                    if (n == ']') return a;
                    if (n != ',') throw new System.FormatException("',' 또는 ']'가 필요합니다. 위치 " + i);
                }
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == st) throw new System.FormatException("알 수 없는 값. 위치 " + i);
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }

        static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new System.FormatException("문자열이 필요합니다. 위치 " + i);
            i++;
            var sb = new StringBuilder();
            while (true)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)System.Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;   // " \ /
                }
            }
        }
    }
}
