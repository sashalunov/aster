using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class level_open : MonoBehaviour
{
    public GameObject _start_menu;
    public GameObject _death_menu;
    public player _player;

    public GameObject _block;
    public GameObject _asteroid;

    public List<Transform> _start_ast = new List<Transform>();
    public List<Transform> _start_blocks = new List<Transform>();
    public Animator _ground_anim;

    public Transform _asteroid_holder;
    public Transform _pwrups_holder;

    public GameObject shield_pwrup_fx;
    public GameObject block_hit;
    public GameObject block_bonus;
    public GameObject block_destroy;

    public int _max_asteroids = 1;
    public int _mmin = 2;
    public int _mmax = 4;
    public int _cmass = 1;
    public int _cshell = -1;
    public int _cshelllvl = 1;
    public int _blocklvl = 1;
    public bool _canSpawn = false;
     
    public float _spawn_force = 1;
    public float _spawn_rate = 1;

    private float spawn_time_accum = 0;

    // Start is called before the first frame update
    void Start()
    {
        DOTween.Init();
        if (_start_menu != null) _start_menu.SetActive(true);
        Physics.gravity = new Vector3(0, 0.0F, 0);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate()
    {
        spawn_time_accum += Time.fixedDeltaTime;
        if (spawn_time_accum >= _spawn_rate)
        {
            if (_player == null || _player._out_sphere == null) return;

            var vr = Random.insideUnitCircle.normalized * _player._out_sphere.transform.localScale.x / 2f;
            Vector3 cloneposition = new Vector3(_player.transform.position.x + vr.x,
                                                _player.transform.position.y + vr.y,
                                                _player.transform.position.z);

            GameObject clone = Instantiate(_asteroid, cloneposition, Quaternion.identity);
            clone.transform.parent = _asteroid_holder;
            clone.GetComponent<AsteroidBase>().SetHits(1);

            var mass = clone.GetComponent<AsteroidBase>().generate_asteroid(_cmass, _mmin, _mmax, _cshell, _cshelllvl, _blocklvl);

            clone.GetComponent<Rigidbody>().linearVelocity = _player.GetComponent<Rigidbody>().linearVelocity;
            var dir = _player.transform.position - cloneposition;
            clone.GetComponent<Rigidbody>().AddForce(dir * _spawn_force * mass, ForceMode.Impulse);

            spawn_time_accum = 0;
        }
    }
}
