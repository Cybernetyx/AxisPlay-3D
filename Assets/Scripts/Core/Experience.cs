using System;
using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Contract every booth experience follows: build the world in Begin(),
    /// react to motion in Tick(), call Finish(...) when done.
    /// The App owns camera, sun, post FX, HUD and idle reset.
    /// Also provides the shared HUD kit (title, stat cards, countdown, hint).
    /// </summary>
    public abstract class Experience : MonoBehaviour
    {
        protected App App { get; private set; }
        protected Hud Hud => App.Hud;
        protected Camera Cam => App.Cam;

        public abstract string Title { get; }
        public bool Finished { get; protected set; }
        public string ResultText { get; protected set; } = "";
        public string ResultDetail { get; protected set; } = "";

        /// <summary>Seconds without motion before returning to the launcher.</summary>
        public virtual float IdleTimeout => 45f;

        public void Init(App app) => App = app;

        public virtual void Begin() { }
        public virtual void Tick(in MotionState m, float dt) { }
        public virtual void End() { }

        protected void Finish(string result, string detail = "")
        {
            if (Finished) return;
            Finished = true;
            ResultText = result;
            ResultDetail = detail;
        }

        // ---------- HUD kit ----------

        /// <summary>Title + how-to line, top centre.</summary>
        protected void TitleBlock(string title, string howTo)
        {
            Hud.Label(Hud.Layer, title.ToUpperInvariant(), 40, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(1200, 56),
                TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Hud.Label(Hud.Layer, howTo, 24, new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(1300, 40),
                TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.65f));
        }

        /// <summary>Glass stat card. Returns the value text.</summary>
        protected Text StatCard(Vector2 anchor, Vector2 offset, string label, string unit = "", float width = 280f, Color? accent = null)
        {
            var p = Hud.Panel(Hud.Layer, anchor, offset, new Vector2(width, 128), Hud.Glass);
            Hud.Outline(p.gameObject);
            if (accent.HasValue)
                Hud.Panel(p.transform, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(6, 128), accent.Value);
            Hud.Label(p.transform, label.ToUpperInvariant(), 20, new Vector2(0, 1), new Vector2(22, -14), new Vector2(width - 30, 28),
                TextAnchor.UpperLeft, new Color(1, 1, 1, 0.6f));
            var v = Hud.Label(p.transform, "", 56, new Vector2(0, 0), new Vector2(20, 10), new Vector2(width - 30, 72),
                TextAnchor.LowerLeft, Color.white, FontStyle.Bold);
            if (unit.Length > 0)
                Hud.Label(p.transform, unit, 22, new Vector2(1, 0), new Vector2(-18, 20), new Vector2(120, 30),
                    TextAnchor.LowerRight, new Color(1, 1, 1, 0.55f));
            return v;
        }

        /// <summary>Horizontal progress bar with a label above it. Returns the fill.</summary>
        protected RectTransform ProgressBar(Vector2 anchor, Vector2 offset, float width, string label, Color fill)
        {
            Hud.Label(Hud.Layer, label.ToUpperInvariant(), 20, anchor, offset + new Vector2(0, 30), new Vector2(width, 28),
                TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.6f));
            return Hud.Bar(Hud.Layer, anchor, offset, new Vector2(width, 18), fill);
        }

        Text hintText;

        /// <summary>Big bottom-centre instruction. Call with null to hide.</summary>
        protected void Hint(string text)
        {
            if (hintText == null)
                hintText = Hud.Label(Hud.Layer, "", 40, new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(1500, 70),
                    TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            hintText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (text != null) hintText.text = text;
        }

        // 3-2-1-GO countdown. Call StartCountdown() in Begin, then `if (Counting(dt)) return;` in Tick.
        Text countText;
        float countdown = -1f, goTimer = -1f;

        protected void StartCountdown(float seconds = 3f)
        {
            countdown = seconds;
            countText = Hud.Label(Hud.Layer, "", 220, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(800, 300),
                TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        }

        /// <summary>True while the countdown is still running (game should wait).</summary>
        protected bool Counting(float dt)
        {
            if (goTimer >= 0f)
            {
                goTimer -= dt;
                if (goTimer < 0f) countText.gameObject.SetActive(false);
            }
            if (countdown < 0f) return false;
            countdown -= dt;
            if (countdown > 0f)
            {
                countText.text = Mathf.CeilToInt(countdown).ToString();
                float f = countdown - Mathf.Floor(countdown);
                countText.transform.localScale = Vector3.one * (0.8f + f * 0.4f);
                return true;
            }
            countdown = -1f;
            countText.text = "GO!";
            countText.transform.localScale = Vector3.one;
            goTimer = 0.8f;
            return false;
        }

        protected static string Clock(float seconds) =>
            seconds >= 60f ? $"{(int)(seconds / 60f)}:{seconds % 60f:00.0}" : $"{seconds:0.0}";
    }

    public class ExperienceInfo
    {
        public string Id, Title, Tagline;
        public Color Color;
        public int Phase; // build phase from the plan
        public Func<GameObject, Experience> Create;
        public bool Built;
    }

    /// <summary>The 16 booth experiences, in launcher order.</summary>
    public static class Catalog
    {
        public static readonly ExperienceInfo[] All =
        {
            E("summit", "Climb the Summit", "Your pedal power lifts you up the mountain", "#FF8A3D", 2, w => w.AddComponent<SummitClimb>()),
            E("globe", "Spin the Globe", "Pedal to spin the Earth, stop to land on a city", "#3DD6FF", 4, w => w.AddComponent<SpinGlobe>()),
            E("city", "Power the City", "Your watts light up the skyline, district by district", "#FFD23D", 3, w => w.AddComponent<PowerCity>()),
            E("race", "Head-to-Head Race", "Chase-cam sprint against the rival rider", "#FF3D8A", 4, w => w.AddComponent<HeadToHead>()),
            E("ghost", "Ghost Rider", "Race the hologram of the best ride today", "#8A7DFF", 3, w => w.AddComponent<GhostRider>()),
            E("scenic", "Scenic Ride", "Lean to choose your road through the valley", "#4DE08A", 4, w => w.AddComponent<ScenicRide>()),
            E("stillness", "Stillness Challenge", "Stay perfectly still to melt the ice", "#9FE8FF", 3, w => w.AddComponent<Stillness>()),
            E("dj", "Tempo DJ", "Your cadence sets the BPM", "#C04DFF", 3, w => w.AddComponent<TempoDJ>()),
            E("art", "Pedal-to-Develop Art", "Pedal to develop your Polaroid", "#FF6FD8", 5, w => w.AddComponent<DevelopArt>()),
            E("fingerprint", "Ride Fingerprint", "A 3D sculpture shaped by your rhythm", "#3DFFC8", 4, w => w.AddComponent<RideFingerprint>()),
            E("time", "Time Machine", "Pedal through the years and watch the city grow", "#FFB23D", 4, w => w.AddComponent<TimeMachine>()),
            E("physics", "Live Physics Dashboard", "A digital twin of the bike, live", "#4D8AFF", 4, w => w.AddComponent<PhysicsDashboard>()),
            E("leaderboard", "Summit Leaderboard", "Today's best climbers on the podium", "#FFD700", 2, w => w.AddComponent<Leaderboard>()),
            E("knock", "Knock to Interact", "Knock on the table to trigger the show", "#FF5A4D", 3, w => w.AddComponent<KnockShow>()),
            E("visuals", "Motion-Driven Visuals", "A particle tunnel that pulses to your pedalling", "#A04DFF", 3, w => w.AddComponent<MotionTunnel>()),
            E("heartbeat", "Booth Heartbeat", "The booth's crowd energy, hour by hour", "#FF4D6F", 4, w => w.AddComponent<BoothHeartbeat>()),
        };

        public static ExperienceInfo Find(string id) => Array.Find(All, e => e.Id == id);

        static ExperienceInfo E(string id, string title, string tagline, string hex, int phase, Func<GameObject, Experience> create = null)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            var info = new ExperienceInfo { Id = id, Title = title, Tagline = tagline, Color = c, Phase = phase, Built = create != null };
            info.Create = create ?? (w =>
            {
                var p = w.AddComponent<Placeholder>();
                p.Info = info;
                return p;
            });
            return info;
        }
    }
}
