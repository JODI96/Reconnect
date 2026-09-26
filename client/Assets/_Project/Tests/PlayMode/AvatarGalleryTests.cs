using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Reconnect.Client.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// Renders all avatar figures side by side into client/Logs/avatars.png – visual check of the realistic figures,
    /// their materials and the shared Humanoid animator (idle, walk, wave, talk).
    /// </summary>
    public sealed class AvatarGalleryTests
    {
        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator All_figures_are_animated()
        {
            var room = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var field = typeof(RoomView).GetField("avatarCatalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var catalog = (AvatarCatalog)field!.GetValue(room);
            Assert.IsNotNull(catalog);
            Assert.AreEqual(12, catalog.characters.Length);

            // Two rows (women, men) far apart, each rendered on its own; plus a close-up of the gestures.
            var root = new GameObject("Gallery").transform;
            root.position = new Vector3(0f, -500f, 0f);
            var animators = new Animator[catalog.characters.Length];
            for (var i = 0; i < catalog.characters.Length; i++)
            {
                var figure = UnityEngine.Object.Instantiate(catalog.characters[i], root, false);
                figure.transform.localPosition = new Vector3((i % 6) * 1.0f + (i / 6) * 40f, 0f, 0f);
                figure.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // face the camera
                figure.transform.localScale = Vector3.one * catalog.scale;
                var animator = figure.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator, catalog.characters[i].name + " has an animator");
                Assert.IsTrue(animator.isHuman, catalog.characters[i].name + " is a Humanoid");
                animator.runtimeAnimatorController = catalog.animator;
                animators[i] = animator;
            }
            yield return null;
            for (var i = 0; i < animators.Length; i++)
            {
                switch (i % 3)
                {
                    case 1: animators[i].SetBool("Walking", true); break;
                    case 2: animators[i].SetBool("Talking", true); break;
                }
            }

            // Gestures: three extra figures, sampled in the middle of each clip.
            var gestures = new[] { ("Arm Gestures", "wave", 0.45f), ("Head Gestures", "yes", 0.35f), ("Head Gestures", "no", 0.3f) };
            var gestureAnimators = new Animator[gestures.Length];
            for (var g = 0; g < gestures.Length; g++)
            {
                var figure = UnityEngine.Object.Instantiate(catalog.characters[g * 4 + 1], root, false);
                figure.transform.localPosition = new Vector3(80f + g * 1.0f, 0f, 0f);
                figure.transform.localRotation = Quaternion.Euler(0f, 160f, 0f);
                gestureAnimators[g] = figure.GetComponentInChildren<Animator>();
                gestureAnimators[g].runtimeAnimatorController = catalog.animator;
            }
            yield return null;
            var end = Time.realtimeSinceStartup + 0.9f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            for (var g = 0; g < gestures.Length; g++)
            {
                var (layer, state, time) = gestures[g];
                var index = gestureAnimators[g].GetLayerIndex(layer);
                gestureAnimators[g].SetLayerWeight(index, 1f);
                gestureAnimators[g].Play(state, index, time);
                gestureAnimators[g].Update(0f);
            }

            var light = new GameObject("Gallery Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            light.intensity = 1.2f;
            Render(root.position + new Vector3(2.5f, 0.95f, 0f), 7.2f, 1600, 900, "avatars.png");
            Render(root.position + new Vector3(42.5f, 0.95f, 0f), 7.2f, 1600, 900, "avatars-men.png");
            Render(root.position + new Vector3(81f, 1.2f, 0f), 4.2f, 1200, 900, "avatar-gestures.png");
        }

        /// <summary>Front view of <paramref name="centre"/> from <paramref name="distance"/> metres into client/Logs.</summary>
        private static void Render(Vector3 centre, float distance, int width, int height, string file)
        {
            var camera = new GameObject("Gallery Camera").AddComponent<Camera>();
            camera.transform.position = centre + new Vector3(0f, 0.3f, -distance);
            camera.transform.LookAt(centre);
            camera.fieldOfView = 35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.2f, 0.25f);
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", file));
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log($"[Reconnect] Avatars: {path}");
            camera.targetTexture = null;
            UnityEngine.Object.Destroy(camera.gameObject);
            target.Release();
        }
    }
}
