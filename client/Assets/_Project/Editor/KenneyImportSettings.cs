using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Reconnect.Client.Editor
{
    /// <summary>
    /// Import rules for the Kenney CC0 packs in Assets/ThirdParty/Kenney:
    /// characters get a generic rig with their animations (idle/walk/sit loop),
    /// furniture is imported as static meshes. Applied automatically on (re)import.
    /// </summary>
    public sealed class KenneyImportSettings : AssetPostprocessor
    {
        private const string Characters = "Assets/ThirdParty/Kenney/Characters/";
        private const string Furniture = "Assets/ThirdParty/Kenney/Furniture/";
        private static readonly string[] LoopingClips = { "idle", "walk", "sprint", "sit", "static" };

        private void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            if (assetPath.StartsWith(Characters))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.importCameras = false;
                importer.importLights = false;
            }
            else if (assetPath.StartsWith(Furniture))
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.importCameras = false;
                importer.importLights = false;
                importer.addCollider = false;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Characters))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.clipAnimations = importer.defaultClipAnimations
                .Select(clip =>
                {
                    clip.loopTime = LoopingClips.Contains(clip.name);
                    return clip;
                })
                .ToArray();
        }

        /// <summary>Diagnostics: logs the size of some models (batch: -executeMethod ...LogModelSizes).</summary>
        [MenuItem("Reconnect/Diagnostics/Log Kenney Model Sizes")]
        public static void LogModelSizes()
        {
            foreach (var path in new[]
                     {
                         Furniture + "loungeSofa.fbx", Furniture + "tableRound.fbx", Furniture + "chair.fbx",
                         Furniture + "wall.fbx", Furniture + "wallWindow.fbx", Furniture + "floorFull.fbx",
                         Furniture + "pottedPlant.fbx", Furniture + "bookcaseClosedWide.fbx",
                         Characters + "character-female-a.fbx",
                     })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    Debug.Log($"[Reconnect] MISSING {path}");
                    continue;
                }
                var instance = Object.Instantiate(model);
                var bounds = new Bounds(instance.transform.position, Vector3.zero);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).Select(c => c.name);
                Debug.Log($"[Reconnect] {System.IO.Path.GetFileName(path)} size={bounds.size} center={bounds.center} min={bounds.min} clips=[{string.Join(",", clips)}]");
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>Diagnostics: size of every furniture model in metres at room scale (0.15) – for laying out rooms.</summary>
        [MenuItem("Reconnect/Diagnostics/Log Furniture Sizes (m)")]
        public static void LogFurnitureSizes()
        {
            const float scale = 0.15f;
            var lines = AssetDatabase.FindAssets("t:Model", new[] { Furniture.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(path =>
                {
                    var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    Object.DestroyImmediate(instance);
                    var s = bounds.size * scale;
                    return $"{System.IO.Path.GetFileNameWithoutExtension(path)} {s.x:0.00}x{s.z:0.00} h{s.y:0.00}";
                });
            Debug.Log("[Reconnect] SIZES " + string.Join(" | ", lines));
        }
    }
}
