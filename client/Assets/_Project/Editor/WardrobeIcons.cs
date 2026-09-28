using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Avatars;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reconnect.Client.Editor
{
    /// <summary>
    /// Pictures for the character creator's cards: every hairstyle, garment, pair of shoes, beard, brows and skin tone worn by
    /// the start figure in its idle pose, framed on the part (head, upper body, legs, feet, whole figure). Written once to
    /// Resources/WardrobeIcons/&lt;body&gt;/&lt;id&gt;.png (RECONNECT_RENDER_ICONS=1 renders all again).
    /// </summary>
    public static class WardrobeIcons
    {
        private const string Dir = "Assets/_Project/Resources/WardrobeIcons";
        private const int Size = 192;
        private const int Layer = 31;

        /// <summary>Batch entry: renders missing pictures without the whole Setup Project.</summary>
        [MenuItem("Reconnect/Render Wardrobe Icons")]
        public static void Run() => Render(AssetDatabase.LoadAssetAtPath<AvatarCatalog>("Assets/_Project/Settings/AvatarCatalog.asset"));

        public static void Render(AvatarCatalog catalog)
        {
            var idle = catalog.animator != null ? catalog.animator.animationClips.FirstOrDefault(c => c.name == "Idle_Loop") : null;
            var force = Environment.GetEnvironmentVariable("RECONNECT_RENDER_ICONS") == "1";
            var jobs = new List<(string Body, string Id, AvatarLookDto Look, string Frame)>();
            foreach (var body in new[] { Wardrobe.Female, Wardrobe.Male })
            {
                var start = Wardrobe.Default(body);
                jobs.Add((body, "body", start, "full"));
                jobs.AddRange(Wardrobe.Skins[body].Select(skin => (body, "skin-" + skin, start with { Skin = skin }, "face")));
                foreach (var (id, (kind, _)) in Wardrobe.Parts[body])
                {
                    var frame = kind switch
                    {
                        Wardrobe.Hair or Wardrobe.Beard or Wardrobe.Brows => "face",
                        Wardrobe.Hat => "head",
                        Wardrobe.Top => "top",
                        Wardrobe.Bottom => "legs",
                        Wardrobe.Shoes => "feet",
                        _ => "full",
                    };
                    var look = Wardrobe.Wear(kind == Wardrobe.Beard ? Wardrobe.Wear(start, "short04") : start, id);
                    jobs.Add((body, id, look, frame));
                }
            }
            jobs = jobs.Where(j => force || !File.Exists(PathOf(j.Body, j.Id))).ToList();
            if (jobs.Count == 0)
            {
                return;
            }

            var stage = new GameObject("Wardrobe Icon Stage");
            stage.transform.position = new Vector3(0f, -6000f, 0f);
            var camera = new GameObject("Icon Camera").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(40, 38, 54, 255);
            camera.cullingMask = 1 << Layer;
            camera.fieldOfView = 20f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            Light(stage.transform, Quaternion.Euler(30f, 30f, 0f), new Color(1f, 0.94f, 0.86f), 1.5f);
            Light(stage.transform, Quaternion.Euler(15f, -40f, 0f), new Color(0.75f, 0.83f, 1f), 0.6f);
            Light(stage.transform, Quaternion.Euler(20f, 180f, 0f), Color.white, 0.9f);   // rim from behind

            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            var read = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            AnimationMode.StartAnimationMode();
            try
            {
                foreach (var (body, id, look, frame) in jobs)
                {
                    var figure = AvatarAssembler.Build(look, stage.transform, catalog.wardrobe);
                    if (figure == null)
                    {
                        continue;
                    }
                    figure.transform.localRotation = Quaternion.Euler(0f, 180f - 18f, 0f);   // looks at the camera, a little turned
                    foreach (var child in figure.GetComponentsInChildren<Transform>(true))
                    {
                        child.gameObject.layer = Layer;
                    }
                    if (idle != null)
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(figure, idle, 0.5f);
                        AnimationMode.EndSampling();
                    }
                    figure.GetComponent<LODGroup>().ForceLOD(0);
                    Frame(camera, figure, frame);
                    Draw(camera, target);
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    read.Apply();
                    RenderTexture.active = previous;
                    Directory.CreateDirectory(Path.GetDirectoryName(PathOf(body, id))!);
                    File.WriteAllBytes(PathOf(body, id), read.EncodeToJPG(88));
                    Object.DestroyImmediate(figure);
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(read);
                Object.DestroyImmediate(stage);
            }
            AssetDatabase.Refresh();
        }

        private static string PathOf(string body, string id) => $"{Dir}/{body}/{id}.jpg";

        /// <summary>
        /// Points the camera at the part – head, upper body, legs, feet or the whole figure – measured on the posed mesh
        /// itself (baked), a band of heights relative to the top of the head.
        /// </summary>
        private static void Frame(Camera camera, GameObject figure, string frame)
        {
            var renderer = figure.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name.EndsWith("LOD0"));
            var baked = new Mesh();
            renderer.BakeMesh(baked, true);
            var matrix = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
            var points = baked.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
            Object.DestroyImmediate(baked);
            var ground = points.Min(p => p.y);
            var top = points.Max(p => p.y);
            var tall = top - ground;
            var (from, to, pitch, margin) = frame switch
            {
                "face" => (top - 0.27f, top + 0.01f, 4f, 1.15f),
                "head" => (top - 0.36f, top + 0.02f, 6f, 1.2f),
                "top" => (ground + tall * 0.5f, ground + tall * 0.84f, 6f, 1.25f),
                "legs" => (ground, ground + tall * 0.56f, 6f, 1.1f),
                "feet" => (ground, ground + 0.3f, 12f, 1.3f),
                _ => (ground, top, 5f, 1.08f),
            };
            var band = points.Where(p => p.y >= from && p.y <= to).ToArray();
            if (band.Length == 0)
            {
                band = points;
            }
            var centre = new Vector3(band.Average(p => p.x), (from + to) * 0.5f, band.Average(p => p.z));
            var across = band.Max(p => p.x) - band.Min(p => p.x);   // the camera looks along +Z: x is across the picture
            var height = Mathf.Max((to - from) * margin, frame == "feet" ? across * 1.12f : 0f);
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            var distance = height * 0.5f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(centre - rotation * Vector3.forward * distance, rotation);
        }

        private static void Draw(Camera camera, RenderTexture target)
        {
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request))
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.Render();
            }
        }

        private static void Light(Transform stage, Quaternion rotation, Color colour, float intensity)
        {
            var light = new GameObject("Icon Light").AddComponent<Light>();
            light.transform.SetParent(stage, false);
            light.transform.rotation = rotation;
            light.type = LightType.Directional;
            light.color = colour;
            light.intensity = intensity;
            light.cullingMask = 1 << Layer;
        }
    }
}
