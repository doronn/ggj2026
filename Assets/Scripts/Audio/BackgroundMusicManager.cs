using UnityEngine;

namespace BreakingHue.Audio
{
    /// <summary>
    /// Singleton manager for background music that persists across all scenes.
    /// Created in the Main Menu and plays continuously throughout the game.
    /// </summary>
    public class BackgroundMusicManager : MonoBehaviour
    {
        private static BackgroundMusicManager _instance;
        public static BackgroundMusicManager Instance => _instance;
        
        [Header("Music Settings")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] [Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private bool loop = true;
        
        private AudioSource _audioSource;
        
        /// <summary>
        /// Gets whether music is currently playing.
        /// </summary>
        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;
        
        /// <summary>
        /// Gets or sets the music volume.
        /// </summary>
        public float Volume
        {
            get => _audioSource != null ? _audioSource.volume : volume;
            set
            {
                volume = Mathf.Clamp01(value);
                if (_audioSource != null)
                {
                    _audioSource.volume = volume;
                }
            }
        }

        private void Awake()
        {
            // Singleton pattern with DontDestroyOnLoad
            if (_instance != null && _instance != this)
            {
                // Another instance already exists - destroy this one
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Setup AudioSource
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            _audioSource.playOnAwake = false;
            _audioSource.loop = loop;
            _audioSource.volume = volume;
            
            if (backgroundMusic != null)
            {
                _audioSource.clip = backgroundMusic;
            }
            
            // Start playing if configured
            if (playOnAwake && backgroundMusic != null)
            {
                Play();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Play the background music.
        /// </summary>
        public void Play()
        {
            if (_audioSource != null && _audioSource.clip != null && !_audioSource.isPlaying)
            {
                _audioSource.Play();
            }
        }

        /// <summary>
        /// Play a specific music clip.
        /// </summary>
        public void Play(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.clip = clip;
                _audioSource.Play();
            }
        }

        /// <summary>
        /// Stop the background music.
        /// </summary>
        public void Stop()
        {
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }
        }

        /// <summary>
        /// Pause the background music.
        /// </summary>
        public void Pause()
        {
            if (_audioSource != null)
            {
                _audioSource.Pause();
            }
        }

        /// <summary>
        /// Resume paused background music.
        /// </summary>
        public void Resume()
        {
            if (_audioSource != null)
            {
                _audioSource.UnPause();
            }
        }

        /// <summary>
        /// Fade the music volume over time.
        /// </summary>
        public void FadeVolume(float targetVolume, float duration)
        {
            if (_audioSource != null)
            {
                StartCoroutine(FadeVolumeCoroutine(targetVolume, duration));
            }
        }

        private System.Collections.IEnumerator FadeVolumeCoroutine(float targetVolume, float duration)
        {
            float startVolume = _audioSource.volume;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _audioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
                yield return null;
            }
            
            _audioSource.volume = targetVolume;
            volume = targetVolume;
        }

        /// <summary>
        /// Change to a new music track with optional crossfade.
        /// </summary>
        public void ChangeTrack(AudioClip newClip, float crossfadeDuration = 0.5f)
        {
            if (newClip == null || _audioSource == null) return;
            
            if (crossfadeDuration > 0 && _audioSource.isPlaying)
            {
                StartCoroutine(CrossfadeCoroutine(newClip, crossfadeDuration));
            }
            else
            {
                _audioSource.clip = newClip;
                _audioSource.Play();
            }
        }

        private System.Collections.IEnumerator CrossfadeCoroutine(AudioClip newClip, float duration)
        {
            // Fade out
            float originalVolume = volume;
            yield return FadeVolumeCoroutine(0f, duration / 2f);
            
            // Switch track
            _audioSource.clip = newClip;
            _audioSource.Play();
            
            // Fade in
            yield return FadeVolumeCoroutine(originalVolume, duration / 2f);
        }
    }
}
