using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GameOff.Game
{
    public enum Language { English = 0, Korean = 1 }

    /// <summary>
    /// 화면 문구 표(W-034, D-124). 표 파일은 Resources/Strings.csv(id, en, ko)이다.
    /// - 코드에 박힌 문구는 id로 찾는다: Loc.T("menu.continue").
    /// - 데이터 에셋(의뢰·대화·튜토리얼 단계)의 영문은 그대로 두고, 영문을 키로 한국어를 찾는다: Loc.Tr("TRAP THE INTRUDER").
    ///   이 경로는 id가 "d."로 시작하는 행만 쓴다(같은 영문은 한 행).
    /// 한국어 칸이 비어 있거나 한국어가 아니면 영문을 돌려준다. 없는 id는 id 자체를 돌려주고 경고를 한 번 남긴다.
    /// </summary>
    public static class Loc
    {
        public sealed class Row
        {
            public string id, en, ko;
        }

        /// <summary>지금 화면 언어. GameView가 저장된 값(ProgressStore.Language)으로 맞춘다. 기본 영어.</summary>
        public static Language Current = Language.English;

        static readonly List<Row> rows = new List<Row>();
        static readonly Dictionary<string, Row> byId = new Dictionary<string, Row>();
        static readonly Dictionary<string, Row> byEn = new Dictionary<string, Row>();
        static readonly HashSet<string> warned = new HashSet<string>();
        static bool loaded;

        public static IReadOnlyList<Row> Rows { get { EnsureLoaded(); return rows; } }

        public static bool Has(string id) { EnsureLoaded(); return byId.ContainsKey(id); }

        /// <summary>이 영문(데이터 에셋 글자)이 표에 있는가.</summary>
        public static bool HasEn(string en) { EnsureLoaded(); return en != null && byEn.ContainsKey(en); }

        static void EnsureLoaded()
        {
            if (loaded) return;
            var ta = Resources.Load<TextAsset>("Strings");
            LoadFrom(ta != null ? ta.text : "");
            if (ta == null) Debug.LogError("[Loc] Resources/Strings.csv 를 찾지 못했습니다. 영문 없이 id가 그대로 보입니다.");
        }

        /// <summary>CSV 글(첫 줄 id,en,ko)에서 표를 다시 만든다(자동 확인에서도 쓴다).</summary>
        public static void LoadFrom(string csv)
        {
            rows.Clear(); byId.Clear(); byEn.Clear(); warned.Clear();
            loaded = true;
            List<List<string>> table = ParseCsv(csv);
            for (int i = 1; i < table.Count; i++)
            {
                List<string> f = table[i];
                if (f.Count < 2 || string.IsNullOrEmpty(f[0])) continue;
                var r = new Row { id = f[0], en = f[1], ko = f.Count > 2 ? f[2] : "" };
                rows.Add(r);
                if (!byId.ContainsKey(r.id)) byId[r.id] = r;
                if (r.id.StartsWith("d.") && !byEn.ContainsKey(r.en)) byEn[r.en] = r;
            }
        }

        static string Pick(Row r)
        {
            if (Current == Language.Korean && !string.IsNullOrWhiteSpace(r.ko)) return r.ko;
            return r.en;
        }

        /// <summary>id로 문구를 찾는다. args가 있으면 {0}·{1} 자리에 끼워 넣는다.</summary>
        public static string T(string id, params object[] args)
        {
            EnsureLoaded();
            string s;
            if (byId.TryGetValue(id, out Row r)) s = Pick(r);
            else
            {
                if (warned.Add(id)) Debug.LogWarning("[Loc] 표에 없는 id: " + id);
                s = id;
            }
            return args != null && args.Length > 0 ? string.Format(s, args) : s;
        }

        /// <summary>데이터 에셋의 영문 글자를 현재 언어로 바꾼다. 표에 없으면(빈 글자 포함) 그대로 돌려준다.</summary>
        public static string Tr(string en)
        {
            if (string.IsNullOrEmpty(en)) return en;
            EnsureLoaded();
            return byEn.TryGetValue(en, out Row r) ? Pick(r) : en;
        }

        // ---------- CSV(RFC 4180 정도): 따옴표 안의 쉼표·줄바꿈·"" 를 처리한다 ----------

        static List<List<string>> ParseCsv(string text)
        {
            var table = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return table;
            if (text[0] == '﻿') text = text.Substring(1);
            var row = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else quoted = false;
                    }
                    else sb.Append(c);
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == ',') { row.Add(sb.ToString()); sb.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(sb.ToString()); sb.Clear();
                    if (row.Count > 1 || row[0].Length > 0) table.Add(row);
                    row = new List<string>();
                }
                else sb.Append(c);
            }
            if (sb.Length > 0 || row.Count > 0)
            {
                row.Add(sb.ToString());
                table.Add(row);
            }
            return table;
        }
    }
}
