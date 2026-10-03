using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class powerup : MonoBehaviour
{
    public enum PowerupType
    {
        gun0_power = 0,
        gun0_speed = 1,
        gun0_damage = 2,
        shield,
        gun0_double,
        laser0
    }
    //private level_tunnel _lvl = null;

    public PowerupType _type;

    // Start is called before the first frame update
    void Start()
    {
        //_lvl = GameObject.Find("Level_generator").GetComponent<level_tunnel>();
    }

    // Update is called once per frame
    void Update()
    {
        // if (_lvl != null)
        // {

        //     if (transform.position.y < _lvl._bot_collider.position.y  || transform.position.x < -8f || transform.position.x > 8f || transform.position.y > _lvl._top_collider.position.y)
        //     {
        //         GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        //         Vector3 newpos = transform.position;
        //         newpos.y = _lvl._top_collider.position.y - 2f;
        //         newpos.x = Random.Range(-4.0f, 4.0f);
        //         transform.position = newpos;
        //         // GetComponent<Rigidbody>().AddForce(new Vector3(0, -1, 0) * mass * 2, ForceMode.Impulse);

        //     }

        // }

    }
}
