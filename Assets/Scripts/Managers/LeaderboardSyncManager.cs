using UnityEngine;

/// <summary>
/// Scene-agnostic helper that queues best scores while gameplay is active
/// and asks the menu-only PlayFabManager to upload them when available.
/// </summary>
public class LeaderboardSyncManager : MonoBehaviour
{
    public static LeaderboardSyncManager Instance { get; private set; }

    [Header("Developer Settings")]
    [SerializeField, Tooltip("When enabled, development helpers are active (e.g. random PlayFab IDs).")]
    private bool enableDevMode = false;
    [SerializeField, Tooltip("When Dev Mode is enabled, generate a brand new PlayFab Custom ID each login.")]
    private bool randomizeLoginEachSession = false;

    private PlayFabManager activePlayFab;
    private int pendingScore = -1;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ReportScore(int score)
    {
        if (score <= 0)
            return;

        pendingScore = Mathf.Max(pendingScore, score);
        TryFlush();
    }

    internal void RegisterPlayFabManager(PlayFabManager manager)
    {
        activePlayFab = manager;
        TryFlush();
    }

    internal void UnregisterPlayFabManager(PlayFabManager manager)
    {
        if (activePlayFab == manager)
            activePlayFab = null;
    }

    private void TryFlush()
    {
        if (pendingScore <= 0 || activePlayFab == null)
            return;

        int scoreToUpload = pendingScore;
        activePlayFab.TryReportScore(scoreToUpload, (success, error) =>
        {
            if (success)
            {
                pendingScore = -1;
            }
            else
            {
                Debug.LogWarning($"LeaderboardSyncManager: Failed to upload {scoreToUpload}. {error}");
            }
        });
    }

    internal bool TryGetCustomIdOverride(out string customId)
    {
        if (enableDevMode && randomizeLoginEachSession)
        {
            customId = $"DEV-{System.Guid.NewGuid():N}";
            return true;
        }

        customId = null;
        return false;
    }

    public bool IsDevModeEnabled => enableDevMode;
}


