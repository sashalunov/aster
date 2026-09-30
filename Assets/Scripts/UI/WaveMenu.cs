using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WaveMenu : MonoBehaviour
{
    public TMP_Text _countdown;
    int _delay_sec = 0;
    public bool _ready = false;
    float _time_accum = 0;
    public GameObject _start_btn;
    public player _player = null;
    public GameObject _wave_over;
    public GameObject _wave_start;

    public TMP_Text _tmp_pwr;
    public TMP_Text _tmp_rate;
    public TMP_Text _tmp_dmg;
    public TMP_Text _share_points;

    public int _free_total_points = 0;

    void Start()
    {
    }

    private void Awake()
    {
        if (_player == null) _player = FindAnyObjectByType<player>();
        if (_countdown != null && _player != null)
        {
            _countdown.SetText("Starting \nWave " + _player._wavelvl);
        }
    }

    void Update()
    {
        
    }

    void FixedUpdate()
    {
        if (_ready)
        {
            _time_accum += Time.fixedDeltaTime;
            if (_time_accum >= _delay_sec)
            {
                _time_accum = 0;
                if (_wave_start != null) _wave_start.SetActive(false);
                gameObject.SetActive(false);

                if (_player != null)
                {
                    _player._can_play = true;
                }
            }

            if (_countdown != null && _player != null)
            {
                _countdown.SetText("Starting \nWave " + _player._wavelvl + "\n in " + (_delay_sec - _time_accum).ToString("0.00") + " seconds");
            }
        }
    }

    public void StartCountdown(int seconds = 5)
    {
        if (_start_btn != null) _start_btn.SetActive(false);
        _ready = true;
        _delay_sec = seconds;
    }

    public void WaveOver()
    {
        if (_wave_start != null) _wave_start.SetActive(false);
        if (_wave_over != null) _wave_over.SetActive(true);
        _ready = false;

        if (_player != null)
        {
            if (_tmp_pwr != null) _tmp_pwr.SetText("power " + _player._fire_force);
            if (_tmp_rate != null) _tmp_rate.SetText("rate " + _player._fire_hz);
            if (_tmp_dmg != null) _tmp_dmg.SetText("damage " + _player._bullet_dmg);
        }

        CheckLeftoverPwrups();
    }

    public void StartWaves()
    {
        if (_start_btn != null) _start_btn.SetActive(true);

        if (_countdown != null && _player != null)
        {
            _countdown.SetText("Wave " + _player._wavelvl);
        }
        if (_wave_start != null) _wave_start.SetActive(true);
        _ready = false;
        if (_wave_over != null) _wave_over.SetActive(false);
    }

    public void CheckLeftoverPwrups()
    {
        var powerups = FindObjectsByType<powerup>();
        foreach (var item in powerups)
        {
            if (item._type != powerup.PowerupType.shield)
            {
                _free_total_points += 1;
            }
            else if (_player != null)
            {
                _player.shield_value += 1;
            }
            Destroy(item.gameObject);
        }
        if (_share_points != null) _share_points.SetText("review your points: " + _free_total_points);
    }

    public void NextWave()
    {
        if (_wave_start != null) _wave_start.SetActive(true);
        if (_wave_over != null) _wave_over.SetActive(false);
        StartCountdown(3);
    }

    public void AddPower(int p)
    {
        if (_player == null) return;
        if (p < 0)
        {
            if ((_player._fire_force + p) < 1) return;
        }
        else
        {
            if (_free_total_points < 1) return;
        }
        _free_total_points -= p;
        _player._fire_force += p;
        if (_tmp_pwr != null) _tmp_pwr.SetText("power " + _player._fire_force);
        if (_share_points != null) _share_points.SetText("review your points: " + _free_total_points);
        _player.UpdateWeaponHUD();
    }

    public void AddRate(int p)
    {
        if (_player == null) return;
        if (p < 0)
        {
            if ((_player._fire_hz + p) < 1) return;
        }
        else
        {
            if (_free_total_points < 1) return;
        }
        _free_total_points -= p;
        _player._fire_hz += p;
        _player._fire_rate = 1f / _player._fire_hz;

        if (_tmp_rate != null) _tmp_rate.SetText("rate " + _player._fire_hz);
        if (_share_points != null) _share_points.SetText("review your points: " + _free_total_points);
        _player.UpdateWeaponHUD();
    }

    public void AddDamage(int p)
    {
        if (_player == null) return;
        if (p < 0)
        {
            if ((_player._bullet_dmg + p) < 1) return;
        }
        else
        {
            if (_free_total_points < 1) return;
        }
        _free_total_points -= p;
        _player._bullet_dmg += p;
        if (_tmp_dmg != null) _tmp_dmg.SetText("damage " + _player._bullet_dmg);
        if (_share_points != null) _share_points.SetText("review your points: " + _free_total_points);
        _player.UpdateWeaponHUD();
    }
}
