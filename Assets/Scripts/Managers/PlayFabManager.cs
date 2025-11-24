using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class LeaderboardEntry
{
    public string displayName;
    public int position;
    public int score;
    public bool isLocalPlayer;
}

public class PlayFabManager : MonoBehaviour
{
    public static PlayFabManager Instance { get; private set; }

    [Header("PlayFab Settings")]
    [SerializeField] private string fallbackTitleId = string.Empty;
    [SerializeField] private string statisticName = "HighScore";
    [SerializeField, Tooltip("If true, the manager will try to login automatically on Start using a device identifier.")]
    private bool autoLoginOnStart = true;
    [SerializeField, Tooltip("How many leaderboard entries to fetch for the global table.")]
    private int leaderboardEntryCount = 10;

    // Runtime events consumed by UI/other systems.
    public event Action<PlayFabLoginResult> OnLoginSucceeded;
    public event Action<string> OnLoginFailed;
    public event Action<IReadOnlyList<LeaderboardEntry>, LeaderboardEntry> OnLeaderboardUpdated;

    private string titleId;
    private string sessionTicket;
    private string playFabId;
    private string displayName;
    private bool isLoggingIn;
    private int lastUploadedScore = -1;
    private int pendingScore = -1;
    private readonly List<LeaderboardEntry> leaderboardCache = new();
    private PlayFabLoginResult lastLoginResult;
    private LeaderboardEntry lastPlayerEntry;

    private const string PlayerPrefsCustomIdKey = "Buzzword_PlayFab_CustomId";

    private string BaseUrl => $"https://{titleId.ToLowerInvariant()}.playfabapi.com";

    public bool IsLoggedIn => !string.IsNullOrEmpty(sessionTicket);
    public string DisplayName => displayName;
    public IReadOnlyList<LeaderboardEntry> CachedLeaderboard => leaderboardCache;
    public PlayFabLoginResult LastLoginResult => lastLoginResult;
    public LeaderboardEntry LastPlayerEntry => lastPlayerEntry;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Initialize();
        LeaderboardSyncManager.Instance?.RegisterPlayFabManager(this);
    }

    private void Start()
    {
        if (autoLoginOnStart && !IsLoggedIn)
        {
            LoginWithDeviceId();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        LeaderboardSyncManager.Instance?.UnregisterPlayFabManager(this);
    }

    private void Initialize()
    {
        titleId = !string.IsNullOrEmpty(SecretManager.Secrets.playFabTitleId)
            ? SecretManager.Secrets.playFabTitleId
            : fallbackTitleId;

        if (string.IsNullOrEmpty(titleId))
        {
            Debug.LogWarning("PlayFabManager: Title ID is not configured. Set a fallback in the inspector or via secrets.");
        }
    }

    #region Login / Registration

    public void LoginWithDeviceId(Action<bool, string> callback = null)
    {
        if (string.IsNullOrEmpty(titleId))
        {
            string error = "Cannot login to PlayFab because Title ID is missing.";
            Debug.LogError($"PlayFabManager: {error}");
            callback?.Invoke(false, error);
            return;
        }

        if (isLoggingIn)
        {
            callback?.Invoke(false, "Login already in progress.");
            return;
        }

        string customId = GetOrCreateCustomId();
        StartCoroutine(LoginWithCustomIdRoutine(customId, callback));
    }

    public void LoginWithEmail(string email, string password, Action<bool, string> callback = null)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            callback?.Invoke(false, "Email and password are required.");
            return;
        }

        if (string.IsNullOrEmpty(titleId))
        {
            callback?.Invoke(false, "PlayFab Title ID missing.");
            return;
        }

        if (isLoggingIn)
        {
            callback?.Invoke(false, "Login already in progress.");
            return;
        }

        StartCoroutine(LoginWithEmailRoutine(email, password, callback));
    }

    public void RegisterWithEmail(string email, string password, string desiredDisplayName, Action<bool, string> callback = null)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            callback?.Invoke(false, "Email and password are required.");
            return;
        }

        if (string.IsNullOrEmpty(titleId))
        {
            callback?.Invoke(false, "PlayFab Title ID missing.");
            return;
        }

        StartCoroutine(RegisterRoutine(email, password, desiredDisplayName, callback));
    }

    public void UpdateDisplayName(string newDisplayName, Action<bool, string> callback = null)
    {
        if (!IsLoggedIn)
        {
            callback?.Invoke(false, "Please login before updating the display name.");
            return;
        }

        if (string.IsNullOrWhiteSpace(newDisplayName))
        {
            callback?.Invoke(false, "Display name cannot be empty.");
            return;
        }

        StartCoroutine(UpdateDisplayNameRoutine(newDisplayName.Trim(), callback));
    }

    #endregion

    #region Score + Leaderboard

    public void TryReportScore(int score, Action<bool, string> callback = null)
    {
        if (score <= 0)
        {
            Debug.LogWarning($"PlayFabManager: Ignoring score {score} (must be > 0).");
            callback?.Invoke(false, "Score must be greater than zero.");
            return;
        }

        if (score <= lastUploadedScore)
        {
            Debug.Log($"PlayFabManager: Score {score} not higher than last uploaded {lastUploadedScore}.");
            callback?.Invoke(false, "Score already synchronized.");
            return;
        }

        if (!IsLoggedIn)
        {
            pendingScore = Mathf.Max(pendingScore, score);
            Debug.Log($"PlayFabManager: Not logged in. Queued score {pendingScore}.");
            callback?.Invoke(false, "Not logged in yet. Score will be sent after login.");

            if (autoLoginOnStart && !isLoggingIn)
                LoginWithDeviceId();
            return;
        }

        Debug.Log($"PlayFabManager: Uploading score {score} to statistic '{statisticName}'.");
        StartCoroutine(UpdateStatisticRoutine(score, callback));
    }

    public void RefreshLeaderboard()
    {
        if (!IsLoggedIn)
        {
            Debug.LogWarning("PlayFabManager: Cannot refresh leaderboard before login.");
            return;
        }

        StartCoroutine(RefreshLeaderboardRoutine());
    }

    #endregion

    #region Coroutines

    private IEnumerator LoginWithCustomIdRoutine(string customId, Action<bool, string> callback)
    {
        isLoggingIn = true;
        var payload = new LoginWithCustomIdRequest
        {
            TitleId = titleId,
            CustomId = customId,
            CreateAccount = true,
            InfoRequestParameters = new LoginInfoParameters
            {
                GetPlayerProfile = true,
                GetPlayerStatistics = true
            }
        };

        using UnityWebRequest request = BuildRequest("/Client/LoginWithCustomID", JsonUtility.ToJson(payload));
        yield return request.SendWebRequest();

        HandleLoginResponse(request, callback);
    }

    private IEnumerator LoginWithEmailRoutine(string email, string password, Action<bool, string> callback)
    {
        isLoggingIn = true;
        var payload = new LoginWithEmailRequest
        {
            TitleId = titleId,
            Email = email,
            Password = password,
            InfoRequestParameters = new LoginInfoParameters
            {
                GetPlayerProfile = true,
                GetPlayerStatistics = true
            }
        };

        using UnityWebRequest request = BuildRequest("/Client/LoginWithEmailAddress", JsonUtility.ToJson(payload));
        yield return request.SendWebRequest();

        HandleLoginResponse(request, callback);
    }

    private IEnumerator RegisterRoutine(string email, string password, string displayNameOverride, Action<bool, string> callback)
    {
        var payload = new RegisterPlayFabRequest
        {
            TitleId = titleId,
            Email = email,
            Password = password,
            DisplayName = string.IsNullOrWhiteSpace(displayNameOverride) ? null : displayNameOverride.Trim(),
            RequireBothUsernameAndEmail = false
        };

        using UnityWebRequest request = BuildRequest("/Client/RegisterPlayFabUser", JsonUtility.ToJson(payload));
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error = ParseError(request);
            callback?.Invoke(false, error);
            yield break;
        }

        callback?.Invoke(true, null);
    }

    private IEnumerator UpdateDisplayNameRoutine(string newDisplayName, Action<bool, string> callback)
    {
        var payload = new UpdateDisplayNameRequest { DisplayName = newDisplayName };
        using UnityWebRequest request = BuildRequest("/Client/UpdateUserTitleDisplayName", JsonUtility.ToJson(payload), sessionTicket);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error = ParseError(request);
            callback?.Invoke(false, error);
            yield break;
        }

        displayName = newDisplayName;
        callback?.Invoke(true, null);
    }

    private IEnumerator UpdateStatisticRoutine(int score, Action<bool, string> callback)
    {
        var payload = new UpdatePlayerStatisticsRequest
        {
            Statistics = new[]
            {
                new StatisticUpdate
                {
                    StatisticName = statisticName,
                    Value = score
                }
            }
        };

        using UnityWebRequest request = BuildRequest("/Client/UpdatePlayerStatistics", JsonUtility.ToJson(payload), sessionTicket);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error = ParseError(request);
            Debug.LogError($"PlayFabManager: Failed to upload score {score}. {error}");
            callback?.Invoke(false, error);
            yield break;
        }

        lastUploadedScore = Mathf.Max(lastUploadedScore, score);
        Debug.Log($"PlayFabManager: Successfully uploaded {score} to '{statisticName}'.");
        callback?.Invoke(true, null);
        RefreshLeaderboard();
    }

    private IEnumerator RefreshLeaderboardRoutine()
    {
        yield return FetchGlobalLeaderboardRoutine();
        yield return FetchAroundPlayerRoutine();
    }

    private IEnumerator FetchGlobalLeaderboardRoutine()
    {
        var payload = new GetLeaderboardRequest
        {
            StatisticName = statisticName,
            MaxResultsCount = Mathf.Max(1, leaderboardEntryCount)
        };

        using UnityWebRequest request = BuildRequest("/Client/GetLeaderboard", JsonUtility.ToJson(payload), sessionTicket);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"PlayFabManager: Failed to fetch leaderboard. {ParseError(request)}");
            yield break;
        }

        var response = JsonUtility.FromJson<GetLeaderboardResponse>(request.downloadHandler.text);
        leaderboardCache.Clear();

        if (response?.data?.Leaderboard != null)
        {
            foreach (PlayFabLeaderboardEntry entry in response.data.Leaderboard)
            {
                LeaderboardEntry converted = ConvertEntry(entry);
                leaderboardCache.Add(converted);
            }
        }
    }

    private IEnumerator FetchAroundPlayerRoutine()
    {
        if (string.IsNullOrEmpty(playFabId))
            yield break;

        var payload = new GetLeaderboardAroundPlayerRequest
        {
            StatisticName = statisticName,
            MaxResultsCount = Mathf.Max(3, leaderboardEntryCount),
            PlayFabId = playFabId
        };

        using UnityWebRequest request = BuildRequest("/Client/GetLeaderboardAroundPlayer", JsonUtility.ToJson(payload), sessionTicket);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"PlayFabManager: Failed to fetch player rank. {ParseError(request)}");
            lastPlayerEntry = null;
            OnLeaderboardUpdated?.Invoke(leaderboardCache, null);
            yield break;
        }

        var response = JsonUtility.FromJson<GetLeaderboardResponse>(request.downloadHandler.text);
        LeaderboardEntry playerEntry = null;

        if (response?.data?.Leaderboard != null)
        {
            foreach (PlayFabLeaderboardEntry entry in response.data.Leaderboard)
            {
                if (entry.PlayFabId == playFabId)
                {
                    playerEntry = ConvertEntry(entry, true);
                    break;
                }
            }
        }

        lastPlayerEntry = playerEntry;
        OnLeaderboardUpdated?.Invoke(leaderboardCache, playerEntry);
    }

    private void HandleLoginResponse(UnityWebRequest request, Action<bool, string> callback)
    {
        isLoggingIn = false;

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error = ParseError(request);
            Debug.LogError($"PlayFabManager: Login failed. {error}");
            OnLoginFailed?.Invoke(error);
            callback?.Invoke(false, error);
            return;
        }

        var response = JsonUtility.FromJson<LoginWithCustomIdResponse>(request.downloadHandler.text);
        sessionTicket = response?.data?.SessionTicket;
        playFabId = response?.data?.PlayFabId;
        displayName = response?.data?.InfoResultPayload?.PlayerProfile?.DisplayName;

        if (string.IsNullOrEmpty(displayName))
            displayName = $"Player-{playFabId}";

        UpdateLastUploadedScoreCache(response?.data?.InfoResultPayload?.PlayerStatistics);

        var result = new PlayFabLoginResult
        {
            PlayFabId = playFabId,
            SessionTicket = sessionTicket,
            DisplayName = displayName,
            NewlyCreated = response?.data?.NewlyCreated ?? false
        };

        lastLoginResult = result;
        OnLoginSucceeded?.Invoke(result);
        callback?.Invoke(true, null);

        if (pendingScore > 0)
        {
            int scoreToUpload = pendingScore;
            pendingScore = -1;
            TryReportScore(scoreToUpload);
        }
        else
        {
            SyncLocalBestScore();
        }

        RefreshLeaderboard();
    }

    private void UpdateLastUploadedScoreCache(StatisticValue[] stats)
    {
        if (stats == null)
            return;

        foreach (StatisticValue stat in stats)
        {
            if (stat == null || string.IsNullOrEmpty(stat.StatisticName))
                continue;

            if (string.Equals(stat.StatisticName, statisticName, StringComparison.OrdinalIgnoreCase))
            {
                lastUploadedScore = stat.Value;
                Debug.Log($"PlayFabManager: Server reports last uploaded '{statisticName}' = {lastUploadedScore}.");
                break;
            }
        }
    }

    private void SyncLocalBestScore()
    {
        if (GameManager.Instance == null)
            return;

        int localBest = GameManager.Instance.BestScore;
        if (localBest <= 0)
            return;

        if (localBest <= lastUploadedScore)
            return;

        Debug.Log($"PlayFabManager: Syncing local best score {localBest}.");
        TryReportScore(localBest);
    }

    #endregion

    #region Helper Methods

    private string GetOrCreateCustomId()
    {
        if (LeaderboardSyncManager.Instance != null &&
            LeaderboardSyncManager.Instance.TryGetCustomIdOverride(out string overrideId))
        {
            Debug.Log("PlayFabManager: Using dev-mode random custom ID.");
            return overrideId;
        }

        if (PlayerPrefs.HasKey(PlayerPrefsCustomIdKey))
            return PlayerPrefs.GetString(PlayerPrefsCustomIdKey);

        string deviceId = SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrEmpty(deviceId))
            deviceId = Guid.NewGuid().ToString("N");

        PlayerPrefs.SetString(PlayerPrefsCustomIdKey, deviceId);
        return deviceId;
    }

    private UnityWebRequest BuildRequest(string path, string jsonPayload, string authToken = null)
    {
        string url = $"{BaseUrl}{path}";
        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(jsonPayload) ? "{}" : jsonPayload);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        if (!string.IsNullOrEmpty(authToken))
            request.SetRequestHeader("X-Authorization", authToken);

        request.timeout = 20;
        return request;
    }

    private string ParseError(UnityWebRequest request)
    {
        string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        if (string.IsNullOrEmpty(body))
            return request.error;

        try
        {
            var error = JsonUtility.FromJson<PlayFabErrorResponse>(body);
            if (error != null && !string.IsNullOrEmpty(error.errorMessage))
                return error.errorMessage;
        }
        catch
        {
            // ignored
        }

        return request.error ?? "Unknown PlayFab error";
    }

    private LeaderboardEntry ConvertEntry(PlayFabLeaderboardEntry entry, bool isLocalOverride = false)
    {
        return new LeaderboardEntry
        {
            displayName = string.IsNullOrEmpty(entry.DisplayName) ? entry.PlayFabId : entry.DisplayName,
            position = entry.Position,
            score = entry.StatValue,
            isLocalPlayer = isLocalOverride || entry.PlayFabId == playFabId
        };
    }

    #endregion
}

#region DTOs

[Serializable]
public class PlayFabLoginResult
{
    public string PlayFabId;
    public string SessionTicket;
    public string DisplayName;
    public bool NewlyCreated;
}

[Serializable]
public class LoginWithCustomIdRequest
{
    public string TitleId;
    public string CustomId;
    public bool CreateAccount;
    public LoginInfoParameters InfoRequestParameters;
}

[Serializable]
public class LoginWithEmailRequest
{
    public string TitleId;
    public string Email;
    public string Password;
    public LoginInfoParameters InfoRequestParameters;
}

[Serializable]
public class RegisterPlayFabRequest
{
    public string TitleId;
    public string Email;
    public string Password;
    public string DisplayName;
    public bool RequireBothUsernameAndEmail;
}

[Serializable]
public class UpdateDisplayNameRequest
{
    public string DisplayName;
}

[Serializable]
public class LoginInfoParameters
{
    public bool GetPlayerProfile;
    public bool GetPlayerStatistics;
}

[Serializable]
public class LoginWithCustomIdResponse
{
    public LoginWithCustomIdData data;
}

[Serializable]
public class LoginWithCustomIdData
{
    public string SessionTicket;
    public string PlayFabId;
    public bool NewlyCreated;
    public LoginInfoPayload InfoResultPayload;
}

[Serializable]
public class LoginInfoPayload
{
    public LoginPlayerProfile PlayerProfile;
    public StatisticValue[] PlayerStatistics;
}

[Serializable]
public class LoginPlayerProfile
{
    public string DisplayName;
}

[Serializable]
public class StatisticValue
{
    public string StatisticName;
    public int Value;
}

[Serializable]
public class UpdatePlayerStatisticsRequest
{
    public StatisticUpdate[] Statistics;
}

[Serializable]
public class StatisticUpdate
{
    public string StatisticName;
    public int Value;
}

[Serializable]
public class GetLeaderboardRequest
{
    public string StatisticName;
    public int MaxResultsCount;
}

[Serializable]
public class GetLeaderboardAroundPlayerRequest
{
    public string StatisticName;
    public int MaxResultsCount;
    public string PlayFabId;
}

[Serializable]
public class GetLeaderboardResponse
{
    public GetLeaderboardData data;
}

[Serializable]
public class GetLeaderboardData
{
    public PlayFabLeaderboardEntry[] Leaderboard;
}

[Serializable]
public class PlayFabLeaderboardEntry
{
    public string DisplayName;
    public string PlayFabId;
    public int Position;
    public int StatValue;
}

[Serializable]
public class PlayFabErrorResponse
{
    public int code;
    public string status;
    public string error;
    public int errorCode;
    public string errorMessage;
}

#endregion

