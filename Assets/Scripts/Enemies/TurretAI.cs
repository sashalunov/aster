using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A universal targeting and auto-aim controller.
/// Drives an attached Gun component, rotating towards and engaging targets based on the selected faction.
/// Can be configured as hostile (targeting Player) or friendly (targeting Asteroids and Blocks).
/// </summary>
public class TurretAI : MonoBehaviour
{
    public enum TargetFaction
    {
        Player,
        AsteroidsAndBlocks,
        Enemies
    }

    [Header("Faction & Targeting")]
    [Tooltip("What type of entities this turret should search for and attack.")]
    public TargetFaction targetFaction = TargetFaction.Player;

    [Tooltip("Maximum distance to detect and engage targets.")]
    public float detectionRange = 18f;

    [Tooltip("Explicit target transform. If null, automatically acquires targets within detection range.")]
    public Transform target;

    [Tooltip("Whether line of sight is required before shooting.")]
    public bool requireLineOfSight = false;

    [Tooltip("Layer mask representing obstacles that block line of sight.")]
    public LayerMask obstacleMask;

    [Header("Aiming Dynamics")]
    [Tooltip("The rotating sub-transform of the turret (turret0). If unassigned, auto-locates or uses the Gun's turret point.")]
    public Transform turretPart;

    [Tooltip("Restrict aiming and rotation to the 2D XY plane.")]
    public bool restrictToXYPlane = true;

    [Tooltip("Speed in degrees per second at which the turret rotates towards the target.")]
    public float rotationSpeed = 150f;

    [Tooltip("Maximum angle difference (in degrees) to consider the turret aligned enough to fire.")]
    public float aimTolerance = 12f;

    [Tooltip("Smoothly rotate towards the target instead of snapping immediately.")]
    public bool smoothAim = true;

    [Tooltip("Return to default idle rotation when no targets are in range.")]
    public bool returnToDefaultWhenIdle = true;

    [Tooltip("Visual aim cone reference (optional).")]
    public Transform aimCone;

    [Header("Weapon Integration")]
    [Tooltip("The Gun component this controller drives. If unassigned, automatically finds one on this GameObject or children.")]
    public Gun mountedGun;

    private Quaternion defaultLocalRotation;

    private void Awake()
    {
        AutoConfigureReferences();

        if (turretPart != null)
        {
            defaultLocalRotation = turretPart.localRotation;
        }

        if (mountedGun != null)
        {
            mountedGun.SetOwner(gameObject);
        }
    }

    private void Reset()
    {
        AutoConfigureReferences();
    }

    public void AutoConfigureReferences()
    {
        if (mountedGun == null)
        {
            mountedGun = GetComponent<Gun>() ?? GetComponentInChildren<Gun>();
        }

        if (turretPart == null)
        {
            if (mountedGun != null && mountedGun.TurretPoint != mountedGun.transform)
            {
                turretPart = mountedGun.TurretPoint;
            }
            else
            {
                turretPart = transform.Find("hull/turret0") ?? transform.Find("turret0");
            }
        }

        if (aimCone == null)
        {
            if (turretPart != null)
            {
                aimCone = turretPart.Find("Cone");
            }
            if (aimCone == null)
            {
                aimCone = transform.Find("hull/turret0/Cone");
            }
        }
    }

    private void Update()
    {
        EnsureTarget();

        bool hasTarget = target != null;
        bool inRange = false;
        bool isAimed = false;
        Vector3 targetDirection = Vector3.zero;

        if (hasTarget)
        {
            Vector3 myPos = turretPart != null ? turretPart.position : transform.position;
            targetDirection = target.position - myPos;

            if (restrictToXYPlane)
            {
                targetDirection.z = 0f;
            }

            float distance = targetDirection.magnitude;

            if (distance <= detectionRange)
            {
                if (requireLineOfSight)
                {
                    if (Physics.Raycast(myPos, targetDirection.normalized, out RaycastHit hit, distance, obstacleMask))
                    {
                        if (hit.transform == target || hit.transform.IsChildOf(target))
                        {
                            inRange = true;
                        }
                    }
                    else
                    {
                        inRange = true;
                    }
                }
                else
                {
                    inRange = true;
                }
            }
            else
            {
                // Lost target because it went out of range
                target = null;
            }
        }

        // Aiming logic
        if (inRange && turretPart != null && targetDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = CalculateAimRotation(targetDirection, restrictToXYPlane);

            if (smoothAim)
            {
                turretPart.rotation = Quaternion.RotateTowards(turretPart.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            else
            {
                turretPart.rotation = targetRotation;
            }

            // In Asteroids, transform.up is the forward/firing vector of the turret
            float angleDiff = Vector3.Angle(turretPart.up, targetDirection.normalized);
            isAimed = angleDiff <= aimTolerance;
        }
        else if (returnToDefaultWhenIdle && turretPart != null)
        {
            turretPart.localRotation = Quaternion.RotateTowards(turretPart.localRotation, defaultLocalRotation, (rotationSpeed * 0.5f) * Time.deltaTime);
        }

        // Shooting logic delegated to the Gun component
        if (inRange && isAimed && mountedGun != null)
        {
            mountedGun.TryFire();
        }
    }

    /// <summary>
    /// Calculates the target rotation for the turret so its up vector points along the target direction.
    /// </summary>
    public static Quaternion CalculateAimRotation(Vector3 direction, bool restrictTo2D = true)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Quaternion.identity;
        }

        if (restrictTo2D)
        {
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            return Quaternion.Euler(0f, 0f, targetAngle);
        }

        return Quaternion.FromToRotation(Vector3.up, direction.normalized);
    }

    /// <summary>
    /// Acquires a new valid target if one isn't currently assigned or within range.
    /// </summary>
    private void EnsureTarget()
    {
        // If current target is destroyed or inactive, clear it
        if (target != null && (!target.gameObject.activeInHierarchy))
        {
            target = null;
        }

        if (target != null) return;

        Vector3 myPos = turretPart != null ? turretPart.position : transform.position;

        switch (targetFaction)
        {
            case TargetFaction.Player:
                FindPlayerTarget();
                break;

            case TargetFaction.AsteroidsAndBlocks:
                FindAsteroidOrBlockTarget(myPos);
                break;

            case TargetFaction.Enemies:
                FindEnemyTarget(myPos);
                break;
        }
    }

    private void FindPlayerTarget()
    {
        player p = Object.FindAnyObjectByType<player>();
        if (p != null && !p.isDead)
        {
            target = p.transform;
            return;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
        }
    }

    private void FindAsteroidOrBlockTarget(Vector3 myPos)
    {
        Collider[] hits = Physics.OverlapSphere(myPos, detectionRange);
        float closestDist = float.MaxValue;
        Transform bestTarget = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || col.isTrigger) continue;

            bool isAsteroidTarget = false;

            if (col.CompareTag("core") || col.CompareTag("block"))
            {
                isAsteroidTarget = true;
            }
            else if (col.GetComponentInParent<BlockBase>() != null || col.GetComponentInParent<AsteroidBase>() != null)
            {
                isAsteroidTarget = true;
            }

            if (isAsteroidTarget)
            {
                Vector3 diff = col.transform.position - myPos;
                if (restrictToXYPlane) diff.z = 0f;
                float d = diff.sqrMagnitude;

                if (d < closestDist)
                {
                    closestDist = d;
                    bestTarget = col.transform;
                }
            }
        }

        target = bestTarget;
    }

    private void FindEnemyTarget(Vector3 myPos)
    {
        TurretAI[] allTurrets = Object.FindObjectsByType<TurretAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float closestDist = float.MaxValue;
        Transform bestTarget = null;

        for (int i = 0; i < allTurrets.Length; i++)
        {
            TurretAI other = allTurrets[i];
            if (other == null || other == this) continue;

            // Target hostiles that target the player
            if (other.targetFaction == TargetFaction.Player)
            {
                Vector3 diff = other.transform.position - myPos;
                if (restrictToXYPlane) diff.z = 0f;
                float d = diff.sqrMagnitude;

                if (d < closestDist)
                {
                    closestDist = d;
                    bestTarget = other.transform;
                }
            }
        }

        target = bestTarget;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = turretPart != null ? turretPart.position : transform.position;

        // Draw detection range
        Gizmos.color = targetFaction == TargetFaction.Player ? Color.red : Color.green;
        Gizmos.DrawWireSphere(center, detectionRange);

        // Draw aim direction
        if (turretPart != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(center, turretPart.up * Mathf.Min(detectionRange, 10f));
        }

        // Draw line to target if in range
        if (target != null)
        {
            Vector3 toTarget = target.position - center;
            if (restrictToXYPlane)
            {
                toTarget.z = 0f;
            }
            if (toTarget.magnitude <= detectionRange)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(center, target.position);
            }
        }
    }
}
