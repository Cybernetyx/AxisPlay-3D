using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Isometric night city of 36 buildings in 6 districts. Pedal power (watts → energy)
    /// charges the current district; when full, its windows and roof edges light up and
    /// an energy pulse runs through the streets to the next one. 45 s to light all six.
    /// </summary>
    public class PowerCity : Experience
    {
        public override string Title => "Power the City";

        const string Id = "city";
        const float TimeLimit = 45f;
        const float JoulesPerDistrict = 1400f; // ~200 W average lights all 6 in ~42 s
        static readonly Color Warm = new Color(1f, 0.78f, 0.35f);
        static readonly Color Cyan = new Color(0.3f, 0.85f, 1f);

        readonly Material[] windows = new Material[6];
        readonly Material[] roofs = new Material[6];
        readonly float[] lit = new float[6];
        List<Kit.Building> city;
        Transform pulse;
        LineRenderer grid;
        int district;
        float charge, timer, orbit, totalJ;
        bool started;
        Text wattText, energyText, districtText, timeText;
        RectTransform chargeBar;
        Light pulseLight;

        // District centres (grid of 3 x 2 blocks of the 6x6 city)
        static readonly int[] Order = { 0, 1, 2, 5, 4, 3 };

        public override void Begin()
        {
            App.Env(new Color(0.02f, 0.025f, 0.07f), new Color(0.12f, 0.14f, 0.25f), 30f, 90f);
            App.Sun.color = new Color(0.5f, 0.6f, 1f);
            App.Sun.intensity = 0.35f;
            App.Sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            Cam.fieldOfView = 35f;

            Geo.Prim(PrimitiveType.Plane, transform, new Vector3(0, -0.01f, 0), Vector3.one * 12f, Mats.Lit(new Color(0.03f, 0.035f, 0.06f), 0.4f));
            Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.0f, 0), new Vector3(26, 0.04f, 26), Mats.Lit(new Color(0.06f, 0.065f, 0.1f), 0.5f));

            for (int i = 0; i < 6; i++)
            {
                windows[i] = Mats.Unique(Mats.Glow(Warm, 0.05f));
                roofs[i] = Mats.Unique(Mats.Glow(Cyan, 0.1f));
            }
            var wall = Mats.Lit(new Color(0.1f, 0.11f, 0.17f), 0.2f);
            // Roof edges use per-district materials: build with a placeholder then swap.
            city = Kit.City(transform, windows, wall, roofs[0]);
            foreach (var b in city)
            {
                var roof = b.Root.GetChild(b.Root.childCount - 1).GetComponent<Renderer>();
                roof.sharedMaterial = roofs[b.District];
            }

            // Street grid lines (energy network)
            var gridMat = Mats.Additive(Cyan, 0.35f);
            for (int i = 0; i <= 6; i++)
            {
                float p = -12f + i * 4f;
                Kit.Poly(transform, gridMat, 0.08f, new Vector3(p, 0.05f, -12f), new Vector3(p, 0.05f, 12f));
                Kit.Poly(transform, gridMat, 0.08f, new Vector3(-12f, 0.05f, p), new Vector3(12f, 0.05f, p));
            }

            // Power plant at the edge: where the pulse starts.
            var plant = new GameObject("Plant").transform;
            plant.SetParent(transform, false);
            plant.localPosition = new Vector3(-16f, 0, -14f);
            Geo.Prim(PrimitiveType.Cylinder, plant, new Vector3(0, 1.5f, 0), new Vector3(2.5f, 1.5f, 2.5f), Mats.Lit(new Color(0.15f, 0.16f, 0.22f)));
            Geo.Prim(PrimitiveType.Cylinder, plant, new Vector3(0, 3.05f, 0), new Vector3(2.6f, 0.05f, 2.6f), Mats.Glow(Cyan, 2f));
            Geo.WorldText(plant, "YOUR POWER", new Vector3(0, 4.2f, 0), new Vector2(400, 60), 50, Cyan).transform.parent.gameObject.AddComponent<Billboard>();

            pulse = Geo.Prim(PrimitiveType.Sphere, transform, plant.localPosition + Vector3.up * 0.4f, Vector3.one * 0.6f, Mats.Glow(Cyan, 4f)).transform;
            pulseLight = Geo.PointLight(pulse, Vector3.zero, Cyan, 3f, 6f);
            grid = Kit.Poly(transform, Mats.Additive(Cyan, 2.2f), 0.18f, plant.localPosition, plant.localPosition);

            TitleBlock(Title, "Pedal to charge each district. Light all six before time runs out.");
            wattText = StatCard(new Vector2(0, 0.5f), new Vector2(40, 140), "Power", "W", 280, Warm);
            energyText = StatCard(new Vector2(0, 0.5f), new Vector2(40, -10), "Energy", "kJ", 280, Cyan);
            districtText = StatCard(new Vector2(1, 0.5f), new Vector2(-40, 140), "Districts lit", "/ 6", 280, Warm);
            timeText = StatCard(new Vector2(1, 0.5f), new Vector2(-40, -10), "Time left", "s", 280, Color.white);
            chargeBar = ProgressBar(new Vector2(0.5f, 0), new Vector2(0, 60), 900, "District charge", Warm);
            Hint("PEDAL TO SEND POWER");
            UpdatePulseTarget(0f);
        }

        Vector3 DistrictCentre(int d)
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var b in city) if (b.District == d) { sum += b.Pos; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        void UpdatePulseTarget(float t)
        {
            if (district >= 6) return;
            Vector3 from = district == 0 ? new Vector3(-16f, 0.4f, -14f) : DistrictCentre(Order[district - 1]) + Vector3.up * 0.4f;
            Vector3 to = DistrictCentre(Order[district]) + Vector3.up * 0.4f;
            Vector3 corner = new Vector3(to.x, 0.4f, from.z); // follow the streets: x then z
            Vector3 p = t < 0.5f ? Vector3.Lerp(from, corner, t * 2f) : Vector3.Lerp(corner, to, (t - 0.5f) * 2f);
            pulse.localPosition = p;
            grid.positionCount = t < 0.5f ? 2 : 3;
            grid.SetPosition(0, from);
            grid.SetPosition(1, t < 0.5f ? p : corner);
            if (t >= 0.5f) grid.SetPosition(2, p);
        }

        public override void Tick(in MotionState m, float dt)
        {
            orbit += dt * 6f;
            float ang = (orbit + 35f) * Mathf.Deg2Rad;
            Cam.transform.position = new Vector3(Mathf.Sin(ang) * 34f, 26f, -Mathf.Cos(ang) * 34f);
            Cam.transform.LookAt(new Vector3(0, 2f, 0));

            // lit districts glow up smoothly
            for (int i = 0; i < 6; i++)
            {
                float target = i < district ? 1f : 0f;
                lit[i] = Mathf.MoveTowards(lit[i], target, dt * 1.5f);
                int d = Order[i];
                float flicker = lit[i] > 0.99f ? 1f + Mathf.Sin(Time.time * 3f + i) * 0.06f : 1f;
                windows[d].SetColor("_EmissionColor", Warm * Mathf.Lerp(0.05f, 2.4f, lit[i]) * flicker);
                roofs[d].SetColor("_EmissionColor", Cyan * Mathf.Lerp(0.1f, 3f, lit[i]));
            }

            if (!Finished)
            {
                if (!started && m.Watts > 20f) { started = true; Hint(null); }
                if (started) timer += dt;
                float joules = m.Watts * dt;
                totalJ += joules;
                if (district < 6) charge += joules;
                if (district < 6 && charge >= JoulesPerDistrict)
                {
                    charge = 0f;
                    district++;
                    App.Audio.PlayOneShot(Kit.Clap);
                    if (district >= 6)
                    {
                        int rank = ScoreService.Record(Id, timer, lowerIsBetter: true);
                        Finish($"City lit in {timer:0.0} s", rank == 1 ? "NEW BEST!" : $"#{rank} today · {totalJ / 1000f:0.0} kJ generated");
                    }
                }
                if (timer >= TimeLimit && district < 6)
                    Finish($"{district} / 6 districts lit", $"{totalJ / 1000f:0.0} kJ generated · keep pedalling next time!");
            }

            float t = district < 6 ? charge / JoulesPerDistrict : 1f;
            UpdatePulseTarget(t);
            pulse.gameObject.SetActive(district < 6);
            pulse.localScale = Vector3.one * (0.5f + Mathf.Sin(Time.time * 10f) * 0.08f + m.Watts * 0.0008f);
            pulseLight.intensity = 2f + m.Watts * 0.01f;

            wattText.text = m.Watts.ToString("0");
            energyText.text = (totalJ / 1000f).ToString("0.0");
            districtText.text = district.ToString();
            timeText.text = Mathf.Max(0f, TimeLimit - timer).ToString("0");
            Hud.SetFill(chargeBar, t);
        }
    }
}
