using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Stand-in for experiences not built yet. Doubles as a motion playground:
    /// the core spins with cadence, tilts with lean, and flashes on knock, so it is
    /// useful for checking the sensor mapping on the booth.
    /// Replace by writing the real experience and passing it to Catalog.E(...).
    /// </summary>
    public class Placeholder : Experience
    {
        public ExperienceInfo Info;
        public override string Title => Info != null ? Info.Title : "Coming soon";

        Transform core, orbit;
        Material coreMat;
        float spin, flash;
        Text live;

        public override void Begin()
        {
            var c = Info.Color;
            App.Env(Color.Lerp(new Color(0.02f, 0.02f, 0.05f), c, 0.08f), new Color(0.2f, 0.2f, 0.28f), 8f, 40f);
            App.Sun.intensity = 0.6f;
            Cam.transform.SetPositionAndRotation(new Vector3(0, 2.2f, -8f), Quaternion.Euler(8f, 0, 0));

            Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.15f, 0), new Vector3(3f, 0.15f, 3f), Mats.Lit(new Color(0.08f, 0.08f, 0.12f), 0.7f));
            Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.31f, 0), new Vector3(3.1f, 0.01f, 3.1f), Mats.Glow(c, 1.5f));

            core = new GameObject("Core").transform;
            core.SetParent(transform, false);
            core.localPosition = new Vector3(0, 2.2f, 0);
            coreMat = Mats.Unique(Mats.Glow(c, 2f));
            Geo.Prim(PrimitiveType.Cube, core, Vector3.zero, Vector3.one * 1.3f, coreMat, rot: Quaternion.Euler(35f, 45f, 0f));
            Geo.Prim(PrimitiveType.Sphere, core, Vector3.zero, Vector3.one * 2.4f, Mats.Additive(c, 0.12f));

            orbit = new GameObject("Orbit").transform;
            orbit.SetParent(core, false);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Geo.Prim(PrimitiveType.Sphere, orbit, new Vector3(Mathf.Cos(a) * 2.2f, 0, Mathf.Sin(a) * 2.2f), Vector3.one * 0.18f, Mats.Glow(Color.white, 2f));
            }
            Geo.PointLight(transform, new Vector3(0, 3f, -2f), c, 5f, 12f);

            var L = Hud.Layer;
            Hud.Label(L, Info.Title, 64, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1400, 80), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Hud.Label(L, Info.Tagline, 30, new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(1400, 50), TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.7f));
            var p = Hud.Panel(L, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1000, 150), Hud.Glass);
            Hud.Outline(p.gameObject);
            Hud.Label(p.transform, $"3D design is ready. Full experience arrives in build phase {Info.Phase}.", 26,
                new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(960, 40), TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.75f));
            live = Hud.Label(p.transform, "", 30, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(960, 60), TextAnchor.MiddleCenter, Color.white);
        }

        public override void Tick(in MotionState m, float dt)
        {
            spin += (20f + m.Cadence * 4f) * dt;
            flash = m.Knock ? 1f : Mathf.MoveTowards(flash, 0f, dt * 2.5f);
            core.localRotation = Quaternion.Euler(0f, 0f, -m.Lean * 30f) * Quaternion.Euler(0f, spin, 0f);
            core.localScale = Vector3.one * (1f + flash * 0.25f + Mathf.Sin(m.CrankAngle * Mathf.Deg2Rad) * 0.03f);
            orbit.localRotation = Quaternion.Euler(0f, -spin * 1.5f, 0f);
            coreMat.SetColor("_EmissionColor", Info.Color * (2f + m.Watts * 0.01f + flash * 6f));
            live.text = $"Motion test:  {m.Cadence:0} rpm   ·   {m.Watts:0} W   ·   lean {m.Lean:+0.0;-0.0}   ·   stillness {m.Stillness:0.00}";
        }
    }
}
