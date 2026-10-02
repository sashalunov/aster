using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class player_gun : MonoBehaviour
{
    public player _player;
    public bool locked = false;
    public Transform _point_base;
    public Transform _point_turret;

    // Start is called before the first frame update
    public Transform _gun_muzzle_point;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void fire()
    {
        var muzzle = Instantiate(_player.gun_muzzle_fx, _gun_muzzle_point.position, Quaternion.identity) as GameObject;
        //muzzle.GetComponentInChildren<TextMeshPro>().SetText("+" + damage.ToString());
        muzzle.transform.parent = _gun_muzzle_point;
        _point_turret.GetComponent<Animator>().Play("urret_fire");

        fire_bullet();
    }

    void fire_bullet()
    {
        Transform clone = Instantiate(_player.bullet, _gun_muzzle_point.position, _point_turret.rotation) as Transform;
        clone.GetComponent<bullet1>()._player = _player;
        clone.GetComponent<bullet1>()._hit_damage = (int)_player._bullet_dmg;
        Physics.IgnoreCollision(clone.GetComponent<Collider>(), _player.GetComponent<Collider>());
        clone.GetComponent<Rigidbody>().linearVelocity = _player.GetComponent<Rigidbody>().linearVelocity;
        clone.GetComponent<Rigidbody>().AddForce(clone.transform.up * (_player._bullet_force), ForceMode.Impulse);

    }
}