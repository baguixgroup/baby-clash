using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Plays music and sound effects. Renders all placeholder audio once at startup, keeps a small pool of
    /// voices so overlapping effects can each have their own pitch, and follows the volume settings.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const int VoiceCount = 8;
        const float MusicFadeSpeed = 1.5f;

        [SerializeField] AudioSource musicSource;

        [Tooltip("Scales every sound effect on top of the player's volume setting.")]
        [Range(0f, 1f)] [SerializeField] float effectsMix = 0.8f;

        [Tooltip("Scales music on top of the player's volume setting.")]
        [Range(0f, 1f)] [SerializeField] float musicMix = 0.6f;

        AudioSource[] voices;
        int nextVoice;
        AudioClip[] effects;
        AudioClip titleTheme;
        AudioClip matchTheme;
        MusicId currentMusic = MusicId.None;
        float musicFade = 1f;
        float musicTarget = 1f;

        /// <summary>Scene wiring (editor tooling).</summary>
        public void Configure(AudioSource music) => musicSource = music;

        void Awake()
        {
            effects = SoundBank.CreateEffects();
            titleTheme = SoundBank.CreateTitleTheme();
            matchTheme = SoundBank.CreateMatchTheme();

            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
            if (musicSource != null)
            {
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.spatialBlend = 0f;
            }
        }

        void OnEnable() => GameSettings.Changed += ApplyMusicVolume;

        void OnDisable() => GameSettings.Changed -= ApplyMusicVolume;

        void OnDestroy()
        {
            if (effects != null)
            {
                foreach (AudioClip clip in effects)
                    Destroy(clip);
            }
            Destroy(titleTheme);
            Destroy(matchTheme);
        }

        public void Play(SfxId id, float pitch = 1f, float volume = 1f)
        {
            AudioClip clip = effects[(int)id];
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume * effectsMix * GameSettings.SfxVolume);
        }

        public void PlayMusic(MusicId id)
        {
            if (musicSource == null || id == currentMusic)
                return;
            currentMusic = id;
            musicSource.Stop();
            musicSource.clip = id == MusicId.Title ? titleTheme : id == MusicId.Match ? matchTheme : null;
            musicFade = 0f;
            if (musicSource.clip != null)
                musicSource.Play();
            ApplyMusicVolume();
        }

        /// <summary>Temporarily lowers the music (e.g. under a victory fanfare or the pause menu).</summary>
        public void DuckMusic(bool duck) => musicTarget = duck ? 0.35f : 1f;

        void Update()
        {
            if (musicSource == null)
                return;
            musicFade = Mathf.MoveTowards(musicFade, musicTarget, MusicFadeSpeed * Time.unscaledDeltaTime);
            ApplyMusicVolume();
        }

        void ApplyMusicVolume()
        {
            if (musicSource != null)
                musicSource.volume = GameSettings.MusicVolume * musicMix * musicFade;
        }
    }
}
