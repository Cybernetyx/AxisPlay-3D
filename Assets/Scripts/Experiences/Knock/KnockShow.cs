using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// A fist knocks on a 3D table top; every knock sends shockwave rings across it,
    /// lights a tile and builds a combo. Knock as many times as you can in 15 s.
    /// The accelerometer detects real knocks; a tap on the screen also counts.
    /// </summary>
    public class KnockShow : Experience
    {
        public override string Title => "Knock to Interact";

        const float Seconds = 15f;
        static readonly Color Red = new Color(1f, 0.35f, 0.3f);

        Transform fist;
        float fistT = 1f, timer, lastKnock = -10f, comboTimer;
        int knocks, combo, bestCombo;
        bool started;
        readonly List<LineRenderer> waves = new List<LineRenderer>();
        readonly List<float> waveAge = new List<float>();
        readonly List<Material> waveMats = new List<Material>();
        readonly List<Material> tiles = new List<Material>();
        readonly float[] tileGlow = new float[24];
        int waveNext;
        Vector3 hitPoint;
        Light flash;
        ParticleSystem sparks;
        Text countText, timeText, comboText;

        public override void Begin()
        {
            App.Env(new Color(0.05f, 0.02f, 0.03f), new Color(0.25f, 0.18f, 0.2f), 14f, 40f);
            App.Sun.intensity = 0.4f;
            App.Sun.transform.rotation = Quaternion.Euler(55f, -20f, 0);
            Cam.fieldOfView = 42f;
            Cam.transform.SetPositionAndRotation(new Vector3(0, 7.2f, -7.5f), Quaternion.Euler(42f, 0, 0));

            Geo.Prim(PrimitiveType.Plane, transform, new Vector3(0, -1.2f, 0), Vector3.one * 5f, Mats.Lit(new Color(0.06f, 0.04f, 0.05f), 0.5f));
            // table
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0, -0.15f, 0), new Vector3(10f, 0.3f, 6f), Mats.Lit(new Color(0.28f, 0.17f, 0.11f), 0.55f));
            foreach (var x in new[] { -4.5f, 4.5f })
            foreach (var z in new[] { -2.5f, 2.5f })
                Geo.Prim(PrimitiveType.Cube, transform, new Vector3(x, -0.75f, z), new Vector3(0.35f, 1.2f, 0.35f), Mats.Lit(new Color(0.2f, 0.12f, 0.08f)));
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.0f, -3.01f), new Vector3(10.02f, 0.04f, 0.04f), Mats.Glow(Red, 2f));

            // light-up tiles inlaid in the table
            for (int i = 0; i < 24; i++)
            {
                int gx = i % 6, gz = i / 6;
                var mat = Mats.Unique(Mats.Glow(Color.HSVToRGB(Mathf.Repeat(0.98f + i * 0.04f, 1f), 0.7f, 1f), 0.05f));
                tiles.Add(mat);
                Geo.Prim(PrimitiveType.Cube, transform, new Vector3(-3.75f + gx * 1.5f, 0.005f, -1.9f + gz * 1.3f), new Vector3(1.3f, 0.01f, 1.1f), mat);
            }

            // shockwave pool
            for (int i = 0; i < 10; i++)
            {
                var mat = Mats.Unique(Mats.Additive(Red, 0f));
                var r = Kit.Ring(transform, 1f, mat, 0.08f, 64);
                r.gameObject.SetActive(false);
                waves.Add(r); waveAge.Add(99f); waveMats.Add(mat);
            }

            // fist: palm + four fingers + thumb
            fist = new GameObject("Fist").transform;
            fist.SetParent(transform, false);
            var skin = Mats.Lit(new Color(0.95f, 0.72f, 0.58f), 0.3f);
            Geo.Prim(PrimitiveType.Cube, fist, new Vector3(0, 0.55f, 0.15f), new Vector3(0.9f, 0.7f, 0.75f), skin);
            for (int i = 0; i < 4; i++)
                Geo.Prim(PrimitiveType.Cube, fist, new Vector3(-0.33f + i * 0.22f, 0.18f, -0.08f), new Vector3(0.2f, 0.3f, 0.42f), skin, rot: Quaternion.Euler(10f, 0, 0));
            Geo.Prim(PrimitiveType.Cube, fist, new Vector3(-0.52f, 0.38f, -0.15f), new Vector3(0.22f, 0.22f, 0.45f), skin, rot: Quaternion.Euler(0, 25f, 0));
            Geo.Prim(PrimitiveType.Cylinder, fist, new Vector3(0, 1.5f, 0.3f), new Vector3(0.65f, 0.65f, 0.6f), Mats.Lit(new Color(0.15f, 0.2f, 0.35f)), rot: Quaternion.Euler(-12f, 0, 0)); // sleeve
            fist.localPosition = new Vector3(0, 1.4f, 0);

            flash = Geo.PointLight(transform, new Vector3(0, 2f, 0), Red, 0f, 9f);
            sparks = Kit.Particles(fist, new Color(1f, 0.7f, 0.4f), 0f, 0.08f, 0.6f, 5f, ParticleSystemShapeType.Hemisphere, Vector3.one * 0.3f);

            TitleBlock(Title, "Knock on the table as many times as you can in 15 seconds.");
            countText = StatCard(new Vector2(0, 0.5f), new Vector2(40, 60), "Knocks", "", 260, Red);
            timeText = StatCard(new Vector2(1, 0.5f), new Vector2(-40, 60), "Time left", "s", 260, Color.white);
            comboText = Hud.Label(Hud.Layer, "", 64, new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(900, 100), TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.4f), FontStyle.Bold);
            Hint("KNOCK ON THE TABLE TO START");
        }

        void Knock()
        {
            knocks++;
            combo = Time.time - lastKnock < 0.9f ? combo + 1 : 1;
            bestCombo = Mathf.Max(bestCombo, combo);
            lastKnock = Time.time;
            comboTimer = 0.8f;
            fistT = 0f;
            hitPoint = new Vector3(Random.Range(-3.5f, 3.5f), 0.02f, Random.Range(-1.8f, 2f));

            var w = waves[waveNext];
            w.transform.localPosition = hitPoint;
            w.gameObject.SetActive(true);
            waveAge[waveNext] = 0f;
            waveNext = (waveNext + 1) % waves.Count;

            int tile = Mathf.Clamp(Mathf.FloorToInt((hitPoint.x + 4.5f) / 1.5f), 0, 5) + Mathf.Clamp(Mathf.FloorToInt((hitPoint.z + 2.55f) / 1.3f), 0, 3) * 6;
            tileGlow[tile] = 1f;
            Kit.Burst(sparks, 30);
            App.Audio.PlayOneShot(Kit.Kick, 0.8f);
            if (combo >= 3) App.Audio.PlayOneShot(Kit.Blip, 0.4f);
        }

        public override void Tick(in MotionState m, float dt)
        {
            bool tap = Input.GetMouseButtonDown(0) && !Hud.PointerOverUI();
            bool knock = m.Knock || tap;

            if (!Finished)
            {
                if (knock)
                {
                    if (!started) { started = true; Hint(null); }
                    Knock();
                }
                if (started)
                {
                    timer += dt;
                    if (timer >= Seconds)
                    {
                        int rank = ScoreService.Record("knock", knocks, lowerIsBetter: false);
                        Finish($"{knocks} knocks in {Seconds:0} s", $"Best combo x{bestCombo} · " + (rank == 1 ? "NEW RECORD!" : $"#{rank} today"));
                    }
                }
            }

            // fist animation: fast strike down, slower lift
            fistT += dt;
            float down = fistT < 0.08f ? fistT / 0.08f : Mathf.Clamp01(1f - (fistT - 0.08f) / 0.3f);
            Vector3 rest = new Vector3(hitPoint.x * 0.5f, 1.4f, hitPoint.z * 0.4f - 0.3f);
            Vector3 hit = new Vector3(hitPoint.x, 0.02f, hitPoint.z);
            fist.localPosition = Vector3.Lerp(rest, hit, down);
            fist.localRotation = Quaternion.Euler(down * 15f, 0, 0);

            for (int i = 0; i < waves.Count; i++)
            {
                if (waveAge[i] > 1.2f) { if (waves[i].gameObject.activeSelf) waves[i].gameObject.SetActive(false); continue; }
                waveAge[i] += dt;
                float t = waveAge[i] / 1.2f;
                waves[i].transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 6f, t);
                waves[i].widthMultiplier = Mathf.Lerp(0.15f, 0.02f, t);
                waveMats[i].SetColor("_BaseColor", Red * (1f - t) * 2.5f);
            }
            for (int i = 0; i < tiles.Count; i++)
            {
                tileGlow[i] = Mathf.MoveTowards(tileGlow[i], 0f, dt * 0.8f);
                tiles[i].SetColor("_EmissionColor", Color.HSVToRGB(Mathf.Repeat(0.98f + i * 0.04f, 1f), 0.7f, 1f) * (0.05f + tileGlow[i] * 3f));
            }
            flash.intensity = Mathf.Max(0f, 1f - fistT * 4f) * 8f;

            comboTimer -= dt;
            comboText.text = combo >= 3 && comboTimer > 0f ? $"COMBO x{combo}!" : "";
            countText.text = knocks.ToString();
            timeText.text = Mathf.Max(0f, Seconds - timer).ToString("0");
        }
    }
}
