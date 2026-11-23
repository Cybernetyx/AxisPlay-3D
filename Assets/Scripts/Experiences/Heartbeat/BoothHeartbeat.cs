using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// The booth's own activity, live: a 3D bar chart of sessions per hour today (peak
    /// hour highlighted), a floating pulse line beating with crowd energy, and a glowing
    /// energy orb. Data comes from ScoreService.LogSession, recorded on every launch.
    /// </summary>
    public class BoothHeartbeat : Experience
    {
        public override string Title => "Booth Heartbeat";

        const int Hours = 12;
        int FirstHour;
        const int PulsePoints = 160;
        static readonly Color Rose = new Color(1f, 0.32f, 0.45f);
        static readonly Color GoldC = new Color(1f, 0.8f, 0.3f);

        LineRenderer pulse;
        Transform orb;
        Material orbMat;
        Light orbLight;
        float energy, bpm, beatClock, orbit;
        readonly float[] trace = new float[PulsePoints];
        readonly List<Transform> bars = new List<Transform>();
        readonly List<float> barTargets = new List<float>();
        float grow;

        public override void Begin()
        {
            App.Env(new Color(0.04f, 0.01f, 0.03f), new Color(0.22f, 0.16f, 0.2f), 18f, 50f);
            App.Sun.intensity = 0.35f;
            Cam.fieldOfView = 42f;

            Geo.Prim(PrimitiveType.Plane, transform, Vector3.zero, Vector3.one * 5f, Mats.Lit(new Color(0.05f, 0.03f, 0.05f), 0.85f));
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.05f, 0), new Vector3(13.5f, 0.1f, 2.4f), Mats.Lit(new Color(0.1f, 0.07f, 0.1f), 0.5f));

            FirstHour = Mathf.Clamp(System.DateTime.Now.Hour - (Hours - 1), 0, 24 - Hours);
            int peakHour = -1, peak = 0, total = 0;
            for (int h = 0; h < 24; h++) total += ScoreService.SessionsInHour(h);
            var counts = new int[Hours];
            for (int i = 0; i < Hours; i++)
            {
                counts[i] = ScoreService.SessionsInHour(FirstHour + i);
                if (counts[i] > peak) { peak = counts[i]; peakHour = i; }
            }
            int maxCount = Mathf.Max(peak, 5);

            int nowHour = System.DateTime.Now.Hour;
            for (int i = 0; i < Hours; i++)
            {
                bool isPeak = i == peakHour && peak > 0;
                bool isNow = FirstHour + i == nowHour;
                var col = isPeak ? GoldC : Rose;
                var root = new GameObject("Bar" + i).transform;
                root.SetParent(transform, false);
                root.localPosition = new Vector3(-6f + i * 1.1f, 0.1f, 0);
                var bar = new GameObject("Grow").transform;
                bar.SetParent(root, false);
                Geo.Prim(PrimitiveType.Cube, bar, new Vector3(0, 0.5f, 0), new Vector3(0.75f, 1f, 0.75f), Mats.Glow(col, isPeak ? 2f : 0.9f));
                bars.Add(bar);
                barTargets.Add(0.08f + counts[i] / (float)maxCount * 4.5f);
                Geo.WorldText(root, $"{FirstHour + i}:00", new Vector3(0, -0.02f, -0.6f), new Vector2(120, 40), 26, isNow ? Color.white : new Color(1, 1, 1, 0.55f))
                    .transform.parent.localRotation = Quaternion.Euler(60f, 0, 0);
                var cnt = Geo.WorldText(root, counts[i].ToString(), new Vector3(0, barTargets[i] + 0.45f, 0), new Vector2(100, 50), 40, col);
                cnt.transform.parent.gameObject.AddComponent<Billboard>();
                if (isNow) Kit.Ring(root, 0.6f, Mats.Additive(Color.white, 0.8f), 0.03f, 32).transform.localPosition = new Vector3(0, 0.02f, 0);
            }

            pulse = Geo.Line(transform, Mats.Additive(Rose, 2.4f), 0.06f);
            pulse.useWorldSpace = false;
            pulse.positionCount = PulsePoints;

            orb = new GameObject("EnergyOrb").transform;
            orb.SetParent(transform, false);
            orb.localPosition = new Vector3(8.4f, 2.4f, 0.5f);
            orbMat = Mats.Unique(Mats.Glow(Rose, 2f));
            Geo.Prim(PrimitiveType.Sphere, orb, Vector3.zero, Vector3.one * 1.6f, orbMat);
            Geo.Prim(PrimitiveType.Sphere, orb, Vector3.zero, Vector3.one * 2.3f, Mats.Additive(Rose, 0.15f));
            Kit.Ring(orb, 1.5f, Mats.Additive(Rose, 0.7f), 0.03f);
            orbLight = Geo.PointLight(orb, Vector3.zero, Rose, 3f, 8f);
            Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(8.4f, 0.6f, 0.5f), new Vector3(1.4f, 0.6f, 1.4f), Mats.Lit(new Color(0.12f, 0.08f, 0.12f), 0.6f));

            energy = Mathf.Clamp01(ScoreService.RecentSessions(30f) / 10f);

            // most played
            string most = "—"; int mostN = 0;
            foreach (var e in Catalog.All) { int n = ScoreService.Plays(e.Id); if (n > mostN) { mostN = n; most = e.Title; } }

            TitleBlock(Title, "The booth's activity today: sessions per hour and live crowd energy.");
            StatCard(new Vector2(0, 0), new Vector2(40, 40), "Riders today", "", 260, Rose).text = total.ToString();
            StatCard(new Vector2(0, 0), new Vector2(320, 40), "Peak hour", "", 260, GoldC).text = peak > 0 ? $"{FirstHour + peakHour}:00" : "—";
            var mp = StatCard(new Vector2(0, 0), new Vector2(600, 40), "Most played", "", 480, Rose);
            mp.text = most; mp.fontSize = 34;
            energyText = StatCard(new Vector2(1, 0), new Vector2(-40, 40), "Crowd energy", "%", 280, Rose);
        }

        Text energyText;

        static float Ecg(float p)
        {
            // one heartbeat shape over phase 0..1
            if (p < 0.1f) return Mathf.Sin(p / 0.1f * Mathf.PI) * 0.12f;
            if (p < 0.16f) return -0.15f * Mathf.Sin((p - 0.1f) / 0.06f * Mathf.PI);
            if (p < 0.22f) return Mathf.Sin((p - 0.16f) / 0.06f * Mathf.PI) * 1f;
            if (p < 0.28f) return -0.3f * Mathf.Sin((p - 0.22f) / 0.06f * Mathf.PI);
            if (p < 0.45f) return Mathf.Sin((p - 0.3f) / 0.15f * Mathf.PI) * 0.22f;
            return 0f;
        }

        public override void Tick(in MotionState m, float dt)
        {
            orbit += dt * 6f;
            float a = (Mathf.Sin(orbit * Mathf.Deg2Rad) * 18f) * Mathf.Deg2Rad;
            Cam.transform.position = new Vector3(Mathf.Sin(a) * 15f + 1.2f, 6.5f, -Mathf.Cos(a) * 15f);
            Cam.transform.LookAt(new Vector3(1.2f, 2.2f, 0));

            // a rider pedalling here adds to the crowd energy
            float live = Mathf.Clamp01(energy + m.Watts / 400f);
            bpm = Kit.Damp(bpm, 60f + live * 100f, 1f, dt);
            beatClock += bpm / 60f * dt;

            grow = Mathf.MoveTowards(grow, 1f, dt * 0.7f);
            for (int i = 0; i < bars.Count; i++)
                bars[i].localScale = new Vector3(1f, barTargets[i] * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(grow * 1.4f - i * 0.04f)), 1f);

            // scrolling ECG trace above the bars
            System.Array.Copy(trace, 1, trace, 0, PulsePoints - 1);
            trace[PulsePoints - 1] = Ecg(Mathf.Repeat(beatClock, 1f)) * (0.6f + live);
            for (int i = 0; i < PulsePoints; i++)
                pulse.SetPosition(i, new Vector3(-6.8f + i / (float)(PulsePoints - 1) * 13.6f, 5.6f + trace[i] * 1.2f, 0.2f));

            float beat = Mathf.Max(0f, Ecg(Mathf.Repeat(beatClock, 1f)));
            orb.localScale = Vector3.one * (0.8f + live * 0.5f + beat * 0.15f);
            orbMat.SetColor("_EmissionColor", Color.Lerp(Rose, GoldC, live) * (1.2f + beat * 3f + live * 2f));
            orbLight.intensity = 2f + beat * 4f;
            energyText.text = (live * 100f).ToString("0");
        }
    }
}
