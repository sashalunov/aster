using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Target References")]
    [SerializeField] private player _player;
    [SerializeField] private PlayerProgression _progression;

    [Header("Progression UI")]
    public TMP_Text playerNameText;
    public TMP_Text xpText;
    public TMP_Text xpNextText;
    public TMP_Text waveText;
    public TMP_Text timerText;

    [Header("Weapon UI")]
    public TMP_Text wpnPowerText;
    public TMP_Text wpnRateText;
    public TMP_Text wpnDamageText;

    [Header("Shield UI")]
    public TMP_Text shieldText;
    public Image shieldProgress;

    [Header("Health UI")]
    public TMP_Text healthText;
    public Image healthProgress;

    private bool _isSubscribed = false;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        Subscribe();
        RefreshAllDisplays();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        RefreshAllDisplays();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!_isSubscribed)
        {
            ResolveReferences();
            if (_player != null)
            {
                Subscribe();
                RefreshAllDisplays();
            }
        }
    }

    public void ResolveReferences()
    {
        if (_player == null)
        {
            _player = FindAnyObjectByType<player>();
        }
        if (_player != null)
        {
            PlayerProgression activeProg = _player.Progression ?? _player.GetComponent<PlayerProgression>();
            if (_progression != activeProg)
            {
                if (_isSubscribed && _progression != null)
                {
                    _progression.OnXPChanged -= UpdateXPDisplay;
                    _progression.OnWaveCompleted -= UpdateWaveDisplay;
                }
                _progression = activeProg;
                if (_isSubscribed && _progression != null)
                {
                    _progression.OnXPChanged += UpdateXPDisplay;
                    _progression.OnWaveCompleted += UpdateWaveDisplay;
                }
            }
        }
    }

    public void Subscribe()
    {
        if (_isSubscribed) return;

        PlayerProfile.OnPlayerNameChanged += UpdatePlayerNameDisplay;

        if (_progression != null)
        {
            _progression.OnXPChanged += UpdateXPDisplay;
            _progression.OnWaveCompleted += UpdateWaveDisplay;
        }
        if (_player != null)
        {
            _player.OnWeaponStatsChanged += UpdateWeaponDisplay;
            _player.OnShieldChanged += UpdateShieldDisplay;
            _player.OnHealthChanged += UpdateHealthDisplay;
            _isSubscribed = true;
        }
    }

    public void Unsubscribe()
    {
        if (!_isSubscribed) return;

        PlayerProfile.OnPlayerNameChanged -= UpdatePlayerNameDisplay;

        if (_progression != null)
        {
            _progression.OnXPChanged -= UpdateXPDisplay;
            _progression.OnWaveCompleted -= UpdateWaveDisplay;
        }
        if (_player != null)
        {
            _player.OnWeaponStatsChanged -= UpdateWeaponDisplay;
            _player.OnShieldChanged -= UpdateShieldDisplay;
            _player.OnHealthChanged -= UpdateHealthDisplay;
        }
        _isSubscribed = false;
    }

    public void RefreshAllDisplays()
    {
        UpdatePlayerNameDisplay(PlayerProfile.PlayerName);

        if (_progression != null)
        {
            UpdateXPDisplay(_progression.CurrentXP, _progression.GetNextXPGoal());
            UpdateWaveDisplay(_progression.WaveLevel);
        }
        if (_player != null)
        {
            UpdateWeaponDisplay(_player._bullet_force, _player._fire_rate, _player._bullet_dmg);
            UpdateShieldDisplay(_player.shield_value, _player.shield_max_value);
            UpdateHealthDisplay(_player.health_value, _player.health_max_value);
        }
    }

    public void UpdatePlayerNameDisplay(string name)
    {
        if (playerNameText != null)
        {
            playerNameText.SetText( name);
        }
    }

    public void UpdateXPDisplay(ulong currentXP, ulong nextGoalXP)
    {
        if (xpText != null) xpText.SetText("XP: " + currentXP);
        if (xpNextText != null) xpNextText.SetText("Next: " + nextGoalXP);
        if (waveText != null && _progression != null) waveText.SetText("Wave: " + _progression.WaveLevel);
    }

    public void UpdateWaveDisplay(int wave)
    {
        if (waveText != null) waveText.SetText("Wave: " + wave);
    }

    public void UpdateWeaponDisplay(float power, float rate, float damage)
    {
        if (wpnPowerText != null) wpnPowerText.SetText("Power: " + power.ToString());
        if (wpnRateText != null) wpnRateText.SetText("Rate: " + (rate > 0 ? (1f / rate).ToString("0.#") + "Hz" : "0Hz"));
        if (wpnDamageText != null) wpnDamageText.SetText("Damage: " + damage.ToString());
    }

    public void UpdateShieldDisplay(float currentShield, float maxShield)
    {
        if (shieldText != null)
        {
            shieldText.SetText("SHIELD: " + Mathf.Max(0, (int)currentShield) + " / " + (int)maxShield);
        }
        if (shieldProgress != null && maxShield > 0)
        {
            shieldProgress.fillAmount = Mathf.Clamp01(currentShield / maxShield);
        }
    }

    public void UpdateHealthDisplay(float currentHealth, float maxHealth)
    {
        if (healthText != null)
        {
            healthText.SetText("HULL: " + Mathf.Max(0, (int)currentHealth) + " / " + (int)maxHealth);
        }
        if (healthProgress != null && maxHealth > 0)
        {
            float fill = Mathf.Clamp01(currentHealth / maxHealth);
            healthProgress.fillAmount = fill;

            // Dynamically tint color based on health level: green -> yellow -> red
            if (fill > 0.5f)
            {
                healthProgress.color = Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(0.2f, 0.9f, 0.3f), (fill - 0.5f) * 2f);
            }
            else
            {
                healthProgress.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.8f, 0.2f), fill * 2f);
            }
        }
    }
}
