using System;
using Reconnect.Client.Auth;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Screens
{
    public sealed class LoginScreen : ScreenBase
    {
        private readonly VisualTreeAsset _template;
        private readonly AuthService _auth;
        private readonly Action _openRegister;

        public LoginScreen(VisualTreeAsset template, AuthService auth, Action openRegister)
        {
            _template = template;
            _auth = auth;
            _openRegister = openRegister;
        }

        protected override VisualTreeAsset Template => _template;

        protected override void OnShow()
        {
            var email = Q<TextField>("email");
            var password = Q<TextField>("password");
            var status = Q<Label>("status");
            var form = Q<VisualElement>("form");
            SetStatus(status, null);

            Q<Button>("login").clicked += () => RunAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(email.value) || string.IsNullOrEmpty(password.value))
                {
                    SetStatus(status, "Bitte E-Mail und Passwort eingeben.");
                    return;
                }

                SetStatus(status, "Anmelden …", isError: false);
                var result = await _auth.LoginAsync(email.value, password.value, Lifetime);
                if (!result.IsSuccess)
                {
                    // Success is handled by the router via AuthService.SessionChanged.
                    SetStatus(status, result.StatusCode == 401 ? "E-Mail oder Passwort falsch." : result.Error.ToDisplayString());
                }
            }, form);

            Q<Button>("to-register").clicked += _openRegister;
        }
    }
}
