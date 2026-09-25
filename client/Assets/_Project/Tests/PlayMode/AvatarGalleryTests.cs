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
    /// <summary>Renders all avatar figures side by side (idle and walking) into client/Logs/avatars.png – visual check.</summary>
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

            var root = new GameObject("Gallery").transform;
            root.position = new Vector3(0f, -500f, 0f);
            for (var i = 0; i < catalog.characters.Length; i++)
            {
                var figure = UnityEngine.Object.Instantiate(catalog.characters[i], root, false);
                figure.transform.localPosition = new Vector3((i % 6) * 1.4f, 0f, (i / 6) * -1.8f);
                figure.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // face the camera
                figure.transform.localScale = Vector3.one * catalog.scale;
                var animator = figure.GetComponentInChildren<Animator>() ?? figure.AddComponent<Animator>();
                animator.runtimeAnimatorController = catalog.animator;
                animator.SetBool("Walking", i % 2 == 1);
            }

            for (var frame = 0; frame < 40; frame++)
            {
                yield return null;
            }

            var cameraGo = new GameObject("Gallery Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = root.position + new Vector3(3.5f, 1.6f, -6.5f);
            camera.transform.LookAt(root.position + new Vector3(3.5f, 0.5f, -0.9f));
            camera.fieldOfView = 40f;
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
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "avatars.png"));
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log($"[Reconnect] Avatars: {path}");
        }
    }
}
