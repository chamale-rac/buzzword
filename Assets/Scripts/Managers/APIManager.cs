using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// APIManager - Handles all API requests to Gemini and Datamuse
/// Includes offline fallback data
/// </summary>
public class APIManager : MonoBehaviour
{
    public static APIManager Instance { get; private set; }

    [Header("API Configuration")]
    [SerializeField] private string geminiApiKey = "YOUR_GEMINI_API_KEY_HERE";
    private const string GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent";
    private const string DATAMUSE_ENDPOINT = "https://api.datamuse.com/words";

    [Header("Offline Mode")]
    [SerializeField] private bool useOfflineMode = false;
    [SerializeField] private TextAsset offlinePromptsJson;

    private OfflineData offlineData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadOfflineData();
            Debug.Log("APIManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadOfflineData()
    {
        if (offlinePromptsJson != null)
        {
            try
            {
                offlineData = JsonUtility.FromJson<OfflineData>(offlinePromptsJson.text);
                Debug.Log("Offline data loaded successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load offline data: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Generate a phrase for a given difficulty level using Gemini API
    /// </summary>
    public IEnumerator GeneratePhrase(int difficulty, Action<PhraseResult> callback)
    {
        if (useOfflineMode || string.IsNullOrEmpty(geminiApiKey) || geminiApiKey == "YOUR_GEMINI_API_KEY_HERE")
        {
            Debug.Log("Using offline mode for phrase generation");
            yield return GetOfflinePhrase(difficulty, callback);
            yield break;
        }

        string prompt = GetPromptForDifficulty(difficulty);
        string requestBody = CreateGeminiRequestBody(prompt);

        bool shouldFallback = false;

        using (UnityWebRequest request = new UnityWebRequest(GEMINI_ENDPOINT, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", geminiApiKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                    string phraseText = response.candidates[0].content.parts[0].text;
                    
                    // Extract target word and phrase from Gemini response
                    PhraseResult result = ParseGeminiResponse(phraseText);
                    callback?.Invoke(result);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing Gemini response: {e.Message}");
                    shouldFallback = true;
                }
            }
            else
            {
                Debug.LogError($"Gemini API Error: {request.error}");
                shouldFallback = true;
            }
        }

        if (shouldFallback)
        {
            yield return GetOfflinePhrase(difficulty, callback);
        }
    }

    /// <summary>
    /// Check if a guessed word matches the target phrase using Datamuse API
    /// </summary>
    public IEnumerator CheckWordMatch(string phrase, string guessedWord, Action<MatchResult> callback)
    {
        string url = $"{DATAMUSE_ENDPOINT}?ml={UnityWebRequest.EscapeURL(phrase)}&max=50";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    DatamuseWord[] words = JsonHelper.FromJson<DatamuseWord>(request.downloadHandler.text);
                    MatchResult result = EvaluateMatch(words, guessedWord);
                    callback?.Invoke(result);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing Datamuse response: {e.Message}");
                    callback?.Invoke(new MatchResult { matched = false, score = 0, message = "API Error" });
                }
            }
            else
            {
                Debug.LogError($"Datamuse API Error: {request.error}");
                callback?.Invoke(new MatchResult { matched = false, score = 0, message = "Network Error" });
            }
        }
    }

    private IEnumerator GetOfflinePhrase(int difficulty, Action<PhraseResult> callback)
    {
        if (offlineData != null && offlineData.prompts != null && offlineData.prompts.Length > 0)
        {
            // Filter prompts by difficulty
            List<PromptData> filteredPrompts = new List<PromptData>();
            foreach (var prompt in offlineData.prompts)
            {
                if (prompt.difficulty == difficulty)
                    filteredPrompts.Add(prompt);
            }

            if (filteredPrompts.Count > 0)
            {
                PromptData selected = filteredPrompts[UnityEngine.Random.Range(0, filteredPrompts.Count)];
                callback?.Invoke(new PhraseResult
                {
                    phrase = selected.phrase,
                    targetWord = selected.targetWord,
                    difficulty = difficulty
                });
            }
            else
            {
                Debug.LogWarning($"No offline prompts found for difficulty {difficulty}");
                callback?.Invoke(GetDefaultPhrase(difficulty));
            }
        }
        else
        {
            callback?.Invoke(GetDefaultPhrase(difficulty));
        }
        yield return null;
    }

    private PhraseResult GetDefaultPhrase(int difficulty)
    {
        switch (difficulty)
        {
            case 1:
                return new PhraseResult { phrase = "A domestic animal that purrs and meows", targetWord = "cat", difficulty = 1 };
            case 2:
                return new PhraseResult { phrase = "A constant ringing sound in your ears", targetWord = "tinnitus", difficulty = 2 };
            case 3:
                return new PhraseResult { phrase = "The fear of long words", targetWord = "hippopotomonstrosesquippedaliophobia", difficulty = 3 };
            default:
                return new PhraseResult { phrase = "A round object used in sports", targetWord = "ball", difficulty = 1 };
        }
    }

    private string GetPromptForDifficulty(int difficulty)
    {
        switch (difficulty)
        {
            case 1:
                return "Generate a simple, natural language phrase that describes a common word without saying the word itself. The word should be something most people know. Respond in this exact JSON format: {\"targetWord\": \"word\", \"phrase\": \"description\"}";
            case 2:
                return "Generate a moderately challenging natural language phrase that describes a less common word without saying the word itself. Respond in this exact JSON format: {\"targetWord\": \"word\", \"phrase\": \"description\"}";
            case 3:
                return "Generate a difficult natural language phrase that describes an uncommon or technical word without saying the word itself. Respond in this exact JSON format: {\"targetWord\": \"word\", \"phrase\": \"description\"}";
            default:
                return "Generate a natural language phrase that describes a word without saying the word itself. Respond in this exact JSON format: {\"targetWord\": \"word\", \"phrase\": \"description\"}";
        }
    }

    private string CreateGeminiRequestBody(string prompt)
    {
        return $@"{{
            ""contents"": [{{
                ""parts"": [{{
                    ""text"": ""{prompt.Replace("\"", "\\\"")}""
                }}]
            }}],
            ""generationConfig"": {{
                ""responseMimeType"": ""application/json""
            }}
        }}";
    }

    private PhraseResult ParseGeminiResponse(string responseText)
    {
        try
        {
            // Try to parse as JSON first
            GeminiPhraseData data = JsonUtility.FromJson<GeminiPhraseData>(responseText);
            return new PhraseResult
            {
                phrase = data.phrase,
                targetWord = data.targetWord,
                difficulty = GameManager.Instance.currentLevel
            };
        }
        catch
        {
            // If not JSON, treat entire response as phrase
            return new PhraseResult
            {
                phrase = responseText,
                targetWord = "unknown",
                difficulty = GameManager.Instance.currentLevel
            };
        }
    }

    private MatchResult EvaluateMatch(DatamuseWord[] words, string guessedWord)
    {
        if (words == null || words.Length == 0)
        {
            return new MatchResult
            {
                matched = false,
                score = 0,
                points = 0,
                message = "No matches found"
            };
        }

        guessedWord = guessedWord.ToLower().Trim();
        int topScore = words[0].score;

        foreach (var word in words)
        {
            if (word.word.ToLower() == guessedWord)
            {
                // Calculate points: (guessScore / topScore) * 100
                int points = Mathf.RoundToInt((float)word.score / topScore * 100);
                
                string rank = GetRankForPercentage(points);
                
                return new MatchResult
                {
                    matched = true,
                    score = word.score,
                    points = points,
                    rank = rank,
                    message = $"{rank} Match! +{points} points"
                };
            }
        }

        return new MatchResult
        {
            matched = false,
            score = 0,
            points = 0,
            message = "No match found. Try again!"
        };
    }

    private string GetRankForPercentage(int percentage)
    {
        if (percentage >= 95) return "Perfect";
        if (percentage >= 80) return "Excellent";
        if (percentage >= 60) return "Great";
        if (percentage >= 40) return "Good";
        if (percentage >= 20) return "Fair";
        return "Close";
    }
}

// Data structures for API responses
[System.Serializable]
public class GeminiResponse
{
    public GeminiCandidate[] candidates;
}

[System.Serializable]
public class GeminiCandidate
{
    public GeminiContent content;
}

[System.Serializable]
public class GeminiContent
{
    public GeminiPart[] parts;
}

[System.Serializable]
public class GeminiPart
{
    public string text;
}

[System.Serializable]
public class GeminiPhraseData
{
    public string targetWord;
    public string phrase;
}

[System.Serializable]
public class DatamuseWord
{
    public string word;
    public int score;
    public string[] tags;
}

[System.Serializable]
public class PhraseResult
{
    public string phrase;
    public string targetWord;
    public int difficulty;
}

[System.Serializable]
public class MatchResult
{
    public bool matched;
    public int score;
    public int points;
    public string rank;
    public string message;
}

[System.Serializable]
public class OfflineData
{
    public PromptData[] prompts;
}

[System.Serializable]
public class PromptData
{
    public string phrase;
    public string targetWord;
    public int difficulty;
}

// Helper class for JSON array deserialization
public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        string wrappedJson = "{\"array\":" + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrappedJson);
        return wrapper.array;
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] array;
    }
}

