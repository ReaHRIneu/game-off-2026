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

        public void Clear(IEnumerable<string> ids)
        {
            foreach (string id in ids)
            {
                cache.Remove(id);
                try { PlayerPrefs.DeleteKey(Prefix + id); } catch (Exception) { }
            }
            try { PlayerPrefs.Save(); } catch (Exception) { }
        }
    }
}
