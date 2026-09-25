using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Client.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace Reconnect.Client.City
{
    /// <summary>
    /// Downloads swisstopo WMTS tiles (Web Mercator) and caches them in memory and on disk
    /// (Application.temporaryCachePath – the OS may clear it, which is fine).
    /// Data: © swisstopo, free to use under the OGD terms with attribution.
    /// </summary>
    public sealed class MapTileLoader
    {
        private const string UrlTemplate = "https://wmts.geo.admin.ch/1.0.0/{0}/default/current/3857/{1}/{2}/{3}.jpeg";

        private readonly Dictionary<string, Texture2D> _memory = new();
        private readonly string _diskCacheDir = Path.Combine(Application.temporaryCachePath, "tiles");

        /// <summary>Returns null if the tile could not be loaded (offline, outside coverage…).</summary>
        public async Task<Texture2D> LoadAsync(MapLayer layer, int zoom, int x, int y, CancellationToken ct)
        {
            var key = $"{layer.Id()}/{zoom}/{x}/{y}";
            if (_memory.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var bytes = await LoadBytesAsync(key, string.Format(UrlTemplate, layer.Id(), zoom, x, y), ct);
            if (bytes == null)
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGB24, mipChain: true)
            {
                name = key,
                wrapMode = TextureWrapMode.Clamp,   // avoids seams between neighbouring tiles
                anisoLevel = 4,
            };
            if (!texture.LoadImage(bytes, markNonReadable: true))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            _memory[key] = texture;
            return texture;
        }

        private async Task<byte[]> LoadBytesAsync(string key, string url, CancellationToken ct)
        {
            var file = Path.Combine(_diskCacheDir, key.Replace('/', '_') + ".jpeg");
            if (File.Exists(file))
            {
                return File.ReadAllBytes(file);
            }

            using var request = UnityWebRequest.Get(url);
            request.timeout = 20;
            using (ct.Register(request.Abort))
            {
                await request.SendWebRequest().AsTask();
            }
            ct.ThrowIfCancellationRequested();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Reconnect] Tile {key} failed: {request.error}");
                return null;
            }

            var bytes = request.downloadHandler.data;
            try
            {
                Directory.CreateDirectory(_diskCacheDir);
                File.WriteAllBytes(file, bytes);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[Reconnect] Could not cache tile {key}: {ex.Message}");
            }
            return bytes;
        }
    }

    public enum MapLayer
    {
        Aerial,
        Map,
    }

    public static class MapLayerExtensions
    {
        public static string Id(this MapLayer layer) => layer switch
        {
            MapLayer.Aerial => "ch.swisstopo.swissimage",
            MapLayer.Map => "ch.swisstopo.pixelkarte-farbe",
            _ => throw new ArgumentOutOfRangeException(nameof(layer)),
        };
    }
}
