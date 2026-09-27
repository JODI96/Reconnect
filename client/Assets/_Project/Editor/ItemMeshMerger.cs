using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reconnect.Client.Editor
{
    /// <summary>
    /// Furniture models come in many parts (a Poly Haven chair: frame, cushion, legs … each its own mesh). Drawn as they
    /// are, a full tower storey costs several draw calls per item. This bakes every catalog model into one mesh with one
    /// sub-mesh per material (Assets/_Project/Build/Merged/&lt;id&gt;.mesh + .prefab), shared by all copies of the item.
    /// Models with anything but plain meshes (lights, scripts, skinning) and those whose parts are looked up by name stay
    /// as they are.
    /// </summary>
    internal static class ItemMeshMerger
    {
        private const string Dir = "Assets/_Project/Build/Merged";

        /// <summary>Models whose parts are found by name (chess pieces for the piece pictures).</summary>
        private static readonly HashSet<string> KeepParts = new() { "ph-chess_set" };

        /// <summary>The merged prefab for the model, or the model itself when it can't or needn't be merged.</summary>
        public static GameObject Merge(string itemId, GameObject model)
        {
            if (model == null || KeepParts.Contains(itemId))
            {
                return model;
            }
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length < 2 || model.GetComponentsInChildren<Component>(true).Any(c => c is not (Transform or MeshFilter or MeshRenderer)))
            {
                return model;
            }

            var root = model.transform;
            var toRoot = root.worldToLocalMatrix;
            var materials = new List<Material>();
            var parts = new Dictionary<Material, List<CombineInstance>>();
            foreach (var renderer in renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !renderer.enabled || !IsActive(renderer.transform, root))
                {
                    continue;
                }
                var shared = renderer.sharedMaterials;
                for (var sub = 0; sub < mesh.subMeshCount && shared.Length > 0; sub++)
                {
                    var material = shared[Mathf.Min(sub, shared.Length - 1)];
                    if (material == null)
                    {
                        continue;
                    }
                    if (!parts.TryGetValue(material, out var list))
                    {
                        parts[material] = list = new List<CombineInstance>();
                        materials.Add(material);
                    }
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = toRoot * renderer.transform.localToWorldMatrix });
                }
            }
            if (materials.Count == 0)
            {
                return model;
            }

            // One mesh per material, then those as the sub-meshes of the item's mesh.
            var perMaterial = materials.Select(material =>
            {
                var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                part.CombineMeshes(parts[material].ToArray(), true, true);
                return part;
            }).ToList();
            var vertices = perMaterial.Sum(p => p.vertexCount);
            var merged = new Mesh { name = itemId, indexFormat = vertices > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            merged.CombineMeshes(perMaterial.Select(p => new CombineInstance { mesh = p, transform = Matrix4x4.identity }).ToArray(), false, false);
            merged.RecalculateBounds();
            merged.Optimize();
            foreach (var part in perMaterial)
            {
                Object.DestroyImmediate(part);
            }

            Directory.CreateDirectory(Dir);
            var meshPath = $"{Dir}/{itemId}.mesh";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(merged, existing);   // keeps the asset's GUID (no churn in references)
                Object.DestroyImmediate(merged);
                merged = existing;
                EditorUtility.SetDirty(merged);
            }
            else
            {
                AssetDatabase.CreateAsset(merged, meshPath);
            }

            var go = new GameObject(itemId);
            go.transform.localPosition = root.localPosition;
            go.transform.localRotation = root.localRotation;
            go.transform.localScale = root.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = merged;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = materials.ToArray();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/{itemId}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static bool IsActive(Transform transform, Transform root)
        {
            for (var t = transform; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                {
                    return false;
                }
                if (t == root)
                {
                    break;
                }
            }
            return true;
        }
    }
}
