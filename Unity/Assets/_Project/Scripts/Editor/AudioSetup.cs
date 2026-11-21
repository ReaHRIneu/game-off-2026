using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameOff.Audio;
using GameOff.Game;
using UnityEditor;
using UnityEngine;

namespace GameOff.EditorTools
{
    /// <summary>
    /// W-020: 소리 목록 에셋(SfxSet·BgmSet)을 만들고, 사용자가 고른 파일을 반입하는 메뉴(GameOff/Import Audio)를 제공한다.
    /// 에셋은 sfx-plan §2의 모든 ID와 bgm-plan §4-3의 곡 슬롯 8개를 빈 항목으로 둔다(이미 있는 항목의 값은 덮어쓰지 않는다).
    /// 반입 입력은 저장소 루트의 Audio/selection.json(git 제외)이다.
    /// 원본 오디오는 재배포할 수 없어(sfx-plan §7-0) 효과음은 git 제외 폴더 Assets/_Project/Audio/ThirdParty/에 복사한다.
    /// </summary>
    public static class AudioSetup
    {
        public const string AudioDir = "Assets/_Project/Audio";
        public const string SfxPath = AudioDir + "/SfxSet.asset";
        public const string BgmPath = AudioDir + "/BgmSet.asset";
        public const string SfxFolder = AudioDir + "/ThirdParty/Sfx";
        public const string BgmFolder = AudioDir + "/Bgm";
        /// <summary>저장소 루트 기준 선택 목록 파일(git 제외).</summary>
        public const string SelectionRelPath = "Audio/selection.json";

        public sealed class ImportRow
        {
            public string Kind, Id, File, Result;
            public bool Ok;
        }

        static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }

        public static SfxSet EnsureSfxSet()
        {
            EnsureFolder(AudioDir);
            var set = AssetDatabase.LoadAssetAtPath<SfxSet>(SfxPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SfxSet>();
                AssetDatabase.CreateAsset(set, SfxPath);
            }
            var list = new List<SfxEntry>(set.entries ?? new SfxEntry[0]);
            bool changed = false;
            foreach (string id in SfxSet.AllIds)
            {
                if (set.Find(id) != null) continue;
                list.Add(new SfxEntry
                {
                    id = id,
                    clips = new AudioClip[0],
                    gainDb = SfxSet.DefaultGainDb(id),
                    pitchRandomSemitones = SfxSet.DefaultPitchSemitones(id),
                    loop = SfxSet.DefaultLoop(id),
                });
                changed = true;
            }
            if (changed) { set.entries = list.ToArray(); EditorUtility.SetDirty(set); }
            return set;
        }

        public static BgmSet EnsureBgmSet()
        {
            EnsureFolder(AudioDir);
            var set = AssetDatabase.LoadAssetAtPath<BgmSet>(BgmPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<BgmSet>();
                AssetDatabase.CreateAsset(set, BgmPath);
            }
            var list = new List<BgmEntry>(set.entries ?? new BgmEntry[0]);
            bool changed = false;
            foreach (string slot in BgmSet.Slots)
            {
                if (set.Find(slot) != null) continue;
                list.Add(new BgmEntry { slot = slot });
                changed = true;
            }
            if (changed) { set.entries = list.ToArray(); EditorUtility.SetDirty(set); }
            return set;
        }

        // ---------- 반입 ----------

        [MenuItem("GameOff/Import Audio")]
        public static void ImportMenu()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string file = Path.Combine(repoRoot, SelectionRelPath);
            if (!File.Exists(file))
            {
                Debug.Log("[ImportAudio] " + SelectionRelPath + " 파일이 없습니다. 저장소 루트의 Audio/ 폴더에 만들어 주세요.");
                return;
            }
            List<ImportRow> rows = Import(File.ReadAllText(file, Encoding.UTF8), repoRoot);
            Debug.Log(FormatTable(rows));
        }

        /// <summary>경로가 저장소 루트 기준 상대 경로인가(절대 경로·드라이브·상위 폴더 이동은 허용하지 않는다).</summary>
        public static bool IsSafeRelative(string rel)
        {
            if (string.IsNullOrEmpty(rel)) return false;
            if (Path.IsPathRooted(rel) || rel.StartsWith("/") || rel.StartsWith("\\") || rel.Contains(":")) return false;
            foreach (string part in rel.Replace('\\', '/').Split('/'))
                if (part == "..") return false;
            return true;
        }

        public static string FormatTable(List<ImportRow> rows)
        {
            var sb = new StringBuilder("[ImportAudio] 결과 " + rows.Count + "행\n");
            sb.AppendLine("종류 | ID/슬롯 | 파일 | 결과");
            foreach (ImportRow r in rows) sb.AppendLine(r.Kind + " | " + r.Id + " | " + r.File + " | " + (r.Ok ? "성공" : "실패") + (string.IsNullOrEmpty(r.Result) ? "" : " (" + r.Result + ")"));
            return sb.ToString();
        }

        /// <summary>
        /// selection.json 내용을 읽어 파일을 복사·설정하고 SfxSet·BgmSet에 연결한다. 빈 배열·없는 키는 건드리지 않는다.
        /// rootDir은 파일 경로의 기준 폴더(저장소 루트). 자동 확인은 임시 폴더를 넘긴다.
        /// </summary>
        public static List<ImportRow> Import(string jsonText, string rootDir)
        {
            var rows = new List<ImportRow>();
            object parsed;
            try { parsed = MiniJson.Parse(jsonText); }
            catch (Exception e)
            {
                rows.Add(new ImportRow { Kind = "-", Id = "-", File = SelectionRelPath, Result = "JSON 오류: " + e.Message });
                return rows;
            }
            var root = parsed as MiniJson.Obj;
            if (root == null)
            {
                rows.Add(new ImportRow { Kind = "-", Id = "-", File = SelectionRelPath, Result = "최상위가 객체가 아님" });
                return rows;
            }

            SfxSet sfx = EnsureSfxSet();
            BgmSet bgm = EnsureBgmSet();
            MiniJson.Obj sfxObj = root.O("sfx"), bgmObj = root.O("bgm");

            if (sfxObj != null)
            {
                foreach (string id in sfxObj.Keys)
                {
                    List<object> files = sfxObj.A(id);
                    if (files == null || files.Count == 0) continue;   // 빈 배열은 건드리지 않는다
                    SfxEntry entry = sfx.Find(id);
                    if (entry == null)
                    {
                        rows.Add(new ImportRow { Kind = "sfx", Id = id, File = "-", Result = "알 수 없는 ID" });
                        continue;
                    }
                    var clips = new List<AudioClip>();
                    foreach (object f in files)
                    {
                        string rel = f as string;
                        string asset = CopyIn(rel, rootDir, SfxFolder, id + "_" + (rel != null ? Path.GetFileName(rel) : ""), AudioClipLoadType.DecompressOnLoad, rows, "sfx", id);
                        if (asset == null) continue;
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(asset);
                        if (clip == null) { rows.Add(new ImportRow { Kind = "sfx", Id = id, File = rel, Result = "클립으로 읽지 못함" }); continue; }
                        clips.Add(clip);
                        rows.Add(new ImportRow { Kind = "sfx", Id = id, File = rel, Ok = true });
                    }
                    if (clips.Count > 0) { entry.clips = clips.ToArray(); EditorUtility.SetDirty(sfx); }
                }
            }

            if (bgmObj != null)
            {
                foreach (string slot in bgmObj.Keys)
                {
                    string rel = bgmObj.S(slot);
                    if (string.IsNullOrEmpty(rel)) continue;
                    BgmEntry entry = bgm.Find(slot);
                    if (entry == null)
                    {
                        rows.Add(new ImportRow { Kind = "bgm", Id = slot, File = rel, Result = "알 수 없는 슬롯" });
                        continue;
                    }
                    string ext = IsSafeRelative(rel) ? Path.GetExtension(rel).ToLowerInvariant() : "";
                    string asset = CopyIn(rel, rootDir, BgmFolder, slot + (ext.Length > 0 ? ext : ".wav"), AudioClipLoadType.Streaming, rows, "bgm", slot);
                    if (asset == null) continue;
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(asset);
                    if (clip == null) { rows.Add(new ImportRow { Kind = "bgm", Id = slot, File = rel, Result = "클립으로 읽지 못함" }); continue; }
                    entry.clip = clip;
                    EditorUtility.SetDirty(bgm);
                    rows.Add(new ImportRow { Kind = "bgm", Id = slot, File = rel, Ok = true });
                }
            }

            AssetDatabase.SaveAssets();
            return rows;
        }

        /// <summary>파일 하나를 Assets 안으로 복사하고 가져오기 설정(Force To Mono 끔, 효과음 Decompress On Load, BGM Streaming)을 건다. 실패하면 null과 표 행을 남긴다.</summary>
        static string CopyIn(string rel, string rootDir, string folder, string destName, AudioClipLoadType loadType, List<ImportRow> rows, string kind, string id)
        {
            if (!IsSafeRelative(rel))
            {
                Debug.LogError("[ImportAudio] 저장소 루트 기준 상대 경로만 쓸 수 있습니다(절대 경로·상위 폴더 금지): " + rel);
                rows.Add(new ImportRow { Kind = kind, Id = id, File = rel ?? "(null)", Result = "상대 경로가 아님, 건너뜀" });
                return null;
            }
            string src = Path.GetFullPath(Path.Combine(rootDir, rel));
            if (!File.Exists(src))
            {
                rows.Add(new ImportRow { Kind = kind, Id = id, File = rel, Result = "파일 없음" });
                return null;
            }
            EnsureFolder(folder);
            string asset = folder + "/" + destName;
            string dest = Path.GetFullPath(Path.Combine(Application.dataPath, "..", asset));
            File.Copy(src, dest, true);
            AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(asset) as AudioImporter;
            if (imp == null)
            {
                rows.Add(new ImportRow { Kind = kind, Id = id, File = rel, Result = "오디오 파일이 아님" });
                return null;
            }
            imp.forceToMono = false;
            AudioImporterSampleSettings ss = imp.defaultSampleSettings;
            ss.loadType = loadType;
            imp.defaultSampleSettings = ss;
            imp.SaveAndReimport();
            return asset;
        }
    }
}
