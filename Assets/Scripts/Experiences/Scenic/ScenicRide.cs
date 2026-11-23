using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Ride a low-poly valley. At the fork, lean left (Lakeside) or right (Forest Pass);
    /// the chosen road glows. Ends at the end of the chosen branch.
    /// </summary>
    public class ScenicRide : Experience
    {
        public override string Title => "Scenic Ride";

        const float ForkZ = 200f;
        const float SpeedBoost = 1.6f;

        List<Vector3> trunk, left, right, route;
        float[] routeLen;
        int choice; // -1 left, +1 right, 0 none yet
        bool committed;
        float dist, speed, timer;
        Kit.RiderRig rider;
        Material leftGlow, rightGlow;
        Text speedText, distText, routeText;
        Transform signL, signR;
        readonly List<Transform> clouds = new List<Transform>();

        static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1 - t;
            return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
        }

        static List<Vector3> Branch(float side)
        {
            var l = new List<Vector3>();
            for (int i = 0; i <= 20; i++)
                l.Add(Bez(new Vector3(0, 0, ForkZ), new Vector3(0, 0, ForkZ + 60), new Vector3(side * 45, 0, ForkZ + 80), new Vector3(side * 45, 0, ForkZ + 140), i / 20f));
            for (float z = ForkZ + 150; z <= 580; z += 10) l.Add(new Vector3(side * 45 + Mathf.Sin(z * 0.02f) * 6f * side, 0, z));
            return l;
        }

        static float DistToPolyline(Vector2 p, List<Vector3> l)
        {
            float best = float.MaxValue;
            for (int i = 0; i < l.Count - 1; i++)
            {
                Vector2 a = new Vector2(l[i].x, l[i].z), b = new Vector2(l[i + 1].x, l[i + 1].z);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                best = Mathf.Min(best, (a + ab * t - p).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }

        bool InLake(float x, float z) => ((x + 85f) * (x + 85f)) / (30f * 30f) + ((z - 400f) * (z - 400f)) / (110f * 110f) < 1f;

        float Height(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = Mathf.Min(DistToPolyline(p, trunk), Mathf.Min(DistToPolyline(p, left), DistToPolyline(p, right)));
            if (InLake(x, z)) return -2.5f;
            float hills = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(7f, 45f, d));
            float n = Mathf.PerlinNoise(x * 0.025f + 3f, z * 0.025f + 1f);
            float big = Mathf.InverseLerp(60f, 120f, Mathf.Abs(x)) * 30f;
            return hills * (4f + n * 16f) + big * n + Mathf.PerlinNoise(x * 0.1f, z * 0.1f) * 0.6f;
        }

        public override void Begin()
        {
            var sky = new Color(0.55f, 0.75f, 0.95f);
            App.Env(sky, new Color(0.55f, 0.58f, 0.62f), 80f, 380f);
            App.Sun.color = new Color(1f, 0.92f, 0.78f);
            App.Sun.intensity = 1.35f;
            App.Sun.transform.rotation = Quaternion.Euler(35f, 140f, 0f);
            Cam.fieldOfView = 58f;

            trunk = new List<Vector3>();
            for (float z = -30; z <= ForkZ; z += 10) trunk.Add(new Vector3(0, 0, z));
            left = Branch(-1f);
            right = Branch(1f);

            BuildTerrain();
            var lake = Geo.Prim(PrimitiveType.Plane, transform, new Vector3(-85f, -0.4f, 400f), new Vector3(7f, 1f, 23f), Mats.Lit(new Color(0.2f, 0.5f, 0.75f), 0.92f));
            lake.name = "Lake";

            var roadMat = Mats.Lit(new Color(0.22f, 0.22f, 0.25f), 0.3f);
            Kit.MeshObject("Trunk", transform, Kit.Ribbon(trunk, 5f, 0.06f), roadMat);
            Kit.MeshObject("LeftRoad", transform, Kit.Ribbon(left, 5f, 0.06f), roadMat);
            Kit.MeshObject("RightRoad", transform, Kit.Ribbon(right, 5f, 0.06f), roadMat);
            leftGlow = Mats.Unique(Mats.Additive(new Color(0.3f, 0.85f, 1f), 0.3f));
            rightGlow = Mats.Unique(Mats.Additive(new Color(0.4f, 1f, 0.6f), 0.3f));
            Kit.MeshObject("LeftGlow", transform, Kit.Ribbon(left, 1.2f, 0.1f), leftGlow);
            Kit.MeshObject("RightGlow", transform, Kit.Ribbon(right, 1.2f, 0.1f), rightGlow);

            BuildTrees();
            signL = Sign(new Vector3(-5f, 0, ForkZ - 12f), "LAKESIDE", new Color(0.3f, 0.85f, 1f), -1);
            signR = Sign(new Vector3(5f, 0, ForkZ - 12f), "FOREST PASS", new Color(0.4f, 1f, 0.6f), 1);

            var cloudMat = Mats.Lit(new Color(1f, 1f, 1f), 0.1f);
            var rnd = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                var c = Geo.Prim(PrimitiveType.Sphere, transform, new Vector3((float)rnd.NextDouble() * 400 - 200, 70 + (float)rnd.NextDouble() * 30, (float)rnd.NextDouble() * 700),
                    new Vector3(30 + (float)rnd.NextDouble() * 30, 6, 14), cloudMat).transform;
                clouds.Add(c);
            }

            rider = Kit.Rider(transform, Mats.Lit(new Color(0.95f, 0.5f, 0.3f), 0.3f), Mats.Lit(new Color(0.15f, 0.15f, 0.2f), 0.6f), Mats.Glow(new Color(0.3f, 0.85f, 1f), 1.5f));

            TitleBlock(Title, "Pedal to ride. At the fork, lean left or right to choose your road.");
            speedText = StatCard(new Vector2(0, 0), new Vector2(40, 40), "Speed", "km/h", 260, new Color(0.4f, 1f, 0.6f));
            distText = StatCard(new Vector2(1, 0), new Vector2(-40, 40), "Distance", "m", 260, Color.white);
            routeText = Hud.Label(Hud.Layer, "", 30, new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(900, 50), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Hint("PEDAL TO START YOUR RIDE");

            SetRoute(trunk);
            PlaceCamera(0f, true);
        }

        Transform Sign(Vector3 pos, string label, Color c, int dir)
        {
            var s = new GameObject("Sign " + label).transform;
            s.SetParent(transform, false);
            s.localPosition = pos;
            Geo.Prim(PrimitiveType.Cube, s, new Vector3(0, 1.3f, 0), new Vector3(0.15f, 2.6f, 0.15f), Mats.Lit(new Color(0.4f, 0.3f, 0.2f)));
            Geo.Prim(PrimitiveType.Cube, s, new Vector3(dir * 0.9f, 2.5f, 0), new Vector3(2.4f, 0.7f, 0.1f), Mats.Lit(new Color(0.12f, 0.14f, 0.18f)));
            Geo.Prim(PrimitiveType.Cube, s, new Vector3(dir * 0.9f, 2.5f, 0.06f), new Vector3(2.5f, 0.78f, 0.04f), Mats.Glow(c, 0.8f));
            Geo.WorldText(s, (dir < 0 ? "< " : "") + label + (dir > 0 ? " >" : ""), new Vector3(dir * 0.9f, 2.5f, -0.07f), new Vector2(240, 60), 30, Color.white);
            return s;
        }

        void BuildTerrain()
        {
            var fb = new Kit.FlatMeshBuilder(4);
            const float cell = 6f, x0 = -132f, z0 = -60f;
            const int nx = 44, nz = 110;
            var h = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
                h[i, j] = Height(x0 + i * cell, z0 + j * cell);
            Vector3 P(int i, int j) => new Vector3(x0 + i * cell, h[i, j], z0 + j * cell);
            void T(Vector3 a, Vector3 b, Vector3 c)
            {
                float y = (a.y + b.y + c.y) / 3f;
                int sub = y < -0.5f ? 3 : y < 3f ? 0 : y < 14f ? 1 : 2;
                fb.Tri(sub, a, b, c);
            }
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                Vector3 a = P(i, j), b = P(i + 1, j), c = P(i, j + 1), d = P(i + 1, j + 1);
                T(a, c, b);
                T(b, c, d);
            }
            Kit.MeshObject("Valley", transform, fb.Build("Valley"),
                Mats.Lit(new Color(0.42f, 0.66f, 0.32f), 0.05f),  // meadow
                Mats.Lit(new Color(0.28f, 0.5f, 0.27f), 0.05f),   // hills
                Mats.Lit(new Color(0.55f, 0.52f, 0.5f), 0.1f),    // rock
                Mats.Lit(new Color(0.75f, 0.68f, 0.5f), 0.1f));   // lake bed / sand
        }

        void BuildTrees()
        {
            var cone = ConeMesh(7);
            var leaves = new[] { Mats.Lit(new Color(0.15f, 0.42f, 0.22f), 0.05f), Mats.Lit(new Color(0.2f, 0.5f, 0.25f), 0.05f) };
            var trunkMat = Mats.Lit(new Color(0.35f, 0.25f, 0.18f));
            var rnd = new System.Random(12);
            int placed = 0;
            for (int tries = 0; tries < 900 && placed < 170; tries++)
            {
                float x = (float)rnd.NextDouble() * 220f - 110f, z = (float)rnd.NextDouble() * 620f - 40f;
                var p2 = new Vector2(x, z);
                float d = Mathf.Min(DistToPolyline(p2, trunk), Mathf.Min(DistToPolyline(p2, left), DistToPolyline(p2, right)));
                if (d < 6f || InLake(x, z)) continue;
                bool forest = x > 15f && z > ForkZ;
                if (!forest && rnd.NextDouble() < 0.55) continue;
                float y = Height(x, z), s = 0.8f + (float)rnd.NextDouble() * 0.9f;
                var t = new GameObject("Tree").transform;
                t.SetParent(transform, false);
                t.localPosition = new Vector3(x, y, z);
                t.localScale = Vector3.one * s * 1.6f;
                Geo.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.5f, 0), new Vector3(0.25f, 0.5f, 0.25f), trunkMat);
                var c = Kit.MeshObject("Leaves", t, cone, leaves[placed % 2]);
                c.transform.localPosition = new Vector3(0, 0.8f, 0);
                c.transform.localScale = new Vector3(1.6f, 3.2f, 1.6f);
                placed++;
            }
        }

        static Mesh ConeMesh(int sides)
        {
            var fb = new Kit.FlatMeshBuilder(1);
            Vector3 top = new Vector3(0, 1, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                fb.Tri(0, p0, top, p1);
                fb.Tri(0, p1, Vector3.zero, p0);
            }
            return fb.Build("Cone");
        }

        void SetRoute(List<Vector3> branch)
        {
            route = new List<Vector3>(trunk);
            if (branch != trunk) route.AddRange(branch.GetRange(1, branch.Count - 1));
            routeLen = new float[route.Count];
            for (int i = 1; i < route.Count; i++) routeLen[i] = routeLen[i - 1] + Vector3.Distance(route[i - 1], route[i]);
        }

        float RouteLength => routeLen[routeLen.Length - 1];

        Vector3 At(float s, out Vector3 dir)
        {
            s = Mathf.Clamp(s, 0f, RouteLength - 0.01f);
            int i = 1;
            while (i < routeLen.Length - 1 && routeLen[i] < s) i++;
            float seg = routeLen[i] - routeLen[i - 1];
            float t = seg > 0 ? (s - routeLen[i - 1]) / seg : 0;
            dir = (route[i] - route[i - 1]).normalized;
            return Vector3.Lerp(route[i - 1], route[i], t) + Vector3.up * 0.06f;
        }

        void PlaceCamera(float dt, bool snap)
        {
            var p = At(dist - 7f, out _);
            var look = At(dist + 12f, out _);
            Vector3 want = p + Vector3.up * 3.4f;
            Cam.transform.position = snap ? want : Kit.Damp(Cam.transform.position, want, 4f, dt);
            var rot = Quaternion.LookRotation(look + Vector3.up * 1.2f - Cam.transform.position);
            Cam.transform.rotation = snap ? rot : Quaternion.Slerp(Cam.transform.rotation, rot, 1f - Mathf.Exp(-4f * dt));
        }

        public override void Tick(in MotionState m, float dt)
        {
            if (!Finished)
            {
                speed = Kit.Damp(speed, Kit.SpeedFromWatts(m.Watts) * SpeedBoost, 1.2f, dt);
                if (speed > 0.5f) timer += dt;
                dist += speed * dt;
            }
            else speed = Mathf.MoveTowards(speed, 0f, 5f * dt);

            // Choosing at the fork
            if (!committed)
            {
                if (m.Lean < -0.3f) choice = -1;
                if (m.Lean > 0.3f) choice = 1;
                bool near = dist > ForkZ - 90f;
                if (speed > 0.5f) Hint(near ? "LEAN LEFT OR RIGHT TO CHOOSE YOUR ROAD" : null);
                routeText.text = choice == 0 ? "" : choice < 0 ? "<  LAKESIDE" : "FOREST PASS  >";
                if (dist >= trunk[trunk.Count - 1].z + 30f - 2f)
                {
                    if (choice == 0) choice = -1;
                    committed = true;
                    SetRoute(choice < 0 ? left : right);
                    Hint(null);
                    App.Audio.PlayOneShot(Kit.Blip);
                }
            }
            float pulse = 0.6f + Mathf.Sin(Time.time * 4f) * 0.25f;
            leftGlow.SetColor("_BaseColor", new Color(0.3f, 0.85f, 1f) * (choice < 0 ? 1.3f * pulse : 0.15f));
            rightGlow.SetColor("_BaseColor", new Color(0.4f, 1f, 0.6f) * (choice > 0 ? 1.3f * pulse : 0.15f));
            signL.localScale = Vector3.one * (choice < 0 ? 1.15f : 1f);
            signR.localScale = Vector3.one * (choice > 0 ? 1.15f : 1f);

            if (committed && !Finished && dist >= RouteLength - 2f)
            {
                string name = choice < 0 ? "Lakeside" : "Forest Pass";
                ScoreService.Record("scenic", timer, lowerIsBetter: true);
                Finish($"{name} complete!", $"{RouteLength:0} m in {Clock(timer)} s");
            }

            if (!committed && dist > trunk[trunk.Count - 1].z + 28f) dist = trunk[trunk.Count - 1].z + 28f;
            var pos = At(dist, out var dir);
            rider.Root.localPosition = pos;
            if (dir.sqrMagnitude > 0.01f) rider.Root.localRotation = Quaternion.Slerp(rider.Root.localRotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-6f * dt));
            rider.Animate(dist, m.CrankAngle, m.Lean);
            PlaceCamera(dt, false);

            foreach (var c in clouds) c.localPosition += Vector3.right * dt * 2f;

            speedText.text = (speed * 3.6f / SpeedBoost).ToString("0");
            distText.text = dist.ToString("0");
        }
    }
}
