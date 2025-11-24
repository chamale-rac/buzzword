using System;
using System.IO;
using UnityEngine;

[Serializable]
public class SecretsPayload
{
    public string playFabTitleId;
    public string playFabApiKey;
    public string geminiApiKey;

    public bool HasAnyValue()
    {
        return !string.IsNullOrEmpty(playFabTitleId) ||
               !string.IsNullOrEmpty(playFabApiKey) ||
               !string.IsNullOrEmpty(geminiApiKey);
    }
}

/// <summary>
/// Loads sensitive configuration from environment variables or local files that are excluded from version control.
/// </summary>
public static class SecretManager
{
    private const string SecretsJsonEnvKey = "BUZZWORD_SECRETS_JSON";
    private const string PlayFabTitleEnvKey = "PLAYFAB_TITLE_ID";
    private const string PlayFabSecretEnvKey = "PLAYFAB_DEV_SECRET";
    private const string GeminiKeyEnv = "GEMINI_API_KEY";
    private const string LocalSecretsFileName = "buzzword-secrets.json";

    private static SecretsPayload cachedPayload;

    public static SecretsPayload Secrets
    {
        get
        {
            if (cachedPayload == null)
                cachedPayload = LoadSecrets();
            return cachedPayload;
        }
    }

    public static void ForceReload()
    {
        cachedPayload = null;
    }

    private static SecretsPayload LoadSecrets()
    {
        SecretsPayload payload = TryLoadFromEnvironmentJson();
        if (payload?.HasAnyValue() == true)
            return payload;

        payload = TryLoadFromIndividualEnvironmentVariables();
        if (payload?.HasAnyValue() == true)
            return payload;

        payload = TryLoadFromProjectLocalSettings();
        if (payload?.HasAnyValue() == true)
            return payload;

        payload = TryLoadFromPersistentData();
        if (payload?.HasAnyValue() == true)
            return payload;

        return new SecretsPayload();
    }

    private static SecretsPayload TryLoadFromEnvironmentJson()
    {
        string raw = Environment.GetEnvironmentVariable(SecretsJsonEnvKey);
        if (string.IsNullOrEmpty(raw))
            return null;

        try
        {
            return JsonUtility.FromJson<SecretsPayload>(raw);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SecretManager: Failed to parse {SecretsJsonEnvKey} payload. {ex.Message}");
            return null;
        }
    }

    private static SecretsPayload TryLoadFromIndividualEnvironmentVariables()
    {
        SecretsPayload payload = new SecretsPayload
        {
            playFabTitleId = Environment.GetEnvironmentVariable(PlayFabTitleEnvKey),
            playFabApiKey = Environment.GetEnvironmentVariable(PlayFabSecretEnvKey),
            geminiApiKey = Environment.GetEnvironmentVariable(GeminiKeyEnv)
        };

        return payload.HasAnyValue() ? payload : null;
    }

    private static SecretsPayload TryLoadFromProjectLocalSettings()
    {
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return null;

            string localSettingsPath = Path.Combine(projectRoot, "LocalSettings", LocalSecretsFileName);
            if (!File.Exists(localSettingsPath))
                return null;

            string json = File.ReadAllText(localSettingsPath);
            return JsonUtility.FromJson<SecretsPayload>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SecretManager: Could not read LocalSettings secrets. {ex.Message}");
            return null;
        }
    }

    private static SecretsPayload TryLoadFromPersistentData()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, LocalSecretsFileName);
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SecretsPayload>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SecretManager: Could not read persistent secrets. {ex.Message}");
            return null;
        }
    }
}

