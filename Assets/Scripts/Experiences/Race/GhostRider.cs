using System;
using System.Collections.Generic;
using UnityEngine;

namespace AxisPlay
{
    /// <summary>
    /// Race the hologram replay of the best ride so far, on a night track under an aurora.
    /// The ghost leaves fading afterimages; beat it and your ride becomes the new ghost.
    /// </summary>
    public class GhostRider : RaceBase
    {
        public override string Title => "Ghost Rider";
        protected override string RivalName => "The ghost";
        protected override Color RivalColor => new Color(0.6f, 0.55f, 1f);
        protected override Color EdgeColor => new Color(0.5f, 0.45f, 1f);

        [Serializable]
        class GhostData { public float time; public List<float> d = new List<float>(); public string name; }
        const float SampleStep = 0.25f;

        GhostData ghost;
        readonly List<float> recording = new List<float>();
        float sampleTimer;
        readonly List<Transform> afterimages = new List<Transform>();
        readonly List<Transform> aurora = new List<Transform>();

        protected override void SetupSky()
        {
            App.Env(new Color(0.01f, 0.02f, 0.05f), new Color(0.12f, 0.15f, 0.28f), 40f, 300f);
            App.Sun.color = new Color(0.5f, 0.6f, 1f);
            App.Sun.intensity = 0.4f;

            // stars
            var rnd = new System.Random(9);
            var star = Mats.Additive(Color.white, 1f);
            for (int i = 0; i < 160; i++)
            {
                var p = new Vector3((float)rnd.NextDouble() * 400 - 200, 20 + (float)rnd.NextDouble() * 90, Length + 50 + (float)rnd.NextDouble() * 200);
                Geo.Prim(PrimitiveType.Quad, transform, p, Vector3.one * 0.5f, star);
            }
            // aurora curtains
            Color[] cols = { new Color(0.2f, 1f, 0.6f), new Color(0.4f, 0.6f, 1f), new Color(0.75f, 0.35f, 1f) };
            for (int i = 0; i < 9; i++)
            {
                var q = Geo.Prim(PrimitiveType.Quad, transform, new Vector3(-120 + i * 30, 55, Length + 150 + (i % 3) * 25),
                    new Vector3(60, 45, 1), Mats.Additive(cols[i % 3], 0.22f)).transform;
                q.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 8 : -8));
                aurora.Add(q);
            }

            ghost = LoadGhost();
            for (int k = 1; k <= 3; k++)
            {
                var a = Kit.Rider(transform, Mats.Additive(RivalColor, 0.25f / k), Mats.Additive(RivalColor, 0.35f / k), Mats.Additive(RivalColor, 0.6f / k));
                afterimages.Add(a.Root);
            }
            if (ghost == null)
                Hint(null);
        }

        static GhostData LoadGhost()
        {
            string json = PlayerPrefs.GetString("ghost.best", "");
            if (string.IsNullOrEmpty(json)) return null;
            try { var g = JsonUtility.FromJson<GhostData>(json); return g != null && g.d.Count > 2 ? g : null; }
            catch { return null; }
        }

        protected override float RivalDistance(float t, float dt)
        {
            if (ghost == null) return t * 7.5f; // default ghost: steady 27 km/h
            float f = t / SampleStep;
            int i = Mathf.FloorToInt(f);
            if (i >= ghost.d.Count - 1) return Length + (t - ghost.time) * 8f;
            return Mathf.Lerp(ghost.d[i], ghost.d[i + 1], f - i);
        }

        protected override void RaceTick(in MotionState m, float dt)
        {
            sampleTimer += dt;
            while (sampleTimer >= SampleStep) { sampleTimer -= SampleStep; recording.Add(pDist); }
        }

        public override void Tick(in MotionState m, float dt)
        {
            base.Tick(m, dt);
            for (int k = 0; k < afterimages.Count; k++)
            {
                afterimages[k].localPosition = new Vector3(Lane, 0, Mathf.Max(0f, rDist - (k + 1) * 0.9f));
            }
            for (int i = 0; i < aurora.Count; i++)
            {
                var q = aurora[i];
                q.localScale = new Vector3(60 + Mathf.Sin(Time.time * 0.4f + i) * 12f, 45 + Mathf.Sin(Time.time * 0.3f + i * 2f) * 10f, 1);
            }
        }

        protected override void PlayerFinished()
        {
            recording.Add(Length);
            bool beat = ghost == null || raceTime < ghost.time;
            if (beat)
            {
                var g = new GhostData { time = raceTime, d = new List<float>(recording), name = ScoreService.CurrentRider };
                PlayerPrefs.SetString("ghost.best", JsonUtility.ToJson(g));
                PlayerPrefs.Save();
            }
            ScoreService.Record("ghost", raceTime, lowerIsBetter: true);
            string ghostName = ghost != null ? ghost.name : "the pace ghost";
            if (beat) Finish($"You beat {ghostName}!", $"{raceTime:0.0} s · you are the new ghost");
            else Finish($"The ghost wins by {raceTime - ghost.time:0.0} s", $"Your time {raceTime:0.0} s · ghost {ghost.time:0.0} s");
        }
    }
}
