using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Pedal through 1900 → 2050. Each building has a construction year: first a dashed
    /// outline and a crane appear, then it rises. A glowing time ring circles the city and
    /// lights a marker for every decade; the sky shifts from sepia to modern night.
    /// </summary>
    public class TimeMachine : Experience
    {
        public override string Title => "Time Machine";

        const float StartYear = 1900f, EndYear = 2050f;
        static readonly Color Amber = new Color(1f, 0.7f, 0.25f);
        static readonly Color Sepia = new Color(0.45f, 0.33f, 0.22f);
        static readonly Color Night = new Color(0.03f, 0.04f, 0.1f);

        class Site { public Kit.Building B; public GameObject Crane, Outline; public float Grow; }

        readonly List<Site> sites = new List<Site>();
        readonly List<Material> decadeMarks = new List<Material>();
        Transform timeRing;
        Material windowMat;
        float year = StartYear, timer, orbit;
        bool started;
        Text yearText, builtText, rateText, eraText;
        RectTransform yearBar;

        public override void Begin()
        {
            App.Env(Sepia, new Color(0.4f, 0.36f, 0.32f), 35f, 110f);
            App.Sun.color = new Color(1f, 0.85f, 0.65f);
            App.Sun.intensity = 1.1f;
            App.Sun.transform.rotation = Quaternion.Euler(40f, -40f, 0f);
            Cam.fieldOfView = 35f;

            Geo.Prim(PrimitiveType.Plane, transform, new Vector3(0, -0.01f, 0), Vector3.one * 12f, Mats.Lit(new Color(0.18f, 0.17f, 0.15f), 0.2f));
            Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, -0.02f, 0), new Vector3(36f, 0.04f, 36f), Mats.Lit(new Color(0.12f, 0.12f, 0.14f), 0.4f));

            windowMat = Mats.Unique(Mats.Glow(new Color(1f, 0.8f, 0.45f), 0.3f));
            var wins = new[] { windowMat, windowMat, windowMat, windowMat, windowMat, windowMat };
            var city = Kit.City(transform, wins, Mats.Lit(new Color(0.55f, 0.5f, 0.48f), 0.2f), Mats.Glow(Amber, 1.2f), 4f, 6, 21);

            var rnd = new System.Random(8);
            var craneMat = Mats.Glow(new Color(1f, 0.75f, 0.2f), 1.3f);
            var outlineMat = Mats.Additive(new Color(0.4f, 0.85f, 1f), 0.9f);
            // order: low buildings near the centre first, towers last; ~5 already stand in 1900
            var order = new List<Kit.Building>(city);
            var key = new Dictionary<Kit.Building, float>();
            foreach (var b in city) key[b] = new Vector2(b.Pos.x, b.Pos.z).magnitude / 14f + b.Height / 14f + (float)rnd.NextDouble() * 0.35f;
            order.Sort((x, y) => key[x].CompareTo(key[y]));
            for (int i = 0; i < order.Count; i++) order[i].Year = Mathf.RoundToInt(1880f + i / (float)(order.Count - 1) * 165f);
            foreach (var b in city)
            {
                var s = new Site { B = b };

                s.Crane = new GameObject("Crane").gameObject;
                s.Crane.transform.SetParent(b.Root, false);
                float ch = b.Height + 2f;
                Geo.Prim(PrimitiveType.Cube, s.Crane.transform, new Vector3(1.4f, ch / 2, 1.4f), new Vector3(0.12f, ch, 0.12f), craneMat);
                Geo.Prim(PrimitiveType.Cube, s.Crane.transform, new Vector3(0.6f, ch, 1.4f), new Vector3(2.6f, 0.1f, 0.1f), craneMat);
                Geo.Prim(PrimitiveType.Cube, s.Crane.transform, new Vector3(-0.3f, ch - 0.8f, 1.4f), new Vector3(0.03f, 1.6f, 0.03f), craneMat);

                s.Outline = new GameObject("Outline");
                s.Outline.transform.SetParent(b.Root, false);
                float w = 1.3f, hh = b.Height;
                Kit.Poly(s.Outline.transform, outlineMat, 0.05f,
                    new Vector3(-w, 0, -w), new Vector3(w, 0, -w), new Vector3(w, 0, w), new Vector3(-w, 0, w), new Vector3(-w, 0, -w),
                    new Vector3(-w, hh, -w), new Vector3(w, hh, -w), new Vector3(w, 0, -w), new Vector3(w, hh, -w), new Vector3(w, hh, w),
                    new Vector3(w, 0, w), new Vector3(w, hh, w), new Vector3(-w, hh, w), new Vector3(-w, 0, w), new Vector3(-w, hh, w), new Vector3(-w, hh, -w));
                sites.Add(s);
            }

            // the time ring with decade markers
            timeRing = new GameObject("TimeRing").transform;
            timeRing.SetParent(transform, false);
            timeRing.localPosition = new Vector3(0, 0.4f, 0);
            Kit.Ring(timeRing, 18f, Mats.Additive(Amber, 1.3f), 0.18f, 128);
            Kit.Ring(timeRing, 18.8f, Mats.Additive(Amber, 0.4f), 0.06f, 128);
            for (int d = 0; d <= 15; d++)
            {
                float a = d / 16f * Mathf.PI * 2f;
                var mark = Mats.Unique(Mats.Glow(Amber, 0.2f));
                decadeMarks.Add(mark);
                var p = new Vector3(Mathf.Cos(a) * 18f, 0.4f, Mathf.Sin(a) * 18f);
                Geo.Prim(PrimitiveType.Cube, timeRing, p, new Vector3(0.5f, 0.8f, 0.5f), mark);
                var label = Geo.WorldText(timeRing, (1900 + d * 10).ToString(), p + Vector3.up * 1.5f, new Vector2(200, 60), 46, Amber);
                label.transform.parent.gameObject.AddComponent<Billboard>();
            }

            TitleBlock(Title, "Pedal to travel through time and watch the city grow from 1900 to 2050.");
            yearText = StatCard(new Vector2(0, 0.5f), new Vector2(40, 140), "Year", "", 300, Amber);
            rateText = StatCard(new Vector2(0, 0.5f), new Vector2(40, -10), "Speed", "yrs/s", 300, Amber);
            builtText = StatCard(new Vector2(1, 0.5f), new Vector2(-40, 140), "Buildings", "/ 36", 280, new Color(0.4f, 0.85f, 1f));
            eraText = Hud.Label(Hud.Layer, "", 30, new Vector2(1, 0.5f), new Vector2(-44, -40), new Vector2(420, 120), TextAnchor.UpperRight, new Color(1, 1, 1, 0.75f));
            yearBar = ProgressBar(new Vector2(0.5f, 0), new Vector2(0, 60), 1000, "1900  →  2050", Amber);
            Hint("PEDAL TO START THE TIME MACHINE");
            UpdateCity(0f);
        }

        static string Era(float y) =>
            y < 1920 ? "Early industrial town" : y < 1950 ? "Interwar growth" : y < 1975 ? "Post-war boom" :
            y < 2000 ? "Office towers rise" : y < 2025 ? "Glass & steel skyline" : "Future city";

        void UpdateCity(float dt)
        {
            int built = 0;
            foreach (var s in sites)
            {
                float since = year - s.B.Year;
                bool construction = since > -6f && since < 0f;
                s.Crane.SetActive(construction || (since >= 0f && since < 1.5f));
                s.Outline.SetActive(construction);
                float target = since >= 0f ? 1f : 0f;
                s.Grow = dt <= 0f ? target : Mathf.MoveTowards(s.Grow, target, dt * 1.6f);
                if (s.Grow > 0.99f) built++;
                foreach (Transform child in s.B.Root)
                {
                    if (child.gameObject == s.Crane || child.gameObject == s.Outline) continue;
                    child.gameObject.SetActive(s.Grow > 0.01f);
                }
            }
            builtText.text = built.ToString();
        }

        public override void Tick(in MotionState m, float dt)
        {
            orbit += dt * 5f;
            float a = (orbit + 20f) * Mathf.Deg2Rad;
            Cam.transform.position = new Vector3(Mathf.Sin(a) * 40f, 27f, -Mathf.Cos(a) * 40f);
            Cam.transform.LookAt(new Vector3(0, 3f, 0));

            float rate = m.Watts / 200f * 4f; // years per second
            if (!Finished)
            {
                if (!started && m.Watts > 20f) { started = true; Hint(null); }
                if (started) timer += dt;
                year = Mathf.Min(EndYear, year + rate * dt);
                if (year >= EndYear)
                {
                    int rank = ScoreService.Record("time", timer, lowerIsBetter: true);
                    Finish($"150 years in {timer:0.0} s", rank == 1 ? "NEW BEST TIME TRAVELLER!" : $"#{rank} today");
                }
            }

            UpdateCity(dt);
            float t = (year - StartYear) / (EndYear - StartYear);
            // grow buildings by scaling the whole root vertically
            foreach (var s in sites)
            {
                float g = Mathf.SmoothStep(0f, 1f, s.Grow);
                s.B.Root.localScale = new Vector3(1f, Mathf.Max(g, 0.001f), 1f);
                // keep the crane and outline full height while the building rises
                float inv = 1f / Mathf.Max(g, 0.001f);
                s.Crane.transform.localScale = new Vector3(1f, s.Grow > 0.01f ? inv : 1f, 1f);
                s.Outline.transform.localScale = new Vector3(1f, s.Grow > 0.01f ? inv : 1f, 1f);
                if (s.Grow <= 0.01f) s.B.Root.localScale = new Vector3(1f, 1f, 1f);
            }

            for (int d = 0; d < decadeMarks.Count; d++)
                decadeMarks[d].SetColor("_EmissionColor", Amber * (year >= 1900 + d * 10 ? 3f : 0.15f));
            timeRing.localRotation = Quaternion.Euler(0, year * 2f, 0);

            var sky = t < 0.5f ? Color.Lerp(Sepia, new Color(0.3f, 0.45f, 0.65f), t * 2f) : Color.Lerp(new Color(0.3f, 0.45f, 0.65f), Night, (t - 0.5f) * 2f);
            App.Env(sky, Color.Lerp(new Color(0.4f, 0.36f, 0.32f), new Color(0.18f, 0.2f, 0.3f), t), 35f, 110f);
            App.Sun.intensity = Mathf.Lerp(1.1f, 0.35f, t);
            windowMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.45f) * Mathf.Lerp(0.2f, 2.6f, t));

            yearText.text = Mathf.FloorToInt(year).ToString();
            rateText.text = rate.ToString("0.0");
            eraText.text = Era(year);
            Hud.SetFill(yearBar, t);
        }
    }
}
