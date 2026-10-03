using System;
using NUnit.Framework;
using UnityEngine;

public class PlayerProfileTests
{
    [SetUp]
    public void SetUp()
    {
        PlayerProfile.ResetAllProfileData();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerProfile.ResetAllProfileData();
    }

    [Test]
    public void PlayerProfile_DefaultName_WhenUnset_ReturnsMaveric()
    {
        Assert.AreEqual(PlayerProfile.DEFAULT_NAME, PlayerProfile.PlayerName);
    }

    [Test]
    public void PlayerProfile_SetPlayerName_PersistsAndFiresEvent()
    {
        string eventName = null;
        PlayerProfile.OnPlayerNameChanged += (name) => eventName = name;

        string result = PlayerProfile.SetPlayerName("StarPilot_42");

        Assert.AreEqual("StarPilot_42", result);
        Assert.AreEqual("StarPilot_42", PlayerProfile.PlayerName);
        Assert.AreEqual("StarPilot_42", eventName);
    }

    [Test]
    public void PlayerProfile_SanitizeName_TrimsWhitespaceAndExcessSpaces()
    {
        string result = PlayerProfile.SanitizePlayerName("   Star    Pilot   ");
        Assert.AreEqual("Star Pilot", result);
    }

    [Test]
    public void PlayerProfile_SanitizeName_StripsHtmlTags()
    {
        string injection = "<script>alert('xss')</script><b>PilotAce</b>";
        string result = PlayerProfile.SanitizePlayerName(injection);
        Assert.AreEqual("alertxssPilotAce", result);
    }

    [Test]
    public void PlayerProfile_SanitizeName_FiltersInvalidCharacters()
    {
        string raw = "Ace@#$_Pilot-99!";
        string result = PlayerProfile.SanitizePlayerName(raw);
        Assert.AreEqual("Ace_Pilot-99", result);
    }

    [Test]
    public void PlayerProfile_SanitizeName_EnforcesMinLength_FallbackToDefault()
    {
        Assert.AreEqual(PlayerProfile.DEFAULT_NAME, PlayerProfile.SanitizePlayerName(""));
        Assert.AreEqual(PlayerProfile.DEFAULT_NAME, PlayerProfile.SanitizePlayerName("   "));
        Assert.AreEqual(PlayerProfile.DEFAULT_NAME, PlayerProfile.SanitizePlayerName("A"));
        Assert.AreEqual(PlayerProfile.DEFAULT_NAME, PlayerProfile.SanitizePlayerName(null));
    }

    [Test]
    public void PlayerProfile_SanitizeName_TruncatesExceedingMaxLength()
    {
        string longName = "SuperDuperLongPilotCallsign123456789";
        string result = PlayerProfile.SanitizePlayerName(longName);

        Assert.AreEqual(PlayerProfile.MAX_NAME_LENGTH, result.Length);
        Assert.AreEqual("SuperDuperLongPi", result);
    }

    [Test]
    public void PlayerProfile_IsValidPlayerName_ValidatesCorrectly()
    {
        Assert.IsTrue(PlayerProfile.IsValidPlayerName("Viper_01", out string _));
        Assert.IsTrue(PlayerProfile.IsValidPlayerName("Star-Lord", out string _));

        Assert.IsFalse(PlayerProfile.IsValidPlayerName("", out string emptyReason));
        Assert.IsNotEmpty(emptyReason);

        Assert.IsFalse(PlayerProfile.IsValidPlayerName("X", out string shortReason));
        Assert.IsNotEmpty(shortReason);

        Assert.IsFalse(PlayerProfile.IsValidPlayerName("ThisNameIsWayTooLongForLeaderboard123", out string longReason));
        Assert.IsNotEmpty(longReason);

        Assert.IsFalse(PlayerProfile.IsValidPlayerName("Hacker<script>", out string charReason));
        Assert.IsNotEmpty(charReason);
    }

    [Test]
    public void PlayerProfile_PlayerId_GeneratesPersistentUUID()
    {
        string id1 = PlayerProfile.PlayerId;
        Assert.IsFalse(string.IsNullOrEmpty(id1));

        string id2 = PlayerProfile.PlayerId;
        Assert.AreEqual(id1, id2, "PlayerId must remain consistent across calls");

        PlayerProfile.SetPlayerName("NewCallsign");
        Assert.AreEqual(id1, PlayerProfile.PlayerId, "PlayerId must not change when player name changes");
    }

    [Test]
    public void PlayerProfile_RecordRun_TracksHighScoreAndBestWave()
    {
        ulong newScoreFired = 0;
        PlayerProfile.OnNewHighScore += (s) => newScoreFired = s;

        Assert.AreEqual(0, PlayerProfile.HighScore);
        Assert.AreEqual(0, PlayerProfile.HighestWave);

        bool isHigh1 = PlayerProfile.RecordRun(500, 3, 45.5f, out bool isWave1);
        Assert.IsTrue(isHigh1);
        Assert.IsTrue(isWave1);
        Assert.AreEqual(500, PlayerProfile.HighScore);
        Assert.AreEqual(3, PlayerProfile.HighestWave);
        Assert.AreEqual(500, newScoreFired);

        // Lower score run does not trigger high score
        newScoreFired = 0;
        bool isHigh2 = PlayerProfile.RecordRun(300, 4, 60f, out bool isWave2);
        Assert.IsFalse(isHigh2);
        Assert.IsTrue(isWave2);
        Assert.AreEqual(500, PlayerProfile.HighScore);
        Assert.AreEqual(4, PlayerProfile.HighestWave);
        Assert.AreEqual(0, newScoreFired);
    }

    [Test]
    public void PlayerProfile_LeaderboardPayload_SerializesAndDeserializesCorrectly()
    {
        PlayerProfile.SetPlayerName("Orion");
        LeaderboardPayload payload = PlayerProfile.CreateLeaderboardPayload(12500, 7, 185.25f);

        Assert.AreEqual(PlayerProfile.PlayerId, payload.playerId);
        Assert.AreEqual("Orion", payload.playerName);
        Assert.AreEqual("12500", payload.score);
        Assert.AreEqual(12500, payload.scoreValue);
        Assert.AreEqual(7, payload.wave);
        Assert.AreEqual(185.25f, payload.survivalTime, 0.01f);

        string json = payload.ToJson();
        Assert.IsFalse(string.IsNullOrEmpty(json));
        Assert.IsTrue(json.Contains("Orion"));
        Assert.IsTrue(json.Contains("12500"));

        LeaderboardPayload parsed = LeaderboardPayload.FromJson(json);
        Assert.IsNotNull(parsed);
        Assert.AreEqual(payload.playerId, parsed.playerId);
        Assert.AreEqual("Orion", parsed.playerName);
        Assert.AreEqual(7, parsed.wave);
    }

    [Test]
    public void PlayerProfile_SubmitLeaderboardEntry_FiresEventWithCorrectPayload()
    {
        PlayerProfile.SetPlayerName("Nova");
        LeaderboardPayload receivedPayload = null;
        PlayerProfile.OnLeaderboardSubmissionReady += (p) => receivedPayload = p;

        LeaderboardPayload submitted = PlayerProfile.SubmitLeaderboardEntry(9999, 10, 300f);

        Assert.IsNotNull(receivedPayload);
        Assert.AreEqual(submitted.playerId, receivedPayload.playerId);
        Assert.AreEqual("Nova", receivedPayload.playerName);
        Assert.AreEqual(9999, receivedPayload.scoreValue);
    }

    [Test]
    public void PlayerProfile_HasSaveData_ReturnsFalseWhenClean_AndTrueWhenSaved()
    {
        PlayerMetaProgression.ResetAllProgress();
        PlayerProfile.ResetAllProfileData();

        Assert.IsFalse(PlayerProfile.HasSaveData(), "Fresh profile should not report saved data");

        PlayerProfile.SetPlayerName("RoguePilot");
        Assert.IsTrue(PlayerProfile.HasSaveData(), "Setting a custom name should report saved data");

        PlayerProfile.ResetAllProfileData();
        Assert.IsFalse(PlayerProfile.HasSaveData(), "Resetting profile should clear saved data flag");

        PlayerMetaProgression.AddScrap(50);
        Assert.IsTrue(PlayerProfile.HasSaveData(), "Meta scrap should report saved data");

        PlayerMetaProgression.ResetAllProgress();
        Assert.IsFalse(PlayerProfile.HasSaveData(), "Resetting meta progress should clear saved data flag");
    }
}
