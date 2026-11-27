using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>의뢰별 최고 별만 저장한다(PlayerPrefs). 읽기·쓰기가 실패해도 그 세션 동안은 메모리 값으로 진행한다.</summary>
    public sealed class ProgressStore
    {
        const string Prefix = "gameoff.best.";
        readonly Dictionary<string, int> cache = new Dictionary<string, int>();

        /// <summary>
        /// W-034(D-124): 자동 확인(스모크)이 실제 사용자 저장 값을 건드리지 않게 하는 테스트 전용 저장 공간.
        /// 에디터에서 스모크가 EditorPrefs "GameOff.TestStore"를 켜 두는 동안만 모든 키 앞에 "test/"가 붙는다. 빌드에서는 항상 실제 저장소다.
        /// </summary>
        public const string TestStoreFlag = "GameOff.TestStore";
        public const string TestKeyPrefix = "test/";
        readonly string ns = TestStoreOn() ? TestKeyPrefix : "";

        static bool TestStoreOn()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(TestStoreFlag, false);
#else
            return false;
#endif
        }

        string K(string key) => ns + key;
        /// <summary>이 저장소가 테스트 전용 공간인가(자동 확인 점검용).</summary>
        public bool IsTestStore => ns.Length > 0;

        public int GetBest(string id)
        {
            if (cache.TryGetValue(id, out int v)) return v;
            v = 0;
            try { v = PlayerPrefs.GetInt(K(Prefix + id), 0); }
            catch (Exception e) { Debug.LogWarning("[Progress] 읽기 실패, 0으로 진행: " + e.Message); }
            cache[id] = v;
            return v;
        }

        public void SetBest(string id, int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            if (stars <= GetBest(id)) return;
            cache[id] = stars;
            try { PlayerPrefs.SetInt(K(Prefix + id), stars); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("[Progress] 저장 실패, 이 세션만 유지: " + e.Message); }
        }

        const string ClearPrefix = "gameoff.cleared.";
        readonly HashSet<string> clearedCache = new HashSet<string>();

        /// <summary>그 의뢰를 클리어한 적이 있는가(W-008). 별이 없는 튜토리얼 의뢰도 이 값으로 진행을 센다.</summary>
        public bool IsCleared(string id)
        {
            if (clearedCache.Contains(id)) return true;
            bool v = false;
            try { v = PlayerPrefs.GetInt(K(ClearPrefix + id), 0) != 0; }
            catch (Exception e) { Debug.LogWarning("[Progress] 읽기 실패, 미클리어로 진행: " + e.Message); }
            if (v) clearedCache.Add(id);
            return v;
        }

        public void SetCleared(string id)
        {
            if (!clearedCache.Add(id)) return;
            try { PlayerPrefs.SetInt(K(ClearPrefix + id), 1); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("[Progress] 저장 실패, 이 세션만 유지: " + e.Message); }
        }

        // ---------- 챕터 끝 예고 화면(W-047): 본 적 있는지 ----------
        const string PreviewPrefix = "gameoff.preview.ch";
        readonly HashSet<int> previewSeen = new HashSet<int>();

        /// <summary>그 챕터의 끝 예고 화면을 본 적이 있는가.</summary>
        public bool ChapterPreviewSeen(int chapter)
        {
            if (previewSeen.Contains(chapter)) return true;
            bool v = false;
            try { v = PlayerPrefs.GetInt(K(PreviewPrefix + chapter), 0) != 0; }
            catch (Exception e) { Debug.LogWarning("[Progress] 읽기 실패, 본 적 없음으로 진행: " + e.Message); }
            if (v) previewSeen.Add(chapter);
            return v;
        }

        public void SetChapterPreviewSeen(int chapter)
        {
            if (!previewSeen.Add(chapter)) return;
            try { PlayerPrefs.SetInt(K(PreviewPrefix + chapter), 1); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("[Progress] 저장 실패, 이 세션만 유지: " + e.Message); }
        }

        // ---------- 환경 효과 줄이기(W-011, D-103): 일시정지 메뉴의 토글 값 ----------
        public const string EnvReducedKey = "gameoff.envfx.reduced";
        bool envReduced, envReducedLoaded;

        /// <summary>환경 효과 줄이기가 켜져 있는가(입자만 끄고 하늘색·밝기는 유지). 기본 꺼짐.</summary>
        public bool EnvFxReduced
        {
            get
            {
                if (!envReducedLoaded)
                {
                    envReducedLoaded = true;
                    try { envReduced = PlayerPrefs.GetInt(K(EnvReducedKey), 0) != 0; }
                    catch (Exception e) { Debug.LogWarning("[Progress] 읽기 실패, 꺼짐으로 진행: " + e.Message); }
                }
                return envReduced;
            }
            set
            {
                envReduced = value;
                envReducedLoaded = true;
                try { PlayerPrefs.SetInt(K(EnvReducedKey), value ? 1 : 0); PlayerPrefs.Save(); }
                catch (Exception e) { Debug.LogWarning("[Progress] 저장 실패, 이 세션만 유지: " + e.Message); }
            }
        }

        /// <summary>어느 의뢰든 클리어 기록이 있거나 최고 별이 1 이상인가(W-019, D-105: 메인 메뉴 CONTINUE 표시 기준).</summary>
        public bool HasAnyProgress(IEnumerable<string> ids)
        {
            foreach (string id in ids)
                if (IsCleared(id) || GetBest(id) > 0) return true;
            return false;
        }

        // ---------- 설정(W-019, D-104·D-105): 값은 바뀌는 즉시 저장한다 ----------
        public const string BgmKey = "gameoff.vol.bgm", SfxKey = "gameoff.vol.sfx", GlitchKey = "gameoff.glitch", LanguageKey = "gameoff.lang";
        public const int VolumeMax = 10, VolumeDefault = 7;
        int bgm = -1, sfx = -1, glitchLevel = -1, language = -1;

        int LoadInt(string key, int def, int min, int max)
        {
            int v = def;
            try { v = PlayerPrefs.GetInt(K(key), def); }
            catch (Exception e) { Debug.LogWarning("[Progress] 읽기 실패, 기본값으로 진행: " + e.Message); }
            return Mathf.Clamp(v, min, max);
        }

        void SaveInt(string key, int v)
        {
            try { PlayerPrefs.SetInt(K(key), v); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("[Progress] 저장 실패, 이 세션만 유지: " + e.Message); }
        }

        /// <summary>BGM 음량 0~10(처음 7). 아직 BGM이 없어 값만 저장한다.</summary>
        public int BgmVolume
        {
            get { if (bgm < 0) bgm = LoadInt(BgmKey, VolumeDefault, 0, VolumeMax); return bgm; }
            set { bgm = Mathf.Clamp(value, 0, VolumeMax); SaveInt(BgmKey, bgm); }
        }

        /// <summary>효과음 음량 0~10(처음 7).</summary>
        public int SfxVolume
        {
            get { if (sfx < 0) sfx = LoadInt(SfxKey, VolumeDefault, 0, VolumeMax); return sfx; }
            set { sfx = Mathf.Clamp(value, 0, VolumeMax); SaveInt(SfxKey, sfx); }
        }

        /// <summary>글리치 세기(OFF·LOW·NORMAL, 처음 NORMAL).</summary>
        public GlitchStrength GlitchSetting
        {
            get { if (glitchLevel < 0) glitchLevel = LoadInt(GlitchKey, (int)GlitchStrength.Normal, 0, 2); return (GlitchStrength)glitchLevel; }
            set { glitchLevel = Mathf.Clamp((int)value, 0, 2); SaveInt(GlitchKey, glitchLevel); }
        }

        /// <summary>화면 언어(W-034, D-124). 저장 값이 없으면 영어(첫 실행). 바뀌는 즉시 저장한다.</summary>
        public Language Language
        {
            get { if (language < 0) language = LoadInt(LanguageKey, (int)Language.English, 0, 1); return (Language)language; }
            set { language = Mathf.Clamp((int)value, 0, 1); SaveInt(LanguageKey, language); }
        }

        /// <summary>
        /// 이 저장소(실제 또는 테스트 전용)의 의뢰 기록과 설정을 모두 지워 첫 실행 상태로 되돌린다(W-034). 메모리 값도 비운다.
        /// </summary>
        public void ClearAll(IEnumerable<string> ids)
        {
            Clear(ids);
            foreach (string key in new[] { EnvReducedKey, BgmKey, SfxKey, GlitchKey, LanguageKey })
            {
                try { PlayerPrefs.DeleteKey(K(key)); } catch (Exception) { }
            }
            try { PlayerPrefs.Save(); } catch (Exception) { }
            envReduced = false; envReducedLoaded = false;
            bgm = sfx = glitchLevel = language = -1;
        }

        public void Clear(IEnumerable<string> ids)
        {
            previewSeen.Clear();   // W-047: 의뢰 기록을 지우면 챕터 끝 예고 화면 기록도 함께 지운다
            for (int c = 0; c < RequestBoard.ChapterCount; c++)
            {
                try { PlayerPrefs.DeleteKey(K(PreviewPrefix + c)); } catch (Exception) { }
            }
            foreach (string id in ids)
            {
                cache.Remove(id);
                clearedCache.Remove(id);
                try { PlayerPrefs.DeleteKey(K(Prefix + id)); PlayerPrefs.DeleteKey(K(ClearPrefix + id)); } catch (Exception) { }
            }
            try { PlayerPrefs.Save(); } catch (Exception) { }
        }
    }
}
