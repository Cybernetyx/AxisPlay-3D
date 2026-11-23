using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Hidden staff menu for calibration at the booth: hold the top-right corner for
    /// 3 seconds (or press F2). Pauses the current experience while open.
    /// </summary>
    public class StaffMenu : MonoBehaviour
    {
        public static bool Open { get; private set; }

        GameObject root;
        Text live, config, resetLabel;
        float hold;
        bool resetArmed;

        void Start()
        {
            var go = new GameObject("StaffCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 50;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;

            root = Hud.Stretch(go.transform, "Staff").gameObject;
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.75f);

            var p = Hud.Panel(root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 860), new Color(0.07f, 0.07f, 0.12f, 0.97f));
            Hud.Outline(p.gameObject);
            var t = p.transform;
            Hud.Label(t, "STAFF MENU  ·  CALIBRATION", 36, new Vector2(0, 1), new Vector2(40, -30), new Vector2(1000, 50), TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            live = Hud.Label(t, "", 24, new Vector2(1, 1), new Vector2(-40, -30), new Vector2(620, 300), TextAnchor.UpperLeft, new Color(0.6f, 1f, 0.85f));
            config = Hud.Label(t, "", 26, new Vector2(0, 1), new Vector2(40, -100), new Vector2(760, 240), TextAnchor.UpperLeft, Color.white);

            float y = -360f;
            Row(t, y, "Crank axis", new[] { "X", "Y", "Z" }, i => { MotionConfig.CrankAxis = i; });
            Row(t, y - 90f, "Lean axis", new[] { "X", "Y", "Z" }, i => { MotionConfig.LeanAxis = i; });
            Row(t, y - 180f, "Lean", new[] { "-", "+", "FLIP" }, i =>
            {
                if (i == 0) MotionConfig.LeanScale = Mathf.Sign(MotionConfig.LeanScale) * Mathf.Max(0.5f, Mathf.Abs(MotionConfig.LeanScale) - 0.25f);
                if (i == 1) MotionConfig.LeanScale = Mathf.Sign(MotionConfig.LeanScale) * Mathf.Min(6f, Mathf.Abs(MotionConfig.LeanScale) + 0.25f);
                if (i == 2) MotionConfig.LeanScale = -MotionConfig.LeanScale;
            });
            Row(t, y - 270f, "Knock threshold", new[] { "-", "+" }, i => MotionConfig.KnockThreshold = Mathf.Clamp(MotionConfig.KnockThreshold + (i == 0 ? -0.1f : 0.1f), 0.3f, 4f));
            Row(t, y - 360f, "Power scale", new[] { "-", "+" }, i => MotionConfig.WattsPerRpm2 = Mathf.Clamp(MotionConfig.WattsPerRpm2 + (i == 0 ? -0.0025f : 0.0025f), 0.005f, 0.08f));

            var reset = Hud.Button(t, "RESET ALL SCORES", new Vector2(1, 0), new Vector2(-40, 140), new Vector2(460, 80), new Color(0.6f, 0.15f, 0.2f, 0.9f), () =>
            {
                if (!resetArmed) { resetArmed = true; resetLabel.text = "TAP AGAIN TO CONFIRM"; return; }
                ScoreService.ResetAll();
                resetArmed = false;
                resetLabel.text = "SCORES CLEARED";
            }, 28);
            resetLabel = reset.GetComponentInChildren<Text>();
            Hud.Button(t, "SAVE & CLOSE", new Vector2(1, 0), new Vector2(-40, 40), new Vector2(460, 80), new Color(0.2f, 0.6f, 0.4f, 0.95f), () => SetOpen(false), 30);
            Hud.Button(t, "MENU", new Vector2(1, 0), new Vector2(-520, 40), new Vector2(220, 80), new Color(1, 1, 1, 0.15f), () => { SetOpen(false); App.I.ShowLauncher(); }, 28);

            root.SetActive(false);
        }

        void Row(Transform parent, float y, string label, string[] options, System.Action<int> pick)
        {
            Hud.Label(parent, label.ToUpperInvariant(), 26, new Vector2(0, 1), new Vector2(40, y), new Vector2(320, 70), TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.7f));
            for (int i = 0; i < options.Length; i++)
            {
                int k = i;
                Hud.Button(parent, options[i], new Vector2(0, 1), new Vector2(380 + i * 150, y), new Vector2(130, 70), new Color(1, 1, 1, 0.12f), () => pick(k), 30);
            }
        }

        void SetOpen(bool open)
        {
            Open = open;
            root.SetActive(open);
            resetArmed = false;
            if (resetLabel != null) resetLabel.text = "RESET ALL SCORES";
            if (!open) MotionConfig.Save();
        }

        void Update()
        {
            bool corner = false;
            Vector2 p = Vector2.zero;
            if (Input.touchCount > 0) { p = Input.GetTouch(0).position; corner = true; }
            else if (Input.GetMouseButton(0)) { p = Input.mousePosition; corner = true; }
            corner &= p.x > Screen.width * 0.9f && p.y > Screen.height * 0.85f;
            if (!corner) hold = 0f;
            else if (hold >= 0f) hold += Time.unscaledDeltaTime;
            if (hold > 3f && !Open) { hold = -1f; SetOpen(true); }
            if (Input.GetKeyDown(KeyCode.F2)) SetOpen(!Open);

            if (!Open || App.I == null) return;
            var m = App.I.Motion.State;
            string ax = "XYZ";
            config.text =
                $"Crank axis:  <b>{ax[MotionConfig.CrankAxis]}</b>\n" +
                $"Lean axis:   <b>{ax[MotionConfig.LeanAxis]}</b>   scale <b>{MotionConfig.LeanScale:+0.00;-0.00}</b>\n" +
                $"Knock threshold:  <b>{MotionConfig.KnockThreshold:0.0} g</b>\n" +
                $"Power scale:  <b>{MotionConfig.WattsPerRpm2:0.0000}</b>  (90 rpm = {MotionConfig.WattsPerRpm2 * 8100f:0} W)\n" +
                $"Rider counter: <b>{ScoreService.RiderNumber}</b>";
            live.text =
                $"LIVE SENSOR\n" +
                $"gyro  {Input.gyro.rotationRateUnbiased:F2}\n" +
                $"accel {Input.acceleration:F2}\n" +
                $"cadence {m.Cadence:0} rpm   power {m.Watts:0} W\n" +
                $"lean {m.Lean:+0.00;-0.00}   stillness {m.Stillness:0.00}\n" +
                $"knock {(m.Knock ? "KNOCK!" : "-")}   source {m.Source}\n" +
                $"displays {Display.displays.Length}   {1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f):0} fps";
        }
    }

    /// <summary>
    /// If a second display is connected, shows the live leaderboard and who is riding now.
    /// </summary>
    public class SecondScreen : MonoBehaviour
    {
        Text list, now, title;
        float refresh;

        void Start()
        {
            Display.displays[1].Activate();
            var camGo = new GameObject("Display2Camera");
            camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.targetDisplay = 1;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.07f);
            cam.cullingMask = 0;

            var go = new GameObject("Display2Canvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.targetDisplay = 1;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;

            var t = go.transform;
            Hud.Label(t, "<b>AXISPLAY</b>  ·  MOTION BOOTH", 34, new Vector2(0, 1), new Vector2(60, -40), new Vector2(900, 50), TextAnchor.UpperLeft, new Color(1, 1, 1, 0.8f));
            title = Hud.Label(t, "SUMMIT LEADERBOARD", 72, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(1600, 100), TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.3f), FontStyle.Bold);
            var p = Hud.Panel(t, new Vector2(0.5f, 0.5f), new Vector2(-260, -60), new Vector2(1000, 720), new Color(1, 1, 1, 0.06f));
            Hud.Outline(p.gameObject);
            list = Hud.Label(p.transform, "", 44, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 680), TextAnchor.UpperLeft, Color.white);
            var n = Hud.Panel(t, new Vector2(1, 0.5f), new Vector2(-60, -60), new Vector2(560, 720), new Color(1, 1, 1, 0.06f));
            Hud.Outline(n.gameObject);
            now = Hud.Label(n.transform, "", 40, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 680), TextAnchor.UpperLeft, Color.white);
        }

        void Update()
        {
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0f || App.I == null) return;
            refresh = 1f;

            bool today = ScoreService.Top("summit", 1, todayOnly: true).Count > 0;
            var top = ScoreService.Top("summit", 10, today);
            var sb = new System.Text.StringBuilder();
            if (top.Count == 0) sb.Append("\n\nNo climbs yet.\nBe the first to the summit!");
            for (int i = 0; i < top.Count; i++)
            {
                string medal = i == 0 ? "<color=#FFCC44>" : i == 1 ? "<color=#D0DCF0>" : i == 2 ? "<color=#E58040>" : "<color=#FFFFFF>";
                sb.Append($"{medal}<b>{i + 1,2}</b></color>   {top[i].name,-12}   <b>{top[i].value:0.0} s</b>\n");
            }
            list.text = sb.ToString();
            title.text = today ? "TODAY'S SUMMIT LEADERBOARD" : "SUMMIT LEADERBOARD";

            var cur = App.I.Current;
            var m = App.I.Motion.State;
            now.text = App.I.InLauncher || cur == null
                ? "<b>NOW</b>\n\nChoosing a ride…\n\nStep on the bike\nand pedal to play!"
                : $"<b>NOW RIDING</b>\n\n{ScoreService.CurrentRider}\n<color=#7FDBFF>{cur.Title}</color>\n\n<size=72><b>{m.Watts:0}</b></size> W\n<size=56><b>{m.Cadence:0}</b></size> rpm";
        }
    }
}
