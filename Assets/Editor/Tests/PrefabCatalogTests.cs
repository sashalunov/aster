using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class PrefabCatalogTests
{
    private PrefabCatalog _originalCatalog;

    [SetUp]
    public void SetUp()
    {
        _originalCatalog = PrefabManager.ActiveCatalog;
    }

    [TearDown]
    public void TearDown()
    {
        if (_originalCatalog != null)
        {
            PrefabManager.RegisterCatalog(_originalCatalog);
        }
        else
        {
            PrefabManager.EnsureCatalogLoaded();
        }
    }

    [Test]
    public void PrefabCatalog_InitializesAndIndexesByIdAndName()
    {
        var catalog = ScriptableObject.CreateInstance<PrefabCatalog>();
        var dummyGo = new GameObject("DummyPrefab");

        try
        {
            catalog.AddEntry(PrefabId.UltraDeath, dummyGo, "CustomAlias");

            Assert.AreSame(dummyGo, catalog.Get(PrefabId.UltraDeath));
            Assert.AreSame(dummyGo, catalog.Get("DummyPrefab"));
            Assert.AreSame(dummyGo, catalog.Get("dummyprefab")); // Case-insensitive
            Assert.AreSame(dummyGo, catalog.Get("CustomAlias"));
            Assert.AreSame(dummyGo, catalog.Get("UltraDeath")); // Enum name lookup
        }
        finally
        {
            Object.DestroyImmediate(dummyGo);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void PrefabCatalog_TryGet_ReturnsExpectedResults()
    {
        var catalog = ScriptableObject.CreateInstance<PrefabCatalog>();
        var dummyGo = new GameObject("DummyBlock");

        try
        {
            catalog.AddEntry(PrefabId.Block0, dummyGo);

            Assert.IsTrue(catalog.TryGet(PrefabId.Block0, out var foundById));
            Assert.AreSame(dummyGo, foundById);

            Assert.IsTrue(catalog.TryGet("DummyBlock", out var foundByName));
            Assert.AreSame(dummyGo, foundByName);

            Assert.IsFalse(catalog.TryGet(PrefabId.None, out _));
            Assert.IsFalse(catalog.TryGet("NonExistentPrefabKey", out _));
        }
        finally
        {
            Object.DestroyImmediate(dummyGo);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void PrefabCatalog_GetComponent_RetrievesAttachedComponent()
    {
        var catalog = ScriptableObject.CreateInstance<PrefabCatalog>();
        var dummyGo = new GameObject("DummyBox");
        var collider = dummyGo.AddComponent<BoxCollider>();

        try
        {
            catalog.AddEntry(PrefabId.Box1, dummyGo);

            var retrievedCol = catalog.Get<BoxCollider>(PrefabId.Box1);
            Assert.IsNotNull(retrievedCol);
            Assert.AreSame(collider, retrievedCol);

            var retrievedByName = catalog.Get<BoxCollider>("DummyBox");
            Assert.IsNotNull(retrievedByName);
            Assert.AreSame(collider, retrievedByName);
        }
        finally
        {
            Object.DestroyImmediate(dummyGo);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void PrefabManager_ActiveCatalog_ResolvesCatalogFromAssetDatabase()
    {
        PrefabManager.ResetForTesting();
        var catalog = PrefabManager.EnsureCatalogLoaded();

        Assert.IsNotNull(catalog, "PrefabManager should resolve PrefabCatalog asset from project.");
        Assert.IsTrue(PrefabManager.IsInitialized);
    }

    [Test]
    public void PrefabManager_GetKnownPrefabs_ResolvesValidGameObjects()
    {
        PrefabManager.EnsureCatalogLoaded();

        GameObject ultraDeath = PrefabManager.Get(PrefabId.UltraDeath);
        Assert.IsNotNull(ultraDeath, "UltraDeath prefab must be registered in the catalog.");
        Assert.AreEqual("UltraDeath", ultraDeath.name);

        GameObject block0 = PrefabManager.Get(PrefabId.Block0);
        Assert.IsNotNull(block0, "Block0 prefab must be registered in the catalog.");
        Assert.AreEqual("block0", block0.name);

        GameObject bulletKinetic = PrefabManager.Get(PrefabId.BulletKinetic);
        Assert.IsNotNull(bulletKinetic, "BulletKinetic prefab must be registered in the catalog.");
        Assert.AreEqual("bulletKinetic", bulletKinetic.name);
    }

    [Test]
    public void PrefabManager_Instantiate_SpawnsInstanceCorrectly()
    {
        PrefabManager.EnsureCatalogLoaded();

        Vector3 spawnPos = new Vector3(10f, 20f, 0f);
        GameObject instance = PrefabManager.Instantiate(PrefabId.UltraDeath, spawnPos, Quaternion.identity);

        try
        {
            Assert.IsNotNull(instance);
            Assert.AreEqual(spawnPos, instance.transform.position);
            Assert.IsTrue(instance.name.StartsWith("UltraDeath"));
        }
        finally
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }
    }

    [Test]
    public void PrefabManager_MockCatalogInjection_OverridesActiveRegistry()
    {
        var mockCatalog = ScriptableObject.CreateInstance<PrefabCatalog>();
        var dummyGo = new GameObject("MockPrefab");

        try
        {
            mockCatalog.AddEntry(PrefabId.CoreBlock, dummyGo);
            PrefabManager.SetCatalog(mockCatalog);

            Assert.AreSame(dummyGo, PrefabManager.Get(PrefabId.CoreBlock));
        }
        finally
        {
            Object.DestroyImmediate(dummyGo);
            Object.DestroyImmediate(mockCatalog);
        }
    }

    [Test]
    public void PrefabCatalogBuilder_EnsuresPreloadedAssetRegistration()
    {
        var preloaded = PlayerSettings.GetPreloadedAssets();
        var catalogInPreloaded = preloaded.FirstOrDefault(a => a is PrefabCatalog);

        Assert.IsNotNull(catalogInPreloaded, "GamePrefabCatalog must be registered in PlayerSettings.PreloadedAssets for standalone autoload.");
    }

    [Test]
    public void Player_Die_InstantiatesUltraDeathViaPrefabManager()
    {
        var playerGo = new GameObject("TestPlayerShip");
        var pl = playerGo.AddComponent<player>();

        // Ensure catalog loaded
        PrefabManager.EnsureCatalogLoaded();

        try
        {
            pl.Die();

            Assert.IsTrue(pl.isDead);
            // UltraDeath clone should have spawned in the scene
            var spawned = Object.FindObjectsByType<UltraDeath>(FindObjectsSortMode.None);
            Assert.IsTrue(spawned.Length > 0, "UltraDeath should have been spawned on player death.");

            var rootsToDestroy = new HashSet<GameObject>();
            for (int i = 0; i < spawned.Length; i++)
            {
                if (spawned[i] != null)
                {
                    rootsToDestroy.Add(spawned[i].transform.root.gameObject);
                }
            }
            foreach (var root in rootsToDestroy)
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }
        finally
        {
            if (playerGo != null)
            {
                Object.DestroyImmediate(playerGo);
            }
        }
    }

    [Test]
    public void RelocatedPrefabs_AllCategoriesAreAccessibleViaPrefabManager()
    {
        PrefabManager.EnsureCatalogLoaded();

        // Ammo
        Assert.IsNotNull(PrefabManager.Get(PrefabId.BulletKinetic), "BulletKinetic should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.BulletPlasma), "BulletPlasma should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.BulletEnemy), "BulletEnemy should be accessible");

        // Guns
        Assert.IsNotNull(PrefabManager.Get(PrefabId.GunKinetic), "GunKinetic should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.GunPlasma), "GunPlasma should be accessible");

        // Ships
        Assert.IsNotNull(PrefabManager.Get(PrefabId.PlayerShip), "PlayerShip should be accessible");

        // Level
        Assert.IsNotNull(PrefabManager.Get(PrefabId.Block0), "Block0 should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.AsteroidGrid), "AsteroidGrid should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.AsteroidBase), "AsteroidBase should be accessible");

        // FX
        Assert.IsNotNull(PrefabManager.Get(PrefabId.UltraDeath), "UltraDeath should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.BlockHitFx), "BlockHitFx should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.CoreHitFx), "CoreHitFx should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.BlockDestroyFx), "BlockDestroyFx should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.MuzzleFlashFx), "MuzzleFlashFx should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.AdditiveBonusFx), "AdditiveBonusFx should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.TapMarker), "TapMarker should be accessible");

        // Powerups
        Assert.IsNotNull(PrefabManager.Get(PrefabId.PowerupDefault), "PowerupDefault should be accessible");
        Assert.IsNotNull(PrefabManager.Get(PrefabId.PowerupShield), "PowerupShield should be accessible");
    }

    [Test]
    public void Weapons_CorrectlyResolveBulletPrefabsFromPrefabManager()
    {
        PrefabManager.EnsureCatalogLoaded();

        GunData kineticData = GunKinetic.CreateKineticGunData();
        Assert.IsNotNull(kineticData.bulletPrefab, "GunKinetic default data must resolve bulletKinetic via PrefabManager");
        Assert.AreEqual("bulletKinetic", kineticData.bulletPrefab.name);

        GunData plasmaData = GunPlasma.CreatePlasmaGunData();
        Assert.IsNotNull(plasmaData.bulletPrefab, "GunPlasma default data must resolve bulletPlasma via PrefabManager");
        Assert.AreEqual("bulletPlasma", plasmaData.bulletPrefab.name);

        GunData flakData = GunFlak.CreateFlakGunData();
        Assert.IsNotNull(flakData.bulletPrefab, "GunFlak default data must resolve bulletKinetic via PrefabManager");
        Assert.AreEqual("bulletKinetic", flakData.bulletPrefab.name);

        Object.DestroyImmediate(kineticData);
        Object.DestroyImmediate(plasmaData);
        Object.DestroyImmediate(flakData);
    }

    [Test]
    public void TurretAI_CorrectlyResolvesPrefabsFromPrefabManager()
    {
        PrefabManager.EnsureCatalogLoaded();

        GameObject go = new GameObject("Turret");
        TurretAI ai = go.AddComponent<TurretAI>();
        ai.AutoConfigureReferences();

        Assert.IsNotNull(ai.bulletPrefab, "TurretAI must resolve bulletPrefab via PrefabManager");
        Assert.AreEqual("bullet1", ai.bulletPrefab.name);
        Assert.IsNotNull(ai.muzzleFlashPrefab, "TurretAI must resolve muzzleFlashPrefab via PrefabManager");
        Assert.AreEqual("ps_muzzle", ai.muzzleFlashPrefab.name);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BlockSpawner_CorrectlyResolvesPrefabsFromPrefabManager()
    {
        PrefabManager.EnsureCatalogLoaded();

        GameObject go = new GameObject("Spawner");
        BlockSpawner spawner = go.AddComponent<BlockSpawner>();

        Assert.IsNotNull(spawner.blockPrefab, "BlockSpawner must resolve blockPrefab via PrefabManager");
        Assert.AreEqual("block0", spawner.blockPrefab.name);
        Assert.IsNotNull(spawner.asterPrefab, "BlockSpawner must resolve asterPrefab via PrefabManager");
        Assert.AreEqual("AsteroidGrid", spawner.asterPrefab.name);

        Object.DestroyImmediate(go);
    }
}

