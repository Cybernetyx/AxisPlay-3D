using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// 3D carousel of the 16 experiences curving around the viewer, on a glowing
    /// grid floor, with key light that takes the selected card's colour.
    /// Navigate: arrows, swipe, lean, or tap a side card. Start: Enter, Start button,
    /// tap the front card, or knock.
    /// </summary>
    public class Launcher : Experience
    {
        public int StartIndex;
        public override string Title => "AxisPlay";

        const float Radius = 7f;
        static int Count => Catalog.All.Length;
        static float Step => 360f / Count;

        Transform ring;
        readonly List<Transform> cards = new List<Transform>();
        readonly List<Material> frameMats = new List<Material>();
        int sel;
        float yaw, targetYaw, leanCooldown, t;
        Vector3 pointerDown;
        bool pointerHeld;
        Light keyLight;
        Material floorGlow;
        Text title, tagline, status;
        Image startBg;

        public override void Begin()
        {
            sel = ((StartIndex % Count) + Count) % Count;
            targetYaw = yaw = -sel * Step;

            App.Env(new Color(0.02f, 0.02f, 0.06f), new Color(0.18f, 0.18f, 0.28f), 6f, 28f);
            App.Sun.intensity = 0.25f;
            App.Sun.transform.rotation = Quaternion.Euler(60f, -20f, 0f);
            Cam.fieldOfView = 58f;
            Cam.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, 0f), Quaternion.Euler(5f, 0f, 0f));

            BuildFloor();
            BuildRing();
            keyLight = Geo.PointLight(transform, new Vector3(0f, 3.2f, 4.5f), Catalog.All[sel].Color, 6f, 14f);
            BuildHud();
            Refresh();
        }

        void BuildFloor()
        {
            Geo.Prim(PrimitiveType.Plane, transform, Vector3.zero, Vector3.one * 8f, Mats.Lit(new Color(0.03f, 0.03f, 0.06f), 0.85f));
            floorGlow = Mats.Unique(Mats.Glow(new Color(0.3f, 0.4f, 1f), 1.2f));
            for (int i = -20; i <= 20; i += 2)
            {
                Geo.Prim(PrimitiveType.Cube, transform, new Vector3(i, 0.005f, 0f), new Vector3(0.025f, 0.01f, 80f), floorGlow);
                Geo.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 0.005f, i), new Vector3(80f, 0.01f, 0.025f), floorGlow);
            }
        }

        void BuildRing()
        {
            ring = new GameObject("Ring").transform;
            ring.SetParent(transform, false);
            ring.localPosition = new Vector3(0f, 1.55f, 0f);

            for (int i = 0; i < Count; i++)
            {
                var info = Catalog.All[i];
                var pivot = new GameObject("Pivot" + i).transform;
                pivot.SetParent(ring, false);
                pivot.localRotation = Quaternion.Euler(0f, i * Step, 0f);

                var card = new GameObject("Card").transform;
                card.SetParent(pivot, false);
                card.localPosition = new Vector3(0f, 0f, Radius);
                cards.Add(card);

                // Face (clickable): name carries the index for raycasts.
                var face = Geo.Prim(PrimitiveType.Quad, card, Vector3.zero, new Vector3(2.1f, 2.9f, 1f),
                    Mats.Lit(Color.Lerp(new Color(0.05f, 0.05f, 0.09f), info.Color, 0.18f), 0.6f), keepCollider: true);
                face.name = "card_" + i;

                // Glowing frame.
                var fm = Mats.Unique(Mats.Glow(info.Color, 1.5f));
                frameMats.Add(fm);
                float w = 2.1f, h = 2.9f, e = 0.04f;
                Geo.Prim(PrimitiveType.Cube, card, new Vector3(0, h / 2, -0.01f), new Vector3(w + e, e, e), fm);
                Geo.Prim(PrimitiveType.Cube, card, new Vector3(0, -h / 2, -0.01f), new Vector3(w + e, e, e), fm);
                Geo.Prim(PrimitiveType.Cube, card, new Vector3(-w / 2, 0, -0.01f), new Vector3(e, h, e), fm);
                Geo.Prim(PrimitiveType.Cube, card, new Vector3(w / 2, 0, -0.01f), new Vector3(e, h, e), fm);

                // Icon: a glowing shape floating in front of the card.
                var icon = Geo.Prim(i % 3 == 0 ? PrimitiveType.Sphere : i % 3 == 1 ? PrimitiveType.Cube : PrimitiveType.Capsule,
                    card, new Vector3(0f, 0.45f, -0.35f), Vector3.one * 0.55f, Mats.Glow(info.Color, 2.5f));
                icon.name = "Icon";

                Geo.WorldText(card, (i + 1).ToString("00"), new Vector3(-0.72f, 1.22f, -0.02f), new Vector2(60, 40), 30,
                    new Color(1, 1, 1, 0.55f), TextAnchor.UpperLeft);
                Geo.WorldText(card, info.Title.ToUpperInvariant(), new Vector3(0f, -0.75f, -0.02f), new Vector2(190, 90), 22, Color.white);
                if (!info.Built)
                    Geo.WorldText(card, "PHASE " + info.Phase, new Vector3(0f, -1.2f, -0.02f), new Vector2(190, 30), 14,
                        new Color(1, 1, 1, 0.45f));

                // Floor reflection stand-in: a faint additive glow under each card.
                Geo.Prim(PrimitiveType.Quad, card, new Vector3(0f, -1.53f, -0.1f), new Vector3(2.0f, 0.6f, 1f),
                    Mats.Additive(info.Color, 0.18f), rot: Quaternion.Euler(90f, 0f, 0f));
            }
        }

        void BuildHud()
        {
            var L = Hud.Layer;
            Hud.Label(L, "<b>AXISPLAY</b>  ·  MOTION BOOTH", 30, new Vector2(0, 1), new Vector2(48, -40), new Vector2(800, 50),
                TextAnchor.UpperLeft, new Color(1, 1, 1, 0.85f));
            Hud.Label(L, "LEAN · SWIPE · ARROWS to browse        KNOCK or START to play", 22, new Vector2(1, 1),
                new Vector2(-48, -46), new Vector2(900, 40), TextAnchor.UpperRight, new Color(1, 1, 1, 0.5f));

            var panel = Hud.Panel(L, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1100, 210), new Color(0.06f, 0.06f, 0.12f, 0.7f));
            Hud.Outline(panel.gameObject);
            title = Hud.Label(panel.transform, "", 54, new Vector2(0, 1), new Vector2(40, -24), new Vector2(720, 70),
                TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            tagline = Hud.Label(panel.transform, "", 26, new Vector2(0, 1), new Vector2(42, -100), new Vector2(720, 70),
                TextAnchor.UpperLeft, new Color(1, 1, 1, 0.7f));
            status = Hud.Label(panel.transform, "", 20, new Vector2(0, 0), new Vector2(42, 18), new Vector2(720, 30),
                TextAnchor.LowerLeft, new Color(1, 1, 1, 0.5f));
            var start = Hud.Button(panel.transform, "START", new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(260, 110),
                Color.white, StartSelected, 40);
            startBg = start.GetComponent<Image>();
        }

        void Refresh()
        {
            var info = Catalog.All[sel];
            title.text = info.Title;
            tagline.text = info.Tagline;
            status.text = $"{sel + 1:00} / {Count}   ·   PLAYED {ScoreService.Plays(info.Id)} TIMES";
            var c = info.Color;
            startBg.color = new Color(c.r, c.g, c.b, 0.85f);
        }

        void Move(int d)
        {
            if (d == 0) return;
            sel = ((sel + d) % Count + Count) % Count;
            targetYaw -= d * Step;
            Refresh();
        }

        void JumpTo(int i)
        {
            int d = i - sel;
            if (d > Count / 2) d -= Count;
            if (d < -Count / 2) d += Count;
            Move(d);
        }

        void StartSelected() => App.Launch(sel);

        public override void Tick(in MotionState m, float dt)
        {
            t += dt;
            if (Input.GetKeyDown(KeyCode.RightArrow)) Move(1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Move(-1);

            leanCooldown -= dt;
            if (Mathf.Abs(m.Lean) > 0.6f && leanCooldown <= 0f)
            {
                Move(m.Lean > 0 ? 1 : -1);
                leanCooldown = 0.7f;
            }

            if (Input.GetMouseButtonDown(0) && !Hud.PointerOverUI())
            {
                pointerDown = Input.mousePosition;
                pointerHeld = true;
            }
            if (Input.GetMouseButtonUp(0) && pointerHeld)
            {
                pointerHeld = false;
                Vector3 d = Input.mousePosition - pointerDown;
                if (Mathf.Abs(d.x) > Screen.width * 0.06f) Move(d.x < 0 ? 1 : -1);
                else if (TapCard(Input.mousePosition)) return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || m.Knock)
            {
                StartSelected();
                return;
            }

            yaw = Mathf.Lerp(yaw, targetYaw, 1f - Mathf.Exp(-7f * dt));
            ring.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var c = Catalog.All[sel].Color;
            keyLight.color = Color.Lerp(keyLight.color, c, 1f - Mathf.Exp(-4f * dt));
            floorGlow.SetColor("_EmissionColor", Color.Lerp(new Color(0.3f, 0.4f, 1f), c, 0.5f) * 1.2f);

            for (int i = 0; i < cards.Count; i++)
            {
                bool on = i == sel;
                var card = cards[i];
                float s = Mathf.Lerp(card.localScale.x, on ? 1.12f : 0.92f, 1f - Mathf.Exp(-8f * dt));
                card.localScale = Vector3.one * s;
                card.localPosition = new Vector3(0f, on ? Mathf.Sin(t * 2f) * 0.05f : 0f, Radius);
                frameMats[i].SetColor("_EmissionColor", Catalog.All[i].Color * (on ? 3f : 0.8f));
                var icon = card.Find("Icon");
                if (icon) icon.localRotation = Quaternion.Euler(20f, t * (on ? 60f : 15f) + i * 20f, 0f);
            }

            // Gentle idle camera sway.
            Cam.transform.rotation = Quaternion.Euler(5f + Mathf.Sin(t * 0.4f) * 0.6f, Mathf.Sin(t * 0.3f) * 1.2f, 0f);
        }

        bool TapCard(Vector3 screenPos)
        {
            if (!Physics.Raycast(Cam.ScreenPointToRay(screenPos), out var hit, 50f)) return false;
            var n = hit.collider.name;
            if (!n.StartsWith("card_") || !int.TryParse(n.Substring(5), out int i)) return false;
            if (i == sel) { StartSelected(); return true; }
            JumpTo(i);
            return false;
        }
    }
}
