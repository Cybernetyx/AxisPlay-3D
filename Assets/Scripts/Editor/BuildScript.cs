using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AxisPlay.EditorTools
{
    /// <summary>
    /// Makes the project buildable with no hand-made assets: creates the URP pipeline
    /// asset, base materials (in Resources so their shader variants ship), the single
    /// boot scene and Android player settings — then builds the APK.
    /// Cloud build calls BuildAndroid via -executeMethod; in the editor use the AxisPlay menu.
    /// </summary>
    public static class BuildScript
    {
        const string GenDir = "Assets/Generated";
        const string ResDir = "Assets/Resources/AxisPlay";
        const string ScenePath = "Assets/Scenes/Boot.unity";
        const string AppId = "com.axisplay.motionbooth";

        [InitializeOnLoadMethod]
        static void AutoSetup()
        {
            if (Application.isBatchMode) return; // BuildAndroid runs Setup itself
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(GenDir + "/URP.asset") || !File.Exists(ScenePath)) Setup();
            };
        }

        [MenuItem("AxisPlay/Setup Project")]
        public static void Setup()
        {
            Directory.CreateDirectory(GenDir);
            Directory.CreateDirectory(ResDir);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();

            SetupPipeline();
            SetupMaterials();
            SetupScene();
            SetupPlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[AxisPlay] Project setup complete.");
        }

        static void SetupPipeline()
        {
            string assetPath = GenDir + "/URP.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, GenDir + "/URP_Renderer.asset");
                if (renderer.postProcessData == null)
                    renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                        "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                EditorUtility.SetDirty(renderer);

                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, assetPath);
            }

            urp.supportsHDR = true;          // needed for bloom glow
            urp.msaaSampleCount = 1;         // FXAA on the camera instead (cheaper on mobile)
            urp.renderScale = 1f;
            urp.shadowDistance = 80f;
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void SetupMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            Make("Lit", lit, m => { });
            Make("LitEmissive", lit, m =>
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            Make("UnlitTransparent", unlit, m => Transparent(m, additive: false));
            Make("UnlitAdditive", unlit, m => Transparent(m, additive: true));
            Make("ParticleAdditive", particles, m => Transparent(m, additive: true));
        }

        static void Make(string name, Shader shader, Action<Material> init)
        {
            string path = $"{ResDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            init(m);
            EditorUtility.SetDirty(m);
        }

        static void Transparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f); // double-sided
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static void SetupScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // Linear fog on in the boot scene keeps the fog shader variant in the build.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void SetupPlayer()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "AxisPlay";
            PlayerSettings.productName = "AxisPlay Motion Booth";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(android, AppId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;

            var run = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            if (int.TryParse(run, out int code)) PlayerSettings.Android.bundleVersionCode = code;
        }

        [MenuItem("AxisPlay/Build Android APK")]
        public static void BuildAndroid()
        {
            Setup();
            EditorUserBuildSettings.buildAppBundle = false;

            string output = Arg("-customBuildPath") ?? "build/Android/AxisPlay.apk";
            if (!output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) output = Path.Combine(output, "AxisPlay.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "build");

            var options = BuildOptions.None;
            if (HasArg("-devBuild")) options |= BuildOptions.Development | BuildOptions.ConnectWithProfiler;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options,
            });

            var s = report.summary;
            Debug.Log($"[AxisPlay] Build {s.result}: {output} ({s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime})");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
