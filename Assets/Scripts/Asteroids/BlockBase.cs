using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Abstract base class for all physical blocks within asteroid clusters and floating space debris.
/// Encapsulates health tracking, compound Rigidbody physics lifecycle, damage reception,
/// player XP attribution, and virtual hooks for destruction and visuals.
/// </summary>
public abstract class BlockBase : MonoBehaviour
{
    [Header("Core Status")]
    [Tooltip("If true, this block acts as the central anchor core of an asteroid cluster.")]
    public bool isCore = false;

    [Header("Block State")]
    public int _level = 1;
    public float _hits = 1f;
    public float _initialHits = 1f;
    public bool _dead = false;
    public bool _detached = false;
    public bool _bonus = false;

    // Encapsulated Properties
    public virtual bool IsCore => isCore;
    public float Hits => _hits;
    public float InitialHits => _initialHits;
    public bool IsDead => _dead;
    public bool IsDetached { get => _detached; set => _detached = value; }
    public bool HasBonus { get => _bonus; set => _bonus = value; }
    public int Level
    {
        get => _level > 0 ? _level : 1;
        set => _level = Mathf.Max(1, value);
    }

    // Polymorphic Block Events
    public event Action<BlockBase, Transform, ProjectileBase> OnHit;
    public event Action<BlockBase, Transform, ProjectileBase> OnDestroyed;

    /// <summary>
    /// Epsilon threshold for float-based zero health checks.
    /// Blocks with remaining hits/health less than 0.1f are destroyed.
    /// </summary>
    public const float HEALTH_EPSILON = 0.1f;

    protected virtual void Start()
    {
        if (_initialHits < _hits)
        {
            _initialHits = _hits;
        }
        if (_level < 1)
        {
            _level = 1;
        }

        ConfigurePhysics();
        UpdateVisuals();

        if (isCore || gameObject.name == "core_block" || CompareTag("core"))
        {
            isCore = true;
            gameObject.tag = "core";
        }
    }

    /// <summary>
    /// Configures Rigidbody state based on whether this block is part of an asteroid compound body
    /// or floating as standalone debris in space.
    /// </summary>
    public virtual void ConfigurePhysics()
    {
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
            // Standalone floating debris: ensure it has its own Rigidbody with planar constraints
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            rb.useGravity = false;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 0.05f;
            rb.mass = _hits > 0 ? _hits : 1f;
        }
    }

    /// <summary>
    /// Receives projectile or impact hit, calculates damage, triggers popup FX and XP, and handles destruction.
    /// </summary>
    /// <param name="source">Source transform that caused the hit.</param>
    /// <param name="b1">Optional bullet component for projectile metadata.</param>
    /// <param name="customDamage">Optional override damage amount. If negative, defaults to projectile damage or 1.</param>
    /// <returns>Excess remaining damage from penetrating impacts.</returns>
    public virtual float block_receive_hit(Transform source, ProjectileBase b1, float customDamage = -1f)
    {
        if (_dead) return 0f;

        float incomingDamage = customDamage >= 0f ? customDamage : (b1 != null ? b1.Damage : 1f);
        float newhits = _hits - incomingDamage;
        float newhitdamage = Mathf.Max(0f, incomingDamage - _hits);
        float damage = incomingDamage >= _hits ? _hits : incomingDamage;

        player p = ResolvePlayer(source, b1);

        // Damage FX & Feedback
        if (damage > 0f)
        {
            //SpawnHitPopup(damage);

            // Play block impact/collision sound
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.PlayCollision(transform.position, Mathf.Max(damage * 3f, 2f));
            }
        }

        // Fast float-based destruction check optimized for thousands of blocks
        if (newhits <= HEALTH_EPSILON)
        {
            _dead = true;
            Vector3 deathPos = transform.position;
            AsteroidBase parentAst = GetComponentInParent<AsteroidBase>();

            HandleDestruction(source, b1, deathPos, parentAst, p);

            DestroyBlockObject();
            return newhitdamage;
        }

        _hits = newhits;
        InvokeHitEvent(source, b1);
        UpdateVisuals();

        return newhitdamage;
    }

    /// <summary>
    /// Resolves player reference from bullet, direct source, or projectile owner.
    /// </summary>
    protected virtual player ResolvePlayer(Transform source, ProjectileBase b1)
    {
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
        return p;
    }

    /// <summary>
    /// Spawns floating damage text popup at the block position.
    /// </summary>
    protected virtual void SpawnHitPopup(float damage)
    {
        GameObject hitFx = PrefabManager.Get(IsCore ? PrefabId.CoreHitFx : PrefabId.BlockHitFx);
        if (hitFx != null)
        {
            GameObject popup = Instantiate(hitFx, transform.position, Quaternion.identity);
            if (popup != null)
            {
                TextMeshPro tmp = popup.GetComponentInChildren<TextMeshPro>();
                if (tmp != null) tmp.SetText(damage.ToString("0.#"));
            }
        }
    }

    /// <summary>
    /// Spawns floating XP text popup at the block position upon destruction.
    /// </summary>
    protected virtual void SpawnXpPopup(int xp)
    {
        GameObject hitFx = PrefabManager.Get(IsCore ? PrefabId.CoreHitFx : PrefabId.BlockHitFx);
        if (hitFx != null)
        {
            GameObject popup = Instantiate(hitFx, transform.position, Quaternion.identity);
            if (popup != null)
            {
                TextMeshPro tmp = popup.GetComponentInChildren<TextMeshPro>();
                if (tmp != null) tmp.SetText("+" + xp + " XP");
            }
        }
    }

    /// <summary>
    /// Handles destruction sequence, events, FX, drops, and asteroid notification.
    /// Can be overridden by subclasses (e.g. BlockCore vs BlockAsteroid vs BlockExplosive).
    /// </summary>
    protected virtual void HandleDestruction(Transform source, ProjectileBase b1, Vector3 deathPos, AsteroidBase parentAst, player p)
    {
        InvokeDestroyedEvent(source, b1);

        SpawnDestroyFx(deathPos);

        // Award Kill XP strictly after block destruction (based on mass * level)
        if (p != null)
        {
            float massHits = _initialHits > 0f ? _initialHits : Mathf.Max(1f, _hits);
            int killXP = Mathf.Max(1, Mathf.RoundToInt(massHits * Level));
            p.AddXP(killXP, transform);
            SpawnXpPopup(killXP);
        }

        HandleDrop(deathPos, parentAst, p);

        if (parentAst != null)
        {
            Vector3 impactImpulse = CalculateImpactImpulse(source, b1, deathPos);
            parentAst.check_for_unconnected(impactImpulse);
        }
        else
        {
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.UnregisterThreat(gameObject);
            }
        }
    }

    /// <summary>
    /// Calculates impact impulse vector for pushing disconnected debris upon block destruction.
    /// </summary>
    protected virtual Vector3 CalculateImpactImpulse(Transform source, ProjectileBase b1, Vector3 deathPos)
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
            impactImpulse = bulletDir * Mathf.Max(b1.CalculateBounceImpulse(), 1f);
        }
        else if (source != null)
        {
            Vector3 pushDir = (deathPos - source.position).normalized;
            pushDir.z = 0f;
            impactImpulse = pushDir * 2.0f;
        }
        return impactImpulse;
    }

    /// <summary>
    /// Spawns destruction particle FX.
    /// </summary>
    protected virtual void SpawnDestroyFx(Vector3 deathPos)
    {
        GameObject destroyFx = PrefabManager.Get(PrefabId.BlockDestroyFx);
        if (destroyFx != null)
        {
            Instantiate(destroyFx, deathPos, Quaternion.identity);
        }
       
    }

    /// <summary>
    /// Handles item and powerup drops upon block destruction.
    /// </summary>
    protected virtual void HandleDrop(Vector3 deathPos, AsteroidBase parentAst, player p)
    {
        bool wasCore = IsCore;
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
                    GameObject rndbonus = Instantiate(pwrupFx, deathPos, Quaternion.identity);
                    if (rndbonus != null)
                    {
                        StandardPowerup pup = rndbonus.GetComponent<StandardPowerup>();
                        if (pup != null)
                        {
                            StandardPowerup.StandardType[] fallbackTypes = { 
                                StandardPowerup.StandardType.XpUp, StandardPowerup.StandardType.ShieldUp, StandardPowerup.StandardType.UpgradePoint };

                            pup.Type = wasCore ? StandardPowerup.StandardType.ShieldUp : fallbackTypes[UnityEngine.Random.Range(0, fallbackTypes.Length)];
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Destroys the GameObject with appropriate editor/runtime guards.
    /// </summary>
    protected virtual void DestroyBlockObject()
    {
        if (this != null)
        {
            try
            {
                if (gameObject != null)
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
            }
            catch (MissingReferenceException)
            {
                // Ignored: already destroyed as part of parent hierarchy (e.g. core collapse)
            }
        }
    }

    /// <summary>
    /// Sets hit points and updates visuals.
    /// </summary>
    public virtual void SetHits(float newhits)
    {
        _hits = newhits;
        _initialHits = newhits;
        UpdateVisuals();
    }

    /// <summary>
    /// Attaches an additive bonus visual marker and flags block for guaranteed bonus drop.
    /// </summary>
    public virtual void AddBonus()
    {
        _bonus = true;
        GameObject bonusPrefab = PrefabManager.Get(PrefabId.AdditiveBonusFx);
        if (bonusPrefab != null)
        {
            GameObject bonus = Instantiate(bonusPrefab, new Vector3(transform.position.x, transform.position.y, -0.294f), Quaternion.identity);
            if (bonus != null) bonus.transform.parent = gameObject.transform;
        }
    }

    /// <summary>
    /// Detaches the block from an asteroid compound body, reparenting it and restoring its standalone Rigidbody.
    /// Strictly enforces unit Vector3.one scale to prevent scale distortion upon detachment.
    /// </summary>
    public virtual void EndLife(Transform newparent)
    {
        try
        {
            transform.SetParent(newparent, true);
        }
        catch (Exception)
        {
            transform.SetParent(null, true);
        }

        transform.localScale = Vector3.one;

        Rigidbody rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezePositionZ;
        rb.mass = _hits > 0f ? _hits : 1f;
        rb.useGravity = false;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        _detached = true;
    }

    /// <summary>
    /// Hook for updating block visual appearance (materials, text, UI).
    /// </summary>
    public virtual void UpdateVisuals() { }

    protected virtual void InvokeHitEvent(Transform source, ProjectileBase  b1)
    {
        OnHit?.Invoke(this, source, b1);
    }

    protected virtual void InvokeDestroyedEvent(Transform source, ProjectileBase b1)
    {
        OnDestroyed?.Invoke(this, source, b1);
    }

    protected virtual void OnDestroy()
    {
        if (!Application.isPlaying || !gameObject.scene.isLoaded) return;

        if (!_dead)
        {
            _dead = true;
            InvokeDestroyedEvent(null, null);

            AsteroidBase parentAst = GetComponentInParent<AsteroidBase>();
            if (parentAst != null && !parentAst.isDestructing)
            {
                if (IsCore)
                {
                    parentAst.core_destruct(null);
                }
                else
                {
                    parentAst.check_for_unconnected(Vector3.zero);
                }
            }
            else if (!IsCore && WaveManager.Instance != null)
            {
                WaveManager.Instance.UnregisterThreat(gameObject);
            }
        }
    }
}
