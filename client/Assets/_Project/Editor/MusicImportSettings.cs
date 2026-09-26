using UnityEditor;
using UnityEngine;

namespace Reconnect.Client.Editor
{
    /// <summary>
    /// Room music (Assets/ThirdParty/Music, tools/fetch_music.py): streamed from disk and Vorbis-compressed, so a
    /// three-minute track costs a few hundred KB of memory on a phone instead of tens of MB.
    /// </summary>
    public sealed class MusicImportSettings : AssetPostprocessor
    {
        private const string Music = "Assets/ThirdParty/Music/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Music))
            {
                return;
            }
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.45f;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 44100;
            importer.defaultSampleSettings = settings;
        }
    }
}
