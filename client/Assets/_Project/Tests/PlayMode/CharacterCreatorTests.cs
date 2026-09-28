using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Reconnect.Client.Rooms;
using Reconnect.Client.UI.Screens;
using Reconnect.Contracts.Avatars;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// The character creator without a backend: switch body, pick hair, clothes, colours and a walk style by tapping the
    /// cards, random looks, save. Screenshots of each step go to client/Logs/creator-*.png.
    /// </summary>
    public sealed class CharacterCreatorTests
    {
        private static RenderTexture _target;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Making_a_look_by_tapping_cards_and_saving_it()
        {
            var room = Object.FindFirstObjectByType<RoomView>();
            // Our own phone-sized panel rendering into a texture (screenshots work in batch mode).
            var app = Object.FindFirstObjectByType<Reconnect.Client.Core.AppBootstrap>();
            var appDocument = app.GetComponent<UIDocument>();
            var panel = Object.Instantiate(appDocument.panelSettings);
            panel.clearColor = true;
            panel.colorClearValue = new Color32(20, 18, 32, 255);
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = 1f;
            var target = new RenderTexture(430, 932, 24);
            panel.targetTexture = target;
            _target = target;
            var uiObject = new GameObject("Creator UI");
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            yield return null;
            var root = document.rootVisualElement;
            root.styleSheets.Add(app.Ui.theme);
            root.style.flexGrow = 1;
            AvatarLookDto saved = null;
            var closed = false;
            var creator = new CharacterCreatorPanel(root, room.AvatarCatalog, look =>
            {
                saved = look;
                return Task.FromResult<string>(null);
            }, () => closed = true);

            creator.Open(null, firstTime: true);
            yield return Frames(10);
            Assert.IsNotNull(root.Q("creator"), "the creator is shown");
            Assert.Greater(FigurePixels(creator.Preview.Texture), 2000, "the figure stands on the stage");
            yield return Shot("creator-body");

            // A man: tap "Mann", then build a look from the cards.
            Tap(root.Query<Button>(className: "creator-card").ToList().First(b => b.tooltip == "Mann"));
            Assert.AreEqual(Wardrobe.Male, creator.Look.Body);

            // Body and face shape with sliders and cards: live in the look, the camera goes to the face.
            root.Q<Slider>("creator-morph-weight").value = 0.6f;
            Assert.AreEqual(0.6f, Wardrobe.ShapeOf(creator.Look, "weight"), 0.01f);
            Tap(root.Q<Button>("creator-category-face"));
            Assert.AreEqual(CharacterPreview.Focus.Face, creator.Preview.View, "the camera looks at the face");
            Tap(root.Q<Button>("creator-shape-head-square"));
            Assert.Greater(Wardrobe.ShapeOf(creator.Look, "head-square"), 0f);
            Tap(root.Q<Button>("creator-category-nose"));
            root.Q<Slider>("creator-morph-nose-width").value = 0.9f;
            root.Q<Slider>("creator-morph-nose-bridge").value = 0.7f;
            yield return Frames(20);
            yield return Shot("creator-nose");
            Tap(root.Q<Button>("creator-full"));
            Assert.AreEqual(CharacterPreview.Focus.Full, creator.Preview.View, "Ganzer Look");
            Tap(root.Q<Button>("creator-undo"));
            Assert.AreEqual(0f, Wardrobe.ShapeOf(creator.Look, "nose-width"), 0.01f, "undo takes back the last change (the nose)");
            Assert.Greater(Wardrobe.ShapeOf(creator.Look, "head-square"), 0f, "… only that");
            Assert.IsEmpty(Wardrobe.Problems(creator.Look));

            Tap(root.Q<Button>("creator-category-hair"));
            Tap(root.Q<Button>("creator-card-cortu_short_messy_hair"));
            Tap(root.Q<Button>("swatch-espresso"));
            yield return Frames(5);
            yield return Shot("creator-hair");
            Assert.AreEqual("espresso", Wardrobe.Worn(creator.Look, Wardrobe.Hair).Tint);

            Tap(root.Q<Button>("creator-category-beard"));
            Tap(root.Q<Button>("creator-card-wdg_scruffy_beard"));
            Tap(root.Q<Button>("creator-category-outfit"));
            Tap(root.Q<Button>("creator-card-toigo_suit_with_jacket_and_bowtie"));
            Assert.AreEqual(CharacterPreview.Focus.Full, creator.Preview.View);
            Assert.IsNull(Wardrobe.Worn(creator.Look, Wardrobe.Top), "the suit replaces the t-shirt");
            yield return Frames(5);
            yield return Shot("creator-outfit");

            Tap(root.Q<Button>("creator-category-shoes"));
            Assert.AreEqual(CharacterPreview.Focus.Feet, creator.Preview.View, "shoes: the camera goes to the feet");
            yield return Frames(30);
            yield return Shot("creator-shoes");
            Tap(root.Q<Button>("creator-category-top"));
            Tap(root.Q<Button>("creator-card-namuhekam_male_polo_shirt"));
            Tap(root.Q<Button>("swatch-navy"));
            Assert.IsNotNull(Wardrobe.Worn(creator.Look, Wardrobe.Bottom), "a top brings trousers back");

            Tap(root.Q<Button>("creator-category-walk"));
            Tap(root.Query<Button>(className: "creator-card").ToList().First(b => b.tooltip == "Selbstbewusst"));
            Assert.AreEqual("confident", creator.Look.WalkStyle);
            Assert.IsTrue(creator.Preview.Walking, "choosing a walk style shows it");
            yield return Frames(30);
            yield return Shot("creator-walk");

            for (var i = 0; i < 5; i++)
            {
                Tap(root.Q<Button>("creator-random"));
                Assert.IsEmpty(Wardrobe.Problems(creator.Look));
            }
            yield return Frames(5);
            yield return Shot("creator-random");

            var look = creator.Look;
            Tap(root.Q<Button>("creator-save"));
            yield return Frames(2);
            Assert.AreEqual(look, saved, "the look is handed over to be saved");
            Assert.IsTrue(closed);
            Assert.IsNull(root.Q("creator"));
            Object.Destroy(uiObject);
        }

        private static void Tap(Button button)
        {
            Assert.IsNotNull(button, "button exists");
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        /// <summary>Pixels of the stage that are not its background.</summary>
        private static int FigurePixels(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            RenderTexture.active = previous;
            var background = new Color(0.11f, 0.11f, 0.15f);
            var count = read.GetPixels().Count(p => Mathf.Abs(p.r - background.r) + Mathf.Abs(p.g - background.g) + Mathf.Abs(p.b - background.b) > 0.08f);
            Object.Destroy(read);
            return count;
        }

        private static IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(1.2f);   // the camera glides (frames are very fast in batch mode)
            yield return null;
            yield return null;   // the panel has repainted into its texture (WaitForEndOfFrame hangs in batch mode)
            var target = _target;
            RenderTexture.active = target;
            var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            shot.Apply();
            RenderTexture.active = null;
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), shot.EncodeToPNG());
            Object.Destroy(shot);
        }
    }
}
