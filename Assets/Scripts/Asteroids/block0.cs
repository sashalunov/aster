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
    public int _hits = 1;
    public bool _dead = false;
    public bool _detached = false;
    public bool _bonus = false;

    // Events
    public event Action<block0, Transform, bullet1> OnCoreHit;
    public event Action<block0, Transform, bullet1> OnCoreDestroyed;

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
    public int block_receive_hit(Transform source, bullet1 b1, int customDamage = -1)
    {
        if (_dead) return 0;

        int incomingDamage = customDamage >= 0 ? customDamage : (b1 != null ? b1._hit_damage : 1);
        var newhits = _hits - incomingDamage;
        var newhitdamage = incomingDamage - _hits;
        if (newhitdamage < 0) newhitdamage = 0;

        var damage = incomingDamage >= _hits ? _hits : incomingDamage;

        // Damage FX & XP Awarding
        if (damage > 0)
        {
            string popupResource = isCore ? "show_corehit" : "show_blockhit";
            var hitFx = Resources.Load(popupResource) ?? Resources.Load("show_blockhit");
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

            player p = (b1 != null && b1._player != null) ? b1._player : null;
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

            if (p != null)
            {
                p.AddXP(damage, transform);
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

            var destroyFx = Resources.Load("blockdestroy");
            if (destroyFx != null)
            {
                Instantiate(destroyFx, deathPos, Quaternion.identity);
            }

            // Bonus and Core Drops
            if (_bonus || wasCore)
            {
                string dropResource = wasCore ? "powerup_shield" : "powerup";
                var pwrupFx = Resources.Load(dropResource) ?? Resources.Load("powerup");
                if (pwrupFx != null)
                {
                    GameObject rndbonus = Instantiate(pwrupFx, deathPos, Quaternion.identity) as GameObject;
                    if (rndbonus != null)
                    {
                        powerup pup = rndbonus.GetComponent<powerup>();
                        if (pup != null)
                        {
                            pup._type = wasCore ? powerup.PowerupType.shield : (powerup.PowerupType)UnityEngine.Random.Range(0, 3);
                        }
                    }
                }
            }

            if (wasCore && parentAst != null)
            {
                parentAst.core_destruct(b1);
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
            mr.material = _grade_mats[_hits - 1];
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
        var bonusPrefab = Resources.Load("additive_bonus");
        if (bonusPrefab != null)
        {
            GameObject bonus = Instantiate(bonusPrefab, new Vector3(transform.position.x, transform.position.y, -0.294f), Quaternion.identity) as GameObject;
            if (bonus != null) bonus.transform.parent = gameObject.transform;
        }
    }

    public void EndLife(Transform newparent)
    {
        transform.parent = newparent;
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
        if ((isCore || gameObject.name == "core_block" || CompareTag("core")) && !_dead)
        {
            _dead = true;
            OnCoreDestroyed?.Invoke(this, null, null);
            AsteroidBase parentAst = GetComponentInParent<AsteroidBase>();
            if (parentAst != null)
            {
                parentAst.core_destruct(null);
            }
        }
    }
}
