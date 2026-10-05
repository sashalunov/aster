using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class AudioManagerTests
{
    private GameObject _audioMgrGo;
    private AudioManager _audioManager;

    [SetUp]
    public void SetUp()
    {
        _audioMgrGo = new GameObject("Test_AudioManager");
        _audioManager = _audioMgrGo.AddComponent<AudioManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_audioMgrGo != null)
        {
            Object.DestroyImmediate(_audioMgrGo);
        }
    }

    [Test]
    public void AudioManager_Initializes_Instance_Correctly()
    {
        Assert.IsNotNull(AudioManager.Instance);
        Assert.AreEqual(_audioManager, AudioManager.Instance);
    }

    [Test]
    public void VolumeSettings_ClampAndScaleCorrectly()
    {
        _audioManager.SetMasterVolume(0.5f);
        _audioManager.SetMusicVolume(0.8f);
        _audioManager.SetSFXVolume(0.6f);
        _audioManager.SetEngineVolume(0.4f);

        Assert.AreEqual(0.5f, _audioManager.MasterVolume, 0.001f);
        Assert.AreEqual(0.8f, _audioManager.MusicVolume, 0.001f);
        Assert.AreEqual(0.6f, _audioManager.SFXVolume, 0.001f);
        Assert.AreEqual(0.4f, _audioManager.EngineVolume, 0.001f);

        Assert.AreEqual(0.5f * 0.8f, _audioManager.MusicEffectiveVolume, 0.001f);
        Assert.AreEqual(0.5f * 0.6f, _audioManager.SFXEffectiveVolume, 0.001f);
        Assert.AreEqual(0.5f * 0.4f, _audioManager.EngineEffectiveVolume, 0.001f);
    }

    [Test]
    public void Play3D_AllocatesAndPositionsSource()
    {
        AudioClip testClip = AudioClip.Create("TestClip", 44100, 1, 44100, false);
        Vector3 targetPos = new Vector3(10f, 20f, 0f);

        AudioSource src = _audioManager.Play3D(testClip, targetPos, 0.8f, 0f);

        Assert.IsNotNull(src);
        Assert.AreEqual(targetPos, src.transform.position);
        Assert.AreEqual(1.0f, src.spatialBlend, 0.001f);
        Assert.AreEqual(testClip, src.clip);

        Object.DestroyImmediate(testClip);
    }

    [Test]
    public void Play2D_ExecutesWithoutError()
    {
        AudioClip testClip = AudioClip.Create("TestClip2D", 44100, 1, 44100, false);
        Assert.DoesNotThrow(() => _audioManager.Play2D(testClip, 0.5f));
        Object.DestroyImmediate(testClip);
    }

    [Test]
    public void PlayMusic_And_StopMusic_ExecuteWithoutError()
    {
        AudioClip musicA = AudioClip.Create("MusicA", 44100, 1, 44100, false);
        AudioClip musicB = AudioClip.Create("MusicB", 44100, 1, 44100, false);

        Assert.DoesNotThrow(() => _audioManager.PlayMusic(musicA, 0f));
        Assert.DoesNotThrow(() => _audioManager.PlayMusic(musicB, 0f));
        Assert.DoesNotThrow(() => _audioManager.PauseMusic());
        Assert.DoesNotThrow(() => _audioManager.ResumeMusic());
        Assert.DoesNotThrow(() => _audioManager.StopMusic(0f));

        Object.DestroyImmediate(musicA);
        Object.DestroyImmediate(musicB);
    }

    [Test]
    public void ShipEngineAudio_ConfiguresPlayerAndEnemySpatialBlend()
    {
        GameObject shipGo = new GameObject("TestShip");
        shipGo.AddComponent<Rigidbody>();
        ShipEngineAudio engine = shipGo.AddComponent<ShipEngineAudio>();

        // Player: 2D stereo (spatialBlend = 0)
        engine.IsPlayer = true;
        Assert.AreEqual(0f, engine.Source.spatialBlend, 0.001f);

        // Enemy: 3D spatial (spatialBlend = 1)
        engine.IsPlayer = false;
        Assert.AreEqual(1f, engine.Source.spatialBlend, 0.001f);

        Object.DestroyImmediate(shipGo);
    }

    [Test]
    public void ShipEngineAudio_ModulatesThrottle()
    {
        GameObject shipGo = new GameObject("TestShipThrottle");
        shipGo.AddComponent<Rigidbody>();
        ShipEngineAudio engine = shipGo.AddComponent<ShipEngineAudio>();

        engine.SetCustomThrottle(1.0f);
        Assert.IsNotNull(engine.Source);

        Object.DestroyImmediate(shipGo);
    }
}
