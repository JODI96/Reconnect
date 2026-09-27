using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Reconnect.Client.Core
{
    /// <summary>
    /// "Grafik: Hoch / Normal" (remembered per device). High: full render resolution with 4× anti-aliasing (a "_High"
    /// copy of the quality level's pipeline asset, made by Setup Project), ambient occlusion (SSAO), reflections and more
    /// lamp lights in rooms, sharper city tiles. Normal: for older phones. Default: high unless it's a phone with little
    /// memory. The pipeline is switched, never edited – the assets stay as they are in the repository.
    /// </summary>
    public sealed class GraphicsQuality
    {
        private const string Key = "graphics.high";
        private readonly IReadOnlyList<ScriptableRendererData> _renderers;
        private readonly IReadOnlyList<RenderPipelineAsset> _highPipelines;

        /// <param name="highPipelines">"_High" copies of the quality levels' pipeline assets (matched by name).</param>
        public GraphicsQuality(IReadOnlyList<ScriptableRendererData> renderers, IReadOnlyList<RenderPipelineAsset> highPipelines = null)
        {
            _renderers = renderers ?? Array.Empty<ScriptableRendererData>();
            _highPipelines = highPipelines ?? Array.Empty<RenderPipelineAsset>();
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
            // Pipeline of the current quality level (Mobile / PC), or its "_High" copy.
            var basePipeline = QualitySettings.GetRenderPipelineAssetAt(QualitySettings.GetQualityLevel());
            RenderPipelineAsset high = null;
            foreach (var candidate in _highPipelines)
            {
                if (candidate != null && basePipeline != null && candidate.name == basePipeline.name.Replace("_RPAsset", "_High_RPAsset"))
                {
                    high = candidate;
                }
            }
            if (basePipeline != null)
            {
                QualitySettings.renderPipeline = High && high != null ? high : basePipeline;
            }

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
