using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnerCollider : MonoBehaviour
{
    public bool _spawn_triger_active = false;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate()
    {

    }
    void OnTriggerEnter(Collider col)
    {
        _spawn_triger_active = true;
        //print("stay");
    }

    void OnTriggerStay(Collider col)
    {
        _spawn_triger_active = true;
        //print("stay");
    }

    void OnTriggerExit(Collider col)
    {
        _spawn_triger_active = false;
        //print("exit");

    }

}
