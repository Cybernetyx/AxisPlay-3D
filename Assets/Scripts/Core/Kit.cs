using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AxisPlay
{
    /// <summary>Shared 3D builders used by several experiences.</summary>
    public static class Kit
    {
        // ---------- lines & meshes ----------

        /// <summary>Closed circle line in local space. xz = lying flat, otherwise in the XY plane.</summary>
        public static LineRenderer Ring(Transform parent, float radius, Material mat, float width, int segments = 64, bool flat = true)
        {
            var lr = Geo.Line(parent, mat, width);
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, flat ? new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius)
                                       : new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0));
            }
            return lr;
        }

        /// <summary>Open polyline in local space.</summary>
        public static LineRenderer Poly(Transform parent, Material mat, float width, params Vector3[] pts)
        {
            var lr = Geo.Line(parent, mat, width);
            lr.useWorldSpace = false;
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
            return lr;
        }

        /// <summary>Flat strip following a path on the ground (roads, tracks).</summary>
        public static Mesh Ribbon(IList<Vector3> pts, float width, float y = 0.02f)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 a = pts[Mathf.Max(i - 1, 0)], b = pts[Mathf.Min(i + 1, pts.Count - 1)];
                Vector3 dir = b - a; dir.y = 0;
                Vector3 side = Vector3.Cross(Vector3.up, dir.normalized) * (width * 0.5f);
                v.Add(pts[i] - side + Vector3.up * y);
                v.Add(pts[i] + side + Vector3.up * y);
                if (i > 0)
                {
                    int k = i * 2;
                    t.Add(k - 2); t.Add(k); t.Add(k - 1);
                    t.Add(k - 1); t.Add(k); t.Add(k + 1);
                }
            }
            var m = new Mesh { name = "Ribbon" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            if (mats.Length > 0 && mats[0].renderQueue >= 3000) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Flat-shaded mesh: one triangle list per submesh, vertices not shared.</summary>
        public class FlatMeshBuilder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<int>[] subs;
            public FlatMeshBuilder(int submeshes)
            {
                subs = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) subs[i] = new List<int>();
            }
            public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c)
            {
                int i0 = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                subs[sub].Add(i0); subs[sub].Add(i0 + 1); subs[sub].Add(i0 + 2);
            }
            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(verts);
                m.subMeshCount = subs.Length;
                for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        // ---------- rider ----------

        public class RiderRig
        {
            public Transform Root, Body, WheelF, WheelR, Crank;
            public Renderer[] Renderers;

            /// <summary>Spin wheels and crank from distance travelled and crank angle.</summary>
            public void Animate(float distance, float crankAngle, float lean = 0f)
            {
                float wheelDeg = distance / (2f * Mathf.PI * 0.35f) * 360f;
                WheelF.localRotation = Quaternion.Euler(wheelDeg, 0, 0);
                WheelR.localRotation = Quaternion.Euler(wheelDeg, 0, 0);
                Crank.localRotation = Quaternion.Euler(crankAngle, 0, 0);
                Body.localRotation = Quaternion.Euler(0, 0, -lean * 12f);
            }
        }

        /// <summary>Low-poly bike + rider facing +Z, ~1.7 m tall, origin on the ground.</summary>
        public static RiderRig Rider(Transform parent, Material body, Material bike, Material accent)
        {
            var rig = new RiderRig();
            rig.Root = new GameObject("Rider").transform;
            rig.Root.SetParent(parent, false);
            rig.Body = new GameObject("Body").transform;
            rig.Body.SetParent(rig.Root, false);
            var b = rig.Body;

            Transform Wheel(string n, float z)
            {
                var pivot = new GameObject(n).transform;
                pivot.SetParent(b, false);
                pivot.localPosition = new Vector3(0, 0.35f, z);
                Geo.Prim(PrimitiveType.Cylinder, pivot, Vector3.zero, new Vector3(0.7f, 0.025f, 0.7f), bike, rot: Quaternion.Euler(0, 0, 90));
                Geo.Prim(PrimitiveType.Cylinder, pivot, Vector3.zero, new Vector3(0.6f, 0.03f, 0.6f), accent, rot: Quaternion.Euler(0, 0, 90));
                Geo.Prim(PrimitiveType.Cube, pivot, Vector3.zero, new Vector3(0.03f, 0.62f, 0.04f), bike);
                return pivot;
            }
            rig.WheelF = Wheel("WheelF", 0.55f);
            rig.WheelR = Wheel("WheelR", -0.55f);

            // frame
            Geo.Prim(PrimitiveType.Cube, b, new Vector3(0, 0.62f, 0.05f), new Vector3(0.05f, 0.05f, 0.85f), bike, rot: Quaternion.Euler(-8, 0, 0));
            Geo.Prim(PrimitiveType.Cube, b, new Vector3(0, 0.5f, 0.2f), new Vector3(0.05f, 0.05f, 0.75f), bike, rot: Quaternion.Euler(-40, 0, 0));
            Geo.Prim(PrimitiveType.Cube, b, new Vector3(0, 0.62f, -0.2f), new Vector3(0.05f, 0.6f, 0.05f), bike, rot: Quaternion.Euler(20, 0, 0));
            Geo.Prim(PrimitiveType.Cube, b, new Vector3(0, 0.95f, 0.55f), new Vector3(0.45f, 0.04f, 0.04f), bike); // bars
            Geo.Prim(PrimitiveType.Cube, b, new Vector3(0, 0.92f, -0.28f), new Vector3(0.12f, 0.04f, 0.25f), bike); // saddle

            rig.Crank = new GameObject("Crank").transform;
            rig.Crank.SetParent(b, false);
            rig.Crank.localPosition = new Vector3(0, 0.35f, 0.02f);
            Geo.Prim(PrimitiveType.Cube, rig.Crank, new Vector3(0.1f, 0.0f, 0.08f), new Vector3(0.03f, 0.03f, 0.18f), accent);
            Geo.Prim(PrimitiveType.Cube, rig.Crank, new Vector3(-0.1f, 0.0f, -0.08f), new Vector3(0.03f, 0.03f, 0.18f), accent);

            // rider
            Geo.Prim(PrimitiveType.Capsule, b, new Vector3(0, 1.22f, -0.02f), new Vector3(0.36f, 0.38f, 0.3f), body, rot: Quaternion.Euler(55, 0, 0));
            Geo.Prim(PrimitiveType.Sphere, b, new Vector3(0, 1.5f, 0.32f), Vector3.one * 0.24f, body);
            Geo.Prim(PrimitiveType.Sphere, b, new Vector3(0, 1.53f, 0.33f), new Vector3(0.27f, 0.17f, 0.3f), accent); // helmet
            Geo.Prim(PrimitiveType.Capsule, b, new Vector3(0.12f, 0.7f, -0.1f), new Vector3(0.13f, 0.32f, 0.13f), body, rot: Quaternion.Euler(30, 0, 0));
            Geo.Prim(PrimitiveType.Capsule, b, new Vector3(-0.12f, 0.7f, -0.1f), new Vector3(0.13f, 0.32f, 0.13f), body, rot: Quaternion.Euler(-10, 0, 0));
            Geo.Prim(PrimitiveType.Capsule, b, new Vector3(0.17f, 1.15f, 0.35f), new Vector3(0.1f, 0.28f, 0.1f), body, rot: Quaternion.Euler(70, 0, 0));
            Geo.Prim(PrimitiveType.Capsule, b, new Vector3(-0.17f, 1.15f, 0.35f), new Vector3(0.1f, 0.28f, 0.1f), body, rot: Quaternion.Euler(70, 0, 0));

            rig.Renderers = rig.Root.GetComponentsInChildren<Renderer>();
            return rig;
        }

        // ---------- track ----------

        /// <summary>Glowing straight race track along +Z from 0 to length, with finish gate.</summary>
        public static void Track(Transform parent, float length, float width, Color edge, Color ground)
        {
            Geo.Prim(PrimitiveType.Plane, parent, new Vector3(0, -0.02f, length * 0.5f), new Vector3(60, 1, (length + 400) / 10f), Mats.Lit(ground, 0.3f));
            Geo.Prim(PrimitiveType.Cube, parent, new Vector3(0, 0, length * 0.5f), new Vector3(width, 0.02f, length + 60), Mats.Lit(new Color(0.06f, 0.06f, 0.09f), 0.7f));
            var glow = Mats.Glow(edge, 2.2f);
            Geo.Prim(PrimitiveType.Cube, parent, new Vector3(-width / 2, 0.03f, length * 0.5f), new Vector3(0.12f, 0.04f, length + 60), glow);
            Geo.Prim(PrimitiveType.Cube, parent, new Vector3(width / 2, 0.03f, length * 0.5f), new Vector3(0.12f, 0.04f, length + 60), glow);
            var dash = Mats.Glow(Color.white, 0.8f);
            for (float z = 0; z < length + 30; z += 8f)
                Geo.Prim(PrimitiveType.Cube, parent, new Vector3(0, 0.02f, z), new Vector3(0.08f, 0.02f, 3f), dash);
            for (float z = 50; z < length; z += 50f)
            {
                Geo.Prim(PrimitiveType.Cube, parent, new Vector3(-width / 2 - 1.2f, 0.6f, z), new Vector3(0.12f, 1.2f, 0.12f), glow);
                var t = Geo.WorldText(parent, $"{z:0} m", new Vector3(-width / 2 - 1.2f, 1.5f, z), new Vector2(200, 60), 48, Color.white);
                t.transform.parent.localRotation = Quaternion.Euler(0, 0, 0);
            }
            // finish gate
            var gate = Mats.Glow(edge, 3f);
            Geo.Prim(PrimitiveType.Cube, parent, new Vector3(-width / 2 - 0.3f, 2.2f, length), new Vector3(0.3f, 4.4f, 0.3f), gate);
            Geo.Prim(PrimitiveType.Cube, parent, new Vector3(width / 2 + 0.3f, 2.2f, length), new Vector3(0.3f, 4.4f, 0.3f), gate);
            Geo.Prim(PrimitiveType.Cube, parent, new Vector3(0, 4.4f, length), new Vector3(width + 1f, 0.5f, 0.3f), gate);
            for (int i = 0; i < 10; i++)
                Geo.Prim(PrimitiveType.Cube, parent, new Vector3(-width / 2 + (i + 0.5f) * width / 10f, 0.03f, length),
                    new Vector3(width / 10f, 0.03f, 0.6f), i % 2 == 0 ? Mats.Glow(Color.white, 1.5f) : Mats.Lit(Color.black));
            Geo.WorldText(parent, "FINISH", new Vector3(0, 4.4f, length - 0.2f), new Vector2(400, 60), 50, Color.black);
        }

        /// <summary>Silhouette skyline of boxes along a far line, for horizons.</summary>
        public static void Skyline(Transform parent, Vector3 center, float width, float depthZ, Color color, int count, int seed)
        {
            var rnd = new System.Random(seed);
            var mat = Mats.Lit(color, 0.1f);
            var win = Mats.Glow(new Color(1f, 0.75f, 0.45f), 1.4f);
            for (int i = 0; i < count; i++)
            {
                float x = center.x - width / 2 + (float)rnd.NextDouble() * width;
                float h = 6f + (float)rnd.NextDouble() * 26f;
                float w = 3f + (float)rnd.NextDouble() * 6f;
                var p = new Vector3(x, h / 2, center.z + (float)rnd.NextDouble() * depthZ);
                Geo.Prim(PrimitiveType.Cube, parent, p, new Vector3(w, h, w), mat);
                if (rnd.NextDouble() < 0.6)
                    Geo.Prim(PrimitiveType.Cube, parent, p + new Vector3(0, h * 0.2f, -w / 2 - 0.02f), new Vector3(w * 0.6f, h * 0.35f, 0.02f), win);
            }
        }

        // ---------- city ----------

        public class Building
        {
            public Transform Root;
            public float Height;
            public int District;
            public int Year;      // for Time Machine
            public Vector3 Pos;
        }

        /// <summary>
        /// 6x6 block city on a grid. Each building has floor-stripe windows using its
        /// district's material (so lighting a district is one material change).
        /// </summary>
        public static List<Building> City(Transform parent, Material[] districtWindows, Material wall, Material roofEdge,
            float spacing = 4f, int grid = 6, int seed = 3)
        {
            var list = new List<Building>();
            var rnd = new System.Random(seed);
            float half = (grid - 1) * spacing * 0.5f;
            for (int gz = 0; gz < grid; gz++)
            for (int gx = 0; gx < grid; gx++)
            {
                float h = 2.5f + (float)rnd.NextDouble() * 7f;
                if (Mathf.Abs(gx - 2.5f) < 1.2f && Mathf.Abs(gz - 2.5f) < 1.2f) h += 4f; // taller downtown
                float w = 2.2f + (float)rnd.NextDouble() * 0.8f;
                int district = (gx / 2) + (gz / 3) * 3; // 6 districts of 2x3 blocks
                var root = new GameObject($"B{gx}_{gz}").transform;
                root.SetParent(parent, false);
                var pos = new Vector3(gx * spacing - half, 0, gz * spacing - half);
                root.localPosition = pos;
                Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, h / 2, 0), new Vector3(w, h, w), wall);
                var win = districtWindows[district % districtWindows.Length];
                for (float y = 0.8f; y < h - 0.4f; y += 0.7f)
                    Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, y, 0), new Vector3(w + 0.04f, 0.16f, w + 0.04f), win);
                Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, h + 0.03f, 0), new Vector3(w + 0.06f, 0.06f, w + 0.06f), roofEdge);
                list.Add(new Building { Root = root, Height = h, District = district, Pos = pos });
            }
            return list;
        }

        // ---------- particles ----------

        public static ParticleSystem Particles(Transform parent, Color c, float rate, float size, float life, float speed,
            ParticleSystemShapeType shape, Vector3 shapeScale, bool world = true)
        {
            var go = new GameObject("Particles");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = c;
            main.maxParticles = 1500;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            var em = ps.emission;
            em.rateOverTime = rate;
            var sh = ps.shape;
            sh.shapeType = shape;
            sh.scale = shapeScale;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle(Color.white);
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>One-shot burst (confetti, sparks).</summary>
        public static void Burst(ParticleSystem ps, int count) => ps.Emit(count);

        // ---------- audio (synthesised, no asset files) ----------

        static AudioClip kick, hat, clap, blip;
        public static AudioClip Kick => kick != null ? kick : (kick = Synth("kick", 0.35f, (t, ph) =>
            Mathf.Sin(ph(50f + 110f * Mathf.Exp(-t * 28f))) * Mathf.Exp(-t * 9f)));
        public static AudioClip Hat => hat != null ? hat : (hat = Synth("hat", 0.08f, (t, ph) =>
            (Random.value * 2f - 1f) * Mathf.Exp(-t * 70f) * 0.35f));
        public static AudioClip Clap => clap != null ? clap : (clap = Synth("clap", 0.25f, (t, ph) =>
            (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.5f + Mathf.Sin(ph(190f)) * Mathf.Exp(-t * 30f) * 0.3f));
        public static AudioClip Blip => blip != null ? blip : (blip = Synth("blip", 0.18f, (t, ph) =>
            Mathf.Sin(ph(880f)) * Mathf.Exp(-t * 20f) * 0.5f));

        delegate float Voice(float t, System.Func<float, float> phase);

        static AudioClip Synth(string name, float seconds, Voice f)
        {
            const int sr = 44100;
            int n = (int)(sr * seconds);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                data[i] = Mathf.Clamp(f(t, hz => { phase += 2f * Mathf.PI * hz / sr; return phase; }), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioSource Audio(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0f;
            return a;
        }

        // ---------- misc ----------

        public static float Damp(float a, float b, float k, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-k * dt));
        public static Vector3 Damp(Vector3 a, Vector3 b, float k, float dt) => Vector3.Lerp(a, b, 1f - Mathf.Exp(-k * dt));

        /// <summary>Speed (m/s) a rider would hold at this power on the flat.</summary>
        public static float SpeedFromWatts(float watts) => watts <= 1f ? 0f : Mathf.Pow(watts / 0.32f, 1f / 3f) * 0.95f;
        // 200 W -> ~8.1 m/s (29 km/h), 400 W -> ~10.2 m/s
    }
}
