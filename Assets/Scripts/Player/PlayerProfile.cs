using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Manages player identity, persistent profile settings, sanitization of player names,
/// and score/wave records formatted for web-based server leaderboards.
/// </summary>
public static class PlayerProfile
{
    public const string PREF_PLAYER_NAME = "Aster_Player_Name";
    public const string PREF_PLAYER_ID = "Aster_Player_Id";
    public const string PREF_HIGH_SCORE = "Aster_High_Score";
    public const string PREF_HIGH_WAVE = "Aster_High_Wave";
    public const string PREF_BEST_TIME = "Aster_Best_Time";

    public const string DEFAULT_NAME = "Maveric";
    public const int MIN_NAME_LENGTH = 2;
    public const int MAX_NAME_LENGTH = 16;

    // Events for reactive UI updates
    public static event Action<string> OnPlayerNameChanged;
    public static event Action<ulong> OnNewHighScore;
    public static event Action<LeaderboardPayload> OnLeaderboardSubmissionReady;

    /// <summary>
    /// Current sanitized player name, persisted in PlayerPrefs.
    /// Returns DEFAULT_NAME if not set or blank.
    /// </summary>
    public static string PlayerName
    {
        get
        {
            string saved = PlayerPrefs.GetString(PREF_PLAYER_NAME, string.Empty);
            if (string.IsNullOrWhiteSpace(saved))
            {
                return DEFAULT_NAME;
            }
            return saved;
        }
        set
        {
            SetPlayerName(value);
        }
    }

    /// <summary>
    /// Unique persistent player ID (UUID). Generated once per client installation.
    /// Enables server backends to distinguish players with identical display names
    /// and safely handle name changes on leaderboards.
    /// </summary>
    public static string PlayerId
    {
        get
        {
            string id = PlayerPrefs.GetString(PREF_PLAYER_ID, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PREF_PLAYER_ID, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    /// <summary>
    /// Highest XP/score recorded locally by this player.
    /// </summary>
    public static ulong HighScore
    {
        get
        {
            string raw = PlayerPrefs.GetString(PREF_HIGH_SCORE, "0");
            return ulong.TryParse(raw, out ulong score) ? score : 0;
        }
        private set
        {
            PlayerPrefs.SetString(PREF_HIGH_SCORE, value.ToString());
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Highest wave reached locally by this player.
    /// </summary>
    public static int HighestWave
    {
        get => PlayerPrefs.GetInt(PREF_HIGH_WAVE, 0);
        private set
        {
            PlayerPrefs.SetInt(PREF_HIGH_WAVE, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Longest survival time (in seconds) recorded locally.
    /// </summary>
    public static float BestSurvivalTime
    {
        get => PlayerPrefs.GetFloat(PREF_BEST_TIME, 0f);
        private set
        {
            PlayerPrefs.SetFloat(PREF_BEST_TIME, Mathf.Max(0f, value));
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Sanitizes and updates the active player name.
    /// Strips disallowed symbols, HTML tags, excess spaces, and clamps to limits.
    /// Persists to PlayerPrefs and invokes OnPlayerNameChanged.
    /// </summary>
    public static string SetPlayerName(string rawName)
    {
        string sanitized = SanitizePlayerName(rawName);
        PlayerPrefs.SetString(PREF_PLAYER_NAME, sanitized);
        PlayerPrefs.Save();

        OnPlayerNameChanged?.Invoke(sanitized);
        return sanitized;
    }

    /// <summary>
    /// Sanitizes a raw name string for safe web display and server storage.
    /// Removes HTML tags, filters characters, trims whitespace, and validates length.
    /// </summary>
    public static string SanitizePlayerName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return DEFAULT_NAME;
        }

        // 1. Strip HTML/XML tags to prevent web injection (XSS/formatting errors)
        string cleaned = Regex.Replace(input, "<.*?>", string.Empty);

        // 2. Filter allowed characters: alphanumeric, underscores, hyphens, periods, spaces
        cleaned = Regex.Replace(cleaned, @"[^a-zA-Z0-9_\-\. ]", string.Empty);

        // 3. Collapse multiple whitespace characters into single space
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        // 4. Trim boundary whitespace
        cleaned = cleaned.Trim();

        // 5. Check length limits
        if (cleaned.Length < MIN_NAME_LENGTH)
        {
            return DEFAULT_NAME;
        }

        if (cleaned.Length > MAX_NAME_LENGTH)
        {
            cleaned = cleaned.Substring(0, MAX_NAME_LENGTH).TrimEnd();
        }

        return string.IsNullOrEmpty(cleaned) ? DEFAULT_NAME : cleaned;
    }

    /// <summary>
    /// Validates whether a given name is acceptable without requiring modification.
    /// </summary>
    public static bool IsValidPlayerName(string input, out string reason)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            reason = "Name cannot be empty.";
            return false;
        }

        string stripped = input.Trim();
        if (stripped.Length < MIN_NAME_LENGTH)
        {
            reason = $"Name must be at least {MIN_NAME_LENGTH} characters long.";
            return false;
        }

        if (stripped.Length > MAX_NAME_LENGTH)
        {
            reason = $"Name cannot exceed {MAX_NAME_LENGTH} characters.";
            return false;
        }

        if (Regex.IsMatch(stripped, @"[^a-zA-Z0-9_\-\. ]"))
        {
            reason = "Name contains invalid characters. Use letters, numbers, spaces, dots, dashes, and underscores.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Evaluates run results against local bests, updating stored records.
    /// Returns true if a new high score was set.
    /// </summary>
    public static bool RecordRun(ulong score, int wave, float survivalTime, out bool isNewBestWave)
    {
        bool isNewHighScore = false;
        isNewBestWave = false;

        if (score > HighScore)
        {
            HighScore = score;
            isNewHighScore = true;
            OnNewHighScore?.Invoke(score);
        }

        if (wave > HighestWave)
        {
            HighestWave = wave;
            isNewBestWave = true;
        }

        if (survivalTime > BestSurvivalTime)
        {
            BestSurvivalTime = survivalTime;
        }

        return isNewHighScore;
    }

    /// <summary>
    /// Overload for RecordRun when best wave status is not needed.
    /// </summary>
    public static bool RecordRun(ulong score, int wave, float survivalTime)
    {
        return RecordRun(score, wave, survivalTime, out _);
    }

    /// <summary>
    /// Generates a standardized, serializable payload ready for transmission to a server leaderboard.
    /// </summary>
    public static LeaderboardPayload CreateLeaderboardPayload(ulong score, int wave, float survivalTime)
    {
        return new LeaderboardPayload
        {
            playerId = PlayerId,
            playerName = PlayerName,
            score = score.ToString(),
            scoreValue = score,
            wave = wave,
            survivalTime = (float)Math.Round(survivalTime, 2),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            gameVersion = Application.version,
            platform = Application.platform.ToString()
        };
    }

    /// <summary>
    /// Creates and notifies listeners that a leaderboard payload is ready to be dispatched to a server.
    /// </summary>
    public static LeaderboardPayload SubmitLeaderboardEntry(ulong score, int wave, float survivalTime)
    {
        LeaderboardPayload payload = CreateLeaderboardPayload(score, wave, survivalTime);
        OnLeaderboardSubmissionReady?.Invoke(payload);
        return payload;
    }

    /// <summary>
    /// Checks whether any saved progress or custom profile data exists.
    /// </summary>
    public static bool HasSaveData()
    {
        return PlayerPrefs.HasKey(PREF_PLAYER_NAME)
            || PlayerPrefs.HasKey(PREF_HIGH_SCORE)
            || PlayerPrefs.HasKey(PREF_HIGH_WAVE)
            || PlayerPrefs.HasKey(PREF_BEST_TIME)
            || PlayerMetaProgression.HasAnyProgress();
    }

    /// <summary>
    /// Resets all local player profile data and high scores.
    /// </summary>
    public static void ResetAllProfileData()
    {
        PlayerPrefs.DeleteKey(PREF_PLAYER_NAME);
        PlayerPrefs.DeleteKey(PREF_PLAYER_ID);
        PlayerPrefs.DeleteKey(PREF_HIGH_SCORE);
        PlayerPrefs.DeleteKey(PREF_HIGH_WAVE);
        PlayerPrefs.DeleteKey(PREF_BEST_TIME);
        PlayerPrefs.Save();

        OnPlayerNameChanged?.Invoke(DEFAULT_NAME);
    }
}

/// <summary>
/// Serializable data packet designed for future HTTP / WebSocket server leaderboard endpoints.
/// </summary>
[Serializable]
public class LeaderboardPayload
{
    public string playerId;
    public string playerName;
    public string score;
    public ulong scoreValue;
    public int wave;
    public float survivalTime;
    public long timestamp;
    public string gameVersion;
    public string platform;

    public string ToJson()
    {
        return JsonUtility.ToJson(this);
    }

    public static LeaderboardPayload FromJson(string json)
    {
        return JsonUtility.FromJson<LeaderboardPayload>(json);
    }
}
