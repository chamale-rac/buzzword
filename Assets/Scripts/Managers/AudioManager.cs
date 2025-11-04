using UnityEngine;

/// <summary>
/// AudioManager - Controls global sounds and music
/// Persists across scenes
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music Clips")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameplayMusic;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip buttonClickSfx;
    [SerializeField] private AudioClip correctAnswerSfx;
    [SerializeField] private AudioClip wrongAnswerSfx;
    [SerializeField] private AudioClip levelCompleteSfx;
    [SerializeField] private AudioClip typingSfx;

    [Header("Settings")]
    [SerializeField] private float musicVolume = 0.5f;
    [SerializeField] private float sfxVolume = 0.7f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            Debug.Log("AudioManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudioSources()
    {
        // Create audio sources if they don't exist
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("MusicSource");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFXSource");
            sfxObj.transform.SetParent(transform);
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        UpdateVolume();
    }

    /// <summary>
    /// Play menu music
    /// </summary>
    public void PlayMenuMusic()
    {
        PlayMusic(menuMusic);
    }

    /// <summary>
    /// Play gameplay music
    /// </summary>
    public void PlayGameplayMusic()
    {
        PlayMusic(gameplayMusic);
    }

    /// <summary>
    /// Play a music clip
    /// </summary>
    private void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;

        if (musicSource.clip == clip && musicSource.isPlaying)
            return;

        musicSource.clip = clip;
        musicSource.Play();
    }

    /// <summary>
    /// Stop music
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    /// <summary>
    /// Play button click sound
    /// </summary>
    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSfx);
    }

    /// <summary>
    /// Play correct answer sound
    /// </summary>
    public void PlayCorrectAnswer()
    {
        PlaySFX(correctAnswerSfx);
    }

    /// <summary>
    /// Play wrong answer sound
    /// </summary>
    public void PlayWrongAnswer()
    {
        PlaySFX(wrongAnswerSfx);
    }

    /// <summary>
    /// Play level complete sound
    /// </summary>
    public void PlayLevelComplete()
    {
        PlaySFX(levelCompleteSfx);
    }

    /// <summary>
    /// Play typing sound
    /// </summary>
    public void PlayTyping()
    {
        PlaySFX(typingSfx);
    }

    /// <summary>
    /// Play a sound effect
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// Set music volume
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
            musicSource.volume = musicVolume;
    }

    /// <summary>
    /// Set SFX volume
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    /// <summary>
    /// Update volumes
    /// </summary>
    private void UpdateVolume()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume;
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    /// <summary>
    /// Toggle music mute
    /// </summary>
    public void ToggleMusicMute()
    {
        if (musicSource != null)
            musicSource.mute = !musicSource.mute;
    }

    /// <summary>
    /// Toggle SFX mute
    /// </summary>
    public void ToggleSFXMute()
    {
        if (sfxSource != null)
            sfxSource.mute = !sfxSource.mute;
    }

    /// <summary>
    /// Get music volume
    /// </summary>
    public float GetMusicVolume()
    {
        return musicVolume;
    }

    /// <summary>
    /// Get SFX volume
    /// </summary>
    public float GetSFXVolume()
    {
        return sfxVolume;
    }
}

