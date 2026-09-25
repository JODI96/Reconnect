using UnityEngine;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI
{
    /// <summary>Shows exactly one screen at a time and keeps it inside the device safe area.</summary>
    public sealed class ScreenNavigator
    {
        private readonly VisualElement _host;
        private ScreenBase _current;
        private Rect _appliedSafeArea;

        public ScreenNavigator(VisualElement root, StyleSheet theme)
        {
            root.styleSheets.Add(theme);
            root.AddToClassList("app-root");

            _host = new VisualElement { name = "screen-host" };
            _host.AddToClassList("screen-host");
            root.Add(_host);
        }

        public void Show(ScreenBase screen)
        {
            _current?.Destroy();
            _host.Clear();
            _current = screen;
            _host.Add(screen.Create());
        }

        /// <summary>Call every frame: notch / home indicator can change with rotation.</summary>
        public void UpdateSafeArea()
        {
            var safe = Screen.safeArea;
            if (safe == _appliedSafeArea || Screen.width == 0 || Screen.height == 0 || _host.panel == null)
            {
                return;
            }
            _appliedSafeArea = safe;

            // Screen pixels → panel units (the panel may be scaled).
            var panelSize = _host.panel.visualTree.layout.size;
            var scaleX = panelSize.x / Screen.width;
            var scaleY = panelSize.y / Screen.height;

            _host.style.paddingLeft = safe.xMin * scaleX;
            _host.style.paddingRight = (Screen.width - safe.xMax) * scaleX;
            _host.style.paddingTop = (Screen.height - safe.yMax) * scaleY;
            _host.style.paddingBottom = safe.yMin * scaleY;
        }
    }
}
