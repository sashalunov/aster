using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class AsteroidBase : MonoBehaviour
{
    [ContextMenuItem("Randomize Name", "Randomize")]
    public string Name;

    [Header("Core Reference")]
    [Tooltip("The central anchor core block for this asteroid cluster.")]
    public BlockAsteroidCore coreBlock;

    [Tooltip("Prefab instantiated for the core block. If null, resolves from PrefabManager (BlockAsteroidCore).")]
    public GameObject _coreBlockPrefab;

    [Tooltip("Prefab instantiated for standard perimeter blocks. If null, resolves from PrefabManager (BlockAsteroid or Block0).")]
    public GameObject _blockPrefab;

    [Header("Block & Core Stats")]
    public int _block_hits = 1;
    public int _shell_hits = 2;
    public int _core_hits = 3;
    public int _core_shell = 0;

    [Header("Core Visual Overrides (Optional)")]
    [Tooltip("Materials assigned to the core block based on remaining hit points. Passed to coreBlock if it lacks grade materials.")]
    public Material[] _grade_mats;

    [Tooltip("Text component displaying core hit points. Passed to coreBlock if it lacks a level text component.")]
    public TMP_Text _tmp_core_hits;

    [Header("Block Capacity Limits")]
    [Tooltip("Maximum limit of how many blocks can be attached to this asteroid.")]
    [Range(1, 64)]
    public int _max_blocks = 24;

    /// <summary>
    /// Current number of attached perimeter blocks.
    /// </summary>
    public virtual int CurrentBlockCount => gen_num_children();

    /// <summary>
    /// True if the asteroid has reached its maximum block attachment limit.
    /// </summary>
    public virtual bool IsAtCapacity => CurrentBlockCount >= _max_blocks;

    [Header("Destruction State")]
    [Tooltip("True if this asteroid is currently in the process of gameplay destruction.")]
    public bool isDestructing = false;

    [Header("Simulation LOD & Sleep State")]
    [Tooltip("Whether this asteroid is currently in a dormant sleep state to save physics and CPU.")]
    public bool isSleeping = false;
    public bool IsSleeping => isSleeping;

    protected Vector3 savedLinearVelocity = Vector3.zero;
    protected Vector3 savedAngularVelocity = Vector3.zero;

    public int _num_boxes_generated = 0;

    private readonly Vector3[] neighbors = {
        new Vector3(-1,-1,0), new Vector3(-1,0,0),  new Vector3(-1,1,0),
        new Vector3(0,1,0),   new Vector3(1,1,0),
        new Vector3(1,0,0),   new Vector3(1,-1,0),  new Vector3(0,-1,0)
    };

    private readonly List<Transform> tres = new List<Transform>();

    private void Randomize()
    {
        Name = "Asteroid_" + UnityEngine.Random.Range(100, 999);
    }

    public virtual GameObject GetBlockPrefab()
    {
        if (_blockPrefab != null) return _blockPrefab;
        _blockPrefab = PrefabManager.Get(PrefabId.BlockAsteroid) ?? PrefabManager.Get(PrefabId.Block0);
        return _blockPrefab;
    }

    public virtual GameObject GetCorePrefab()
    {
        if (_coreBlockPrefab != null) return _coreBlockPrefab;
        _coreBlockPrefab = PrefabManager.Get(PrefabId.BlockAsteroidCore);
        return _coreBlockPrefab;
    }

    /// <summary>
    /// Locates or ensures an authoritative BlockAsteroidCore exists for this asteroid cluster.
    /// </summary>
    public virtual BlockAsteroidCore EnsureCoreBlock()
    {
        if (coreBlock != null) return coreBlock;

        // 1. Search for child named "core_block"
        Transform coreT = transform.Find("core_block");
        if (coreT != null)
        {
            coreBlock = coreT.GetComponent<BlockAsteroidCore>();
            if (coreBlock == null)
            {
                BlockBase b = coreT.GetComponent<BlockBase>();
                if (b != null)
                {
                    coreBlock = UpgradeChildToCore(coreT.gameObject, b);
                }
            }
        }

        // 2. Search children by BlockAsteroidCore component
        if (coreBlock == null)
        {
            coreBlock = GetComponentInChildren<BlockAsteroidCore>();
        }

        // 3. Search children tagged "core"
        if (coreBlock == null)
        {
            foreach (Transform child in transform)
            {
                if (child.CompareTag("core"))
                {
                    coreBlock = child.GetComponent<BlockAsteroidCore>();
                    if (coreBlock == null)
                    {
                        BlockBase b = child.GetComponent<BlockBase>();
                        if (b != null)
                        {
                            coreBlock = UpgradeChildToCore(child.gameObject, b);
                            if (coreBlock != null) break;
                        }
                    }
                    else break;
                }
            }
        }

        // 4. Search children with isCore flag set
        if (coreBlock == null)
        {
            foreach (Transform child in transform)
            {
                BlockBase b = child.GetComponent<BlockBase>();
                if (b != null && b.isCore)
                {
                    coreBlock = b as BlockAsteroidCore ?? UpgradeChildToCore(child.gameObject, b);
                    if (coreBlock != null) break;
                }
            }
        }

        if (coreBlock != null)
        {
            SyncCoreBlockProperties();
            BindCoreBlockEvents();
        }

        return coreBlock;
    }

    private BlockAsteroidCore UpgradeChildToCore(GameObject go, BlockBase existing)
    {
        if (existing is BlockAsteroidCore core) return core;

        float hits = existing._hits > 0 ? existing._hits : _core_hits;
        bool bonus = existing._bonus;
#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(existing);
        else Destroy(existing);
#else
        Destroy(existing);
#endif
        BlockAsteroidCore newCore = go.AddComponent<BlockAsteroidCore>();
        newCore.isCore = true;
        newCore._bonus = bonus;
        newCore.SetHits((int)hits);
        go.tag = "core";
        return newCore;
    }

    /// <summary>
    /// Spawns a BlockAsteroidCore GameObject under this asteroid at local position.
    /// </summary>
    public virtual BlockAsteroidCore SpawnCoreBlock(int hits, Vector3 localPos = default)
    {
        if (coreBlock != null)
        {
            coreBlock.transform.localPosition = localPos;
            coreBlock.SetHits(hits);
            _core_hits = hits;
            return coreBlock;
        }

        GameObject prefab = GetCorePrefab();
        GameObject coreObj;
        if (prefab != null)
        {
            coreObj = Instantiate(prefab, transform.TransformPoint(localPos), transform.rotation, transform);
        }
        else
        {
            GameObject fallbackPrefab = GetBlockPrefab();
            if (fallbackPrefab != null)
            {
                coreObj = Instantiate(fallbackPrefab, transform.TransformPoint(localPos), transform.rotation, transform);
            }
            else
            {
                coreObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                coreObj.transform.SetParent(transform);
                coreObj.transform.position = transform.TransformPoint(localPos);
                coreObj.transform.rotation = transform.rotation;
            }
        }

        coreObj.transform.localPosition = localPos;
        coreObj.transform.localRotation = Quaternion.identity;
        coreObj.name = "core_block";
        coreObj.tag = "core";

        // Remove any local Rigidbody so PhysX compound hierarchy moves as one asteroid
        Rigidbody rb = coreObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(rb);
            else Destroy(rb);
#else
            Destroy(rb);
#endif
        }

        coreBlock = coreObj.GetComponent<BlockAsteroidCore>();
        if (coreBlock == null)
        {
            BlockBase existing = coreObj.GetComponent<BlockBase>();
            if (existing != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(existing);
                else Destroy(existing);
#else
                Destroy(existing);
#endif
            }
            coreBlock = coreObj.AddComponent<BlockAsteroidCore>();
        }

        coreBlock.isCore = true;
        coreBlock._detached = false;
        SyncCoreBlockProperties();
        coreBlock.SetHits(hits);
        _core_hits = hits;

        BindCoreBlockEvents();
        return coreBlock;
    }

    protected virtual void SyncCoreBlockProperties()
    {
        if (coreBlock == null) return;
        coreBlock.isCore = true;
        if (!coreBlock.CompareTag("core"))
        {
            coreBlock.gameObject.tag = "core";
        }
        if ((coreBlock._grade_mats == null || coreBlock._grade_mats.Length == 0) && _grade_mats != null && _grade_mats.Length > 0)
        {
            coreBlock.GradeMaterials = _grade_mats;
        }
        if (coreBlock._tmp_lvl == null && _tmp_core_hits != null)
        {
            coreBlock.LevelText = _tmp_core_hits;
        }
    }

    protected virtual void BindCoreBlockEvents()
    {
        if (coreBlock != null)
        {
            coreBlock.OnDestroyed -= HandleCoreBlockDestroyed;
            coreBlock.OnDestroyed += HandleCoreBlockDestroyed;
            coreBlock.OnHit -= HandleCoreBlockHit;
            coreBlock.OnHit += HandleCoreBlockHit;
        }
    }

    protected virtual void UnbindCoreBlockEvents()
    {
        if (coreBlock != null)
        {
            coreBlock.OnDestroyed -= HandleCoreBlockDestroyed;
            coreBlock.OnHit -= HandleCoreBlockHit;
        }
    }

    protected virtual void HandleCoreBlockHit(BlockBase core, Transform source, ProjectileBase b1)
    {
        if (isSleeping)
        {
            SetSleeping(false);
        }
        _core_hits = (int)core._hits;
        UpdateMass();
    }

    protected virtual void HandleCoreBlockDestroyed(BlockBase core, Transform source, ProjectileBase b1)
    {
        core_destruct(b1);
    }

    public virtual bool IsCoreDead()
    {
        if (coreBlock != null)
        {
            return coreBlock._dead || coreBlock._hits <= 0;
        }
        return _core_hits <= 0;
    }

    /// <summary>
    /// Sets the dormant sleep state of the asteroid to save PhysX and CPU cycles when outside view radius.
    /// Preserves linear and angular velocity upon waking.
    /// </summary>
    public virtual void SetSleeping(bool sleep)
    {
        if (isDestructing) return;
        if (isSleeping == sleep) return;

        isSleeping = sleep;
        Rigidbody rb = GetComponent<Rigidbody>();

        if (isSleeping)
        {
            if (rb != null)
            {
                savedLinearVelocity = rb.linearVelocity;
                savedAngularVelocity = rb.angularVelocity;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.Sleep();
            }
            if (coreBlock != null && coreBlock.CoreParticleEffect != null)
            {
                coreBlock.CoreParticleEffect.Pause();
            }
        }
        else
        {
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.WakeUp();
                if (savedLinearVelocity.sqrMagnitude > 0.0001f || savedAngularVelocity.sqrMagnitude > 0.0001f)
                {
                    rb.linearVelocity = savedLinearVelocity;
                    rb.angularVelocity = savedAngularVelocity;
                }
            }
            if (coreBlock != null && coreBlock.CoreParticleEffect != null)
            {
                coreBlock.CoreParticleEffect.Play();
            }
        }
    }

    protected virtual void Reset()
    {
        EnsureRigidbody();
    }

    protected virtual void Awake()
    {
        EnsureRigidbody();
        EnsureCoreBlock();
    }

    protected virtual void OnEnable()
    {
        BindCoreBlockEvents();
    }

    protected virtual void OnDisable()
    {
        UnbindCoreBlockEvents();
    }

    public virtual Rigidbody EnsureRigidbody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        rb.useGravity = false;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        return rb;
    }

    protected virtual void Start()
    {
        EnsureRigidbody();
        EnsureCoreBlock();
        SetHits(_core_hits);

        if (Application.isPlaying && WaveManager.Instance != null)
        {
            WaveManager.Instance.RegisterThreat(gameObject);
        }
    }

    protected virtual void Update()
    {
        if (!isDestructing && IsCoreDead())
        {
            core_destruct(null);
        }
    }

    [ContextMenu("Clear Asteroid")]
    public virtual void Clear()
    {
        UnbindCoreBlockEvents();
        coreBlock = null;

        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            if (child.GetComponent<BlockBase>() != null || child.name == "core_block" || child.name.StartsWith("b_"))
            {
                toDestroy.Add(child.gameObject);
            }
        }

        foreach (GameObject go in toDestroy)
        {
            if (go != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(go);
                else Destroy(go);
#else
                Destroy(go);
#endif
            }
        }
        _num_boxes_generated = 0;
    }

    [ContextMenu("Regenerate Asteroid")]
    private void Regenerate()
    {
        _num_boxes_generated = 0;
        var gg = generate_shell(_core_shell, _shell_hits);
        foreach (GameObject g in gg)
        {
            if (IsAtCapacity) break;
            add_raycast_neighbors(g, _block_hits);
        }

        if (gg.Count < 1)
        {
            GameObject centerGo = coreBlock != null ? coreBlock.gameObject : gameObject;
            while (!IsAtCapacity)
            {
                add_raycast_neighbors(centerGo, _block_hits);
            }
        }
    }

    public virtual int generate_asteroid(int coremass = 1, int massmin = 1, int massmax = 16, int shell = 0, int shlvl = 1, int blocklvl = 1)
    {
        _block_hits = blocklvl;
        _shell_hits = shlvl;
        _core_shell = shell;
        if (massmax > _max_blocks)
        {
            _max_blocks = massmax;
        }
        int targetBlocks = (massmax > 0) ? UnityEngine.Random.Range(massmin, Mathf.Min(massmax, _max_blocks) + 1) : 0;

        _num_boxes_generated = 0;

        SpawnCoreBlock(coremass, Vector3.zero);

        var gg = generate_shell(_core_shell, _shell_hits);
        foreach (GameObject g in gg)
        {
            if (CurrentBlockCount >= targetBlocks || IsAtCapacity) break;
            add_raycast_neighbors(g, _block_hits);
        }
        if (gg.Count < 1)
        {
            GameObject centerGo = coreBlock != null ? coreBlock.gameObject : gameObject;
            while (CurrentBlockCount < targetBlocks && !IsAtCapacity)
            {
                add_raycast_neighbors(centerGo, _block_hits);
            }
        }
        return UpdateMass();
    }

    public virtual void check_for_unconnected()
    {
        check_for_unconnected(Vector3.zero);
    }

    public virtual void check_for_unconnected(Vector3 impactImpulse)
    {
        tres.Clear();
        Transform rootCenter = coreBlock != null ? coreBlock.transform : transform;
        scan_connected(rootCenter);
        foreach (Transform child in transform)
        {
            if (coreBlock != null && child.gameObject == coreBlock.gameObject) continue;

            BlockBase b0 = child.GetComponent<BlockBase>();
            if (b0 != null && !b0.isCore)
            {
                if (!tres.Contains(child))
                {
                    b0.EndLife(transform.parent);
                    Rigidbody rb = b0.GetComponent<Rigidbody>();
                    if (rb != null && impactImpulse.sqrMagnitude > 0.001f)
                    {
                        rb.AddForce(impactImpulse, ForceMode.Impulse);
                    }
                }
            }
        }
        UpdateMass();
    }

    public virtual List<GameObject> generate_shell(int thickness = 1, int hit = 3)
    {
        int i = 0;
        GameObject newbox;
        List<GameObject> shell_go = new List<GameObject>();
        GameObject blockPrefab = GetBlockPrefab();
        if (blockPrefab == null) return shell_go;

        Transform centerT = coreBlock != null ? coreBlock.transform : transform;

        if (thickness == 0)
        {
            foreach (Vector3 n in neighbors)
            {
                i++;
                if (i % 2 == 0)
                {
                    newbox = Instantiate(blockPrefab, centerT.TransformPoint(n), transform.rotation);
                    newbox.transform.name = "b_" + i.ToString();
                    newbox.transform.parent = transform;
                    newbox.GetComponent<BlockBase>()?.SetHits(hit);
                    _num_boxes_generated++;
                    shell_go.Add(newbox);
                }
            }
        }
        else if (thickness > 0)
        {
            i = 0;
            foreach (Vector3 n in neighbors)
            {
                newbox = Instantiate(blockPrefab, centerT.TransformPoint(n), transform.rotation);
                newbox.transform.name = "b_" + i.ToString();
                newbox.transform.parent = transform;
                newbox.GetComponent<BlockBase>()?.SetHits(hit);

                i++;
                _num_boxes_generated++;
                shell_go.Add(newbox);
            }

            if (thickness > 1)
            {
                List<GameObject> tg = new List<GameObject>();
                foreach (GameObject g in shell_go)
                {
                    for (int j = 0; j < thickness; j++)
                    {
                        GameObject neighbor = add_raycast_neighbors(g, hit);
                        if (neighbor != null) tg.Add(neighbor);
                    }
                }
                shell_go.AddRange(tg);
            }
        }
        UpdateMass();
        return shell_go;
    }

    /// <summary>
    /// Processes a hit directed at the core. Delegates damage, popup FX, audio, and XP to BlockAsteroidCore.
    /// </summary>
    public virtual int core_receive_hit(Transform source, ProjectileBase b1)
    {
        if (isSleeping)
        {
            SetSleeping(false);
        }

        if (coreBlock != null && !coreBlock._dead)
        {
            float remaining = coreBlock.block_receive_hit(source, b1);
            if (coreBlock == null || coreBlock._dead)
            {
                core_destruct(b1);
            }
            else
            {
                _core_hits = (int)coreBlock._hits;
                UpdateMass();
            }
            return (int)remaining;
        }

        // Fallback for legacy asteroids without an attached coreBlock
        float b1_dmg = (b1 != null) ? b1.Damage : 1f;
        var newhits = _core_hits - b1_dmg;
        var newhitdamage = Mathf.Max(0f, b1_dmg - _core_hits);
        var damage = b1_dmg >= _core_hits ? _core_hits : (int)b1_dmg;

        if (damage > 0 && newhits > 0)
        {
            if (b1 != null && b1._player != null)
            {
                b1._player.AddXP(damage, transform);
                 var bonus = PrefabManager.Instantiate(PrefabId.CoreHitFx, transform.position, Quaternion.identity);
            if (bonus != null)
            {
                bonus.GetComponentInChildren<TextMeshPro>()?.SetText("+" + damage.ToString());
            }
            }
            UpdateMass();
          
        }
        if (newhits < 1)
        {
            core_destruct(b1);
        }

        _core_hits = (int)newhits;
        UpdateMats();
        UpdateText();

        return (int)newhitdamage;
    }

    /// <summary>
    /// Handles total destruction of the asteroid upon core collapse.
    /// </summary>
    public virtual int core_destruct(ProjectileBase b1)
    {
        if (isDestructing) return 0;
        isDestructing = true;

        UnbindCoreBlockEvents();

        int reward = DetachChildrenOnDestruction();
        int totalXp = reward + _core_hits;
        player p = (b1 != null && b1._player != null) ? b1._player : null;
        if (p != null)
        {
            p.AddXP(totalXp, transform);
        }

        GameObject bonus = PrefabManager.Instantiate(PrefabId.CoreHitFx, transform.position, Quaternion.identity);
        if (bonus != null)
        {
            TextMeshPro tmp = bonus.GetComponentInChildren<TextMeshPro>();
            if (tmp != null) tmp.SetText("+" + totalXp.ToString());
            bonus.transform.localScale = new Vector3(1.5f, 1.5f, 1.1f);
        }

        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.HandleBlockDestructionDrop(transform.position, isCore: true, p);
        }
        else
        {
            var shieldFx = PrefabManager.Get(PrefabId.PowerupShield) ?? PrefabManager.Get(PrefabId.PowerupDefault);
            if (shieldFx != null)
            {
                GameObject shbonus = Instantiate(shieldFx, transform.position, Quaternion.identity) as GameObject;
                StandardPowerup pu = shbonus != null ? shbonus.GetComponent<StandardPowerup>() : null;
                if (pu != null) pu.Type = StandardPowerup.StandardType.ShieldUp;
            }
        }

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.UnregisterThreat(gameObject);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(gameObject);
        else
            Destroy(gameObject);
#else
        Destroy(gameObject);
#endif

        return totalXp;
    }

    /// <summary>
    /// Detaches perimeter blocks on destruction, converting them to physical debris and calculating reward.
    /// </summary>
    protected virtual int DetachChildrenOnDestruction()
    {
        int reward = 0;
        List<Transform> childrenToDetach = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            if (coreBlock != null && child.gameObject == coreBlock.gameObject) continue;
            if (child.name == "core_block") continue;

            BlockBase b0 = child.GetComponent<BlockBase>();
            if (b0 != null && !b0.isCore)
            {
                childrenToDetach.Add(child);
            }
        }

        Transform targetParent = transform.parent;
        if (targetParent != null && Application.isPlaying && (!targetParent.gameObject.scene.isLoaded || !targetParent.gameObject.activeInHierarchy))
        {
            targetParent = null;
        }

        foreach (Transform child in childrenToDetach)
        {
            if (child == null) continue;
            BlockBase b0 = child.GetComponent<BlockBase>();
            if (b0 != null && !b0._dead)
            {
                reward += (int)b0._hits;
                b0.EndLife(targetParent);
                b0.gameObject.tag = "block";

                Collider c = child.GetComponent<Collider>();
                if (c != null)
                {
                    c.enabled = true;
                    c.isTrigger = false;
                }

                Rigidbody rb = child.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = child.gameObject.AddComponent<Rigidbody>();
                }
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
                rb.mass = b0._hits > 0 ? b0._hits : 1f;

                Vector3 ejectDir = (child.position - transform.position).normalized;
                if (ejectDir.sqrMagnitude < 0.001f) ejectDir = UnityEngine.Random.insideUnitSphere;
                ejectDir.z = 0f;
                ejectDir.Normalize();
                rb.linearVelocity = ejectDir * UnityEngine.Random.Range(2.5f, 6.0f);
            }
        }
        return reward;
    }

    public void box_got_hit(Collider col, Vector3 bdir)
    {
        Vector3 colpos = col.transform.position;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForceAtPosition(bdir * 15f, colpos, ForceMode.Impulse);
        }
        PrefabManager.Instantiate(PrefabId.AdditiveBonusFx, colpos, Quaternion.identity);
        Destroy(col.gameObject);
    }

    private Vector3[] scan_free_neighbors(GameObject initial_go)
    {
        Ray ray;
        RaycastHit hit;
        List<Vector3> nres = new List<Vector3>();

        foreach (Vector3 v in neighbors)
        {
            Vector3 tp = initial_go.transform.TransformPoint(v);
            ray = new Ray(tp + new Vector3(0, 0, -2), Vector3.forward);

            if (Physics.Raycast(ray, out hit, 2))
            {
                continue;
            }
            nres.Add(tp);
        }
        return nres.ToArray();
    }

    private GameObject add_raycast_neighbors(GameObject initial_go, int block_hits)
    {
        Vector3[] n = scan_free_neighbors(initial_go);
        int numch = gen_num_children();
        GameObject blockPrefab = GetBlockPrefab();
        if (blockPrefab == null) return null;

        if (n.Length > 0)
        {
            int rndinx = UnityEngine.Random.Range(0, n.Length);
            var b = Instantiate(blockPrefab, n[rndinx], transform.rotation) as GameObject;
            b.transform.name = "bl_" + (numch + 1);
            b.transform.parent = transform;
            b.GetComponent<BlockBase>()?.SetHits(block_hits);
            _num_boxes_generated++;

            return b;
        }
        else
        {
            List<GameObject> gos = new List<GameObject>();

            foreach (Transform child in transform)
            {
                BlockBase b0 = child.GetComponent<BlockBase>();
                if (b0 != null && !b0.isCore)
                {
                    gos.Add(child.gameObject);
                }
            }
            if (gos.Count > 0)
            {
                int rndchld = UnityEngine.Random.Range(0, gos.Count);
                return add_raycast_neighbors(gos[rndchld], block_hits);
            }
        }

        return null;
    }

    public virtual void UpdateText()
    {
        if (coreBlock != null)
        {
            coreBlock.UpdateVisuals();
        }
        else if (_tmp_core_hits != null)
        {
            _tmp_core_hits.SetText(_core_hits.ToString());
        }
    }

    public virtual void UpdateMats()
    {
        if (coreBlock != null)
        {
            coreBlock.UpdateVisuals();
        }
        else
        {
            if (_core_hits < 1 || _grade_mats == null || _grade_mats.Length == 0) return;
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr == null) return;

            int index = Mathf.Clamp(_core_hits - 1, 0, _grade_mats.Length - 1);
            if (_grade_mats[index] != null)
            {
                mr.material = _grade_mats[index];
            }
        }
    }

    public virtual int UpdateMass()
    {
        EnsureRigidbody();
        int childmass = 0;
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            if (coreBlock != null && child.gameObject == coreBlock.gameObject) continue;
            if (child.name == "core_block") continue;

            BlockBase b0 = child.GetComponent<BlockBase>();
            if (b0 != null && !b0.isCore)
            {
                childmass += (int)b0._hits;
            }
        }
        int totalMass = childmass + _core_hits;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = totalMass > 0 ? totalMass : 1f;
        }
        return totalMass;
    }

    public virtual void SetHits(int newhits)
    {
        _core_hits = newhits;
        if (coreBlock != null)
        {
            coreBlock.SetHits(newhits);
        }
        else
        {
            UpdateText();
            UpdateMats();
        }
        UpdateMass();
    }

    public int gen_num_children()
    {
        int num_ch = 0;
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            if (coreBlock != null && child.gameObject == coreBlock.gameObject) continue;
            if (child.name == "core_block") continue;

            BlockBase b0 = child.GetComponent<BlockBase>();
            if (b0 != null && !b0.isCore)
            {
                num_ch += 1;
            }
        }
        return num_ch;
    }

    private Transform[] scan_connected(Transform initial_go)
    {
        Ray ray;
        RaycastHit hit;

        foreach (Vector3 v in neighbors)
        {
            Vector3 tp = initial_go.TransformPoint(v);
            ray = new Ray(tp + new Vector3(0, 0, -1), Vector3.forward);

            LayerMask mask = LayerMask.GetMask("Asteroid");

            if (Physics.Raycast(ray, out hit, 1.0f, mask))
            {
                if (hit.collider.transform.parent == transform)
                {
                    BlockBase b = hit.collider.GetComponent<BlockBase>();
                    if (b == null || b._dead) continue;

                    if (tres.Contains(hit.collider.transform)) continue;

                    tres.Add(hit.collider.transform);
                    scan_connected(hit.collider.transform);
                }
            }
        }
        return tres.ToArray();
    }
}
