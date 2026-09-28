using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Avatars;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// Figures from the character creator's wardrobe (AvatarAssembler): every look builds, every worn part ends up in the
    /// figure, clothes hide the skin under them, and a line-up of looks standing and walking is rendered into
    /// client/Logs/wardrobe-*.png for a look. No backend needed.
    /// </summary>
    public sealed class AvatarWardrobeTests
    {
        private static readonly AvatarLookDto[] Looks =
        {
            Wardrobe.Default(Wardrobe.Male),
            new(Wardrobe.Male, "young_african_male", "brown", "eyebrow002", new[]
            {
                new AvatarPartDto("short04"), new AvatarPartDto("male_elegantsuit01", "default"), new AvatarPartDto("shoes03"),
            }, "confident"),
            new(Wardrobe.Male, "young_asian_male", "brown", "eyebrow001", new[]
            {
                new AvatarPartDto("cortu_short_messy_hair", Tint: "espresso"), new AvatarPartDto("namuhekam_male_polo_shirt", Tint: "navy"),
                new AvatarPartDto("cortu_cargo_pants", Tint: "sand"), new AvatarPartDto("toigo_ankle_boots_male"),
            }, "relaxed"),
            new(Wardrobe.Male, "toigo_light_skin_male_ginger", "green", "eyebrow003", new[]
            {
                new AvatarPartDto("short02", Tint: "copper"), new AvatarPartDto("wdg_scruffy_beard", Tint: "copper"),
                new AvatarPartDto("male_casualsuit01", "toigo_male_casual_suit_01_khaki"), new AvatarPartDto("shoes05"),
                new AvatarPartDto("fedora01"),
            }),
            Wardrobe.Default(Wardrobe.Female),
            new(Wardrobe.Female, "young_african_female", "brown", "eyebrow011", new[]
            {
                new AvatarPartDto("afro01"), new AvatarPartDto("toigo_camisole_top", Tint: "emerald"),
                new AvatarPartDto("toigo_long_full_skirt", Tint: "cream"), new AvatarPartDto("toigo_flats"),
            }, "model"),
            new(Wardrobe.Female, "young_asian_female", "brown", "eyebrow012", new[]
            {
                new AvatarPartDto("toigo_blunt_bob_with_bangs", Tint: "black"), new AvatarPartDto("toigo_female_suit"),
                new AvatarPartDto("toigo_stiletto_booties"),
            }, "elegant"),
            new(Wardrobe.Female, "toigo_light_skin_female_freckles", "blue", "eyebrow010", new[]
            {
                new AvatarPartDto("ponytail01", Tint: "blonde"), new AvatarPartDto("toigo_fisherman_sweater", Tint: "sage"),
                new AvatarPartDto("cortu_jeans_shorts"), new AvatarPartDto("toigo_ankle_boots_female"),
            }),
        };

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Looks_build_into_one_figure_each_with_the_skin_hidden_under_clothes()
        {
            var room = Object.FindFirstObjectByType<RoomView>();
            var field = typeof(RoomView).GetField("avatarCatalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var catalog = (AvatarCatalog)field!.GetValue(room);
            Assert.IsNotNull(catalog.wardrobe.opaque, "wardrobe materials are set up");

            foreach (var look in Looks)
            {
                Assert.IsEmpty(Wardrobe.Problems(look), "the test looks are valid");
            }

            // Standing row and walking row, facing the camera.
            var root = new GameObject("Wardrobe").transform;
            root.position = new Vector3(0f, -600f, 0f);
            var animators = new List<Animator>();
            for (var row = 0; row < 3; row++)
            {
                for (var i = 0; i < Looks.Length; i++)
                {
                    var holder = new GameObject("Look " + i).transform;
                    holder.SetParent(root, false);
                    holder.localPosition = new Vector3(i * 0.9f, 0f, row * 30f);
                    holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    var figure = AvatarAssembler.Build(Looks[i], holder, catalog.wardrobe);
                    Assert.IsNotNull(figure, $"look {i} builds");
                    var renderers = figure.GetComponentsInChildren<SkinnedMeshRenderer>();
                    Assert.AreEqual(3, renderers.Length, $"look {i}: one merged mesh per level of detail");
                    var far = renderers.First(r => r.name.EndsWith("LOD2"));
                    Assert.AreEqual(1, far.sharedMaterials.Length, $"look {i}: far away one material (atlas), one draw");
                    Assert.Less(far.sharedMesh.triangles.Length / 3, 1300, $"look {i}: far away a few triangles");
                    if (row == 2)
                    {
                        figure.GetComponent<LODGroup>().ForceLOD(2);   // the far level up close: colours right?
                    }
                    var lod0 = renderers.First(r => r.name.EndsWith("LOD0"));
                    // Body, eyes, lashes, brows and every worn part are in the figure (parts in the same material share one).
                    Assert.GreaterOrEqual(lod0.sharedMaterials.Length, 3 + Looks[i].Parts.Count - 1, $"look {i}: all parts built");
                    Debug.Log($"[Reconnect] Look {i}: {lod0.sharedMesh.triangles.Length / 3} triangles, {lod0.sharedMaterials.Length} materials, " +
                              $"LOD1 {renderers.First(r => r.name.EndsWith("LOD1")).sharedMesh.triangles.Length / 3} triangles, LOD2 {far.sharedMesh.triangles.Length / 3} triangles");
                    var animator = figure.GetComponent<Animator>();
                    Assert.IsTrue(animator != null && animator.isHuman, $"look {i}: humanoid figure");
                    animator.runtimeAnimatorController = catalog.ControllerFor(Looks[i].WalkStyle);
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // off the main camera, rendered by the test's own
                    animators.Add(animator);
                    if (row == 1)
                    {
                        animator.SetBool("Walking", true);
                    }
                }
            }

            // Clothes hide the body under them: a man in a suit shows far fewer body triangles than in a T-shirt and shorts.
            var bare = BodyTriangles(new AvatarLookDto(Wardrobe.Male, Wardrobe.Skins[Wardrobe.Male][1], "brown", "eyebrow001", new AvatarPartDto[0]), catalog);
            var suited = BodyTriangles(Looks[1], catalog);
            Debug.Log($"[Reconnect] Body triangles bare {bare}, in a suit {suited}");
            Assert.Less(suited, bare * 0.75f, "the suit hides the body under it");

            yield return null;
            var end = Time.realtimeSinceStartup + 1.1f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            var light = new GameObject("Wardrobe Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(30f, -25f, 0f);
            light.intensity = 1.3f;
            var width = (Looks.Length - 1) * 0.9f;
            Render(root.position + new Vector3(width / 2f, 0.95f, 0f), 7.4f, 1800, 900, "wardrobe-standing.png");
            Render(root.position + new Vector3(width / 2f, 0.95f, 30f), 7.4f, 1800, 900, "wardrobe-walking.png");
            Render(root.position + new Vector3(0.9f, 1.55f, 0f), 1.9f, 1200, 900, "wardrobe-faces.png");
            Render(root.position + new Vector3(width / 2f, 0.95f, 60f), 7.4f, 1800, 900, "wardrobe-far.png");
            Render(root.position + new Vector3(4f * 0.9f, 1.0f, 30f), 2.6f, 900, 1200, "wardrobe-closeup-dress.png");
            Render(root.position + new Vector3(0f, 1.2f, 30f), 2.6f, 900, 1200, "wardrobe-closeup-tshirt.png");
            Object.Destroy(root.gameObject);
            Object.Destroy(light.gameObject);
        }

        [UnityTest]
        public IEnumerator Shapes_change_body_and_face_and_the_clothes_follow()
        {
            var catalog = Object.FindFirstObjectByType<RoomView>().AvatarCatalog;
            var root = new GameObject("Shapes").transform;
            root.position = new Vector3(0f, -800f, 0f);
            var man = Wardrobe.Default(Wardrobe.Male);
            var woman = Wardrobe.Default(Wardrobe.Female);
            AvatarLookDto Shaped(AvatarLookDto look, params (string Id, float Value)[] shapes) =>
                shapes.Aggregate(look, (current, shape) => Wardrobe.WithShape(current, shape.Id, shape.Value));

            // Build: thin, average, heavy (clothes on) – then faces.
            var bodies = new[]
            {
                Shaped(man, ("weight", -1f)), man, Shaped(man, ("weight", 1f), ("muscle", 0.5f)),
                Shaped(woman, ("weight", -1f)), woman, Shaped(woman, ("weight", 1f), ("bust", 0.8f), ("hips", 0.8f)),
            };
            var widths = new List<float>();
            for (var i = 0; i < bodies.Length; i++)
            {
                var holder = new GameObject("Body " + i).transform;
                holder.SetParent(root, false);
                holder.localPosition = new Vector3(i * 0.9f, 0f, 0f);
                holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var figure = AvatarAssembler.Build(bodies[i], holder, catalog.wardrobe, levels: 1);
                // Girth at the waist (bind pose: T-pose, arms far above), clothes included.
                var renderer = figure.GetComponentInChildren<SkinnedMeshRenderer>();
                var baked = new Mesh();
                renderer.BakeMesh(baked, true);
                var waist = baked.vertices.Select(v => renderer.transform.TransformPoint(v) - holder.position)
                    .Where(v => v.y > 0.95f && v.y < 1.1f).ToList();
                Object.Destroy(baked);
                widths.Add((waist.Max(v => v.x) - waist.Min(v => v.x)) * (waist.Max(v => v.z) - waist.Min(v => v.z)));
            }

            var faces = new[]
            {
                man,
                Shaped(man, ("nose-width", 1f), ("nose-bridge", 1f), ("jaw", 1f)),
                Shaped(man, ("eye-size", 1f), ("eye-distance", -1f), ("eye-tilt", 1f), ("mouth-width", -1f), ("upper-lip", 1f)),
                Shaped(man, ("head-round", 1f), ("face-fullness", 1f), ("ear-angle", 1f), ("ear-size", 1f)),
                woman,
                Shaped(woman, ("head-heart", 1f), ("cheekbones", 1f), ("chin", 1f)),
                Shaped(woman, ("eye-open", 1f), ("eye-lid", 1f), ("nose-length", -1f), ("nose-tip", 1f), ("lower-lip", 1f)),
                Shaped(woman, ("face-length", 1f), ("nose-size", 1f), ("brow-height", 1f), ("mouth-corners", 1f)),
            };
            for (var i = 0; i < faces.Length; i++)
            {
                Assert.IsEmpty(Wardrobe.Problems(faces[i]), $"face {i} valid");
                var holder = new GameObject("Face " + i).transform;
                holder.SetParent(root, false);
                holder.localPosition = new Vector3(i * 0.45f, 0f, 20f);
                holder.localRotation = Quaternion.Euler(0f, 160f, 0f);
                AvatarAssembler.Build(faces[i], holder, catalog.wardrobe, levels: 1);
            }
            yield return null;
            var light = new GameObject("Shape Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(25f, -20f, 0f);
            Render(root.position + new Vector3(2.25f, 0.95f, 0f), 6.6f, 1800, 900, "shapes-bodies.png");
            Render(root.position + new Vector3(1.5f * 0.45f, 1.62f, 20f), 1.9f, 1800, 700, "shapes-faces-men.png");
            Render(root.position + new Vector3(5.5f * 0.45f, 1.55f, 20f), 1.9f, 1800, 700, "shapes-faces-women.png");
            Object.Destroy(root.gameObject);
            Object.Destroy(light.gameObject);
            Debug.Log($"[Reconnect] Build footprints: {string.Join(", ", widths.Select(w => w.ToString("0.000")))}");
            Assert.Less(widths[0], widths[1], "thin man is narrower (clothes included)");
            Assert.Less(widths[1], widths[2], "heavy man is wider (clothes included)");
            Assert.Less(widths[3], widths[5], "heavy woman is wider");

        }

        [UnityTest]
        public IEnumerator Every_walk_style_stays_upright_while_walking()
        {
            var room = Object.FindFirstObjectByType<RoomView>();
            var catalog = room.AvatarCatalog;
            var root = new GameObject("Walk styles").transform;
            root.position = new Vector3(0f, -700f, 0f);
            var figures = new List<(string Style, string Body, Animator Animator)>();
            var column = 0;
            foreach (var body in new[] { Wardrobe.Female, Wardrobe.Male })
            {
                foreach (var style in Wardrobe.WalkStyles.Keys)
                {
                    var holder = new GameObject(style).transform;
                    holder.SetParent(root, false);
                    holder.localPosition = new Vector3(column++ * 0.9f, 0f, 0f);
                    holder.localRotation = Quaternion.Euler(0f, 150f, 0f);
                    var figure = AvatarAssembler.Build(Wardrobe.Default(body) with { WalkStyle = style }, holder, catalog.wardrobe);
                    var animator = figure.GetComponent<Animator>();
                    animator.runtimeAnimatorController = catalog.ControllerFor(style);
                    if (style != "normal")
                    {
                        Assert.AreNotSame(catalog.animator, animator.runtimeAnimatorController, $"{style}: its own walk");
                    }
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.SetBool("Walking", true);
                    figures.Add((style, body, animator));
                }
            }
            var light = new GameObject("Walk Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(30f, -25f, 0f);
            var problems = new List<string>();
            var width = (column - 1) * 0.9f;
            for (var frame = 0; frame < 4; frame++)
            {
                var end = Time.realtimeSinceStartup + 0.8f;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    foreach (var (style, body, animator) in figures)
                    {
                        var head = animator.GetBoneTransform(HumanBodyBones.Head).position.y - root.position.y;
                        var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position.y - root.position.y;
                        var drift = Vector3.Distance(animator.transform.position, animator.transform.parent.position);
                        if (head < 1.3f || hips < 0.7f || drift > 0.05f)
                        {
                            problems.Add($"{body} {style}: head {head:0.00} m, hips {hips:0.00} m, drift {drift:0.00} m");
                        }
                    }
                }
                Render(root.position + new Vector3(width / 2f, 0.95f, 0f), 8.6f, 1800, 700, $"walkstyles-{frame}.png");
            }
            Object.Destroy(root.gameObject);
            Object.Destroy(light.gameObject);
            Assert.IsEmpty(problems.Distinct().Take(20), string.Join("\n", problems.Distinct().Take(20)));
        }

        private static int BodyTriangles(AvatarLookDto look, AvatarCatalog catalog)
        {
            var holder = new GameObject("Count").transform;
            var figure = AvatarAssembler.Build(look, holder, catalog.wardrobe);
            var renderer = figure.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name.EndsWith("LOD0"));
            var skin = System.Array.FindIndex(renderer.sharedMaterials, m => m.name.Contains("/skin/"));
            var count = renderer.sharedMesh.GetTriangles(skin).Length / 3;
            Object.Destroy(holder.gameObject);
            return count;
        }

        private static void Render(Vector3 centre, float distance, int width, int height, string file)
        {
            var camera = new GameObject("Wardrobe Camera").AddComponent<Camera>();
            camera.transform.position = centre + new Vector3(0f, 0.3f, -distance);
            camera.transform.LookAt(centre);
            camera.fieldOfView = 35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.22f, 0.22f, 0.26f);
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", file)), image.EncodeToPNG());
            camera.targetTexture = null;
            Object.Destroy(camera.gameObject);
            target.Release();
        }
    }
}
