using UnityEngine;

/// <summary>
/// Data container for weapon archetypes (Blasters, Shotguns, Lasers, Burst Rifles).
/// Can be authored as ScriptableObject assets or instantiated dynamically at runtime.
/// </summary>
[CreateAssetMenu(fileName = "NewGunData", menuName = "Combat/Gun Data")]
public class GunData : ScriptableObject
{
    [Header("Identity & Progression")]
    [Tooltip("Unique technical identifier for this weapon type.")]
    public string gunId = "standard_blaster";

    [Tooltip("Player-facing name displayed in UI / drops.")]
    public string displayName = "Standard Blaster";

    [Tooltip("XP required in PlayerProgression to unlock this gun.")]
    public ulong unlockXP = 0;

    [Tooltip("Rarity or drop weight in PowerupManager drop tables.")]
    [Range(0.01f, 100f)]
    public float dropWeight = 1.0f;

    [Header("Firing Specs")]
    [Tooltip("Projectile prefab to instantiate.")]
    public GameObject bulletPrefab;

    [Tooltip("Muzzle flash FX prefab to instantiate upon firing.")]
    public GameObject muzzleFlashPrefab;

    [Tooltip("Audio clip played when gun fires.")]
    public AudioClip fireSound;

    [Tooltip("Number of shots per second (fire rate frequency).")]
    public float fireRate = 3.0f;

    [Tooltip("Damage dealt per projectile.")]
    public float bulletDamage = 1.0f;

    [Tooltip("Impulse force applied to the projectile.")]
    public float bulletForce = 8.0f;

    [Tooltip("Lifetime of spawned bullets in seconds before auto-destroy.")]
    public float bulletLifetime = 5.0f;

    [Header("Pattern & Multi-Shot")]
    [Tooltip("Number of projectiles spawned per single trigger pull (e.g. >1 for Shotgun).")]
    [Range(1, 16)]
    public int projectilesPerShot = 1;

    [Tooltip("Total spread cone angle in degrees.")]
    [Range(0f, 90f)]
    public float spreadAngle = 0.0f;

    [Tooltip("Number of consecutive shots fired in a burst.")]
    [Range(1, 10)]
    public int burstCount = 1;

    [Tooltip("Delay in seconds between successive shots in a burst.")]
    public float burstInterval = 0.08f;
}
