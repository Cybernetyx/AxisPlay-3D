using UnityEngine;
using UnityEngine.UI;

namespace AxisPlay
{
    /// <summary>
    /// Pedal to spin a low-poly Earth (tilted, with atmosphere and orbit ring).
    /// Stop pedalling: it slows and lands on the city facing you, which lights a beacon.
    /// </summary>
    public class SpinGlobe : Experience
    {
        public override string Title => "Spin the Globe";

        struct City { public string Name, Country; public float Lat, Lon; public City(string n, string c, float la, float lo) { Name = n; Country = c; Lat = la; Lon = lo; } }

        static readonly City[] Cities =
        {
            new City("Tokyo", "Japan", 35.7f, 139.7f), new City("Sydney", "Australia", -33.9f, 151.2f),
            new City("Singapore", "Singapore", 1.35f, 103.8f), new City("Mumbai", "India", 19.1f, 72.9f),
            new City("Dubai", "UAE", 25.2f, 55.3f), new City("Cairo", "Egypt", 30.0f, 31.2f),
            new City("Nairobi", "Kenya", -1.3f, 36.8f), new City("Cape Town", "South Africa", -33.9f, 18.4f),
            new City("London", "UK", 51.5f, -0.1f), new City("Paris", "France", 48.9f, 2.35f),
            new City("Moscow", "Russia", 55.8f, 37.6f), new City("New York", "USA", 40.7f, -74.0f),
            new City("San Francisco", "USA", 37.8f, -122.4f), new City("Mexico City", "Mexico", 19.4f, -99.1f),
            new City("Rio de Janeiro", "Brazil", -22.9f, -43.2f), new City("Buenos Aires", "Argentina", -34.6f, -58.4f),
            new City("Beijing", "China", 39.9f, 116.4f), new City("Reykjavik", "Iceland", 64.1f, -21.9f),
        };

        // Rough continents as (lat, lon, radius°) blobs; noise breaks up the edges.
        static readonly Vector3[] Land =
        {
            new Vector3(48, -100, 22), new Vector3(60, -120, 16), new Vector3(30, -95, 12), new Vector3(18, -100, 8), new Vector3(65, -150, 9),
            new Vector3(72, -40, 9), new Vector3(-12, -58, 16), new Vector3(-32, -64, 10), new Vector3(2, -68, 10),
            new Vector3(50, 12, 11), new Vector3(60, 30, 13), new Vector3(54, -3, 3.5f), new Vector3(64, 15, 6),
            new Vector3(8, 20, 20), new Vector3(-15, 27, 15), new Vector3(25, 15, 12), new Vector3(-20, 47, 4),
            new Vector3(55, 90, 26), new Vector3(40, 100, 18), new Vector3(22, 78, 9), new Vector3(25, 45, 9),
            new Vector3(15, 103, 9), new Vector3(-2, 115, 7), new Vector3(36, 138, 4), new Vector3(65, 140, 14),
            new Vector3(-25, 134, 14), new Vector3(-42, 172, 3.5f), new Vector3(-85, 0, 16), new Vector3(-80, 120, 14), new Vector3(-80, -120, 14),
        };

        const float Radius = 3f;
        Transform axis, globe, beacon, pinsRoot;
        float spin, angle, maxSpin, landT = -1f, timer;
        int landed = -1;
        Quaternion landFrom, landTo;
        Text speedText, statusText, cityText;
        Light beaconLight;
        Material beaconMat;

        static Vector3 Dir(float lat, float lon)
        {
            float la = lat * Mathf.Deg2Rad, lo = lon * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(la) * Mathf.Cos(lo), Mathf.Sin(la), Mathf.Cos(la) * Mathf.Sin(lo));
        }

        static bool IsLand(Vector3 d)
        {
            float n = Mathf.PerlinNoise(d.x * 3f + 5f, d.y * 3f + d.z * 2f + 9f) * 9f - 4.5f;
            foreach (var b in Land)
            {
                float ang = Vector3.Angle(d, Dir(b.x, b.y));
                if (ang < b.z + n) return true;
            }
            return false;
        }

        public override void Begin()
        {
            App.Env(new Color(0.01f, 0.015f, 0.05f), new Color(0.25f, 0.28f, 0.4f), 30f, 80f);
            App.Sun.color = new Color(1f, 0.96f, 0.9f);
            App.Sun.intensity = 1.6f;
            App.Sun.transform.rotation = Quaternion.Euler(15f, -60f, 0f);
            Cam.fieldOfView = 40f;
            Cam.transform.SetPositionAndRotation(new Vector3(0, 0.6f, -10.5f), Quaternion.Euler(3f, 0, 0));

            // starfield
            var stars = new GameObject("Stars").transform;
            stars.SetParent(transform, false);
            var rnd = new System.Random(4);
            var starMat = Mats.Additive(Color.white, 1.2f);
            for (int i = 0; i < 220; i++)
            {
                var d = new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 0.6f + 0.4f).normalized;
                Geo.Prim(PrimitiveType.Quad, stars, d * 60f, Vector3.one * (0.08f + (float)rnd.NextDouble() * 0.15f), starMat,
                    rot: Quaternion.LookRotation(d));
            }

            axis = new GameObject("Axis").transform;
            axis.SetParent(transform, false);
            axis.localRotation = Quaternion.Euler(0, 0, 23.4f);
            globe = new GameObject("Globe").transform;
            globe.SetParent(axis, false);

            Kit.MeshObject("Earth", globe, BuildEarth(),
                Mats.Lit(new Color(0.08f, 0.32f, 0.62f), 0.75f),  // ocean
                Mats.Lit(new Color(0.25f, 0.62f, 0.32f), 0.1f),   // land
                Mats.Lit(new Color(0.92f, 0.95f, 1f), 0.4f));     // ice
            Geo.Prim(PrimitiveType.Sphere, axis, Vector3.zero, Vector3.one * Radius * 2.18f, Mats.Additive(new Color(0.25f, 0.55f, 1f), 0.16f));
            Geo.Prim(PrimitiveType.Sphere, axis, Vector3.zero, Vector3.one * Radius * 2.06f, Mats.Additive(new Color(0.4f, 0.7f, 1f), 0.1f));
            Geo.Prim(PrimitiveType.Cylinder, axis, Vector3.zero, new Vector3(0.03f, Radius * 1.25f, 0.03f), Mats.Additive(new Color(0.5f, 0.8f, 1f), 0.6f));

            var ring = new GameObject("OrbitRing").transform;
            ring.SetParent(transform, false);
            ring.localRotation = Quaternion.Euler(12f, 0, 8f);
            Kit.Ring(ring, Radius * 1.55f, Mats.Additive(new Color(0.3f, 0.85f, 1f), 0.9f), 0.03f, 128);
            Kit.Ring(ring, Radius * 1.62f, Mats.Additive(new Color(0.3f, 0.85f, 1f), 0.35f), 0.015f, 128);

            pinsRoot = new GameObject("Pins").transform;
            pinsRoot.SetParent(globe, false);
            var pinMat = Mats.Glow(new Color(1f, 0.4f, 0.5f), 2.5f);
            var stemMat = Mats.Lit(new Color(0.9f, 0.9f, 0.95f));
            for (int i = 0; i < Cities.Length; i++)
            {
                var d = Dir(Cities[i].Lat, Cities[i].Lon);
                var pin = new GameObject(Cities[i].Name).transform;
                pin.SetParent(pinsRoot, false);
                pin.localPosition = d * Radius;
                pin.localRotation = Quaternion.FromToRotation(Vector3.up, d);
                Geo.Prim(PrimitiveType.Cylinder, pin, new Vector3(0, 0.12f, 0), new Vector3(0.025f, 0.12f, 0.025f), stemMat);
                Geo.Prim(PrimitiveType.Sphere, pin, new Vector3(0, 0.27f, 0), Vector3.one * 0.09f, pinMat);
            }

            beacon = new GameObject("Beacon").transform;
            beacon.SetParent(globe, false);
            beaconMat = Mats.Unique(Mats.Additive(new Color(1f, 0.8f, 0.3f), 1.5f));
            Geo.Prim(PrimitiveType.Cylinder, beacon, new Vector3(0, 1.2f, 0), new Vector3(0.09f, 1.2f, 0.09f), beaconMat);
            Kit.Ring(beacon, 0.25f, beaconMat, 0.03f, 32);
            beaconLight = Geo.PointLight(beacon, new Vector3(0, 0.5f, 0), new Color(1f, 0.8f, 0.3f), 4f, 4f);
            beacon.gameObject.SetActive(false);

            angle = Random.Range(0f, 360f);

            TitleBlock(Title, "Pedal to spin the Earth. Stop pedalling to land on a city.");
            speedText = StatCard(new Vector2(0, 0.5f), new Vector2(40, 60), "Spin speed", "rpm", 280, new Color(0.3f, 0.85f, 1f));
            statusText = Hud.Label(Hud.Layer, "", 26, new Vector2(0, 0.5f), new Vector2(44, -40), new Vector2(420, 120),
                TextAnchor.UpperLeft, new Color(1, 1, 1, 0.7f));
            cityText = Hud.Label(Hud.Layer, "", 64, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(1400, 160),
                TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Hint("PEDAL TO SPIN");
        }

        Mesh BuildEarth()
        {
            var fb = new Kit.FlatMeshBuilder(3);
            const int lat = 28, lon = 56;
            Vector3 P(int i, int j)
            {
                float la = Mathf.Lerp(-90f, 90f, i / (float)lat);
                float lo = j / (float)lon * 360f;
                return Dir(la, lo) * Radius;
            }
            void T(Vector3 a, Vector3 b, Vector3 c)
            {
                var d = ((a + b + c) / 3f).normalized;
                int sub = Mathf.Abs(d.y) > 0.92f ? 2 : IsLand(d) ? 1 : 0;
                if (sub == 1) { float bump = 1.02f; a *= bump; b *= bump; c *= bump; }
                fb.Tri(sub, a, b, c);
            }
            for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                Vector3 a = P(i, j), b = P(i + 1, j), c = P(i, j + 1), d = P(i + 1, j + 1);
                if (i > 0) T(a, b, c);      // outward winding
                if (i < lat - 1) T(b, d, c);
            }
            return fb.Build("Earth");
        }

        public override void Tick(in MotionState m, float dt)
        {
            timer += dt;
            if (landed < 0)
            {
                float target = m.Cadence * 2.2f; // deg/s per rpm
                spin = target > spin ? Kit.Damp(spin, target, 1.5f, dt) : Mathf.MoveTowards(spin, 0f, 25f * dt);
                maxSpin = Mathf.Max(maxSpin, spin);
                angle += spin * dt;
                globe.localRotation = Quaternion.Euler(0, angle, 0);

                if (maxSpin > 90f) Hint(m.Cadence > 5f ? "STOP PEDALLING TO LAND" : null);
                statusText.text = maxSpin > 90f ? "Slowing down…" : "Get it spinning past 40 rpm";
                if (maxSpin > 90f && spin < 6f && m.Cadence < 5f) Land();
            }
            else
            {
                landT += dt;
                globe.localRotation = Quaternion.Slerp(landFrom, landTo, Mathf.SmoothStep(0, 1, Mathf.Clamp01(landT / 1.6f)));
                float pulse = 1f + Mathf.Sin(landT * 6f) * 0.25f;
                beacon.localScale = Vector3.one * Mathf.Clamp01(landT / 0.6f);
                beaconLight.intensity = 4f * pulse;
                beaconMat.SetColor("_BaseColor", new Color(1f, 0.8f, 0.3f) * 1.5f * pulse);
                Cam.fieldOfView = Kit.Damp(Cam.fieldOfView, 30f, 1.5f, dt);
                if (landT > 4f) Finish(Cities[landed].Name, Cities[landed].Country);
            }
            speedText.text = (spin / 6f).ToString("0");
            axis.localRotation = Quaternion.Euler(0, 0, 23.4f);
        }

        void Land()
        {
            // City nearest to the camera-facing direction.
            Vector3 toCam = (Cam.transform.position - axis.position).normalized;
            float best = -2f;
            for (int i = 0; i < Cities.Length; i++)
            {
                Vector3 w = globe.TransformDirection(Dir(Cities[i].Lat, Cities[i].Lon));
                float dot = Vector3.Dot(w, toCam);
                if (dot > best) { best = dot; landed = i; }
            }
            var c = Cities[landed];
            Vector3 local = Dir(c.Lat, c.Lon);
            beacon.localPosition = local * Radius;
            beacon.localRotation = Quaternion.FromToRotation(Vector3.up, local);
            beacon.gameObject.SetActive(true);

            // Rotate about the spin axis so the city faces the camera.
            Vector3 camLocal = axis.InverseTransformDirection(toCam);
            Vector3 cityNow = globe.localRotation * local;
            float a1 = Mathf.Atan2(cityNow.z, cityNow.x) * Mathf.Rad2Deg;
            float a2 = Mathf.Atan2(camLocal.z, camLocal.x) * Mathf.Rad2Deg;
            landFrom = globe.localRotation;
            landTo = Quaternion.Euler(0, angle + Mathf.DeltaAngle(a2, a1), 0);
            landT = 0f;
            spin = 0f;
            Hint(null);
            statusText.text = "Landed!";
            cityText.text = $"{c.Name.ToUpperInvariant()}\n<size=32>{c.Country}</size>";
            App.Audio.PlayOneShot(Kit.Blip);
        }
    }
}
