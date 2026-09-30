using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
   public GameObject _menu_;
    public GameObject _menuRe_;

    //public level_tunnel _level_generator;
    public GameObject _ultradeath_on_start;
    public GameObject _wave_menu;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void StartGame()
    {
        _menu_.SetActive(false);
        _menuRe_.SetActive(false);

        //_ultradeath_on_start.SetActive(true);

        //_level_generator.StartWave();
        _wave_menu.SetActive(true);
        _wave_menu.GetComponent<WaveMenu>().StartWaves();

    }

    public void ResumeGame()
    {
        _menu_.SetActive(false);
        _menuRe_.SetActive(false);

        //_level_generator.RechargeGame();

    }

    public void ExitGame()
    {
        _menu_.SetActive(false);
        _menuRe_.SetActive(false);

        Application.Quit();

    }

}