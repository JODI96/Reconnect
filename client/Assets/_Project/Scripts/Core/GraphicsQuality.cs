using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Reconnect.Client.Core
{
    /// <summary>
    /// "Grafik: Hoch / Normal" (remembered per device). High: ambient occlusion (SSAO) and reflections in rooms, more
    /// lamp lights. Normal: for older phones. Default: high unless it's a phone with little memory.
    /// </summary>
    public sealed class GraphicsQuality
    {
        private const string Key = "graphics.high";
        private readonly IReadOnlyList<ScriptableRendererData> _renderers;

        public GraphicsQuality(IReadOnlyList<ScriptableRendererData> renderers)
        {
            _renderers = renderers ?? Array.Empty<ScriptableRendererData>();
            High = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetInt(Key) == 1 : DefaultHigh();
            Apply();
        }

        public bool High { get; private set; }

        /// <summary>The setting changed (rooms rebuild their reflections and lights on the next visit).</summary>
        public event Action Changed;

        public void Set(bool high)
        {
            if (high == High)
            {
                return;
            }
            High = high;
            PlayerPrefs.SetInt(Key, high ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        private static bool DefaultHigh() => !Application.isMobilePlatform || SystemInfo.systemMemorySize >= 5500;

        private void Apply()
        {
            foreach (var renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                foreach (var feature in renderer.rendererFeatures)
                {
                    if (feature != null && feature.GetType().Name.Contains("AmbientOcclusion"))
                    {
                        feature.SetActive(High);
                    }
                }
            }
        }
    }
}
