# AxisPlay Motion Booth (Unity 3D)

Unity 6 (URP) Android app for the motion booth: a 3D launcher carousel and 16 motion-driven
experiences, built from the **Motion Booth 3D** design canvas.

Everything is created **in code** at startup: no hand-made scenes or prefabs. So the
project can be written without the Unity editor and built in the cloud.

## What's in this starter

| Part | File | Status |
|---|---|---|
| Motion engine: cadence, watts, lean, crank angle, stillness, knock | `Assets/Scripts/Core/MotionEngine.cs` | ✅ |
| Gyro/accelerometer source + desk simulator | `Assets/Scripts/Core/MotionSources.cs` | ✅ |
| App shell: camera, sun, post FX, idle reset, back button | `Assets/Scripts/Core/App.cs` | ✅ |
| Experience interface + catalogue of all 16 | `Assets/Scripts/Core/Experience.cs` | ✅ |
| HUD (glass panels, bars, buttons) + sensor debug overlay | `Assets/Scripts/Core/Hud.cs` | ✅ |
| Materials (matte, glow, glass, additive), post FX, local scores | `Assets/Scripts/Core/Look.cs` | ✅ |
| 3D launcher carousel | `Assets/Scripts/Launcher/Launcher.cs` | ✅ |
| **Climb the Summit** (hero) | `Assets/Scripts/Experiences/Summit/SummitClimb.cs` | ✅ playable |
| Other 15 experiences | `Placeholder.cs` (motion test scene) | ⏳ by phase |
| URP + materials + scene + Android settings, APK build | `Assets/Scripts/Editor/BuildScript.cs` | ✅ |
| Free cloud build (GameCI on GitHub Actions) | `.github/workflows/android.yml` | ✅ |

## Controls

| Action | Bike / panel | Keyboard (simulator) |
|---|---|---|
| Pedal | crank gyro | hold **Space / W / Up** (Shift = sprint) |
| Lean | frame tilt | **A / D** |
| Knock | accelerometer spike | **K** |
| Shake | — | **J** |
| Browse launcher | lean, swipe, tap a card | **← / →** |
| Start | knock, START, tap front card | **Enter** |
| Back to menu | MENU button, Android back | **Esc** |
| Sensor debug overlay | 3-finger tap | **F1** |

The app returns to the launcher after 45 s with no motion.

## Cloud build setup (one time, free)

1. **Create a GitHub repository** (private is fine) and push this folder:
   ```bash
   git init -b main && git lfs install && git add . && git commit -m "Starter" && git remote add origin <your-repo-url> && git push -u origin main
   ```
2. **Get a Unity Personal license file.** Install only Unity Hub (no editor needed, ~1 GB),
   sign in, then *Preferences → Licenses → Add → Get a free personal license*.
   The file is at `~/Library/Application Support/Unity/Unity_lic.ulf`.
3. **Add three repository secrets** (*Settings → Secrets and variables → Actions*):
   `UNITY_LICENSE` (full contents of the .ulf file), `UNITY_EMAIL`, `UNITY_PASSWORD`.
4. **Run the build:** *Actions → Build Android APK → Run workflow*. First run ≈ 25–35 min,
   later runs ≈ 10–15 min thanks to the Library cache.
5. **Install on the panel** (USB or same Wi-Fi with adb):
   ```bash
   adb install -r AxisPlay.apk
   ```
   Read logs while testing:
   ```bash
   adb logcat -s Unity
   ```

> If GameCI has no Docker image for the Unity version in `ProjectSettings/ProjectVersion.txt`,
> change it to the nearest Unity 6000.0 LTS version listed at game.ci/docs/docker/versions.

## Week 0: hardware check (do this with the first build)

Run a **development build**, open the debug overlay (3-finger tap) and check:

1. **Sensor:** does `gyro rate` change when the crank turns? Which axis? Set
   `MotionConfig.CrankAxis` / `LeanAxis` to match. If nothing changes, the bike sensor
   isn't a standard Android sensor → add a Kotlin plugin source (next step in the plan).
2. **Speed:** does Climb the Summit hold ~60 fps at 1080p?
3. **Second TV:** does `displays` show 2 with the TV connected?

## Adding an experience

1. Create `Assets/Scripts/Experiences/<Name>/<Name>.cs` with a class that extends
   `Experience`: build the world in `Begin()`, react to `MotionState` in `Tick()`, set
   `Finished` and `ResultText` when done.
2. In `Catalog.All` (Experience.cs), pass it as the last argument:
   `E("city", ..., 3, w => w.AddComponent<PowerTheCity>())`.

## Build order

1. **Foundation:** motion engine, simulator, launcher, idle reset ✅ · calibration screen
2. **Hero:** Climb the Summit ✅ · second-TV leaderboard · QR score cards
3. **Quick wins:** Stillness, Tempo DJ, Power the City, Ghost Rider, Knock, Visuals
4. **Extended:** Scenic Ride, Head-to-Head, Physics, Fingerprint, Globe, Time Machine, Heartbeat
5. **AI + polish:** Pedal-to-Develop Art (cloud backend), branding, rehearsal
6. **Show mode:** kiosk / lock-task, start on boot, offline score sync, staff PIN menu
