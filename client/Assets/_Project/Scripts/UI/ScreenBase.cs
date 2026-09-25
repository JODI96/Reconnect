using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI
{
    /// <summary>
    /// A full-screen view built from a UXML template. Subclasses query their elements in
    /// <see cref="OnShow"/> and wire up events. Async work goes through <see cref="RunAsync"/>.
    /// </summary>
    public abstract class ScreenBase
    {
        private CancellationTokenSource _lifetime;

        protected VisualElement Root { get; private set; }

        /// <summary>Cancelled when the screen is left – pass it to API calls.</summary>
        protected CancellationToken Lifetime => _lifetime.Token;

        protected abstract VisualTreeAsset Template { get; }

        internal VisualElement Create()
        {
            _lifetime = new CancellationTokenSource();
            Root = Template.Instantiate();
            Root.AddToClassList("screen");
            OnShow();
            return Root;
        }

        internal void Destroy()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            OnHide();
        }

        protected abstract void OnShow();

        protected virtual void OnHide() { }

        protected T Q<T>(string name) where T : VisualElement =>
            Root.Q<T>(name) ?? throw new InvalidOperationException($"{GetType().Name}: element '{name}' missing in UXML.");

        /// <summary>
        /// Runs async UI work: disables <paramref name="busyElement"/> meanwhile, swallows
        /// cancellation when the screen closes and logs unexpected exceptions.
        /// </summary>
        protected async void RunAsync(Func<Task> work, VisualElement busyElement = null)
        {
            busyElement?.SetEnabled(false);
            try
            {
                await work();
            }
            catch (OperationCanceledException)
            {
                // Screen was closed – nothing to do.
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                busyElement?.SetEnabled(true);
            }
        }

        protected static void SetStatus(Label label, string message, bool isError = true)
        {
            label.text = message ?? "";
            label.EnableInClassList("status--error", isError);
            label.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
