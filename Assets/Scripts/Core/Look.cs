using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>Post-processing from the style guide: bloom for glow, ACES, vignette.</summary>
    public static class Look
    {
        public static Volume CreateVolume(Transform parent)
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.7f);

            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            var vg = p.Add<Vignette>(true);
            vg.intensity.Override(0.3f);
            vg.smoothness.Override(0.5f);

            var ca = p.Add<ColorAdjustments>(true);
            ca.saturation.Override(12f);
            ca.postExposure.Override(0.2f);

            v.profile = p;
            return v;
        }
    }

    /// <summary>
    /// Material kinds from the style guide (matte, glow, glass, hologram/additive).
    /// Base materials are generated into Resources by the editor setup script so
    /// their shader variants ship in the build.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> bases = new Dictionary<string, Material>();
        static readonly Dictionary<(string, Color, float), Material> cache = new Dictionary<(string, Color, float), Material>();

        static Material Base(string name, string fallbackShader)
        {
            if (bases.TryGetValue(name, out var m)) return m;
            m = Resources.Load<Material>("AxisPlay/" + name);
            if (m == null)
            {
                Debug.LogWarning($"[AxisPlay] Material {name} missing from Resources; run AxisPlay > Setup Project.");
                m = new Material(Shader.Find(fallbackShader) ?? Shader.Find("Sprites/Default"));
            }
            bases[name] = m;
            return m;
        }

        static Material Get(string kind, string fallback, Color c, float k, Action<Material> init)
        {
            if (cache.TryGetValue((kind, c, k), out var m)) return m;
            m = new Material(Base(kind, fallback)) { name = $"{kind}_{ColorUtility.ToHtmlStringRGB(c)}" };
            init(m);
            cache[(kind, c, k)] = m;
            return m;
        }

        /// <summary>Matte / satin lit surface.</summary>
        public static Material Lit(Color c, float smoothness = 0.15f) =>
            Get("Lit", "Universal Render Pipeline/Lit", c, smoothness, m =>
            {
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smoothness);
            });

        /// <summary>Lit surface with HDR emission (feeds bloom).</summary>
        public static Material Glow(Color c, float intensity = 2f) =>
            Get("LitEmissive", "Universal Render Pipeline/Lit", c, intensity, m =>
            {
                m.SetColor("_BaseColor", c * 0.25f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * intensity);
            });

        /// <summary>Alpha-blended unlit (glass panels, fog cards). Use alpha in c.</summary>
        public static Material Glass(Color c) =>
            Get("UnlitTransparent", "Universal Render Pipeline/Unlit", c, 0f, m => m.SetColor("_BaseColor", c));

        /// <summary>Additive unlit (holograms, light trails, beams).</summary>
        public static Material Additive(Color c, float intensity = 1f) =>
            Get("UnlitAdditive", "Universal Render Pipeline/Unlit", c, intensity, m => m.SetColor("_BaseColor", c * intensity));

        /// <summary>Additive particles (respects particle vertex colour).</summary>
        public static Material Particle(Color c) =>
            Get("ParticleAdditive", "Universal Render Pipeline/Particles/Unlit", c, 0f, m => m.SetColor("_BaseColor", c));

        /// <summary>Unique copy you can animate without affecting other objects.</summary>
        public static Material Unique(Material m) => new Material(m);
    }

    /// <summary>Small geometry helpers.</summary>
    public static class Geo
    {
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale,
            Material mat, bool keepCollider = false, Quaternion? rot = null)
        {
            var g = GameObject.CreatePrimitive(type);
            if (!keepCollider) UnityEngine.Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = rot ?? Quaternion.identity;
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = mat.renderQueue >= 3000 ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return g;
        }

        public static LineRenderer Line(Transform parent, Material mat, float width)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.widthMultiplier = width;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCornerVertices = 2;
            return lr;
        }

        public static Light PointLight(Transform parent, Vector3 localPos, Color c, float intensity, float range)
        {
            var go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>World-space text (for labels that live in the 3D scene).</summary>
        public static Text WorldText(Transform parent, string text, Vector3 localPos, Vector2 box, int fontSize,
            Color color, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var go = new GameObject("WorldText", typeof(RectTransform), typeof(Canvas));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localPosition = localPos;
            rt.sizeDelta = box;
            rt.localScale = Vector3.one * 0.01f;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            return Hud.Label(rt, text, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, box, align, color, style);
        }
    }
}
