using System;

public enum PrefabId
{
    None = 0,

    // Player & Core
    PlayerShip = 1,
    UltraDeath = 2,
    TapMarker = 3,
    Cross1Marker = 4,

    // Asteroids & Environment
    Block0 = 10,
    AsteroidGrid = 11,
    AsteroidBase = 12,
    BlockAsteroid = 13,
    BlockAsteroidCore = 14,

    // Combat & Weapons
    GunKinetic = 19,
    GunPlasma = 20,
    GunFlak = 21,
    BulletKinetic = 22,
    BulletPlasma = 23,
    BulletEnemy = 24,

    // VFX & Feedback
    BlockHitFx = 30,
    CoreHitFx = 31,
    BlockDestroyFx = 32,
    MuzzleFlashFx = 33,
    ShieldDamageFx = 34,
    BonusPowerupFx = 35,
    AdditiveBonusFx = 36,
    ThrusterFlameFx = 37,
    ShowUpgradeFx = 38,
    ShieldUpgradeFx = 39,

    // Powerups & Drops
    PowerupDefault = 40,
    PowerupShield = 41,
    PowerupXP = 42,
    PowerupUpgradePoint = 43,
    PowerupAmmoExplosive = 44,
    PowerupAmmoKinetic = 45,
    PowerupAmmoFlak = 46,
    PowerupGunKinetic = 47,
    PowerupGunPlasma = 48,
    PowerupGunFlak = 49,

    // Enemies
    LightTankA = 150,
    LightTankB = 151,
    TurretGun0 = 152,

    // Spawners & Level
    AsteroidFieldSpawner = 360
}
