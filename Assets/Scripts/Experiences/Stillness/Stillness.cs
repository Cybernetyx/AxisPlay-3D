using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// A frosted ice cube on a pedestal hides a gold prize orb. Stay perfectly still and
    /// the ice clears; moving refreezes it. Clear it within 40 s to free the orb.
    /// </summary>
    public class Stillness : Experience
    {
        public override string Title => "Stillness Challenge";
        public override float IdleTimeout => 120f; // stillness is the point here

        const float NeededSeconds = 10f, TimeLimit = 40f;
        static readonly Color Ice = new Color(0.75f, 0.92f, 1f);
        static readonly Color Gold = new Color(1f, 0.78f, 0.25f);

        Transform cube, frost, orb, rig;
        Material iceMat, frostMat, orbMat;
        Light orbLight;
        ParticleSystem sparkle, burst;
        float clear, timer, orbit, freedT = -1f, shake;
        bool started;
        Text clearText, timeText, stateText;
        RectTransform stillBar;

        public override void Begin()
        {
            App.Env(new Color(0.03f, 0.06f, 0.11f), new Color(0.25f, 0.32f, 0.42f), 12f, 45f);
            App.Sun.color = new Color(0.7f, 0.85f, 1f);
            App.Sun.intensity = 0.5f;
            App.Sun.transform.rotation = Quaternion.Euler(60f, 20f, 0f);
            Cam.fieldOfView = 42f;

            Geo.Prim(PrimitiveType.Plane, transform, Vector3.zero, Vector3.one * 6f, Mats.Lit(new Color(0.05f, 0.08f, 0.12f), 0.85f));
            Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.6f, 0), new Vector3(2.6f, 0.6f, 2.6f), Mats.Lit(new Color(0.12f, 0.14f, 0.2f), 0.6f));
            Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 1.21f, 0), new Vector3(2.7f, 0.015f, 2.7f), Mats.Glow(Ice, 1.2f));
            Kit.Ring(transform, 4f, Mats.Additive(Ice, 0.5f), 0.04f).transform.localPosition = new Vector3(0, 0.02f, 0);

            rig = new GameObject("IceRig").transform;
            rig.SetParent(transform, false);
            rig.localPosition = new Vector3(0, 2.45f, 0);

            orbMat = Mats.Unique(Mats.Glow(Gold, 2.5f));
            orb = Geo.Prim(PrimitiveType.Sphere, rig, Vector3.zero, Vector3.one * 0.8f, orbMat).transform;
            orbLight = Geo.PointLight(orb, Vector3.zero, Gold, 0.5f, 6f);

            frostMat = Mats.Unique(Mats.Glass(new Color(0.9f, 0.96f, 1f, 0.75f)));
            frost = Geo.Prim(PrimitiveType.Cube, rig, Vector3.zero, Vector3.one * 1.9f, frostMat, rot: Quaternion.Euler(0, 20, 0)).transform;
            iceMat = Mats.Unique(Mats.Glass(new Color(0.7f, 0.88f, 1f, 0.35f)));
            cube = Geo.Prim(PrimitiveType.Cube, rig, Vector3.zero, Vector3.one * 2.2f, iceMat, rot: Quaternion.Euler(0, 20, 0)).transform;
            // ice edges
            var edge = Mats.Additive(Ice, 0.8f);
            float h = 1.1f;
            var e = Kit.Poly(cube, edge, 0.02f,
                new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, -1, 1), new Vector3(-1, -1, 1), new Vector3(-1, -1, -1),
                new Vector3(-1, 1, -1), new Vector3(1, 1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(1, 1, 1),
                new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(-1, -1, 1), new Vector3(-1, 1, 1), new Vector3(-1, 1, -1));
            e.transform.localScale = Vector3.one * (h / 2.2f * 2f) / 2f;
            e.widthMultiplier = 0.02f;

            var spot = new GameObject("Spot").AddComponent<Light>();
            spot.transform.SetParent(transform, false);
            spot.transform.localPosition = new Vector3(0, 9f, -2f);
            spot.transform.LookAt(rig.position);
            spot.type = LightType.Spot;
            spot.spotAngle = 35f;
            spot.range = 20f;
            spot.intensity = 25f;
            spot.color = new Color(0.8f, 0.9f, 1f);

            sparkle = Kit.Particles(rig, Ice, 25f, 0.06f, 2.5f, 0.3f, ParticleSystemShapeType.Box, new Vector3(3f, 3f, 3f));
            burst = Kit.Particles(rig, Gold, 0f, 0.15f, 1.6f, 4f, ParticleSystemShapeType.Sphere, Vector3.one);

            TitleBlock(Title, "Stay perfectly still to melt the ice and free the golden orb.");
            clearText = StatCard(new Vector2(0, 0.5f), new Vector2(40, 60), "Ice cleared", "%", 280, Ice);
            timeText = StatCard(new Vector2(1, 0.5f), new Vector2(-40, 60), "Time left", "s", 280, Color.white);
            stillBar = ProgressBar(new Vector2(0.5f, 0), new Vector2(0, 150), 900, "Stillness", Ice);
            stateText = Hud.Label(Hud.Layer, "", 44, new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(1400, 70), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        }

        public override void Tick(in MotionState m, float dt)
        {
            orbit += dt * 10f;
            float a = orbit * Mathf.Deg2Rad;
            Cam.transform.position = new Vector3(Mathf.Sin(a) * 9f, 4.2f, -Mathf.Cos(a) * 9f);
            Cam.transform.LookAt(new Vector3(0, 2.2f, 0));

            float still = m.Stillness;
            if (!Finished && freedT < 0f)
            {
                if (!started) { started = true; }
                timer += dt;
                if (still > 0.8f) clear += dt / NeededSeconds;
                else if (still < 0.5f) { clear -= dt * 0.12f; shake = 0.25f; }
                clear = Mathf.Clamp01(clear);
                stateText.text = still > 0.8f ? "HOLD STILL…" : still > 0.5f ? "STEADY…" : "TOO MUCH MOVEMENT!";
                stateText.color = still > 0.8f ? new Color(0.6f, 1f, 0.85f) : still > 0.5f ? Color.white : new Color(1f, 0.5f, 0.5f);

                if (clear >= 1f)
                {
                    freedT = 0f;
                    Kit.Burst(burst, 160);
                    App.Audio.PlayOneShot(Kit.Clap);
                    stateText.text = "THE ORB IS FREE!";
                    stateText.color = Gold;
                }
                else if (timer >= TimeLimit)
                {
                    ScoreService.Record("stillness", clear * 100f, lowerIsBetter: false);
                    Finish($"{clear * 100f:0}% of the ice melted", "So close — try to stay completely still");
                }
            }
            if (freedT >= 0f)
            {
                freedT += dt;
                orb.localPosition = new Vector3(0, Mathf.SmoothStep(0, 1.6f, Mathf.Clamp01(freedT / 1.5f)), 0);
                orb.localRotation = Quaternion.Euler(0, freedT * 90f, 0);
                if (freedT > 3.5f && !Finished)
                {
                    int rank = ScoreService.Record("stillness-time", timer, lowerIsBetter: true);
                    Finish($"Freed in {timer:0.0} s", rank == 1 ? "NEW BEST!" : $"#{rank} today");
                }
            }

            // visuals
            shake = Mathf.MoveTowards(shake, 0f, dt);
            rig.localPosition = new Vector3(Random.Range(-1f, 1f) * shake * 0.05f, 2.45f, Random.Range(-1f, 1f) * shake * 0.05f);
            float c = freedT >= 0f ? 1f : clear;
            iceMat.SetColor("_BaseColor", new Color(0.7f, 0.88f, 1f, Mathf.Lerp(0.35f, 0.04f, c)));
            frostMat.SetColor("_BaseColor", new Color(0.9f, 0.96f, 1f, Mathf.Lerp(0.8f, 0f, c)));
            frost.localScale = Vector3.one * Mathf.Lerp(1.9f, 1.2f, c);
            cube.localScale = Vector3.one * (freedT >= 0f ? Mathf.Lerp(2.2f, 0f, Mathf.Clamp01(freedT / 0.8f)) : 2.2f);
            orbMat.SetColor("_EmissionColor", Gold * Mathf.Lerp(0.6f, 3.5f, c));
            orbLight.intensity = Mathf.Lerp(0.3f, 4f, c);
            var em = sparkle.emission;
            em.rateOverTime = Mathf.Lerp(30f, 5f, c);

            clearText.text = (c * 100f).ToString("0");
            timeText.text = Mathf.Max(0f, TimeLimit - timer).ToString("0");
            Hud.SetFill(stillBar, still);
        }
    }
}
