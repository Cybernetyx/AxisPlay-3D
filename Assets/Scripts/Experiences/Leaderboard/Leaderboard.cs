using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// A 3D podium with gold, silver and bronze orbs for today's three fastest summit
    /// climbs, a spotlight and confetti, plus a glass top-10 board and other records.
    /// </summary>
    public class Leaderboard : Experience
    {
        public override string Title => "Summit Leaderboard";

        static readonly Color GoldC = new Color(1f, 0.8f, 0.25f);
        static readonly Color SilverC = new Color(0.82f, 0.86f, 0.95f);
        static readonly Color BronzeC = new Color(0.9f, 0.5f, 0.25f);

        readonly List<Transform> orbs = new List<Transform>();
        ParticleSystem confetti;
        float confettiT, orbit;

        public override void Begin()
        {
            App.Env(new Color(0.03f, 0.02f, 0.06f), new Color(0.2f, 0.18f, 0.28f), 15f, 45f);
            App.Sun.intensity = 0.2f;
            Cam.fieldOfView = 42f;

            Geo.Prim(PrimitiveType.Plane, transform, Vector3.zero, Vector3.one * 5f, Mats.Lit(new Color(0.05f, 0.04f, 0.08f), 0.9f));
            Kit.Ring(transform, 4.2f, Mats.Additive(GoldC, 0.5f), 0.05f).transform.localPosition = new Vector3(0, 0.02f, 0);

            bool today = ScoreService.Top("summit", 1, todayOnly: true).Count > 0;
            var top = ScoreService.Top("summit", 10, todayOnly: today);

            Step(0, new Vector3(0, 0, 0), 1.7f, GoldC, top.Count > 0 ? top[0] : null);
            Step(1, new Vector3(-2.3f, 0, 0.3f), 1.15f, SilverC, top.Count > 1 ? top[1] : null);
            Step(2, new Vector3(2.3f, 0, 0.3f), 0.8f, BronzeC, top.Count > 2 ? top[2] : null);

            var spot = new GameObject("Spot").AddComponent<Light>();
            spot.transform.SetParent(transform, false);
            spot.transform.localPosition = new Vector3(0, 9f, -3f);
            spot.transform.LookAt(new Vector3(0, 1.5f, 0));
            spot.type = LightType.Spot;
            spot.spotAngle = 40f;
            spot.range = 18f;
            spot.intensity = 30f;
            spot.color = new Color(1f, 0.95f, 0.85f);
            Geo.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 4.5f, -1.5f), new Vector3(3.2f, 4.5f, 3.2f), Mats.Additive(new Color(1f, 0.9f, 0.7f), 0.05f),
                rot: Quaternion.Euler(-18f, 0, 0));

            var c = new GameObject("Confetti").transform;
            c.SetParent(transform, false);
            c.localPosition = new Vector3(0, 6f, 0);
            confetti = Kit.Particles(c, Color.white, 0f, 0.14f, 4f, 2.5f, ParticleSystemShapeType.Box, new Vector3(7f, 0.2f, 3f));
            var main = confetti.main;
            main.gravityModifier = 0.25f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.4f, 0.6f), new Color(0.3f, 0.85f, 1f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);

            // glass board
            var board = Hud.Panel(Hud.Layer, new Vector2(1, 0.5f), new Vector2(-40, -20), new Vector2(560, 720), new Color(0.06f, 0.05f, 0.12f, 0.7f));
            Hud.Outline(board.gameObject);
            Hud.Label(board.transform, today ? "TODAY'S TOP 10" : "ALL-TIME TOP 10", 26, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(520, 40),
                TextAnchor.MiddleCenter, GoldC, FontStyle.Bold);
            if (top.Count == 0)
                Hud.Label(board.transform, "No climbs yet.\nBe the first to reach the summit!", 28, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 200),
                    TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.75f));
            for (int i = 0; i < top.Count; i++)
            {
                var col = i == 0 ? GoldC : i == 1 ? SilverC : i == 2 ? BronzeC : Color.white;
                float y = -90f - i * 60f;
                if (i < 3) Hud.Panel(board.transform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(520, 52), new Color(col.r, col.g, col.b, 0.12f));
                Hud.Label(board.transform, $"{i + 1}", 28, new Vector2(0, 1), new Vector2(30, y), new Vector2(50, 52), TextAnchor.MiddleLeft, col, FontStyle.Bold);
                Hud.Label(board.transform, top[i].name, 28, new Vector2(0, 1), new Vector2(90, y), new Vector2(260, 52), TextAnchor.MiddleLeft, Color.white);
                Hud.Label(board.transform, $"{top[i].value:0.0} s", 28, new Vector2(1, 1), new Vector2(-30, y), new Vector2(160, 52), TextAnchor.MiddleRight, col, FontStyle.Bold);
            }

            // other records
            string Rec(string id, string label, string unit, bool lower)
            {
                var l = ScoreService.Top(id, 1);
                return l.Count == 0 ? $"{label}: —" : $"{label}: {l[0].value:0.#}{unit}  ({l[0].name})";
            }
            var rec = Hud.Panel(Hud.Layer, new Vector2(0, 0), new Vector2(40, 40), new Vector2(620, 230), Hud.Glass);
            Hud.Outline(rec.gameObject);
            Hud.Label(rec.transform, "OTHER RECORDS", 22, new Vector2(0, 1), new Vector2(22, -14), new Vector2(580, 30), TextAnchor.UpperLeft, new Color(1, 1, 1, 0.6f));
            Hud.Label(rec.transform,
                Rec("race", "Head-to-Head", " s", true) + "\n" + Rec("ghost", "Ghost Rider", " s", true) + "\n" +
                Rec("city", "Power the City", " s", true) + "\n" + Rec("dj", "Tempo DJ", "%", false) + "\n" + Rec("knock", "Knock", " knocks", false),
                24, new Vector2(0, 1), new Vector2(22, -50), new Vector2(580, 170), TextAnchor.UpperLeft, Color.white);

            TitleBlock(Title, "Today's fastest climbers. Ride Climb the Summit to get on the podium.");
        }

        void Step(int place, Vector3 pos, float height, Color c, ScoreEntry e)
        {
            var root = new GameObject("Step" + (place + 1)).transform;
            root.SetParent(transform, false);
            root.localPosition = pos;
            Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, height / 2, 0), new Vector3(2.1f, height, 2f), Mats.Lit(new Color(0.12f, 0.11f, 0.16f), 0.5f));
            Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, height + 0.02f, 0), new Vector3(2.15f, 0.04f, 2.05f), Mats.Glow(c, 1.6f));
            Geo.WorldText(root, (place + 1).ToString(), new Vector3(0, height * 0.5f, -1.02f), new Vector2(120, 120), 90, c);

            var orb = new GameObject("Orb").transform;
            orb.SetParent(root, false);
            orb.localPosition = new Vector3(0, height + 0.75f, 0);
            float s = e != null ? 0.9f : 0.5f;
            Geo.Prim(PrimitiveType.Sphere, orb, Vector3.zero, Vector3.one * s, e != null ? Mats.Glow(c, 2.4f) : Mats.Lit(new Color(0.2f, 0.2f, 0.25f)));
            Kit.Ring(orb, s * 0.85f, Mats.Additive(c, e != null ? 1f : 0.2f), 0.025f);
            if (e != null) Geo.PointLight(orb, Vector3.zero, c, 2.5f, 4f);
            orbs.Add(orb);

            var label = Geo.WorldText(root, e != null ? $"{e.name}\n{e.value:0.0} s" : "—", new Vector3(0, height + 1.7f, 0), new Vector2(300, 110), 36, Color.white);
            label.transform.parent.gameObject.AddComponent<Billboard>();
        }

        public override void Tick(in MotionState m, float dt)
        {
            orbit += dt * 8f;
            float a = (Mathf.Sin(orbit * Mathf.Deg2Rad) * 20f) * Mathf.Deg2Rad;
            Cam.transform.position = new Vector3(Mathf.Sin(a) * 9f - 1.2f, 3.6f, -Mathf.Cos(a) * 9f);
            Cam.transform.LookAt(new Vector3(-1.2f, 1.6f, 0));

            for (int i = 0; i < orbs.Count; i++)
            {
                var o = orbs[i];
                o.localRotation = Quaternion.Euler(0, Time.time * 40f + i * 60f, 0);
                var p = o.localPosition;
                o.localPosition = new Vector3(p.x, p.y + Mathf.Sin(Time.time * 2f + i) * 0.003f, p.z);
            }

            confettiT -= dt;
            if (confettiT <= 0f || m.Knock)
            {
                Kit.Burst(confetti, 120);
                confettiT = 6f;
            }
        }
    }
}
