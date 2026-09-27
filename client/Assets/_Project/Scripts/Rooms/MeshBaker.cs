using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Items we build from primitives (bar, lift core, facade, game stations …) consist of dozens of little boxes. This
    /// merges the still parts of one item into one mesh per material, so each costs a few draw calls instead of dozens.
    /// Moving parts (anything with a script on it or above it inside the item) and meshes that can't be read stay as they
    /// are; colliders stay where they were.
    /// </summary>
    public static class MeshBaker
    {
        public static void MergeStill(Transform item)
        {
            var groups = new Dictionary<Material, List<(MeshFilter Filter, MeshRenderer Renderer)>>();
            var order = new List<Material>();
            foreach (var renderer in item.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable || !renderer.enabled
                    || renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null || Moves(renderer.transform, item))
                {
                    continue;
                }
                if (!groups.TryGetValue(renderer.sharedMaterial, out var list))
                {
                    groups[renderer.sharedMaterial] = list = new List<(MeshFilter, MeshRenderer)>();
                    order.Add(renderer.sharedMaterial);
                }
                list.Add((filter, renderer));
            }

            var toItem = item.worldToLocalMatrix;
            foreach (var material in order)
            {
                var parts = groups[material];
                if (parts.Count < 2)
                {
                    continue;
                }
                var combine = new CombineInstance[parts.Count];
                var vertices = 0;
                for (var i = 0; i < parts.Count; i++)
                {
                    combine[i] = new CombineInstance { mesh = parts[i].Filter.sharedMesh, transform = toItem * parts[i].Filter.transform.localToWorldMatrix };
                    vertices += parts[i].Filter.sharedMesh.vertexCount;
                }
                var mesh = new Mesh { name = item.name + " " + material.name, indexFormat = vertices > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.CombineMeshes(combine, true, true);
                var merged = new GameObject("Merged " + material.name);
                merged.transform.SetParent(item, false);
                merged.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = merged.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = parts[0].Renderer.shadowCastingMode;
                renderer.receiveShadows = parts[0].Renderer.receiveShadows;
                foreach (var (filter, partRenderer) in parts)
                {
                    Remove(partRenderer);
                    Remove(filter);
                }
            }
        }

        private static void Remove(Object component)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(component);
            }
            else
            {
                Object.DestroyImmediate(component);
            }
        }

        /// <summary>Whether a script sits on the part or between it and the item (spinning, bobbing, flickering …).</summary>
        private static bool Moves(Transform part, Transform item)
        {
            for (var t = part; t != null && t != item; t = t.parent)
            {
                if (t.GetComponent<MonoBehaviour>() != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
