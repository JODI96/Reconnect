using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reconnect.Contracts.Avatars;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reconnect.Client.Rooms
{
    /// <summary>The wardrobe manifest of one body (tools/avatars/build_wardrobe.py → Resources/Wardrobe/&lt;body&gt;/wardrobe.json).</summary>
    [Serializable]
    public sealed class WardrobeManifest
    {
        public string body;
        public int proxyVertices;
        public Entry[] skins;
        public Entry[] eyes;
        public Part[] parts;

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string texture;
            public string normal;
            public string colour;
            public bool alpha;
        }

        [Serializable]
        public sealed class Part
        {
            public string id;
            public string kind;
            public string file;
            public Entry[] variants;
            public int triangles;
            public int[] hides;
            public int[] under;
        }

        public Part Find(string id) => parts.FirstOrDefault(p => p.id == id);
    }

    /// <summary>Materials the assembler clones (so their shader variants are in the build).</summary>
    [Serializable]
    public sealed class AvatarMaterials
    {
        public Material opaque;
        public Material opaqueNormal;
        public Material cutout;
        public Material recolour;

        /// <summary>Humanoid avatars of the two bodies (the wardrobe body shares the ready-made figures' rig).</summary>
        public Avatar femaleAvatar;
        public Avatar maleAvatar;
    }

    /// <summary>
    /// Builds a figure from a look (character creator): the body of the look with every worn part on its skeleton, the
    /// body's triangles under the clothes removed (parts list the body vertices they cover; the body mesh carries its
    /// vertex numbers in UV2), all merged into one skinned mesh per level of detail – one skinning pass per figure, one
    /// draw per material. Textures and materials are shared between figures; tinted textures are made once per tint.
    /// </summary>
    public static class AvatarAssembler
    {
        private const string Root = "Wardrobe/";
        private static readonly Dictionary<string, WardrobeManifest> Manifests = new();
        private static readonly Dictionary<string, Material> Materials = new();
        private static readonly Dictionary<string, Texture> Tinted = new();

        /// <summary>Screen height below which the reduced figure is used, and below which it is culled.</summary>
        private static readonly float[] LodHeights = { 0.3f, 0.07f, 0.004f };   // close-ups full, the room view reduced, crowds far away one draw

        private static readonly Dictionary<string, Material> Atlases = new();

        public static WardrobeManifest Manifest(string body)
        {
            if (!Manifests.TryGetValue(body, out var manifest))
            {
                var json = Resources.Load<TextAsset>(Root + body + "/wardrobe");
                manifest = json != null ? JsonUtility.FromJson<WardrobeManifest>(json.text) : null;
                Manifests[body] = manifest;
            }
            return manifest;
        }

        /// <summary>The figure, a child of <paramref name="parent"/>, with an Animator (humanoid) ready for the controller.</summary>
        public static GameObject Build(AvatarLookDto look, Transform parent, AvatarMaterials materials)
        {
            var manifest = Manifest(look.Body);
            var bodyModel = Resources.Load<GameObject>(Root + look.Body + "/body");
            if (manifest == null || bodyModel == null)
            {
                return null;
            }
            var figure = UnityEngine.Object.Instantiate(bodyModel, parent, false);
            figure.name = "Figure " + look.Body;
            var bodyRenderers = figure.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var skeleton = figure.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var rootBone = bodyRenderers.First(r => r.name.StartsWith("Body_LOD0")).rootBone;

            var worn = (look.Parts ?? Array.Empty<AvatarPartDto>())
                .Select(p => (Look: p, Part: manifest.Find(p.Id)))
                .Where(p => p.Part != null)
                .ToList();
            var brows = manifest.Find(look.Brows);
            var hidden = new HashSet<int>(worn.SelectMany(p => p.Part.hides ?? Array.Empty<int>()));
            var under = new HashSet<int>(worn.SelectMany(p => p.Part.under ?? Array.Empty<int>()));

            var lods = new List<LOD>();
            for (var level = 0; level < 3; level++)
            {
                var sources = new List<Source>();
                var body = bodyRenderers.First(r => r.name.StartsWith($"Body_LOD{level}"));
                sources.Add(new Source(body, SkinMaterial(look, manifest, materials), hidden, under));
                if (level == 0)
                {
                    sources.Add(new Source(bodyRenderers.First(r => r.name.StartsWith("Eyes")), EyesMaterial(look, manifest, materials), null));
                    sources.Add(new Source(bodyRenderers.First(r => r.name.StartsWith("Lashes")), LashesMaterial(look, materials), null));
                    if (brows != null)
                    {
                        AddPart(sources, look.Body, brows, new AvatarPartDto(brows.id), 0, materials);
                    }
                }
                foreach (var (partLook, part) in worn)
                {
                    AddPart(sources, look.Body, part, partLook, level, materials);
                }
                var merged = Merge(sources, skeleton, rootBone, figure.transform, $"Figure_LOD{level}", level == 2 ? materials : null);
                merged.transform.SetParent(figure.transform, false);
                lods.Add(new LOD(LodHeights[level], new Renderer[] { merged }));
            }
            foreach (var renderer in bodyRenderers)
            {
                renderer.gameObject.SetActive(false);   // gone at once (Destroy only at the end of the frame)
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(renderer.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(renderer.gameObject);   // editor: wardrobe pictures
                }
            }
            var group = figure.GetComponent<LODGroup>();
            if (group == null)
            {
                group = figure.AddComponent<LODGroup>();
            }
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();
            figure.transform.localScale = Vector3.one * Mathf.Clamp(look.Height, Wardrobe.MinHeight, Wardrobe.MaxHeight);
            var animator = figure.GetComponent<Animator>();
            if (animator == null)   // Unity's null: no ?? on components
            {
                animator = figure.AddComponent<Animator>();
            }
            var avatar = look.Body == Wardrobe.Male ? materials.maleAvatar : materials.femaleAvatar;
            if (avatar != null)
            {
                animator.avatar = avatar;
            }
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.applyRootMotion = false;
            animator.Rebind();   // knows the avatar and the merged renderers (its visibility for culling) from now on
            return figure;
        }

        private static void AddPart(List<Source> sources, string body, WardrobeManifest.Part part, AvatarPartDto look, int level, AvatarMaterials materials)
        {
            var model = Resources.Load<GameObject>(Root + body + "/" + Path.GetFileNameWithoutExtension(part.file));
            var renderer = model != null
                ? model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name.EndsWith($"_LOD{level}"))
                : null;
            if (renderer != null)
            {
                sources.Add(new Source(renderer, PartMaterial(body, part, look, materials), null));
            }
        }

        // ---------- Merging ----------

        private sealed class Source
        {
            public Source(SkinnedMeshRenderer renderer, Material material, HashSet<int> hiddenVertices, HashSet<int> underVertices = null)
            {
                Mesh = renderer.sharedMesh;
                Bones = renderer.bones.Select(b => b != null ? b.name : null).ToArray();
                Material = material;
                Hidden = hiddenVertices;
                Under = underVertices;
            }

            public Mesh Mesh { get; }
            public string[] Bones { get; }
            public Material Material { get; }
            public HashSet<int> Hidden { get; }

            /// <summary>Body vertices under clothes: pulled in a little, so moving skin never shows through the cloth.</summary>
            public HashSet<int> Under { get; }
        }

        /// <summary>How far skin under clothes is pulled in (metres).</summary>
        private const float SkinInset = 0.02f;

        /// <param name="atlasTemplate">
        /// Set for the far level: every part's texture goes small into one texture of the figure (a cell each), the mesh
        /// points into it – one material, one draw per figure in a crowd.
        /// </param>
        private static SkinnedMeshRenderer Merge(List<Source> sources, Dictionary<string, Transform> skeleton, Transform rootBone, Transform figure, string name,
            AvatarMaterials atlasTemplate = null)
        {
            Material atlas = null;
            var cells = new Dictionary<Material, int>();
            if (atlasTemplate != null)
            {
                foreach (var source in sources.Where(s => s.Mesh != null && !cells.ContainsKey(s.Material)).Take(AtlasGrid * AtlasGrid))
                {
                    cells[source.Material] = cells.Count;
                }
                atlas = Atlas(cells.Keys.ToList(), atlasTemplate);
            }
            var bones = new List<Transform>();
            var bindposes = new List<Matrix4x4>();
            var boneIndex = new Dictionary<string, int>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var submeshes = new List<(Material Material, List<int> Triangles)>();

            foreach (var source in sources)
            {
                var mesh = source.Mesh;
                if (mesh == null || !mesh.isReadable)
                {
                    continue;
                }
                // Bone numbers of this mesh → numbers in the merged mesh (by bone name, same rig for every part).
                var map = new int[source.Bones.Length];
                for (var i = 0; i < source.Bones.Length; i++)
                {
                    var bone = source.Bones[i];
                    if (bone == null || !skeleton.TryGetValue(bone, out var transform))
                    {
                        map[i] = 0;
                        continue;
                    }
                    if (!boneIndex.TryGetValue(bone, out var index))
                    {
                        index = bones.Count;
                        boneIndex[bone] = index;
                        bones.Add(transform);
                        bindposes.Add(mesh.bindposes[i]);
                    }
                    map[i] = index;
                }

                var offset = vertices.Count;
                var meshVertices = mesh.vertices;
                var meshNormals = mesh.normals;
                if (source.Under is { Count: > 0 })
                {
                    var numbers = new List<Vector2>();
                    mesh.GetUVs(1, numbers);
                    for (var v = 0; v < meshVertices.Length && numbers.Count == meshVertices.Length; v++)
                    {
                        if (source.Under.Contains(Mathf.RoundToInt(numbers[v].x)))
                        {
                            meshVertices[v] -= meshNormals[v] * SkinInset;
                        }
                    }
                }
                vertices.AddRange(meshVertices);
                normals.AddRange(meshNormals);
                var meshTangents = mesh.tangents;
                tangents.AddRange(meshTangents.Length == mesh.vertexCount ? meshTangents : new Vector4[mesh.vertexCount]);
                var meshUvs = mesh.uv;
                if (meshUvs.Length != mesh.vertexCount)
                {
                    meshUvs = new Vector2[mesh.vertexCount];
                }
                if (atlas != null)
                {
                    var cell = cells.TryGetValue(source.Material, out var c) ? c : 0;
                    for (var v = 0; v < meshUvs.Length; v++)
                    {
                        meshUvs[v] = AtlasUv(cell, meshUvs[v]);
                    }
                }
                uvs.AddRange(meshUvs);
                foreach (var weight in mesh.boneWeights)
                {
                    weights.Add(new BoneWeight
                    {
                        boneIndex0 = map[weight.boneIndex0], weight0 = weight.weight0,
                        boneIndex1 = map[weight.boneIndex1], weight1 = weight.weight1,
                        boneIndex2 = map[weight.boneIndex2], weight2 = weight.weight2,
                        boneIndex3 = map[weight.boneIndex3], weight3 = weight.weight3,
                    });
                }

                // The body without what the clothes cover: a triangle goes when all its corners are covered.
                bool[] covered = null;
                if (source.Hidden is { Count: > 0 })
                {
                    var numbers = new List<Vector2>();
                    mesh.GetUVs(1, numbers);
                    if (numbers.Count == mesh.vertexCount)
                    {
                        covered = numbers.Select(n => source.Hidden.Contains(Mathf.RoundToInt(n.x))).ToArray();
                    }
                }
                var material = atlas ?? source.Material;
                var triangles = submeshes.FirstOrDefault(s => s.Material == material).Triangles;
                if (triangles == null)
                {
                    triangles = new List<int>();
                    submeshes.Add((material, triangles));
                }
                for (var sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var indices = mesh.GetTriangles(sub);
                    for (var t = 0; t < indices.Length; t += 3)
                    {
                        if (covered != null)
                        {
                            if (covered[indices[t]] && covered[indices[t + 1]] && covered[indices[t + 2]])
                            {
                                continue;   // only fully covered triangles go, the edge of a garment never shows a gap
                            }
                        }
                        triangles.Add(offset + indices[t]);
                        triangles.Add(offset + indices[t + 1]);
                        triangles.Add(offset + indices[t + 2]);
                    }
                }
            }

            var merged = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            merged.SetVertices(vertices);
            merged.SetNormals(normals);
            merged.SetTangents(tangents);
            merged.SetUVs(0, uvs);
            merged.boneWeights = weights.ToArray();
            merged.bindposes = bindposes.ToArray();
            merged.subMeshCount = submeshes.Count;
            for (var i = 0; i < submeshes.Count; i++)
            {
                merged.SetTriangles(submeshes[i].Triangles, i, false);
            }
            merged.RecalculateBounds();

            var go = new GameObject(name);
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = merged;
            renderer.bones = bones.ToArray();
            renderer.rootBone = rootBone;
            renderer.sharedMaterials = submeshes.Select(s => s.Material).ToArray();
            renderer.quality = SkinQuality.Bone2;
            renderer.updateWhenOffscreen = false;
            renderer.skinnedMotionVectors = false;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            // Culling box around the whole figure (feet to raised hands, sitting, walking). The box is in the root bone's space
            // (the hips, with their own turned axes): its centre is the figure's middle, a cube so the axes don't matter.
            var middle = rootBone.InverseTransformPoint(figure.TransformPoint(new Vector3(0f, 0.95f, 0f)));
            var size = 2.3f / Mathf.Max(0.0001f, rootBone.lossyScale.x / figure.lossyScale.x);
            renderer.localBounds = new Bounds(middle, Vector3.one * size);
            return renderer;
        }

        // ---------- Atlas of the far level ----------

        private const int AtlasSize = 256;
        private const int AtlasGrid = 4;   // 4 × 4 cells of 64 pixels
        private const int AtlasPadding = 3;

        /// <summary>Graphics.DrawTexture's internal shader doubles the colour: half = unchanged.</summary>
        private const float AtlasColourScale = 0.5f;

        private static Vector2 AtlasUv(int cell, Vector2 uv)
        {
            var size = AtlasSize / AtlasGrid;
            var inner = (size - 2f * AtlasPadding) / AtlasSize;
            var x = cell % AtlasGrid * size + AtlasPadding;
            var y = cell / AtlasGrid * size + AtlasPadding;
            return new Vector2(x / (float)AtlasSize + Mathf.Clamp01(uv.x) * inner, y / (float)AtlasSize + Mathf.Clamp01(uv.y) * inner);
        }

        /// <summary>One cut-out material (hair needs it) whose texture holds every part small, made once per combination.</summary>
        private static Material Atlas(List<Material> parts, AvatarMaterials materials)
        {
            var key = string.Join("|", parts.Select(p => p.name));
            if (Atlases.TryGetValue(key, out var done) && done != null)
            {
                return done;
            }
            var target = new RenderTexture(AtlasSize, AtlasSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "Figure atlas", useMipMap = true, autoGenerateMips = true, wrapMode = TextureWrapMode.Clamp,
            };
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(0.5f, 0.5f, 0.5f, 1f));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, AtlasSize, 0, AtlasSize);   // origin bottom left, like UVs
            var size = AtlasSize / AtlasGrid;
            for (var i = 0; i < parts.Count; i++)
            {
                var texture = parts[i].GetTexture("_BaseMap");
                var colour = parts[i].GetColor("_BaseColor");
                var cell = new Rect(i % AtlasGrid * size, i / AtlasGrid * size, size, size);
                var inner = new Rect(cell.x + AtlasPadding, cell.y + AtlasPadding, size - 2 * AtlasPadding, size - 2 * AtlasPadding);
                var source = texture != null ? texture : Texture2D.whiteTexture;
                var tint = texture != null ? Color.white : colour;
                // Once over the whole cell (fills the padding, so smaller mip levels don't bleed grey), then exactly where
                // the UVs point.
                Graphics.DrawTexture(cell, source, new Rect(0f, 0f, 1f, 1f), 0, 0, 0, 0, tint * AtlasColourScale);
                Graphics.DrawTexture(inner, source, new Rect(0f, 0f, 1f, 1f), 0, 0, 0, 0, tint * AtlasColourScale);
            }
            GL.PopMatrix();
            RenderTexture.active = previous;

            var material = new Material(materials.cutout) { name = "Figure atlas " + Atlases.Count };
            material.SetTexture("_BaseMap", target);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.25f);
            Atlases[key] = material;
            return material;
        }

        // ---------- Materials ----------

        private static Material SkinMaterial(AvatarLookDto look, WardrobeManifest manifest, AvatarMaterials materials)
        {
            var skin = manifest.skins.FirstOrDefault(s => s.id == look.Skin) ?? manifest.skins.First();
            return Textured($"{look.Body}/skin/{skin.id}", materials.opaque, Texture(look.Body, skin.texture), null, 0.35f);
        }

        private static Material EyesMaterial(AvatarLookDto look, WardrobeManifest manifest, AvatarMaterials materials)
        {
            var eyes = manifest.eyes.FirstOrDefault(e => e.id == look.Eyes) ?? manifest.eyes.First();
            return Textured($"{look.Body}/eyes/{eyes.id}", materials.opaque, Texture(look.Body, eyes.texture), null, 0.85f);
        }

        private static Material LashesMaterial(AvatarLookDto look, AvatarMaterials materials) =>
            Textured($"{look.Body}/lashes", materials.cutout, Texture(look.Body, "lashes.png"), null, 0.2f);

        private static Material PartMaterial(string body, WardrobeManifest.Part part, AvatarPartDto look, AvatarMaterials materials)
        {
            var variant = part.variants.FirstOrDefault(v => v.id == look.Variant) ?? part.variants.FirstOrDefault();
            // Hair cards, and garments whose texture cuts hems, lace and edges out with alpha.
            var cutout = part.kind is Wardrobe.Hair or Wardrobe.Beard or Wardrobe.Brows || variant is { alpha: true };
            var template = cutout ? materials.cutout : materials.opaque;
            if (variant == null)
            {
                return Plain($"{body}/{part.id}", template, Color.grey);
            }
            if (string.IsNullOrEmpty(variant.texture))
            {
                // A part in one plain colour (solid brows); a tint changes it.
                var colour = look.Tint != null ? ToColor(Wardrobe.TintColour(look.Tint)) : ParseHex(variant.colour);
                return Plain($"{body}/{part.id}/{variant.id}/{look.Tint}", materials.opaque, colour);
            }
            var texture = (Texture)Texture(body, variant.texture);
            if (look.Tint != null && texture != null)
            {
                texture = Recoloured(texture, look.Tint, materials.recolour);
            }
            var normal = !cutout && !string.IsNullOrEmpty(variant.normal) ? Texture(body, variant.normal) : null;   // no normal map on cut-out
            if (normal != null)
            {
                template = materials.opaqueNormal;
            }
            return Textured($"{body}/{part.id}/{variant.id}/{look.Tint}", template, texture, normal, cutout ? 0.25f : 0.3f);
        }

        private static Material Textured(string key, Material template, Texture texture, Texture normal, float smoothness)
        {
            if (!Materials.TryGetValue(key, out var material) || material == null)
            {
                material = new Material(template) { name = key };
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", smoothness);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                }
                Materials[key] = material;
            }
            return material;
        }

        private static Material Plain(string key, Material template, Color colour)
        {
            if (!Materials.TryGetValue(key, out var material) || material == null)
            {
                material = new Material(template) { name = key };
                material.SetTexture("_BaseMap", null);
                material.SetColor("_BaseColor", colour);
                Materials[key] = material;
            }
            return material;
        }

        private static Texture2D Texture(string body, string file) =>
            string.IsNullOrEmpty(file) ? null : Resources.Load<Texture2D>(Root + body + "/textures/" + Path.GetFileNameWithoutExtension(file));

        /// <summary>The texture in another colour, keeping light and shade (Hidden/Reconnect/Recolour), made once per tint.</summary>
        private static Texture Recoloured(Texture source, string tint, Material recolour)
        {
            var key = source.GetInstanceID() + "/" + tint;
            if (Tinted.TryGetValue(key, out var done) && done != null)
            {
                return done;
            }
            if (recolour == null)
            {
                return source;
            }
            var target = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = key, useMipMap = true, autoGenerateMips = true, wrapMode = TextureWrapMode.Clamp,
            };
            recolour.SetColor("_Tint", ToColor(Wardrobe.TintColour(tint)));
            Graphics.Blit(source, target, recolour);
            Tinted[key] = target;
            return target;
        }

        private static Color ToColor((float R, float G, float B) c) => new(c.R, c.G, c.B);

        private static Color ParseHex(string hex) =>
            ColorUtility.TryParseHtmlString("#" + (hex ?? "808080"), out var colour) ? colour : Color.grey;
    }
}
