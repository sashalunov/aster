using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UltraDeath : MonoBehaviour
{
    private Animator _animator;

    void Awake()
    {
        _animator = GetComponent<Animator>() ?? GetComponentInParent<Animator>();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (_animator != null)
        {
            _animator.SetInteger("state", 1);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Animation event callback from ultra_death.anim when the explosion animation completes.
    /// </summary>
    public void DestroyNow()
    {
        if (transform.parent != null)
        {
            Destroy(transform.parent.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider col)
    {
        if (col == null || col.isTrigger) return;

        if (col.tag == "block")
        {
            Destroy(col.gameObject);
        }
        else if (col.tag == "core")
        {
            AsteroidBase ast = col.GetComponent<AsteroidBase>() ?? col.GetComponentInParent<AsteroidBase>();
            if (ast != null)
            {
                ast.core_destruct(null);
            }
            else
            {
                foreach (Transform child in col.transform)
                {
                    BlockBase b0 = child.GetComponent<BlockBase>();
                    if (b0 != null)
                    {
                        b0.EndLife(null);
                    }
                }
                Destroy(col.gameObject);
            }
        }
    }
}
