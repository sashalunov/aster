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

public class player : MonoBehaviour
{
    public Transform _ship_hull;
    public List<Gun> _guns = new List<Gun>();
    public List<GunSocket> _sockets = new List<GunSocket>();
    public Transform _guns_container;

    public Camera _player_cam;

    public event System.Action<float, float, float> OnWeaponStatsChanged;
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

    public float health_value = 10.0f;
    public float health_max_value = 10.0f;

    public float shield_value = 10.0f;
    public float shield_max_value = 10.0f;
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

    public float _bullet_dmg = 1.0f;
    public float _bullet_force = 1.0f;
    public float _thrust_force = 5.3f;
    public float _fire_hz = 1;
    public float _fire_rate = 1;

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
        Progression = GetComponent<PlayerProgression>();
        if (Progression == null)
        {
            Progression = gameObject.AddComponent<PlayerProgression>();
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

        if (_upgradePoints > 0)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                UpgradeDamageWithPoints(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                UpgradeForceWithPoints(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                UpgradeFireRateWithPoints(1);
            }
        }

        if (Input.GetKey("left"))
        {
            //transform.position = transform.position - new Vector3(speed * Time.deltaTime,0,0);
            //GetComponent<Rigidbody>().AddForce(new Vector3(-1, 0, 0) * _thrust_force * Time.deltaTime, ForceMode.Impulse);


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
                _player_cam.transform.localPosition = campos;
            }
            if (Input.GetAxis("Mouse ScrollWheel") > 0 ) 
            {
                campos.z +=2f;
                zoom_lastTime = Time.time;
                _player_cam.transform.localPosition = campos;
            }
        }
    }

    /// It is recommended that you make only one call to Move or SimpleMove per frame.	

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
        if (isDead) return;

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

    public void UpdateWeaponHUD()
    {
        OnWeaponStatsChanged?.Invoke(_bullet_force, _fire_rate, _bullet_dmg);
    }

    public void UpdateShieldHUD()
    {
        OnShieldChanged?.Invoke(shield_value, shield_max_value);
    }

    public void UpdateHealthHUD()
    {
        OnHealthChanged?.Invoke(health_value, health_max_value);
    }

    #region Weapons & Sockets Inspection

    /// <summary>
    /// Checks if the player vessel has any weapon sockets (GunSocket components).
    /// Inspects both the serialized _sockets list and child GameObjects.
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive GameObjects in the check.</param>
    /// <returns>True if at least one GunSocket exists.</returns>
    public bool HasSockets(bool includeInactive = true)
    {
        if (_sockets != null)
        {
            for (int i = 0; i < _sockets.Count; i++)
            {
                if (_sockets[i] != null && (includeInactive || _sockets[i].gameObject.activeInHierarchy))
                    return true;
            }
        }

        GunSocket[] inChildren = GetComponentsInChildren<GunSocket>(includeInactive);
        return inChildren != null && inChildren.Length > 0;
    }

    /// <summary>
    /// Alias for HasSockets. Checks if the player vessel has any weapon sockets.
    /// </summary>
    public bool HasAnySockets(bool includeInactive = true) => HasSockets(includeInactive);

    /// <summary>
    /// Checks if the player has any weapon socket that is currently empty (unoccupied by a mounted gun).
    /// </summary>
    public bool HasAvailableSocket(bool includeInactive = true)
    {
        List<GunSocket> sockets = GetSockets(includeInactive);
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i] != null && !sockets[i].HasGun)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if the player has any guns equipped or mounted.
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive GameObjects in the check.</param>
    /// <returns>True if at least one gun exists on or under the player.</returns>
    public bool HasGuns(bool includeInactive = true)
    {
        // 1. Check direct _guns list
        if (_guns != null)
        {
            for (int i = 0; i < _guns.Count; i++)
            {
                if (_guns[i] != null && (includeInactive || _guns[i].gameObject.activeInHierarchy))
                    return true;
            }
        }

        // 2. Check modern guns mounted on sockets
        List<GunSocket> sockets = GetSockets(includeInactive);
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i] != null && sockets[i].HasGun)
            {
                if (includeInactive || (sockets[i].MountedGun != null && sockets[i].MountedGun.gameObject.activeInHierarchy))
                    return true;
            }
        }

        // 3. Check modern Gun components in children
        Gun[] guns = GetComponentsInChildren<Gun>(includeInactive);
        if (guns != null && guns.Length > 0) return true;

        return false;
    }

    /// <summary>
    /// Alias for HasGuns. Checks if the player vessel has any guns equipped or mounted.
    /// </summary>
    public bool HasAnyGuns(bool includeInactive = true) => HasGuns(includeInactive);

    /// <summary>
    /// Checks whether the player vessel has BOTH at least one weapon socket and at least one gun.
    /// </summary>
    public bool HasSocketsAndGuns(bool includeInactive = true)
    {
        return HasSockets(includeInactive) && HasGuns(includeInactive);
    }

    /// <summary>
    /// Alias for HasSocketsAndGuns.
    /// </summary>
    public bool HasAnySocketsAndGuns(bool includeInactive = true) => HasSocketsAndGuns(includeInactive);

    /// <summary>
    /// Checks whether the player vessel has either at least one socket OR at least one gun.
    /// </summary>
    public bool HasAnySocketsOrGuns(bool includeInactive = true)
    {
        return HasSockets(includeInactive) || HasGuns(includeInactive);
    }

    /// <summary>
    /// Checks sockets and guns in a single call, returning individual boolean flags via out parameters.
    /// Returns true only if both sockets and guns are present.
    /// </summary>
    public bool CheckSocketsAndGuns(out bool hasSockets, out bool hasGuns, bool includeInactive = true)
    {
        hasSockets = HasSockets(includeInactive);
        hasGuns = HasGuns(includeInactive);
        return hasSockets && hasGuns;
    }

    /// <summary>
    /// Returns a tuple containing boolean flags for (hasSockets, hasGuns).
    /// </summary>
    public (bool hasSockets, bool hasGuns) CheckSocketsAndGunsStatus(bool includeInactive = true)
    {
        return (HasSockets(includeInactive), HasGuns(includeInactive));
    }

    /// <summary>
    /// Gets all unique GunSocket components on this vessel.
    /// </summary>
    public List<GunSocket> GetSockets(bool includeInactive = true)
    {
        List<GunSocket> result = new List<GunSocket>();
        HashSet<GunSocket> seen = new HashSet<GunSocket>();

        if (_sockets != null)
        {
            for (int i = 0; i < _sockets.Count; i++)
            {
                GunSocket s = _sockets[i];
                if (s != null && (includeInactive || s.gameObject.activeInHierarchy))
                {
                    if (seen.Add(s))
                    {
                        result.Add(s);
                    }
                }
            }
        }

        GunSocket[] inChildren = GetComponentsInChildren<GunSocket>(includeInactive);
        if (inChildren != null)
        {
            for (int i = 0; i < inChildren.Length; i++)
            {
                GunSocket s = inChildren[i];
                if (s != null && seen.Add(s))
                {
                    result.Add(s);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets all distinct Gun components attached or mounted to the player.
    /// </summary>
    public List<Gun> GetEquippedGuns(bool includeInactive = true)
    {
        List<Gun> result = new List<Gun>();
        HashSet<Gun> seen = new HashSet<Gun>();

        if (_guns != null)
        {
            for (int i = 0; i < _guns.Count; i++)
            {
                Gun g = _guns[i];
                if (g != null && (includeInactive || g.gameObject.activeInHierarchy))
                {
                    if (seen.Add(g))
                    {
                        result.Add(g);
                    }
                }
            }
        }

        Gun[] childGuns = GetComponentsInChildren<Gun>(includeInactive);
        if (childGuns != null)
        {
            for (int i = 0; i < childGuns.Length; i++)
            {
                Gun g = childGuns[i];
                if (g != null && seen.Add(g))
                {
                    result.Add(g);
                }
            }
        }

        List<GunSocket> sockets = GetSockets(includeInactive);
        for (int i = 0; i < sockets.Count; i++)
        {
            GunSocket s = sockets[i];
            if (s != null && s.HasGun && s.MountedGun != null)
            {
                if (includeInactive || s.MountedGun.gameObject.activeInHierarchy)
                {
                    if (seen.Add(s.MountedGun))
                    {
                        result.Add(s.MountedGun);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Legacy compatibility accessor returning equipped Gun components.
    /// </summary>
    public List<Component> GetGuns(bool includeInactive = true)
    {
        List<Component> result = new List<Component>();
        List<Gun> equipped = GetEquippedGuns(includeInactive);
        for (int i = 0; i < equipped.Count; i++)
        {
            result.Add(equipped[i]);
        }
        return result;
    }

    /// <summary>
    /// Total count of unique weapon sockets on the player.
    /// </summary>
    public int SocketCount => GetSockets().Count;

    /// <summary>
    /// Total count of mounted or equipped guns on the player.
    /// </summary>
    public int GunCount => GetEquippedGuns().Count;

    #endregion

    #region Weapon Control & Socket Management

    /// <summary>
    /// Aims all equipped weapons (GunSockets and standalone Guns) toward target world position.
    /// </summary>
    public void AimWeapons(Vector3 targetWorldPosition)
    {
        // 1. Aim mounted guns on GunSockets
        List<GunSocket> sockets = GetSockets(false);
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i] != null)
            {
                sockets[i].AimMountedGun(targetWorldPosition);
            }
        }

        // 2. Aim standalone modern Guns not mounted on a GunSocket
        Gun[] directGuns = GetComponentsInChildren<Gun>(false);
        for (int i = 0; i < directGuns.Length; i++)
        {
            if (directGuns[i] != null && directGuns[i].GetComponentInParent<GunSocket>() == null)
            {
                directGuns[i].AimAt(targetWorldPosition);
            }
        }

        // 3. Aim standalone guns in _guns
        if (_guns != null)
        {
            for (int i = 0; i < _guns.Count; i++)
            {
                if (_guns[i] != null && _guns[i].GetComponentInParent<GunSocket>() == null)
                {
                    _guns[i].AimAt(targetWorldPosition);
                }
            }
        }
    }

    /// <summary>
    /// Triggers weapon fire on all weapon systems (mounted socket guns and standalone guns).
    /// </summary>
    /// <returns>True if at least one weapon fired.</returns>
    public bool FireWeapons()
    {
        bool anyFired = false;

        // 1. Fire mounted guns on GunSockets
        List<GunSocket> sockets = GetSockets(false);
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i] != null && sockets[i].HasGun)
            {
                if (sockets[i].TryFireMountedGun())
                {
                    anyFired = true;
                }
            }
        }

        // 2. Fire direct Gun components not attached to a socket
        Gun[] directGuns = GetComponentsInChildren<Gun>(false);
        for (int i = 0; i < directGuns.Length; i++)
        {
            if (directGuns[i] != null && directGuns[i].GetComponentInParent<GunSocket>() == null)
            {
                if (directGuns[i].TryFire())
                {
                    anyFired = true;
                }
            }
        }

        // 3. Fire direct guns in _guns
        if (_guns != null)
        {
            for (int i = 0; i < _guns.Count; i++)
            {
                if (_guns[i] != null && _guns[i].GetComponentInParent<GunSocket>() == null)
                {
                    if (_guns[i].TryFire())
                    {
                        anyFired = true;
                    }
                }
            }
        }

        if (anyFired)
        {
            AudioSource audio = GetComponentInChildren<AudioSource>();
            if (audio != null && !audio.isPlaying)
            {
                audio.Play();
            }
        }

        return anyFired;
    }

    /// <summary>
    /// Mounts a gun prefab to a specified socket index or the first available socket.
    /// </summary>
    public bool MountGun(Gun gunPrefab, int socketIndex = -1)
    {
        if (gunPrefab == null) return false;

        List<GunSocket> sockets = GetSockets(true);
        if (socketIndex >= 0 && socketIndex < sockets.Count)
        {
            return sockets[socketIndex].AttachGun(gunPrefab, gameObject);
        }

        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i] != null && !sockets[i].HasGun)
            {
                return sockets[i].AttachGun(gunPrefab, gameObject);
            }
        }

        return false;
    }

    /// <summary>
    /// Mounts an existing gun instance to a specified socket index or the first available socket.
    /// </summary>
    public bool MountGunInstance(Gun gunInstance, int socketIndex = -1)
    {
        if (gunInstance == null) return false;

        List<GunSocket> sockets = GetSockets(true);
        if (socketIndex >= 0 && socketIndex < sockets.Count)
        {
            return sockets[socketIndex].AttachGunInstance(gunInstance, gameObject);
        }

        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i] != null && !sockets[i].HasGun)
            {
                return sockets[i].AttachGunInstance(gunInstance, gameObject);
            }
        }

        return false;
    }

    /// <summary>
    /// Detaches and returns the mounted gun from a socket.
    /// </summary>
    public Gun DetachGun(int socketIndex = 0)
    {
        List<GunSocket> sockets = GetSockets(true);
        if (socketIndex >= 0 && socketIndex < sockets.Count && sockets[socketIndex] != null)
        {
            return sockets[socketIndex].DetachGun();
        }
        return null;
    }

    /// <summary>
    /// Adds a new GunSocket component to the player ship.
    /// </summary>
    public GunSocket AddSocket(string socketId = null, Vector3? localPosition = null)
    {
        Transform container = _guns_container != null ? _guns_container : transform.Find("hull/gun_sockets");
        if (container == null && _ship_hull != null)
        {
            container = _ship_hull;
        }
        if (container == null)
        {
            container = transform;
        }

        int index = SocketCount;
        string name = string.IsNullOrEmpty(socketId) ? $"socket_{index}" : socketId;

        GameObject socketObj = new GameObject(name);
        socketObj.transform.SetParent(container);
        socketObj.transform.localPosition = localPosition ?? new Vector3(0f, 0f, 0f);
        socketObj.transform.localRotation = Quaternion.identity;

        GunSocket socket = socketObj.AddComponent<GunSocket>();
        _sockets.Add(socket);
        return socket;
    }

    /// <summary>
    /// Adds a gun to the player, mounting onto an available empty socket or creating a new socket.
    /// Supports modern Gun prefabs (Kinetic Cannon, Plasma Repeater).
    /// </summary>
    public bool AddGun(Gun gunPrefab = null)
    {
        if (GunCount >= PowerupManager.MAX_GUNS) return false;

        // 1. If no specific gun is provided, default to Kinetic gun or Plasma gun
        if (gunPrefab == null)
        {
            gunPrefab = PrefabManager.Get<Gun>(PrefabId.GunKinetic);
            if (gunPrefab == null)
            {
                gunPrefab = PrefabManager.Get<Gun>(PrefabId.GunPlasma);
            }
        }

        if (gunPrefab == null) return false;

        // 2. Try mounting to an existing empty socket
        if (HasAvailableSocket())
        {
            if (MountGun(gunPrefab))
            {
                UpdateWeaponHUD();
                return true;
            }
        }

        // 3. If no empty socket, try creating a new socket up to MAX_GUNS
        if (SocketCount < PowerupManager.MAX_GUNS)
        {
            GunSocket newSocket = AddSocket();
            if (newSocket != null)
            {
                bool mounted = newSocket.AttachGun(gunPrefab, gameObject);
                UpdateWeaponHUD();
                return mounted;
            }
        }

        // 4. Fallback: attach gun directly under guns_container or ship
        Transform container = _guns_container != null ? _guns_container : transform.Find("gun_container") ?? transform;
        Gun newGunInstance = Instantiate(gunPrefab, container.position, container.rotation, container);
        newGunInstance.SetOwner(gameObject);
        _guns.Add(newGunInstance);
        UpdateWeaponHUD();
        return true;
    }

    #endregion

    #region Gun Stat Upgrades

    /// <summary>
    /// Upgrades all guns matching the gunId (or all guns if gunId is null/empty).
    /// Returns the number of guns upgraded.
    /// </summary>
    public int UpgradeGuns(string gunId, float dmgBonus = 0f, float rateBonus = 0f, float forceBonus = 0f, int burstBonus = 0)
    {
        int count = 0;
        List<Gun> guns = GetEquippedGuns();
        for (int i = 0; i < guns.Count; i++)
        {
            Gun g = guns[i];
            if (g == null) continue;
            if (string.IsNullOrEmpty(gunId) || (g.Data != null && g.Data.gunId == gunId))
            {
                g.UpgradeStats(dmgBonus, rateBonus, forceBonus, burstBonus);
                count++;
            }
        }
        UpdateWeaponHUD();
        return count;
    }

    /// <summary>
    /// Specifically upgrades Kinetic guns equipped on the player.
    /// </summary>
    public int UpgradeKineticGuns(float dmgBonus = 1f, float forceBonus = 2f)
    {
        return UpgradeGuns(GunKinetic.DEFAULT_GUN_ID, dmgBonus, 0f, forceBonus, 0);
    }

    /// <summary>
    /// Specifically upgrades Plasma guns equipped on the player.
    /// </summary>
    public int UpgradePlasmaGuns(float dmgBonus = 0.5f, float rateBonus = 0.5f, int burstBonus = 0)
    {
        return UpgradeGuns(GunPlasma.DEFAULT_GUN_ID, dmgBonus, rateBonus, 0f, burstBonus);
    }

    /// <summary>
    /// Refills ammunition on all finite-ammo guns equipped on the player.
    /// </summary>
    public void RefillAllWeaponsAmmo(int amount = -1)
    {
        List<Gun> guns = GetEquippedGuns();
        for (int i = 0; i < guns.Count; i++)
        {
            if (guns[i] == null) continue;
            if (amount < 0)
            {
                guns[i].RefillAmmo();
            }
            else
            {
                guns[i].AddAmmo(amount);
            }
        }
    }

    /// <summary>
    /// Spends collected upgrade points on a weapon stat (Damage, Force, or FireRate).
    /// Enhances both base ship weapon attributes and currently equipped Gun instances.
    /// </summary>
    public bool SpendUpgradePoints(WeaponStatType stat, int pointsCost = 1, string gunId = null)
    {
        if (pointsCost <= 0 || !HasUpgradePoints(pointsCost)) return false;

        _upgradePoints -= pointsCost;
        OnUpgradePointsChanged?.Invoke(_upgradePoints);

        switch (stat)
        {
            case WeaponStatType.Damage:
                _bullet_dmg = Mathf.Clamp(_bullet_dmg + 1f * pointsCost, 1f, PowerupManager.MAX_BULLET_DMG);
                UpgradeGuns(gunId, dmgBonus: 1f * pointsCost);
                if (PowerupManager.Instance != null)
                {
                    PowerupManager.Instance.SpawnFloatingFeedback("+DMG UPGRADE!", transform.position, new Color(1f, 0.3f, 0.3f));
                }
                break;

            case WeaponStatType.Force:
                _bullet_force = Mathf.Clamp(_bullet_force + 2f * pointsCost, 1f, PowerupManager.MAX_BULLET_FORCE);
                UpgradeGuns(gunId, forceBonus: 2f * pointsCost);
                if (PowerupManager.Instance != null)
                {
                    PowerupManager.Instance.SpawnFloatingFeedback("+FORCE UPGRADE!", transform.position, new Color(0.9f, 0.3f, 1f));
                }
                break;

            case WeaponStatType.FireRate:
                _fire_hz = Mathf.Clamp(_fire_hz + 0.5f * pointsCost, 1f, PowerupManager.MAX_FIRE_HZ);
                _fire_rate = 1f / _fire_hz;
                UpgradeGuns(gunId, rateBonus: 0.5f * pointsCost);
                if (PowerupManager.Instance != null)
                {
                    PowerupManager.Instance.SpawnFloatingFeedback("+RATE UPGRADE!", transform.position, new Color(1f, 0.9f, 0.2f));
                }
                break;
        }

        UpdateWeaponHUD();
        return true;
    }

    /// <summary>
    /// Spends upgrade points to boost weapon damage.
    /// </summary>
    public bool UpgradeDamageWithPoints(int pointsCost = 1, string gunId = null) =>
        SpendUpgradePoints(WeaponStatType.Damage, pointsCost, gunId);

    /// <summary>
    /// Spends upgrade points to boost projectile impulse force.
    /// </summary>
    public bool UpgradeForceWithPoints(int pointsCost = 1, string gunId = null) =>
        SpendUpgradePoints(WeaponStatType.Force, pointsCost, gunId);

    /// <summary>
    /// Spends upgrade points to boost weapon fire rate.
    /// </summary>
    public bool UpgradeFireRateWithPoints(int pointsCost = 1, string gunId = null) =>
        SpendUpgradePoints(WeaponStatType.FireRate, pointsCost, gunId);

    #endregion
}