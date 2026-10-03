using UnityEngine;

/// <summary>
/// Standard kinetic bullet projectile. Inherits from ProjectileBase.
/// Maintains backwards compatibility with all existing game systems, AsteroidBase, and TurretAI.
/// </summary>
public class bullet1 : ProjectileBase
{
    [Header("Visual Effects")]
    public ParticleSystem _trail;

    // Legacy fields & properties for backwards compatibility
    public player _player
    {
        get => owner != null ? (owner.GetComponent<player>() ?? owner.GetComponentInParent<player>()) : null;
        set => SetOwner(value != null ? value.gameObject : null);
    }

    public int _hit_damage
    {
        get => damage;
        set => damage = value;
    }

    public float bullet_mass
    {
        get => mass;
        set => mass = value;
    }

    public bool isDead
    {
        get => _isDead;
        set => _isDead = value;
    }
}
