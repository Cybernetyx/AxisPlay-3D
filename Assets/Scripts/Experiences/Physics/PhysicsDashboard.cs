using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// A wireframe digital twin of the bike that copies the real bike's roll and crank
    /// angle live, with X/Y/Z axes and three tilted graph panels (cadence, power, lean).
    /// Free exploration: no score; MENU or idle returns to the launcher.
    /// </summary>
    public class PhysicsDashboard : Experience
    {
        public override string Title => "Live Physics Dashboard";

        static readonly Color Blue = new Color(0.3f, 0.6f, 1f);
        static readonly Color Cyan = new Color(0.3f, 0.95f, 1f);
        const int Samples = 120;

        Transform bike, crank, wheelF, wheelR;
        float dist, orbit;
        Graph gCad, gPow, gLean;
        Text rollText, crankText, cadText, powText, accText;

        class Graph
        {
            public LineRenderer Line;
            public Text Value;
            public float[] Data = new float[Samples];
            public float Max;
            public void Push(float v)
            {
                System.Array.Copy(Data, 1, Data, 0, Samples - 1);
                Data[Samples - 1] = v;
                for (int i = 0; i < Samples; i++)
                    Line.SetPosition(i, new Vector3(-1.4f + i / (float)(Samples - 1) * 2.8f, -0.7f + Mathf.Clamp01((Data[i] + (Max < 0 ? -Max : 0)) / Mathf.Abs(Max) / (Max < 0 ? 2f : 1f)) * 1.4f, -0.02f));
            }
        }

        public override void Begin()
        {
            App.Env(new Color(0.01f, 0.02f, 0.05f), new Color(0.15f, 0.18f, 0.28f), 15f, 50f);
            App.Sun.intensity = 0.3f;
            Cam.fieldOfView = 45f;

            // blueprint floor grid
            Geo.Prim(PrimitiveType.Plane, transform, Vector3.zero, Vector3.one * 6f, Mats.Lit(new Color(0.02f, 0.04f, 0.08f), 0.7f));
            var gm = Mats.Additive(Blue, 0.25f);
            for (int i = -10; i <= 10; i++)
            {
                Kit.Poly(transform, gm, 0.02f, new Vector3(i, 0.01f, -10), new Vector3(i, 0.01f, 10));
                Kit.Poly(transform, gm, 0.02f, new Vector3(-10, 0.01f, i), new Vector3(10, 0.01f, i));
            }

            BuildBike();

            // axes
            var ax = new GameObject("Axes").transform;
            ax.SetParent(transform, false);
            ax.localPosition = new Vector3(-2.6f, 0.02f, -1.2f);
            Axis(ax, Vector3.right, new Color(1f, 0.3f, 0.3f), "X");
            Axis(ax, Vector3.up, new Color(0.3f, 1f, 0.4f), "Y");
            Axis(ax, Vector3.forward, new Color(0.3f, 0.5f, 1f), "Z");

            gCad = Panel(new Vector3(3.6f, 2.9f, 0.6f), "CADENCE  rpm", Cyan, 130f);
            gPow = Panel(new Vector3(3.9f, 1.4f, 0.2f), "POWER  W", new Color(1f, 0.75f, 0.3f), 450f);
            gLean = Panel(new Vector3(3.6f, -0.1f, -0.2f), "LEAN  ±", new Color(1f, 0.4f, 0.8f), -1f);

            TitleBlock(Title, "A live digital twin of the bike: tilt it and pedal to see the physics.");
            rollText = StatCard(new Vector2(0, 0), new Vector2(40, 190), "Roll", "°", 250, Blue);
            crankText = StatCard(new Vector2(0, 0), new Vector2(40, 40), "Crank angle", "°", 250, Blue);
            cadText = StatCard(new Vector2(0, 1), new Vector2(40, -140), "Cadence", "rpm", 250, Cyan);
            powText = StatCard(new Vector2(0, 1), new Vector2(40, -290), "Power", "W", 250, new Color(1f, 0.75f, 0.3f));
            accText = Hud.Label(Hud.Layer, "", 22, new Vector2(1, 0), new Vector2(-40, 30), new Vector2(560, 90), TextAnchor.LowerRight, new Color(1, 1, 1, 0.6f));
        }

        void Axis(Transform parent, Vector3 dir, Color c, string label)
        {
            var m = Mats.Additive(c, 1.6f);
            Kit.Poly(parent, m, 0.04f, Vector3.zero, dir * 1.4f);
            var head = Geo.Prim(PrimitiveType.Cube, parent, dir * 1.45f, Vector3.one * 0.1f, Mats.Glow(c, 2f));
            Geo.WorldText(parent, label, dir * 1.7f, new Vector2(60, 60), 50, c).transform.parent.gameObject.AddComponent<Billboard>();
        }

        void BuildBike()
        {
            var line = Mats.Additive(Cyan, 1.6f);
            var faint = Mats.Additive(Cyan, 0.5f);
            bike = new GameObject("BikeTwin").transform;
            bike.SetParent(transform, false);
            bike.localPosition = new Vector3(0, 0, 0);

            Transform Wheel(string n, float x)
            {
                var w = new GameObject(n).transform;
                w.SetParent(bike, false);
                w.localPosition = new Vector3(x, 0.7f, 0);
                Kit.Ring(w, 0.7f, line, 0.04f, 48, flat: false);
                Kit.Ring(w, 0.62f, faint, 0.02f, 48, flat: false);
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * Mathf.PI * 2f;
                    Kit.Poly(w, faint, 0.015f, Vector3.zero, new Vector3(Mathf.Cos(a) * 0.62f, Mathf.Sin(a) * 0.62f, 0));
                }
                return w;
            }
            wheelR = Wheel("WheelR", -1.05f);
            wheelF = Wheel("WheelF", 1.05f);

            Vector3 bb = new Vector3(0, 0.7f, 0), seat = new Vector3(-0.3f, 1.55f, 0), head = new Vector3(0.75f, 1.6f, 0);
            Kit.Poly(bike, line, 0.04f, new Vector3(-1.05f, 0.7f, 0), bb, seat, new Vector3(-1.05f, 0.7f, 0)); // rear triangle
            Kit.Poly(bike, line, 0.04f, bb, head, seat);                                                     // front triangle
            Kit.Poly(bike, line, 0.04f, head, new Vector3(1.05f, 0.7f, 0));                                  // fork
            Kit.Poly(bike, line, 0.04f, head, head + new Vector3(0, 0.25f, 0), head + new Vector3(0.1f, 0.25f, 0.3f), head + new Vector3(0, 0.25f, 0), head + new Vector3(0.1f, 0.25f, -0.3f)); // bars
            Kit.Poly(bike, line, 0.04f, seat, seat + new Vector3(0, 0.15f, 0), seat + new Vector3(-0.25f, 0.15f, 0), seat + new Vector3(0.15f, 0.15f, 0)); // saddle

            crank = new GameObject("Crank").transform;
            crank.SetParent(bike, false);
            crank.localPosition = bb;
            Kit.Ring(crank, 0.2f, line, 0.03f, 24, flat: false);
            Kit.Poly(crank, Mats.Additive(new Color(1f, 0.75f, 0.3f), 2f), 0.05f, new Vector3(0, 0, 0.12f), new Vector3(0, -0.4f, 0.12f), new Vector3(0.12f, -0.4f, 0.25f));
            Kit.Poly(crank, Mats.Additive(new Color(1f, 0.75f, 0.3f), 2f), 0.05f, new Vector3(0, 0, -0.12f), new Vector3(0, 0.4f, -0.12f), new Vector3(0.12f, 0.4f, -0.25f));

            // ground contact shadow + roll arc
            Kit.Ring(transform, 1.6f, Mats.Additive(Blue, 0.4f), 0.03f, 64).transform.localPosition = new Vector3(0, 0.02f, 0);
        }

        Graph Panel(Vector3 pos, string label, Color c, float max)
        {
            var root = new GameObject("Graph " + label).transform;
            root.SetParent(transform, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, 28f, 0);
            Geo.Prim(PrimitiveType.Quad, root, Vector3.zero, new Vector3(3.2f, 1.7f, 1f), Mats.Glass(new Color(0.4f, 0.6f, 1f, 0.08f)));
            Kit.Poly(root, Mats.Additive(c, 0.5f), 0.015f, new Vector3(-1.6f, -0.85f, -0.01f), new Vector3(1.6f, -0.85f, -0.01f), new Vector3(1.6f, 0.85f, -0.01f), new Vector3(-1.6f, 0.85f, -0.01f), new Vector3(-1.6f, -0.85f, -0.01f));
            if (max < 0) Kit.Poly(root, Mats.Additive(Color.white, 0.25f), 0.01f, new Vector3(-1.4f, 0, -0.01f), new Vector3(1.4f, 0, -0.01f));
            var g = new Graph { Max = max };
            g.Line = Geo.Line(root, Mats.Additive(c, 2f), 0.035f);
            g.Line.useWorldSpace = false;
            g.Line.positionCount = Samples;
            Geo.WorldText(root, label, new Vector3(-0.9f, 0.68f, -0.02f), new Vector2(160, 30), 20, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft);
            g.Value = Geo.WorldText(root, "", new Vector3(1.05f, 0.62f, -0.02f), new Vector2(100, 40), 32, c, TextAnchor.MiddleRight);
            g.Push(0f);
            return g;
        }

        public override void Tick(in MotionState m, float dt)
        {
            orbit += dt * 8f;
            float a = (Mathf.Sin(orbit * Mathf.Deg2Rad * 2f) * 25f - 30f) * Mathf.Deg2Rad;
            Cam.transform.position = new Vector3(Mathf.Sin(a) * 8.5f, 3.2f, -Mathf.Cos(a) * 8.5f);
            Cam.transform.LookAt(new Vector3(1.0f, 1.2f, 0));

            float roll = m.Lean * 25f;
            bike.localRotation = Quaternion.Euler(roll, 0, 0);
            crank.localRotation = Quaternion.Euler(0, 0, -m.CrankAngle);
            dist += Kit.SpeedFromWatts(m.Watts) * dt;
            float wheelDeg = dist / (2f * Mathf.PI * 0.7f) * 360f;
            wheelF.localRotation = Quaternion.Euler(0, 0, -wheelDeg);
            wheelR.localRotation = Quaternion.Euler(0, 0, -wheelDeg);

            gCad.Push(m.Cadence); gCad.Value.text = m.Cadence.ToString("0");
            gPow.Push(m.Watts); gPow.Value.text = m.Watts.ToString("0");
            gLean.Push(m.Lean); gLean.Value.text = m.Lean.ToString("+0.00;-0.00");

            rollText.text = roll.ToString("+0;-0;0");
            crankText.text = m.CrankAngle.ToString("0");
            cadText.text = m.Cadence.ToString("0");
            powText.text = m.Watts.ToString("0");
            accText.text = $"accel {Input.acceleration.x:+0.00;-0.00}  {Input.acceleration.y:+0.00;-0.00}  {Input.acceleration.z:+0.00;-0.00} g\n" +
                           $"stillness {m.Stillness:0.00}   jitter {m.Jitter:0.000}   source {m.Source}";
        }
    }
}
