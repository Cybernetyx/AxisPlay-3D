using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AxisPlay
{
    /// <summary>Creates the app at startup, so the build needs only one empty scene.</summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (UnityEngine.Object.FindAnyObjectByType<App>() != null) return;
            var go = new GameObject("AxisPlay");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }
    }

    /// <summary>
    /// Owns the shared rig (camera, sun, post FX, HUD, motion engine) and switches
    /// between the launcher and experiences. Returns to the launcher after idle time.
    /// </summary>
    public class App : MonoBehaviour
    {
        public static App I { get; private set; }
        public MotionEngine Motion { get; private set; }
        public Hud Hud { get; private set; }
        public Camera Cam { get; private set; }
        public Light Sun { get; private set; }
        public AudioSource Audio { get; private set; }

        public bool InLauncher => current is Launcher;
        public Experience Current => current;

        Experience current;
        GameObject world;
        float finishedFor = -1f;
        int lastSelected;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Motion = gameObject.AddComponent<MotionEngine>();
            Audio = gameObject.AddComponent<AudioSource>();
            Audio.playOnAwake = false;
            Audio.spatialBlend = 0f;

            var camGo = new GameObject("Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            Cam.allowHDR = true;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 400f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            var cd = Cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = true;
            cd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;

            Look.CreateVolume(transform);
            Hud = Hud.Create(transform);
            gameObject.AddComponent<StaffMenu>();
            if (Display.displays.Length > 1) gameObject.AddComponent<SecondScreen>();
            ShowLauncher();
        }

        public void ShowLauncher() => Switch(w =>
        {
            var l = w.AddComponent<Launcher>();
            l.StartIndex = lastSelected;
            return l;
        });

        public void Launch(int index)
        {
            lastSelected = index;
            ScoreService.NextRider();
            ScoreService.LogSession(Catalog.All[index].Id);
            Switch(Catalog.All[index].Create);
        }

        void Switch(Func<GameObject, Experience> make)
        {
            if (current != null) current.End();
            if (world != null) Destroy(world);
            Hud.Clear();
            ResetEnvironment();

            world = new GameObject("World");
            world.transform.SetParent(transform, false);
            current = make(world);
            current.Init(this);
            current.Begin();
            if (!InLauncher)
                Hud.Button(Hud.Layer, "MENU", new Vector2(0, 1), new Vector2(40, -40), new Vector2(170, 64),
                    new Color(1, 1, 1, 0.12f), ShowLauncher, 26);
            finishedFor = -1f;
        }

        /// <summary>Neutral defaults each experience can override in Begin().</summary>
        public void ResetEnvironment()
        {
            Env(new Color(0.03f, 0.03f, 0.07f), new Color(0.25f, 0.25f, 0.35f), 20f, 120f);
            Sun.color = Color.white;
            Sun.intensity = 1f;
            Sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            Cam.fieldOfView = 55f;
            Cam.transform.SetPositionAndRotation(new Vector3(0, 2, -10), Quaternion.identity);
        }

        public void Env(Color sky, Color ambient, float fogStart, float fogEnd)
        {
            Cam.backgroundColor = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear; // keep Linear: it is the fog variant kept in the build
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        void Update()
        {
            var m = Motion.State;
            float dt = Time.deltaTime;
            var exp = current;
            if (StaffMenu.Open) return; // staff menu pauses the experience
            if (exp != null) exp.Tick(m, dt);
            if (exp != current) return; // the experience switched scenes itself

            if (InLauncher) return;

            if (Input.GetKeyDown(KeyCode.Escape)) { ShowLauncher(); return; } // Android back button
            if (Motion.SecondsSinceActive > current.IdleTimeout) { ShowLauncher(); return; }

            if (current.Finished)
            {
                if (finishedFor < 0f)
                {
                    finishedFor = 0f;
                    Hud.ShowResult(current.Title, current.ResultText, current.ResultDetail);
                    Audio.PlayOneShot(Kit.Blip);
                }
                finishedFor += dt;
                if (finishedFor > 12f || (finishedFor > 2f && m.Knock)) ShowLauncher();
            }
        }
    }
}
