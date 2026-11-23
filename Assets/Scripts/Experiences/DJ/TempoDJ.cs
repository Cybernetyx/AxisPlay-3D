using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// A DJ deck in 3D. Cadence sets the BPM of a synthesised beat; the vinyl spins,
    /// the pitch fader moves and a wall of EQ columns pumps on every beat.
    /// Keep your BPM in the target zone (it changes every 10 s) to score.
    /// </summary>
    public class TempoDJ : Experience
    {
        public override string Title => "Tempo DJ";

        static readonly int[] Targets = { 100, 120, 140, 126 };
        const float SegmentSeconds = 10f, Zone = 8f;
        static readonly Color Purple = new Color(0.75f, 0.3f, 1f);

        Transform platterL, platterR, fader, arm;
        readonly List<Transform> columns = new List<Transform>();
        readonly List<Material> columnMats = new List<Material>();
        readonly float[] levels = new float[16];
        Light[] lights;
        AudioSource sound;
        float bpm, beatPhase, timer, inZone, pulse, spinL, spinR;
        int beatCount;
        bool started;
        Text bpmText, targetText, scoreText, timeText, grooveText;
        RectTransform zoneBar;

        public override void Begin()
        {
            App.Env(new Color(0.04f, 0.01f, 0.07f), new Color(0.2f, 0.12f, 0.3f), 18f, 60f);
            App.Sun.intensity = 0.25f;
            Cam.fieldOfView = 48f;
            Cam.transform.SetPositionAndRotation(new Vector3(0, 6.2f, -8.5f), Quaternion.Euler(30f, 0, 0));

            Geo.Prim(PrimitiveType.Plane, transform, Vector3.zero, Vector3.one * 6f, Mats.Lit(new Color(0.04f, 0.03f, 0.06f), 0.9f));
            // deck body
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.9f, 0), new Vector3(9f, 0.5f, 3.6f), Mats.Lit(new Color(0.08f, 0.08f, 0.11f), 0.6f));
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.6f, -1.82f), new Vector3(9.05f, 0.06f, 0.06f), Mats.Glow(Purple, 2.5f));
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.45f, 0), new Vector3(8.6f, 0.9f, 3.2f), Mats.Lit(new Color(0.05f, 0.05f, 0.07f)));

            platterL = Platter(new Vector3(-2.4f, 1.16f, 0), new Color(1f, 0.35f, 0.6f));
            platterR = Platter(new Vector3(2.4f, 1.16f, 0), new Color(0.3f, 0.85f, 1f));

            // tonearm on the left deck
            arm = new GameObject("Arm").transform;
            arm.SetParent(transform, false);
            arm.localPosition = new Vector3(-0.6f, 1.3f, 1.1f);
            Geo.Prim(PrimitiveType.Cylinder, arm, Vector3.zero, new Vector3(0.25f, 0.1f, 0.25f), Mats.Lit(new Color(0.6f, 0.6f, 0.65f), 0.8f));
            Geo.Prim(PrimitiveType.Cube, arm, new Vector3(-0.5f, 0.08f, -0.55f), new Vector3(0.05f, 0.05f, 1.5f), Mats.Lit(new Color(0.8f, 0.8f, 0.85f), 0.8f), rot: Quaternion.Euler(0, 40, 0));

            // mixer + pitch fader
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0, 1.16f, 0), new Vector3(1.6f, 0.05f, 3f), Mats.Lit(new Color(0.12f, 0.12f, 0.16f), 0.5f));
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0.4f, 1.2f, 0), new Vector3(0.08f, 0.02f, 2.2f), Mats.Glow(Purple, 1.2f));
            fader = Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0.4f, 1.27f, 0), new Vector3(0.4f, 0.12f, 0.22f), Mats.Lit(new Color(0.9f, 0.9f, 0.95f), 0.7f)).transform;
            for (int i = 0; i < 4; i++)
                Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(-0.4f, 1.22f, -1f + i * 0.65f), new Vector3(0.22f, 0.05f, 0.22f), Mats.Glow(i % 2 == 0 ? Purple : new Color(0.3f, 0.85f, 1f), 1.5f));

            // EQ wall
            var wall = new GameObject("EQ").transform;
            wall.SetParent(transform, false);
            wall.localPosition = new Vector3(0, 0, 4.5f);
            for (int i = 0; i < 16; i++)
            {
                float hue = Mathf.Lerp(0.78f, 0.52f, i / 15f);
                var mat = Mats.Unique(Mats.Glow(Color.HSVToRGB(hue, 0.8f, 1f), 1.5f));
                columnMats.Add(mat);
                var col = new GameObject("Col" + i).transform;
                col.SetParent(wall, false);
                col.localPosition = new Vector3(-7.5f + i, 0, 0);
                Geo.Prim(PrimitiveType.Cube, col, new Vector3(0, 0.5f, 0), new Vector3(0.75f, 1f, 0.75f), mat);
                columns.Add(col);
            }
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0, 3f, 5.2f), new Vector3(18f, 6f, 0.2f), Mats.Lit(new Color(0.03f, 0.02f, 0.05f)));

            lights = new Light[3];
            for (int i = 0; i < 3; i++)
            {
                var l = new GameObject("Beam" + i).AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.transform.localPosition = new Vector3(-5f + i * 5f, 7f, 2f);
                l.transform.localRotation = Quaternion.Euler(75f, 0, 0);
                l.type = LightType.Spot;
                l.spotAngle = 40f;
                l.range = 14f;
                l.intensity = 0f;
                lights[i] = l;
            }

            sound = Kit.Audio(transform);

            TitleBlock(Title, "Your cadence sets the BPM. Hold it in the green zone to score.");
            bpmText = StatCard(new Vector2(0, 0.5f), new Vector2(40, 140), "Your BPM", "", 300, Purple);
            targetText = StatCard(new Vector2(0, 0.5f), new Vector2(40, -10), "Target", "BPM", 300, new Color(0.4f, 1f, 0.6f));
            scoreText = StatCard(new Vector2(1, 0.5f), new Vector2(-40, 140), "In the groove", "%", 280, new Color(0.4f, 1f, 0.6f));
            timeText = StatCard(new Vector2(1, 0.5f), new Vector2(-40, -10), "Time left", "s", 280, Color.white);

            // zone meter: 60..180 BPM with the target window marked
            var meter = Hud.Panel(Hud.Layer, new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(1000, 26), new Color(1, 1, 1, 0.1f));
            zoneBar = Hud.Panel(meter.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 26), new Color(0.4f, 1f, 0.6f, 0.45f)).rectTransform;
            grooveText = Hud.Label(Hud.Layer, "", 40, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(1200, 60), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Hint(null);
            grooveText.text = "PEDAL TO DROP THE BEAT";
            meterRect = meter.rectTransform;
            needle = Hud.Panel(meter.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(8, 50), Color.white).rectTransform;
        }

        RectTransform meterRect, needle;

        Transform Platter(Vector3 pos, Color c)
        {
            var p = new GameObject("Platter").transform;
            p.SetParent(transform, false);
            p.localPosition = pos;
            Geo.Prim(PrimitiveType.Cylinder, transform, pos - new Vector3(0, 0.03f, 0), new Vector3(3.3f, 0.03f, 3.3f), Mats.Lit(new Color(0.55f, 0.55f, 0.6f), 0.85f));
            Geo.Prim(PrimitiveType.Cylinder, p, Vector3.zero, new Vector3(3.1f, 0.03f, 3.1f), Mats.Lit(new Color(0.02f, 0.02f, 0.025f), 0.95f));
            Geo.Prim(PrimitiveType.Cylinder, p, new Vector3(0, 0.035f, 0), new Vector3(1f, 0.01f, 1f), Mats.Glow(c, 1.2f));
            Geo.Prim(PrimitiveType.Cube, p, new Vector3(0.25f, 0.045f, 0), new Vector3(0.3f, 0.01f, 0.08f), Mats.Lit(Color.white));
            var groove = Mats.Additive(new Color(0.6f, 0.6f, 0.7f), 0.25f);
            for (int i = 0; i < 6; i++)
            {
                var r = Kit.Ring(p, 0.65f + i * 0.13f, groove, 0.012f, 64);
                r.transform.localPosition = new Vector3(0, 0.04f, 0);
            }
            // shine streak
            Geo.Prim(PrimitiveType.Cube, p, new Vector3(0, 0.045f, 0.9f), new Vector3(0.06f, 0.005f, 0.6f), Mats.Additive(Color.white, 0.4f));
            return p;
        }

        public override void Tick(in MotionState m, float dt)
        {
            float rawBpm = m.Cadence < 15f ? 0f : Mathf.Clamp(m.Cadence * 1.4f, 60f, 190f);
            bpm = Kit.Damp(bpm, rawBpm, 3f, dt);
            int seg = Mathf.Min(Targets.Length - 1, (int)(timer / SegmentSeconds));
            int target = Targets[seg];
            bool groove = bpm > 0f && Mathf.Abs(bpm - target) <= Zone;

            if (!Finished)
            {
                if (!started && bpm > 40f) started = true;
                if (started)
                {
                    timer += dt;
                    if (groove) inZone += dt;
                    float total = SegmentSeconds * Targets.Length;
                    if (timer >= total)
                    {
                        float pct = inZone / total * 100f;
                        int rank = ScoreService.Record("dj", pct, lowerIsBetter: false);
                        Finish($"{pct:0}% in the groove", rank == 1 ? "NEW BEST DJ SET!" : $"#{rank} today");
                    }
                }
            }

            // beat clock
            if (bpm > 40f)
            {
                beatPhase += bpm / 60f * dt;
                if (beatPhase >= 1f)
                {
                    beatPhase -= 1f;
                    beatCount++;
                    sound.PlayOneShot(Kit.Kick, 0.9f);
                    if (beatCount % 2 == 0) sound.PlayOneShot(Kit.Clap, 0.5f);
                    pulse = 1f;
                    for (int i = 0; i < levels.Length; i++)
                        levels[i] = Mathf.Max(levels[i], 0.3f + Random.value * (groove ? 1f : 0.6f) * (1f - Mathf.Abs(i - 7.5f) / 12f));
                }
                if (beatPhase > 0.5f && beatPhase - bpm / 60f * dt <= 0.5f) sound.PlayOneShot(Kit.Hat, 0.6f);
            }
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 3f);

            for (int i = 0; i < columns.Count; i++)
            {
                levels[i] = Mathf.MoveTowards(levels[i], 0.05f, dt * 1.3f);
                columns[i].localScale = new Vector3(1f, 0.2f + levels[i] * 6f, 1f);
                columnMats[i].SetColor("_EmissionColor", Color.HSVToRGB(Mathf.Lerp(0.78f, 0.52f, i / 15f), 0.8f, 1f) * (1f + pulse * 2f));
            }
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].intensity = pulse * 30f;
                lights[i].color = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.1f + i * 0.33f, 1f), 0.7f, 1f);
                lights[i].transform.localRotation = Quaternion.Euler(70f, Mathf.Sin(Time.time + i) * 30f, 0);
            }

            spinL += bpm / 120f * 200f * dt;
            spinR += bpm / 120f * 200f * dt * 0.98f;
            platterL.localRotation = Quaternion.Euler(0, spinL, 0);
            platterR.localRotation = Quaternion.Euler(0, spinR, 0);
            fader.localPosition = new Vector3(0.4f, 1.27f, Mathf.Lerp(-1f, 1f, Mathf.InverseLerp(60f, 190f, bpm)));
            arm.localRotation = Quaternion.Euler(0, bpm > 0 ? -18f : 0f, 0);
            Cam.transform.position = new Vector3(Mathf.Sin(Time.time * 0.2f) * 1.2f, 6.2f + pulse * 0.05f, -8.5f);

            bpmText.text = bpm < 1f ? "--" : bpm.ToString("0");
            targetText.text = target.ToString();
            scoreText.text = (timer > 0 ? inZone / Mathf.Max(timer, 0.01f) * 100f : 0f).ToString("0");
            timeText.text = Mathf.Max(0f, SegmentSeconds * Targets.Length - timer).ToString("0");
            if (started) grooveText.text = bpm < 1f ? "KEEP PEDALLING!" : groove ? "IN THE GROOVE!" : bpm < target ? "PEDAL FASTER" : "PEDAL SLOWER";
            grooveText.color = groove ? new Color(0.4f, 1f, 0.6f) : Color.white;

            float w = meterRect.rect.width;
            float Map(float b) => Mathf.InverseLerp(60f, 190f, b) * w;
            zoneBar.anchoredPosition = new Vector2(Map(target - Zone), 0);
            zoneBar.sizeDelta = new Vector2(Map(target + Zone) - Map(target - Zone), 26);
            needle.anchoredPosition = new Vector2(Map(Mathf.Max(bpm, 60f)) - 4f, 0);
        }
    }
}
