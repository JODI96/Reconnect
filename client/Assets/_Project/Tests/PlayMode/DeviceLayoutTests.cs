using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Reconnect.Client.Auth;
using Reconnect.Client.City;
using Reconnect.Client.Core;
using Reconnect.Client.Economy;
using Reconnect.Client.Networking;
using Reconnect.Client.Rooms;
using Reconnect.Client.UI;
using Reconnect.Client.UI.Screens;
using Reconnect.Contracts;
using Reconnect.Contracts.Buildings;
using Reconnect.Contracts.Common;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// Lays out every screen on the most common phones (size, density, notch / home indicator) and fails when
    /// something leaves the safe area, overlaps, cuts off text or is too small to tap. Screenshots of the UI go to
    /// client/Logs/devices/. Needs the local backend (dev admin); the city runs on swisstopo (no Google session).
    /// </summary>
    [Category("Integration")]
    public sealed class DeviceLayoutTests
    {
        private const string BaseUrl = "http://localhost:5191";
        private const string TokenKey = "reconnect.refreshToken";
        private const float MinTouch = 44f;
        private static readonly Guid PrimeTowerId = Guid.Parse("0199a000-0000-7000-8000-000000000003");

        /// <summary>Pixels, dpi and safe-area insets (pixels) as the devices report them.</summary>
        private static readonly Device[] Devices =
        {
            new("iphone-se", 750, 1334, 326, top: 40, bottom: 0),
            new("iphone-14", 1170, 2532, 460, top: 141, bottom: 102),
            new("iphone-15", 1179, 2556, 460, top: 177, bottom: 102),
            new("iphone-15-pro-max", 1290, 2796, 460, top: 177, bottom: 102),
            new("galaxy-s23", 1080, 2340, 480, top: 110, bottom: 63),
            new("pixel-8", 1080, 2400, 420, top: 132, bottom: 63),
        };

        private string _savedToken;
        private GameObject _uiObject;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            // The app itself stays on its login screen (no city, no room) while this test drives its own UI.
            _savedToken = PlayerPrefs.GetString(TokenKey, null);
            PlayerPrefs.DeleteKey(TokenKey);
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return new WaitForSeconds(2f);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (_uiObject != null)
            {
                UnityEngine.Object.Destroy(_uiObject);
            }
            if (!string.IsNullOrEmpty(_savedToken))
            {
                PlayerPrefs.SetString(TokenKey, _savedToken);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Every_screen_fits_the_common_phones()
        {
            var app = UnityEngine.Object.FindFirstObjectByType<AppBootstrap>();
            var appDocument = app.GetComponent<UIDocument>();
            appDocument.rootVisualElement.style.display = DisplayStyle.None;
            var city = UnityEngine.Object.FindFirstObjectByType<CityView>();
            var roomView = UnityEngine.Object.FindFirstObjectByType<RoomView>();

            var api = new ApiClient(new SwisstopoOnlyTransport(new UnityWebRequestTransport(15)), BaseUrl);
            var auth = new AuthService(api, new MemoryTokenStore());
            api.Tokens = auth;
            var login = auth.LoginAsync("Admin", "Admin");
            yield return Wait(login);
            if (!login.Result.IsSuccess)
            {
                Assert.Ignore("Backend with dev admin not running on " + BaseUrl + ".");
            }
            var rooms = new RoomService(api);
            var maps = new MapService(api, googleAllowed: false);
            var tower = new TowerService(api);
            var wallet = new WalletService(api);
            var realEstate = new RealEstateService(api);
            using var session = new SignalRRoomSession(BaseUrl, auth);

            var list = api.GetAsync<PagedResponse<RoomSummaryDto>>(ApiRoutes.Rooms.Group + "?pageSize=50");
            yield return Wait(list);
            var skyLounge = list.Result.Value.Items.Single(r => r.Name == "Sky Lounge").Id;
            var primeTower = new BuildingDto(PrimeTowerId, "Prime Tower", "Hardstrasse 201, 8005 Zürich", 47.38622, 8.51733, null);

            // Our own panel renders into a phone-sized texture; the navigator simulates the device.
            var ui = app.Ui;
            var panel = UnityEngine.Object.Instantiate(appDocument.panelSettings);
            panel.clearColor = true;
            panel.colorClearValue = new Color32(40, 60, 80, 255);   // stands in for the 3D world
            _uiObject = new GameObject("Device UI");
            var document = _uiObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            yield return null;
            var navigator = new ScreenNavigator(document.rootVisualElement, ui.theme, panel);

            var failures = new List<string>();
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "devices"));
            Directory.CreateDirectory(outDir);

            foreach (var device in Devices)
            {
                var target = new RenderTexture(device.Width, device.Height, 24);
                panel.targetTexture = target;
                navigator.Display = () => device.Metrics;

                IEnumerator Capture(string screenName, float settleSeconds = 0.4f)
                {
                    var end = Time.realtimeSinceStartup + settleSeconds;
                    do
                    {
                        navigator.UpdateLayout();
                        yield return null;
                    } while (Time.realtimeSinceStartup < end);
                    yield return null;   // the panel has repainted into the texture (WaitForEndOfFrame hangs in batch mode)
                    Save(target, Path.Combine(outDir, $"{device.Name}-{screenName}.png"));
                    failures.AddRange(Check(document.rootVisualElement, device).Select(f => $"{device.Name} / {screenName}: {f}"));
                }

                navigator.Show(new LoginScreen(ui.login, auth, () => { }));
                yield return Capture("login");
                navigator.Show(new RegisterScreen(ui.register, auth, () => { }));
                yield return Capture("register");
                navigator.Show(new RoomListScreen(ui.roomList, ui.roomListItem, rooms, auth, _ => { }, () => { }));
                yield return Capture("room-list", 2f);
                navigator.Show(new RoomDetailScreen(ui.roomDetail, rooms, skyLounge, () => { }, _ => { }));
                yield return Capture("room-detail", 1.5f);

                var cityScreen = new CityScreen(ui.city, city, app.CitySettings, new BuildingService(api), maps, tower, wallet, rooms, auth,
                    () => { }, _ => { }, _ => { }, _ => { });
                navigator.Show(cityScreen);
                yield return Capture("city", 2f);
                cityScreen.Select(primeTower);
                yield return Capture("city-tower", 2f);
                cityScreen.ShowMenu(true);
                yield return Capture("city-menu");

                navigator.Show(new OfficesScreen(ui.offices, realEstate, wallet, PrimeTowerId, () => { }));
                yield return Capture("offices", 2f);

                var roomScreen = new RoomScreen(ui.room, roomView, city, session, tower, rooms, skyLounge, auth.UserId!.Value,
                    isAdmin: true, () => { });
                navigator.Show(roomScreen);
                yield return Capture("room", 4f);
                roomScreen.OpenLift();
                yield return Capture("room-lift", 2f);
                roomScreen.OpenGame("tictactoe");
                yield return Capture("room-tictactoe");
                roomScreen.OpenGame("quiz");
                yield return Capture("room-quiz");
                foreach (var game in BoardGames.All)
                {
                    roomScreen.OpenGame(game);
                    yield return Capture("room-" + game);
                }
                roomScreen.OpenGame(null);
                roomScreen.OpenBuild();
                yield return Capture("room-build", 1f);
                roomScreen.Build.Editor.Pick("ph-sofa_02", roomView.Focus.x, roomView.Focus.z);
                yield return Capture("room-build-item", 1f);

                navigator.Show(new LoginScreen(ui.login, auth, () => { }));   // leaves the room
                yield return null;
                panel.targetTexture = null;
                target.Release();
            }

            Assert.IsEmpty(failures, "Layout problems:\n" + string.Join("\n", failures));
        }

        /// <summary>Visible controls must be inside the safe area, not overlap, show their text and be tappable.</summary>
        private static IEnumerable<string> Check(VisualElement root, Device device)
        {
            var scale = device.Metrics.UiScale;
            var safe = device.Metrics.SafeArea;
            var safeRect = new Rect(safe.xMin / scale, (device.Height - safe.yMax) / scale, safe.width / scale, safe.height / scale);

            // Everything visible on screen: scroll view content only where it is inside the viewport.
            var controls = root.Query<VisualElement>().Where(e => e is Button or TextField or Label).ToList()
                .Where(e => IsShown(e) && !IsOverlay(e) && VisibleBox(e).width > 0f && VisibleBox(e).height > 0f)
                .ToList();
            foreach (var control in controls)
            {
                var box = control.worldBound;
                var id = Describe(control);
                if (!InsideScrollView(control)
                    && (box.xMin < safeRect.xMin - 1f || box.xMax > safeRect.xMax + 1f || box.yMin < safeRect.yMin - 1f || box.yMax > safeRect.yMax + 1f))
                {
                    yield return $"{id} outside the safe area ({box} vs {safeRect})";
                }
                // Game boards are dense on purpose (8 chess squares must fit 360 pt): at least 36 pt there.
                var minTouch = control.ClassListContains("board-cell") ? 36f : MinTouch;
                if (control is Button && !InsideScrollView(control) && (box.height < minTouch - 0.5f || box.width < minTouch - 0.5f))
                {
                    yield return $"{id} too small to tap ({box.width:0}×{box.height:0})";
                }
                if (control is TextElement text && !string.IsNullOrEmpty(text.text) && text.resolvedStyle.whiteSpace == WhiteSpace.NoWrap
                    && text.resolvedStyle.textOverflow != TextOverflow.Ellipsis)
                {
                    var needed = text.MeasureTextSize(text.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                    if (needed > text.contentRect.width + 2f && text.childCount == 0)
                    {
                        yield return $"{id} text cut off ({needed:0} > {text.contentRect.width:0})";
                    }
                }
            }

            foreach (var scroller in root.Query<Scroller>().ToList().Where(s => IsShown(s) && s.worldBound.width > 0f))
            {
                yield return $"scroll bar visible in '{scroller.parent?.parent?.name}' (phones scroll by dragging)";
            }

            // No two controls may cover each other (a label inside its button or text field is fine).
            for (var i = 0; i < controls.Count; i++)
            {
                for (var j = i + 1; j < controls.Count; j++)
                {
                    if (controls[i].Contains(controls[j]) || controls[j].Contains(controls[i]))
                    {
                        continue;
                    }
                    var a = VisibleBox(controls[i]);
                    var b = VisibleBox(controls[j]);
                    var overlap = Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
                    if (overlap.width > 1f && overlap.height > 1f)
                    {
                        yield return $"{Describe(controls[i])} overlaps {Describe(controls[j])}";
                    }
                }
            }
        }

        /// <summary>The part of the element that is on screen: clipped by the viewports of scroll views around it.</summary>
        private static Rect VisibleBox(VisualElement element)
        {
            var box = element.worldBound;
            for (var e = element.parent; e != null; e = e.parent)
            {
                if (e is ScrollView scroll)
                {
                    var view = scroll.contentViewport.worldBound;
                    box = Rect.MinMaxRect(Mathf.Max(box.xMin, view.xMin), Mathf.Max(box.yMin, view.yMin),
                        Mathf.Min(box.xMax, view.xMax), Mathf.Min(box.yMax, view.yMax));
                }
            }
            return box.width > 0f && box.height > 0f ? box : Rect.zero;
        }

        private static bool IsShown(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Name tags, speech bubbles and building labels follow the 3D world and may leave the screen.</summary>
        private static bool IsOverlay(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent)
            {
                if (e.name == "labels" || e.name == "lift-ride")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Content of scroll views may be outside the viewport; the scroll view itself is checked.</summary>
        private static bool InsideScrollView(VisualElement element)
        {
            for (var e = element.parent; e != null; e = e.parent)
            {
                if (e is ScrollView || e is ListView)
                {
                    return true;
                }
            }
            return false;
        }

        private static string Describe(VisualElement e)
        {
            var text = e is TextElement t && !string.IsNullOrEmpty(t.text) ? $" \"{t.text}\"" : "";
            return $"{e.GetType().Name} '{e.name}'{text}";
        }

        private static void Save(RenderTexture target, string path)
        {
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
        }

        private static IEnumerator Wait(Task task)
        {
            var end = Time.realtimeSinceStartup + 15f;
            while (!task.IsCompleted)
            {
                Assert.Less(Time.realtimeSinceStartup, end, "timed out");
                yield return null;
            }
        }

        private sealed class Device
        {
            public Device(string name, int width, int height, float dpi, int top, int bottom)
            {
                Name = name;
                Width = width;
                Height = height;
                Metrics = new DisplayMetrics(width, height, dpi, new Rect(0, bottom, width, height - top - bottom), isMobile: true);
            }

            public string Name { get; }
            public int Width { get; }
            public int Height { get; }
            public DisplayMetrics Metrics { get; }
        }

        private sealed class MemoryTokenStore : ITokenStore
        {
            private string _token;
            public string LoadRefreshToken() => _token;
            public void SaveRefreshToken(string refreshToken) => _token = refreshToken;
            public void Clear() => _token = null;
        }

        /// <summary>Tests never start a paid Google session: the map session is answered with swisstopo.</summary>
        private sealed class SwisstopoOnlyTransport : IHttpTransport
        {
            private readonly IHttpTransport _inner;

            public SwisstopoOnlyTransport(IHttpTransport inner) => _inner = inner;

            public Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken ct) =>
                request.Url.EndsWith(ApiRoutes.Maps.Session, StringComparison.Ordinal)
                    ? Task.FromResult(new HttpResponse(200, Newtonsoft.Json.JsonConvert.SerializeObject(
                        new MapSessionDto(MapProviders.Swisstopo, null, true, null, MapFallbackReasons.NotConfigured, 170))))
                    : _inner.SendAsync(request, ct);
        }
    }
}
