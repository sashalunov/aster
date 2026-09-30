using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class scroller : MonoBehaviour
{
    // Start is called before the first frame update
    public Material _mat;
    public Color _color;
    public float _speed = -0.1f;

    private float scroll_accum;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        scroll_accum += _speed * Time.deltaTime;
        _mat.SetTextureOffset("_MainTex", new Vector2(0, scroll_accum));
        _mat.SetColor("_Color", _color);

    }
}
