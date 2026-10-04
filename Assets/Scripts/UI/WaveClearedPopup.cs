using UnityEngine;
using TMPro;
using DG.Tweening; // Using DOTween already in your project

public class WaveClearedPopup : MonoBehaviour
{
    [Header("UI References")]
         public GameObject panel;
         public TMP_Text titleText;
        public TMP_Text statsText;
        public CanvasGroup canvasGroup;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
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
        }
   
        private void ShowPopup(int waveNumber, WaveDefinition config)
        {
            if (panel == null) return;
   
            // 1. Populate Text
            if (titleText != null)
            {
                titleText.SetText($"WAVE {waveNumber} CLEARED!");
            }
   
            if (statsText != null)
            {
                string gunReward = config != null && config.grantExtraGun ? "\n+ NEW TURRET UNLOCKED!" : "";
                int xpReward = config != null ? config.rewardXP : 0;
                int credReward = config != null ? config.rewardCredits : 0;
   
                player p = FindAnyObjectByType<player>();
                string shipStats = p != null 
                    ? $"\n\nPower: {p._bullet_force} | Rate: {p._fire_hz:0.#}Hz | Dmg: {p._bullet_dmg}" 
                    : "";
   
                statsText.SetText($"+{xpReward} XP   +{credReward} Credits{gunReward}{shipStats}");
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
