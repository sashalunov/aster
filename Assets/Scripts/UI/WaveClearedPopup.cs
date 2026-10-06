using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

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
        // If WaveIntermissionPopup exists in the scene and handles intermission, suppress this duplicate cleared popup
        if (FindFirstObjectByType<WaveIntermissionPopup>() != null) return;

        if (panel == null) return;

        ResolveStartButton();

        // 1. Populate Text
        if (titleText != null)
        {
            titleText.SetText($"WAVE {waveNumber} CLEARED!");
        }

        if (statsText != null)
        {
            int xpReward = config != null ? config.rewardXP : 0;
           

            player p = FindAnyObjectByType<player>();
            string shipStats = p != null 
                ? $"\n\nPower: {p._bullet_force} | Rate: {p._fire_hz:0.#}Hz | Dmg: {p._bullet_dmg}" 
                : "";

            statsText.SetText($"+{xpReward} XP   Credits{shipStats}");
        }

        // 2. Open Panel with Fade Animation
        panel.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.25f).SetUpdate(true);
        }
    }

    private void HandleStateChanged(WaveManager.WaveState prevState, WaveManager.WaveState newState)
    {
        // Automatically close when exiting Intermission or when transitioning into Intermission
        if (newState == WaveManager.WaveState.Intermission || (prevState == WaveManager.WaveState.Intermission && newState != WaveManager.WaveState.Intermission))
        {
            ClosePopup();
        }
    }

    private void ClosePopup()
    {
        if (panel == null || !panel.activeSelf) return;

        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
            {
                if (panel != null) panel.SetActive(false);
            });
        }
        else
        {
            panel.SetActive(false);
        }
    }
}
