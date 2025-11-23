using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MenuController - Handles main menu UI interactions
/// </summary>
public class MenuController : MonoBehaviour
{
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

    [Header("Title Animation")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private float titleAnimationSpeed = 1f;

    private void Start()
    {
        InitializeMenu();
        ConfigureLanguageButtons();
        SetupButtonListeners();
        UpdateScoreDisplay();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuMusic();
    }

    private void Update()
    {
        AnimateTitle();
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

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadEndlessMode();
        }
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
        
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.QuitGame();
        }
        else
        {
            Application.Quit();
        }
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
        return isActive ? $"{baseText} ✓" : baseText;
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
            totalScoreText.text = $"High Score: {GameManager.Instance.totalScore}\nLanguage: {GameManager.Instance.GetLanguageDisplayName()}";
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
}

