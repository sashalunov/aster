using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Specialized central anchor core block for asteroid clusters.
/// Inherits health management, compound Rigidbody physics, damage processing, and drop lifecycle from <see cref="BlockBase"/>.
/// Enforces core identification, tag assignment, ambient core particle effects, and multi-grade visual updates.
/// Destruction triggers total structural collapse of the parent asteroid cluster.
/// </summary>
[AddComponentMenu("Asteroids/Block Asteroid Core")]
public class BlockAsteroidCore : BlockBase
{
    [Header("Visuals & UI")]
    [Tooltip("Materials assigned to the core block based on remaining hit points.")]
    public Material[] _grade_mats;

    [Tooltip("Text component displaying current hit points/level.")]
    public TMP_Text _tmp_lvl;

    [Tooltip("Particle system attached to the core representing active internal energy.")]
    public ParticleSystem _corePfx;

    // Encapsulated properties
    public override bool IsCore => true;

    public Material[] GradeMaterials
    {
        get => _grade_mats;
        set
        {
            _grade_mats = value;
            UpdateVisuals();
        }
    }

    public TMP_Text LevelText
    {
        get => _tmp_lvl;
        set
        {
            _tmp_lvl = value;
            UpdateVisuals();
        }
    }

    public ParticleSystem CoreParticleEffect
    {
        get => _corePfx;
        set => _corePfx = value;
    }

    private MeshRenderer _meshRenderer;

    public BlockAsteroidCore()
    {
        isCore = true;
        _hits = 3f;
    }

    protected virtual void Awake()
    {
        isCore = true;

        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        if (_tmp_lvl == null)
        {
            _tmp_lvl = GetComponentInChildren<TMP_Text>();
        }

        if (_corePfx == null)
        {
            _corePfx = GetComponentInChildren<ParticleSystem>(true);
        }

        if (_corePfx != null && !_corePfx.gameObject.activeSelf)
        {
            _corePfx.gameObject.SetActive(true);
        }
    }

    protected override void Start()
    {
        isCore = true;
        if (!CompareTag("core"))
        {
            gameObject.tag = "core";
        }

        base.Start();
    }

    /// <summary>
    /// Updates both hit point text and material grade visuals.
    /// </summary>
    public override void UpdateVisuals()
    {
        base.UpdateVisuals();
        UpdateText();
        UpdateMats();
    }

    /// <summary>
    /// Updates the TMP text overlay to reflect current remaining hits.
    /// </summary>
    protected virtual void UpdateText()
    {
        if (_tmp_lvl == null)
        {
            _tmp_lvl = GetComponentInChildren<TMP_Text>();
        }

        if (_tmp_lvl != null)
        {
            _tmp_lvl.SetText(_hits.ToString());
        }
    }

    /// <summary>
    /// Updates the mesh material to match the grade corresponding to current hits.
    /// </summary>
    protected virtual void UpdateMats()
    {
        if (_hits < 1 || _grade_mats == null || _grade_mats.Length == 0) return;

        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        if (_meshRenderer == null) return;

        int matIndex = (int)_hits - 1;
        if (_hits > _grade_mats.Length)
        {
            _meshRenderer.material = _grade_mats[_grade_mats.Length - 1];
        }
        else if (matIndex >= 0 && matIndex < _grade_mats.Length)
        {
            _meshRenderer.material = _grade_mats[matIndex];
        }
    }
}
