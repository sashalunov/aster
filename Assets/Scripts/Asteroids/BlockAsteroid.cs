using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Standard physical armor and body block within asteroid clusters and debris.
/// Inherits health management, compound Rigidbody physics, damage processing, and drop lifecycle from <see cref="BlockBase"/>.
/// Implements visual level text display and multi-grade material state based on remaining hits.
/// </summary>
[AddComponentMenu("Asteroids/Block Asteroid")]
public class BlockAsteroid : BlockBase
{
    [Header("Visuals & UI")]
    [Tooltip("Materials assigned to the block based on remaining hit points.")]
    public Material[] _grade_mats;

    [Tooltip("Text component displaying current hit points/level.")]
    public TMP_Text _tmp_lvl;

    // Encapsulated properties
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

    private MeshRenderer _meshRenderer;

    protected virtual void Awake()
    {
        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        if (_tmp_lvl == null)
        {
            _tmp_lvl = GetComponentInChildren<TMP_Text>();
        }
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
            _tmp_lvl.SetText(_hits.ToString("0.#"));
        }
    }

    /// <summary>
    /// Updates the mesh material to match the grade corresponding to current hits.
    /// Fast float evaluation and CeilToInt mapping for fractional damage states.
    /// </summary>
    protected virtual void UpdateMats()
    {
        if (_hits <= HEALTH_EPSILON || _grade_mats == null || _grade_mats.Length == 0) return;

        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        if (_meshRenderer == null) return;

        int matIndex = Mathf.CeilToInt(_hits) - 1;
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
