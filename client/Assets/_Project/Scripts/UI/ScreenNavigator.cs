using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI
{
    /// <summary>
    /// Shows exactly one screen at a time, scales the UI to the device density and keeps it inside the safe area.
    /// </summary>
    public sealed class ScreenNavigator
    {
        private readonly VisualElement _host;
        private readonly PanelSettings _panel;
        private ScreenBase _current;
        private Rect _appliedSafeArea;
        private float _appliedScale;

        /// <param name="panel">Runtime copy of the panel settings; its scale is changed per device.</param>
        public ScreenNavigator(VisualElement root, StyleSheet theme, PanelSettings panel)
        {
            _panel = panel;
            root.styleSheets.Add(theme);
            root.AddToClassList("app-root");
            root.pickingMode = PickingMode.Ignore;   // containers never swallow touches meant for the 3D world

            _host = new VisualElement { name = "screen-host", pickingMode = PickingMode.Ignore };
            _host.AddToClassList("screen-host");
            root.Add(_host);
        }

        /// <summary>The screen to lay out for; tests replace it with simulated phones.</summary>
        public Func<DisplayMetrics> Display { get; set; } = DisplayMetrics.FromScreen;

        public ScreenBase Current => _current;

        public void Show(ScreenBase screen)
        {
            _current?.Destroy();
            _host.Clear();
            _current = screen;
            var root = screen.Create();
            _host.EnableInClassList("screen-host--opaque", !root.ClassListContains("screen--transparent"));
            _host.Add(root);
            HideScrollBarsOnTouchDevices();
        }

        /// <summary>Phones scroll by dragging; scroll bars only take space (desktop keeps them).</summary>
        private void HideScrollBarsOnTouchDevices()
        {
            if (Display().IsMobile)
            {
                _host.Query<ScrollView>().ForEach(scroll => scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden);
            }
        }

        /// <summary>Call every frame: device, notch / home indicator and density can change (rotation, simulator).</summary>
        public void UpdateLayout()
        {
            var display = Display();
            if (display.Width == 0 || display.Height == 0 || _host.panel == null)
            {
                return;
            }

            var scale = display.UiScale;
            if (!Mathf.Approximately(scale, _appliedScale))
            {
                _panel.scaleMode = PanelScaleMode.ConstantPixelSize;
                _panel.scale = scale;
            }
            var safe = display.SafeArea;
            if (safe == _appliedSafeArea && Mathf.Approximately(scale, _appliedScale))
            {
                return;
            }
            _appliedSafeArea = safe;
            _appliedScale = scale;

            // Screen pixels → UI units.
            _host.style.paddingLeft = safe.xMin / scale;
            _host.style.paddingRight = (display.Width - safe.xMax) / scale;
            _host.style.paddingTop = (display.Height - safe.yMax) / scale;
            _host.style.paddingBottom = safe.yMin / scale;
        }
    }
}
