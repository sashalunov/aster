using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using System.Xml.Xsl;

[ExecuteInEditMode]
public class AsteroidBase : MonoBehaviour
{
    [ContextMenuItem("Randomize Name", "Randomize")]
    public string Name;

    public int _block_hits = 1;
    public int _shell_hits = 2;

    public int _core_hits = 3;
    public int _core_shell = 0;
    public int _blocks = 1;
    //public int _block_max = 3;
    public Material[] _grade_mats;
    public TMP_Text _tmp_core_hits;

    private int[] neighbor_indexes = { 0, 1, 2, 3, 4, 5, 6, 7 };
    private Vector3[] neighbors = {
        new Vector3(-1,-1,0),new Vector3(-1,0,0), new Vector3(-1,1,0),
        new Vector3(0,1,0),new Vector3(1,1,0),
        new Vector3(1, 0, 0),new Vector3(1,-1,0),new Vector3(0,-1,0) };

    List<Transform> tres = new List<Transform>();

    public bool _ready = false;
    private System.Random srnd = new System.Random();
    public GameObject _block;

    public int _num_boxes_generated = 0;
    public Collider _spherec;
    // Start is called before the first frame update

    void Start()
    {
        SetHits(_core_hits);
     }

    void Update()
    {

    }

    void FixedUpdate()
    {

    }

    void reset_asteroid()
    {
    }

    [ContextMenu("Clear Asteroid")]
    private void Clear()
    {
        List<GameObject> gos = new List<GameObject>();

        foreach (Transform child in transform)
        {
            block0 b0 = child.GetComponent<block0>();
            if (b0 != null)
            {
                DestroyImmediate(child.gameObject);

               // Destroy(child.gameObject);
                //return add_raycast_neighbors(child.gameObject,block_hits);
            }
        }

    }

    [ContextMenu("Regenerate Asteroid")]
    private void Regenerate()
    {
   
        int rndmass = Random.Range(2, 6);
    
        _num_boxes_generated = 0;

        //generate_asteroid(1, 2, 2, 0, 1, 1);
        var gg = generate_shell(_core_shell, _shell_hits);


        foreach (GameObject g in gg)
        {
            for (int j = 0; j < _blocks; j++)
            {

                add_raycast_neighbors(g, _block_hits);
                
            }
        }

        if( gg.Count < 1 )
        {

            for (int i = 0; i < _blocks; i++)
            {
                add_raycast_neighbors(gameObject, _block_hits);

            }

        }
    }
    public int generate_asteroid(int coremass = 1, int massmin = 1, int massmax = 16, int shell = 0, int shlvl = 1, int blocklvl = 1)
    {
        int i = 0;
        _block_hits = blocklvl;
        _shell_hits = shlvl;
        _core_shell = shell;
        // random number of boxes
        _blocks = Random.Range(massmin, massmax);
        //generate_shell(shell, shlvl);

        //for (i = 0; i < _num_boxes_generated; i++)
        //{
        //    add_raycast_neighbors(gameObject, blocklvl);

        //}
        //_ready = true;

        _num_boxes_generated = 0;
        var gg = generate_shell(_core_shell, _shell_hits);
        foreach (GameObject g in gg)
        {
            for (int j = 0; j < _blocks; j++)
            {
                add_raycast_neighbors(g, _block_hits);
            }
        }
        if (gg.Count < 1)
        {
            for (i = 0; i < _blocks; i++)
            {
                add_raycast_neighbors(gameObject, _block_hits);
            }
        }


        return UpdateMass();

    }


    public void check_for_unconected()
    {
        tres.Clear();
        Transform[] m = scan_connected(transform);

        //foreach(Transform t in m)print(t);


        foreach (Transform child in transform)
        {
            block0 b0 = child.GetComponent<block0>();

            if (b0 != null)
            {

                if (tres.Contains(child))
                {

                }
                else
                {
                    b0.EndLife(transform.parent);
                }
            }
        }
        UpdateMass();
    }
    public List<GameObject> generate_shell(int thickness = 1, int hit = 3)
    {
        int i = 0;
        GameObject newbox;
        List<GameObject> shell_go = new List<GameObject>();

        if (thickness == -1)
        {


        }
        else if (thickness == 0)
        {
            foreach (Vector3 n in neighbors)
            {
                i++;
                if (i % 2 == 0)
                {
                    newbox = Instantiate(_block, transform.TransformPoint(n), transform.rotation);
                    newbox.transform.name = "b_" + i.ToString();
                    newbox.transform.parent = transform;
                    newbox.GetComponent<block0>().SetHits(hit);
                    _num_boxes_generated++;
                    shell_go.Add(newbox);

                }
            }

        }
        else if (thickness > 0)
        {
            i = 0;

            foreach (Vector3 n in neighbors)
            {
                newbox = Instantiate(_block, transform.TransformPoint(n), transform.rotation);
                newbox.transform.name = "b_" + i.ToString();
                newbox.transform.parent = transform;
                newbox.GetComponent<block0>().SetHits(hit);

                i++;
                _num_boxes_generated++;
                shell_go.Add(newbox);
            }

            if (thickness > 1)
            {
                List<GameObject> tg = new List<GameObject>();

                foreach (GameObject g in shell_go)
                {
                    for (int j = 0; j < thickness; j++) 
                    {
                        tg.Add(
                        add_raycast_neighbors(g, hit)
                        );
                    }
                }
                shell_go.AddRange(tg);
            }
        }
        UpdateMass();
        return shell_go;
    }
    public int core_receive_hit(Transform source, bullet1 b1)
    {
        int b1_dmg = (b1 != null) ? b1._hit_damage : 1;
        var newhits = _core_hits - b1_dmg;
        var newhitdamage = b1_dmg - _core_hits;
        if (newhitdamage < 0) newhitdamage = 0;

        var damage = 0;

        if (b1_dmg >= _core_hits)
        {
            damage = _core_hits;
        }
        else
        {
            damage = b1_dmg;
        }

        if (damage > 0 && newhits > 0)
        {
            if (b1 != null && b1._player != null)
            {
                b1._player.AddXP(damage, transform);
            }
            UpdateMass();
            var bonus = Instantiate(Resources.Load("show_corehit"), transform.position, Quaternion.identity) as GameObject;
            bonus.GetComponentInChildren<TextMeshPro>().SetText("+" + damage.ToString());
            bonus.transform.localScale = new Vector3(1.3f, 1.3f, 1.15f);
        }
        if (newhits < 1)
        {
            core_destruct(b1);
            //return newhitdamage;

        }

        _core_hits = newhits;
       // b1._hit_damage = newhitdamage;

        UpdateMats();
        UpdateText();

        return newhitdamage;

    }

    public int core_destruct(bullet1 b1)
    {
        int reward = 0;
        foreach(Transform child in transform)
        {
            block0 b0 = child.GetComponent<block0>();
            if (b0 != null)
            {
                reward += child.GetComponent<block0>()._hits;
                var destroyFx = Resources.Load("blockdestroy");
                if (destroyFx != null)
                {
                    Instantiate(destroyFx, child.position, Quaternion.identity);
                }
            }
            Destroy(child.gameObject);
        }
        if (b1 != null && b1._player != null)
        {
            b1._player.AddXP((reward + _core_hits), transform);
        }

        GameObject bonus = Instantiate(Resources.Load("show_corehit"), transform.position, Quaternion.identity) as GameObject;
        bonus.GetComponentInChildren<TextMeshPro>().SetText("+"+ (reward + _core_hits).ToString());
        bonus.transform.localScale = new Vector3(1.5f, 1.5f, 1.1f);


        // GetComponent<AudioSource>().PlayOneShot(GetComponent<AudioSource>().clip);
        Destroy(gameObject);

        var shieldFx = Resources.Load("powerup_shield") ?? Resources.Load("powerup");
        if (shieldFx != null)
        {
            GameObject shbonus = Instantiate(shieldFx, transform.position, Quaternion.identity) as GameObject;
            powerup pu = shbonus.GetComponent<powerup>();
            if (pu != null) pu._type = powerup.PowerupType.shield;
        }


        return reward + _core_hits;

    }

    public void box_got_hit(Collider col, Vector3 bdir)
    {
        Vector3 colpos = col.transform.position;
        this.GetComponent<Rigidbody>().AddForceAtPosition(bdir * 15f, colpos, ForceMode.Impulse);
        Instantiate(Resources.Load("additive_bonus"), colpos, Quaternion.identity);
       Destroy(col.gameObject);
    }

    private Vector3[] scan_free_neighbors(GameObject initial_go)
    {
        Ray ray;
        int free = 0;
        RaycastHit hit;
        List<Vector3> nres = new List<Vector3>();

        foreach (Vector3 v in neighbors)
        {
            Vector3 tp = initial_go.transform.TransformPoint(v);
            ray = new Ray(tp + new Vector3(0, 0, -2), Vector3.forward);

            if (Physics.Raycast(ray, out hit, 2))
            {
                continue;
            }
            free++;
            nres.Add(tp);
        }
        Vector3[] r = nres.ToArray();
        return r;
    }
    private GameObject add_raycast_neighbors(GameObject initial_go, int block_hits)
    {
        RaycastHit hit;

        Vector3[] n = scan_free_neighbors(initial_go);
        //print (n.Length);

        int numch = gen_num_children();

        if (n.Length > 0)
        {
            int rndinx = Random.Range(0, n.Length - 1);
            var b = Instantiate(_block, n[rndinx], transform.rotation) as GameObject;
            b.transform.name = "bl_" + (numch + 1);
            b.transform.parent = transform;
            b.GetComponent<block0>().SetHits(block_hits);
            _num_boxes_generated++;

            return b;
        }
        else
        {
            int childmass = 0;
            List<GameObject> gos = new List<GameObject>();

            foreach (Transform child in transform)
            {
                block0 b0 = child.GetComponent<block0>();
                if (b0 != null)
                {
                    gos.Add(child.gameObject);
                    //return add_raycast_neighbors(child.gameObject,block_hits);
                }
            }
            int rndchld = Random.Range(0, gos.Count() - 1);
            return add_raycast_neighbors(gos[rndchld], block_hits);

        }

        //int[] RandomNeighbors = neighbor_indexes.OrderBy(x => srnd.Next()).ToArray();
        //foreach (int idx in RandomNeighbors)
        //{
        //    Vector3 n = neighbors[idx];
        //    Vector3 tp = initial_go.transform.TransformPoint(n);
        //    Ray r = new Ray(tp + new Vector3(0,0,-10), Vector3.forward);

        //    if (Physics.Raycast(r, out hit))
        //    {
        //        print(tp);
        //        if (hit.collider.transform.IsChildOf(transform))
        //        {

        //            return add_raycast_neighbors(hit.collider.gameObject, block_hits);
        //        }
        //    }
        //    else
        //    {
        //       var b = Instantiate(_block, tp, transform.rotation) as GameObject;
        //        b.GetComponent<block0>().SetHits(block_hits);
        //        return b;
        //    }
        //}

        return null;

    }


    void UpdateText()
    {
        _tmp_core_hits.SetText(_core_hits.ToString());
    }

    void UpdateMats()
    {
        if (_core_hits < 1) return;

        GetComponent<MeshRenderer>().material = _grade_mats[_core_hits - 1];

    }

    public int UpdateMass()
    {
        int childmass = 0;
        foreach (Transform child in transform)
        {
            block0 b0 = child.GetComponent<block0>();
            if (b0 != null)
            {
                childmass += b0._hits;
            }
        }
        GetComponent<Rigidbody>().mass = childmass + _core_hits;
        return childmass + _core_hits;
    }

    public void SetHits(int newhits)
    {
        _core_hits = newhits;
        UpdateText();
        UpdateMats();
        UpdateMass();
    }
    public int gen_num_children()
    {
        int num_ch = 0;
        foreach (Transform child in transform)
        {
            block0 b0 = child.GetComponent<block0>();
            if (b0 != null)
                num_ch += 1;

        }
        return num_ch;
    }
    private Transform[] scan_connected(Transform initial_go)
    {
        Ray ray;
        int free = 0;
        RaycastHit hit;

        foreach (Vector3 v in neighbors){
            Vector3 tp = initial_go.TransformPoint(v);
            ray = new Ray(tp + new Vector3(0, 0, -1), Vector3.forward);

            // Check for a Asteroid.
            LayerMask mask = LayerMask.GetMask("Asteroid");

            if (Physics.Raycast(ray, out hit, 1.0f, mask) )
            {
                //if (hit.collider.isTrigger) continue;
                if (hit.collider.transform.parent == transform)
                {
                    if (hit.collider.GetComponent<block0>() == null) continue;
                    if (hit.collider.GetComponent<block0>()._dead) continue;

                    if (tres.Contains(hit.collider.transform))continue;

                    tres.Add(hit.collider.transform);
                    Transform[] tmp=scan_connected(hit.collider.transform);
                   // tres.AddRange(tmp);
                }
            }
            free++; 
        }
        Transform[] r = tres.ToArray();
        return r;
    }


}