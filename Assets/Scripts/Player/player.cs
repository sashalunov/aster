using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class player : MonoBehaviour
{
    public Transform _ship_hull;
    public List<player_gun> _guns = new List<player_gun>();
    public List<GunSocket> _sockets = new List<GunSocket>();
    public Transform _guns_container;

    public Camera _player_cam;
    public AudioSource _clip_lvlup;

    public event System.Action<float, float, float> OnWeaponStatsChanged;
    public event System.Action<float, float> OnShieldChanged;
    public event System.Action<float, float> OnHealthChanged;
    public event System.Action OnDeath;

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

        if (game_prefabs.ultra_death != null)
        {
            Instantiate(game_prefabs.ultra_death, transform.position, Quaternion.identity);
        }

        OnDeath?.Invoke();
    }

    public ulong _xp_value => Progression != null ? Progression.CurrentXP : 0;
    public ulong _cred_value = 0;

    public int _wavelvl => Progression != null ? Progression.WaveLevel : 0;
    public string PlayerName => PlayerProfile.PlayerName;


    private Vector3 moveDirection = Vector3.zero;

    public Transform bullet;
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



    public ParticleSystem _ps_lvlup;

    public UnityEngine.Object shield_fx;
    public UnityEngine.Object pwrup_fx;

    public bool _can_play = false;
    public Collider _out_sphere;

    void Awake()
    {
        Progression = GetComponent<PlayerProgression>();
        if (Progression == null)
        {
            Progression = gameObject.AddComponent<PlayerProgression>();
        }
    }

    void OnEnable()
    {
        if (Progression != null)
        {
            Progression.OnPlayerLevelUp += HandlePlayerLevelUp;
            Progression.OnWeaponLevelUp += HandleWeaponLevelUp;
        }
    }

    void OnDisable()
    {
        if (Progression != null)
        {
            Progression.OnPlayerLevelUp -= HandlePlayerLevelUp;
            Progression.OnWeaponLevelUp -= HandleWeaponLevelUp;
        }
    }

    // Use this for initialization
    void Start()
    {
        shield_fx = Resources.Load("shield_damage");
        pwrup_fx = Resources.Load("powerup");
     
        PlayerMetaProgression.ApplyTo(this);

        UpdateShieldHUD();
        UpdateHealthHUD();
        UpdateWeaponHUD();
    }

    void PlaceTapMarker(Vector3 pos)
    {
        GameObject tap = Instantiate(Resources.Load("tap_marker"), pos, Quaternion.identity) as GameObject;
        Destroy(tap, 3.5f);
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 mousepos = Vector3.zero;
        Vector3 playerpos = Vector3.zero;
        playerpos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
        playerpos.y = 0;

        mousepos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
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

        if (_can_play)
        {
            AimWeapons(mousepos);

            _ship_hull.localEulerAngles = new Vector3(0, 0,Mathf.Rad2Deg * Mathf.Atan2(b.y - a.y, b.x - a.x) - 90f);
           // _guns_container.transform.localEulerAngles = new Vector3(0, 0, Mathf.Rad2Deg * Mathf.Atan2(b.y - a.y, b.x - a.x) - 90f);

            _tap_time_accum += Time.deltaTime;
            fire_time_accum += Time.deltaTime;
           

            if (Input.GetMouseButton(0)  )
            {
                
                if (_tap_time_accum >= _tap_rate && _tap_enabled && _tap_hold == false)
                {
                    _tap_time_accum = 0;
                    _tap_enabled = false;
                     _tap_hold = true;
                    PlaceTapMarker(gh.Length > 0 ? gh[0].point : mousepos);
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
                //transform.position = transform.position - new Vector3(speed * Time.deltaTime,0,0);
                //GetComponent<Rigidbody>().AddForce(new Vector3(-1, 0, 0) * _thrust_force * Time.deltaTime, ForceMode.Impulse);


            }
            if (Input.GetKey("right"))
            {
                //transform.position = transform.position + new Vector3(speed * Time.deltaTime, 0, 0);
                //GetComponent<Rigidbody>().AddForce(new Vector3(1, 0, 0) * _thrust_force * Time.deltaTime, ForceMode.Impulse);
            }

            //float dist = (transform.position.x - mousepos.x);
        }
        
        
        Vector3 campos = _player_cam.transform.localPosition;
       // float z = campos.z;
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
    /// It is recommended that you make only one call to Move or SimpleMove per frame.	

    void OnTriggerExit(Collider col)
    {
        // Destroy everything that leaves the trigger

        if (col.tag == "core")
        {
            Destroy(col.gameObject);
        }
    }

    public int AddXP(int xp_amount, Transform col = null)
    {
        if (Progression != null)
        {
            return Progression.AddXP(xp_amount, col);
        }
        return 0;
    }

    private void HandleWeaponLevelUp(int level, Vector3 pos)
    {
        print("WEAPON UP " + level);
        if (pwrup_fx != null)
        {
            GameObject rndbonus = Instantiate(pwrup_fx, pos, Quaternion.identity) as GameObject;
            powerup pup = rndbonus.GetComponent<powerup>();
            if (pup != null)
            {
                pup._type = (powerup.PowerupType)Random.Range(0, 3);
            }
        }
    }

    private void HandlePlayerLevelUp(int level)
    {
        print("LVL UP " + level);
        if (_ps_lvlup != null) _ps_lvlup.Play();
        if (_clip_lvlup != null) _clip_lvlup.Play();
    }


    void OnCollisionEnter(Collision col)
    {
        if (isDead) return;
        if (col == null || col.collider == null) return;

        if (col.collider.tag == "upgrade")
        {
            GameObject showup;

            powerup pup = col.collider.GetComponent<powerup>();
            print(pup._type);

            switch (pup._type)
            {
                case powerup.PowerupType.gun0_speed:
                    _fire_hz += 1;
                    _fire_rate = 1f / _fire_hz;

                    showup = Instantiate(Resources.Load("show_upgrade"), transform.position, Quaternion.identity) as GameObject;
                    showup.GetComponentInChildren<TextMeshPro>().SetText("SPEED UP! ");
                    showup.transform.localScale = new Vector3(1.6f, 1.6f, 1.2f);

                    break;

                case powerup.PowerupType.gun0_power:
                    _bullet_force += 1f;

                    showup = Instantiate(Resources.Load("show_upgrade"), transform.position, Quaternion.identity) as GameObject;
                    showup.GetComponentInChildren<TextMeshPro>().SetText("POWER UP! ");
                    showup.transform.localScale = new Vector3(1.6f, 1.6f, 1.2f);

                    break;
                case powerup.PowerupType.gun0_damage:
                    _bullet_dmg += 1f;

                    showup = Instantiate(Resources.Load("show_upgrade"), transform.position, Quaternion.identity) as GameObject;
                    showup.GetComponentInChildren<TextMeshPro>().SetText("DAMAGE UP! ");
                    showup.transform.localScale = new Vector3(1.6f, 1.6f, 1.2f);

                    break;

                case powerup.PowerupType.gun0_double:
                    showup = Instantiate(Resources.Load("show_upgrade"), transform.position, Quaternion.identity) as GameObject;
                    showup.GetComponentInChildren<TextMeshPro>().SetText("DOUBLE GUN! ");
                    showup.transform.localScale = new Vector3(1.6f, 1.6f, 1.2f);

                    //_gun0_double = true;
                    //foreach (Transform t in _gun0_points)
                    //	t.gameObject.SetActive(true);
                    break;

                case powerup.PowerupType.laser0:
                    showup = Instantiate(Resources.Load("show_upgrade"), transform.position, Quaternion.identity) as GameObject;
                    showup.GetComponentInChildren<TextMeshPro>().SetText("LASER GUN! ");
                    showup.transform.localScale = new Vector3(1.6f, 1.6f, 1.2f);

                    //_laser0 = true;
                    break;
                case powerup.PowerupType.shield:
                    shield_value += 1f;

                    showup = Instantiate(Resources.Load("shield_upgrade"), transform.position, Quaternion.identity) as GameObject;
                    showup.GetComponentInChildren<TextMeshPro>().SetText("SHIELD UP! ");
                    showup.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);

                    break;

            }
            //_player.AddXP(col.GetComponent<AsteroidDestructible>().core_destruct());

            Destroy(col.collider.gameObject);

        }

        if (col.collider.tag == "block")
        {
            block0 b0 = col.collider.transform.GetComponent<block0>();
            var dmg = b0 != null ? b0._hits : 1;

            if (shield_fx != null)
            {
                GameObject showup = Instantiate(shield_fx, transform.position, Quaternion.identity) as GameObject;
                showup.transform.parent = transform;
                TextMeshPro tmp = showup.GetComponentInChildren<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.SetText("-" + dmg.ToString() + " SHIELD! ");
                }
            }

            float blockDmg = dmg;
            if (shield_value > 0f)
            {
                if (blockDmg <= shield_value)
                {
                    shield_value -= blockDmg;
                    blockDmg = 0f;
                }
                else
                {
                    blockDmg -= shield_value;
                    shield_value = 0f;
                }
                UpdateShieldHUD();
            }
            if (blockDmg > 0f)
            {
                health_value -= blockDmg;
                if (health_value < 0f) health_value = 0f;
                UpdateHealthHUD();
            }

            // Resolve target Rigidbody: if block is attached to an asteroid, apply impulse to whole asteroid
            AsteroidBase parentAsteroid = col.collider.GetComponentInParent<AsteroidBase>();
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
                if (otherRb == null)
                {
                    otherRb = col.collider.gameObject.AddComponent<Rigidbody>();
                    otherRb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
                    otherRb.useGravity = false;
                    otherRb.linearDamping = 0.5f;
                    otherRb.angularDamping = 0.5f;
                    otherRb.mass = dmg;
                }
            }

            // Calculate bounce direction
            Vector3 pushBlockDir = Vector3.zero;
            if (col.contactCount > 0)
            {
                pushBlockDir = -col.GetContact(0).normal;
            }
            else
            {
                pushBlockDir = col.collider.bounds.center - transform.position;
            }
            pushBlockDir.z = 0;

            if (pushBlockDir.sqrMagnitude < 0.001f)
            {
                pushBlockDir = col.transform.position - transform.position;
                pushBlockDir.z = 0;
            }
            if (pushBlockDir.sqrMagnitude < 0.001f)
            {
                pushBlockDir = transform.up;
            }
            pushBlockDir.Normalize();

            Vector3 pushPlayerDir = -pushBlockDir;

            float relativeSpeed = col.relativeVelocity.magnitude;
            float bounceImpulse = Mathf.Max(relativeSpeed * 0.25f, 1f);
            Vector3 contactPoint = col.contactCount > 0 ? col.GetContact(0).point : col.collider.bounds.center;

            // Bounce player ship away
            Rigidbody playerRb = GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 playerVel = playerRb.linearVelocity;
                if (Vector3.Dot(playerVel, pushBlockDir) > 0)
                {
                    playerRb.linearVelocity -= Vector3.Project(playerVel, pushBlockDir);
                }
                playerRb.AddForceAtPosition(pushPlayerDir * bounceImpulse, contactPoint, ForceMode.Impulse);
            }

            // Bounce block or whole asteroid away
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

            // Calculate player impact impulse vector imparted to detached blocks
            Vector3 impactImpulse = pushBlockDir * Mathf.Max(relativeSpeed * 1.5f, 2.0f);

            // Damage struck block from player kinetic impact
            int impactDamage = Mathf.Max(1, Mathf.RoundToInt(relativeSpeed * 0.5f));
            if (b0 != null && !b0._dead)
            {
                b0.block_receive_hit(transform, null, impactDamage);
            }

            // Check for unconnected blocks on the asteroid cluster and detach them with impact force
            if (parentAsteroid != null)
            {
                parentAsteroid.check_for_unconnected(impactImpulse);
            }
        }
        if (col.collider.CompareTag("core"))
        {
            block0 coreB0 = col.collider.GetComponent<block0>();
            AsteroidBase astBase = col.collider.GetComponentInParent<AsteroidBase>();

            // Calculate impact energy based on relative speed
            float relativeSpeed = col.relativeVelocity.magnitude;
            float playerDamage = Mathf.Max(1f, Mathf.Round(relativeSpeed * 0.5f));

            // 1. Damage player ship cleanly via TakeDamage
            TakeDamage(playerDamage);

            // 2. Damage core block0 cleanly
            if (coreB0 != null)
            {
                coreB0.block_receive_hit(transform, null, (int)playerDamage);
            }
            else if (astBase != null)
            {
                astBase.core_receive_hit(transform, null);
            }

            // 3. Bounce player ship away from the core
            Vector3 pushDir = Vector3.zero;
            if (col.contactCount > 0)
            {
                pushDir = col.GetContact(0).normal;
            }
            if (pushDir.sqrMagnitude < 0.001f)
            {
                pushDir = (transform.position - col.collider.bounds.center).normalized;
            }
            pushDir.z = 0;
            if (pushDir.sqrMagnitude < 0.001f) pushDir = transform.up;

            Rigidbody playerRb = GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.AddForce(pushDir * Mathf.Max(relativeSpeed * 1.5f, 4f), ForceMode.Impulse);
            }

            // 4. Bounce the whole asteroid away too and check for unconnected blocks
            if (astBase != null)
            {
                Rigidbody astRb = astBase.GetComponent<Rigidbody>();
                if (astRb != null)
                {
                    astRb.AddForce(-pushDir * Mathf.Max(relativeSpeed * 1.5f, 4f), ForceMode.Impulse);
                }

                Vector3 impactImpulse = -pushDir * Mathf.Max(relativeSpeed * 1.5f, 2.0f);
                astBase.check_for_unconnected(impactImpulse);
            }
        }

        UpdateShieldHUD();
        UpdateHealthHUD();
        UpdateWeaponHUD();

        if (health_value <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(float dmg)
    {
        if (isDead) return;

        if (shield_fx != null)
        {
            GameObject showup = Instantiate(shield_fx, transform.position, Quaternion.identity) as GameObject;
            showup.transform.parent = transform;
            TextMeshPro tmp = showup.GetComponentInChildren<TextMeshPro>();
            if (tmp != null)
            {
                tmp.SetText("-" + dmg.ToString() + " SHIELD! ");
            }
        }

        // Shield absorbs damage first
        if (shield_value > 0f)
        {
            if (dmg <= shield_value)
            {
                shield_value -= dmg;
                dmg = 0f;
            }
            else
            {
                dmg -= shield_value;
                shield_value = 0f;
            }
            UpdateShieldHUD();
        }

        // Remaining damage damages hull health
        if (dmg > 0f)
        {
            health_value -= dmg;
            if (health_value < 0f) health_value = 0f;
            UpdateHealthHUD();
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
    /// Checks if the player has any guns equipped or mounted (legacy player_gun or modern Gun).
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive GameObjects in the check.</param>
    /// <returns>True if at least one gun exists on or under the player.</returns>
    public bool HasGuns(bool includeInactive = true)
    {
        // 1. Check legacy _guns list
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

        // 3. Check legacy player_gun components in children
        player_gun[] pGuns = GetComponentsInChildren<player_gun>(includeInactive);
        if (pGuns != null && pGuns.Length > 0) return true;

        // 4. Check modern Gun components in children
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
    /// Gets all distinct gun Components (either player_gun or Gun) attached or mounted to the player.
    /// </summary>
    public List<Component> GetGuns(bool includeInactive = true)
    {
        List<Component> result = new List<Component>();
        HashSet<Component> seen = new HashSet<Component>();

        if (_guns != null)
        {
            for (int i = 0; i < _guns.Count; i++)
            {
                player_gun pg = _guns[i];
                if (pg != null && (includeInactive || pg.gameObject.activeInHierarchy))
                {
                    if (seen.Add(pg))
                    {
                        result.Add(pg);
                    }
                }
            }
        }

        player_gun[] childPlayerGuns = GetComponentsInChildren<player_gun>(includeInactive);
        if (childPlayerGuns != null)
        {
            for (int i = 0; i < childPlayerGuns.Length; i++)
            {
                player_gun pg = childPlayerGuns[i];
                if (pg != null && seen.Add(pg))
                {
                    result.Add(pg);
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
    /// Total count of unique weapon sockets on the player.
    /// </summary>
    public int SocketCount => GetSockets().Count;

    /// <summary>
    /// Total count of mounted or equipped guns on the player.
    /// </summary>
    public int GunCount => GetGuns().Count;

    #endregion

    #region Weapon Control & Socket Management

    /// <summary>
    /// Aims all equipped weapons (GunSockets, standalone Guns, and legacy player_guns) toward target world position.
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

        // 3. Aim legacy player_gun components
        if (_guns != null)
        {
            for (int i = 0; i < _guns.Count; i++)
            {
                var g = _guns[i];
                if (g != null && g._point_turret != null)
                {
                    Vector3 na = g._point_turret.position;
                    g._point_turret.rotation = Quaternion.Euler(new Vector3(0, 0, Mathf.Rad2Deg * Mathf.Atan2(targetWorldPosition.y - na.y, targetWorldPosition.x - na.x) - 90f));
                }
            }
        }
    }

    /// <summary>
    /// Triggers weapon fire on all weapon systems (mounted socket guns, standalone guns, and legacy player_guns).
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

        // 3. Fire legacy player_gun components based on player fire_rate cadence
        if (_guns != null && _guns.Count > 0)
        {
            if (fire_time_accum >= _fire_rate)
            {
                fire_time_accum = 0;
                for (int i = 0; i < _guns.Count; i++)
                {
                    if (_guns[i] != null)
                    {
                        _guns[i].fire();
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
    /// Supports both modern Gun prefabs (Kinetic, Plasma) and legacy gun0 fallback.
    /// </summary>
    public bool AddGun(Gun gunPrefab = null)
    {
        if (GunCount >= PowerupManager.MAX_GUNS) return false;

        // 1. If no specific gun is provided, default to Kinetic gun, Plasma gun, or gun0
        if (gunPrefab == null)
        {
            GameObject kineticPrefab = Resources.Load<GameObject>("gunKinetic");
            if (kineticPrefab != null)
            {
                gunPrefab = kineticPrefab.GetComponent<Gun>();
            }
            if (gunPrefab == null)
            {
                GameObject plasmaPrefab = Resources.Load<GameObject>("gunPlasma");
                if (plasmaPrefab != null)
                {
                    gunPrefab = plasmaPrefab.GetComponent<Gun>();
                }
            }
            if (gunPrefab == null)
            {
                GameObject gun0Prefab = Resources.Load<GameObject>("gun0");
                if (gun0Prefab != null)
                {
                    gunPrefab = gun0Prefab.GetComponent<Gun>();
                }
            }
        }

        // 2. Try mounting to an existing empty socket
        if (gunPrefab != null && HasAvailableSocket())
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
            if (newSocket != null && gunPrefab != null)
            {
                bool mounted = newSocket.AttachGun(gunPrefab, gameObject);
                UpdateWeaponHUD();
                return mounted;
            }
        }

        // 4. Fallback legacy instantiation if legacy gun0 is used without sockets
        Transform gun0Res = Resources.Load<Transform>("gun0");
        if (_guns_container == null)
        {
            Transform container = transform.Find("gun_container");
            _guns_container = container != null ? container : transform;
        }
        if (gun0Res != null)
        {
            Transform newgun = Instantiate(gun0Res, transform.position, Quaternion.identity) as Transform;
            newgun.parent = _guns_container;
            player_gun pg = newgun.GetComponent<player_gun>();
            if (pg != null)
            {
                pg._player = this;
                _guns.Add(pg);
            }
            newgun.localPosition = Vector3.zero;
            newgun.localRotation = Quaternion.identity;
            UpdateWeaponHUD();
            return true;
        }

        return false;
    }

    #endregion
}