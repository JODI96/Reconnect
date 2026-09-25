using UnityEngine;

namespace Reconnect.Client.Auth
{
    /// <summary>Persists the refresh token between app starts.</summary>
    public interface ITokenStore
    {
        string LoadRefreshToken();
        void SaveRefreshToken(string refreshToken);
        void Clear();
    }

    /// <summary>
    /// PlayerPrefs-based store – fine for development.
    /// TODO (before release): use iOS Keychain / Android Keystore instead of PlayerPrefs.
    /// </summary>
    public sealed class PlayerPrefsTokenStore : ITokenStore
    {
        private const string Key = "reconnect.refreshToken";

        public string LoadRefreshToken()
        {
            var value = PlayerPrefs.GetString(Key, "");
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public void SaveRefreshToken(string refreshToken)
        {
            PlayerPrefs.SetString(Key, refreshToken);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
