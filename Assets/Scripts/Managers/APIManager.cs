using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// APIManager - Handles Gemini structured responses, offline fallbacks, and local scoring logic.
/// </summary>
public class APIManager : MonoBehaviour
{
    public static APIManager Instance { get; private set; }

    [Header("API Configuration")]
    [SerializeField] private string geminiApiKey = "YOUR_GEMINI_API_KEY_HERE";
    [SerializeField] private string geminiModel = "gemini-2.0-flash-exp";

    private string GeminiEndpoint => $"https://generativelanguage.googleapis.com/v1beta/models/{geminiModel}:generateContent";

    [Header("Offline Mode")]
    [SerializeField] private bool useOfflineMode = false;
    [SerializeField] private TextAsset offlinePromptsJson;

    [Header("Phrase History")]
    [SerializeField] private int maxHistory = 20;

    private OfflineData offlineData;
    private readonly List<string> usedPhrases = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplySecretOverrides();
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
        if (offlinePromptsJson == null) return;

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

    private void ApplySecretOverrides()
    {
        SecretsPayload secrets = SecretManager.Secrets;
        if (secrets != null && !string.IsNullOrEmpty(secrets.geminiApiKey))
        {
            geminiApiKey = secrets.geminiApiKey;
        }
    }

    public void GeneratePhrase(int difficultyTier, string languageCode, Action<PhraseResult> callback)
    {
        StartCoroutine(GeneratePhraseCoroutine(Mathf.Max(1, difficultyTier), NormalizeLanguage(languageCode), callback));
    }

    private IEnumerator GeneratePhraseCoroutine(int difficultyTier, string languageCode, Action<PhraseResult> callback)
    {
        if (useOfflineMode || string.IsNullOrEmpty(geminiApiKey) || geminiApiKey == "YOUR_GEMINI_API_KEY_HERE")
        {
            Debug.Log("Using offline mode for phrase generation");
            yield return GetOfflinePhrase(difficultyTier, languageCode, callback);
            yield break;
        }

        bool shouldFallback = false;
        string prompt = BuildPrompt(difficultyTier, languageCode);
        string requestBody = CreateGeminiRequestBody(prompt);

        using (UnityWebRequest request = new UnityWebRequest(GeminiEndpoint, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", geminiApiKey);
            request.timeout = 30;

            Debug.Log($"Sending request to Gemini API for difficulty {difficultyTier} ({languageCode})...");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);

                    if (response?.candidates != null &&
                        response.candidates.Length > 0 &&
                        response.candidates[0].content?.parts != null &&
                        response.candidates[0].content.parts.Length > 0)
                    {
                        string payload = response.candidates[0].content.parts[0].text;
                        PhraseResult result = ParseGeminiResponse(payload, difficultyTier, languageCode);
                        callback?.Invoke(result);
                        Debug.Log($"Phrase generated: {result.phrase} -> {result.targetWord} ({languageCode})");
                    }
                    else
                    {
                        Debug.LogError("Gemini response missing content");
                        shouldFallback = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing Gemini response: {ex.Message}\nPayload: {request.downloadHandler.text}");
                    shouldFallback = true;
                }
            }
            else
            {
                Debug.LogError($"Gemini API Error: {request.error}\nResponse Code: {request.responseCode}");
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Debug.LogError($"Body: {request.downloadHandler.text}");
                }
                shouldFallback = true;
            }
        }

        if (shouldFallback)
        {
            Debug.Log("Falling back to offline phrase generation");
            yield return GetOfflinePhrase(difficultyTier, languageCode, callback);
        }
    }

    public MatchResult EvaluateGuess(PhraseResult phrase, string guess, float responseTime, float roundTimeLimit)
    {
        if (phrase == null)
        {
            return new MatchResult
            {
                matched = false,
                positionIndex = -1,
                basePoints = 0,
                speedBonus = 0,
                points = 0,
                responseTime = responseTime,
                matchedWord = string.Empty,
                message = "No phrase available",
                acceptedWords = Array.Empty<string>()
            };
        }

        string[] answers = phrase.acceptedWords != null && phrase.acceptedWords.Length > 0
            ? phrase.acceptedWords
            : new[] { phrase.targetWord };

        string normalizedGuess = NormalizeWord(guess);
        int positionIndex = -1;

        for (int i = 0; i < answers.Length; i++)
        {
            if (NormalizeWord(answers[i]) == normalizedGuess && !string.IsNullOrEmpty(normalizedGuess))
            {
                positionIndex = i;
                break;
            }
        }

        int basePoints = CalculateBasePoints(positionIndex);
        int speedBonus = positionIndex >= 0 ? CalculateSpeedBonus(responseTime, roundTimeLimit) : 0;
        int totalPoints = Mathf.Max(0, basePoints + speedBonus);

        return new MatchResult
        {
            matched = positionIndex >= 0,
            positionIndex = positionIndex,
            basePoints = basePoints,
            speedBonus = speedBonus,
            points = totalPoints,
            responseTime = Mathf.Clamp(responseTime, 0f, roundTimeLimit),
            matchedWord = positionIndex >= 0 ? answers[positionIndex] : guess,
            message = BuildMatchMessage(positionIndex, phrase.languageCode),
            acceptedWords = answers
        };
    }

    public MatchResult BuildTimeoutResult(PhraseResult phrase, float roundTimeLimit)
    {
        string[] answers = phrase?.acceptedWords ?? Array.Empty<string>();
        bool spanish = IsSpanish(phrase?.languageCode);
        return new MatchResult
        {
            matched = false,
            positionIndex = -1,
            basePoints = 0,
            speedBonus = 0,
            points = 0,
            responseTime = roundTimeLimit,
            matchedWord = string.Empty,
            message = spanish ? "¡Se acabó el tiempo!" : "Time ran out!",
            acceptedWords = answers
        };
    }

    private IEnumerator GetOfflinePhrase(int difficultyTier, string languageCode, Action<PhraseResult> callback)
    {
        PhraseResult result = null;

        if (offlineData?.prompts != null && offlineData.prompts.Length > 0)
        {
            List<PromptData> filtered = new();
            foreach (PromptData prompt in offlineData.prompts)
            {
                if (prompt.difficulty == difficultyTier &&
                    NormalizeLanguage(prompt.language) == languageCode)
                {
                    filtered.Add(prompt);
                }
            }

            if (filtered.Count > 0)
            {
                PromptData selected = filtered[UnityEngine.Random.Range(0, filtered.Count)];
                List<string> answers = ComposeAnswerList(selected.targetWord, selected.acceptedWords);
                result = new PhraseResult
                {
                    phrase = selected.phrase,
                    targetWord = selected.targetWord,
                    acceptedWords = answers.ToArray(),
                    languageCode = languageCode,
                    difficulty = difficultyTier,
                    hint = selected.hint
                };
            }
        }

        if (result == null)
        {
            Debug.LogWarning($"No offline prompts found for difficulty {difficultyTier} ({languageCode}). Using default fallback.");
            result = GetDefaultPhrase(difficultyTier, languageCode);
        }

        callback?.Invoke(result);
        yield return null;
    }

    private PhraseResult GetDefaultPhrase(int difficultyTier, string languageCode)
    {
        bool spanish = IsSpanish(languageCode);
        int bucket = Mathf.Clamp(difficultyTier, 1, 6);

        string phrase;
        string target;
        string hint;
        string[] additionalAnswers;

        if (!spanish)
        {
            if (bucket <= 2)
            {
                phrase = "A fluffy pet that purrs when you scratch its chin";
                target = "cat";
                hint = "House companion that chases laser dots";
                additionalAnswers = new[] { "kitty", "feline", "housecat", "pet cat" };
            }
            else if (bucket <= 4)
            {
                phrase = "A word that reads the same forward and backward";
                target = "palindrome";
                hint = "Mirror-friendly vocabulary";
                additionalAnswers = new[] { "mirrorword", "symmetrical word", "reversible word" };
            }
            else
            {
                phrase = "A dramatic fear sparked by towering, endless words";
                target = "hippopotomonstrosesquippedaliophobia";
                hint = "Ironically, it's the fear of long words";
                additionalAnswers = new[] { "fear of long words", "longwordphobia", "sesquipedalophobia" };
            }
        }
        else
        {
            if (bucket <= 2)
            {
                phrase = "Un felino doméstico que maúlla y adora las siestas al sol";
                target = "gato";
                hint = "Animal compañero que ronronea";
                additionalAnswers = new[] { "gatito", "felino", "minino" };
            }
            else if (bucket <= 4)
            {
                phrase = "Una palabra que se lee igual de izquierda a derecha";
                target = "palíndromo";
                hint = "Ejemplo: reconocer";
                additionalAnswers = new[] { "palindromo", "palabra simétrica", "palabra espejo" };
            }
            else
            {
                phrase = "Un miedo exagerado a palabras larguísimas y complicadas";
                target = "hipopotomonstrosesquipedaliofobia";
                hint = "Sí, describe temor a palabras largas";
                additionalAnswers = new[] { "miedo a palabras largas", "sesquipedaliofobia" };
            }
        }

        List<string> answers = ComposeAnswerList(target, additionalAnswers);
        return new PhraseResult
        {
            phrase = phrase,
            targetWord = target,
            acceptedWords = answers.ToArray(),
            languageCode = languageCode,
            difficulty = difficultyTier,
            hint = hint
        };
    }

    private PhraseResult ParseGeminiResponse(string responseText, int difficultyTier, string languageFallback)
    {
        try
        {
            GeminiPhraseData data = JsonUtility.FromJson<GeminiPhraseData>(responseText);
            if (data == null || string.IsNullOrEmpty(data.phrase) || string.IsNullOrEmpty(data.targetWord))
            {
                throw new Exception("Structured response missing required fields");
            }

            AddPhraseToHistory(data.phrase);

            List<string> answers = ComposeAnswerList(data.targetWord, data.synonyms);
            string lang = string.IsNullOrEmpty(data.languageCode) ? languageFallback : NormalizeLanguage(data.languageCode);

            return new PhraseResult
            {
                phrase = data.phrase,
                targetWord = data.targetWord,
                acceptedWords = answers.ToArray(),
                languageCode = lang,
                difficulty = difficultyTier,
                hint = data.hint
            };
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Gemini response fallback triggered: {ex.Message}");
            AddPhraseToHistory(responseText);

            return new PhraseResult
            {
                phrase = responseText,
                targetWord = "unknown",
                acceptedWords = new[] { "unknown" },
                languageCode = languageFallback,
                difficulty = difficultyTier,
                hint = string.Empty
            };
        }
    }

    private void AddPhraseToHistory(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return;

        usedPhrases.Add(phrase);
        if (usedPhrases.Count > maxHistory)
        {
            usedPhrases.RemoveAt(0);
        }
    }

    private List<string> ComposeAnswerList(string primary, string[] additional)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> answers = new();

        void TryAdd(string value)
        {
            string normalized = NormalizeWord(value);
            if (string.IsNullOrEmpty(normalized) || seen.Contains(normalized))
                return;

            seen.Add(normalized);
            answers.Add(value.Trim());
        }

        TryAdd(primary);

        if (additional != null)
        {
            foreach (string word in additional)
            {
                TryAdd(word);
            }
        }

        if (answers.Count == 0)
        {
            answers.Add("unknown");
        }

        return answers;
    }

    private int CalculateBasePoints(int positionIndex)
    {
        if (positionIndex < 0)
            return 0;

        switch (positionIndex)
        {
            case 0: return 120;
            case 1: return 105;
            case 2: return 90;
            case 3: return 75;
            case 4: return 65;
            default:
                int deduction = (positionIndex - 4) * 5;
                return Mathf.Max(30, 65 - deduction);
        }
    }

    private int CalculateSpeedBonus(float responseTime, float roundTimeLimit)
    {
        if (roundTimeLimit <= 0f) return 0;

        float remaining = Mathf.Max(0f, roundTimeLimit - responseTime);
        float ratio = Mathf.Clamp01(remaining / roundTimeLimit);
        return Mathf.RoundToInt(40f * ratio);
    }

    private string BuildMatchMessage(int positionIndex, string languageCode)
    {
        bool spanish = IsSpanish(languageCode);

        if (positionIndex < 0)
        {
            return spanish ? "Sin coincidencias." : "No match.";
        }

        int place = positionIndex + 1;
        return spanish ? $"¡Coincidencia #{place}!" : $"Match #{place}!";
    }

    private string BuildPrompt(int difficultyTier, string languageCode)
    {
        string lang = NormalizeLanguage(languageCode);
        string difficultyDescription = DescribeDifficulty(difficultyTier, lang);
        string languageName = GetLanguageName(lang);
        string nativeLabel = GetNativeLanguageName(lang);
        string[] contexts = GetContextOptions(lang);
        string context = contexts[UnityEngine.Random.Range(0, contexts.Length)];
        string avoid = BuildAvoidInstruction();

        float creativity = GameManager.Instance != null ? GameManager.Instance.GetDifficultyMultiplier() : 1f;
        string creativityNote = creativity > 1.5f
            ? "Use layered metaphors and vivid imagery."
            : "Keep wording approachable and playful.";

        return $"You provide structured data for an endless word-guessing video game. " +
               $"All text (phrase, hint, accepted words) must be written in {languageName} ({nativeLabel}). " +
               $"Difficulty tier {difficultyTier} means {difficultyDescription}. {creativityNote} " +
               $"Theme focus: {context}. Create a single clue phrase that never states the answer directly. " +
               $"Return between 4 and 6 accepted answers: the canonical solution first, followed by likely synonyms, all lowercase and punctuation-free. " +
               "Include a concise hint (<=80 characters). " +
               $"Set languageCode to \"{lang}\". {avoid}";
    }

    private string DescribeDifficulty(int tier, string languageCode)
    {
        bool spanish = IsSpanish(languageCode);

        if (tier <= 1)
            return spanish ? "vocabulario cotidiano muy sencillo" : "very common everyday vocabulary";
        if (tier == 2)
            return spanish ? "palabras comunes con un toque creativo" : "routine words with a clever twist";
        if (tier == 3)
            return spanish ? "conceptos menos frecuentes que requieren contexto" : "less frequent concepts that need context";
        if (tier == 4)
            return spanish ? "terminología especializada o cultural" : "specialized or cultural terminology";

        return spanish ? "palabras raras y retadoras que exigen deducción" : "rare, demanding words that require deduction";
    }

    private string[] GetContextOptions(string languageCode)
    {
        return IsSpanish(languageCode)
            ? new[]
            {
                "describir un objeto cotidiano",
                "describir una profesión interesante",
                "describir comida o bebida",
                "describir fenómenos naturales",
                "describir tecnología o ciencia"
            }
            : new[]
            {
                "describing a clever invention",
                "describing a cultural tradition",
                "describing a scientific concept",
                "describing a place or landmark",
                "describing a tactile sensation"
            };
    }

    private string BuildAvoidInstruction()
    {
        if (usedPhrases.Count == 0) return string.Empty;

        int takeCount = Mathf.Min(5, usedPhrases.Count);
        List<string> recent = usedPhrases.GetRange(usedPhrases.Count - takeCount, takeCount);
        return $"Avoid repeating phrases similar to: {string.Join("; ", recent)}.";
    }

    private string CreateGeminiRequestBody(string prompt)
    {
        string safePrompt = EscapeForJson(prompt);
        return $@"{{
    ""contents"": [{{
        ""parts"": [{{
            ""text"": ""{safePrompt}""
        }}]
    }}],
    ""generationConfig"": {{
        ""responseMimeType"": ""application/json"",
        ""responseSchema"": {{
            ""type"": ""OBJECT"",
            ""properties"": {{
                ""languageCode"": {{ ""type"": ""STRING"" }},
                ""phrase"": {{ ""type"": ""STRING"" }},
                ""targetWord"": {{ ""type"": ""STRING"" }},
                ""synonyms"": {{
                    ""type"": ""ARRAY"",
                    ""items"": {{ ""type"": ""STRING"" }}
                }},
                ""hint"": {{ ""type"": ""STRING"" }}
            }},
            ""required"": [""languageCode"", ""phrase"", ""targetWord"", ""synonyms""]
        }},
        ""temperature"": 0.95,
        ""topP"": 0.95,
        ""topK"": 40,
        ""maxOutputTokens"": 256,
    }}
}}";
    }

    private string EscapeForJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private string NormalizeLanguage(string code)
    {
        return string.Equals(code, "es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";
    }

    private static string NormalizeWord(string word)
    {
        return string.IsNullOrWhiteSpace(word) ? string.Empty : word.Trim().ToLowerInvariant();
    }

    private bool IsSpanish(string code)
    {
        return string.Equals(code, "es", StringComparison.OrdinalIgnoreCase);
    }

    private string GetLanguageName(string code)
    {
        return IsSpanish(code) ? "Spanish" : "English";
    }

    private string GetNativeLanguageName(string code)
    {
        return IsSpanish(code) ? "español" : "English";
    }
}

#region Data Structures

[Serializable]
public class GeminiResponse
{
    public GeminiCandidate[] candidates;
}

[Serializable]
public class GeminiCandidate
{
    public GeminiContent content;
}

[Serializable]
public class GeminiContent
{
    public GeminiPart[] parts;
}

[Serializable]
public class GeminiPart
{
    public string text;
}

[Serializable]
public class GeminiPhraseData
{
    public string languageCode;
    public string phrase;
    public string targetWord;
    public string[] synonyms;
    public string hint;
}

[Serializable]
public class PhraseResult
{
    public string phrase;
    public string targetWord;
    public string[] acceptedWords;
    public string languageCode;
    public int difficulty;
    public string hint;
}

[Serializable]
public class MatchResult
{
    public bool matched;
    public int positionIndex;
    public int basePoints;
    public int speedBonus;
    public int points;
    public float responseTime;
    public string matchedWord;
    public string message;
    public string[] acceptedWords;
}

[Serializable]
public class OfflineData
{
    public PromptData[] prompts;
}

[Serializable]
public class PromptData
{
    public string phrase;
    public string targetWord;
    public string[] acceptedWords;
    public string hint;
    public string language;
    public int difficulty;
}

#endregion
