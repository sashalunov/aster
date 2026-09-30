using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class game_prefabs
{
    private static UnityEngine.Object _ultra_death;

    public static UnityEngine.Object ultra_death
    {
        get
        {
            if (_ultra_death == null)
            {
                _ultra_death = Resources.Load("UltraDeath");
            }
            return _ultra_death;
        }
    }
}
