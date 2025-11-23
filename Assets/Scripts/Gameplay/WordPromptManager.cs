using System;
using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;
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

    [Header("Loading Panel")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Hint System")]
    [SerializeField] private Button hintButton;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private int maxHints = 2;
    private int hintsUsed = 0;

    private PhraseResult currentPhrase;
    private float timeRemaining;
    private float roundTimeLimit;
    private bool roundActive = false;
    private bool hasAnswered = false;

    private void Start()
    {
        InitializeUI();
        StartNewRound();
    }

    private void Update()
    {
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

        resultPanel?.SetActive(false);
        loadingPanel?.SetActive(false);

        if (hintText != null)
            hintText.gameObject.SetActive(false);

        UpdateLevelInfo();
    }

    private void StartNewRound()
    {
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
            totalScoreText.text = $"{Localize("Total Score", "Puntaje total")}: {GameManager.Instance.totalScore}";
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
        StartNewRound();
    }

    private void OnReturnToMenu()
    {
        AudioManager.Instance?.PlayButtonClick();
        SceneLoader.Instance?.LoadMainMenu();
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
            scoreText.text = $"{Localize("Score", "Puntaje")}: {GameManager.Instance.totalScore}\n{Localize("Rounds", "Rondas")}: {GameManager.Instance.roundsCompleted}";
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
}

