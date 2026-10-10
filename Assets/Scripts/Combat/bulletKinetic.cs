using UnityEngine;

/// <summary>
/// Standard kinetic bullet projectile. Inherits from ProjectileBase.
/// Maintains backwards compatibility with all existing game systems, AsteroidBase, and TurretAI.
/// </summary>
public class bulletKinetic : ProjectileBase
{
    [Header("Visual Effects")]
    public ParticleSystem _trail;

    public override void Expire()
    {
        if (subMunitionEnabled && subMunitionPrefab != null && !_isDead)
        {
            GameObject blastObj = Instantiate(subMunitionPrefab, transform.position, Quaternion.identity);
            if (blastObj.TryGetComponent<HitBlast>(out var hitBlast))
            {
                hitBlast.Explode(this);
            }
        }
        base.Expire();
    }
}
