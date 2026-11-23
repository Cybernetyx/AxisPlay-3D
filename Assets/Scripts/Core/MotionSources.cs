using UnityEngine;

namespace AxisPlay
{
    /// <summary>
    /// Reads the device gyro + accelerometer through Unity's built-in Input API.
    /// Assumes the panel/sensor is mounted on the crank (cadence) and frame (lean).
    /// If the bike sensor is not exposed as a standard Android sensor, replace this
    /// with a source fed by a Kotlin plugin (see README, "Week 0").
    /// </summary>
    public class GyroSource : IMotionSource
    {
        public string Name => "gyro";
        public bool Available { get; }

        Vector3 avgAcc;
        Vector3 lastAcc;
        float knockCooldown;

        public GyroSource()
        {
            Available = SystemInfo.supportsGyroscope;
            if (Available) Input.gyro.enabled = true;
            avgAcc = lastAcc = Input.acceleration;
        }

        public RawSample Sample(float dt)
        {
            Vector3 w = Input.gyro.rotationRateUnbiased; // rad/s
            float rpm = Mathf.Abs(w[MotionConfig.CrankAxis]) * 60f / (2f * Mathf.PI);
            if (rpm < 4f) rpm = 0f; // dead zone against drift

            Vector3 acc = Input.acceleration; // in g
            avgAcc = Vector3.Lerp(avgAcc, acc, 1f - Mathf.Exp(-3f * dt));
            float jitter = (acc - avgAcc).magnitude;

            knockCooldown -= dt;
            bool knock = false;
            if ((acc - lastAcc).magnitude > MotionConfig.KnockThreshold && knockCooldown <= 0f)
            {
                knock = true;
                knockCooldown = 0.35f;
            }
            lastAcc = acc;

            float lean = Mathf.Clamp(avgAcc[MotionConfig.LeanAxis] * MotionConfig.LeanScale, -1f, 1f);
            return new RawSample { Rpm = rpm, Lean = lean, Jitter = jitter, Knock = knock };
        }
    }

    /// <summary>
    /// Desk simulator so development works without the bike.
    /// Hold Space / W / Up = pedal (Shift = sprint), A / D = lean, K = knock, J = shake.
    /// Works on the panel too with a USB keyboard.
    /// </summary>
    public class SimulatorSource : IMotionSource
    {
        public string Name => "simulator";
        public bool Available => true;

        float rpm;

        public RawSample Sample(float dt)
        {
            bool pedal = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            float target = pedal ? (Input.GetKey(KeyCode.LeftShift) ? 115f : 85f) : 0f;
            rpm = Mathf.MoveTowards(rpm, target, (pedal ? 120f : 90f) * dt);

            float lean = 0f;
            if (Input.GetKey(KeyCode.A)) lean -= 1f;
            if (Input.GetKey(KeyCode.D)) lean += 1f;

            float jitter = Input.GetKey(KeyCode.J) ? 0.2f + Random.value * 0.1f : 0f;
            return new RawSample { Rpm = rpm, Lean = lean, Jitter = jitter, Knock = Input.GetKeyDown(KeyCode.K) };
        }
    }
}
