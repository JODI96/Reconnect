using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Reconnect.Client.Editor
{
    /// <summary>
    /// Import rules for the realistic avatars: MakeHuman figures (Assets/ThirdParty/MakeHuman, built by
    /// tools/avatars/build_avatars.py) and the Quaternius animation clips (Assets/ThirdParty/Quaternius).
    /// Both are Humanoid, so every clip plays on every figure (Unity retargets). Applied on (re)import.
    /// </summary>
    public sealed class AvatarImportSettings : AssetPostprocessor
    {
        public const string Figures = "Assets/ThirdParty/MakeHuman/";
        public const string Animations = "Assets/ThirdParty/Quaternius/";

        /// <summary>Bump when the rules change, so Unity reimports the avatars.</summary>
        public override uint GetVersion() => 3;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Figures) && !assetPath.StartsWith(Animations))
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            // Always map from the current file: Unity keeps the first import's skeleton (rest pose) in the .meta
            // otherwise, and a rebuilt figure (tools/avatars) would be retargeted with the old pose.
            var description = importer.humanDescription;
            description.human = System.Array.Empty<HumanBone>();
            description.skeleton = System.Array.Empty<SkeletonBone>();
            importer.humanDescription = description;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.useFileScale = true;
            // The FBX materials ("opaque", "cutout") only name the submeshes; AvatarSetup puts URP materials there.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.importAnimation = assetPath.StartsWith(Animations);
            importer.optimizeGameObjects = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Animations))
            {
                return;
            }
            // Clips play in place: the room moves the avatar, the clip only animates the body.
            var importer = (ModelImporter)assetImporter;
            importer.clipAnimations = importer.defaultClipAnimations
                .Select(clip =>
                {
                    clip.name = clip.name.Split('|').Last();
                    clip.loopTime = clip.name.EndsWith("_Loop");
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                    return clip;
                })
                .ToArray();
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Figures))
            {
                return;
            }
            // Atlases (tools/avatars): opaque.png 1024, cutout.png 512 with alpha. ASTC on phones.
            var importer = (TextureImporter)assetImporter;
            var cutout = System.IO.Path.GetFileNameWithoutExtension(assetPath) == "cutout";
            importer.maxTextureSize = cutout ? 512 : 1024;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = cutout;
            importer.alphaSource = cutout ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = importer.maxTextureSize;
                settings.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
