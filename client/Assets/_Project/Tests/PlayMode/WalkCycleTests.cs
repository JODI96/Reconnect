using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Reconnect.Client.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// Visual and numeric check of the avatar body language with the real animator: the walk at four points of the
    /// gait (front and side: client/Logs/walk-front.png, walk-side.png) and every emote at its key moment
    /// (client/Logs/emotes.png). The walking body must stay centred over the tile and upright.
    /// </summary>
    public sealed class WalkCycleTests
    {
        private static readonly float[] Phases = { 0f, 0.25f, 0.5f, 0.75f };

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Walk_and_emotes_look_natural()
        {
            var catalog = Catalog();
            var root = new GameObject("Pose Test").transform;
            root.position = new Vector3(0f, -500f, 0f);
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);


            // Walk, front and side.
            foreach (var (view, yaw) in new[] { ("front", 180f), ("side", 90f) })
            {
                var figures = new List<Animator>();
                for (var i = 0; i < Phases.Length; i++)
                {
                    var animator = Spawn(catalog, 6, root, new Vector3(i * 1.1f, 0f, 0f), yaw);
                    var posture = animator.GetComponent<UprightPosture>();
                    posture.Walking = true;
                    posture.Snap();
                    animator.Play("Walk", 0, Phases[i]);
                    figures.Add(animator);
                }
                yield return Settle(figures);
                for (var i = 0; i < figures.Count; i++)
                {
                    var figure = figures[i];
                    var hips = figure.GetBoneTransform(HumanBodyBones.Hips).position;
                    var neck = figure.GetBoneTransform(HumanBodyBones.Neck).position;
                    var sideways = Vector3.Dot(hips - figure.transform.position, figure.transform.right);
                    var (lean, tilt) = Tilt(figure.transform, neck - hips);
                    Debug.Log($"[Reconnect] Walk ({view}) phase {Phases[i]:0.00}: hips sideways {sideways:+0.00;-0.00} m, forward {lean:+0.0;-0.0}°, sideways {tilt:+0.0;-0.0}°");
                    Assert.Less(Mathf.Abs(sideways), 0.1f, "the body stays over the tile");
                    Assert.Less(Mathf.Abs(lean), 8f, "the body walks upright");
                    Assert.Less(Mathf.Abs(tilt), 6f, "the body does not hang to one side");
                }
                Render(root.position + new Vector3(1.65f, 0.95f, 0f), 6.2f, $"walk-{view}.png");
                Clear(root);
                yield return null;
            }

            // Emotes at their key moment, slightly turned to the camera.
            var emotes = new (string Label, string Layer, string State, float Time)[]
            {
                ("wave", "Arm Gestures", "wave", 0.45f),
                ("yes", "Head Gestures", "yes", 0.3f),
                ("no", "Head Gestures", "no", 0.3f),
                ("jump", null, "JumpStart", 0.6f),
                ("sit", null, "Sitting", 0.5f),
            };
            var posed = new List<Animator>();
            for (var i = 0; i < emotes.Length; i++)
            {
                var animator = Spawn(catalog, i % 2 == 0 ? 0 : 6, root, new Vector3(i * 1.2f, 0f, 0f), 160f);
                var (_, layer, state, time) = emotes[i];
                var index = layer == null ? 0 : animator.GetLayerIndex(layer);
                animator.SetLayerWeight(index, 1f);
                animator.Play(state, index, time);
                if (state == "Sitting")
                {
                    var posture = animator.GetComponent<UprightPosture>();
                    posture.Sitting = true;
                    posture.Snap();
                }
                posed.Add(animator);
            }
            yield return Settle(posed);
            Render(root.position + new Vector3(2.4f, 0.95f, 0f), 7.5f, "emotes.png");
        }

        /// <summary>Forward and sideways tilt (degrees) of a body direction relative to the figure.</summary>
        private static (float Forward, float Side) Tilt(Transform figure, Vector3 direction)
        {
            var up = Vector3.Dot(direction, Vector3.up);
            return (Mathf.Atan2(Vector3.Dot(direction, figure.forward), up) * Mathf.Rad2Deg,
                    Mathf.Atan2(Vector3.Dot(direction, figure.right), up) * Mathf.Rad2Deg);
        }

        private static AvatarCatalog Catalog()
        {
            var room = Object.FindFirstObjectByType<RoomView>();
            var field = typeof(RoomView).GetField("avatarCatalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (AvatarCatalog)field!.GetValue(room);
        }

        private static Animator Spawn(AvatarCatalog catalog, int index, Transform root, Vector3 position, float yaw)
        {
            var figure = Object.Instantiate(catalog.characters[index], root, false);
            figure.transform.localPosition = position;
            figure.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var animator = figure.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = catalog.animator;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return animator;
        }

        /// <summary>Two frames: the animators write the pose, the posture correction runs on top.</summary>
        private static IEnumerator Settle(List<Animator> animators)
        {
            yield return null;
            yield return null;
        }

        private static void Clear(Transform root)
        {
            foreach (Transform child in root)
            {
                Object.Destroy(child.gameObject);
            }
        }

        private static void Render(Vector3 centre, float distance, string file)
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.position = centre + new Vector3(0f, 0.15f, -distance);
            camera.transform.LookAt(centre);
            camera.fieldOfView = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.2f, 0.25f);
            var target = new RenderTexture(1600, 900, 24);
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
