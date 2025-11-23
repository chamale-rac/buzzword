using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SceneLoader - Manages asynchronous scene transitions with animated loading screen
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Loading Screen References")]
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI percentageText;

    [Header("Animation Settings")]
    [SerializeField] private float minimumLoadTime = 1f;
    [SerializeField] private string[] loadingMessages = new string[]
    {
        "Loading words...",
        "Preparing phrases...",
        "Connecting to word database...",
        "Almost there..."
    };

    [Header("Scene Targets")]
    [SerializeField] private string endlessSceneName = "Level1";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (loadingScreen != null)
                loadingScreen.SetActive(false);
            
            Debug.Log("SceneLoader initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Load a scene asynchronously with loading screen
    /// </summary>
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    /// <summary>
    /// Load a scene by build index
    /// </summary>
    public void LoadScene(int sceneIndex)
    {
        StartCoroutine(LoadSceneAsync(sceneIndex));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        float startTime = Time.time;

        // Activate loading screen
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        // Start loading the scene
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        int messageIndex = 0;
        float messageTimer = 0f;
        float messageInterval = 0.5f;

        while (!operation.isDone)
        {
            // Calculate progress (0.0 to 0.9 is loading, 0.9 to 1.0 is activation)
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            // Update UI
            if (progressBar != null)
                progressBar.value = progress;

            if (percentageText != null)
                percentageText.text = $"{Mathf.RoundToInt(progress * 100)}%";

            // Rotate loading messages
            if (loadingText != null)
            {
                messageTimer += Time.deltaTime;
                if (messageTimer >= messageInterval)
                {
                    messageTimer = 0f;
                    messageIndex = (messageIndex + 1) % loadingMessages.Length;
                    loadingText.text = loadingMessages[messageIndex];
                }
            }

            // Check if scene is ready to activate
            if (operation.progress >= 0.9f)
            {
                // Ensure minimum load time has passed for better UX
                float elapsedTime = Time.time - startTime;
                if (elapsedTime >= minimumLoadTime)
                {
                    operation.allowSceneActivation = true;
                }
            }

            yield return null;
        }

        // Deactivate loading screen
        if (loadingScreen != null)
            loadingScreen.SetActive(false);
    }

    private IEnumerator LoadSceneAsync(int sceneIndex)
    {
        float startTime = Time.time;

        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);
        operation.allowSceneActivation = false;

        int messageIndex = 0;
        float messageTimer = 0f;
        float messageInterval = 0.5f;

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            if (progressBar != null)
                progressBar.value = progress;

            if (percentageText != null)
                percentageText.text = $"{Mathf.RoundToInt(progress * 100)}%";

            if (loadingText != null)
            {
                messageTimer += Time.deltaTime;
                if (messageTimer >= messageInterval)
                {
                    messageTimer = 0f;
                    messageIndex = (messageIndex + 1) % loadingMessages.Length;
                    loadingText.text = loadingMessages[messageIndex];
                }
            }

            if (operation.progress >= 0.9f)
            {
                float elapsedTime = Time.time - startTime;
                if (elapsedTime >= minimumLoadTime)
                {
                    operation.allowSceneActivation = true;
                }
            }

            yield return null;
        }

        if (loadingScreen != null)
            loadingScreen.SetActive(false);
    }

    /// <summary>
    /// Reload the current scene
    /// </summary>
    public void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Load main menu
    /// </summary>
    public void LoadMainMenu()
    {
        LoadScene("MainMenu");
    }

    /// <summary>
    /// Load the endless gameplay scene. Former level selection now redirects here.
    /// </summary>
    public void LoadEndlessMode()
    {
        if (string.IsNullOrEmpty(endlessSceneName))
        {
            Debug.LogError("Endless scene name not configured on SceneLoader.");
            return;
        }

        LoadScene(endlessSceneName);
    }

    /// <summary>
    /// Legacy method kept for compatibility. Ignores the level number and loads the endless scene.
    /// </summary>
    public void LoadLevel(int levelNumber)
    {
        Debug.LogWarning($"LoadLevel({levelNumber}) is deprecated. Redirecting to endless mode.");
        LoadEndlessMode();
    }

    /// <summary>
    /// Quit the game
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}

