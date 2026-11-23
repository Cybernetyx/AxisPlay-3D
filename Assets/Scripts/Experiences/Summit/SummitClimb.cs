using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Hero experience. A procedural low-poly mountain at sunset; pedal power (watts)
    /// moves the climber up a glowing spiral trail past camp flags to the summit.
    /// Score = time to the top, saved locally.
    /// </summary>
    public class SummitClimb : Experience
    {
        public override string Title => "Climb the Summit";

        const string Id = "summit";
        const float GoalMeters = 600f;   // virtual climb; ~60 s at 200 W
        const float MetersPerWattSecond = 0.05f;
        const float BaseAltitude = 4248f; // shown altitude = base + climbed
        const float R = 42f, H = 28f;
        const int Res = 56;

        static readonly Color Sunset = new Color(1f, 0.55f, 0.25f);
        static readonly Color Trail = new Color(1f, 0.62f, 0.3f);
        static readonly Color Sky = new Color(0.36f, 0.22f, 0.4f);

        Vector3[] path;
        float climbed, timer;
        bool started;
        Transform climber, beam;
        LineRenderer doneTrail;
        Vector3 camPos;
        Text altText, statText, timeText, hint;
        RectTransform bar;

        float Height(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z) / R;
            if (r >= 1f) return 0f;
            float body = Mathf.Pow(1f - r, 1.45f);
            float ridges = Mathf.PerlinNoise(x * 0.07f + 13f, z * 0.07f + 7f) * 0.4f
                         + Mathf.PerlinNoise(x * 0.19f + 3f, z * 0.19f + 5f) * 0.12f;
            return H * body * (0.72f + ridges);
        }

        public override void Begin()
        {
            App.Env(Sky, new Color(0.55f, 0.45f, 0.6f), 45f, 200f);
            App.Sun.color = Sunset;
            App.Sun.intensity = 1.8f;
            App.Sun.transform.rotation = Quaternion.Euler(26f, -35f, 0f);
            Cam.fieldOfView = 50f;

            var mountain = BuildMountainMesh();
            var mats = new[]
            {
                Mats.Lit(new Color(0.36f, 0.42f, 0.38f), 0.05f),  // lower slopes
                Mats.Lit(new Color(0.55f, 0.45f, 0.48f), 0.1f),  // rock
                Mats.Lit(new Color(0.95f, 0.92f, 1f), 0.45f),    // snow
            };
            AddMesh("Mountain", mountain, mats, Vector3.zero, Vector3.one, 0f);

            // Distant range for depth (same mesh, darker, scaled).
            var far = new[] { Mats.Lit(new Color(0.16f, 0.12f, 0.24f)), Mats.Lit(new Color(0.22f, 0.16f, 0.3f)), Mats.Lit(new Color(0.7f, 0.62f, 0.8f)) };
            var rnd = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.4f;
                float d = 115f + (float)rnd.NextDouble() * 40f;
                float s = 0.7f + (float)rnd.NextDouble() * 0.6f;
                AddMesh("Far" + i, mountain, far, new Vector3(Mathf.Cos(a) * d, -2f, Mathf.Sin(a) * d), new Vector3(s, s * 1.2f, s), a * 57f);
            }

            Geo.Prim(PrimitiveType.Plane, transform, new Vector3(0, -0.05f, 0), Vector3.one * 40f, Mats.Lit(new Color(0.12f, 0.09f, 0.18f)));

            BuildTrail();
            BuildCamps();
            BuildClimber();
            BuildSnow();
            BuildHud();

            camPos = ClimberCamTarget(0f, out _);
            Cam.transform.position = camPos;
        }

        Mesh BuildMountainMesh()
        {
            // Flat-shaded: every triangle has its own vertices. Submesh picked by height/slope.
            var verts = new List<Vector3>();
            var subs = new[] { new List<int>(), new List<int>(), new List<int>() };
            float size = R * 1.15f, cell = size * 2f / Res;

            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                float h = (a.y + b.y + c.y) / 3f;
                int k = h > H * 0.62f ? 2 : (n.y < 0.72f || h > H * 0.3f) ? 1 : 0;
                int i0 = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                subs[k].Add(i0); subs[k].Add(i0 + 1); subs[k].Add(i0 + 2);
            }

            Vector3 P(int ix, int iz)
            {
                float x = -size + ix * cell, z = -size + iz * cell;
                // jitter interior points for an organic low-poly look
                float j = Mathf.PerlinNoise(ix * 0.9f, iz * 0.9f) - 0.5f;
                x += j * cell * 0.5f; z -= j * cell * 0.5f;
                return new Vector3(x, Height(x, z), z);
            }

            for (int z = 0; z < Res; z++)
            for (int x = 0; x < Res; x++)
            {
                Vector3 a = P(x, z), b = P(x + 1, z), c = P(x, z + 1), d = P(x + 1, z + 1);
                if (a.y + b.y + c.y + d.y <= 0f) continue; // skip flat ground; the plane covers it
                Tri(a, c, b);
                Tri(b, c, d);
            }

            var mesh = new Mesh { name = "LowPolyMountain" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(subs[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void AddMesh(string name, Mesh mesh, Material[] mats, Vector3 pos, Vector3 scale, float yaw)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
        }

        Vector3 PathPoint(float t)
        {
            float ang = t * Mathf.PI * 2f * 2.25f + 0.6f;
            float rr = Mathf.Lerp(R * 0.86f, 0.6f, Mathf.Pow(t, 0.9f));
            float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
            return new Vector3(x, Height(x, z) + 0.35f, z);
        }

        Vector3 PathAt(float t)
        {
            float f = Mathf.Clamp01(t) * (path.Length - 1);
            int i = Mathf.Min((int)f, path.Length - 2);
            return Vector3.Lerp(path[i], path[i + 1], f - i);
        }

        void BuildTrail()
        {
            path = new Vector3[260];
            for (int i = 0; i < path.Length; i++) path[i] = PathPoint(i / (path.Length - 1f));

            var dim = Geo.Line(transform, Mats.Additive(Trail, 0.35f), 0.22f);
            dim.positionCount = path.Length;
            dim.SetPositions(path);

            doneTrail = Geo.Line(transform, Mats.Additive(Trail, 2.2f), 0.35f);
            doneTrail.positionCount = 0;
        }

        void BuildCamps()
        {
            float[] camps = { 0.25f, 0.5f, 0.75f, 1f };
            string[] names = { "CAMP I", "CAMP II", "CAMP III", "SUMMIT" };
            for (int i = 0; i < camps.Length; i++)
            {
                var p = PathAt(camps[i]);
                var flag = new GameObject(names[i]).transform;
                flag.SetParent(transform, false);
                flag.localPosition = p;
                Geo.Prim(PrimitiveType.Cylinder, flag, new Vector3(0, 1.1f, 0), new Vector3(0.06f, 1.1f, 0.06f), Mats.Lit(new Color(0.85f, 0.85f, 0.9f)));
                var c = i == 3 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.4f);
                Geo.Prim(PrimitiveType.Cube, flag, new Vector3(0.45f, 1.9f, 0), new Vector3(0.8f, 0.45f, 0.03f), Mats.Glow(c, 1.6f));
                Geo.PointLight(flag, new Vector3(0, 1.5f, 0), c, 1.5f, 6f);
                var label = Geo.WorldText(flag, names[i], new Vector3(0, 2.8f, 0), new Vector2(300, 60), 40, Color.white);
                label.transform.parent.gameObject.AddComponent<Billboard>();
            }
        }

        void BuildClimber()
        {
            climber = new GameObject("Climber").transform;
            climber.SetParent(transform, false);
            Geo.Prim(PrimitiveType.Capsule, climber, new Vector3(0, 0.5f, 0), new Vector3(0.45f, 0.5f, 0.45f), Mats.Glow(new Color(0.3f, 0.85f, 1f), 2.2f));
            Geo.Prim(PrimitiveType.Sphere, climber, new Vector3(0, 1.15f, 0), Vector3.one * 0.32f, Mats.Glow(Color.white, 2f));
            beam = Geo.Prim(PrimitiveType.Cylinder, climber, new Vector3(0, 6f, 0), new Vector3(0.12f, 5f, 0.12f), Mats.Additive(new Color(0.4f, 0.85f, 1f), 0.5f)).transform;
            Geo.PointLight(climber, new Vector3(0, 1.4f, 0), new Color(0.4f, 0.85f, 1f), 3f, 8f);
        }

        void BuildSnow()
        {
            var go = new GameObject("Snow");
            go.transform.SetParent(Cam.transform, false);
            go.transform.localPosition = new Vector3(0, 6, 10);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 6f;
            main.startSpeed = 0.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new Color(1f, 1f, 1f, 0.8f);
            main.maxParticles = 600;
            main.gravityModifier = 0.04f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 90f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(30, 1, 25);
            var nz = ps.noise;
            nz.enabled = true;
            nz.strength = 0.4f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle(new Color(1f, 1f, 1f, 0.7f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildHud()
        {
            var L = Hud.Layer;
            var alt = Hud.Panel(L, new Vector2(0, 1), new Vector2(40, -130), new Vector2(520, 200), Hud.Glass);
            Hud.Outline(alt.gameObject);
            Hud.Label(alt.transform, "ALTITUDE", 22, new Vector2(0, 1), new Vector2(28, -20), new Vector2(460, 30), TextAnchor.UpperLeft, new Color(1, 1, 1, 0.6f));
            altText = Hud.Label(alt.transform, "", 84, new Vector2(0, 1), new Vector2(24, -50), new Vector2(480, 100), TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            timeText = Hud.Label(alt.transform, "", 24, new Vector2(0, 0), new Vector2(28, 16), new Vector2(460, 30), TextAnchor.LowerLeft, new Color(1, 1, 1, 0.7f));

            var stats = Hud.Panel(L, new Vector2(1, 1), new Vector2(-140, -40), new Vector2(380, 140), Hud.Glass);
            Hud.Outline(stats.gameObject);
            statText = Hud.Label(stats.transform, "", 30, new Vector2(0, 1), new Vector2(24, -20), new Vector2(340, 110), TextAnchor.UpperLeft, Color.white);

            bar = Hud.Bar(L, new Vector2(1, 0.5f), new Vector2(-50, -60), new Vector2(26, 560), new Color(1f, 0.62f, 0.3f, 0.95f), vertical: true);
            foreach (var f in new[] { 0.25f, 0.5f, 0.75f })
                Hud.Panel(bar.parent, new Vector2(0.5f, f), Vector2.zero, new Vector2(44, 4), new Color(1, 1, 1, 0.6f));

            hint = Hud.Label(L, "START PEDALLING TO CLIMB", 44, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(1200, 70),
                TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            float best = ScoreService.BestTime(Id);
            if (best < float.MaxValue)
                Hud.Label(L, $"Today's best: {best:0.0} s", 26, new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(800, 40),
                    TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.6f));
        }

        Vector3 ClimberCamTarget(float t, out Vector3 look)
        {
            Vector3 p = PathAt(t);
            Vector3 outward = new Vector3(p.x, 0, p.z);
            if (outward.sqrMagnitude < 4f) outward = new Vector3(PathAt(0.97f).x, 0, PathAt(0.97f).z);
            outward = Quaternion.Euler(0, -28f, 0) * outward.normalized;
            look = p + Vector3.up * 1.5f;
            return p + outward * Mathf.Lerp(26f, 30f, t) + Vector3.up * Mathf.Lerp(12f, 14f, t);
        }

        public override void Tick(in MotionState m, float dt)
        {
            if (!Finished)
            {
                if (!started && m.Cadence > 10f)
                {
                    started = true;
                    hint.gameObject.SetActive(false);
                }
                if (started)
                {
                    timer += dt;
                    climbed = Mathf.Min(GoalMeters, climbed + m.Watts * MetersPerWattSecond * dt);
                }
                if (climbed >= GoalMeters) Finish();
            }

            float t = climbed / GoalMeters;
            Vector3 p = PathAt(t);
            Vector3 ahead = PathAt(t + 0.01f);
            climber.localPosition = p;
            Vector3 dir = ahead - p; dir.y = 0;
            if (dir.sqrMagnitude > 1e-4f) climber.localRotation = Quaternion.Slerp(climber.localRotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-6f * dt));
            float bob = Mathf.Abs(Mathf.Sin(m.CrankAngle * Mathf.Deg2Rad)) * 0.08f;
            climber.GetChild(0).localPosition = new Vector3(0, 0.5f + bob, 0);
            beam.localScale = new Vector3(0.12f, 5f + m.Watts * 0.01f, 0.12f);

            int n = Mathf.Max(2, Mathf.CeilToInt(t * (path.Length - 1)) + 1);
            doneTrail.positionCount = n;
            for (int i = 0; i < n - 1; i++) doneTrail.SetPosition(i, path[i]);
            doneTrail.SetPosition(n - 1, p);

            Vector3 target = ClimberCamTarget(t, out var look);
            camPos = Vector3.Lerp(camPos, target, 1f - Mathf.Exp(-2f * dt));
            Cam.transform.position = camPos;
            var wantRot = Quaternion.LookRotation(look - camPos);
            Cam.transform.rotation = Quaternion.Slerp(Cam.transform.rotation, wantRot, 1f - Mathf.Exp(-4f * dt));

            altText.text = $"{BaseAltitude + climbed:N0} m";
            timeText.text = started ? $"TIME {timer:0.0} s   ·   {GoalMeters - climbed:0} m to summit" : "Pedal to start the clock";
            statText.text = $"<b>{m.Watts:0}</b> W\n<b>{m.Cadence:0}</b> rpm";
            Hud.SetFill(bar, t);
        }

        void Finish()
        {
            Finished = true;
            bool record = ScoreService.SubmitTime(Id, timer, out float best);
            ResultText = record ? $"{timer:0.0} s\nNEW BEST!" : $"{timer:0.0} s\nbest {best:0.0} s";
        }
    }

    /// <summary>Keeps world-space labels facing the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = App.I != null ? App.I.Cam : null;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
