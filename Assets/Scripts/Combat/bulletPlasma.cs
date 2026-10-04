using UnityEngine;

/// <summary>
/// Standard plasma bullet projectile. Inherits from ProjectileBase.
/// Maintains backwards compatibility with all existing game systems, AsteroidBase, and TurretAI.
/// </summary>
public class bulletPlasma : ProjectileBase
{
    [Header("Visual Effects")]
    public ParticleSystem _trail;
}
