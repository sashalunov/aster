using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening; // Using DOTween already in your project

public class WaveClearedPopup : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text statsText;
    public CanvasGroup canvasGroup;
    public Button startButton;

    private void Awake()
    {
        ResolveStartButton();
    }

    private void Start()
    {
        ResolveStartButton();

        if (panel != null) panel.SetActive(false);

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveCompleted += ShowPopup;
            WaveManager.Instance.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveCompleted -= ShowPopup;
            WaveManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartButtonClicked);
        }
    }

    private void ResolveStartButton()
    {
        if (startButton == null && panel != null)
        {
            startButton = panel.GetComponentInChildren<Button>(true);
        }
        else if (startButton == null)
        {
            startButton = GetComponentInChildren<Button>(true);
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartButtonClicked);
            startButton.onClick.AddListener(HandleStartButtonClicked);
        }
    }

    private void HandleStartButtonClicked()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.StartNextWave();
        }
    }

    private void ShowPopup(int waveNumber, WaveDefinition config)
    {
        if (panel == null) return;

        ResolveStartButton();

        // 1. Populate Text
        if (titleText != null)
        {
            titleText.SetText($"WAVE {waveNumber} CLEARED!");
        }

        if (statsText != null)
        {
            //string gunReward = config != null && config.grantExtraGun ? "\n+ NEW TURRET UNLOCKED!" : "";
            int xpReward = config != null ? config.rewardXP : 0;
            int credReward = config != null ? config.rewardCredits : 0;

            player p = FindAnyObjectByType<player>();
            string shipStats = p != null 
                ? $"\n\nPower: {p._bullet_force} | Rate: {p._fire_hz:0.#}Hz | Dmg: {p._bullet_dmg}" 
                : "";

            statsText.SetText($"+{xpReward} XP   +{credReward} Credits{shipStats}");
        }

        // 2. Open Panel with Fade/Punch Animation
        panel.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.25f);
        }
        //panel.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f);
    }
   
        private void HandleStateChanged(WaveManager.WaveState prevState, WaveManager.WaveState newState)
        {
            // Automatically close when exiting Intermission
            if (prevState == WaveManager.WaveState.Intermission && newState != WaveManager.WaveState.Intermission)
            {
                ClosePopup();
            }
        }
   
        private void ClosePopup()
        {
            if (panel == null || !panel.activeSelf) return;
   
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, 0.2f).OnComplete(() => panel.SetActive(false));
           }
            else
            {
                panel.SetActive(false);
            }
       }
}
