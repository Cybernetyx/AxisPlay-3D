using System.Collections.Generic;
using UnityEngine;

namespace AxisPlay
{
    /// <summary>What every experience sees. No experience reads sensors directly.</summary>
    public struct MotionState
    {
        public float Cadence;    // rpm, smoothed
        public float Watts;      // estimated from cadence
        public float Lean;       // -1 (left) .. +1 (right)
        public float CrankAngle; // degrees, 0..360, integrated from cadence
        public float Stillness;  // 0 (moving) .. 1 (perfectly still)
        public float Jitter;     // raw shake level, g
        public bool Knock;       // true for one frame when a knock is detected
        public bool Active;      // someone is interacting right now
        public string Source;    // which source is driving cadence
    }

    public struct RawSample
    {
        public float Rpm, Lean, Jitter;
        public bool Knock;
    }

    public interface IMotionSource
    {
        bool Available { get; }
        string Name { get; }
        RawSample Sample(float dt);
    }

    /// <summary>
    /// Tunable mapping from the physical sensor to motion values.
    /// Adjust on the real bike (staff menu later); values persist in PlayerPrefs.
    /// </summary>
    public static class MotionConfig
    {
        public static int CrankAxis = PlayerPrefs.GetInt("cfg.crankAxis", 2); // 0=x 1=y 2=z of gyro
        public static int LeanAxis = PlayerPrefs.GetInt("cfg.leanAxis", 0);   // accelerometer axis
        public static float LeanScale = PlayerPrefs.GetFloat("cfg.leanScale", 2.0f);
        public static float KnockThreshold = PlayerPrefs.GetFloat("cfg.knock", 1.1f); // g change per frame
        public static float WattsPerRpm2 = PlayerPrefs.GetFloat("cfg.watts", 0.025f); // 90 rpm ~ 200 W

        public static void Save()
        {
            PlayerPrefs.SetInt("cfg.crankAxis", CrankAxis);
            PlayerPrefs.SetInt("cfg.leanAxis", LeanAxis);
            PlayerPrefs.SetFloat("cfg.leanScale", LeanScale);
            PlayerPrefs.SetFloat("cfg.knock", KnockThreshold);
            PlayerPrefs.SetFloat("cfg.watts", WattsPerRpm2);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Merges all sources (real gyro + simulator) into one filtered MotionState.</summary>
    public class MotionEngine : MonoBehaviour
    {
        public MotionState State;
        public float SecondsSinceActive => Time.time - lastActive;

        readonly List<IMotionSource> sources = new List<IMotionSource>();
        float cadence, lean, crank, still = 1f, lastActive;

        void Awake()
        {
            sources.Add(new GyroSource());
            sources.Add(new SimulatorSource());
        }

        static float Smooth(float a, float b, float k, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-k * dt));

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            RawSample best = default;
            string src = "none";

            foreach (var s in sources)
            {
                if (!s.Available) continue;
                var r = s.Sample(dt);
                if (r.Rpm > best.Rpm) { best.Rpm = r.Rpm; src = s.Name; }
                if (Mathf.Abs(r.Lean) > Mathf.Abs(best.Lean)) best.Lean = r.Lean;
                best.Jitter = Mathf.Max(best.Jitter, r.Jitter);
                best.Knock |= r.Knock;
            }

            cadence = Smooth(cadence, best.Rpm, 5f, dt);
            if (cadence < 0.5f && best.Rpm == 0f) cadence = 0f;
            lean = Smooth(lean, Mathf.Clamp(best.Lean, -1f, 1f), 10f, dt);
            crank = Mathf.Repeat(crank + cadence * 6f * dt, 360f); // rpm * 360 / 60
            float calm = 1f - Mathf.Clamp01(best.Jitter / 0.06f + cadence / 15f + Mathf.Abs(lean) * 0.8f);
            still = Smooth(still, calm, 4f, dt);

            bool touching = Input.touchCount > 0 || Input.anyKey;
            bool active = cadence > 8f || Mathf.Abs(lean) > 0.25f || best.Knock || best.Jitter > 0.08f || touching;
            if (active) lastActive = Time.time;

            State = new MotionState
            {
                Cadence = cadence,
                Watts = MotionConfig.WattsPerRpm2 * cadence * cadence,
                Lean = lean,
                CrankAngle = crank,
                Stillness = still,
                Jitter = best.Jitter,
                Knock = best.Knock,
                Active = active,
                Source = src,
            };
        }
    }
}
