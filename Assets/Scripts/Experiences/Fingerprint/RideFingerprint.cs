using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Ride for 20 s; every quarter-second a glowing ring is added to a twisting sculpture:
    /// radius from cadence, tilt from lean, colour from power. Ends with a showcase spin
    /// and three scores: Rhythm (steadiness), Power and Balance.
    /// </summary>
    public class RideFingerprint : Experience
    {
        public override string Title => "Ride Fingerprint";

        const float RideSeconds = 20f, Step = 0.25f;
        static readonly Color Teal = new Color(0.25f, 1f, 0.8f);

        Transform sculpture;
        readonly List<float> cad = new List<float>();
        readonly List<float> lean = new List<float>();
        readonly List<float> power = new List<float>();
        float timer, sampleT, twist, orbit, showT = -1f;
        bool started;
        Text timeText, ringsText;
        RectTransform rideBar;

        public override void Begin()
        {
            App.Env(new Color(0.02f, 0.03f, 0.05f), new Color(0.15f, 0.2f, 0.25f), 12f, 40f);
            App.Sun.intensity = 0.3f;
            Cam.fieldOfView = 45f;

            Geo.Prim(PrimitiveType.Plane, transform, Vector3.zero, Vector3.one * 5f, Mats.Lit(new Color(0.03f, 0.04f, 0.06f), 0.85f));
            Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.5f, 0), new Vector3(2.2f, 0.5f, 2.2f), Mats.Lit(new Color(0.1f, 0.12f, 0.15f), 0.6f));
            Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 1.005f, 0), new Vector3(2.3f, 0.01f, 2.3f), Mats.Glow(Teal, 1f));
            Kit.Ring(transform, 3f, Mats.Additive(Teal, 0.4f), 0.03f).transform.localPosition = new Vector3(0, 0.02f, 0);
            Kit.Ring(transform, 3.6f, Mats.Additive(Teal, 0.2f), 0.02f).transform.localPosition = new Vector3(0, 0.02f, 0);

            sculpture = new GameObject("Sculpture").transform;
            sculpture.SetParent(transform, false);
            sculpture.localPosition = new Vector3(0, 1.1f, 0);

            var spot = new GameObject("Spot").AddComponent<Light>();
            spot.transform.SetParent(transform, false);
            spot.transform.localPosition = new Vector3(0, 8f, -1f);
            spot.transform.LookAt(new Vector3(0, 1f, 0));
            spot.type = LightType.Spot;
            spot.spotAngle = 30f;
            spot.range = 14f;
            spot.intensity = 18f;
            spot.color = new Color(0.8f, 1f, 0.95f);
            Geo.PointLight(sculpture, new Vector3(0, 1.5f, 0), Teal, 2f, 6f);

            TitleBlock(Title, "Ride for 20 seconds. Your rhythm, power and balance shape a unique sculpture.");
            timeText = StatCard(new Vector2(0, 0.5f), new Vector2(40, 60), "Time left", "s", 260, Teal);
            ringsText = StatCard(new Vector2(1, 0.5f), new Vector2(-40, 60), "Rings", "", 260, Teal);
            rideBar = ProgressBar(new Vector2(0.5f, 0), new Vector2(0, 60), 900, "Recording your ride", Teal);
            Hint("PEDAL TO START SCULPTING");
        }

        void AddRing(in MotionState m)
        {
            int i = cad.Count;
            cad.Add(m.Cadence); lean.Add(m.Lean); power.Add(m.Watts);
            float r = 0.25f + Mathf.Clamp01(m.Cadence / 120f) * 0.95f + Mathf.Sin(m.CrankAngle * Mathf.Deg2Rad) * 0.04f;
            float hue = Mathf.Lerp(0.5f, 0.92f, Mathf.Clamp01(m.Watts / 350f));
            var col = Color.HSVToRGB(hue, 0.75f, 1f);
            twist += 6f + m.Cadence * 0.08f;
            var ring = Kit.Ring(sculpture, r, Mats.Additive(col, 1.4f), 0.035f, 40);
            ring.transform.localPosition = new Vector3(Mathf.Sin(i * 0.2f) * 0.08f, i * 0.045f, 0);
            ring.transform.localRotation = Quaternion.Euler(m.Lean * 25f, twist, Mathf.Sin(i * 0.3f) * 8f);
            if (i % 8 == 0)
            {
                var bead = Geo.Prim(PrimitiveType.Sphere, sculpture, ring.transform.localPosition + ring.transform.localRotation * new Vector3(r, 0, 0),
                    Vector3.one * 0.08f, Mats.Glow(col, 2.5f));
                bead.name = "Bead";
            }
        }

        static float Mean(List<float> l) { float s = 0; foreach (var v in l) s += v; return l.Count > 0 ? s / l.Count : 0; }
        static float Std(List<float> l) { float mu = Mean(l), s = 0; foreach (var v in l) s += (v - mu) * (v - mu); return l.Count > 1 ? Mathf.Sqrt(s / l.Count) : 0; }

        public override void Tick(in MotionState m, float dt)
        {
            orbit += dt * (showT >= 0f ? 30f : 12f);
            float a = orbit * Mathf.Deg2Rad;
            float h = 1.1f + cad.Count * 0.045f * 0.5f;
            Cam.transform.position = new Vector3(Mathf.Sin(a) * 7.5f, h + 2.2f, -Mathf.Cos(a) * 7.5f);
            Cam.transform.LookAt(new Vector3(0, h, 0));

            if (showT < 0f)
            {
                if (!started && m.Cadence > 10f) { started = true; Hint(null); }
                if (started)
                {
                    timer += dt;
                    sampleT += dt;
                    while (sampleT >= Step) { sampleT -= Step; AddRing(m); }
                    if (timer >= RideSeconds) { showT = 0f; Hint("YOUR RIDE FINGERPRINT"); App.Audio.PlayOneShot(Kit.Clap); }
                }
            }
            else
            {
                showT += dt;
                sculpture.localRotation = Quaternion.Euler(0, showT * 40f, 0);
                if (showT > 4f && !Finished)
                {
                    float avgCad = Mean(cad);
                    int rhythm = Mathf.RoundToInt(Mathf.Clamp(100f - Std(cad) * 2.2f - (avgCad < 30f ? 40f : 0f), 0f, 100f));
                    int pwr = Mathf.RoundToInt(Mean(power));
                    float absLean = 0; foreach (var l in lean) absLean += Mathf.Abs(l);
                    int balance = Mathf.RoundToInt(Mathf.Clamp(100f - absLean / Mathf.Max(lean.Count, 1) * 100f, 0f, 100f));
                    ScoreService.Record("fingerprint", rhythm, lowerIsBetter: false);
                    Finish($"Rhythm {rhythm} · Power {pwr} W · Balance {balance}", $"{cad.Count} rings · average {avgCad:0} rpm");
                }
            }

            timeText.text = Mathf.Max(0f, RideSeconds - timer).ToString("0");
            ringsText.text = cad.Count.ToString();
            Hud.SetFill(rideBar, timer / RideSeconds);
        }
    }
}
