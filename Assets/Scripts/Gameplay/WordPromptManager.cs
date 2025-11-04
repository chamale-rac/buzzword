using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// WordPromptManager - Manages word prompts, guessing, and scoring for gameplay
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
        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitGuess);

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextRound);

        if (menuButton != null)
            menuButton.onClick.AddListener(OnReturnToMenu);

        if (hintButton != null)
            hintButton.onClick.AddListener(OnRequestHint);

        if (resultPanel != null)
            resultPanel.SetActive(false);

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        if (hintText != null)
            hintText.gameObject.SetActive(false);

        UpdateLevelInfo();
    }

    private void StartNewRound()
    {
        hasAnswered = false;
        hintsUsed = 0;
        
        if (hintText != null)
            hintText.gameObject.SetActive(false);

        if (hintButton != null)
            hintButton.interactable = true;

        if (resultPanel != null)
            resultPanel.SetActive(false);

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
        
        // Generate new phrase
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            if (loadingText != null)
                loadingText.text = "Generating phrase...";
        }

        StartCoroutine(GenerateNewPhrase());
    }

    private IEnumerator GenerateNewPhrase()
    {
        int difficulty = GameManager.Instance.currentLevel;
        bool phraseReceived = false;

        APIManager.Instance.GeneratePhrase(difficulty, (result) =>
        {
            currentPhrase = result;
            phraseReceived = true;

            if (phraseText != null)
                phraseText.text = result.phrase;

            Debug.Log($"Phrase generated: {result.phrase} (Target: {result.targetWord})");
        });

        // Wait for phrase to be received
        float timeout = 10f;
        float elapsed = 0f;
        while (!phraseReceived && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!phraseReceived)
        {
            Debug.LogError("Failed to generate phrase - timeout");
            if (feedbackText != null)
                feedbackText.text = "Error loading phrase. Please try again.";
        }

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        // Start timer
        timeRemaining = GameManager.Instance.GetCurrentTimeLimit();
        roundActive = true;
    }

    private void OnSubmitGuess()
    {
        if (!roundActive || hasAnswered || guessInputField == null) return;

        string guess = guessInputField.text.Trim();

        if (string.IsNullOrEmpty(guess))
        {
            if (feedbackText != null)
                feedbackText.text = "Please enter a word!";
            return;
        }

        hasAnswered = true;
        roundActive = false;

        if (submitButton != null)
            submitButton.interactable = false;

        if (guessInputField != null)
            guessInputField.interactable = false;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            if (loadingText != null)
                loadingText.text = "Checking answer...";
        }

        StartCoroutine(CheckGuess(guess));
    }

    private IEnumerator CheckGuess(string guess)
    {
        bool resultReceived = false;
        MatchResult matchResult = null;

        APIManager.Instance.CheckWordMatch(currentPhrase.phrase, guess, (result) =>
        {
            matchResult = result;
            resultReceived = true;
        });

        // Wait for result
        float timeout = 10f;
        float elapsed = 0f;
        while (!resultReceived && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        if (resultReceived && matchResult != null)
        {
            ProcessResult(matchResult, guess);
        }
        else
        {
            if (feedbackText != null)
                feedbackText.text = "Error checking answer. Please try again.";
        }
    }

    private void ProcessResult(MatchResult result, string guess)
    {
        // Add score
        GameManager.Instance.AddScore(result.points);

        // Play sound
        if (result.matched)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayCorrectAnswer();
        }
        else
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayWrongAnswer();
        }

        // Show result panel
        ShowResultPanel(result, guess);
    }

    private void ShowResultPanel(MatchResult result, string guess)
    {
        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (resultMessage != null)
        {
            if (result.matched)
            {
                resultMessage.text = $"{result.rank} Match!\n\nYou guessed: <b>{guess}</b>\nTarget word: <b>{currentPhrase.targetWord}</b>";
            }
            else
            {
                resultMessage.text = $"No Match\n\nYou guessed: <b>{guess}</b>\nTarget word: <b>{currentPhrase.targetWord}</b>\n\n{result.message}";
            }
        }

        if (pointsEarnedText != null)
            pointsEarnedText.text = $"+{result.points} points";

        if (totalScoreText != null)
            totalScoreText.text = $"Total Score: {GameManager.Instance.totalScore}";

        UpdateScoreDisplay();
    }

    private void OnNextRound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        GameManager.Instance.NextRound();

        if (GameManager.Instance.IsLevelComplete())
        {
            OnLevelComplete();
        }
        else
        {
            StartNewRound();
        }
    }

    private void OnLevelComplete()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLevelComplete();

        if (GameManager.Instance.IsGameComplete())
        {
            // All levels complete - return to menu
            OnReturnToMenu();
        }
        else
        {
            // Load next level
            GameManager.Instance.NextLevel();
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadLevel(GameManager.Instance.currentLevel);
        }
    }

    private void OnReturnToMenu()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadMainMenu();
    }

    private void OnRequestHint()
    {
        if (hintsUsed >= maxHints || !roundActive || hasAnswered) return;

        hintsUsed++;

        if (hintText != null)
        {
            hintText.gameObject.SetActive(true);
            
            if (hintsUsed == 1)
            {
                // First hint: show word length
                hintText.text = $"Hint: The word has {currentPhrase.targetWord.Length} letters";
            }
            else if (hintsUsed == 2)
            {
                // Second hint: show first letter
                hintText.text = $"Hint: The word starts with '{currentPhrase.targetWord[0].ToString().ToUpper()}'";
            }
        }

        if (hintsUsed >= maxHints && hintButton != null)
        {
            hintButton.interactable = false;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
    }

    private void UpdateTimer()
    {
        timeRemaining -= Time.deltaTime;

        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(timeRemaining);
            timerText.text = $"Time: {seconds}s";

            // Color warning
            if (timeRemaining < 10f)
                timerText.color = Color.red;
            else
                timerText.color = Color.white;
        }

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
            feedbackText.text = "Time's up!";

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayWrongAnswer();

        // Show result with 0 points
        MatchResult timeoutResult = new MatchResult
        {
            matched = false,
            points = 0,
            message = "Time ran out!"
        };

        ShowResultPanel(timeoutResult, "");
    }

    private void UpdateLevelInfo()
    {
        if (levelText != null)
            levelText.text = $"Level {GameManager.Instance.currentLevel}";

        if (roundText != null)
            roundText.text = $"Round {GameManager.Instance.currentRound + 1}/{GameManager.Instance.roundsPerLevel}";

        UpdateScoreDisplay();
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {GameManager.Instance.totalScore}";
    }
}

