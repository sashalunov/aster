using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Target References")]
    [SerializeField] private player _player;
    [SerializeField] private PlayerProgression _progression;

    [Header("Progression UI")]
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

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
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

    public void ResolveReferences()
    {
        if (_player == null)
        {
            _player = FindAnyObjectByType<player>();
        }
        if (_progression == null && _player != null)
        {
            _progression = _player.Progression ?? _player.GetComponent<PlayerProgression>();
        }
    }

    private void Subscribe()
    {
        if (_progression != null)
        {
            _progression.OnXPChanged += UpdateXPDisplay;
            _progression.OnWaveCompleted += UpdateWaveDisplay;
        }
        if (_player != null)
        {
            _player.OnWeaponStatsChanged += UpdateWeaponDisplay;
            _player.OnShieldChanged += UpdateShieldDisplay;
        }
    }

    private void Unsubscribe()
    {
        if (_progression != null)
        {
            _progression.OnXPChanged -= UpdateXPDisplay;
            _progression.OnWaveCompleted -= UpdateWaveDisplay;
        }
        if (_player != null)
        {
            _player.OnWeaponStatsChanged -= UpdateWeaponDisplay;
            _player.OnShieldChanged -= UpdateShieldDisplay;
        }
    }

    public void RefreshAllDisplays()
    {
        if (_progression != null)
        {
            UpdateXPDisplay(_progression.CurrentXP, _progression.GetNextXPGoal());
            UpdateWaveDisplay(_progression.WaveLevel);
        }
        if (_player != null)
        {
            UpdateWeaponDisplay(_player._fire_force, _player._fire_rate, _player._bullet_dmg);
            UpdateShieldDisplay(_player.shield_value, _player.shield_max_value);
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
            shieldText.SetText(Mathf.Max(0, (int)currentShield).ToString());
            if (shieldText.transform.parent != null)
            {
                Animator anim = shieldText.transform.parent.GetComponent<Animator>();
                if (anim != null) anim.SetInteger("state", currentShield < 4 ? 1 : 0);
            }
        }
        if (shieldProgress != null && maxShield > 0)
        {
            shieldProgress.fillAmount = Mathf.Clamp01(currentShield / maxShield);
        }
    }
}
