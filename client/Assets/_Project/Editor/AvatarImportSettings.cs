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

        /// <summary>The character creator's parts (tools/avatars/build_wardrobe.py), loaded on demand from Resources.</summary>
        public const string Wardrobe = "Assets/ThirdParty/MakeHuman/Resources/Wardrobe/";

        /// <summary>Bump when the rules change, so Unity reimports the avatars.</summary>
        public override uint GetVersion() => 7;


        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Figures) && !assetPath.StartsWith(Animations))
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            if (assetPath.StartsWith(Wardrobe) && System.IO.Path.GetFileNameWithoutExtension(assetPath) != "body")
            {
                // A garment, hairstyle …: a skinned mesh on the body's skeleton, no avatar of its own (the body has it).
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importBlendShapes = false;
                importer.useFileScale = true;
                importer.optimizeGameObjects = false;
                importer.meshCompression = ModelImporterMeshCompression.Medium;
                importer.isReadable = true;   // the creator reads the body's vertex numbers and merges parts
                return;
            }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            if (assetPath.StartsWith(Wardrobe))
            {
                // The wardrobe body has the same mixamo rig as the ready-made figures (Unity's own mapping fails on it):
                // it takes the avatar of one of them – same bone names, T-pose rest.
                var source = assetPath.Contains("/female/") ? "lena" : "luca";
                var avatar = AssetDatabase.LoadAllAssetsAtPath($"{Figures}{source}/{source}.fbx").OfType<Avatar>().FirstOrDefault();
                if (avatar != null)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = avatar;
                }
            }
            // Always map from the current file: Unity keeps the first import's skeleton (rest pose) in the .meta
            // otherwise, and a rebuilt figure (tools/avatars) would be retargeted with the old pose.
            if (!assetPath.StartsWith(Wardrobe))
            {
                var description = importer.humanDescription;
                description.human = System.Array.Empty<HumanBone>();
                description.skeleton = System.Array.Empty<SkeletonBone>();
                importer.humanDescription = description;
                importer.autoGenerateAvatarMappingIfUnspecified = true;
            }
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
            if (assetPath.StartsWith(Wardrobe))
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.isReadable = true;
                importer.meshCompression = ModelImporterMeshCompression.Off;   // UV2 carries exact vertex numbers
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Animations))
            {
                return;
            }
            // Clips play in place: the room moves the avatar, the clip only animates the body. The root follows the
            // body's centre of mass and orientation (not the clip's original root), so a figure walks upright and
            // centred on its tile instead of drifting sideways or leaning; the feet keep it on the floor.
            var importer = (ModelImporter)assetImporter;
            importer.clipAnimations = importer.defaultClipAnimations
                .Select(clip =>
                {
                    clip.name = clip.name.Split('|').Last();
                    clip.loopTime = clip.name.EndsWith("_Loop");
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = false;
                    clip.keepOriginalPositionY = false;
                    clip.heightFromFeet = true;
                    clip.keepOriginalPositionXZ = false;
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
            if (assetPath.StartsWith(Wardrobe))
            {
                // Colour (JPEG), normal maps (*_n) and cut-out parts with alpha (hair, brows, beards, lashes: PNG).
                var texture = (TextureImporter)assetImporter;
                var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                var normal = name.EndsWith("_n");
                var alpha = assetPath.EndsWith(".png");
                texture.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                texture.sRGBTexture = !normal;
                texture.maxTextureSize = name.StartsWith("skin_") ? 1024 : normal ? 256 : 512;
                texture.mipmapEnabled = true;
                texture.alphaIsTransparency = alpha;
                texture.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                texture.isReadable = false;
                foreach (var platform in new[] { "Android", "iPhone" })
                {
                    var settings = texture.GetPlatformTextureSettings(platform);
                    settings.overridden = true;
                    settings.maxTextureSize = texture.maxTextureSize;
                    settings.format = TextureImporterFormat.ASTC_6x6;
                    texture.SetPlatformTextureSettings(settings);
                }
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
