using System;
using UnityEngine;

namespace AxisPlay
{
    /// <summary>
    /// Contract every booth experience follows: build the world in Begin(),
    /// react to motion in Tick(), set Finished + ResultText when done.
    /// The App owns camera, sun, post FX, HUD and idle reset.
    /// </summary>
    public abstract class Experience : MonoBehaviour
    {
        protected App App { get; private set; }
        protected Hud Hud => App.Hud;
        protected Camera Cam => App.Cam;

        public abstract string Title { get; }
        public bool Finished { get; protected set; }
        public string ResultText { get; protected set; } = "";

        public void Init(App app) => App = app;

        public virtual void Begin() { }
        public virtual void Tick(in MotionState m, float dt) { }
        public virtual void End() { }
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
            E("globe", "Spin the Globe", "Pedal to spin the Earth, stop to land on a city", "#3DD6FF", 4),
            E("city", "Power the City", "Your watts light up the skyline, district by district", "#FFD23D", 3),
            E("race", "Head-to-Head Race", "Chase-cam sprint against the rival rider", "#FF3D8A", 4),
            E("ghost", "Ghost Rider", "Race the hologram of the best ride today", "#8A7DFF", 3),
            E("scenic", "Scenic Ride", "Lean to choose your road through the valley", "#4DE08A", 4),
            E("stillness", "Stillness Challenge", "Stay perfectly still to melt the ice", "#9FE8FF", 3),
            E("dj", "Tempo DJ", "Your cadence sets the BPM", "#C04DFF", 3),
            E("art", "Pedal-to-Develop Art", "Pedal to develop an AI Polaroid", "#FF6FD8", 5),
            E("fingerprint", "Ride Fingerprint", "A 3D sculpture shaped by your rhythm", "#3DFFC8", 4),
            E("time", "Time Machine", "Pedal through the years and watch the city grow", "#FFB23D", 4),
            E("physics", "Live Physics Dashboard", "A digital twin of the bike, live", "#4D8AFF", 4),
            E("leaderboard", "Summit Leaderboard", "Today's best climbers on the podium", "#FFD700", 2),
            E("knock", "Knock to Interact", "Knock on the table to trigger the show", "#FF5A4D", 3),
            E("visuals", "Motion-Driven Visuals", "A particle tunnel that pulses to your pedalling", "#A04DFF", 3),
            E("heartbeat", "Booth Heartbeat", "The booth's crowd energy, hour by hour", "#FF4D6F", 4),
        };

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
