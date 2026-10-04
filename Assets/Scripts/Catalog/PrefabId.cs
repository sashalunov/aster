using System;

public enum PrefabId
{
    None = 0,

    // Player & Core
    PlayerShip = 1,
    UltraDeath = 2,
    TapMarker = 3,

    // Asteroids & Environment
    Block0 = 10,
    AsteroidGrid = 11,
    AsteroidBase = 12,
    CoreBlock = 13,
    Box1 = 14,
    BirthSphere = 15,

    // Combat & Weapons
    GunKinetic = 20,
    GunPlasma = 21,
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
    PowerupAmmo = 42,
    PowerupUpgradePoint = 43,
    PowerupBulletSpeed = 44,
    PowerupXP = 45,
    PowerupGunKinetic = 46,
    PowerupGunPlasma = 47,

    // Enemies
    LightTankA = 50,
    LightTankB = 51,
    TurretGun0 = 52,

    // Spawners & Level
    AsteroidFieldSpawner = 60
}
