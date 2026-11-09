using System.Collections.Generic;
using UnityEngine;

namespace Ggj2026.Game.Shell.Audio
{
    public enum ColonySoundId
    {
        ConstructionComplete,
        ConstructionPlace,
        WorkerAssigned,
        RaidWarning,
        RaidResolved,
        MilestoneFanfare,
        UiClick,
        UiHover,
        SelectionChanged,
        ErrorBlocked,
        OnboardingStep
    }

    public sealed class ColonyAudioService : MonoBehaviour
    {
        private const int kSampleRate = 44100;

        private AudioSource _sfxSource;
        private AudioSource _ambientSource;
        private Dictionary<ColonySoundId, AudioClip> _clips;
        private AudioClip _ambientClip;

        public static ColonyAudioService Active { get; private set; }

        public float SfxVolume
        {
            get => _sfxSource != null ? _sfxSource.volume : 0.6f;
            set { if (_sfxSource != null) _sfxSource.volume = Mathf.Clamp01(value); }
        }

        public float AmbientVolume
        {
            get => _ambientSource != null ? _ambientSource.volume : 0.15f;
            set { if (_ambientSource != null) _ambientSource.volume = Mathf.Clamp01(value); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Active != null)
            {
                return;
            }

            var go = new GameObject("ColonyAudioService");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            Active = go.AddComponent<ColonyAudioService>();
        }

        public void PlaySound(ColonySoundId soundId)
        {
            if (_sfxSource == null || _clips == null)
            {
                return;
            }

            if (_clips.TryGetValue(soundId, out AudioClip clip))
            {
                _sfxSource.PlayOneShot(clip);
            }
        }

        public void PlayAmbient()
        {
            if (_ambientSource == null || _ambientClip == null)
            {
                return;
            }

            _ambientSource.clip = _ambientClip;
            if (!_ambientSource.isPlaying)
            {
                _ambientSource.Play();
            }
        }

        public void StopAmbient()
        {
            if (_ambientSource != null)
            {
                _ambientSource.Stop();
            }
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = volume;
        }

        public void SetAmbientVolume(float volume)
        {
            AmbientVolume = volume;
        }

        private void Awake()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.spatialBlend = 0f;
            _sfxSource.volume = 0.6f;
            _sfxSource.playOnAwake = false;

            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.spatialBlend = 0f;
            _ambientSource.volume = 0.15f;
            _ambientSource.loop = true;
            _ambientSource.playOnAwake = false;

            GenerateClips();
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void GenerateClips()
        {
            _clips = new Dictionary<ColonySoundId, AudioClip>
            {
                { ColonySoundId.ConstructionComplete, GenerateTone(440f, 0.15f) },
                { ColonySoundId.ConstructionPlace, GenerateTone(330f, 0.15f) },
                { ColonySoundId.WorkerAssigned, GenerateTone(500f, 0.12f) },
                { ColonySoundId.RaidWarning, GenerateTone(150f, 0.3f, 0.1f) },
                { ColonySoundId.RaidResolved, GenerateTone(440f, 0.2f) },
                { ColonySoundId.MilestoneFanfare, GenerateAscendingTone(523f, 659f, 0.3f) },
                { ColonySoundId.UiClick, GenerateTone(880f, 0.05f, 0.02f) },
                { ColonySoundId.UiHover, GenerateTone(700f, 0.04f, 0.02f) },
                { ColonySoundId.SelectionChanged, GenerateTone(600f, 0.08f, 0.03f) },
                { ColonySoundId.ErrorBlocked, GenerateDescendingTone(300f, 180f, 0.15f) },
                { ColonySoundId.OnboardingStep, GenerateTone(550f, 0.1f, 0.03f) }
            };

            // Seamless loop: 60 Hz at 44100 sample rate gives exactly 735 samples/cycle.
            // 2 seconds = 88200 samples = 120 complete cycles, so no click on loop boundary.
            _ambientClip = GenerateAmbientLoop(60f, 2f);
        }

        private static AudioClip GenerateTone(float frequency, float duration, float fadeOut = 0.05f)
        {
            int sampleCount = Mathf.CeilToInt(kSampleRate * duration);
            var samples = new float[sampleCount];
            int fadeOutSamples = Mathf.Max(1, Mathf.CeilToInt(kSampleRate * fadeOut));

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / kSampleRate;
                float amplitude = 0.5f;
                int remaining = sampleCount - i;
                if (remaining < fadeOutSamples)
                {
                    amplitude *= (float)remaining / fadeOutSamples;
                }

                samples[i] = amplitude * Mathf.Sin(2f * Mathf.PI * frequency * t);
            }

            var clip = AudioClip.Create($"Tone_{frequency:0}Hz", sampleCount, 1, kSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateAscendingTone(float startFreq, float endFreq, float duration)
        {
            int sampleCount = Mathf.CeilToInt(kSampleRate * duration);
            var samples = new float[sampleCount];
            int half = sampleCount / 2;
            int crossfadeSamples = Mathf.Max(1, Mathf.CeilToInt(kSampleRate * 0.03f));

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / kSampleRate;
                float freq = i < half ? startFreq : endFreq;
                float amplitude = 0.5f;

                if (i < half)
                {
                    int remaining = half - i;
                    if (remaining < crossfadeSamples)
                    {
                        amplitude *= (float)remaining / crossfadeSamples;
                    }
                }
                else
                {
                    int offset = i - half;
                    if (offset < crossfadeSamples)
                    {
                        amplitude *= (float)offset / crossfadeSamples;
                    }

                    int remaining = sampleCount - i;
                    if (remaining < crossfadeSamples)
                    {
                        amplitude *= (float)remaining / crossfadeSamples;
                    }
                }

                samples[i] = amplitude * Mathf.Sin(2f * Mathf.PI * freq * t);
            }

            var clip = AudioClip.Create("Fanfare", sampleCount, 1, kSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateDescendingTone(float startFreq, float endFreq, float duration)
        {
            int sampleCount = Mathf.CeilToInt(kSampleRate * duration);
            var samples = new float[sampleCount];
            int fadeOutSamples = Mathf.Max(1, Mathf.CeilToInt(kSampleRate * 0.04f));

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / kSampleRate;
                float progress = (float)i / sampleCount;
                float freq = Mathf.Lerp(startFreq, endFreq, progress);
                float amplitude = 0.5f;
                int remaining = sampleCount - i;
                if (remaining < fadeOutSamples)
                {
                    amplitude *= (float)remaining / fadeOutSamples;
                }

                samples[i] = amplitude * Mathf.Sin(2f * Mathf.PI * freq * t);
            }

            var clip = AudioClip.Create("Descend", sampleCount, 1, kSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateAmbientLoop(float frequency, float duration)
        {
            int samplesPerCycle = Mathf.RoundToInt(kSampleRate / frequency);
            int totalSamples = Mathf.CeilToInt(kSampleRate * duration);
            int cycles = Mathf.Max(1, totalSamples / samplesPerCycle);
            totalSamples = cycles * samplesPerCycle;

            var samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / kSampleRate;
                samples[i] = 0.5f * Mathf.Sin(2f * Mathf.PI * frequency * t);
            }

            var clip = AudioClip.Create("AmbientHum", totalSamples, 1, kSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
