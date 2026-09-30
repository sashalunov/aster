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
    public event System.Action OnDeath;

    float speed = 6.0f;
    public ulong _wpn_value = 0;
    public float shield_value = 10.0f;
    public float shield_max_value = 10.0f;

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
            //AddGun();
        }

        UpdateShieldHUD();
        UpdateWeaponHUD();
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

            fire_time_accum += Time.deltaTime;
            _laser_cooldawn_accum += Time.deltaTime;

            if (Input.GetMouseButton(0))
            {
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

                //if (_laser_cooldawn_accum >= _laser_rate && _laser0)
                //{
                //             _laser_cooldawn_accum = 0;
                //	_laser_enabled = true;
                //             _laser0_trail.gameObject.SetActive(true);
                //             _laser0_trail.GetComponent<Animation>().Play();
                //         }
            }

            if (_laser_enabled)
            {
                _laser_time_accum += Time.deltaTime;
                Vector3[] poss = { Vector3.up * _laser_max_dist, Vector3.zero };
                Ray r = new Ray(transform.position, dir * _laser_max_dist);
                var rc = Physics.Raycast(r, out hit, _laser_max_dist);
                //if (rc) poss[0] = Vector3.up * hit.distance;

                if (_laser_time_accum >= _laser_time)
                {
                    RaycastHit[] h = Physics.RaycastAll(r, _laser_max_dist);

                    foreach (RaycastHit rhit in h)
                    {

                        //if (rc == true)
                        //{
                        if (rhit.collider.tag == "block" /*|| hit.collider.tag == "core"*/)
                        {
                            //print(hit.collider);
                            Rigidbody rr = rhit.collider.transform.parent.gameObject.GetComponent<Rigidbody>();
                            //asteroidController ac = cloneparent.GetComponent<asteroidController>();
                            if (rr != null)
                            {
                                rr.AddForceAtPosition((transform.forward * 45), rhit.point, ForceMode.Impulse);

                                Instantiate(Resources.Load("additive_bonus"), rhit.point, Quaternion.identity);
                               
                                Destroy(rhit.collider.gameObject);

                            }
                        }
                        if (rhit.collider.tag == "core")
                        {
                         

                            Destroy(rhit.collider.gameObject);

                        }

                    }

                    _laser_time_accum = 0;
                    _laser_cooldawn_accum = 0;
                    _laser_enabled = false;
                    _laser0_trail.gameObject.SetActive(false);
                }
                _laser0_trail.GetComponent<LineRenderer>().SetPositions(poss);
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
                GetComponent<Rigidbody>().AddForce(new Vector3(-1, 0, 0) * _thrust_force * Time.deltaTime, ForceMode.Impulse);


            }
            if (Input.GetKey("right"))
            {
                //transform.position = transform.position + new Vector3(speed * Time.deltaTime, 0, 0);
                GetComponent<Rigidbody>().AddForce(new Vector3(1, 0, 0) * _thrust_force * Time.deltaTime, ForceMode.Impulse);
            }

            float dist = (transform.position.x - mousepos.x);


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

            shield_value -= dmg;

            // Resolve target Rigidbody (on block, asteroid parent, or add if static)
            Rigidbody otherRb = col.rigidbody;
            if (otherRb == null)
            {
                otherRb = col.collider.GetComponent<Rigidbody>();
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

            // Bounce player ship away
            Rigidbody playerRb = GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 playerVel = playerRb.linearVelocity;
                if (Vector3.Dot(playerVel, pushBlockDir) > 0)
                {
                    playerRb.linearVelocity -= Vector3.Project(playerVel, pushBlockDir);
                }
                playerRb.AddForceAtPosition(pushPlayerDir * bounceImpulse, col.contacts[0].point, ForceMode.Impulse);
            }

            // Bounce block or asteroid away
            if (otherRb != null)
            {
                Vector3 otherVel = otherRb.linearVelocity;
                if (Vector3.Dot(otherVel, pushPlayerDir) > 0)
                {
                    otherRb.linearVelocity -= Vector3.Project(otherVel, pushPlayerDir);
                }
                float massFactor = Mathf.Clamp(otherRb.mass, 1f, 3f);
                otherRb.AddForceAtPosition(pushBlockDir * (bounceImpulse * massFactor), col.contacts[0].point, ForceMode.Impulse);
            }
        }
        if (col.collider.tag == "core")
        {
            //newhitdamage = col.transform.GetComponent<AsteroidDestructible>().core_receive_hit(transform, this);
            int reward = 0;

            foreach (Transform child in col.collider.transform)
            {
                block0 b0 = child.GetComponent<block0>();
                if (b0 != null)
                {
                    reward += child.GetComponent<block0>()._hits;
                    var bDestroy = Resources.Load("blockdestroy");
                    if (bDestroy != null) Instantiate(bDestroy, child.position, Quaternion.identity);
                }
                Destroy(child.gameObject);
                //Destroy(child.gameObject);
            }
            //col.collider.transform.GetComponent<AsteroidDestructible>()._lvl.ChangeAstCount(-1);
            //col.collider.transform.GetComponent<AsteroidDestructible>()._lvl.AddAsteroidKill(1);

            var fh = reward + col.collider.transform.GetComponent<AsteroidBase>()._core_hits;
            Destroy(col.collider.gameObject);
            shield_value -= reward + col.collider.transform.GetComponent<AsteroidBase>()._core_hits;

            GameObject showup = Instantiate(shield_fx, transform.position, Quaternion.identity) as GameObject;
            showup.transform.parent = transform;
            showup.GetComponentInChildren<TextMeshPro>().SetText("-" + fh.ToString() + " SHIELD! ");
            showup.transform.localScale = new Vector3(1.6f, 1.6f, 1.2f);


        }
        UpdateShieldHUD();
        UpdateWeaponHUD();

        if (shield_value < 0)
        {
            _can_play = false;
            GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

            if (game_prefabs.ultra_death != null)
            {
                Instantiate(game_prefabs.ultra_death, transform.position, Quaternion.identity);
            }

            OnDeath?.Invoke();

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