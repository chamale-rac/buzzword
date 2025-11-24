using UnityEngine;

public enum GameLanguage
{
    English,
    Spanish
}

/// <summary>
/// GameManager - Singleton that tracks endless game state, scoring, difficulty, and language
/// Persists across scenes using DontDestroyOnLoad
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public int totalScore = 0;
    public int currentRoundScore = 0;
    public int roundsCompleted = 0;
    public int livesRemaining = 10;
    public int BestScore => bestScore;

    [Header("Endless Mode Configuration")]
    [SerializeField] private float baseTimeLimit = 55f;
    [SerializeField] private float minTimeLimit = 18f;
    [SerializeField] private float timeDecayPerRound = 1.5f;
    [SerializeField] private int roundsPerDifficultyStep = 4;
    [SerializeField] private int maxDifficultyTier = 6;
    [SerializeField] private float difficultyRamp = 0.07f;

[Header("Life Settings")]
[SerializeField] private int startingLives = 10;
[SerializeField] private int livesLostOnFail = 3;
[SerializeField] private int livesGainedOnSuccess = 1;

[Header("Language Settings")]
[SerializeField] private GameLanguage startingLanguage = GameLanguage.English;
public GameLanguage CurrentLanguage { get; private set; }

    private int bestScore = 0;

    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CurrentLanguage = startingLanguage;
            bestScore = 0;
            Debug.Log($"GameManager initialized (Language: {CurrentLanguage})");
        }
        else
        {
            Destroy(gameObject);
            Debug.Log("Duplicate GameManager destroyed");
        }
    }

    /// <summary>
    /// Reset the game state
    /// </summary>
    public void ResetGame()
    {
        totalScore = 0;
        currentRoundScore = 0;
        roundsCompleted = 0;
        livesRemaining = startingLives;
        Debug.Log("Game reset");
    }

    /// <summary>
    /// Register the outcome of a completed round
    /// </summary>
    public void RegisterRound(MatchResult result)
    {
        if (result == null) return;

        currentRoundScore = Mathf.Max(0, result.points);
        totalScore += currentRoundScore;
        roundsCompleted++;

        if (result.matched)
            livesRemaining = Mathf.Min(livesRemaining + livesGainedOnSuccess, startingLives);
        else
            livesRemaining = Mathf.Max(0, livesRemaining - livesLostOnFail);

        TryUpdateBestScore(totalScore);
        Debug.Log($"Round registered. Points: {currentRoundScore}. Total score: {totalScore}. Rounds: {roundsCompleted}. Lives: {livesRemaining}");
    }

    /// <summary>
    /// Get the dynamic difficulty tier derived from rounds completed
    /// </summary>
    public int GetDifficultyTier()
    {
        int step = Mathf.Max(1, roundsPerDifficultyStep);
        int tier = 1 + Mathf.FloorToInt((float)roundsCompleted / step);
        return Mathf.Clamp(tier, 1, maxDifficultyTier);
    }

    /// <summary>
    /// Provide a friendly label for the current difficulty tier
    /// </summary>
    public string GetDifficultyLabel()
    {
        int tier = GetDifficultyTier();
        bool spanish = CurrentLanguage == GameLanguage.Spanish;

        return tier switch
        {
            1 => spanish ? "Calentamiento" : "Warm-up",
            2 => spanish ? "Analítico" : "Thinker",
            3 => spanish ? "Genio" : "Brainiac",
            4 => spanish ? "Artífice verbal" : "Wordsmith",
            5 => spanish ? "Maestro" : "Mastermind",
            _ => spanish ? "Leyenda" : "Legend"
        };
    }

    /// <summary>
    /// Returns the current timer cap adjusted by progression.
    /// </summary>
    public float GetCurrentTimeLimit()
    {
        float adjusted = baseTimeLimit - roundsCompleted * timeDecayPerRound;
        return Mathf.Clamp(adjusted, minTimeLimit, baseTimeLimit);
    }

    /// <summary>
    /// Value between 1-? that increases steadily and can be used by API prompts.
    /// </summary>
    public float GetDifficultyMultiplier()
    {
        return 1f + roundsCompleted * difficultyRamp;
    }

    public void SetLanguage(GameLanguage language)
    {
        CurrentLanguage = language;
        Debug.Log($"Language set to {language}");
    }

    public string GetLanguageCode()
    {
        return CurrentLanguage == GameLanguage.Spanish ? "es" : "en";
    }

    public string GetLanguageDisplayName()
    {
        return CurrentLanguage == GameLanguage.Spanish ? "Español" : "English";
    }

    public bool IsGameOver()
    {
        return livesRemaining <= 0;
    }

    private void TryUpdateBestScore(int candidateScore)
    {
        if (candidateScore <= bestScore)
            return;

        bestScore = candidateScore;
        LeaderboardSyncManager.Instance?.ReportScore(bestScore);
    }
}

