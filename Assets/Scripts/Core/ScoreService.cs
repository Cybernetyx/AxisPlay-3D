using System;
using System.Collections.Generic;
using UnityEngine;

namespace AxisPlay
{
    [Serializable]
    public class ScoreEntry
    {
        public string name;
        public float value;
        public long ticks;
        public DateTime Time => new DateTime(ticks);
    }

    [Serializable]
    class ScoreList
    {
        public List<ScoreEntry> items = new List<ScoreEntry>();
    }

    [Serializable]
    class TickList
    {
        public List<long> items = new List<long>();
    }

    /// <summary>
    /// Offline-first local scores, rider numbers and booth activity, kept in PlayerPrefs.
    /// A sync service can push these to the cloud later.
    /// </summary>
    public static class ScoreService
    {
        const int Keep = 30;

        public static int RiderNumber => PlayerPrefs.GetInt("riderNo", 1);
        public static string CurrentRider => "Rider " + RiderNumber.ToString("000");

        public static void NextRider()
        {
            PlayerPrefs.SetInt("riderNo", RiderNumber + 1);
            PlayerPrefs.Save();
        }

        static ScoreList Load(string id)
        {
            string json = PlayerPrefs.GetString("scores." + id, "");
            if (string.IsNullOrEmpty(json)) return new ScoreList();
            try { return JsonUtility.FromJson<ScoreList>(json) ?? new ScoreList(); }
            catch { return new ScoreList(); }
        }

        /// <summary>Adds a score. Returns its 1-based rank among kept scores (0 if not kept).</summary>
        public static int Record(string id, float value, bool lowerIsBetter)
        {
            var list = Load(id);
            var e = new ScoreEntry { name = CurrentRider, value = value, ticks = DateTime.Now.Ticks };
            list.items.Add(e);
            list.items.Sort((a, b) => lowerIsBetter ? a.value.CompareTo(b.value) : b.value.CompareTo(a.value));
            if (list.items.Count > Keep) list.items.RemoveRange(Keep, list.items.Count - Keep);
            PlayerPrefs.SetString("scores." + id, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
            return list.items.IndexOf(e) + 1;
        }

        public static List<ScoreEntry> Top(string id, int n, bool todayOnly = false)
        {
            var all = Load(id).items;
            if (todayOnly) all = all.FindAll(e => e.Time.Date == DateTime.Now.Date);
            return all.GetRange(0, Mathf.Min(n, all.Count));
        }

        public static float Best(string id, float fallback)
        {
            var l = Load(id).items;
            return l.Count > 0 ? l[0].value : fallback;
        }

        /// <summary>Lower is better (times). Returns true if this is a new best.</summary>
        public static bool SubmitTime(string id, float seconds, out float best)
        {
            float prev = BestTime(id);
            Record(id, seconds, lowerIsBetter: true);
            best = Mathf.Min(prev, seconds);
            return seconds < prev;
        }

        public static float BestTime(string id) => Best(id, float.MaxValue);

        // ---------- booth activity ----------

        static string HourKey(DateTime d, int hour) => $"sessions.{d:yyyyMMdd}.{hour}";

        public static void LogSession(string id)
        {
            var now = DateTime.Now;
            string k = HourKey(now, now.Hour);
            PlayerPrefs.SetInt(k, PlayerPrefs.GetInt(k, 0) + 1);
            PlayerPrefs.SetInt("plays." + id, PlayerPrefs.GetInt("plays." + id, 0) + 1);

            var recent = LoadTicks();
            recent.items.Add(now.Ticks);
            if (recent.items.Count > 60) recent.items.RemoveRange(0, recent.items.Count - 60);
            PlayerPrefs.SetString("recentSessions", JsonUtility.ToJson(recent));
            PlayerPrefs.Save();
        }

        static TickList LoadTicks()
        {
            string json = PlayerPrefs.GetString("recentSessions", "");
            if (string.IsNullOrEmpty(json)) return new TickList();
            try { return JsonUtility.FromJson<TickList>(json) ?? new TickList(); }
            catch { return new TickList(); }
        }

        /// <summary>Sessions started today in the given hour (0..23).</summary>
        public static int SessionsInHour(int hour) => PlayerPrefs.GetInt(HourKey(DateTime.Now, hour), 0);

        /// <summary>Sessions started in the last N minutes.</summary>
        public static int RecentSessions(float minutes)
        {
            long cutoff = DateTime.Now.AddMinutes(-minutes).Ticks;
            return LoadTicks().items.FindAll(t => t >= cutoff).Count;
        }

        public static int Plays(string id) => PlayerPrefs.GetInt("plays." + id, 0);

        public static void ResetAll()
        {
            foreach (var e in Catalog.All) { PlayerPrefs.DeleteKey("scores." + e.Id); PlayerPrefs.DeleteKey("plays." + e.Id); }
            PlayerPrefs.DeleteKey("recentSessions");
            PlayerPrefs.DeleteKey("ghost.best");
            var d = DateTime.Now;
            for (int h = 0; h < 24; h++) PlayerPrefs.DeleteKey(HourKey(d, h));
            PlayerPrefs.SetInt("riderNo", 1);
            PlayerPrefs.Save();
        }
    }
}
