using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public enum WeaponStatType
{
    Damage,
    Force,
    FireRate
}

[RequireComponent(typeof(PlayerWeapons))]
public class player : MonoBehaviour
{
    public Transform _ship_hull;

    [Header("Weapons Component")]
    [SerializeField] private PlayerWeapons _weapons;
    public PlayerWeapons Weapons
    {
        get
        {
            if (_weapons == null)
            {
                _weapons = GetComponent<PlayerWeapons>();
                if (_weapons == null)
                {
                    _weapons = gameObject.AddComponent<PlayerWeapons>();
                }
            }
            return _weapons;
        }
    }

    [Header("Legacy Sockets & Containers (For Prefab Migration)")]
    [Tooltip("Legacy serialized socket references for automatic migration to PlayerWeapons.")]
    [HideInInspector] public List<Gun> _guns = new List<Gun>();
    [HideInInspector] public List<GunSocket> _sockets = new List<GunSocket>();
    [HideInInspector] public Transform _guns_container;

    public Camera _player_cam;

    public event System.Action<float, float, float> OnWeaponStatsChanged
    {
        add => Weapons.OnWeaponStatsChanged += value;
        remove => Weapons.OnWeaponStatsChanged -= value;
    }
    public event System.Action<float, float> OnShieldChanged;
    public event System.Action<float, Vector3> OnShieldHit;
    public event System.Action<float, float> OnHealthChanged;
    public event System.Action<int> OnUpgradePointsChanged;
    public event System.Action OnDeath;
    public event System.Action OnRespawn;

    [Header("Upgrade Points")]
    [SerializeField] private int _upgradePoints = 0;
    public int UpgradePoints
    {
        get => _upgradePoints;
        set
        {
            _upgradePoints = Mathf.Max(0, value);
            OnUpgradePointsChanged?.Invoke(_upgradePoints);
        }
    }

    public void AddUpgradePoints(int amount)
    {
        if (amount <= 0) return;
        _upgradePoints += amount;
        OnUpgradePointsChanged?.Invoke(_upgradePoints);
    }

    public bool HasUpgradePoints(int cost = 1) => _upgradePoints >= cost;

    public bool isDead = false;
    public bool IsDead => isDead;
    public bool isInvulnerable = false;

    public float health_value = 10.0f;
    public float health_max_value = 10.0f;

    public float shield_value = 10.0f;
    public float shield_max_value = 10.0f;

    public void AddHealth(float amount)
    {
        health_value = Mathf.Clamp(health_value + amount, 0f, health_max_value);
        UpdateHealthHUD();
    }

    public void AddShield(float amount)
    {
        shield_value = Mathf.Clamp(shield_value + amount, 0f, shield_max_value);
        UpdateShieldHUD();
    }
    private PlayerProgression _progression;
    public PlayerProgression Progression
    {
        get
        {
            if (_progression == null)
            {
                _progression = GetComponent<PlayerProgression>();
                if (_progression == null)
                {
                    _progression = gameObject.AddComponent<PlayerProgression>();
                }
            }
            return _progression;
        }
        private set => _progression = value;
    }
    public void Die()
    {
        if (isDead) return;
        isDead = true;
        _can_play = false;
        health_value = 0f;
        shield_value = 0f;
        UpdateShieldHUD();
        UpdateHealthHUD();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        if (_ship_hull != null)
        {
            _ship_hull.gameObject.SetActive(false);
        }

        GameObject ultraDeath = PrefabManager.Get(PrefabId.UltraDeath);
        if (ultraDeath != null)
        {
            Instantiate(ultraDeath, transform.position, Quaternion.identity);
        }
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayExplosion(transform.position, 1f, true);
        }

        OnDeath?.Invoke();
    }

    /// <summary>
    /// Respawns the player ship at the specified world position and rotation,
    /// restoring health, shields, physics, and gameplay capability.
    /// </summary>
    public void Respawn(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        isDead = false;
        _can_play = true;

        health_value = health_max_value;
        shield_value = shield_max_value;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        if (_ship_hull != null)
        {
            _ship_hull.gameObject.SetActive(true);
        }

        if (ps_thruster != null)
        {
            ps_thruster.Stop();
            ps_thruster.Clear();
        }

        if (cross1_marker != null)
        {
            cross1_marker.transform.position = position;
            cross1_marker.SetActive(true);
        }

        PlayerMetaProgression.ApplyTo(this);

        UpdateShieldHUD();
        UpdateHealthHUD();
        UpdateWeaponHUD();

        OnRespawn?.Invoke();
    }

    public ulong _xp_value => Progression != null ? Progression.CurrentXP : 0;
    public ulong _cred_value = 0;

    public int _wavelvl => Progression != null ? Progression.WaveLevel : 0;
    public string PlayerName => PlayerProfile.PlayerName;


    private Vector3 moveDirection = Vector3.zero;

    public ParticleSystem ps_thruster;


    private float zoom_lastTime = 0;
    private float zoom_lerpTime = 2;

    [Header("Combat Attributes (Delegated to PlayerWeapons)")]
    public float _bullet_dmg
    {
        get => Weapons.BulletDamage;
        set => Weapons.BulletDamage = value;
    }
    public float _bullet_force
    {
        get => Weapons.BulletForce;
        set => Weapons.BulletForce = value;
    }
    public float _thrust_force = 5.3f;
    public float _fire_hz
    {
        get => Weapons.FireHz;
        set => Weapons.FireHz = value;
    }
    public float _fire_rate
    {
        get => Weapons.FireRate;
        set => Weapons.FireRate = value;
    }

    private float fire_time_accum = 1;
  
    private bool _tap_hold = false;
    private bool _tap_enabled = true;
    private float _tap_time_accum = 0;
    public float _tap_rate = 0.5f;

    private UnityEngine.Object fx_levelup_tip;
    private UnityEngine.Object fx_shield_damage_tip;
    private UnityEngine.Object fx_powerup_tip;

    public bool _can_play = false;


    private GameObject cross1_marker;

    void Awake()
    {
        if (_weapons == null)
        {
            _weapons = GetComponent<PlayerWeapons>();
            if (_weapons == null)
            {
                _weapons = gameObject.AddComponent<PlayerWeapons>();
            }
        }
        Progression = GetComponent<PlayerProgression>();
        if (Progression == null)
        {
            Progression = gameObject.AddComponent<PlayerProgression>();
        }
        if (GetComponent<DebugCheats>() == null)
        {
            gameObject.AddComponent<DebugCheats>();
        }
        cross1_marker = Instantiate(PrefabManager.Get(PrefabId.Cross1Marker)) as GameObject;
        
    }

    void OnEnable()
    {
        if (Progression != null)
        {
            Progression.OnPlayerLevelUp += HandlePlayerLevelUp;
        }
    }

    void OnDisable()
    {
        if (Progression != null)
        {
            Progression.OnPlayerLevelUp -= HandlePlayerLevelUp;
        }
    }

    // Use this for initialization
    void Start()
    {
        fx_shield_damage_tip = PrefabManager.Get(PrefabId.ShieldDamageFx);
        fx_powerup_tip = PrefabManager.Get(PrefabId.PowerupDefault);
        fx_levelup_tip = PrefabManager.Get(PrefabId.PowerupDefault);

        PlayerMetaProgression.ApplyTo(this);

        UpdateShieldHUD();
        UpdateHealthHUD();
        UpdateWeaponHUD();
    }


    // Update is called once per frame
    void Update()
    {
        if (cross1_marker != null)
        {
            cross1_marker.SetActive(_can_play);
        }

        if (!_can_play) return;

        Camera activeCam = _player_cam != null ? _player_cam : Camera.main;
        if (activeCam == null) return;

        Vector3 mousepos = Vector3.zero;
        Vector3 playerpos = Vector3.zero;
        playerpos = activeCam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -activeCam.transform.position.z));
        playerpos.y = 0;

        mousepos = activeCam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -activeCam.transform.position.z));
        Ray mousegndray = new Ray(mousepos,Vector3.forward);
        RaycastHit[] gh = Physics.RaycastAll(mousegndray, 512f, LayerMask.GetMask("Terrain"));
        //groundmousepos = new Vector3(mousepos.x, 0, mousepos.z);
        //Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Ray ray = new Ray(transform.position, (transform.position - mousepos));

        // Get Vectors direction
        Vector3 mousedir = ray.direction;
        mousedir.z = 0;
        Vector3 dir = -mousedir.normalized;

        Vector3 a = transform.position;
        Vector3 b = mousepos;

        AimWeapons(mousepos);

        _ship_hull.localEulerAngles = new Vector3(0, 0,Mathf.Rad2Deg * Mathf.Atan2(b.y - a.y, b.x - a.x) - 90f);

        _tap_time_accum += Time.deltaTime;
        fire_time_accum += Time.deltaTime;
       
        cross1_marker.transform.position = gh.Length > 0 ? gh[0].point : mousepos;

        if (Input.GetMouseButton(0)  )
        {
            if (_tap_time_accum >= _tap_rate && _tap_enabled && _tap_hold == false)
            {
                _tap_time_accum = 0;
                _tap_enabled = false;
                 _tap_hold = true;
            }

            FireWeapons();
        }
        if(Input.GetMouseButtonUp(0))
        {
            _tap_hold = false;
            _tap_enabled = true;
        }

        if (Input.GetMouseButton(1))
        {
            GetComponent<Rigidbody>().AddForce(dir * _thrust_force * Time.deltaTime, ForceMode.Impulse);
            //transform.rotation = Quaternion.Euler(new Vector3(0, 0, Mathf.Rad2Deg * Mathf.Atan2(b.y - a.y, b.x - a.x) - 90f));
            ps_thruster.Play();
        }
        else
        {
            ps_thruster.Stop();
        }

        if (Input.GetKey("space"))
        {   
        }

        if (Input.GetKey("left"))
        {        
        }
        if (Input.GetKey("right"))
        {
            //transform.position = transform.position + new Vector3(speed * Time.deltaTime, 0, 0);
            //GetComponent<Rigidbody>().AddForce(new Vector3(1, 0, 0) * _thrust_force * Time.deltaTime, ForceMode.Impulse);
        }

        //float dist = (transform.position.x - mousepos.x);
        
        if (_player_cam != null)
        {
            Vector3 campos = _player_cam.transform.localPosition;
            if (Input.GetAxis("Mouse ScrollWheel") < 0 ) 
            {
                campos.z -=2f;
                zoom_lastTime = Time.time;
            }
            if (Input.GetAxis("Mouse ScrollWheel") > 0 ) 
            {
                campos.z +=2f;
                zoom_lastTime = Time.time;
            }
            _player_cam.transform.localPosition = new Vector3(campos.x, campos.y, Mathf.Clamp(campos.z, -60f, -20f));
        }
    }

    public void HandleTriggerExit(Collider col)
    {
        if (col == null) return;

        // When asteroid core leaves the player's view radius, transition asteroid to dormant sleep state
        if (col.CompareTag("core"))
        {
            AsteroidBase asteroid = col.GetComponentInParent<AsteroidBase>();
            if (asteroid != null && !asteroid.isDestructing)
            {
                asteroid.SetSleeping(true);
            }
        }
    }

    public void HandleTriggerEnter(Collider col)
    {
        if (col == null) return;

        // When asteroid enters the player's view radius, reactivate / wake it up
        if (col.CompareTag("core") || col.CompareTag("block"))
        {
            AsteroidBase asteroid = col.GetComponentInParent<AsteroidBase>();
            if (asteroid != null && !asteroid.isDestructing && asteroid.IsSleeping)
            {
                asteroid.SetSleeping(false);
            }
        }
    }

    void OnTriggerExit(Collider col) => HandleTriggerExit(col);
    void OnTriggerEnter(Collider col) => HandleTriggerEnter(col);

    public int AddXP(int xp_amount, Transform col = null)
    {
        if (Progression != null)
        {
            return Progression.AddXP(xp_amount, col);
        }
        return 0;
    }


    private void HandlePlayerLevelUp(int level)
    {
        print("LVL UP " + level);
        //if (_ps_lvlup != null) _ps_lvlup.Play();
        //if (_clip_lvlup != null) _clip_lvlup.Play();
    }


    void OnCollisionEnter(Collision col)
    {
        if (isDead) return;
        if (col == null || col.collider == null) return;

        if (col.collider.tag == "upgrade")
        {
            PowerupBase pb = col.collider.GetComponent<PowerupBase>();
            if (pb != null)
            {
                pb.TryCollect(this);
            }
            else
            {
                Destroy(col.collider.gameObject);
            }
        }

        BlockBase struckBlock = col.collider.GetComponent<BlockBase>();
        if (struckBlock == null) struckBlock = col.collider.GetComponentInParent<BlockBase>();
        AsteroidBase parentAsteroid = col.collider.GetComponentInParent<AsteroidBase>();

        bool isCore = col.collider.CompareTag("core") || (struckBlock != null && struckBlock.IsCore);
        bool isAsteroidOrBlock = col.collider.CompareTag("block") || struckBlock != null || parentAsteroid != null || isCore;

        if (isAsteroidOrBlock)
        {
            // Resolve target Rigidbody: if block is attached to an asteroid, apply impulse to whole asteroid
            Rigidbody otherRb = null;
            if (parentAsteroid != null)
            {
                otherRb = parentAsteroid.GetComponent<Rigidbody>();
                if (otherRb == null)
                {
                    otherRb = parentAsteroid.EnsureRigidbody();
                }
            }
            else
            {
                otherRb = col.rigidbody != null ? col.rigidbody : col.collider.GetComponent<Rigidbody>();
            }

            // Calculate contact normal and point
            Vector3 contactPoint = col.contactCount > 0 ? col.GetContact(0).point : col.collider.bounds.center;
            Vector3 normal = Vector3.zero;
            if (col.contactCount > 0)
            {
                normal = col.GetContact(0).normal;
            }
            else
            {
                normal = transform.position - col.collider.bounds.center;
            }
            normal.z = 0;

            if (normal.sqrMagnitude < 0.001f)
            {
                normal = transform.position - col.transform.position;
                normal.z = 0;
            }
            if (normal.sqrMagnitude < 0.001f)
            {
                normal = -transform.up;
            }
            normal.Normalize();

            // normal points from asteroid/block towards player
            Vector3 pushPlayerDir = normal;
            Vector3 pushBlockDir = -normal;

            float relativeSpeed = col.relativeVelocity.magnitude;
            // Standardized bounce impulse scaling: responsive push that scales cleanly with speed
            float bounceImpulse = Mathf.Max(relativeSpeed * 0.75f, 2.0f);

            // 1. Play physical collision impact sound at contact point
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.PlayCollision(contactPoint, relativeSpeed);
            }

            // 2. Damage player ship cleanly via TakeDamage (which triggers shield / hull damage audio, FX & HUD)
            float playerDamage = isCore ? Mathf.Max(1f, Mathf.Round(relativeSpeed * 0.5f)) : 1f;
            TakeDamage(playerDamage, pushPlayerDir, contactPoint, bounceImpulse);

            // 3. Bounce block or whole asteroid away with consistent physics
            if (otherRb != null)
            {
                Vector3 otherVel = otherRb.linearVelocity;
                if (Vector3.Dot(otherVel, pushPlayerDir) > 0)
                {
                    otherRb.linearVelocity -= Vector3.Project(otherVel, pushPlayerDir);
                }
                float massFactor = Mathf.Clamp(otherRb.mass, 1f, 3f);
                otherRb.AddForceAtPosition(pushBlockDir * (bounceImpulse * massFactor), contactPoint, ForceMode.Impulse);
            }

            // 4. Damage struck block / core from player kinetic impact
            int impactDamage = Mathf.Max(1, Mathf.RoundToInt(relativeSpeed * 0.5f));
            if (struckBlock != null && !struckBlock.IsDead)
            {
                struckBlock.block_receive_hit(transform, null, impactDamage);
            }
            else if (isCore && parentAsteroid != null)
            {
                parentAsteroid.core_receive_hit(transform, null);
            }

            // 5. Check for unconnected blocks on the asteroid cluster and detach them with impact force
            if (parentAsteroid != null)
            {
                Vector3 impactImpulse = pushBlockDir * Mathf.Max(relativeSpeed * 1.5f, 2.0f);
                parentAsteroid.check_for_unconnected(impactImpulse);
            }
        }

        UpdateWeaponHUD();
    }

    public void TakeDamage(float dmg, Vector3 direction = default, Vector3 contactPoint = default, float bounceImpulse = 0f)
    {
        if (isDead || isInvulnerable) return;

        float shieldDmg = 0f;
        float hullDmg = 0f;

        // Shield absorbs damage first
        if (shield_value > 0f)
        {
            if (dmg < shield_value)
            {
                shieldDmg = dmg;
                shield_value -= dmg;
                dmg = 0f;
                if (AudioManager.HasInstance) AudioManager.Instance.PlayShieldHit(transform.position, 1f, isPlayer: true);
            }
            else
            {
                shieldDmg = shield_value;
                dmg -= shield_value;
                shield_value = 0f;
                if (AudioManager.HasInstance) AudioManager.Instance.PlayShieldBreak(transform.position, 1f, isPlayer: true);
            }
            OnShieldHit?.Invoke(shieldDmg, contactPoint);
            UpdateShieldHUD();
        }

        // Remaining damage damages hull health
        if (dmg > 0f)
        {
            hullDmg = dmg;
            health_value -= dmg;
            if (health_value < 0f) health_value = 0f;
            if (AudioManager.HasInstance) AudioManager.Instance.PlayCollision(transform.position, bounceImpulse > 0f ? bounceImpulse : 5f);
            UpdateHealthHUD();
        }

        if (fx_shield_damage_tip != null && (shieldDmg > 0f || hullDmg > 0f))
        {
            GameObject showup = Instantiate(fx_shield_damage_tip, transform.position, Quaternion.identity) as GameObject;
            showup.transform.parent = null;
            TextMeshPro tmp = showup.GetComponentInChildren<TextMeshPro>();
            if (tmp != null)
            {
                if (shieldDmg > 0f && hullDmg > 0f)
                {
                    tmp.SetText("-" + shieldDmg.ToString("0") + " SHIELD! -" + hullDmg.ToString("0") + " HULL!");
                }
                else if (shieldDmg > 0f)
                {
                    tmp.SetText("-" + shieldDmg.ToString("0") + " SHIELD! ");
                }
                else
                {
                    tmp.SetText("-" + hullDmg.ToString("0") + " HULL! ");
                }
            }
        }

        // Bounce player ship away if impulse is imparted
        if (bounceImpulse > 0f && direction.sqrMagnitude > 0.001f)
        {
            direction.z = 0f;
            Rigidbody playerRb = GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 playerVel = playerRb.linearVelocity;
                if (Vector3.Dot(playerVel, direction) > 0)
                {
                    playerRb.linearVelocity -= Vector3.Project(playerVel, direction);
                }
                if (contactPoint != default)
                {
                    playerRb.AddForceAtPosition(direction * bounceImpulse, contactPoint, ForceMode.Impulse);
                }
                else
                {
                    playerRb.AddForce(direction * bounceImpulse, ForceMode.Impulse);
                }
            }
        }

        if (health_value <= 0)
        {
            Die();
        }
    }

    public void UpdateShieldHUD()
    {
        OnShieldChanged?.Invoke(shield_value, shield_max_value);
    }

    public void UpdateHealthHUD()
    {
        OnHealthChanged?.Invoke(health_value, health_max_value);
    }

    public void UpdateWeaponHUD() => Weapons.UpdateWeaponHUD();

    #region Weapons & Sockets Inspection (Delegated to PlayerWeapons)

    /// <summary>
    /// Checks if the player vessel has any weapon sockets (GunSocket components).
    /// </summary>
    public bool HasSockets(bool includeInactive = true) => Weapons.HasSockets(includeInactive);
    public bool HasAnySockets(bool includeInactive = true) => Weapons.HasAnySockets(includeInactive);

    /// <summary>
    /// Checks if the player has any weapon socket that is currently empty (unoccupied by a mounted gun).
    /// </summary>
    public bool HasAvailableSocket(bool includeInactive = true) => Weapons.HasAvailableSocket(includeInactive);

    /// <summary>
    /// Checks if the player has any guns equipped or mounted.
    /// </summary>
    public bool HasGuns(bool includeInactive = true) => Weapons.HasGuns(includeInactive);
    public bool HasAnyGuns(bool includeInactive = true) => Weapons.HasAnyGuns(includeInactive);

    public bool HasSocketsAndGuns(bool includeInactive = true) => Weapons.HasSocketsAndGuns(includeInactive);
    public bool HasAnySocketsAndGuns(bool includeInactive = true) => Weapons.HasAnySocketsAndGuns(includeInactive);
    public bool HasAnySocketsOrGuns(bool includeInactive = true) => Weapons.HasAnySocketsOrGuns(includeInactive);

    public bool CheckSocketsAndGuns(out bool hasSockets, out bool hasGuns, bool includeInactive = true) =>
        Weapons.CheckSocketsAndGuns(out hasSockets, out hasGuns, includeInactive);

    public (bool hasSockets, bool hasGuns) CheckSocketsAndGunsStatus(bool includeInactive = true) =>
        Weapons.CheckSocketsAndGunsStatus(includeInactive);

    public List<GunSocket> GetSockets(bool includeInactive = true) => Weapons.GetSockets(includeInactive);
    public List<Gun> GetEquippedGuns(bool includeInactive = true) => Weapons.GetEquippedGuns(includeInactive);
    public List<Component> GetGuns(bool includeInactive = true) => Weapons.GetGuns(includeInactive);

    public int SocketCount => Weapons.SocketCount;
    public int GunCount => Weapons.GunCount;

    #endregion

    #region Weapon Control & Combat Façade (Delegated to PlayerWeapons)

    public void AimWeapons(Vector3 targetWorldPosition) => Weapons.AimWeapons(targetWorldPosition);
    public bool FireWeapons() => Weapons.FireWeapons();
    public bool FireSocket(int socketIndex) => Weapons.FireSocket(socketIndex);

    public bool MountGun(Gun gunPrefab, int socketIndex = -1) => Weapons.MountGun(gunPrefab, socketIndex);
    public bool MountGunInstance(Gun gunInstance, int socketIndex = -1) => Weapons.MountGunInstance(gunInstance, socketIndex);
    public Gun DetachGun(int socketIndex = 0) => Weapons.DetachGun(socketIndex);
    public GunSocket AddSocket(string socketId = null, Vector3? localPosition = null) => Weapons.AddSocket(socketId, localPosition);
    public bool AddGun(Gun gunPrefab = null) => Weapons.AddGun(gunPrefab);

    public void SyncGunWithPlayerUpgrades(Gun g) => Weapons.SyncGunWithPlayerUpgrades(g);
    public void SyncAllEquippedGunsWithPlayerUpgrades() => Weapons.SyncAllEquippedGunsWithPlayerUpgrades();

    #endregion

    #region Gun Stat Upgrades Façade (Delegated to PlayerWeapons)

    public int UpgradeGuns(string gunId, float dmgBonus = 0f, float rateBonus = 0f, float forceBonus = 0f, int burstBonus = 0) =>
        Weapons.UpgradeGuns(gunId, dmgBonus, rateBonus, forceBonus, burstBonus);
    public int UpgradeKineticGuns(float dmgBonus = 1f, float forceBonus = 2f) => Weapons.UpgradeKineticGuns(dmgBonus, forceBonus);
    public int UpgradePlasmaGuns(float dmgBonus = 0.5f, float rateBonus = 0.5f, int burstBonus = 0) => Weapons.UpgradePlasmaGuns(dmgBonus, rateBonus, burstBonus);
    public void RefillAllWeaponsAmmo(int amount = -1) => Weapons.RefillAllWeaponsAmmo(amount);

    public bool SpendUpgradePoints(WeaponStatType stat, int pointsCost = 1, string gunId = null) =>
        Weapons.SpendUpgradePoints(stat, pointsCost, gunId);
    public bool UpgradeDamageWithPoints(int pointsCost = 1, string gunId = null) =>
        Weapons.UpgradeDamageWithPoints(pointsCost, gunId);
    public bool UpgradeForceWithPoints(int pointsCost = 1, string gunId = null) =>
        Weapons.UpgradeForceWithPoints(pointsCost, gunId);
    public bool UpgradeFireRateWithPoints(int pointsCost = 1, string gunId = null) =>
        Weapons.UpgradeFireRateWithPoints(pointsCost, gunId);

    #endregion
}
