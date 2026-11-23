using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Screen-space HUD built in code (glass panels, labels, bars, buttons).
    /// Layer is cleared on every experience switch. F1 or a 3-finger tap shows
    /// the sensor debug overlay (use it for the Week 0 hardware check).
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static readonly Color Glass = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color GlassEdge = new Color(1f, 1f, 1f, 0.18f);

        public RectTransform Layer { get; private set; }
        public static Font Font { get; private set; }

        Text debugText;
        float fps;

        public static Hud Create(Transform parent)
        {
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(parent, false);
            }

            var hud = go.AddComponent<Hud>();
            hud.Layer = Stretch(go.transform, "Layer");
            var dbg = Stretch(go.transform, "Debug");
            var bg = Panel(dbg, new Vector2(1, 0), new Vector2(-20, 20), new Vector2(560, 330), new Color(0, 0, 0, 0.6f));
            hud.debugText = Label(bg.transform, "", 22, new Vector2(0, 1), new Vector2(18, -14), new Vector2(530, 310), TextAnchor.UpperLeft, Color.white);
            dbg.gameObject.SetActive(false);
            return hud;
        }

        public void Clear()
        {
            for (int i = Layer.childCount - 1; i >= 0; i--) Destroy(Layer.GetChild(i).gameObject);
        }

        void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            var dbg = debugText.transform.parent.parent.gameObject;
            if (Input.GetKeyDown(KeyCode.F1) || (Input.touchCount == 3 && Input.GetTouch(2).phase == TouchPhase.Began))
                dbg.SetActive(!dbg.activeSelf);
            if (!dbg.activeSelf || App.I == null) return;

            var m = App.I.Motion.State;
            debugText.text =
                $"<b>SENSOR CHECK</b>   {fps:0} fps   {Screen.width}x{Screen.height}\n" +
                $"gyro supported: {SystemInfo.supportsGyroscope}   accel: {SystemInfo.supportsAccelerometer}\n" +
                $"gyro rate: {Input.gyro.rotationRateUnbiased:F2}\n" +
                $"accel (g): {Input.acceleration:F2}\n" +
                $"source: {m.Source}\n" +
                $"cadence {m.Cadence:0} rpm   watts {m.Watts:0}\n" +
                $"lean {m.Lean:+0.00;-0.00}   crank {m.CrankAngle:0}°\n" +
                $"stillness {m.Stillness:0.00}   jitter {m.Jitter:0.000}\n" +
                $"knock {(m.Knock ? "KNOCK!" : "-")}   displays: {Display.displays.Length}\n" +
                $"GPU: {SystemInfo.graphicsDeviceName}";
        }

        public void ShowResult(string title, string result)
        {
            var p = Panel(Layer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 420), new Color(0.05f, 0.05f, 0.1f, 0.85f));
            Outline(p.gameObject);
            Label(p.transform, title.ToUpperInvariant(), 34, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(860, 60), TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.7f));
            Label(p.transform, result, 64, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(860, 200), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Label(p.transform, "Knock or tap MENU to play again", 26, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(860, 50), TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.6f));
        }

        // ---------- builders ----------

        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform Place(GameObject go, Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, Vector2 anchor, Vector2 offset, Vector2 size, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            Place(go, parent, anchor, offset, size);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static void Outline(GameObject go)
        {
            var o = go.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = GlassEdge;
            o.effectDistance = new Vector2(2, -2);
        }

        public static Text Label(Transform parent, string text, int size, Vector2 anchor, Vector2 offset, Vector2 box,
            TextAnchor align, Color color, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            Place(go, parent, anchor, offset, box);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static UnityEngine.UI.Button Button(Transform parent, string text, Vector2 anchor, Vector2 offset, Vector2 size,
            Color color, Action onClick, int fontSize = 32)
        {
            var img = Panel(parent, anchor, offset, size, color);
            img.raycastTarget = true;
            Outline(img.gameObject);
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.onClick.AddListener(() => onClick());
            Label(img.transform, text, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            return b;
        }

        /// <summary>A bar; set fill with SetFill(rt, value). vertical fills bottom-up.</summary>
        public static RectTransform Bar(Transform parent, Vector2 anchor, Vector2 offset, Vector2 size, Color fill, bool vertical = false)
        {
            var bg = Panel(parent, anchor, offset, size, new Color(1, 1, 1, 0.1f));
            var f = Stretch(bg.transform, "Fill");
            f.gameObject.AddComponent<Image>().color = fill;
            f.GetComponent<Image>().raycastTarget = false;
            f.name = vertical ? "FillV" : "FillH";
            SetFill(f, 0f);
            return f;
        }

        public static void SetFill(RectTransform fill, float v)
        {
            v = Mathf.Clamp01(v);
            fill.anchorMax = fill.name == "FillV" ? new Vector2(1, v) : new Vector2(v, 1);
        }

        public static bool PointerOverUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return es.IsPointerOverGameObject();
        }
    }
}
