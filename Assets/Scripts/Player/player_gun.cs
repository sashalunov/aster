using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Legacy player gun component. Retained for full backwards compatibility with existing scenes/prefabs.
/// Now bridges cleanly to the modern Gun and GunSocket weapon system when present.
/// </summary>
public class player_gun : MonoBehaviour
{
    public player _player;
    public bool locked = false;
    public Transform _point_base;
    public Transform _point_turret;
    public Transform _gun_muzzle_point;

    private Gun _modernGun;

    void Awake()
    {
        AutoResolveComponents();
    }

    public void AutoResolveComponents()
    {
        if (_player == null)
        {
            _player = GetComponentInParent<player>();
        }

        _modernGun = GetComponent<Gun>();

        if (_point_turret == null)
        {
            _point_turret = transform.Find("hull/turret0") ?? transform.Find("turret0") ?? transform.Find("turret") ?? transform;
        }

        if (_gun_muzzle_point == null)
        {
            _gun_muzzle_point = transform.Find("hull/turret0/gun0/ps_muzzle") ?? transform.Find("ps_muzzle") ?? transform.Find("fire_point") ?? transform;
        }

        if (_point_base == null)
        {
            _point_base = transform.Find("hull") ?? transform;
        }
    }

    void Start()
    {
        AutoResolveComponents();
    }

    public void fire()
    {
        if (_modernGun == null)
        {
            _modernGun = GetComponent<Gun>();
        }

        if (_modernGun != null)
        {
            _modernGun.TryFire();
            return;
        }

        if (_point_turret != null)
        {
            Animator anim = _point_turret.GetComponent<Animator>();
            if (anim != null) anim.Play("urret_fire");
        }

        fire_bullet();
    }

    void fire_bullet()
    {
        if (_player == null) _player = GetComponentInParent<player>();
        if (_player == null || _player.bullet == null) return;

        Vector3 spawnPos = _gun_muzzle_point != null ? _gun_muzzle_point.position : transform.position;
        Quaternion spawnRot = _point_turret != null ? _point_turret.rotation : transform.rotation;

        Transform clone = Instantiate(_player.bullet, spawnPos, spawnRot) as Transform;
        if (clone == null) return;

        bullet1 b1 = clone.GetComponent<bullet1>();
        if (b1 != null)
        {
            b1._player = _player;
            b1._hit_damage = (int)_player._bullet_dmg;
        }

        Collider myCol = _player.GetComponent<Collider>();
        Collider bulletCol = clone.GetComponent<Collider>();
        if (myCol != null && bulletCol != null)
        {
            Physics.IgnoreCollision(bulletCol, myCol);
        }

        Rigidbody playerRb = _player.GetComponent<Rigidbody>();
        Rigidbody bulletRb = clone.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            if (playerRb != null)
            {
                bulletRb.linearVelocity = playerRb.linearVelocity;
            }
            bulletRb.AddForce(clone.transform.up * _player._bullet_force, ForceMode.Impulse);
        }
    }
}