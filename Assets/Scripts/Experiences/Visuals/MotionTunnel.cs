using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// A tunnel of glowing rings and particle streaks rushing toward you. Speed follows
    /// your power, every pedal stroke sends a pulse of light down the tunnel, and leaning
    /// bends it. 30 s session; the score is the distance travelled.
    /// </summary>
    public class MotionTunnel : Experience
    {
        public override string Title => "Motion-Driven Visuals";

        const int RingCount = 34;
        const float Spacing = 3f, Seconds = 30f;

        readonly List<Transform> rings = new List<Transform>();
        readonly List<Material> ringMats = new List<Material>();
        readonly List<float> ringPulse = new List<float>();
        ParticleSystem streaks;
        ParticleSystem.EmissionModule streakEm;
        Transform core;
        Material coreMat;
        float travel, speed, timer, hue, lastCrank, bend;
        bool started;
        int strokes;
        Text speedText, distText, timeText;

        public override void Begin()
        {
            App.Env(new Color(0.0f, 0.0f, 0.02f), new Color(0.1f, 0.1f, 0.15f), 40f, 110f);
            App.Sun.intensity = 0f;
            Cam.fieldOfView = 70f;
            Cam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            for (int i = 0; i < RingCount; i++)
            {
                var root = new GameObject("Ring" + i).transform;
                root.SetParent(transform, false);
                var mat = Mats.Unique(Mats.Additive(Color.white, 1f));
                Kit.Ring(root, 4f, mat, 0.12f, 48, flat: false);
                // dots around the ring for a particle feel
                for (int d = 0; d < 12; d++)
                {
                    float a = d / 12f * Mathf.PI * 2f;
                    Geo.Prim(PrimitiveType.Quad, root, new Vector3(Mathf.Cos(a) * 4.4f, Mathf.Sin(a) * 4.4f, 0), Vector3.one * 0.18f, mat);
                }
                rings.Add(root); ringMats.Add(mat); ringPulse.Add(0f);
            }

            core = Geo.Prim(PrimitiveType.Sphere, transform, new Vector3(0, 0, RingCount * Spacing), Vector3.one * 6f, Mats.Unique(Mats.Additive(new Color(0.6f, 0.3f, 1f), 1.2f))).transform;
            coreMat = core.GetComponent<Renderer>().sharedMaterial;

            var em = new GameObject("Streaks").transform;
            em.SetParent(transform, false);
            em.localPosition = new Vector3(0, 0, 90f);
            em.localRotation = Quaternion.Euler(0, 180f, 0);
            streaks = Kit.Particles(em, new Color(0.8f, 0.6f, 1f), 0f, 0.07f, 2.5f, 40f, ParticleSystemShapeType.Circle, Vector3.one * 3.5f);
            var r = streaks.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 8f;
            r.velocityScale = 0.04f;
            var sh = streaks.shape;
            sh.radius = 3.5f;
            sh.radiusThickness = 0.2f;
            streakEm = streaks.emission;

            TitleBlock(Title, "Pedal to fly. Every pedal stroke pulses the tunnel; lean to steer it.");
            speedText = StatCard(new Vector2(0, 0), new Vector2(40, 40), "Speed", "km/h", 260, new Color(0.7f, 0.4f, 1f));
            distText = StatCard(new Vector2(1, 0), new Vector2(-40, 40), "Distance", "m", 260, new Color(0.7f, 0.4f, 1f));
            timeText = StatCard(new Vector2(1, 1), new Vector2(-40, -40), "Time left", "s", 220, Color.white);
            Hint("PEDAL TO ENTER THE TUNNEL");
        }

        public override void Tick(in MotionState m, float dt)
        {
            float target = 2f + Kit.SpeedFromWatts(m.Watts) * 3.2f;
            speed = Kit.Damp(speed, target, 1.5f, dt);
            travel += speed * dt;
            hue = Mathf.Repeat(hue + dt * (0.02f + m.Cadence * 0.0006f), 1f);
            bend = Kit.Damp(bend, m.Lean, 2f, dt);

            if (!Finished)
            {
                if (!started && m.Cadence > 10f) { started = true; Hint(null); }
                if (started)
                {
                    timer += dt;
                    if (timer >= Seconds)
                    {
                        float metres = travel / 3.2f;
                        int rank = ScoreService.Record("visuals", metres, lowerIsBetter: false);
                        Finish($"{metres:0} m through the tunnel", $"{strokes} pedal strokes · " + (rank == 1 ? "NEW RECORD!" : $"#{rank} today"));
                    }
                }
            }

            // a pedal stroke = crank passing 0° (and 180°); launch a pulse from the nearest ring
            bool stroke = m.Cadence > 10f && (Mathf.Repeat(m.CrankAngle, 180f) < Mathf.Repeat(lastCrank, 180f));
            lastCrank = m.CrankAngle;
            if (stroke) { strokes++; ringPulse[NearestRing()] = 1f; }

            float far = RingCount * Spacing;
            for (int i = 0; i < rings.Count; i++)
            {
                float z = Mathf.Repeat(i * Spacing - travel, far);
                float t = z / far;
                // bend the tunnel with lean (quadratic with depth)
                float x = bend * z * z * 0.006f;
                float y = Mathf.Sin(Time.time * 0.6f + z * 0.05f) * z * 0.02f;
                rings[i].localPosition = new Vector3(x, y, z + 1.5f);
                rings[i].localRotation = Quaternion.Euler(0, 0, travel * 2f + i * 7f);
                // pulse travels down: hand it to the next ring as rings pass
                ringPulse[i] = Mathf.MoveTowards(ringPulse[i], 0f, dt * 2.2f);
                float glow = (1f - t) * 1.4f + ringPulse[i] * 4f + 0.15f;
                var c = Color.HSVToRGB(Mathf.Repeat(hue + i * 0.018f, 1f), 0.75f, 1f);
                ringMats[i].SetColor("_BaseColor", c * glow);
            }
            core.localPosition = new Vector3(bend * far * far * 0.006f, 0, far + 4f);
            coreMat.SetColor("_BaseColor", Color.HSVToRGB(hue, 0.6f, 1f) * (0.8f + m.Cadence * 0.01f));
            streakEm.rateOverTime = speed * 6f;
            var main = streaks.main;
            main.startSpeed = 20f + speed * 2f;
            Cam.transform.localRotation = Quaternion.Euler(0, bend * 6f, -bend * 8f);
            Cam.fieldOfView = Kit.Damp(Cam.fieldOfView, 65f + speed * 0.6f, 2f, dt);

            speedText.text = (speed / 3.2f * 3.6f).ToString("0");
            distText.text = (travel / 3.2f).ToString("0");
            timeText.text = Mathf.Max(0f, Seconds - timer).ToString("0");
        }

        int NearestRing()
        {
            int best = 0; float bz = float.MaxValue;
            for (int i = 0; i < rings.Count; i++)
            {
                float z = rings[i].localPosition.z;
                if (z > 2f && z < bz) { bz = z; best = i; }
            }
            return best;
        }
    }
}
