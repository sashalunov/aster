using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using TMPro;
using UnityEngine;

public class block0 : MonoBehaviour
{
    public Material[] _grade_mats;
    public TMP_Text _tmp_lvl;
    public int _level = 0;
    public int _hits = 1;
    public bool _dead = false;
    public bool _detached = false;
    public bool _bonus = false;
    // Start is called before the first frame update
    void Start()
    {
        UpdateText();
        UpdateMats();

        if (GetComponentInParent<Rigidbody>() == null)
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
            rb.mass = _hits > 0 ? _hits : 1;
        }
    }

    // Update is called once per frame
    void Update()
    {
    }
    void FixedUpdate() 
    { 
    }
    private void OnDestroy()
    {
    }
    public int block_receive_hit(Transform source, bullet1 b1)
    {
        var newhits = _hits - b1._hit_damage;
        var newhitdamage = b1._hit_damage - _hits;
        if (newhitdamage < 0) newhitdamage = 0;
        GameObject rndbonus;
        var damage = 0;

        if (b1._hit_damage >= _hits)
        {
            damage = _hits;
        }
        else
        {
            damage = b1._hit_damage;
        }

        if (damage > 0)
        {
            var hitFx = Resources.Load("show_blockhit");
            if (hitFx != null)
            {
                var bonus = Instantiate(hitFx, transform.position, Quaternion.identity) as GameObject; 
                if (bonus != null)
                {
                    var tmp = bonus.GetComponentInChildren<TextMeshPro>();
                    if (tmp != null) tmp.SetText("+" + damage.ToString());
                    bonus.transform.localScale = new Vector3(1.3f, 1.3f, 1.15f);
                }
            }
            if (b1 != null && b1._player != null)
            {
                b1._player.AddXP(damage, transform);
            }
        }
        if (newhits < 1)
        {
            Destroy(gameObject);
            _dead = true;
            var destroyFx = Resources.Load("blockdestroy");
            if (destroyFx != null)
            {
                Instantiate(destroyFx, transform.position, Quaternion.identity);
            }

            if (_bonus == true)
            {
                var pwrupFx = Resources.Load("powerup");
                if (pwrupFx != null)
                {
                    rndbonus = Instantiate(pwrupFx, transform.position, Quaternion.identity) as GameObject;
                    if (rndbonus != null)
                    {
                        powerup pup = rndbonus.GetComponent<powerup>();
                        if (pup != null) pup._type = (powerup.PowerupType)Random.Range(0, 3);
                    }
                }
            }

            return newhitdamage;
        }

        _hits = newhits;

        UpdateMats();
        UpdateText();

        return newhitdamage;
    }

    void UpdateText()
    {
        if (_tmp_lvl != null)
        {
            _tmp_lvl.SetText(_hits.ToString());
        }
    }

    void UpdateMats()
    {
        if (_hits < 1 || _grade_mats == null || _grade_mats.Length == 0) return;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;

        if (_hits > _grade_mats.Length)
        {
            mr.material = _grade_mats[_grade_mats.Length - 1];
        }
        else
        {
            mr.material = _grade_mats[_hits - 1];
        }
    }

    public void SetHits(int newhits)
    {
        _hits = newhits;
        UpdateText();
        UpdateMats();

    }
    public void AddBonus()
    {
        _bonus = true;
        var bonusPrefab = Resources.Load("additive_bonus");
        if (bonusPrefab != null)
        {
            GameObject bonus = Instantiate(bonusPrefab, new Vector3(transform.position.x, transform.position.y, -0.294f), Quaternion.identity) as GameObject;
            if (bonus != null) bonus.transform.parent = gameObject.transform;
        }
    }

    public void EndLife(Transform newparent)
    {
        transform.parent = newparent;
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezePositionZ;
        rb.mass = _hits;
        rb.useGravity = true;
        _detached = true;

        //Destroy(gameObject);
        //Instantiate(Resources.Load("blockdestroy"), transform.position, Quaternion.identity);
    }

    void OnCollisionEnter(Collision collisionInfo)
    {
        //Destroy(gameObject);
       // Instantiate(Resources.Load("blockdestroy"), transform.position, Quaternion.identity);
    }
}
