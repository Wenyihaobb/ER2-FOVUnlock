using System;
using System.Collections.Generic;
using UnityEngine;

namespace ER2FovUnlock
{
    /// <summary>
    /// FOV takes over the core (a purely managed static class, not injected into IL2CPP).
    /// Key mechanism: For each frame, first observe the field of view (FOV) that the game intends to include in the camera, and then decide whether to take over.
    /// When the game enters a low FOV state such as aiming scope / fixed artillery / ground vehicle targeting, the plugin completely hands over control,
    /// and enable the game to render using the native method (otherwise, the view inside the mirror will become a wide-angle view and you will be able to see the gun barrel and the hand)
    /// The plugin never writes the FOV settings in the game save file. Once disabled or deleted, they will naturally return to their original state.
    /// </summary>
    internal static class FovEngine
    {
        private static readonly List<Camera> _cameras = new List<Camera>();
        private static readonly Dictionary<int, float> _nativeFov = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> _lastWritten = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> _gameFov = new Dictionary<int, float>();
        private static readonly Dictionary<int, string> _cameraNames = new Dictionary<int, string>();
        private static string[] _excludedPatterns = new string[0];
        private static string _excludedSource;
        private static float _cachedSettingsFov;
        private static long _nextSettingsReadMs;
        private static int _mainCameraId = -1;
        private static float _lastApplied = -1f;
        private static float _smoothedRatio = 1f;
        private static float _lastSmoothTime = -1f;
        private static float _curveStart = 1f;
        private static float _curveTarget = 1f;
        private static float _curveProgress = 1f;
        private static long _nextRefreshMs;
        private static long _nextDebugMs;
        private static bool _cameraListLogged;
        private static bool _wasActive;
        private static int _errors;

        internal static string Status { get; private set; } = string.Empty;
        internal static int CameraCount => _cameras.Count;
        internal static bool HasApplied => _lastApplied > 0f;
        internal static float CurrentTarget { get; private set; } = -1f;
        internal static float BaseTarget { get; private set; } = -1f;
        internal static bool IsActive => _wasActive;

        // Exposed for the opt-in verbose diagnostics (FovDebug). Value types only,
        // so assigning them every frame costs nothing.
        internal static bool VehicleSightYield { get; private set; }
        internal static bool ScopeProtection { get; private set; }
        internal static int MainCameraId => _mainCameraId;
        internal static int YieldedCount { get; private set; }
        internal static int AppliedCount { get; private set; }
        internal static float ObservedNativeFov { get; private set; }
        internal static float OpticsThreshold { get; private set; }
        internal static float AppliedRatio { get; private set; }

        internal static void OnControllerAwake()
        {
            // It remains silent during normal operation; only warnings / errors are logged.
        }

        internal static void Tick(FovConfig cfg)
        {
            try
            {
                TickCore(cfg);
                _errors = 0;
            }
            catch (Exception e)
            {
                _errors++;
                if (_errors <= 3 || _errors % 300 == 0)
                    FovUnlockPlugin.Logger.LogError("[FOV] Per-frame error: " + e);
            }
        }

        private static void TickCore(FovConfig cfg)
        {
            GameState.Refresh(cfg);

            bool shouldWork = cfg.Enabled.Value && GameState.InMatch;
            if (_wasActive && !shouldWork && HasApplied)
                Restore(cfg);
            _wasActive = shouldWork;

            if (!shouldWork)
            {
                _smoothedRatio = 1f;
                _lastSmoothTime = -1f;
                ResetCurve(1f);
                CurrentTarget = -1f;
                BaseTarget = -1f;
                Status = !cfg.Enabled.Value ? "Disabled" : "Main menu / loading";
                return;
            }

            bool firstPerson = GameState.View == ViewMode.FirstPerson;
            float baseFov = firstPerson ? cfg.FovFirstPerson.Value : cfg.FovThirdPerson.Value;
            baseFov = Mathf.Clamp(baseFov, 65f, 130f);
            BaseTarget = baseFov;

            float ratio = 1f;
            if (GameState.HoldingBreath)
                ratio = cfg.HoldBreathZoomRatio.Value;
            else if (GameState.Aiming)
                ratio = cfg.AimZoomRatio.Value;
            ratio = Mathf.Lerp(1f, Mathf.Clamp(ratio, 0.2f, 1f),
                Mathf.Clamp(cfg.AimZoomStrength.Value, 0f, 1f));

            // Only the zoom ratio is transitioned. Dragging the FOV slider still takes effect immediately.
            // Breath-holding Animation Improvement only applies to the "infantry combat and switch activated" situation；
            // Ground vehicles, fixed weapons, air vehicles, and the Breath-holding Animation Improvement option when unchecked, all use the standard linear breath control.
            bool enhancedBreath = cfg.EnhancedBreathOnFoot.Value
                && GameState.IsOnFoot
                && GameState.HoldingBreath;
            float smoothSpeed = enhancedBreath
                ? Mathf.Clamp(cfg.HoldBreathSmoothSpeed.Value, 0.1f, 2f)
                : Mathf.Max(0f, cfg.ZoomSmoothSpeed.Value);
            float curve = Mathf.Clamp(cfg.HoldBreathCurve.Value, 0.5f, 10f);
            float appliedRatio = SmoothRatio(ratio, smoothSpeed, enhancedBreath, curve);
            float appliedRequest = baseFov * appliedRatio;
            bool horizontal = cfg.Reference.Value == FovReference.Horizontal;

            var cameras = GetCameras(cfg);
            if (cameras.Count == 0)
            {
                Status = "No usable camera";
                return;
            }

            // The game's saved FOV setting rarely changes, so read it once per second
            // instead of twice per frame (Tick runs in Update and LateUpdate).
            long nowMs = Environment.TickCount64;
            if (nowMs >= _nextSettingsReadMs)
            {
                _nextSettingsReadMs = nowMs + 1000;
                float read;
                _cachedSettingsFov = GameFov.TryReadSettingsFov(out read) ? read : 0f;
            }
            float baseSetting = _cachedSettingsFov;
            float threshold = baseSetting > 1f
                ? baseSetting * Mathf.Clamp(cfg.VanillaViewRatio.Value, 0.2f, 0.8f)
                : 30f;
            OpticsThreshold = threshold;
            AppliedRatio = appliedRatio;
            // Ground-vehicle gunner sights must use the game's native optic.
            // The aiming API often stays false inside a vehicle, so AimPressed also
            // covers the raw right mouse button. Without this, our base FOV / aim
            // ratio would flatten the scope zoom ("not magnified enough").
            bool vehicleSightYield = firstPerson
                && GameState.IsInVehicle
                && !GameState.IsAirVehicle
                && GameState.AimPressed;
            bool scopeActive = GameState.UsingScope;
            // Camera.main can be unavailable during a scene change. In that case we
            // cannot tell the main camera apart, so fall back to the FOV-threshold
            // optics yield rather than risk overwriting the scope camera.
            bool protectMain = scopeActive && _mainCameraId >= 0;
            VehicleSightYield = vehicleSightYield;
            ScopeProtection = scopeActive;

            // A weapon scope renders through its own camera, which is excluded below
            // so the scope picture keeps the vanilla FOV. The MAIN camera must stay
            // under plugin control: aim / breath ratios and the breath animation have
            // to keep working outside the scope. Suppress the FOV-threshold yield on
            // it while scoping, otherwise the game's low native FOV makes the plugin
            // intermittently hand the main camera back (the "scope grows / outside
            // FOV shrinks" glitch). Vehicle sights still yield globally.

            string excluded = cfg.ExcludedCameraNames.Value;
            int applied = 0, yielded = 0;
            float shown = 0f;
            float shownGameFov = 0f;

            for (int i = 0; i < cameras.Count; i++)
            {
                var cam = cameras[i];
                if (cam == null)
                    continue;

                float current;
                int id;
                try
                {
                    current = cam.fieldOfView;
                    id = cam.GetInstanceID();
                }
                catch
                {
                    continue;
                }

                if (current < 1f || current > 179f)
                    continue;

                if (!_nativeFov.ContainsKey(id))
                    _nativeFov[id] = current;
                if (_nativeFov.Count > 64)
                    ClearPerCameraState();

                string name = CameraName(cam);
                if (IsExcluded(name, excluded))
                {
                    _lastWritten[id] = -1f;
                    yielded++;
                    continue;
                }

                // While a weapon scope is active the scope picture is rendered by its
                // own camera. Leave every non-main camera completely untouched - do not
                // write and do not restore its FOV - so the vanilla magnification stays.
                // Only the main camera (outside the scope picture) stays under plugin
                // control, keeping the aim / breath sliders and breath animation alive.
                if (protectMain && id != _mainCameraId)
                {
                    _lastWritten[id] = -1f;
                    yielded++;
                    continue;
                }

                float lastWritten;
                _lastWritten.TryGetValue(id, out lastWritten);
                bool looksLikeOurs = lastWritten > 0f && Mathf.Abs(current - lastWritten) < 0.5f;
                if (!looksLikeOurs)
                    _gameFov[id] = current;

                float gameFov;
                if (!_gameFov.TryGetValue(id, out gameFov))
                    gameFov = current;
                shownGameFov = gameFov;

                bool fovYield = !protectMain && gameFov < threshold;
                if (vehicleSightYield || fovYield)
                {
                    // Dedicated optics / vehicle view: return the camera to the game and,
                    // if necessary, restore it to its original desired FOV.
                    if (Mathf.Abs(current - gameFov) > 0.05f)
                        cam.fieldOfView = gameFov;
                    _lastWritten[id] = -1f;
                    yielded++;
                    continue;
                }

                float target = horizontal ? HorizontalToVertical(appliedRequest, cam.aspect) : appliedRequest;
                target = Mathf.Clamp(target, 1f, 179f);
                if (Mathf.Abs(current - target) > 0.05f)
                    cam.fieldOfView = target;

                _lastWritten[id] = target;
                _lastApplied = target;
                CurrentTarget = target;
                shown = target;
                applied++;
            }

            AppliedCount = applied;
            YieldedCount = yielded;
            ObservedNativeFov = shownGameFov;

            // Building this interpolated string twice per frame is needless when no
            // diagnostics are enabled; FovDebug assembles its own detailed view.
            if (cfg.DebugWarnings.Value || cfg.VerboseDebug.Value)
            {
                string view = GameState.View == ViewMode.FirstPerson ? "FP" : "TP";
                string zoom = GameState.HoldingBreath ? "HoldBreath" : GameState.Aiming ? "Aim" : "Normal";
                Status = yielded > 0
                    ? $"Special optics: yielded {yielded} camera(s), native {shownGameFov:0.#} deg"
                      + (scopeActive ? " (weapon scope; main kept)"
                          : vehicleSightYield ? " (ground vehicle sight)" : "")
                    : applied > 0 ? $"{view} / {zoom} / {shown:0.#} deg" : "Waiting for cameras";
            }
            else
            {
                Status = yielded > 0 ? "Yielded" : applied > 0 ? "Active" : "Waiting for cameras";
            }

            if (cfg.DebugWarnings.Value && Environment.TickCount64 >= _nextDebugMs)
            {
                _nextDebugMs = Environment.TickCount64 + 4000;
                FovUnlockPlugin.Logger.LogWarning(
                    $"[FOV][Debug] {Status}; ratio={appliedRatio:0.00}->{ratio:0.00}; " +
                    $"observed native={shownGameFov:0.#} deg; threshold={threshold:0} deg; " +
                    $"overridden={applied} yielded={yielded}.");
            }
        }

        /// <summary>Adjust the corresponding FOV configuration according to the current person (by using hotkeys to call).</summary>
        internal static void AdjustFov(FovConfig cfg, int direction)
        {
            var entry = GameState.View == ViewMode.FirstPerson
                ? cfg.FovFirstPerson
                : cfg.FovThirdPerson;

            float step = Mathf.Clamp(Mathf.Round(cfg.FovHotkeyStep.Value), 1f, 10f);
            float next = Mathf.Clamp(Mathf.Round(entry.Value) + direction * step, 65f, 130f);
            if (Mathf.Abs(next - entry.Value) > 0.01f)
                entry.Value = next;
        }

        /// <summary>
        /// Zooming rate transition. Default is linear and uniform speed (MoveTowards). When entering the breath-holding position, switch to an exponential slow-in method:
        /// The normalized progress is still advancing at a constant rate, but it is mapped using (e^(k·p) - 1) / (e^k - 1).
        /// </summary>
        private static float SmoothRatio(float target, float speed, bool useCurve, float curve)
        {
            float now = Time.realtimeSinceStartup;
            float dt = _lastSmoothTime < 0f ? 0f : Mathf.Clamp(now - _lastSmoothTime, 0f, 0.1f);
            _lastSmoothTime = now;

            if (speed <= 0f || _smoothedRatio < 0f)
            {
                _smoothedRatio = target;
                ResetCurve(target);
                return target;
            }

            // The target change begins by resetting the transition from the current actual ratio.
            if (Mathf.Abs(target - _curveTarget) > 0.001f)
            {
                _curveStart = _smoothedRatio;
                _curveTarget = target;
                _curveProgress = 0f;
            }

            float span = Mathf.Abs(_curveTarget - _curveStart);
            bool entering = _curveTarget < _curveStart - 0.001f;
            if (!useCurve || !entering || span < 0.001f)
            {
                _curveProgress = 1f;
                _smoothedRatio = Mathf.MoveTowards(_smoothedRatio, target, speed * dt);
                return _smoothedRatio;
            }

            _curveProgress = Mathf.Min(1f, _curveProgress + speed * dt / span);
            float p = _curveProgress;
            float k = Mathf.Clamp(curve, 0.5f, 10f);
            float eased = (Mathf.Exp(k * p) - 1f) / (Mathf.Exp(k) - 1f);
            _smoothedRatio = Mathf.Lerp(_curveStart, _curveTarget, eased);
            if (p >= 1f)
                _smoothedRatio = target;
            return _smoothedRatio;
        }

        private static void ResetCurve(float value)
        {
            _curveStart = value;
            _curveTarget = value;
            _curveProgress = 1f;
        }

        internal static void SetEnabled(FovConfig cfg, bool value)
        {
            if (cfg.Enabled.Value == value)
                return;

            if (!value)
                Restore(cfg);
            else
                Status = "Enabled";

            cfg.Enabled.Value = value;
        }

        /// <summary>Restore the camera that has been modified by the plugin to the original perspective of the game.</summary>
        internal static void Restore(FovConfig cfg)
        {
            float gameSetting = 0f;
            GameFov.TryReadSettingsFov(out gameSetting);

            foreach (var cam in _cameras)
            {
                if (cam == null)
                    continue;
                try
                {
                    int id = cam.GetInstanceID();
                    float lastWritten;
                    // Only restore the cameras that are still being controlled by the plugins; the ones that have been given up will be handed over to the game itself.
                    if (!_lastWritten.TryGetValue(id, out lastWritten) || lastWritten <= 0f)
                        continue;

                    float current = cam.fieldOfView;
                    if (Mathf.Abs(current - lastWritten) > 12f)
                        continue;

                    float native;
                    if (!_nativeFov.TryGetValue(id, out native))
                        native = 0f;
                    float target = gameSetting > 0f ? gameSetting : native;
                    if (target <= 0f)
                        continue;

                    if (Mathf.Abs(current - target) > 0.05f)
                        cam.fieldOfView = target;
                }
                catch
                {
                    // The camera has been destroyed.
                }
            }

            ClearPerCameraState();
            _lastApplied = -1f;
            _smoothedRatio = 1f;
            _lastSmoothTime = -1f;
            ResetCurve(1f);
            CurrentTarget = -1f;
            BaseTarget = -1f;
            _wasActive = false;
            Status = "Disabled";
        }

        private static void ClearPerCameraState()
        {
            _nativeFov.Clear();
            _lastWritten.Clear();
            _gameFov.Clear();
            _cameraNames.Clear();
        }

        private static List<Camera> GetCameras(FovConfig cfg)
        {
            if (cfg.Target.Value == CameraTarget.MainCameraOnly)
            {
                _cameras.Clear();
                try
                {
                    var main = Camera.main;
                    if (main != null)
                    {
                        _cameras.Add(main);
                        int mainId = main.GetInstanceID();
                        _mainCameraId = mainId;
                        if (!_cameraNames.ContainsKey(mainId))
                            _cameraNames[mainId] = CameraName(main);
                    }
                }
                catch
                {
                    // During the scene transition, Camera.main may be temporarily unavailable.
                }
                return _cameras;
            }

            long now = Environment.TickCount64;
            if (now < _nextRefreshMs && _cameras.Count > 0)
                return _cameras;

            _nextRefreshMs = now + 1500;
            _cameras.Clear();
            _cameraNames.Clear();
            try
            {
                var mainCam = Camera.main;
                _mainCameraId = mainCam != null ? mainCam.GetInstanceID() : -1;
            }
            catch
            {
                _mainCameraId = -1;
            }

            // Only build the camera-name diagnostic string when diagnostics are on.
            bool wantCameraList = !_cameraListLogged &&
                (cfg.DebugWarnings.Value || cfg.VerboseDebug.Value);
            List<string> names = wantCameraList ? new List<string>() : null;

            var all = Camera.allCameras;
            for (int i = 0; i < all.Length; i++)
            {
                var cam = all[i];
                if (cam == null)
                    continue;

                string name = CameraName(cam);
                int id;
                try { id = cam.GetInstanceID(); }
                catch { continue; }
                _cameraNames[id] = name;

                if (names != null)
                    names.Add(name + " (" + cam.fieldOfView.ToString("0.#") + "°)");

                if (cfg.Target.Value == CameraTarget.ByName && !MatchesAny(name, cfg.CameraNames.Value))
                    continue;

                _cameras.Add(cam);
            }

            if (names != null)
            {
                _cameraListLogged = true;
                FovUnlockPlugin.Logger.LogWarning("[FOV][Debug] Scene cameras: " + string.Join(", ", names));
            }

            return _cameras;
        }

        private static string CameraName(Camera cam)
        {
            try
            {
                // cam.name crosses the IL2CPP boundary and allocates a string every call.
                // Cache it per camera instance; the cache is rebuilt on camera-list refresh.
                int id = cam.GetInstanceID();
                string cached;
                if (_cameraNames.TryGetValue(id, out cached))
                    return cached;
                var n = cam.name;
                string name = n == null ? string.Empty : n.ToString();
                _cameraNames[id] = name;
                return name;
            }
            catch
            {
                return "?";
            }
        }

        /// <summary>Names that contain any of the keywords will be excluded (case-insensitive).</summary>
        private static bool IsExcluded(string name, string patterns)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(patterns))
                return false;
            // Parse the comma-separated list only when the cfg value actually changes,
            // instead of allocating an array + trimmed strings for every camera each frame.
            if (!string.Equals(patterns, _excludedSource, StringComparison.Ordinal))
            {
                _excludedSource = patterns;
                _excludedPatterns = ParsePatterns(patterns);
            }
            for (int i = 0; i < _excludedPatterns.Length; i++)
                if (name.IndexOf(_excludedPatterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static string[] ParsePatterns(string patterns)
        {
            var raw = patterns.Split(',');
            var list = new List<string>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                string p = raw[i].Trim();
                if (p.Length != 0)
                    list.Add(p);
            }
            return list.ToArray();
        }

        /// <summary>Per-camera FOV breakdown for the opt-in verbose diagnostics.</summary>
        internal static string CameraDump()
        {
            var sb = new System.Text.StringBuilder(256);
            for (int i = 0; i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                if (cam == null)
                    continue;
                try
                {
                    int id = cam.GetInstanceID();
                    float game, written;
                    _gameFov.TryGetValue(id, out game);
                    _lastWritten.TryGetValue(id, out written);
                    string name;
                    if (!_cameraNames.TryGetValue(id, out name))
                        name = "?";
                    if (sb.Length > 0)
                        sb.Append(" | ");
                    sb.Append(name).Append("(id=").Append(id)
                      .Append(" fov=").Append(cam.fieldOfView.ToString("0.#"))
                      .Append(" game=").Append(game.ToString("0.#"))
                      .Append(" written=").Append(written.ToString("0.#"))
                      .Append(")");
                }
                catch
                {
                    // Camera destroyed mid-frame.
                }
            }
            return sb.ToString();
        }

        private static bool MatchesAny(string name, string patterns)
        {
            if (string.IsNullOrWhiteSpace(patterns))
                return false;
            foreach (var raw in patterns.Split(','))
            {
                var pattern = raw.Trim();
                if (pattern.Length != 0 && IsMatch(name, pattern))
                    return true;
            }
            return false;
        }

        private static bool IsMatch(string name, string pattern)
        {
            if (pattern == "*")
                return true;

            bool prefixWildcard = pattern.StartsWith("*", StringComparison.Ordinal);
            bool suffixWildcard = pattern.EndsWith("*", StringComparison.Ordinal);
            string core = pattern.Trim('*');
            if (core.Length == 0)
                return true;

            if (prefixWildcard && suffixWildcard)
                return name.IndexOf(core, StringComparison.OrdinalIgnoreCase) >= 0;
            if (suffixWildcard)
                return name.StartsWith(core, StringComparison.OrdinalIgnoreCase);
            if (prefixWildcard)
                return name.EndsWith(core, StringComparison.OrdinalIgnoreCase);
            return string.Equals(name, core, StringComparison.OrdinalIgnoreCase);
        }

        private static float HorizontalToVertical(float horizontalDeg, float aspect)
        {
            if (aspect <= 0.01f)
                return horizontalDeg;
            double h = Math.Min(horizontalDeg, 179.0) * Math.PI / 180.0;
            double v = 2.0 * Math.Atan(Math.Tan(h / 2.0) / aspect);
            return (float)(v * 180.0 / Math.PI);
        }
    }
}
