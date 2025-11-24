using System;
using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// WordPromptManager - Manages endless prompts, local scoring, and bilingual UI feedback.
/// </summary>
public class WordPromptManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI phraseText;
    [SerializeField] private TMP_InputField guessInputField;
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI roundText;

    [Header("Result Panel")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultMessage;
    [SerializeField] private TextMeshProUGUI pointsEarnedText;
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private TextMeshProUGUI topWordsText; // Displays accepted answers list
    [SerializeField] private Button nextButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    private string defaultNextButtonLabel;

    [Header("Next Button Styling")]
    [SerializeField] private Color nextButtonNormalColor = new Color(0.2f, 0.75f, 0.2f);
    [SerializeField] private Color nextButtonRetryColor = new Color(0.8f, 0.25f, 0.25f);

    [Header("Loading Panel")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Hint System")]
    [SerializeField] private Button hintButton;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private int maxHints = 2;
    private int hintsUsed = 0;

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private PhraseResult currentPhrase;
    private float timeRemaining;
    private float roundTimeLimit;
    private bool roundActive = false;
    private bool hasAnswered = false;
    private bool isPaused = false;
    private bool gameOverTriggered = false;

    private void Start()
    {
        InitializeUI();
        StartNewRound();
    }

    private void Update()
    {
        if (isPaused)
            return;

        if (roundActive && !hasAnswered)
        {
            UpdateTimer();
        }
    }

    private void InitializeUI()
    {
        submitButton?.onClick.AddListener(OnSubmitGuess);
        nextButton?.onClick.AddListener(OnNextRound);
        menuButton?.onClick.AddListener(OnReturnToMenu);
        hintButton?.onClick.AddListener(OnRequestHint);
        pauseButton?.onClick.AddListener(TogglePause);
        resumeButton?.onClick.AddListener(TogglePause);

        resultPanel?.SetActive(false);
        loadingPanel?.SetActive(false);
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (hintText != null)
            hintText.gameObject.SetActive(false);

        var nextLabel = nextButton != null ? nextButton.GetComponentInChildren<TextMeshProUGUI>() : null;
        if (nextLabel != null)
            defaultNextButtonLabel = nextLabel.text;

        UpdateLevelInfo();
    }

    private void StartNewRound()
    {
        SetPaused(false);
        gameOverTriggered = false;
        hasAnswered = false;
        roundActive = false;
        hintsUsed = 0;
        roundTimeLimit = 0f;
        timeRemaining = 0f;

        if (hintText != null)
            hintText.gameObject.SetActive(false);

        if (hintButton != null)
            hintButton.interactable = true;

        resultPanel?.SetActive(false);
        if (nextButton != null)
        {
            nextButton.interactable = true;
            if (!string.IsNullOrEmpty(defaultNextButtonLabel))
                ConfigureNextButtonAppearance(defaultNextButtonLabel, nextButtonNormalColor);
        }

        if (topWordsText != null)
            topWordsText.text = "";

        if (guessInputField != null)
        {
            guessInputField.text = "";
            guessInputField.interactable = true;
        }

        if (submitButton != null)
            submitButton.interactable = true;

        if (feedbackText != null)
            feedbackText.text = "";

        UpdateLevelInfo();
        SetLoadingPanel(true, Localize("Generating clue...", "Generando pista..."));
        StartCoroutine(GenerateNewPhrase());
    }

    private IEnumerator GenerateNewPhrase()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager missing - cannot request phrase.");
            SetLoadingPanel(false, string.Empty);
            yield break;
        }

        int difficulty = GameManager.Instance.GetDifficultyTier();
        string languageCode = GameManager.Instance.GetLanguageCode();
        bool phraseReceived = false;

        APIManager.Instance.GeneratePhrase(difficulty, languageCode, result =>
        {
            currentPhrase = result;
            phraseReceived = true;

            if (phraseText != null && result != null)
                phraseText.text = result.phrase;
        });

        float timeout = 35f;
        float elapsed = 0f;
        while (!phraseReceived && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!phraseReceived || currentPhrase == null)
        {
            Debug.LogError("Failed to generate phrase - API timeout.");
            if (feedbackText != null)
                feedbackText.text = Localize("Error loading clue. Please try again.", "No se pudo cargar la pista. Intenta de nuevo.");

            SetLoadingPanel(false, string.Empty);
            yield break;
        }

        SetLoadingPanel(false, string.Empty);

        roundTimeLimit = GameManager.Instance.GetCurrentTimeLimit();
        timeRemaining = roundTimeLimit;
        roundActive = true;
        UpdateTimerLabel();
    }

    private void OnSubmitGuess()
    {
        if (!roundActive || hasAnswered || guessInputField == null || currentPhrase == null)
            return;

        string guess = guessInputField.text.Trim();
        if (string.IsNullOrEmpty(guess))
        {
            if (feedbackText != null)
                feedbackText.text = Localize("Please enter a word!", "¡Ingresa una palabra!");
            return;
        }

        hasAnswered = true;
        roundActive = false;

        if (submitButton != null)
            submitButton.interactable = false;

        if (guessInputField != null)
            guessInputField.interactable = false;

        float responseTime = Mathf.Clamp(roundTimeLimit - Mathf.Max(0f, timeRemaining), 0f, roundTimeLimit > 0f ? roundTimeLimit : 999f);
        float timeCap = roundTimeLimit > 0f ? roundTimeLimit : Mathf.Max(1f, responseTime);

        MatchResult matchResult = APIManager.Instance.EvaluateGuess(currentPhrase, guess, responseTime, timeCap);
        ProcessResult(matchResult, guess);
    }

    private void ProcessResult(MatchResult result, string guess)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterRound(result);

        if (feedbackText != null)
            feedbackText.text = result.message;

        if (result.matched)
            AudioManager.Instance?.PlayCorrectAnswer();
        else
            AudioManager.Instance?.PlayWrongAnswer();

        ShowResultPanel(result, guess);

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver())
        {
            HandleGameOverUI();
            return;
        }
    }

    private void ShowResultPanel(MatchResult result, string guess)
    {
        resultPanel?.SetActive(true);

        string displayedGuess = string.IsNullOrWhiteSpace(guess) ? Localize("No guess", "Sin respuesta") : guess;

        if (resultMessage != null)
        {
            if (result.matched)
            {
                resultMessage.text = $"{result.message}\n\n{Localize("You guessed", "Respondiste")}: <b>{displayedGuess}</b>";
            }
            else
            {
                resultMessage.text = $"{Localize("No match", "Sin coincidencia")}\n\n{Localize("You guessed", "Respondiste")}: <b>{displayedGuess}</b>\n{result.message}";
            }
        }

        if (pointsEarnedText != null)
        {
            pointsEarnedText.text = $"{Localize("Total", "Total")}: +{result.points} ({Localize("Base", "Base")} {result.basePoints} + {Localize("Speed", "Velocidad")} {result.speedBonus})";
        }

        if (totalScoreText != null && GameManager.Instance != null)
        {
            bool isNewRecord = GameManager.Instance.totalScore == GameManager.Instance.BestScore;
            string bestLine = isNewRecord
                ? Localize("New personal best!", "¡Nuevo récord personal!")
                : $"{Localize("Best", "Mejor")}: {GameManager.Instance.BestScore}";

            totalScoreText.text =
                $"{Localize("Total Score", "Puntaje total")}: {GameManager.Instance.totalScore}\n{bestLine}";
        }

        if (topWordsText != null && result.acceptedWords != null && result.acceptedWords.Length > 0)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"<b>{Localize("Accepted answers", "Respuestas válidas")}:</b>\n");

            for (int i = 0; i < result.acceptedWords.Length; i++)
            {
                string entry = result.acceptedWords[i];
                bool highlight = !string.IsNullOrWhiteSpace(guess) &&
                                 entry.Equals(guess, StringComparison.OrdinalIgnoreCase);

                if (highlight)
                    builder.AppendLine($"{i + 1}. <color=yellow><b>{entry}</b></color>");
                else
                    builder.AppendLine($"{i + 1}. {entry}");
            }

            topWordsText.text = builder.ToString();
        }
        else if (topWordsText != null)
        {
            topWordsText.text = "";
        }

        UpdateScoreDisplay();
    }

    private void OnNextRound()
    {
        AudioManager.Instance?.PlayButtonClick();
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver())
        {
            GameManager.Instance.ResetGame();
            StartNewRound();
            return;
        }

        if (!roundActive && !isPaused)
        {
            StartNewRound();
        }
    }

    private void OnReturnToMenu()
    {
        AudioManager.Instance?.PlayButtonClick();
        ReturnToMainMenu();
    }

    private void OnRequestHint()
    {
        if (hintsUsed >= maxHints || !roundActive || hasAnswered || currentPhrase == null)
            return;

        hintsUsed++;

        if (hintText != null)
        {
            hintText.gameObject.SetActive(true);
            string hintLabel = Localize("Hint", "Pista");

            if (hintsUsed == 1 && !string.IsNullOrEmpty(currentPhrase.hint))
            {
                hintText.text = $"{hintLabel}: {currentPhrase.hint}";
            }
            else if (hintsUsed == 1)
            {
                int length = string.IsNullOrEmpty(currentPhrase.targetWord) ? 0 : currentPhrase.targetWord.Length;
                hintText.text = $"{hintLabel}: {Localize($"The word has {length} letters", $"La palabra tiene {length} letras")}";
            }
            else
            {
                char firstLetter = (!string.IsNullOrEmpty(currentPhrase.targetWord) ? char.ToUpper(currentPhrase.targetWord[0]) : '?');
                hintText.text = $"{hintLabel}: {Localize($"It starts with '{firstLetter}'", $"Empieza con '{firstLetter}'")}";
            }
        }

        if (hintsUsed >= maxHints && hintButton != null)
            hintButton.interactable = false;

        AudioManager.Instance?.PlayButtonClick();
    }

    private void UpdateTimer()
    {
        timeRemaining -= Time.deltaTime;
        UpdateTimerLabel();

        if (timeRemaining <= 0f)
        {
            OnTimeUp();
        }
    }

    private void OnTimeUp()
    {
        roundActive = false;
        hasAnswered = true;

        if (submitButton != null)
            submitButton.interactable = false;

        if (guessInputField != null)
            guessInputField.interactable = false;

        if (feedbackText != null)
            feedbackText.text = Localize("Time's up!", "¡Se acabó el tiempo!");

        AudioManager.Instance?.PlayWrongAnswer();

        MatchResult timeoutResult = APIManager.Instance.BuildTimeoutResult(currentPhrase, roundTimeLimit);
        ProcessResult(timeoutResult, string.Empty);
    }

    private void UpdateLevelInfo()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager.Instance is null - cannot update level info");
            return;
        }

        if (levelText != null)
            levelText.text = $"{Localize("Difficulty", "Dificultad")}: {GameManager.Instance.GetDifficultyLabel()}";

        if (roundText != null)
            roundText.text = $"{Localize("Round", "Ronda")}: {GameManager.Instance.roundsCompleted + 1}";

        UpdateScoreDisplay();
    }

    private void UpdateScoreDisplay()
    {
        if (GameManager.Instance == null)
            return;

        if (scoreText != null)
        {
            scoreText.text =
                $"{Localize("Score", "Puntaje")}: {GameManager.Instance.totalScore}\n" +
                $"{Localize("Best", "Mejor")}: {GameManager.Instance.BestScore}\n" +
                $"{Localize("Rounds", "Rondas")}: {GameManager.Instance.roundsCompleted}\n" +
                $"{Localize("Lives", "Vidas")}: {GameManager.Instance.livesRemaining}";
        }
    }

    private void HandleGameOverUI()
    {
        if (gameOverTriggered)
            return;

        gameOverTriggered = true;
        SetPaused(false);
        roundActive = false;
        hasAnswered = true;

        if (pauseButton != null)
            pauseButton.gameObject.SetActive(false);

        if (resumeButton != null)
            resumeButton.gameObject.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (resultMessage != null)
        {
            resultMessage.text = $"{Localize("Game Over", "Juego terminado")}\n{Localize("You ran out of lives.", "Te quedaste sin vidas.")}";
        }

        if (feedbackText != null)
            feedbackText.text = Localize("No lives remaining. Tap Retry to play again or Menu to exit.", "Sin vidas. Presiona Reintentar para jugar de nuevo o Menú para salir.");

        if (nextButton != null)
        {
            nextButton.interactable = true;
            ConfigureNextButtonAppearance(Localize("Retry", "Reintentar"), nextButtonRetryColor);
        }
    }

    private void TogglePause()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver())
            return;

        SetPaused(!isPaused);
    }

    private void SetPaused(bool paused)
    {
        if (isPaused == paused)
            return;

        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pausePanel != null)
            pausePanel.SetActive(paused);

        if (resumeButton != null)
            resumeButton.gameObject.SetActive(paused);

        if (pauseButton != null)
            pauseButton.gameObject.SetActive(!paused);

        bool canInteract = !paused && roundActive && !hasAnswered;

        if (guessInputField != null)
            guessInputField.interactable = canInteract;

        if (submitButton != null)
            submitButton.interactable = canInteract;

        if (hintButton != null)
            hintButton.interactable = canInteract && hintsUsed < maxHints;

        if (!paused)
            UpdateTimerLabel();
    }

    private void OnDisable()
    {
        if (isPaused)
            SetPaused(false);
    }

    private void SetLoadingPanel(bool active, string message)
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(active);

        if (loadingText != null)
            loadingText.text = message;
    }

    private void UpdateTimerLabel()
    {
        if (timerText == null)
            return;

        int seconds = Mathf.Max(0, Mathf.CeilToInt(timeRemaining));
        timerText.text = $"{Localize("Time", "Tiempo")}: {seconds}s";
        timerText.color = timeRemaining < 10f ? Color.red : Color.white;
    }

    private string Localize(string english, string spanish)
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentLanguage == GameLanguage.Spanish
            ? spanish
            : english;
    }

    private void ConfigureNextButtonAppearance(string label, Color tint)
    {
        if (nextButton == null)
            return;

        var labelComponent = nextButton.GetComponentInChildren<TextMeshProUGUI>();
        if (labelComponent != null && !string.IsNullOrEmpty(label))
            labelComponent.text = label;

        var colors = nextButton.colors;
        colors.normalColor = tint;
        colors.highlightedColor = tint;
        colors.selectedColor = tint;
        colors.pressedColor = tint * 0.9f;
        nextButton.colors = colors;

        if (nextButton.targetGraphic != null)
            nextButton.targetGraphic.color = tint;
    }

    private void ReturnToMainMenu()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("WordPromptManager: Main menu scene name is not set.");
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}

