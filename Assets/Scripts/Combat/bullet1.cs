using UnityEngine;
using System.Collections;
using System.Xml.Xsl;

public class bullet1 : MonoBehaviour
{
    private GameObject cloned;
    public Object for_delete;
    public bool isDead = false;
    public player _player;
    public float bullet_mass = 1;
    public ParticleSystem _trail;
    public int _hit_damage = 1;

    // Use this for initialization
    void Start()
    {
        Destroy(gameObject, 5f);
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerEnter(Collider col)
    {
        var newhitdamage = 0;

        if (col != null && !col.isTrigger && col.tag != "upgrade")
        {
            player hitPlayer = col.GetComponent<player>() ?? col.GetComponentInParent<player>();
            if (hitPlayer != null && hitPlayer != _player)
            {
                hitPlayer.TakeDamage(_hit_damage);
                Destroy(gameObject);
                return;
            }

            if (col.tag == "block")
            {
                //rint("BOX HIT");
                block0 b0 = col.transform.GetComponent<block0>();
                if (b0 != null)
                {
                    newhitdamage = b0.block_receive_hit(transform, this);
                }

                Transform parentTrans = col.transform.parent;
                Rigidbody rb = parentTrans != null ? parentTrans.GetComponent<Rigidbody>() : null;
                if (rb == null) rb = col.transform.GetComponent<Rigidbody>();

                if (parentTrans != null)
                {
                    AsteroidBase astBase = parentTrans.GetComponent<AsteroidBase>();

                    if (astBase != null)
                    {
                        if (rb != null) rb.AddForceAtPosition((transform.up * (_hit_damage + bullet_mass)), transform.position, ForceMode.Impulse);
                        astBase.check_for_unconected();
                    }
                    
                    else if (rb != null)
                    {
                        rb.AddForceAtPosition((transform.up * (_hit_damage + bullet_mass)), transform.position, ForceMode.Impulse);
                    }
                }
                else if (rb != null)
                {
                    rb.AddForceAtPosition((transform.up * (_hit_damage + bullet_mass)), transform.position, ForceMode.Impulse);
                }

            }
             if (col.tag == "core")
             {
                //print("CORE HIT");
                block0 b0 = col.transform.GetComponent<block0>();
                if (b0 != null)
                {
                    newhitdamage = b0.block_receive_hit(transform, this);
                }
                else
                {
                    AsteroidBase astBase = col.transform.GetComponent<AsteroidBase>() ?? col.transform.GetComponentInParent<AsteroidBase>();
                    if (astBase != null)
                    {
                        newhitdamage = astBase.core_receive_hit(transform, this);
                    }
                }

                Rigidbody rb = col.transform.GetComponent<Rigidbody>() ?? col.transform.GetComponentInParent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForceAtPosition((transform.up * _hit_damage), transform.position, ForceMode.Impulse);
                }
             }

            if (newhitdamage < 1)
            {

                Destroy(gameObject);

            }
            _hit_damage = newhitdamage;
        }
    }
    void OnTriggerLeave(Collider col)
    {
        
    }
}