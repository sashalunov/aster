using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UltraDeath : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        transform.parent.GetComponent<Animator>().SetInteger("state", 1);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnTriggerEnter(Collider col)
    {
        var newhitdamage = 0;

        if (!col.isTrigger)
        {
            if (col.tag == "block")
            {
                var dmg = col.transform.GetComponent<block0>()._hits;
                //AsteroidDestructible ast = col.transform.parent.GetComponent<AsteroidDestructible>();
                Rigidbody rb = col.transform.parent.GetComponent<Rigidbody>();

                // if (ast != null)
                // {
                //     col.transform.GetComponent<block0>().EndLife(col.transform.parent.parent);
                //     //rb.AddForceAtPosition((transform.up * dmg), transform.position, ForceMode.Impulse);
                //     ast.check_for_unconected();
                // }
                Destroy(col.gameObject);
            }
            if (col.tag == "core")
            {
                foreach (Transform child in col.transform)
                {
                    block0 b0 = child.GetComponent<block0>();
                    if (b0 != null)
                    {
                        //Instantiate(game_prefabs.block_destroy, child.position, Quaternion.identity);
                        b0.EndLife(col.transform.parent);
                    }
                    //Destroy(child.gameObject);
                }
               // col.transform.GetComponent<AsteroidDestructible>()._lvl.ChangeAstCount(-1);
               // col.transform.GetComponent<AsteroidDestructible>()._lvl.AddAsteroidKill(1);

                Destroy(col.gameObject);

            }



        }
    } 
    }

