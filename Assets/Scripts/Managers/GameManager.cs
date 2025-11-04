using UnityEngine;

/// <summary>
/// GameManager - Singleton that tracks game state, current level, and total score
/// Persists across scenes using DontDestroyOnLoad
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public int currentLevel = 1;
    public int totalScore = 0;
    public int currentRoundScore = 0;
    
    [Header("Level Configuration")]
    public int maxLevels = 3;
    public int roundsPerLevel = 3;
    public int currentRound = 0;

    [Header("Difficulty Settings")]
    public float level1TimeLimit = 60f;
    public float level2TimeLimit = 45f;
    public float level3TimeLimit = 30f;

    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("GameManager initialized");
        }
        else
        {
            Destroy(gameObject);
            Debug.Log("Duplicate GameManager destroyed");
        }
    }

    /// <summary>
    /// Add score from current round
    /// </summary>
    public void AddScore(int points)
    {
        currentRoundScore = points;
        totalScore += points;
        Debug.Log($"Score added: {points}. Total score: {totalScore}");
    }

    /// <summary>
    /// Proceed to the next level
    /// </summary>
    public void NextLevel()
    {
        currentRound = 0;
        currentLevel++;
        
        if (currentLevel > maxLevels)
        {
            Debug.Log("All levels completed!");
            currentLevel = maxLevels;
        }
        
        Debug.Log($"Advanced to Level {currentLevel}");
    }

    /// <summary>
    /// Proceed to next round in current level
    /// </summary>
    public void NextRound()
    {
        currentRound++;
        Debug.Log($"Advanced to Round {currentRound} of Level {currentLevel}");
    }

    /// <summary>
    /// Reset the game state
    /// </summary>
    public void ResetGame()
    {
        currentLevel = 1;
        totalScore = 0;
        currentRoundScore = 0;
        currentRound = 0;
        Debug.Log("Game reset");
    }

    /// <summary>
    /// Get time limit for current level
    /// </summary>
    public float GetCurrentTimeLimit()
    {
        switch (currentLevel)
        {
            case 1: return level1TimeLimit;
            case 2: return level2TimeLimit;
            case 3: return level3TimeLimit;
            default: return level1TimeLimit;
        }
    }

    /// <summary>
    /// Check if current level is complete
    /// </summary>
    public bool IsLevelComplete()
    {
        return currentRound >= roundsPerLevel;
    }

    /// <summary>
    /// Check if all levels are complete
    /// </summary>
    public bool IsGameComplete()
    {
        return currentLevel >= maxLevels && IsLevelComplete();
    }
}

