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

        public int GetBest(string id)
        {
            if (cache.TryGetValue(id, out int v)) return v;
            v = 0;
            try { v = PlayerPrefs.GetInt(Prefix + id, 0); }
            catch (Exception e) { Debug.LogWarning("[Progress] 읽기 실패, 0으로 진행: " + e.Message); }
            cache[id] = v;
            return v;
        }

        public void SetBest(string id, int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            if (stars <= GetBest(id)) return;
            cache[id] = stars;
            try { PlayerPrefs.SetInt(Prefix + id, stars); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("[Progress] 저장 실패, 이 세션만 유지: " + e.Message); }
        }

        const string ClearPrefix = "gameoff.cleared.";
        readonly HashSet<string> clearedCache = new HashSet<string>();

        /// <summary>그 의뢰를 클리어한 적이 있는가(W-008). 별이 없는 튜토리얼 의뢰도 이 값으로 진행을 센다.</summary>
        public bool IsCleared(string id)
        {
            if (clearedCache.Contains(id)) return true;
            bool v = false;
            try { v = PlayerPrefs.GetInt(ClearPrefix + id, 0) != 0; }
            catch (Exception e) { Debug.LogWarning("[Progress] 읽기 실패, 미클리어로 진행: " + e.Message); }
            if (v) clearedCache.Add(id);
            return v;
        }

        public void SetCleared(string id)
        {
            if (!clearedCache.Add(id)) return;
            try { PlayerPrefs.SetInt(ClearPrefix + id, 1); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("[Progress] 저장 실패, 이 세션만 유지: " + e.Message); }
        }

        public void Clear(IEnumerable<string> ids)
        {
            foreach (string id in ids)
            {
                cache.Remove(id);
                clearedCache.Remove(id);
                try { PlayerPrefs.DeleteKey(Prefix + id); PlayerPrefs.DeleteKey(ClearPrefix + id); } catch (Exception) { }
            }
            try { PlayerPrefs.Save(); } catch (Exception) { }
        }
    }
}
