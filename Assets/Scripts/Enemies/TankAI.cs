using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls autonomous AI movement for tanks on the XY plane.
/// Uses the tank's local Y axis (transform.up) as the forward direction,
/// matching TurretAI conventions. Steering rotates purely around the Z axis.
/// </summary>
public class TankAI : MonoBehaviour
{
    public enum TankState
    {
        Idle,       // Waiting / resting in place
        Moving,     // Driving towards target destination
        Avoiding,   // Actively steering to evade an approaching tank or obstacle
        Reversing   // Backing up briefly to resolve deadlocks or close encounters
    }

    [Header("State")]
    [Tooltip("Current operational state of the tank AI")]
    [SerializeField] private TankState currentState = TankState.Idle;

    [Header("Movement Settings")]
    [Tooltip("Forward movement speed in units per second (moves along local Y axis)")]
    public float moveSpeed = 2.5f;

    [Tooltip("Reverse movement speed when backing up")]
    public float reverseSpeed = 1.2f;

    [Tooltip("Steering rotation speed in degrees per second around Z axis")]
    public float rotationSpeed = 120f;

    [Tooltip("Distance threshold to consider destination reached")]
    public float arrivalDistance = 0.8f;

    [Tooltip("Acceleration rate to smoothly ramp speed")]
    public float acceleration = 4.0f;

    [Header("Timers & Durations")]
    [Tooltip("Minimum idle / wait duration in seconds")]
    public float idleDurationMin = 0.6f;

    [Tooltip("Maximum idle / wait duration in seconds")]
    public float idleDurationMax = 1.8f;

    [Tooltip("Minimum movement duration before picking a new action")]
    public float moveDurationMin = 4.0f;

    [Tooltip("Maximum movement duration before picking a new action")]
    public float moveDurationMax = 8.0f;

    [Tooltip("Duration to back up in seconds when reversing")]
    public float reverseDuration = 1.2f;

    [Header("Movement Area / Patrol")]
    [Tooltip("If true, patrol within a radius around initial spawn position. If false, patrol within arena bounds.")]
    public bool roamAroundSpawn = true;

    [Tooltip("Radius around spawn position to wander within (when roamAroundSpawn is true)")]
    public float roamRadius = 10f;

    [Tooltip("World area boundaries to constrain movement within (when roamAroundSpawn is false)")]
    public Rect arenaBounds = new Rect(-20f, -20f, 40f, 40f);

    [Header("Mutual Tank Avoidance")]
    [Tooltip("Actively detect and steer away from other AI tanks to prevent collisions")]
    public bool avoidOtherTanks = true;

    [Tooltip("Distance at which avoidance steering begins when approaching another tank")]
    public float tankAvoidanceRadius = 4.0f;

    [Tooltip("Critical distance at which tank halts immediately to guarantee no collision")]
    public float emergencyStopDistance = 1.8f;

    [Tooltip("Strength of the repulsion force away from other tanks")]
    public float tankSeparationWeight = 3.0f;

    [Header("Obstacle Avoidance")]
    [Tooltip("Cast sensor rays to detect and steer around static obstacles/walls")]
    public bool avoidObstacles = true;

    [Tooltip("Forward distance to check for physical obstacles")]
    public float obstacleCheckDistance = 2.5f;

    [Tooltip("Angle of left and right whisker sensor rays in degrees")]
    public float whiskerAngle = 30f;

    [Tooltip("Layer mask representing obstacles to avoid")]
    public LayerMask obstacleMask = ~0;

    [Header("Physics & Components")]
    [Tooltip("Rigidbody component (auto-configured if unassigned)")]
    public Rigidbody rb;

    [Tooltip("Collider component (auto-configured if unassigned)")]
    public Collider col;

    [Tooltip("Automatically add and configure Collider/Rigidbody if missing")]
    public bool autoAddPhysicsComponents = true;

    [Header("Gizmos")]
    [Tooltip("Draw debug visualizer gizmos in the Scene view")]
    public bool showGizmos = true;

    // Static registry of all active TankAI instances for mutual avoidance
    private static readonly List<TankAI> allTanks = new List<TankAI>();

    // Spawn anchor configuration
    [Tooltip("Initial spawn position of the tank. Automatically recorded on Awake or can be set in Inspector.")]
    [SerializeField] private Vector3 spawnPosition;
    [SerializeField] private bool hasRecordedSpawn = false;

    // Runtime state
    private Vector3 currentDestination;
    private float stateTimer = 0f;
    private float currentSpeed = 0f;
    private Vector3 currentMoveDirection = Vector3.up;
    private float groundZ = 0f;
    private Collider[] myColliders;

    // Stuck detector state
    private Vector3 lastStuckCheckPosition;
    private float stuckCheckTimer = 0f;

    /// <summary>
    /// The tank's effective forward driving direction on the XY plane (local Y axis, matching TurretAI).
    /// </summary>
    public Vector3 TankForward => transform.up;

    /// <summary>
    /// The tank's effective right direction on the XY plane (local X axis).
    /// </summary>
    public Vector3 TankRight => transform.right;

    public TankState CurrentState => currentState;
    public Vector3 CurrentDestination => currentDestination;
    public Vector3 SpawnPosition => spawnPosition;
    public static IReadOnlyList<TankAI> AllTanks => allTanks;

    private void OnValidate()
    {
        if (arrivalDistance > 2f || arrivalDistance < 0.1f)
        {
            arrivalDistance = 0.8f;
        }
        if (idleDurationMax < idleDurationMin) idleDurationMax = idleDurationMin + 1f;
        if (moveDurationMax < moveDurationMin) moveDurationMax = moveDurationMin + 1f;
    }

    private void Awake()
    {
        if (!hasRecordedSpawn || spawnPosition == Vector3.zero)
        {
            spawnPosition = transform.position;
            hasRecordedSpawn = true;
        }
        groundZ = spawnPosition.z;
        Initialize(spawnPosition);
    }

    private void Reset()
    {
        spawnPosition = transform.position;
        groundZ = spawnPosition.z;
        hasRecordedSpawn = true;
        AutoConfigureReferences();
    }

    private void OnEnable()
    {
        RegisterTank(this);
    }

    private void OnDisable()
    {
        UnregisterTank(this);
    }

    /// <summary>
    /// Initializes spawn parameters and registers this tank in the mutual avoidance registry.
    /// </summary>
    public void Initialize(Vector3? customSpawn = null)
    {
        if (customSpawn.HasValue)
        {
            spawnPosition = customSpawn.Value;
            hasRecordedSpawn = true;
        }
        else if (!hasRecordedSpawn || spawnPosition == Vector3.zero)
        {
            spawnPosition = transform.position;
            hasRecordedSpawn = true;
        }
        groundZ = spawnPosition.z;
        AutoConfigureReferences();
        RegisterTank(this);
    }

    public static void RegisterTank(TankAI tank)
    {
        if (tank != null && !allTanks.Contains(tank))
        {
            allTanks.Add(tank);
        }
    }

    public static void UnregisterTank(TankAI tank)
    {
        if (tank != null)
        {
            allTanks.Remove(tank);
        }
    }

    private void Start()
    {
        if (!hasRecordedSpawn || spawnPosition == Vector3.zero)
        {
            spawnPosition = transform.position;
            groundZ = spawnPosition.z;
            hasRecordedSpawn = true;
        }

        // Cache initial forward on the XY plane (local Y axis is tank forward)
        Vector3 initialForward = transform.up;
        initialForward.z = 0f;
        if (initialForward.sqrMagnitude > 0.001f)
        {
            currentMoveDirection = initialForward.normalized;
        }
        else
        {
            currentMoveDirection = Vector3.up;
        }

        // Start driving right away so the user immediately sees the tank move
        EnterMovingState();
    }

    /// <summary>
    /// Ensures proper BoxCollider and Rigidbody exist with XY plane constraints.
    /// </summary>
    public void AutoConfigureReferences()
    {
        if (arrivalDistance > 2f || arrivalDistance < 0.1f)
        {
            arrivalDistance = 0.8f;
        }

        // Remove duplicate BoxColliders on this root object if any exist
        BoxCollider[] rootBoxes = GetComponents<BoxCollider>();
        if (rootBoxes.Length > 1)
        {
            for (int i = 1; i < rootBoxes.Length; i++)
            {
                if (Application.isPlaying) Destroy(rootBoxes[i]);
                else DestroyImmediate(rootBoxes[i]);
            }
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (col == null)
        {
            col = GetComponent<Collider>();
        }

        myColliders = GetComponentsInChildren<Collider>();

        if (autoAddPhysicsComponents)
        {
            if (col == null && (myColliders == null || myColliders.Length == 0))
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                // In Y-forward convention: X is width (0.7), Y is length (0.9), Z is depth (0.6)
                box.center = new Vector3(0f, 0f, 0f);
                box.size = new Vector3(0.7f, 0.9f, 0.6f);
                col = box;
                myColliders = new Collider[] { box };
            }

            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true; // Kinematic prevents static floor collider friction from stalling movement
                rb.constraints = RigidbodyConstraints.FreezePositionZ |
                                 RigidbodyConstraints.FreezeRotationX |
                                 RigidbodyConstraints.FreezeRotationY;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        // Ignore collision with background ground quads
        GameObject groundObj = GameObject.Find("ground");
        if (groundObj != null)
        {
            Collider[] groundCols = groundObj.GetComponentsInChildren<Collider>();
            if (groundCols != null && myColliders != null)
            {
                for (int i = 0; i < groundCols.Length; i++)
                {
                    for (int j = 0; j < myColliders.Length; j++)
                    {
                        if (groundCols[i] != null && myColliders[j] != null)
                        {
                            Physics.IgnoreCollision(myColliders[j], groundCols[i]);
                        }
                    }
                }
            }
        }
    }

    private void Update()
    {
        stateTimer -= Time.deltaTime;

        switch (currentState)
        {
            case TankState.Idle:
                HandleIdleState();
                break;

            case TankState.Moving:
                HandleMovingState();
                break;

            case TankState.Avoiding:
                HandleAvoidingState();
                break;

            case TankState.Reversing:
                HandleReversingState();
                break;
        }

        // Clamp Z position to keep tank firmly on ground plane
        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.z - groundZ) > 0.001f)
        {
            transform.position = new Vector3(pos.x, pos.y, groundZ);
            if (rb != null && !rb.isKinematic)
            {
                Vector3 vel = rb.linearVelocity;
                vel.z = 0f;
                rb.linearVelocity = vel;
            }
        }
    }

    // ==========================================
    // State Handlers
    // ==========================================

    private void HandleIdleState()
    {
        // Decelerate to zero
        currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * Time.deltaTime);

        // Check if an approaching tank requires us to yield / reverse
        if (avoidOtherTanks)
        {
            bool shouldStop, shouldReverse;
            ComputeTankAvoidanceVector(out shouldStop, out shouldReverse);
            if (shouldReverse)
            {
                EnterReversingState();
                return;
            }
        }

        // When idle timer expires, pick a new destination and move
        if (stateTimer <= 0f)
        {
            EnterMovingState();
        }
    }

    private void HandleMovingState()
    {
        Vector3 myPos = transform.position;
        myPos.z = 0f;

        Vector3 toDest = currentDestination - myPos;
        toDest.z = 0f;
        float distanceToDest = toDest.magnitude;

        // Check arrival or timer expiration
        if (distanceToDest <= arrivalDistance || stateTimer <= 0f)
        {
            EnterIdleState(Random.Range(idleDurationMin, idleDurationMax));
            return;
        }

        // Stuck detection: verify the tank is actually making physical progress
        stuckCheckTimer += Time.deltaTime;
        if (stuckCheckTimer >= 1.2f)
        {
            stuckCheckTimer = 0f;
            float progress = Vector3.Distance(transform.position, lastStuckCheckPosition);
            lastStuckCheckPosition = transform.position;

            // If the tank wanted to move but made almost no progress, it is stuck against something
            if (currentSpeed > 0.4f && progress < 0.15f)
            {
                EnterReversingState();
                return;
            }
        }

        // Base desired direction towards target
        Vector3 desiredDirection = toDest.normalized;

        // Check mutual tank avoidance
        bool tankEmergencyStop = false;
        bool tankShouldReverse = false;
        Vector3 tankAvoidance = Vector3.zero;

        if (avoidOtherTanks)
        {
            tankAvoidance = ComputeTankAvoidanceVector(out tankEmergencyStop, out tankShouldReverse);
            if (tankShouldReverse)
            {
                EnterReversingState();
                return;
            }
        }

        // Check obstacle avoidance
        bool obstacleBlocked = false;
        Vector3 obstacleAvoidance = Vector3.zero;
        if (avoidObstacles)
        {
            obstacleAvoidance = ComputeObstacleAvoidanceVector(out obstacleBlocked);
            if (obstacleBlocked)
            {
                EnterReversingState();
                return;
            }
        }

        // Boundary repulsion (prevent driving outside spawn patrol radius)
        Vector3 boundaryAvoidance = ComputeBoundaryAvoidanceVector();

        // Blend all steer vectors
        Vector3 finalSteerDirection = desiredDirection + tankAvoidance + obstacleAvoidance + boundaryAvoidance;
        finalSteerDirection.z = 0f;

        if (finalSteerDirection.sqrMagnitude > 0.001f)
        {
            currentMoveDirection = finalSteerDirection.normalized;
        }

        // Steer towards target direction
        ApplySteering(currentMoveDirection);

        // Calculate target speed
        float targetSpeed = moveSpeed;
        if (tankEmergencyStop)
        {
            targetSpeed = 0f;
            currentState = TankState.Avoiding;
        }
        else
        {
            // If facing away from target heading, prioritize turning in place before rushing forward
            float angleToDesired = Vector3.Angle(transform.up, currentMoveDirection);
            if (angleToDesired > 80f)
            {
                targetSpeed = 0f; // Turn on the spot!
            }
            else if (angleToDesired > 35f)
            {
                targetSpeed *= 0.35f; // Creep while turning
            }
            else if (angleToDesired > 15f)
            {
                targetSpeed *= 0.75f;
            }
        }

        // Accelerate / decelerate
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        // Apply movement forward along the local Y axis (transform.up)
        ApplyMovement(transform.up * currentSpeed);
    }

    private void HandleAvoidingState()
    {
        // Actively steering to resolve proximity
        bool tankEmergencyStop, tankShouldReverse;
        Vector3 tankAvoidance = ComputeTankAvoidanceVector(out tankEmergencyStop, out tankShouldReverse);

        if (tankShouldReverse)
        {
            EnterReversingState();
            return;
        }

        if (!tankEmergencyStop && tankAvoidance.sqrMagnitude < 0.01f)
        {
            // Hazard resolved, resume moving
            currentState = TankState.Moving;
            return;
        }

        // Steer away from hazard
        if (tankAvoidance.sqrMagnitude > 0.001f)
        {
            currentMoveDirection = tankAvoidance.normalized;
            ApplySteering(currentMoveDirection);
        }

        // If stopped too long in avoiding state, back up
        if (stateTimer <= 0f)
        {
            EnterReversingState();
        }
    }

    private void HandleReversingState()
    {
        // Drive backwards along negative Y axis (-transform.up)
        currentSpeed = Mathf.MoveTowards(currentSpeed, reverseSpeed, acceleration * Time.deltaTime);
        ApplyMovement(-transform.up * currentSpeed);

        if (stateTimer <= 0f)
        {
            // Done reversing, pick a fresh destination away from the deadlock
            EnterMovingState();
        }
    }

    // ==========================================
    // State Transitions
    // ==========================================

    private void EnterIdleState(float duration)
    {
        currentState = TankState.Idle;
        stateTimer = duration;
    }

    private void EnterMovingState()
    {
        currentState = TankState.Moving;
        stateTimer = Random.Range(moveDurationMin, moveDurationMax);
        currentDestination = GetRandomDestination();
        lastStuckCheckPosition = transform.position;
        stuckCheckTimer = 0f;
    }

    private void EnterReversingState()
    {
        currentState = TankState.Reversing;
        stateTimer = reverseDuration;
    }

    // ==========================================
    // Steering & Movement Execution
    // ==========================================

    /// <summary>
    /// Calculates the 2D rotation for the tank chassis so its local Y axis (transform.up)
    /// aims along steerDirection, rotating around the world Z axis.
    /// Exactly mirrors TurretAI 2D aim calculation.
    /// </summary>
    public static Quaternion CalculateChassisRotation(Vector3 steerDirection)
    {
        float targetAngle = Mathf.Atan2(steerDirection.y, steerDirection.x) * Mathf.Rad2Deg - 90f;
        return Quaternion.Euler(0f, 0f, targetAngle);
    }

    /// <summary>
    /// Steers the tank on the XY plane by rotating around the Z axis so transform.up aligns with steerDirection.
    /// Synchronizes rotation with Rigidbody so PhysX does not overwrite the rotation back to (0,0,0).
    /// </summary>
    private void ApplySteering(Vector3 steerDirection)
    {
        steerDirection.z = 0f;
        if (steerDirection.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = CalculateChassisRotation(steerDirection);
        Quaternion newRot = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        if (rb != null && rb.isKinematic)
        {
            rb.MoveRotation(newRot);
        }
        transform.rotation = newRot;
    }

    /// <summary>
    /// Applies movement along the given velocity vector.
    /// Updates transform directly and synchronizes with Rigidbody.
    /// </summary>
    private void ApplyMovement(Vector3 velocity)
    {
        velocity.z = 0f;
        Vector3 delta = velocity * Time.deltaTime;

        if (rb != null && rb.isKinematic)
        {
            rb.MovePosition(transform.position + delta);
        }
        else if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = velocity;
        }

        // Direct position update guarantees continuous, uninterrupted motion
        transform.position += delta;
    }

    // ==========================================
    // Avoidance Calculations
    // ==========================================

    /// <summary>
    /// Computes repulsion steering vector away from nearby tanks.
    /// Detects potential crashes and flags emergency stops.
    /// </summary>
    public Vector3 ComputeTankAvoidanceVector(out bool emergencyStop, out bool shouldReverse)
    {
        emergencyStop = false;
        shouldReverse = false;
        Vector3 avoidance = Vector3.zero;

        Vector3 myPos = transform.position;
        myPos.z = 0f;
        Vector3 myForward = transform.up; // Tank forward is local Y (transform.up)
        myForward.z = 0f;
        myForward.Normalize();

        for (int i = 0; i < allTanks.Count; i++)
        {
            TankAI other = allTanks[i];
            if (other == null || other == this || !other.isActiveAndEnabled) continue;

            Vector3 otherPos = other.transform.position;
            otherPos.z = 0f;

            Vector3 toOther = otherPos - myPos;
            float dist = toOther.magnitude;

            if (dist < 0.001f) continue;

            if (dist < tankAvoidanceRadius)
            {
                Vector3 toOtherDir = toOther / dist;
                float forwardDot = Vector3.Dot(myForward, toOtherDir);

                // If other tank is within emergency distance
                if (dist < emergencyStopDistance)
                {
                    emergencyStop = true;

                    // If very close and head-on, trigger reverse
                    if (dist < emergencyStopDistance * 0.75f && forwardDot > 0.2f)
                    {
                        shouldReverse = true;
                    }
                }

                // If other tank is ahead of us (in forward hemisphere)
                if (forwardDot > -0.2f)
                {
                    // Weight inversely proportional to distance
                    float weight = Mathf.Clamp01(1f - (dist / tankAvoidanceRadius)) * tankSeparationWeight;

                    // Pure repulsion away from other tank
                    Vector3 pushAway = -toOtherDir;

                    // Determine side using 2D cross product:
                    // In our orientation (Z-axis out), cross.z < 0 is to the right, > 0 is to the left
                    float crossZ = Vector3.Cross(myForward, toOtherDir).z;
                    Vector3 steerSide;

                    if (crossZ < -0.05f)
                    {
                        // Other tank is to our right -> steer left (-transform.right)
                        steerSide = -transform.right;
                    }
                    else if (crossZ > 0.05f)
                    {
                        // Other tank is to our left -> steer right (+transform.right)
                        steerSide = transform.right;
                    }
                    else
                    {
                        // Head-on dead center: use GetHashCode() to break symmetry deterministically
                        steerSide = (GetHashCode() < other.GetHashCode()) ? transform.right : -transform.right;
                    }
                    steerSide.z = 0f;

                    avoidance += (pushAway + steerSide.normalized).normalized * weight;
                }
            }
        }

        return avoidance;
    }

    /// <summary>
    /// Uses whisker raycasts to detect physical obstacles ahead and steer away.
    /// </summary>
    public Vector3 ComputeObstacleAvoidanceVector(out bool blocked)
    {
        blocked = false;
        Vector3 avoidance = Vector3.zero;

        Vector3 origin = transform.position;
        Vector3 forward = transform.up; // Tank forward is local Y
        forward.z = 0f;
        forward.Normalize();

        Vector3 leftWhisker = Quaternion.Euler(0f, 0f, whiskerAngle) * forward;
        Vector3 rightWhisker = Quaternion.Euler(0f, 0f, -whiskerAngle) * forward;

        bool centerHit = RaycastFiltered(origin, forward, obstacleCheckDistance, out RaycastHit hitC);
        bool leftHit = RaycastFiltered(origin, leftWhisker, obstacleCheckDistance * 0.85f, out RaycastHit hitL);
        bool rightHit = RaycastFiltered(origin, rightWhisker, obstacleCheckDistance * 0.85f, out RaycastHit hitR);

        if (centerHit && hitC.distance < emergencyStopDistance * 0.8f)
        {
            blocked = true;
        }

        if (centerHit)
        {
            // If center hit, steer away from normal or towards clearer side
            avoidance += (rightHit && !leftHit) ? -transform.right : transform.right;
            avoidance += (Vector3)hitC.normal;
        }
        else if (leftHit)
        {
            avoidance += transform.right * 1.5f;
        }
        else if (rightHit)
        {
            avoidance += -transform.right * 1.5f;
        }

        avoidance.z = 0f;
        return avoidance;
    }

    /// <summary>
    /// Steers away from the boundary of the allowed movement area.
    /// </summary>
    private Vector3 ComputeBoundaryAvoidanceVector()
    {
        Vector3 avoidance = Vector3.zero;
        Vector3 pos = transform.position;

        if (roamAroundSpawn)
        {
            Vector3 anchor = (hasRecordedSpawn && spawnPosition != Vector3.zero) ? spawnPosition : transform.position;
            Vector3 fromSpawn = pos - anchor;
            fromSpawn.z = 0f;
            float dist = fromSpawn.magnitude;
            if (dist > roamRadius * 0.75f)
            {
                float excess = dist - (roamRadius * 0.75f);
                avoidance = (-fromSpawn.normalized) * (excess / (roamRadius * 0.25f)) * 3f;
            }
        }
        else
        {
            float margin = 3f;
            if (pos.x < arenaBounds.xMin + margin) avoidance.x += (arenaBounds.xMin + margin - pos.x);
            if (pos.x > arenaBounds.xMax - margin) avoidance.x -= (pos.x - (arenaBounds.xMax - margin));
            if (pos.y < arenaBounds.yMin + margin) avoidance.y += (arenaBounds.yMin + margin - pos.y);
            if (pos.y > arenaBounds.yMax - margin) avoidance.y -= (pos.y - (arenaBounds.yMax - margin));
        }

        avoidance.z = 0f;
        return avoidance;
    }

    /// <summary>
    /// Performs a raycast ignoring the tank's own colliders, other tanks, ground quads, and triggers.
    /// </summary>
    private bool RaycastFiltered(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
    {
        hit = default;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, obstacleMask, QueryTriggerInteraction.Ignore);

        float closestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null) continue;

            // Ignore self
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;

            // Ignore other tanks (handled by mutual avoidance)
            if (c.GetComponentInParent<TankAI>() != null) continue;

            // Ignore background ground quads
            if (c.name.StartsWith("gnd_") || c.name.Contains("ground")) continue;

            if (hits[i].distance < closestDist)
            {
                closestDist = hits[i].distance;
                hit = hits[i];
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Picks a random destination on the XY plane within roaming bounds.
    /// Anchors tightly to the recorded spawnPosition so the tank patrols its spawn zone.
    /// </summary>
    public Vector3 GetRandomDestination()
    {
        Vector2 spawn2D = (hasRecordedSpawn && spawnPosition != Vector3.zero)
            ? new Vector2(spawnPosition.x, spawnPosition.y)
            : new Vector2(transform.position.x, transform.position.y);

        Vector2 current2D = new Vector2(transform.position.x, transform.position.y);
        Vector2 candidate = spawn2D;
        float minMoveDistance = Mathf.Max(arrivalDistance * 2.0f, 2.0f);

        for (int attempts = 0; attempts < 15; attempts++)
        {
            Vector2 randomPoint;
            if (roamAroundSpawn)
            {
                randomPoint = spawn2D + Random.insideUnitCircle * roamRadius;
            }
            else
            {
                randomPoint = new Vector2(
                    Random.Range(arenaBounds.xMin + 2f, arenaBounds.xMax - 2f),
                    Random.Range(arenaBounds.yMin + 2f, arenaBounds.yMax - 2f)
                );
            }

            if (Vector2.Distance(current2D, randomPoint) >= minMoveDistance)
            {
                candidate = randomPoint;
                break;
            }
            candidate = randomPoint;
        }

        return new Vector3(candidate.x, candidate.y, groundZ);
    }

    // ==========================================
    // Gizmos
    // ==========================================

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Vector3 pos = transform.position;

        // Draw roam radius or arena bounds
        if (roamAroundSpawn)
        {
            Vector3 center = (hasRecordedSpawn && spawnPosition != Vector3.zero) ? spawnPosition : transform.position;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, roamRadius);
        }
        else
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireCube(new Vector3(arenaBounds.center.x, arenaBounds.center.y, pos.z),
                                new Vector3(arenaBounds.size.x, arenaBounds.size.y, 0.1f));
        }

        // Draw destination line
        if (Application.isPlaying && currentState == TankState.Moving)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pos, currentDestination);
            Gizmos.DrawWireSphere(currentDestination, arrivalDistance);
        }

        // Draw avoidance zones
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(pos, tankAvoidanceRadius);

        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(pos, emergencyStopDistance);

        // Draw whisker sensor rays (tank forward is transform.up)
        Vector3 forward = transform.up;
        forward.z = 0f;
        forward.Normalize();
        Vector3 leftW = Quaternion.Euler(0f, 0f, whiskerAngle) * forward;
        Vector3 rightW = Quaternion.Euler(0f, 0f, -whiskerAngle) * forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(pos, forward * obstacleCheckDistance);
        Gizmos.DrawRay(pos, leftW * (obstacleCheckDistance * 0.85f));
        Gizmos.DrawRay(pos, rightW * (obstacleCheckDistance * 0.85f));
    }
}
