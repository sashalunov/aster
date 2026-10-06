using UnityEngine;

/// <summary>
/// Defines the respawn location and orientation for the player ship.
/// Handles resetting player transform, physics, survivability stats, and triggering systems on deploy.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    private static SpawnPoint _instance;
    public static SpawnPoint Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<SpawnPoint>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Spawn Configuration")]
    [Tooltip("Target transform used for spawn position and rotation. Defaults to this transform.")]
    [SerializeField] private Transform spawnTransform;

    [Header("Trigger Zone Link")]
    [Tooltip("Optional TriggerZone that starts waves when player enters it. Auto-resolved if null.")]
    [SerializeField] private TriggerZone triggerZone;

    public Transform SpawnTransform => spawnTransform != null ? spawnTransform : transform;
    public Vector3 Position => SpawnTransform.position;
    public Quaternion Rotation => SpawnTransform.rotation;
    public TriggerZone LinkedTriggerZone
    {
        get => triggerZone;
        set => triggerZone = value;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        if (spawnTransform == null)
        {
            spawnTransform = transform;
        }
        ResolveTriggerZone();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void ResolveTriggerZone()
    {
        if (triggerZone == null)
        {
            triggerZone = TriggerZone.Instance != null ? TriggerZone.Instance : FindAnyObjectByType<TriggerZone>();
        }
    }

    /// <summary>
    /// Respawns the player ship at this spawn point, restoring health/shields, physics, and gameplay capability.
    /// Resets the wave trigger zone so entering it will initiate waves.
    /// </summary>
    public void RespawnPlayer(player targetPlayer = null)
    {
        if (targetPlayer == null)
        {
            targetPlayer = FindAnyObjectByType<player>();
        }

        if (targetPlayer != null)
        {
            targetPlayer.Respawn(Position, Rotation);
        }

        ResolveTriggerZone();
        if (triggerZone != null)
        {
            triggerZone.ResetTrigger();
        }

        // If WaveManager was left in GameOver, reset back to Idle
        if (WaveManager.Instance != null && WaveManager.Instance.State == WaveManager.WaveState.GameOver)
        {
            WaveManager.Instance.SetState(WaveManager.WaveState.Idle);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.5f);
        Vector3 pos = spawnTransform != null ? spawnTransform.position : transform.position;
        Gizmos.DrawSphere(pos, 0.8f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pos, 1.2f);
        Gizmos.DrawRay(pos, (spawnTransform != null ? spawnTransform.up : transform.up) * 2f);
    }
}
