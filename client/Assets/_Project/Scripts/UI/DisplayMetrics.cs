using UnityEngine;

namespace Reconnect.Client.UI
{
    /// <summary>
    /// The screen the UI is laid out for: size in pixels, density and safe area. <see cref="FromScreen"/> reads the
    /// real device (the Device Simulator fakes these values too); tests pass simulated phones.
    /// </summary>
    public readonly struct DisplayMetrics
    {
        public DisplayMetrics(int width, int height, float dpi, Rect safeArea, bool isMobile)
        {
            Width = width;
            Height = height;
            Dpi = dpi;
            SafeArea = safeArea;
            IsMobile = isMobile;
        }

        public int Width { get; }
        public int Height { get; }
        public float Dpi { get; }

        /// <summary>Area without notch, Dynamic Island, camera hole and home indicator (pixels, origin bottom left).</summary>
        public Rect SafeArea { get; }

        public bool IsMobile { get; }

        /// <summary>
        /// UnityEngine.Device (not UnityEngine.Screen/Application): same values on a real device, but only these are
        /// faked by the Device Simulator – otherwise the editor counts as desktop and the phone UI is tiny.
        /// </summary>
        public static DisplayMetrics FromScreen() =>
            new(UnityEngine.Device.Screen.width, UnityEngine.Device.Screen.height, UnityEngine.Device.Screen.dpi,
                UnityEngine.Device.Screen.safeArea, UnityEngine.Device.Application.isMobilePlatform);

        /// <summary>
        /// Pixels per UI unit. On phones and tablets UI sizes are points (iOS) / dp (Android): 160 dpi = 1 pixel
        /// per unit, so text and touch targets have the same physical size on every device. Desktop stays 1:1.
        /// </summary>
        public float UiScale
        {
            get
            {
                var shortSide = Mathf.Min(Width, Height);
                if (!IsMobile || shortSide <= 0)
                {
                    return 1f;
                }
                var scale = Dpi > 0f ? Dpi / 160f : shortSide / 390f;
                // Guard against wrong density reports: a phone or tablet is 320–1024 units wide.
                return Mathf.Clamp(scale, shortSide / 1024f, shortSide / 320f);
            }
        }
    }
}
