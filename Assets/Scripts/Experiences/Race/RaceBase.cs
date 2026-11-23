using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Shared 400 m race: glowing track, solid player rider vs hologram rival,
    /// chase camera, speed lines, countdown and a two-marker progress bar.
    /// </summary>
    public abstract class RaceBase : Experience
    {
        protected const float Length = 400f;
        protected const float Lane = 1.4f;

        protected Kit.RiderRig player, rival;
        protected float pDist, pSpeed, rDist, raceTime;
        protected bool racing, rivalDone;
        protected float rivalTime;

        protected abstract string RivalName { get; }
        protected abstract Color RivalColor { get; }
        protected abstract Color EdgeColor { get; }
        protected abstract void SetupSky();
        /// <summary>Rival distance at race time t.</summary>
        protected abstract float RivalDistance(float t, float dt);
        protected abstract void PlayerFinished();
        protected virtual void RaceTick(in MotionState m, float dt) { }

        Text speedText, gapText, timeText, gapLabel;
        RectTransform markP, markR;
        ParticleSystem speedLines;
        ParticleSystem.EmissionModule speedEmission;
        TrailRenderer pTrail, rTrail;

        public override void Begin()
        {
            SetupSky();
            Kit.Track(transform, Length, 6f, EdgeColor, new Color(0.03f, 0.03f, 0.06f));

            player = Kit.Rider(transform, Mats.Lit(new Color(0.95f, 0.95f, 1f), 0.3f), Mats.Lit(new Color(0.15f, 0.15f, 0.2f), 0.6f),
                Mats.Glow(new Color(0.3f, 0.85f, 1f), 2f));
            player.Root.localPosition = new Vector3(-Lane, 0, 0);
            rival = Kit.Rider(transform, Mats.Additive(RivalColor, 0.45f), Mats.Additive(RivalColor, 0.7f), Mats.Additive(RivalColor, 1.6f));
            rival.Root.localPosition = new Vector3(Lane, 0, 0);
            Geo.PointLight(rival.Root, new Vector3(0, 1.2f, 0), RivalColor, 2f, 5f);

            pTrail = Trail(player.Root, new Color(0.3f, 0.85f, 1f));
            rTrail = Trail(rival.Root, RivalColor);

            // speed lines streaming past the camera
            var sl = new GameObject("SpeedLines").transform;
            sl.SetParent(Cam.transform, false);
            sl.localPosition = new Vector3(0, 0, 24f);
            sl.localRotation = Quaternion.Euler(0, 180f, 0);
            speedLines = Kit.Particles(sl, new Color(0.7f, 0.9f, 1f, 0.6f), 0f, 0.05f, 0.5f, 40f, ParticleSystemShapeType.Circle, Vector3.one, world: false);
            var sls = speedLines.shape;
            sls.radius = 7f;
            sls.radiusThickness = 0.15f; // emit from a ring so streaks pass around the view, never through it
            var r = speedLines.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 6f;
            r.velocityScale = 0.05f;
            speedEmission = speedLines.emission;

            TitleBlock(Title, $"Pedal to race {RivalName.ToLowerInvariant()} over {Length:0} m");
            speedText = StatCard(new Vector2(0, 0), new Vector2(40, 40), "Speed", "km/h", 260, new Color(0.3f, 0.85f, 1f));
            timeText = StatCard(new Vector2(1, 0), new Vector2(-40, 40), "Time", "s", 260, Color.white);
            var gapPanel = Hud.Panel(Hud.Layer, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(460, 110), Hud.Glass);
            Hud.Outline(gapPanel.gameObject);
            gapLabel = Hud.Label(gapPanel.transform, "", 20, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(440, 28), TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.6f));
            gapText = Hud.Label(gapPanel.transform, "", 52, new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(440, 70), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

            // progress bar with two markers
            var bar = Hud.Panel(Hud.Layer, new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(900, 10), new Color(1, 1, 1, 0.15f));
            Hud.Label(Hud.Layer, "START", 18, new Vector2(0.5f, 0), new Vector2(-480, 64), new Vector2(80, 24), TextAnchor.MiddleRight, new Color(1, 1, 1, 0.5f));
            Hud.Label(Hud.Layer, "FINISH", 18, new Vector2(0.5f, 0), new Vector2(490, 64), new Vector2(90, 24), TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.5f));
            markR = Marker(bar.transform, RivalColor);
            markP = Marker(bar.transform, new Color(0.3f, 0.85f, 1f));

            StartCountdown();
            PlaceCamera(0f, true);
        }

        static RectTransform Marker(Transform bar, Color c)
        {
            var img = Hud.Panel(bar, new Vector2(0, 0.5f), Vector2.zero, new Vector2(22, 34), c);
            return img.rectTransform;
        }

        static TrailRenderer Trail(Transform t, Color c)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(t, false);
            go.transform.localPosition = new Vector3(0, 0.35f, -0.6f);
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Mats.Additive(c, 1.4f);
            tr.time = 0.9f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0, 0.22f), new Keyframe(1, 0f));
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.minVertexDistance = 0.3f;
            return tr;
        }

        void PlaceCamera(float dt, bool snap)
        {
            Vector3 p = player.Root.position;
            Vector3 want = p + new Vector3(Lane * 0.6f, 2.3f, -5.8f);
            Cam.transform.position = snap ? want : Kit.Damp(Cam.transform.position, want, 6f, dt);
            Cam.transform.rotation = Quaternion.LookRotation(p + new Vector3(Lane * 0.5f, 1.0f, 8f) - Cam.transform.position);
            Cam.fieldOfView = snap ? 55f : Kit.Damp(Cam.fieldOfView, 55f + pSpeed * 1.2f, 2f, dt);
        }

        public override void Tick(in MotionState m, float dt)
        {
            bool counting = Counting(dt);
            if (!counting && !racing && !Finished) racing = true;

            if (racing)
            {
                raceTime += dt;
                pSpeed = Kit.Damp(pSpeed, Kit.SpeedFromWatts(m.Watts), 1.2f, dt);
                pDist = Mathf.Min(Length, pDist + pSpeed * dt);
                float rd = RivalDistance(raceTime, dt);
                if (!rivalDone && rd >= Length) { rivalDone = true; rivalTime = raceTime; }
                rDist = Mathf.Min(Length, rd);
                RaceTick(m, dt);
                if (pDist >= Length) { racing = false; PlayerFinished(); }
            }
            else if (Finished)
            {
                pSpeed = Mathf.MoveTowards(pSpeed, 0f, 4f * dt);
                pDist += pSpeed * dt;
            }

            player.Root.localPosition = new Vector3(-Lane, 0, pDist);
            rival.Root.localPosition = new Vector3(Lane, 0, rDist);
            player.Animate(pDist, m.CrankAngle, m.Lean * 0.5f);
            rival.Animate(rDist, raceTime * 520f % 360f, 0f);
            PlaceCamera(dt, false);
            speedEmission.rateOverTime = pSpeed * 9f;

            float gap = pDist - rDist;
            speedText.text = (pSpeed * 3.6f).ToString("0");
            timeText.text = raceTime.ToString("0.0");
            gapLabel.text = gap >= 0 ? "YOUR LEAD" : $"{RivalName.ToUpperInvariant()} LEADS";
            gapText.text = $"{Mathf.Abs(gap):0.0} m";
            gapText.color = gap >= 0 ? new Color(0.4f, 1f, 0.7f) : new Color(1f, 0.5f, 0.6f);
            markP.anchorMin = markP.anchorMax = new Vector2(pDist / Length, 0.5f);
            markR.anchorMin = markR.anchorMax = new Vector2(rDist / Length, 0.5f);
        }
    }

    /// <summary>Sunset head-to-head race against a paced rival rider.</summary>
    public class HeadToHead : RaceBase
    {
        public override string Title => "Head-to-Head Race";
        protected override string RivalName => "The rival";
        protected override Color RivalColor => new Color(1f, 0.3f, 0.55f);
        protected override Color EdgeColor => new Color(1f, 0.35f, 0.6f);

        float rivalSpeed, rivalD, seed;

        protected override void SetupSky()
        {
            App.Env(new Color(0.42f, 0.18f, 0.3f), new Color(0.45f, 0.32f, 0.45f), 60f, 380f);
            App.Sun.color = new Color(1f, 0.55f, 0.35f);
            App.Sun.intensity = 1.2f;
            App.Sun.transform.rotation = Quaternion.Euler(10f, 180f, 0f);
            // sun disc + skyline beyond the finish
            Geo.Prim(PrimitiveType.Sphere, transform, new Vector3(0, 30f, Length + 260f), Vector3.one * 70f, Mats.Additive(new Color(1f, 0.6f, 0.3f), 0.9f));
            Kit.Skyline(transform, new Vector3(0, 0, Length + 60f), 220f, 60f, new Color(0.18f, 0.1f, 0.2f), 60, 11);
            seed = Random.Range(0f, 100f);
        }

        protected override float RivalDistance(float t, float dt)
        {
            // ~29 km/h with surges, and a kick in the last 80 m
            float target = 8.1f + Mathf.Sin(t * 0.7f + seed) * 0.7f + Mathf.PerlinNoise(t * 0.3f, seed) * 0.8f;
            if (rivalD > Length - 80f) target += 1.4f;
            rivalSpeed = Kit.Damp(rivalSpeed, target, 1f, dt);
            rivalD += rivalSpeed * dt;
            return rivalD;
        }

        protected override void PlayerFinished()
        {
            bool win = !rivalDone;
            int rank = ScoreService.Record("race", raceTime, lowerIsBetter: true);
            if (win) Finish($"YOU WIN!  {raceTime:0.0} s", $"by {pDist - rDist:0.0} m · #{rank} today");
            else Finish($"Rival wins by {raceTime - rivalTime:0.0} s", $"Your time {raceTime:0.0} s · #{rank} today");
        }
    }
}
