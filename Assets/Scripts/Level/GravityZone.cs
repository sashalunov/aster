using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A physics volume that continuously pulls or pushes Rigidbodies that enter it.
/// Can act as a directional wind/gravity field or a point-based gravity well (like a black hole).
/// </summary>
[RequireComponent(typeof(Collider))]
public class GravityZone : MonoBehaviour
{
    public enum GravityType
    {
        /// <summary>Pulls objects in a uniform direction.</summary>
        Directional,
        /// <summary>Pulls objects toward a specific center point.</summary>
        Point
    }

    [Header("Gravity Core Settings")]
    [Tooltip("Whether to pull uniformly in a direction or toward a point.")]
    public GravityType gravityType = GravityType.Directional;

    [Tooltip("Base magnitude of the continuous force applied.")]
    public float forceMagnitude = 10f;

    [Tooltip("Physics force mode used. 'Force' is mass-dependent continuous, 'Acceleration' ignores mass.")]
    public ForceMode forceMode = ForceMode.Force;

    [Header("Directional Settings (If Directional)")]
    [Tooltip("The direction of the pull. Will be automatically normalized.")]
    public Vector3 pullDirection = Vector3.down;

    [Tooltip("If true, pullDirection is relative to this object's rotation. If false, it's global.")]
    public bool useLocalDirection = false;

    [Header("Point Settings (If Point)")]
    [Tooltip("The center point of gravity. If null, uses this GameObject's transform.position.")]
    public Transform pointCenter;

    [Tooltip("If greater than 0, force attenuates to 0 at this distance from the center. If 0, force is constant everywhere.")]
    [Min(0f)]
    public float attenuationRadius = 0f;

    [Header("Target Filters")]
    [Tooltip("Apply gravity to regular detached blocks.")]
    public bool affectBlocks = true;

    [Tooltip("Apply gravity to whole asteroids (clusters or cores).")]
    public bool affectAsteroids = true;

    [Tooltip("Apply gravity to the player ship.")]
    public bool affectPlayer = false;

    private readonly HashSet<Rigidbody> _activeBodies = new HashSet<Rigidbody>();

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    private void FixedUpdate()
    {
        // Clean up destroyed or deactivated bodies
        _activeBodies.RemoveWhere(rb => rb == null || !rb.gameObject.activeInHierarchy);

        foreach (Rigidbody rb in _activeBodies)
        {
            ApplyGravity(rb);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        if (IsValidTarget(other))
        {
            _activeBodies.Add(rb);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            _activeBodies.Remove(rb);
        }
    }

    private bool IsValidTarget(Collider other)
    {
        if (affectPlayer && other.CompareTag("Player"))
        {
            return true;
        }

        BlockBase block = other.GetComponentInParent<BlockBase>();
        AsteroidGrid grid = other.GetComponentInParent<AsteroidGrid>();
        AsteroidBase aster = other.GetComponentInParent<AsteroidBase>();

        bool isCore = false;
        if (block != null && block.IsCore) isCore = true;
        if (other.CompareTag("core")) isCore = true;

        if (affectAsteroids && (grid != null || aster != null || isCore))
        {
            return true;
        }

        if (affectBlocks && block != null && !isCore)
        {
            return true;
        }

        return false;
    }

    private void ApplyGravity(Rigidbody rb)
    {
        if (rb.isKinematic) return;

        Vector3 forceDir = Vector3.zero;
        float currentForce = forceMagnitude;

        if (gravityType == GravityType.Directional)
        {
            forceDir = useLocalDirection ? transform.TransformDirection(pullDirection) : pullDirection;
            forceDir.z = 0f;
            forceDir.Normalize();
        }
        else if (gravityType == GravityType.Point)
        {
            Vector3 center = pointCenter != null ? pointCenter.position : transform.position;
            center.z = 0f;
            Vector3 targetPos = rb.position;
            targetPos.z = 0f;

            Vector3 offset = center - targetPos;
            float dist = offset.magnitude;
            
            if (attenuationRadius > 0f)
            {
                if (dist > attenuationRadius) return; // Outside attenuation range
                
                // Linear falloff: 100% force at center, 0% at attenuationRadius
                currentForce *= (1f - (dist / attenuationRadius));
            }

            if (dist > 0.001f)
            {
                forceDir = offset.normalized;
            }
        }

        if (forceDir.sqrMagnitude > 0.001f)
        {
            rb.AddForce(forceDir * currentForce, forceMode);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.2f);
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.8f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.8f);
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }

    private void OnDrawGizmosSelected()
    {
        if (gravityType == GravityType.Directional)
        {
            Vector3 center = transform.position;
            Vector3 dir = useLocalDirection ? transform.TransformDirection(pullDirection) : pullDirection;
            dir.z = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                dir.Normalize();
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(center, dir * 3f);
                
                // Arrowhead
                Vector3 tip = center + dir * 3f;
                Vector3 right = Quaternion.Euler(0f, 0f, 150f) * dir * 0.5f;
                Vector3 left = Quaternion.Euler(0f, 0f, -150f) * dir * 0.5f;
                Gizmos.DrawRay(tip, right);
                Gizmos.DrawRay(tip, left);
            }
        }
        else if (gravityType == GravityType.Point)
        {
            Vector3 center = pointCenter != null ? pointCenter.position : transform.position;
            center.z = 0f;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(center, 0.5f);

            if (attenuationRadius > 0f)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
                Gizmos.DrawWireSphere(center, attenuationRadius);
            }
        }
    }
#endif
}
