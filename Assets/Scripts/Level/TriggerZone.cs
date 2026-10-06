using System;
using UnityEngine;

/// <summary>
/// Trigger zone that initiates wave progression when the player enters it.
/// Placed in the scene to defer combat wave countdowns until the player flies into the sector.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TriggerZone : MonoBehaviour
{
    private static TriggerZone _instance;
    public static TriggerZone Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<TriggerZone>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Trigger State")]
    [Tooltip("Whether this trigger zone has already been activated during the current run.")]
    [SerializeField] private bool hasTriggered = false;

    [Header("Wave Integration")]
    [Tooltip("Optional explicit reference to WaveManager. Auto-resolves if null.")]
    [SerializeField] private WaveManager waveManager;

    [Header("Visual Feedback (Optional)")]
    [Tooltip("Optional GameObject or marker to deactivate or toggle when triggered.")]
    [SerializeField] private GameObject visualIndicator;

    public bool HasTriggered => hasTriggered;
    public WaveManager ActiveWaveManager => waveManager != null ? waveManager : WaveManager.Instance;

    public event Action OnPlayerEntered;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }

        EnsureTriggerCollider();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// Ensures that any attached collider is marked as a trigger.
    /// </summary>
    public void EnsureTriggerCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other) => HandleTriggerEnter(other);

    /// <summary>
    /// Processes collision trigger entries, initiating waves when the player ship enters.
    /// </summary>
    public void HandleTriggerEnter(Collider other)
    {
        if (other == null) return;

        // Ignore sensor/view boundary triggers like max_view_radius
        if (other.isTrigger) return;

        // Check if the collider belongs to the player ship
        player p = other.GetComponentInParent<player>();
        if (p == null && !other.CompareTag("Player"))
        {
            return;
        }

        TriggerWaveStart();
    }

    /// <summary>
    /// Starts wave execution if not already triggered and WaveManager is idle.
    /// </summary>
    public void TriggerWaveStart()
    {
        if (hasTriggered) return;

        hasTriggered = true;

        if (visualIndicator != null)
        {
            visualIndicator.SetActive(false);
        }

        WaveManager wm = ActiveWaveManager;
        if (wm != null)
        {
            // Only start if not already mid-wave or countdown
            if (wm.State == WaveManager.WaveState.Idle || wm.State == WaveManager.WaveState.GameOver)
            {
                wm.StartRun();
            }
        }

        OnPlayerEntered?.Invoke();
    }

    /// <summary>
    /// Resets the trigger zone so it can be re-entered on subsequent runs or after player respawn.
    /// </summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
        if (visualIndicator != null)
        {
            visualIndicator.SetActive(true);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = hasTriggered
            ? new Color(0.5f, 0.5f, 0.5f, 0.25f)
            : new Color(1f, 0.8f, 0.1f, 0.35f);

        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = hasTriggered ? Color.gray : Color.yellow;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = hasTriggered ? Color.gray : Color.yellow;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        else
        {
            Gizmos.DrawCube(transform.position, Vector3.one * 2f);
        }
    }
}
