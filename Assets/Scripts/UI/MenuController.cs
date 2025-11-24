using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MenuController - Handles main menu UI interactions
/// </summary>
public class MenuController : MonoBehaviour
{
    public static MenuController Instance { get; private set; }

    [Header("Main Menu Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject levelSelectPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Language / Mode Selection")]
    [SerializeField] private Button level1Button; // Repurposed as English button
    [SerializeField] private Button level2Button; // Repurposed as Spanish button
    [SerializeField] private Button level3Button; // Hidden in endless mode
    [SerializeField] private Button backFromLevelSelectButton;

    [Header("Settings")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Button backFromSettingsButton;

    [Header("Credits")]
    [SerializeField] private Button backFromCreditsButton;

    [Header("Score Display")]
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private LeaderboardPanelController leaderboardPanel;

    [Header("Loading Overlay")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingPanelMessage;
    [SerializeField, Tooltip("Seconds the loading overlay stays visible.")]
    private float loadingPanelDuration = 1f;

    [Header("Title Animation")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private float titleAnimationSpeed = 1f;

    [Header("Scene Settings")]
    [SerializeField] private string endlessSceneName = "Level1";

    private Coroutine loadingPanelRoutine;
    private Coroutine waitForPlayFabRoutine;
    private bool playFabReady;
    private bool leaderboardReady;
    private bool playFabSubscribed;
    private float loadingPanelStartTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple MenuController instances detected. Overwriting reference.");
        }

        Instance = this;
    }

    private void Start()
    {
        if (leaderboardPanel == null)
            leaderboardPanel = FindFirstObjectByType<LeaderboardPanelController>(FindObjectsInactive.Include);


        // Show a random loading phrase each time
        string[] loadingPhrases = new[]
        {
            "Loading leaderboard...",
            "Fetching top scores...",
            "Let the numbers roll...",
            "Summoning the champions...",
            "Cracking open the high scores...",
            "Gathering leaderboard legends...",
            "Loading... Success is in reach!",
            "Who will top the list?",
            "Getting the best of the best...",
            "Good things come to those who wait..."
        };
        string chosenPhrase = loadingPhrases[UnityEngine.Random.Range(0, loadingPhrases.Length)];
        ShowLoadingPanel(chosenPhrase);
        SubscribeToPlayFabSignals();

        InitializeMenu();
        ConfigureLanguageButtons();
        SetupButtonListeners();
        UpdateScoreDisplay();
        leaderboardPanel?.ForceImmediateRefresh();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuMusic();
    }

    private void Update()
    {
        AnimateTitle();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnsubscribeFromPlayFabSignals();
        StopLoadingPanelCoroutines();
    }

    private void InitializeMenu()
    {
        // Show only main panel at start
        if (mainPanel != null)
            mainPanel.SetActive(true);

        if (levelSelectPanel != null)
            levelSelectPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        // Initialize volume sliders
        if (AudioManager.Instance != null)
        {
            if (musicVolumeSlider != null)
                musicVolumeSlider.value = AudioManager.Instance.GetMusicVolume();

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.value = AudioManager.Instance.GetSFXVolume();
        }
    }

    private void SetupButtonListeners()
    {
        // Main menu buttons
        if (startButton != null)
            startButton.onClick.AddListener(OnStartGame);

        if (levelSelectButton != null)
        {
            levelSelectButton.onClick.AddListener(OnLanguageSelect);
            SetButtonLabel(levelSelectButton, "Language");
        }

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettings);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCredits);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuit);

        // Level select buttons
        if (level1Button != null)
            level1Button.onClick.AddListener(() => OnSelectLanguage(GameLanguage.English));

        if (level2Button != null)
            level2Button.onClick.AddListener(() => OnSelectLanguage(GameLanguage.Spanish));

        if (level3Button != null)
            level3Button.gameObject.SetActive(false);

        if (backFromLevelSelectButton != null)
            backFromLevelSelectButton.onClick.AddListener(OnBackToMain);

        // Settings
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.AddListener(OnBackToMain);

        // Credits
        if (backFromCreditsButton != null)
            backFromCreditsButton.onClick.AddListener(OnBackToMain);
    }

    private void OnStartGame()
    {
        PlayButtonClick();
        
        // Reset game and start from level 1
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGame();
        }

        LoadSceneSafe(endlessSceneName);
    }

    private void OnLanguageSelect()
    {
        PlayButtonClick();
        
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (levelSelectPanel != null)
            levelSelectPanel.SetActive(true);

        ConfigureLanguageButtons();
    }

    private void OnSettings()
    {
        PlayButtonClick();
        
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    private void OnCredits()
    {
        PlayButtonClick();
        
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(true);
    }

    private void OnQuit()
    {
        PlayButtonClick();
        
        QuitApplication();
    }

    private void OnBackToMain()
    {
        PlayButtonClick();
        
        if (mainPanel != null)
            mainPanel.SetActive(true);

        if (levelSelectPanel != null)
            levelSelectPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        ConfigureLanguageButtons();
    }

    private void OnSelectLanguage(GameLanguage language)
    {
        PlayButtonClick();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetLanguage(language);
            UpdateScoreDisplay();
        }

        ConfigureLanguageButtons();
    }

    private void ConfigureLanguageButtons()
    {
        if (level1Button != null)
            SetButtonLabel(level1Button, BuildLanguageLabel(GameLanguage.English));

        if (level2Button != null)
            SetButtonLabel(level2Button, BuildLanguageLabel(GameLanguage.Spanish));
    }

    private string BuildLanguageLabel(GameLanguage language)
    {
        if (GameManager.Instance == null)
            return language == GameLanguage.Spanish ? "Español" : "English";

        bool isActive = GameManager.Instance.CurrentLanguage == language;
        string baseText = language == GameLanguage.Spanish ? "Español" : "English";
        return isActive ? $"(x) {baseText}" : $"(-) {baseText}";
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;

        var text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = label;
        }
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
    }

    private void UpdateScoreDisplay()
    {
        if (totalScoreText != null && GameManager.Instance != null)
        {
            totalScoreText.text =
                $"Last Run: {GameManager.Instance.totalScore}\n" +
                $"Language: {GameManager.Instance.GetLanguageDisplayName()}";
        }
    }

    private void AnimateTitle()
    {
        if (titleText != null)
        {
            // Simple pulse animation
            float scale = 1f + Mathf.Sin(Time.time * titleAnimationSpeed) * 0.05f;
            titleText.transform.localScale = Vector3.one * scale;
        }
    }

    private void PlayButtonClick()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
    }

    private void LoadSceneSafe(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("MenuController: Scene name not configured for scene load.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void QuitApplication()
    {
        Debug.Log("Quitting game...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void ShowLoadingPanel(string message = null)
    {
        if (loadingPanel == null)
            return;

        loadingPanelStartTime = Time.unscaledTime;
        loadingPanel.SetActive(true);

        if (loadingPanelMessage != null)
            loadingPanelMessage.text = string.IsNullOrEmpty(message) ? "Loading..." : message;
    }

    private IEnumerator HideLoadingPanelAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        HideLoadingPanelImmediate();
    }

    private void HideLoadingPanelImmediate()
    {
        if (loadingPanelRoutine != null)
        {
            StopCoroutine(loadingPanelRoutine);
            loadingPanelRoutine = null;
        }

        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }

    private void SubscribeToPlayFabSignals()
    {
        if (playFabSubscribed)
            return;

        var manager = PlayFabManager.Instance;
        if (manager == null)
        {
            if (waitForPlayFabRoutine == null)
                waitForPlayFabRoutine = StartCoroutine(WaitForPlayFabAndSubscribe());
            return;
        }

        manager.OnLoginSucceeded += HandlePlayFabLoginSucceeded;
        manager.OnLoginFailed += HandlePlayFabLoginFailed;
        manager.OnLeaderboardUpdated += HandlePlayFabLeaderboardUpdated;
        playFabSubscribed = true;

        playFabReady = manager.IsLoggedIn;
        leaderboardReady = manager.CachedLeaderboard != null && manager.CachedLeaderboard.Count > 0;
        TryHideLoadingPanelWhenReady();
    }

    private IEnumerator WaitForPlayFabAndSubscribe()
    {
        while (PlayFabManager.Instance == null)
            yield return null;

        waitForPlayFabRoutine = null;
        SubscribeToPlayFabSignals();
    }

    private void UnsubscribeFromPlayFabSignals()
    {
        if (!playFabSubscribed)
            return;

        var manager = PlayFabManager.Instance;
        if (manager != null)
        {
            manager.OnLoginSucceeded -= HandlePlayFabLoginSucceeded;
            manager.OnLoginFailed -= HandlePlayFabLoginFailed;
            manager.OnLeaderboardUpdated -= HandlePlayFabLeaderboardUpdated;
        }

        playFabSubscribed = false;
    }

    private void HandlePlayFabLoginSucceeded(PlayFabLoginResult _)
    {
        playFabReady = true;
        TryHideLoadingPanelWhenReady();
    }

    private void HandlePlayFabLoginFailed(string _)
    {
        playFabReady = true;
        TryHideLoadingPanelWhenReady();
    }

    private void HandlePlayFabLeaderboardUpdated(IReadOnlyList<LeaderboardEntry> entries, LeaderboardEntry playerEntry)
    {
        leaderboardReady = true;
        TryHideLoadingPanelWhenReady();
    }

    private void TryHideLoadingPanelWhenReady()
    {
        if (!playFabReady || !leaderboardReady || loadingPanel == null)
            return;

        if (loadingPanelRoutine != null)
        {
            StopCoroutine(loadingPanelRoutine);
            loadingPanelRoutine = null;
        }

        float elapsed = Time.unscaledTime - loadingPanelStartTime;
        float delay = Mathf.Max(0f, loadingPanelDuration - elapsed);
        loadingPanelRoutine = StartCoroutine(HideLoadingPanelAfterDelay(delay));
    }

    private void StopLoadingPanelCoroutines()
    {
        if (loadingPanelRoutine != null)
        {
            StopCoroutine(loadingPanelRoutine);
            loadingPanelRoutine = null;
        }

        if (waitForPlayFabRoutine != null)
        {
            StopCoroutine(waitForPlayFabRoutine);
            waitForPlayFabRoutine = null;
        }
    }
}

