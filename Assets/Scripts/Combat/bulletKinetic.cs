using UnityEngine;

/// <summary>
/// Standard kinetic bullet projectile. Inherits from ProjectileBase.
/// Maintains backwards compatibility with all existing game systems, AsteroidBase, and TurretAI.
/// </summary>
public class bulletKinetic : ProjectileBase
{
    [Header("Visual Effects")]
    public ParticleSystem _trail;
}
