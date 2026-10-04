using System;
using UnityEngine;

/// <summary>
/// Abstract base class for all weapon projectiles (bullets, missiles, lasers, plasma bolts).
/// Provides modular hit detection, owner tracking, penetration math, and lifetime management.
/// Designed to support any shooter (player ships, enemy tanks, or turrets).
/// </summary>
public abstract class ProjectileBase : MonoBehaviour
{
    [Header("Base Projectile Specs")]
    [Tooltip("Base damage dealt on impact.")]
    [SerializeField] protected float damage = 1;

    [Tooltip("Physical mass / kinetic impact factor applied to targets.")]
    [SerializeField] protected float mass = 0.1f;

    [Tooltip("Number of target surfaces/blocks this projectile can penetrate before expiring.")]
    [SerializeField] protected int penetration = 1;

    [Tooltip("Time in seconds before the projectile self-destructs.")]
    [SerializeField] protected float lifetime = 5f;

     [SerializeField] protected float radius = 0.15f;

    [Header("Ownership & Targeting")]
    [Tooltip("The GameObject (ship, tank, turret) that fired this projectile.")]
    [SerializeField] protected GameObject owner;

    [SerializeField] protected bool ricochetEnabled = false;
    [SerializeField] protected bool subMunitionEnabled = false;
    [SerializeField] protected GameObject subMunitionPrefab;

    [Tooltip("Layer mask filter for valid target collisions.")]
    [SerializeField] protected LayerMask targetMask = 10;

    // Public properties
    public float Damage
    {
        get => damage;
        set => damage = value;
    }

    public float Mass
    {
        get => mass;
        set
        {
             mass = value;
            if (_rb != null)
            {
                _rb.mass = mass;
            } 
        }
    }

    public int Penetration
    {
        get => penetration;
        set => penetration = value;
    }

    public float Lifetime
    {
        get => lifetime;
        set => lifetime = value;
    }

    public float Radius
    {
        get => radius;
        set
        {
            radius = value;
            if (_col is SphereCollider sphereCol)
            {
                sphereCol.radius = radius;
            }
        }
    }

    public GameObject Owner
    {
        get => owner;
        set => SetOwner(value);
    }

    public bool RicochetEnabled
    {
        get => ricochetEnabled;
        set => ricochetEnabled = value;
    }
    public bool SubMunitionEnabled
    {
        get => subMunitionEnabled;
        set => subMunitionEnabled = value;
    }
    public bool IsSubMunition => subMunitionEnabled;
    public bool IsRicochet => ricochetEnabled;

    public bool IsDead => _isDead;

    // State
    protected bool _isDead = false;
    protected Rigidbody _rb;
    protected Collider _col;

    protected virtual void Awake()
    {
        EnsurePhysicsComponents();
    }

    protected virtual void EnsurePhysicsComponents()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
        }
            _rb.mass = mass;
            _rb.useGravity = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
        if (_col == null)
        {
            _col = gameObject.AddComponent<SphereCollider>();
        }
            (_col as SphereCollider).radius = radius;
            _col.isTrigger = true;
        
    }

     // Auto destroy bullet after configured lifetime
    protected virtual void Start()
    {
#if UNITY_EDITOR
        if (Application.isPlaying && lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
#else
        if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
#endif
    }

    /// <summary>
    /// Configures projectile owner and sets up physics ignore with the shooter's vessel.
    /// </summary>
    public virtual void SetOwner(GameObject newOwner)
    {
        owner = newOwner;
        if (owner == null) return;

        Collider myCol = _col != null ? _col : GetComponent<Collider>();
        if (myCol == null) return;

        Collider[] ownerCols = owner.GetComponentsInChildren<Collider>();
        foreach (var oc in ownerCols)
        {
            if (oc != null && oc.enabled)
            {
                Physics.IgnoreCollision(myCol, oc);
            }
        }
    }

    /// <summary>
    /// Convenient initialization method called immediately after instantiating.
    /// </summary>
    public virtual void Initialize(GameObject shooter)
    {
        SetOwner(shooter);
      
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (_isDead || other == null || other.isTrigger || other.CompareTag("powerup"))
        {
            return;
        }

        // 1. Check Player collision
        player hitPlayer = other.GetComponent<player>() ?? other.GetComponentInParent<player>();
        if (hitPlayer != null)
        {
            if (owner != null && (hitPlayer.gameObject == owner || other.transform.IsChildOf(owner.transform)))
            {
                // Friendly fire against self ignored
                return;
            }
            float bounceImpulse = mass * Penetration * Damage; // Example calculation for bounce impulse
            Vector3 hitDir = (hitPlayer.transform.position - transform.position).normalized;
            OnHitPlayer(hitPlayer, hitDir, bounceImpulse);
            return;
        }
        
        // 2. Check Asteroid Core collision
        if (other.CompareTag("core"))
        {
            block0 coreBlock = other.GetComponent<block0>();
            OnHitCore(coreBlock, other);
            return;
        }

        // 3. Check Asteroid Block collision
        if (other.CompareTag("block"))
        {
            block0 block = other.GetComponent<block0>();
            if (block != null)
            {
                OnHitBlock(block, other);
                return;
            }
        }

        // 4. Default / Obstacle hit
        OnHitGeneric(other);
    }

    protected virtual void OnHitPlayer(player target, Vector3 hitDir, float bounceImpulse = 0f)
    {
        target.TakeDamage(damage,   hitDir, transform.position, bounceImpulse);
        ConsumePenetration();
    }

    protected virtual void OnHitBlock(block0 block, Collider col)
    {
        float remainingDamage = block != null ? block.block_receive_hit(transform, this as bullet1, damage) : 0;
        ApplyImpactImpulse(col, damage + mass);

        damage = remainingDamage;
        if (remainingDamage <= 0)
        {
            ConsumePenetration();
        }
    }

    protected virtual void OnHitCore(block0 coreBlock, Collider col)
    {
        float remainingDamage = 0;
        if (coreBlock != null)
        {
            remainingDamage = coreBlock.block_receive_hit(transform, this as bullet1, damage);
        }
        else
        {
            AsteroidBase astBase = col.GetComponent<AsteroidBase>() ?? col.GetComponentInParent<AsteroidBase>();
            if (astBase != null)
            {
                remainingDamage = astBase.core_receive_hit(transform, this as bullet1);
            }
        }

        ApplyImpactImpulse(col, damage);
        damage = remainingDamage;

        if (remainingDamage <= 0)
        {
            ConsumePenetration();
        }
    }

    protected virtual void OnHitGeneric(Collider col)
    {
        ConsumePenetration();
    }

    protected virtual void ApplyImpactImpulse(Collider col, float impulseMagnitude)
    {
        Rigidbody targetRb = col.GetComponent<Rigidbody>() ?? col.GetComponentInParent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.AddForceAtPosition(transform.up * impulseMagnitude, transform.position, ForceMode.Impulse);
        }

        AsteroidBase astBase = col.GetComponentInParent<AsteroidBase>();
        if (astBase != null)
        {
            astBase.check_for_unconnected();
        }
    }

    protected virtual void ConsumePenetration()
    {
        penetration--;
        if (penetration <= 0)
        {
            Expire();
        }
    }

    /// <summary>
    /// Destroys the projectile upon running out of penetration or colliding.
    /// </summary>
    public virtual void Expire()
    {
        if (_isDead) return;
        _isDead = true;

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
