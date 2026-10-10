using UnityEngine;

public class HitBlast : HitBase
{
    [Header("Sub-Munition Settings")]
    [Tooltip("Radius for the sub-munition area-of-effect damage and gravity push.")]
    public float blastRadius = 3f;
    public float blastDamage = 1f;
    [Tooltip("Duration the GravityZone remains active after impact.")]
    public float blastDuration = 0.5f;

    [Tooltip("Multiplier for the outward push force of the GravityZone.")]
    public float blastPushMultiplier = 50f;
    public float blastDamageMultiplier = 1f;
    /// <summary>
    /// Trigger the blast effect.
    /// </summary>
    public void Explode(ProjectileBase parent)
    {
        ApplyAreaDamage(parent);
        SetupGravityZone(parent);
        
        // Auto-destroy the sub-munition blast effect and gravity zone
#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(gameObject);
        else
            Destroy(gameObject, blastDuration);
#else
        Destroy(gameObject, blastDuration);
#endif
    }

    private void ApplyAreaDamage(ProjectileBase parent)
    {
        var lightSource = GetComponentInChildren<Light>();
        if (lightSource != null)
        {
            lightSource.enabled = true;
            lightSource.intensity = blastDamage * 2f;
            lightSource.range = blastRadius * 2f;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius);
        foreach (Collider col in hits)
        {
            if (col.isTrigger) continue; // Only process solid colliders and cores
            if (parent != null && parent.Owner != null && (col.gameObject == parent.Owner || col.transform.IsChildOf(parent.Owner.transform))) continue;

            float dist = Vector3.Distance(transform.position, col.ClosestPoint(transform.position));
            float damagePercent = 1f - Mathf.Clamp01(dist / blastRadius);
            float aoeDamage = blastDamage * damagePercent * blastDamageMultiplier;

            if (aoeDamage <= 0) continue;

            player hitPlayer = col.GetComponent<player>() ?? col.GetComponentInParent<player>();
            if (hitPlayer != null)
            {
                Vector3 hitDir = (col.transform.position - transform.position).normalized;
                hitPlayer.TakeDamage(aoeDamage, hitDir, col.ClosestPoint(transform.position), 0f);
                continue;
            }

            BlockBase hitBlock = col.GetComponent<BlockBase>() ?? col.GetComponentInParent<BlockBase>();
            if (hitBlock != null)
            {
                hitBlock.block_receive_hit(transform, parent, aoeDamage);
                if (hitBlock != null && !hitBlock.IsDead && hitBlock.Hits < 0.1f)
                {
                    hitBlock.block_receive_hit(transform, parent, hitBlock.Hits);
                }
                continue;
            }

            if (col.CompareTag("core"))
            {
                AsteroidBase astBase = col.GetComponent<AsteroidBase>() ?? col.GetComponentInParent<AsteroidBase>();
                if (astBase != null)
                {
                    astBase.core_receive_hit(transform, parent);
                }
            }
        }
    }

    private void SetupGravityZone(ProjectileBase parent)
    {
        // Ensure there is a trigger collider for the GravityZone
        if (!TryGetComponent<Collider>(out Collider existingCol))
        {
            var sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = blastRadius;
        }
        else
        {
            existingCol.isTrigger = true;
            if (existingCol is SphereCollider sphere)
            {
                sphere.radius = blastRadius;
            }
        }

        // Setup GravityZone for outward push
        if (!TryGetComponent<GravityZone>(out var gz))
        {
            gz = gameObject.AddComponent<GravityZone>();
        }

        gz.gravityType = GravityZone.GravityType.Point;
        gz.forceMagnitude = -blastDamage * blastPushMultiplier; // Negative for outward push
        gz.forceMode = ForceMode.Force;
        gz.attenuationRadius = blastRadius;
        gz.affectAsteroids = true;
        gz.affectBlocks = true;
        gz.affectPlayer = true;
        gz.pointCenter = transform;
    }
}
