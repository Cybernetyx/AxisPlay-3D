using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// A gallery with a floating Polaroid under a spotlight. Lean to pick a theme card,
    /// then pedal: energy streams into the Polaroid as particles and the picture develops.
    /// The artwork is generated on the device from the theme and your ride; the cloud AI
    /// version (phase 5) can replace Generate() without changing the scene.
    /// </summary>
    public class DevelopArt : Experience
    {
        public override string Title => "Pedal-to-Develop Art";

        static readonly string[] Themes = { "Sunset City", "Ocean Dream", "Neon Forest" };
        static readonly Color[] ThemeColors = { new Color(1f, 0.55f, 0.3f), new Color(0.3f, 0.7f, 1f), new Color(0.5f, 1f, 0.45f) };
        static readonly Color Pink = new Color(1f, 0.45f, 0.85f);
        const float JoulesToDevelop = 5000f; // ~25 s at 200 W

        int theme = 1;
        bool locked;
        float energy, developedT = -1f, cadenceSum, samples;
        Transform polaroid;
        Material pictureMat, milkMat;
        readonly List<Transform> cards = new List<Transform>();
        readonly List<Material> cardGlow = new List<Material>();
        ParticleSystem stream;
        ParticleSystem.EmissionModule streamEm;
        Text devText, themeText;
        RectTransform devBar;

        public override void Begin()
        {
            App.Env(new Color(0.06f, 0.05f, 0.07f), new Color(0.22f, 0.2f, 0.24f), 14f, 40f);
            App.Sun.intensity = 0.2f;
            Cam.fieldOfView = 45f;
            Cam.transform.SetPositionAndRotation(new Vector3(0, 2.3f, -7.5f), Quaternion.Euler(4f, 0, 0));

            // gallery room
            var wall = Mats.Lit(new Color(0.16f, 0.15f, 0.17f), 0.1f);
            Geo.Prim(PrimitiveType.Plane, transform, Vector3.zero, Vector3.one * 3f, Mats.Lit(new Color(0.22f, 0.17f, 0.14f), 0.6f));
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0, 4f, 6f), new Vector3(30f, 8f, 0.3f), wall);
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(-12f, 4f, 0), new Vector3(0.3f, 8f, 20f), wall);
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(12f, 4f, 0), new Vector3(0.3f, 8f, 20f), wall);
            // framed pieces on the back wall
            var rnd = new System.Random(2);
            for (int i = 0; i < 4; i++)
            {
                float x = -9f + i * 6f; if (i >= 2) x += 0f;
                if (Mathf.Abs(x) < 3f) continue;
                Geo.Prim(PrimitiveType.Cube, transform, new Vector3(x, 3.6f, 5.8f), new Vector3(2.2f, 1.6f, 0.08f), Mats.Lit(new Color(0.08f, 0.07f, 0.06f)));
                Geo.Prim(PrimitiveType.Quad, transform, new Vector3(x, 3.6f, 5.75f), new Vector3(1.9f, 1.3f, 1f),
                    Mats.Glow(Color.HSVToRGB((float)rnd.NextDouble(), 0.5f, 0.6f), 0.5f));
                Geo.PointLight(transform, new Vector3(x, 5.2f, 5f), new Color(1f, 0.9f, 0.75f), 1.5f, 3f);
            }

            // the Polaroid
            polaroid = new GameObject("Polaroid").transform;
            polaroid.SetParent(transform, false);
            polaroid.localPosition = new Vector3(0, 2.6f, 0);
            Geo.Prim(PrimitiveType.Cube, polaroid, new Vector3(0, -0.15f, 0.03f), new Vector3(2.1f, 2.5f, 0.05f), Mats.Lit(new Color(0.95f, 0.94f, 0.9f), 0.2f));
            pictureMat = Mats.Unique(Mats.Glass(Color.white));
            Geo.Prim(PrimitiveType.Quad, polaroid, new Vector3(0, 0.05f, -0.001f), new Vector3(1.8f, 1.8f, 1f), pictureMat);
            milkMat = Mats.Unique(Mats.Glass(new Color(0.42f, 0.38f, 0.34f, 1f)));
            Geo.Prim(PrimitiveType.Quad, polaroid, new Vector3(0, 0.05f, -0.003f), new Vector3(1.8f, 1.8f, 1f), milkMat);
            themeText = Geo.WorldText(polaroid, "", new Vector3(0, -1.15f, -0.01f), new Vector2(200, 40), 18, new Color(0.2f, 0.2f, 0.25f), TextAnchor.MiddleCenter, FontStyle.Italic);
            themeText.transform.parent.localScale = Vector3.one * 0.01f;

            var spot = new GameObject("Spot").AddComponent<Light>();
            spot.transform.SetParent(transform, false);
            spot.transform.localPosition = new Vector3(0, 7f, -3f);
            spot.transform.LookAt(polaroid.position);
            spot.type = LightType.Spot;
            spot.spotAngle = 28f;
            spot.range = 15f;
            spot.intensity = 30f;
            spot.color = new Color(1f, 0.95f, 0.85f);
            Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.01f, 0), new Vector3(3f, 0.01f, 3f), Mats.Additive(new Color(1f, 0.9f, 0.7f), 0.12f));

            // theme cards, tilted in 3D
            for (int i = 0; i < 3; i++)
            {
                var c = new GameObject("Card" + i).transform;
                c.SetParent(transform, false);
                c.localPosition = new Vector3(-3.4f + i * 3.4f, 0.75f, -2.5f);
                c.localRotation = Quaternion.Euler(55f, (i - 1) * 12f, 0);
                var glow = Mats.Unique(Mats.Glow(ThemeColors[i], 0.5f));
                cardGlow.Add(glow);
                Geo.Prim(PrimitiveType.Cube, c, Vector3.zero, new Vector3(2.4f, 1.1f, 0.04f), Mats.Lit(new Color(0.1f, 0.1f, 0.13f), 0.5f));
                Geo.Prim(PrimitiveType.Cube, c, new Vector3(0, 0, 0.01f), new Vector3(2.5f, 1.2f, 0.03f), glow);
                Geo.WorldText(c, Themes[i].ToUpperInvariant(), new Vector3(0, 0, -0.03f), new Vector2(240, 60), 30, Color.white);
                cards.Add(c);
            }

            // energy stream from the rider into the Polaroid
            var emitter = new GameObject("Stream").transform;
            emitter.SetParent(transform, false);
            emitter.localPosition = new Vector3(0, 0.3f, -4f);
            emitter.LookAt(polaroid.position);
            stream = Kit.Particles(emitter, Pink, 0f, 0.1f, 1.1f, 4.2f, ParticleSystemShapeType.Cone, Vector3.one);
            var sh = stream.shape;
            sh.angle = 6f;
            sh.radius = 0.6f;
            streamEm = stream.emission;

            TitleBlock(Title, "Lean to pick a theme, then pedal to develop your Polaroid.");
            devText = StatCard(new Vector2(1, 0.5f), new Vector2(-40, 60), "Developed", "%", 280, Pink);
            devBar = ProgressBar(new Vector2(0.5f, 0), new Vector2(0, 60), 900, "Developing", Pink);
            Hint("LEAN TO PICK A THEME · PEDAL TO DEVELOP");
            Generate();
        }

        void Generate()
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            var rnd = new System.Random((int)(Time.realtimeSinceStartup * 1000f) + theme * 97 + (int)cadenceSum);
            float rhythm = samples > 0 ? cadenceSum / samples / 90f : 1f;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                Color c;
                if (theme == 0) // Sunset City
                {
                    c = Color.Lerp(new Color(1f, 0.55f, 0.25f), new Color(0.35f, 0.15f, 0.45f), v);
                    float sun = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.38f));
                    if (sun < 0.16f) c = Color.Lerp(new Color(1f, 0.9f, 0.5f), c, sun / 0.16f * 0.5f);
                    float bx = Mathf.Floor(u * 14f);
                    float bh = 0.12f + Mathf.PerlinNoise(bx * 0.7f, 3.3f) * 0.3f * rhythm;
                    if (v < bh) c = new Color(0.12f, 0.07f, 0.16f);
                    if (v < bh && (x / 6 + y / 6) % 3 == 0 && Mathf.PerlinNoise(x * 0.3f, y * 0.3f) > 0.55f) c = new Color(1f, 0.8f, 0.4f);
                }
                else if (theme == 1) // Ocean Dream
                {
                    c = Color.Lerp(new Color(0.05f, 0.2f, 0.45f), new Color(0.55f, 0.85f, 1f), v);
                    float wave = 0.45f + Mathf.Sin(u * 12f * rhythm + Mathf.PerlinNoise(u * 3f, 1f) * 4f) * 0.04f;
                    if (v < wave) c = Color.Lerp(new Color(0.0f, 0.15f, 0.35f), new Color(0.1f, 0.5f, 0.7f), v / wave);
                    if (Mathf.Abs(v - wave) < 0.008f) c = Color.white;
                    float moon = Vector2.Distance(new Vector2(u, v), new Vector2(0.72f, 0.78f));
                    if (moon < 0.08f) c = new Color(1f, 0.98f, 0.9f);
                }
                else // Neon Forest
                {
                    c = Color.Lerp(new Color(0.02f, 0.02f, 0.06f), new Color(0.1f, 0.05f, 0.2f), v);
                    for (int t = 0; t < 7; t++)
                    {
                        float tx = (t + 0.5f) / 7f + Mathf.Sin(t * 12.9f) * 0.04f;
                        float th = 0.35f + Mathf.PerlinNoise(t * 1.7f, 2f) * 0.4f;
                        float halfW = (th - v) * 0.35f;
                        if (v < th && Mathf.Abs(u - tx) < halfW)
                            c = Mathf.Abs(Mathf.Abs(u - tx) - halfW) < 0.01f ? Color.HSVToRGB(0.3f + t * 0.08f, 1f, 1f) : new Color(0.03f, 0.08f, 0.05f);
                    }
                    if (Mathf.PerlinNoise(u * 40f, v * 40f) > 0.82f && v > 0.6f) c = new Color(0.8f, 1f, 0.9f);
                }
                float grain = (float)rnd.NextDouble() * 0.05f;
                px[y * N + x] = new Color(c.r + grain, c.g + grain, c.b + grain, 1f);
            }
            tex.SetPixels32(px);
            tex.Apply();
            pictureMat.SetTexture("_BaseMap", tex);
            pictureMat.SetColor("_BaseColor", Color.white);
            themeText.text = Themes[theme];
        }

        public override void Tick(in MotionState m, float dt)
        {
            if (!locked)
            {
                int pick = m.Lean < -0.35f ? 0 : m.Lean > 0.35f ? 2 : -1;
                if (pick >= 0 && pick != theme) { theme = pick; Generate(); }
                if (m.Watts > 25f) { locked = true; Hint(null); }
            }
            for (int i = 0; i < cards.Count; i++)
            {
                bool on = i == theme;
                cards[i].localScale = Vector3.one * Kit.Damp(cards[i].localScale.x, on ? 1.1f : 0.9f, 8f, dt);
                cardGlow[i].SetColor("_EmissionColor", ThemeColors[i] * (on ? 2.5f : 0.4f));
            }

            if (locked && developedT < 0f)
            {
                energy += m.Watts * dt;
                cadenceSum += m.Cadence; samples++;
                if (energy >= JoulesToDevelop)
                {
                    developedT = 0f;
                    App.Audio.PlayOneShot(Kit.Clap);
                    Hint("YOUR POLAROID IS READY!");
                }
            }
            float dev = Mathf.Clamp01(energy / JoulesToDevelop);
            // developing chemistry: milky brown overlay fades, image saturates
            milkMat.SetColor("_BaseColor", new Color(0.42f, 0.38f, 0.34f, Mathf.Pow(1f - dev, 1.4f)));
            pictureMat.SetColor("_BaseColor", Color.Lerp(new Color(0.7f, 0.65f, 0.6f), Color.white, dev));
            streamEm.rateOverTime = developedT < 0f ? m.Watts * 0.6f : 0f;

            float bob = Mathf.Sin(Time.time * 1.2f) * 0.08f;
            polaroid.localPosition = new Vector3(0, 2.6f + bob, 0);
            polaroid.localRotation = Quaternion.Euler(-4f, Mathf.Sin(Time.time * 0.6f) * 10f - 6f, 4f);

            if (developedT >= 0f)
            {
                developedT += dt;
                polaroid.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, Mathf.Clamp01(developedT));
                if (developedT > 4f)
                {
                    ScoreService.Record("art", 1f, lowerIsBetter: false);
                    Finish("Your Polaroid is developed!", $"Theme: {Themes[theme]} · QR download comes with the AI backend");
                }
            }

            devText.text = (dev * 100f).ToString("0");
            Hud.SetFill(devBar, dev);
        }
    }
}
