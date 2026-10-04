using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Physical block component used for perimeter armor and central anchor cores in asteroid clusters.
/// Supports HP tracking, multi-grade material visual state, bonus drops, and core lifecycle events.
/// </summary>
public class block0 : MonoBehaviour
{
    [Header("Core Status")]
    [Tooltip("If true, this block acts as the central anchor core of an asteroid cluster.")]
    public bool isCore = false;

    [Header("Visuals & UI")]
    public Material[] _grade_mats;
    public TMP_Text _tmp_lvl;

    [Header("Block State")]
    public int _level = 0;
    public float _hits = 1;
    public bool _dead = false;
    public bool _detached = false;
    public bool _bonus = false;

    // Events
    public event Action<block0, Transform, ProjectileBase> OnCoreHit;
    public event Action<block0, Transform, ProjectileBase> OnCoreDestroyed;
    public event Action<block0, Transform, ProjectileBase> OnBlockHit;
    public event Action<block0, Transform, ProjectileBase> OnBlockDestroyed;

    void Start()
    {
        UpdateText();
        UpdateMats();

        AsteroidBase parentAsteroid = GetComponentInParent<AsteroidBase>();
        if (parentAsteroid != null)
        {
            // Part of an asteroid compound body: strip any local Rigidbody so PhysX compound hierarchy works correctly
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(rb);
                else
                    Destroy(rb);
#else
                Destroy(rb);
#endif
            }
        }
        else
        {
            // Standalone floating debris: ensure it has its own Rigidbody
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            rb.useGravity = false;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.mass = _hits > 0 ? _hits : 1;
        }

        if (isCore || gameObject.name == "core_block" || CompareTag("core"))
        {
            isCore = true;
            gameObject.tag = "core";
        }
    }

    /// <summary>
    /// Receives projectile or impact hit, applies damage, triggers popup FX and XP, and handles destruction.
    /// </summary>
    public float block_receive_hit(Transform source, ProjectileBase b1, float customDamage = -1)
    {
        if (_dead) return 0;

        float incomingDamage = customDamage >= 0 ? customDamage : (b1 != null ? b1.Damage : 1);
        var newhits = _hits - incomingDamage;
        var newhitdamage = incomingDamage - _hits;
        if (newhitdamage < 0) newhitdamage = 0;

        var damage = incomingDamage >= _hits ? _hits : incomingDamage;

        // Resolve player reference
        player p = null;
        if (p == null && source != null)
        {
            p = source.GetComponent<player>() ?? source.GetComponentInParent<player>();
            if (p == null)
            {
                ProjectileBase pb = source.GetComponent<ProjectileBase>();
                if (pb != null && pb.Owner != null)
                {
                    p = pb.Owner.GetComponent<player>() ?? pb.Owner.GetComponentInParent<player>();
                }
            }
        }

        // Damage FX & XP Awarding
        if (damage > 0)
        {
            GameObject hitFx = PrefabManager.Get(isCore ? PrefabId.CoreHitFx : PrefabId.BlockHitFx);
            if (hitFx != null)
            {
                var popup = Instantiate(hitFx, transform.position, Quaternion.identity) as GameObject; 
                if (popup != null)
                {
                    var tmp = popup.GetComponentInChildren<TextMeshPro>();
                    if (tmp != null) tmp.SetText("+" + damage.ToString());
                   // popup.transform.localScale = isCore ? new Vector3(1.5f, 1.5f, 1.2f) : new Vector3(1.3f, 1.3f, 1.15f);
                }
            }

            if (p != null)
            {
                p.AddXP((int)damage, transform);
            }
        }

        // Destruction Check
        if (newhits < 1)
        {
            _dead = true;
            bool wasCore = isCore || gameObject.name == "core_block" || CompareTag("core");
            Vector3 deathPos = transform.position;
            AsteroidBase parentAst = GetComponentInParent<AsteroidBase>();

            if (wasCore)
            {
                OnCoreDestroyed?.Invoke(this, source, b1);
            }
            else
            {
                bool hasSubscribers = OnBlockDestroyed != null;
                OnBlockDestroyed?.Invoke(this, source, b1);
                if (!hasSubscribers && parentAst != null)
                {
                    Vector3 impactImpulse = Vector3.zero;
                    if (b1 != null)
                    {
                        Vector3 bulletDir = b1.transform.up;
                        Rigidbody b1Rb = b1.GetComponent<Rigidbody>();
                        if (b1Rb != null && b1Rb.linearVelocity.sqrMagnitude > 0.001f)
                        {
                            bulletDir = b1Rb.linearVelocity.normalized;
                        }
                        bulletDir.z = 0f;
                        impactImpulse = bulletDir * Mathf.Max(b1.Damage, 1f);
                    }
                    else if (source != null)
                    {
                        Vector3 pushDir = (deathPos - source.position).normalized;
                        pushDir.z = 0f;
                        impactImpulse = pushDir * 2.0f;
                    }
                    parentAst.check_for_unconnected(impactImpulse);
                }
            }

            GameObject destroyFx = PrefabManager.Get(PrefabId.BlockDestroyFx);
            if (destroyFx != null)
            {
                Instantiate(destroyFx, deathPos, Quaternion.identity);
            }

            // Bonus and Core Drops
            bool shouldDropDirectly = !wasCore || parentAst == null;
            if (shouldDropDirectly)
            {
                if (PowerupManager.Instance != null)
                {
                    PowerupManager.Instance.HandleBlockDestructionDrop(deathPos, wasCore, p, _bonus);
                }
                else if (_bonus || wasCore)
                {
                    GameObject pwrupFx = PrefabManager.Get(wasCore ? PrefabId.PowerupShield : PrefabId.PowerupDefault);
                    if (pwrupFx != null)
                    {
                        GameObject rndbonus = Instantiate(pwrupFx, deathPos, Quaternion.identity) as GameObject;
                        if (rndbonus != null)
                        {
                            StandardPowerup pup = rndbonus.GetComponent<StandardPowerup>();
                            if (pup != null)
                            {
                                pup.Type = wasCore ? StandardPowerup.StandardType.ShieldUp : (StandardPowerup.StandardType)UnityEngine.Random.Range(0, 4);
                            }
                        }
                    }
                }
            }

            if (wasCore && parentAst != null)
            {
                //parentAst.core_destruct(b1);
            }

            if (this != null && gameObject != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(gameObject);
                else
                    Destroy(gameObject);
#else
                Destroy(gameObject);
#endif
            }

            return newhitdamage;
        }

        _hits = newhits;

        if (isCore)
        {
            OnCoreHit?.Invoke(this, source, b1);
        }
        else
        {
            OnBlockHit?.Invoke(this, source, b1);
        }

        UpdateMats();
        UpdateText();

        return newhitdamage;
    }

    void UpdateText()
    {
        if (_tmp_lvl != null)
        {
            _tmp_lvl.SetText(_hits.ToString());
        }
    }

    void UpdateMats()
    {
        if (_hits < 1 || _grade_mats == null || _grade_mats.Length == 0) return;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;

        if (_hits > _grade_mats.Length)
        {
            mr.material = _grade_mats[_grade_mats.Length - 1];
        }
        else
        {
            mr.material = _grade_mats[(int)_hits - 1];
        }
    }

    public void SetHits(int newhits)
    {
        _hits = newhits;
        UpdateText();
        UpdateMats();
    }

    public void AddBonus()
    {
        _bonus = true;
        GameObject bonusPrefab = PrefabManager.Get(PrefabId.AdditiveBonusFx);
        if (bonusPrefab != null)
        {
            GameObject bonus = Instantiate(bonusPrefab, new Vector3(transform.position.x, transform.position.y, -0.294f), Quaternion.identity) as GameObject;
            if (bonus != null) bonus.transform.parent = gameObject.transform;
        }
    }

    public void EndLife(Transform newparent)
    {
        try
        {
            transform.parent = newparent;
        }
        catch (Exception)
        {
            transform.parent = null;
        }

        Rigidbody rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezePositionZ;
        rb.mass = _hits > 0 ? _hits : 1f;
        rb.useGravity = false;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        _detached = true;
    }

    void OnDestroy()
    {
        if (!Application.isPlaying || !gameObject.scene.isLoaded) return;

        bool wasCore = isCore || gameObject.name == "core_block" || CompareTag("core");
        if (wasCore && !_dead)
        {
            _dead = true;
            OnCoreDestroyed?.Invoke(this, null, null);
            AsteroidBase parentAst = GetComponentInParent<AsteroidBase>();
            if (parentAst != null && !parentAst.isDestructing)
            {
                parentAst.core_destruct(null);
            }
        }
        else if (!wasCore && !_dead)
        {
            _dead = true;
            bool hasSubscribers = OnBlockDestroyed != null;
            OnBlockDestroyed?.Invoke(this, null, null);
            if (!hasSubscribers)
            {
                AsteroidBase parentAst = GetComponentInParent<AsteroidBase>();
                if (parentAst != null && !parentAst.isDestructing)
                {
                    parentAst.check_for_unconnected();
                }
            }
        }
    }
}
