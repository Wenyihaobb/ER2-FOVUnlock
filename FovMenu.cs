using System;
using BepInEx.Configuration;
using UnityEngine;

namespace ER2FovUnlock
{
    /// <summary>
    /// In-game IMGUI menu.
    /// All design dimensions are explicitly multiplied by the scaling factor. The drawing and the mouse are always in the same coordinate system. The title bar can be stably dragged.
    /// </summary>
    internal static class FovMenu
    {
        private static readonly Color Background = new Color(0.055f, 0.065f, 0.058f, 0.96f);
        private static readonly Color AccentOlive = new Color(0.486f, 0.545f, 0.306f, 1f);
        private static readonly Color Panel = new Color(0.10f, 0.115f, 0.10f, 1f);
        private static readonly Color Hover = new Color(0.19f, 0.22f, 0.18f, 1f);
        private static readonly Color TextPrimary = new Color(0.92f, 0.93f, 0.89f, 1f);
        private static readonly Color TextMuted = new Color(0.66f, 0.69f, 0.63f, 1f);

        internal enum CaptureKind { None, MenuKey, ToggleKey, FovIncrease, FovDecrease }

        private const float W = 420f;
        private const float H = 540f;
        private const float TitleH = 34f;

        private const float KeyLabelW = 76f;
        private const float KeyNameW = 112f;
        private const float KeyBtnW = 58f;
        private const float KeyGap = 6f;
        private const float KeyRowH = 24f;
        private const float DropdownItemH = 24f;
        private const int LanguageCount = 5;

        private static GUIStyle _title, _label, _muted, _value, _button, _toggle, _slider, _thumb, _blank, _center, _centerButton;
        private static bool _stylesReady;
        private static float _lastScale = -1f;
        private static bool _drawErrorLogged;
        private static readonly long StartTicks = Environment.TickCount64;

        private static float _winX = 60f;
        private static float _winY = 60f;
        private static bool _dragging;
        private static Vector2 _dragOffset;
        private static bool _langDropdownOpen;

        internal static bool IsOpen { get; private set; }
        internal static CaptureKind Capturing { get; private set; } = CaptureKind.None;

        internal static void Toggle()
        {
            IsOpen = !IsOpen;
            if (!IsOpen)
            {
                Capturing = CaptureKind.None;
                _langDropdownOpen = false;
            }
        }

        internal static void Close()
        {
            IsOpen = false;
            Capturing = CaptureKind.None;
            _langDropdownOpen = false;
        }

        internal static void Draw(FovConfig cfg)
        {
            L.Refresh(cfg);

            if (!IsOpen)
            {
                if (Environment.TickCount64 - StartTicks < 15000)
                {
                    try
                    {
                        float s = Scale();
                        EnsureStyles(s);
                        GUI.Label(new Rect(18f * s, 16f * s, 620f * s, 24f * s),
                            string.Format(L.StartupHint, KeyName(cfg.MenuKey.Value)), _muted);
                    }
                    catch
                    {
                        // 提示失败不影响功能
                    }
                }
                return;
            }

            try
            {
                DrawCore(cfg);
            }
            catch (Exception e)
            {
                if (!_drawErrorLogged)
                {
                    _drawErrorLogged = true;
                    FovUnlockPlugin.Logger.LogError("[FOV] Menu draw failed: " + e);
                }
                IsOpen = false;
                Capturing = CaptureKind.None;
                _langDropdownOpen = false;
            }
        }

        private static float Scale()
        {
            return Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.5f);
        }

        /// <summary>Design coordinates -> Screen coordinates.</summary>
        private static Rect R(float x, float y, float w, float h, float s)
        {
            return new Rect(_winX + x * s, _winY + y * s, w * s, h * s);
        }

        private static Rect WindowRect(float s)
        {
            return new Rect(_winX, _winY, W * s, H * s);
        }

        private static void DrawCore(FovConfig cfg)
        {
            float s = Scale();
            EnsureStyles(s);
            ClampToScreen(s);

            Color oldColor = GUI.color;

            // While the dropdown is open, consume its click before any other control.
            if (_langDropdownOpen)
                HandleDropdownClick(cfg, s);

            // Capture hotkeys: KeyDown must be eaten before any control is drawn.
            if (Capturing != CaptureKind.None)
                HandleCapture(cfg);

            try
            {
                var window = WindowRect(s);
                var titleBar = R(0f, 0f, W, TitleH, s);

                GUI.color = Background;
                GUI.DrawTexture(window, Texture2D.whiteTexture);
                GUI.color = AccentOlive;
                GUI.DrawTexture(titleBar, Texture2D.whiteTexture);
                GUI.color = Panel;
                GUI.DrawTexture(R(8f, TitleH + 8f, W - 16f, 1f, s), Texture2D.whiteTexture);
                GUI.color = oldColor;

                GUI.Label(R(0f, 4f, W, 26f, s), L.Title, _title);
                HandleDrag(titleBar, s);

                const float x = 14f;
                const float cw = W - 28f;
                float half = (cw - 8f) / 2f;

                bool enabled = GUI.Toggle(R(x, 46f, half, 22f, s), cfg.Enabled.Value,
                    new GUIContent("  " + L.Enable), _toggle);
                if (enabled != cfg.Enabled.Value)
                    FovEngine.SetEnabled(cfg, enabled);

                // Language dropdown, right-aligned on the same row as the master switch.
                float lx = x + half + 8f;
                if (GUI.Button(R(lx, 46f, half, 22f, s),
                    L.LangName(cfg.LanguageOption.Value), _centerButton))
                    _langDropdownOpen = !_langDropdownOpen;

                DrawFovSlider(cfg.FovFirstPerson, L.FovFirst, 65f, 130f, x, 80f, cw, s);
                DrawFovSlider(cfg.FovThirdPerson, L.FovThird, 65f, 130f, x, 132f, cw, s);
                DrawRatioSlider(cfg.AimZoomRatio, L.AimZoom, 0.70f, 1.00f, x, 188f, cw, s);
                DrawRatioSlider(cfg.HoldBreathZoomRatio, L.BreathZoom, 0.45f, 0.70f, x, 240f, cw, s);

                bool enhanced = GUI.Toggle(R(x, 294f, cw, 26f, s),
                    cfg.EnhancedBreathOnFoot.Value, new GUIContent("  " + L.EnhancedBreath), _toggle);
                if (enhanced != cfg.EnhancedBreathOnFoot.Value)
                    cfg.EnhancedBreathOnFoot.Value = enhanced;

                bool flightZoom = GUI.Toggle(R(x, 322f, cw, 24f, s),
                    cfg.FlightZoom.Value, new GUIContent("  " + L.FlightZoom), _toggle);
                if (flightZoom != cfg.FlightZoom.Value)
                    cfg.FlightZoom.Value = flightZoom;

                DrawKeyRow(cfg, x, 354f, s, L.KeyMenu, cfg.MenuKey.Value, CaptureKind.MenuKey, false);
                DrawKeyRow(cfg, x, 384f, s, L.KeyToggle, cfg.ToggleKey.Value, CaptureKind.ToggleKey, true);
                DrawKeyRow(cfg, x, 414f, s, L.KeyIncrease, cfg.FovIncreaseKey.Value, CaptureKind.FovIncrease, true);
                DrawKeyRow(cfg, x, 444f, s, L.KeyDecrease, cfg.FovDecreaseKey.Value, CaptureKind.FovDecrease, true);

                if (Capturing != CaptureKind.None)
                    GUI.Label(R(x, 472f, cw, 20f, s), L.PressKey, _value);

                if (GUI.Button(R(x, 498f, cw, 28f, s), L.Close, _button))
                    Close();

                // Draw the expanded list last so it overlays the controls underneath.
                if (_langDropdownOpen)
                    DrawDropdown(cfg, s);

                // Consume mouse events outside the window so they do not reach the game UI.
                if (Event.current != null && Event.current.isMouse &&
                    !window.Contains(Event.current.mousePosition) &&
                    Capturing == CaptureKind.None)
                {
                    GUI.Button(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, _blank);
                }
            }
            finally
            {
                GUI.color = oldColor;
            }
        }

        private static void DrawFovSlider(ConfigEntry<float> entry, string caption,
            float min, float max, float x, float y, float cw, float s)
        {
            float v = Mathf.Clamp(Mathf.Round(entry.Value), min, max);
            GUI.Label(R(x, y, cw, 20f, s), caption + ": " + v.ToString("0") + "°", _label);
            float nv = GUI.HorizontalSlider(R(x, y + 24f, cw, 16f, s), v, min, max, _slider, _thumb);
            float rounded = Mathf.Clamp(Mathf.Round(nv), min, max);
            if (Mathf.Abs(rounded - v) > 0.01f)
                entry.Value = rounded;
        }

        private static void DrawRatioSlider(ConfigEntry<float> entry, string caption,
            float min, float max, float x, float y, float cw, float s)
        {
            float v = Mathf.Clamp(Mathf.Round(entry.Value * 100f) / 100f, min, max);
            GUI.Label(R(x, y, cw, 20f, s), caption + ": " + v.ToString("0.00"), _label);
            float nv = GUI.HorizontalSlider(R(x, y + 24f, cw, 16f, s), v, min, max, _slider, _thumb);
            float rounded = Mathf.Clamp(Mathf.Round(nv * 100f) / 100f, min, max);
            if (Mathf.Abs(rounded - v) > 0.001f)
                entry.Value = rounded;
        }

        private static void DrawKeyRow(FovConfig cfg, float x, float y, float s,
            string label, KeyCode key, CaptureKind capture, bool allowClear)
        {
            GUI.Label(R(x, y, KeyLabelW, KeyRowH, s), label, _label);
            GUI.Label(R(x + KeyLabelW, y, KeyNameW, KeyRowH, s), KeyName(key), _value);

            float bx = x + KeyLabelW + KeyNameW;
            if (GUI.Button(R(bx, y, KeyBtnW, KeyRowH, s), L.Rebind, _button))
                Capturing = capture;
            if (allowClear && GUI.Button(R(bx + KeyBtnW + KeyGap, y, KeyBtnW, KeyRowH, s), L.Clear, _button))
                SetKey(cfg, capture, KeyCode.None);
        }

        private static Rect DropdownListRect(float s)
        {
            const float x = 14f;
            float half = (W - 28f - 8f) / 2f;
            float lx = x + half + 8f;
            return new Rect(_winX + lx * s, _winY + 72f * s,
                half * s, LanguageCount * DropdownItemH * s);
        }

        /// <summary>
        /// The list is drawn last (on top), so handle its clicks first, before any other control
        /// can consume the event. Crucially, EVERY mouse event is swallowed while the dropdown
        /// is open: MouseDown leaking through would let the FOV sliders underneath jump to the
        /// click position (the list overlaps both sliders).
        /// </summary>
        private static void HandleDropdownClick(FovConfig cfg, float s)
        {
            var e = Event.current;
            if (e == null || !e.isMouse)
                return;

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                var rect = DropdownListRect(s);
                if (rect.Contains(e.mousePosition))
                {
                    int idx = Mathf.Clamp(
                        (int)((e.mousePosition.y - rect.y) / (DropdownItemH * s)),
                        0, LanguageCount - 1);
                    cfg.LanguageOption.Value = (PluginLanguage)idx;
                    L.Refresh(cfg);
                }
                _langDropdownOpen = false;
            }

            e.Use();
        }

        private static void DrawDropdown(FovConfig cfg, float s)
        {
            var rect = DropdownListRect(s);
            Color old = GUI.color;

            var e = Event.current;
            Vector2 mouse = e == null
                ? new Vector2(-9999f, -9999f)
                : e.mousePosition;

            GUI.color = Panel;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            for (int i = 0; i < LanguageCount; i++)
            {
                var item = new Rect(rect.x, rect.y + i * DropdownItemH * s,
                    rect.width, DropdownItemH * s);
                bool current = (int)cfg.LanguageOption.Value == i;
                bool hover = item.Contains(mouse);

                if (current)
                    GUI.color = AccentOlive;
                else if (hover)
                    GUI.color = Hover;
                if (current || hover)
                    GUI.DrawTexture(item, Texture2D.whiteTexture);

                GUI.color = old;
                GUI.Label(item, "  " + L.LangName((PluginLanguage)i), _label);
            }
            GUI.color = old;
        }

        private static void HandleDrag(Rect titleBar, float s)
        {
            var e = Event.current;
            if (e == null)
                return;

            if (e.type == EventType.MouseDown && e.button == 0 &&
                titleBar.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = new Vector2(e.mousePosition.x - _winX, e.mousePosition.y - _winY);
                e.Use();
                return;
            }

            if (!_dragging)
                return;

            if (e.type == EventType.MouseDrag)
            {
                _winX = e.mousePosition.x - _dragOffset.x;
                _winY = e.mousePosition.y - _dragOffset.y;
                ClampToScreen(s);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _dragging = false;
                e.Use();
            }
        }

        private static void ClampToScreen(float s)
        {
            float maxX = Mathf.Max(0f, Screen.width - W * s);
            float maxY = Mathf.Max(0f, Screen.height - H * s);
            _winX = Mathf.Clamp(_winX, 0f, maxX);
            _winY = Mathf.Clamp(_winY, 0f, maxY);
        }

        private static void HandleCapture(FovConfig cfg)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;

            var key = e.keyCode;
            if (key == KeyCode.Escape)
            {
                Capturing = CaptureKind.None;
                e.Use();
                return;
            }

            if (key == KeyCode.None)
                return;

            var kind = Capturing;
            SetKey(cfg, kind, key);
            Capturing = CaptureKind.None;
            e.Use();
        }

        private static void SetKey(FovConfig cfg, CaptureKind kind, KeyCode key)
        {
            switch (kind)
            {
                case CaptureKind.MenuKey: cfg.MenuKey.Value = key; break;
                case CaptureKind.ToggleKey: cfg.ToggleKey.Value = key; break;
                case CaptureKind.FovIncrease: cfg.FovIncreaseKey.Value = key; break;
                case CaptureKind.FovDecrease: cfg.FovDecreaseKey.Value = key; break;
            }
        }

        internal static string KeyName(KeyCode key)
        {
            return key == KeyCode.None ? L.Unbound : key.ToString();
        }

        private static void EnsureStyles(float s)
        {
            if (!_stylesReady)
            {
                _stylesReady = true;
                try
                {
                    var skin = GUI.skin;
                    _title = new GUIStyle(skin == null ? new GUIStyle() : skin.label);
                    _label = new GUIStyle(skin == null ? new GUIStyle() : skin.label);
                    _muted = new GUIStyle(skin == null ? new GUIStyle() : skin.label);
                    _value = new GUIStyle(skin == null ? new GUIStyle() : skin.label);
                    _button = new GUIStyle(skin == null ? new GUIStyle() : skin.button);
                    _toggle = new GUIStyle(skin == null ? new GUIStyle() : skin.toggle);
                    _slider = new GUIStyle(skin == null ? new GUIStyle() : skin.horizontalSlider);
                    _thumb = new GUIStyle(skin == null ? new GUIStyle() : skin.horizontalSliderThumb);
                    _blank = new GUIStyle();
                    _center = new GUIStyle(_label);
                    _center.alignment = TextAnchor.MiddleCenter;
                    _centerButton = new GUIStyle(_button);
                    _centerButton.alignment = TextAnchor.MiddleCenter;

                    _title.fontStyle = FontStyle.Bold;
                    _title.alignment = TextAnchor.MiddleCenter;
                    _title.normal.textColor = TextPrimary;
                    _label.normal.textColor = TextPrimary;
                    _muted.normal.textColor = TextMuted;
                    _value.fontStyle = FontStyle.Bold;
                    _value.normal.textColor = TextPrimary;
                    _button.normal.textColor = TextPrimary;
                    _toggle.normal.textColor = TextPrimary;
                }
                catch (Exception e)
                {
                    FovUnlockPlugin.Logger.LogWarning("[FOV] Menu style fallback: " + e.Message);
                    _title = _label = _muted = _value = _button = _toggle = _slider = _thumb = _blank = _center = _centerButton = new GUIStyle();
                }
            }

            if (Mathf.Abs(_lastScale - s) < 0.001f)
                return;
            _lastScale = s;

            _title.fontSize = Mathf.RoundToInt(14f * s);
            _label.fontSize = Mathf.RoundToInt(12f * s);
            _muted.fontSize = Mathf.RoundToInt(11f * s);
            _value.fontSize = Mathf.RoundToInt(13f * s);
            _button.fontSize = Mathf.RoundToInt(12f * s);
            _toggle.fontSize = Mathf.RoundToInt(12f * s);
            _center.fontSize = Mathf.RoundToInt(11f * s);
            _centerButton.fontSize = Mathf.RoundToInt(12f * s);
        }
    }
}
