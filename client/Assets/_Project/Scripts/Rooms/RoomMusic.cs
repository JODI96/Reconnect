using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Plays the music of the current room on a loop, fades between rooms and remembers whether the player muted it.
    /// Sits next to <see cref="RoomView"/> (added by ProjectSetup).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class RoomMusic : MonoBehaviour
    {
        private const string MutedKey = "reconnect.music.muted";
        private const float Volume = 0.35f;
        private const float FadeSeconds = 1.5f;

        [SerializeField] private MusicCatalog catalog;

        private AudioSource _source;
        private float _target;
        private bool _leaving;

        public bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
                _target = value || _source.clip == null ? 0f : Volume;
            }
        }

        public AudioClip Current => _source != null ? _source.clip : null;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0f;
        }

        /// <summary>Starts the track of the theme (fades in); the same track keeps playing across a lift ride.</summary>
        public void Play(string theme)
        {
            var clip = catalog != null ? catalog.For(theme) : null;
            if (clip != _source.clip)
            {
                _source.clip = clip;
                _source.volume = 0f;
                if (clip != null)
                {
                    _source.Play();
                }
            }
            _target = clip == null || Muted ? 0f : Volume;
            _leaving = false;
        }

        /// <summary>Fades out and stops (room left).</summary>
        public void Stop()
        {
            _target = 0f;
            _leaving = true;
        }

        private void Update()
        {
            _source.volume = Mathf.MoveTowards(_source.volume, _target, Volume / FadeSeconds * Time.unscaledDeltaTime);
            if (_leaving && _source.volume == 0f)
            {
                _source.Stop();
                _source.clip = null;
                _leaving = false;
            }
        }
    }
}
