using System;
using System.Globalization;
using Reconnect.Client.Auth;
using Reconnect.Contracts.Auth;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Screens
{
    public sealed class RegisterScreen : ScreenBase
    {
        public const int MinimumAge = 18;   // Server enforces the same rule (AgePolicy).

        private readonly VisualTreeAsset _template;
        private readonly AuthService _auth;
        private readonly Action _back;

        public RegisterScreen(VisualTreeAsset template, AuthService auth, Action back)
        {
            _template = template;
            _auth = auth;
            _back = back;
        }

        protected override VisualTreeAsset Template => _template;

        protected override void OnShow()
        {
            var displayName = Q<TextField>("display-name");
            var email = Q<TextField>("email");
            var password = Q<TextField>("password");
            var birthDate = Q<TextField>("birth-date");
            var status = Q<Label>("status");
            var form = Q<VisualElement>("form");
            SetStatus(status, null);

            Q<Button>("register").clicked += () => RunAsync(async () =>
            {
                if (!TryParseBirthDate(birthDate.value, out var birth))
                {
                    SetStatus(status, "Geburtsdatum bitte als TT.MM.JJJJ eingeben.");
                    return;
                }
                if (!IsAdult(birth, DateTime.Today))
                {
                    SetStatus(status, $"Reconnect ist erst ab {MinimumAge} Jahren.");
                    return;
                }

                SetStatus(status, "Konto wird erstellt …", isError: false);
                var request = new RegisterRequest(email.value.Trim(), password.value, displayName.value.Trim(), birth);
                var result = await _auth.RegisterAsync(request, Lifetime);
                if (!result.IsSuccess)
                {
                    SetStatus(status, result.Error.ToDisplayString());
                }
            }, form);

            Q<Button>("back").clicked += _back;
        }

        public static bool TryParseBirthDate(string text, out DateTime date) =>
            DateTime.TryParseExact(text?.Trim(), new[] { "dd.MM.yyyy", "d.M.yyyy" }, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date);

        public static bool IsAdult(DateTime birthDate, DateTime today)
        {
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.Date.AddYears(-age))
            {
                age--;
            }
            return age >= MinimumAge;
        }
    }
}
