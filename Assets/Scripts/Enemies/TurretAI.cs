using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretAI : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Maximum distance to detect and engage the player")]
    public float detectionRange = 18f;

    [Tooltip("Target transform to aim and shoot at. If unassigned, automatically finds player in scene.")]
    public Transform target;

    [Tooltip("Whether line of sight is required before shooting")]
    public bool requireLineOfSight = false;

    [Tooltip("Layer mask representing obstacles that block line of sight")]
    public LayerMask obstacleMask;

    [Header("Aiming")]
    [Tooltip("The rotating part of the turret (turret0)")]
    public Transform turretPart;

    [Tooltip("Restrict aiming and bullet movement to the 2D XY plane. When false, turret aims and shoots freely in full 3D space.")]
    public bool restrictToXYPlane = false;

    [Tooltip("Speed in degrees per second at which the turret rotates towards target")]
    public float rotationSpeed = 150f;

    [Tooltip("Maximum angle difference (in degrees) to consider the turret aligned with target to fire")]
    public float aimTolerance = 12f;

    [Tooltip("Smoothly rotate towards target instead of snapping immediately")]
    public bool smoothAim = true;

    [Tooltip("Return to default idle rotation when no player is detected in range")]
    public bool returnToDefaultWhenIdle = true;

    [Tooltip("Visual aim cone reference (optional)")]
    public Transform aimCone;

    [Header("Shooting")]
    [Tooltip("Muzzle transform where bullets are spawned")]
    public Transform muzzlePoint;

    [Tooltip("Bullet prefab to spawn (defaults to bullet1)")]
    public GameObject bulletPrefab;

    [Tooltip("Muzzle flash FX prefab")]
    public GameObject muzzleFlashPrefab;

    [Tooltip("Number of shots per second")]
    public float fireRate = 1.2f;

    [Tooltip("Impulse force applied to the bullet")]
    public float fireForce = 1.2f;

    [Tooltip("Damage dealt to player per bullet hit")]
    public float bulletDamage = 1;

    [Tooltip("Lifetime of bullets in seconds before auto-destroy")]
    public float bulletLifetime = 5f;

    [Header("Effects & Audio")]
    public Animator animator;
    public AudioSource audioSource;
    public AudioClip fireSound;
    public string fireAnimationName = "urret_fire";

    
    private Quaternion defaultLocalRotation;
    private float fireTimer = 0f;
    private Collider[] myColliders;

    private void Awake()
    {
        AutoConfigureReferences();
        if (turretPart != null)
        {
            defaultLocalRotation = turretPart.localRotation;
        }
        myColliders = GetComponentsInChildren<Collider>();
    }

    private void Reset()
    {
        AutoConfigureReferences();
    }

    public void AutoConfigureReferences()
    {
        if (turretPart == null)
        {
            turretPart = transform.Find("hull/turret0");
            if (turretPart == null)
            {
                turretPart = transform.Find("turret0");
            }
        }

        if (muzzlePoint == null)
        {
            if (turretPart != null)
            {
                muzzlePoint = turretPart.Find("gun0/ps_muzzle");
            }
            if (muzzlePoint == null)
            {
                muzzlePoint = transform.Find("hull/turret0/gun0/ps_muzzle");
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

        if (animator == null)
        {
            if (turretPart != null)
            {
                animator = turretPart.GetComponent<Animator>();
            }
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (bulletPrefab == null)
        {
            bulletPrefab = PrefabManager.Get(PrefabId.BulletEnemy);
        }

        if (muzzleFlashPrefab == null)
        {
            muzzleFlashPrefab = PrefabManager.Get(PrefabId.MuzzleFlashFx);
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
                    RaycastHit hit;
                    if (Physics.Raycast(myPos, targetDirection.normalized, out hit, distance, obstacleMask))
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
        }

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

            float angleDiff = Vector3.Angle(turretPart.up, targetDirection.normalized);
            isAimed = angleDiff <= aimTolerance;
        }
        else if (returnToDefaultWhenIdle && turretPart != null)
        {
            turretPart.localRotation = Quaternion.RotateTowards(turretPart.localRotation, defaultLocalRotation, (rotationSpeed * 0.5f) * Time.deltaTime);
        }

        // Shooting logic
        fireTimer += Time.deltaTime;
        if (inRange && isAimed)
        {
            float fireInterval = fireRate > 0f ? (1f / fireRate) : 1f;
            if (fireTimer >= fireInterval)
            {
                fireTimer = 0f;
                Shoot();
            }
        }
    }

    /// <summary>
    /// Calculates the target rotation for the turret so its up vector aims along the target direction.
    /// </summary>
    public static Quaternion CalculateAimRotation(Vector3 direction, bool restrictTo2D = false)
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

    public void Shoot()
    {
        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : (turretPart != null ? turretPart.position : transform.position);
      
        Quaternion spawnRot = turretPart != null ? turretPart.rotation : transform.rotation;

        // Play recoil animation
        if (animator != null && !string.IsNullOrEmpty(fireAnimationName))
        {
            animator.Play(fireAnimationName, 0, 0f);
        }

        // Play fire sound
        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound);
        }

        // Spawn muzzle flash FX
        if (muzzleFlashPrefab != null && muzzlePoint != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
            if (Application.isPlaying)
            {
                Destroy(flash, 0.5f);
            }
            else
            {
                DestroyImmediate(flash);
            }
        }

        // Spawn bullet
        if (bulletPrefab != null)
        {
            GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, spawnRot);

            // Ignore collision with turret's own colliders
            Collider bulletCol = bulletObj.GetComponent<Collider>();
            if (bulletCol != null && myColliders != null)
            {
                for (int i = 0; i < myColliders.Length; i++)
                {
                    if (myColliders[i] != null)
                    {
                        Physics.IgnoreCollision(bulletCol, myColliders[i]);
                    }
                }
            }

            // Configure bullet damage and shooter reference
            ProjectileBase b1 = bulletObj.GetComponent<ProjectileBase>();
            if (b1 != null)
            {
                b1.Damage = bulletDamage;
                b1.Owner = gameObject; // AI bullet, not fired by player
            }

            // Apply forward impulse
            Rigidbody rb = bulletObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (!restrictToXYPlane)
                {
                    // Unfreeze Z position constraint so bullet can move freely along Z in 3D
                    rb.constraints &= ~RigidbodyConstraints.FreezePositionZ;
                }

                rb.linearVelocity = Vector3.zero;
                rb.AddForce(bulletObj.transform.up * fireForce, ForceMode.Impulse);
            }

            // Auto destroy after lifetime
            if (bulletLifetime > 0f && Application.isPlaying)
            {
                Destroy(bulletObj, bulletLifetime);
            }
        }
    }

    private void EnsureTarget()
    {
        if (target != null) return;

        player p = Object.FindAnyObjectByType<player>();
        if (p != null)
        {
            target = p.transform;
            return;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
            return;
        }

        GameObject shipObj = GameObject.Find("Player_ship");
        if (shipObj != null)
        {
            target = shipObj.transform;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = turretPart != null ? turretPart.position : transform.position;

        // Draw detection range
        Gizmos.color = Color.red;
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
                Gizmos.color = Color.green;
                Gizmos.DrawLine(center, target.position);
            }
        }
    }
}
