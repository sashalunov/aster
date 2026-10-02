using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class player : MonoBehaviour
{
    public enum GunType
    {
        GUN0 = 0,
        GUN0_double,
        GUN0_triple,
        LASER0,

    }
    public Transform _ship_hull;
    public List<player_gun> _guns = new List<player_gun>();
    public Transform _guns_container;
    public Transform _gun0_prefab;

    public Camera _player_cam;
    public AudioSource _clip_lvlup;
    public AudioSource _clip_gun0_fire;

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

    float speed = 6.0f;
    public ulong _wpn_value = 0;

    public PlayerProgression Progression { get; private set; }

    public ulong _xp_value => Progression != null ? Progression.CurrentXP : 0;
    public ulong _cred_value = 0;
    public int _playerlvl => Progression != null ? Progression.PlayerLevel : 0;
    public int _playerlvlweapon => Progression != null ? Progression.WeaponLevel : 0;
    public int _wavelvl => Progression != null ? Progression.WaveLevel : 0;


    private Vector3 moveDirection = Vector3.zero;

    public Transform bullet;
    public ParticleSystem ps_thruster;
    public Transform urret;
    public Transform _laser0_trail;

    private float zoom_lastTime = 0;
    private float zoom_lerpTime = 2;

    public float _bullet_dmg = 1.0f;
    public float _fire_force = 1.0f;
    public float _thrust_force = 5.3f;
    public float _fire_hz = 1;
    public float _fire_rate = 1;

    private float fire_time_accum = 1;
    public float _laser_rate = 1;
    public float _laser_time = 1;
    public float _laser_time_accum = 0;
    bool _laser_enabled = false;

    private bool _tap_hold = false;
    private bool _tap_enabled = true;
    private float _tap_time_accum = 0;
    public float _tap_rate = 0.5f;


    public float _laser_cooldawn_accum = 0;
    public float _laser_max_dist = 5f;

    public ParticleSystem _ps_lvlup;

    public UnityEngine.Object shield_fx;
    public UnityEngine.Object pwrup_fx;
    public UnityEngine.Object gun_muzzle_fx;

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
        gun_muzzle_fx = Resources.Load("ps_muzzle");

        if (_guns.Count == 0)
        {
            AddGun();
        }

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
        RaycastHit hit;

        //CharacterController controller = GetComponent<CharacterController>();
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

        _guns_container.transform.localEulerAngles = new Vector3(0, 0, Mathf.Rad2Deg * Mathf.Atan2(b.y - a.y, b.x - a.x) - 90f);

        foreach (var g in _guns)
        {
            if (g != null)
            {
                Vector3 na = g._point_turret.position;

                g._point_turret.rotation = Quaternion.Euler(new Vector3(0, 0, Mathf.Rad2Deg * Mathf.Atan2(b.y - na.y, b.x - na.x) - 90f));
            }
        }


        if (_can_play)
        {
            _ship_hull.localEulerAngles = new Vector3(0, 0,Mathf.Rad2Deg * Mathf.Atan2(b.y - a.y, b.x - a.x) - 90f);

            _tap_time_accum += Time.deltaTime;
            fire_time_accum += Time.deltaTime;
            _laser_cooldawn_accum += Time.deltaTime;

            if (Input.GetMouseButton(0)  )
            {
                
                if (_tap_time_accum >= _tap_rate && _tap_enabled && _tap_hold == false)
                {
                    _tap_time_accum = 0;
                    _tap_enabled = false;
                     _tap_hold = true;
                    PlaceTapMarker(gh.Length > 0 ? gh[0].point : mousepos);
                }
                if (fire_time_accum >= _fire_rate)
                {
                    fire_time_accum = 0;
                    foreach (var g in _guns)
                    {
                        if (g != null)
                        {
                            g.fire();
                        }
                    }
                    GetComponentInChildren<AudioSource>().Play();
                }
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
                    _fire_force += 1f;

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
                parentAsteroid.check_for_unconected(impactImpulse);
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
                astBase.check_for_unconected(impactImpulse);
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

    void gun0_fire(Transform torigin)
    {

        Transform clone = Instantiate(bullet, torigin.position, urret.rotation) as Transform;
        clone.GetComponent<bullet1>()._player = this;
        //clone.GetComponent<bullet1>()._trail.startLifetime = 0.1f * (1 / _fire_rate);
        clone.GetComponent<bullet1>()._hit_damage = (int)_bullet_dmg;

        Physics.IgnoreCollision(clone.GetComponent<Collider>(), GetComponent<Collider>());
        // Add force to the cloned object in the object's forward direction
        clone.GetComponent<Rigidbody>().linearVelocity = GetComponent<Rigidbody>().linearVelocity;
        clone.GetComponent<Rigidbody>().AddForce(clone.transform.up * (_fire_force), ForceMode.Impulse);
        //GetComponent<Rigidbody>().AddForce((clone.transform.forward * -bullet_force * 0.25f) , ForceMode.Impulse);

        //GetComponentInChildren<AudioSource>().PlayOneShot(_clip_gun0_fire);

    }
    public void UpdateWeaponHUD()
    {
        OnWeaponStatsChanged?.Invoke(_fire_force, _fire_rate, _bullet_dmg);
    }

    public void UpdateShieldHUD()
    {
        OnShieldChanged?.Invoke(shield_value, shield_max_value);
    }

    public void UpdateHealthHUD()
    {
        OnHealthChanged?.Invoke(health_value, health_max_value);
    }

    public void AddGun()
    {
        if (_guns.Count >= 8) return;

        if (_gun0_prefab == null)
        {
            _gun0_prefab = Resources.Load<Transform>("gun0");
        }
        if (_guns_container == null)
        {
            Transform container = transform.Find("gun_container");
            _guns_container = container != null ? container : transform;
        }

        if (_gun0_prefab == null)
        {
            Debug.LogWarning("Cannot AddGun: _gun0_prefab is null and could not be loaded from Resources/gun0");
            return;
        }

        Transform newgun = Instantiate(_gun0_prefab, transform.position, Quaternion.identity) as Transform;
        newgun.parent = _guns_container;
        newgun.GetComponent<player_gun>()._player = this;
        _guns.Add(newgun.GetComponent<player_gun>());
        newgun.transform.localEulerAngles = new Vector3(0, 0, 0);
        newgun.transform.localScale = new Vector3(0, 0, 0);

        newgun.transform.DOScale(new Vector3(1, 1, 1), 1);
        if (_guns.Count > 1)
        {
            float newangle = 360f / _guns.Count;
            int cnt = 0;

            foreach (var gun in _guns) 
            {

                if(_guns.Count == 2)
                {
                    //gun.transform.localEulerAngles = new Vector3(0, 0, newangle * cnt + 90f);
                    gun.transform.DOLocalRotate(new Vector3(0, 0, newangle * cnt + 90f), 1);
                }
                else
                {
                    //gun.transform.localEulerAngles = new Vector3(0, 0, newangle * cnt);
                    gun.transform.DOLocalRotate(new Vector3(0, 0, newangle * cnt ), 1);

                }

                cnt++;

            }

        }
        UpdateWeaponHUD();
    }
}