using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thin UI helper that binds PlayFabManager events to TextMeshPro labels and buttons.
/// </summary>
public class LeaderboardPanelController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private TextMeshProUGUI leaderboardLabel;
    [SerializeField] private TextMeshProUGUI playerRankLabel;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button loginButton;
    [SerializeField] private TMP_InputField displayNameInput;
    [SerializeField] private Button saveDisplayNameButton;
    [SerializeField, Tooltip("Seconds to show temporary status messages (e.g., name updates).")]
    private float statusFlashDuration = 2f;

    private const string StatusLabelName = "StatusText";
    private const string LeaderboardLabelName = "LeadeboardLabel";
    private const string PlayerRankLabelName = "PlayerRank";
    private const string RefreshButtonName = "RefreshButton";
    private const string LoginButtonName = "LoginButton";
    private const string DisplayInputName = "DisplayInputField";
    private const string SaveNameButtonName = "SaveNameButton";

    private PlayFabManager subscribedManager;
    private Coroutine waitForManagerRoutine;
    private Coroutine statusFlashRoutine;

    private void Awake()
    {
        AutoAssignUIReferences();

        if (refreshButton != null)
            refreshButton.onClick.AddListener(RequestRefresh);

        if (loginButton != null)
            loginButton.onClick.AddListener(() => PlayFabManager.Instance?.LoginWithDeviceId());

        if (saveDisplayNameButton != null)
            saveDisplayNameButton.onClick.AddListener(SaveDisplayName);
    }

    public void ForceImmediateRefresh()
    {
        AutoAssignUIReferences();
        SyncImmediateState();
    }

    private void AutoAssignUIReferences()
    {
        statusLabel = statusLabel ?? FindComponentInChildren<TextMeshProUGUI>(StatusLabelName);
        leaderboardLabel = leaderboardLabel ?? FindComponentInChildren<TextMeshProUGUI>(LeaderboardLabelName);
        playerRankLabel = playerRankLabel ?? FindComponentInChildren<TextMeshProUGUI>(PlayerRankLabelName);
        refreshButton = refreshButton ?? FindComponentInChildren<Button>(RefreshButtonName);
        loginButton = loginButton ?? FindComponentInChildren<Button>(LoginButtonName);
        displayNameInput = displayNameInput ?? FindComponentInChildren<TMP_InputField>(DisplayInputName);
        saveDisplayNameButton = saveDisplayNameButton ?? FindComponentInChildren<Button>(SaveNameButtonName);
    }

    private T FindComponentInChildren<T>(string targetName) where T : Component
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        Transform found = FindChildRecursive(transform, targetName);
        return found != null ? found.GetComponent<T>() : null;
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null)
                return nested;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        AutoAssignUIReferences();
    }
#endif

    private void OnEnable()
    {
        Subscribe();
        SyncImmediateState();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (PlayFabManager.Instance == null)
        {
            if (waitForManagerRoutine == null)
                waitForManagerRoutine = StartCoroutine(WaitForManagerAndSubscribe());
            return;
        }

        if (waitForManagerRoutine != null)
        {
            StopCoroutine(waitForManagerRoutine);
            waitForManagerRoutine = null;
        }

        if (subscribedManager == PlayFabManager.Instance)
            return;

        subscribedManager = PlayFabManager.Instance;
        subscribedManager.OnLoginSucceeded += HandleLoginSucceeded;
        subscribedManager.OnLoginFailed += HandleLoginFailed;
        subscribedManager.OnLeaderboardUpdated += HandleLeaderboardUpdated;
    }

    private void Unsubscribe()
    {
        if (waitForManagerRoutine != null)
        {
            StopCoroutine(waitForManagerRoutine);
            waitForManagerRoutine = null;
        }

        if (subscribedManager == null)
            return;

        subscribedManager.OnLoginSucceeded -= HandleLoginSucceeded;
        subscribedManager.OnLoginFailed -= HandleLoginFailed;
        subscribedManager.OnLeaderboardUpdated -= HandleLeaderboardUpdated;
        subscribedManager = null;
    }

    private IEnumerator WaitForManagerAndSubscribe()
    {
        while (PlayFabManager.Instance == null)
            yield return null;

        waitForManagerRoutine = null;
        Subscribe();
        SyncImmediateState();
    }

    private void HandleLoginSucceeded(PlayFabLoginResult result)
    {
        UpdateStatusLabel();
        SyncDisplayNameInput(result.DisplayName);
    }

    private void HandleLoginFailed(string error)
    {
        if (statusLabel != null)
            statusLabel.text = $"Login Failed:\n{error}";
    }

    private void HandleLeaderboardUpdated(IReadOnlyList<LeaderboardEntry> entries, LeaderboardEntry playerEntry)
    {
        if (leaderboardLabel != null)
            leaderboardLabel.text = BuildLeaderboardText(entries);

        if (playerRankLabel != null)
        {
            if (playerEntry == null)
            {
                playerRankLabel.text = "Play at least one game to earn a rank.";
            }
            else
            {
                string name = !string.IsNullOrEmpty(playerEntry.displayName)
                    ? playerEntry.displayName
                    : PlayFabManager.Instance?.DisplayName ?? "Player";
                playerRankLabel.text = $"Your Rank: #{playerEntry.position + 1} ({name})\nScore: {playerEntry.score}";
            }
        }

    }

    private void RequestRefresh()
    {
        if (PlayFabManager.Instance == null)
            return;

        if (!PlayFabManager.Instance.IsLoggedIn)
        {
            statusLabel.text = "Login required before refreshing leaderboard.";
            return;
        }

        statusLabel.text = "Refreshing leaderboard...";
        PlayFabManager.Instance.RefreshLeaderboard();
    }

    private void SaveDisplayName()
    {
        if (PlayFabManager.Instance == null || displayNameInput == null)
            return;

        string desired = displayNameInput.text;
        PlayFabManager.Instance.UpdateDisplayName(desired, (success, error) =>
        {
            if (statusLabel == null)
                return;

            if (success)
            {
                SyncDisplayNameInput(desired);
                ShowTemporaryStatus("Update successful");
                PlayFabManager.Instance.RefreshLeaderboard();
            }
            else
            {
                statusLabel.text = $"Failed to save name:\n{error}";
            }
        });
    }

    private void SyncImmediateState()
    {
        var manager = PlayFabManager.Instance;
        if (manager == null)
            return;

        UpdateStatusLabel();
        SyncDisplayNameInput(manager.DisplayName);

        if (manager.CachedLeaderboard != null && manager.CachedLeaderboard.Count > 0)
        {
            HandleLeaderboardUpdated(manager.CachedLeaderboard, manager.LastPlayerEntry);
        }
        else if (manager.IsLoggedIn)
        {
            if (statusLabel != null)
                statusLabel.text = "Refreshing leaderboard...";

            manager.RefreshLeaderboard();
        }
    }

    private string BuildLeaderboardText(IReadOnlyList<LeaderboardEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return "No leaderboard data yet.";

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            LeaderboardEntry entry = entries[i];
            string name = string.IsNullOrEmpty(entry.displayName) ? "Player" : entry.displayName;
            string prefix = entry.isLocalPlayer ? "(you) " : string.Empty;
            builder.AppendLine($"{prefix}#{entry.position + 1} - {name} ({entry.score})");
        }

        return builder.ToString();
    }


    private void UpdateStatusLabel(string headerOverride = null)
    {
        if (statusLabel == null)
            return;

        var manager = PlayFabManager.Instance;
        if (manager == null || !manager.IsLoggedIn)
        {
            statusLabel.text = "Login required before refreshing leaderboard.";
            return;
        }

        string id = manager.LastLoginResult?.PlayFabId ?? manager.DisplayName ?? "Unknown";
        string name = manager.DisplayName ?? manager.LastLoginResult?.DisplayName ?? "Player";
        string header;

        if (!string.IsNullOrEmpty(headerOverride))
        {
            header = headerOverride;
        }
        else
        {
            bool newlyCreated = manager.LastLoginResult?.NewlyCreated == true;
            header = newlyCreated ? "New account created" : "Login successful";
        }

        statusLabel.text = $"{header}\nID: {id}\nName: {name}";
    }

    private void SyncDisplayNameInput(string value)
    {
        if (displayNameInput == null)
            return;

        string sanitized = string.IsNullOrEmpty(value) ? string.Empty : value;
        if (displayNameInput.text != sanitized)
            displayNameInput.text = sanitized;
    }

    private void ShowTemporaryStatus(string header)
    {
        if (statusFlashRoutine != null)
            StopCoroutine(statusFlashRoutine);

        statusFlashRoutine = StartCoroutine(TemporaryStatusRoutine(header));
    }

    private IEnumerator TemporaryStatusRoutine(string header)
    {
        UpdateStatusLabel(header);
        yield return new WaitForSeconds(Mathf.Max(0.1f, statusFlashDuration));
        UpdateStatusLabel();
        statusFlashRoutine = null;
    }

}

