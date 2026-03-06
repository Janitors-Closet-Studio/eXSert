// CleanserProjectile.cs
// Purpose: Boomerang/thrown weapon projectile system for the Cleanser boss.
// Works with: CleanserBrain, SpareTossConfig
// Handles straight, curved, and homing projectile paths.

using System.Collections;
using UnityEngine;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// The current phase of the projectile's flight path.
    /// </summary>
    public enum ProjectilePhase
    {
        Outbound,   // Flying toward/past target
        Returning   // Flying back to origin (boomerang)
    }

    /// <summary>
    /// Projectile behavior for the Cleanser's Spare Toss attack.
    /// Supports straight, curved boomerang, and homing paths.
    /// Treated as wing-type damage (can be guarded, cannot be parried).
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?tab=t.0#bookmark=id.m9pj5xkdlqb0")]
    public class CleanserProjectile : MonoBehaviour
    {
        [Header("Base Settings")]
        [Tooltip("Damage dealt on hit.")]
        public float Damage = 15f;
        
        [Tooltip("Layer mask for player detection.")]
        public LayerMask PlayerLayerMask;
        
        [Tooltip("Layer mask for wall collision.")]
        public LayerMask WallLayerMask;
        
        [Tooltip("Radius of the projectile collider for overlap checks.")]
        public float HitRadius = 0.5f;
        
        [Tooltip("Maximum lifetime before auto-destroy.")]
        public float MaxLifetime = 10f;

        [Header("Guard Mitigation")]
        [Tooltip("Damage multiplier when player is guarding (0.25 = 75% reduction, 0.5 = 50% reduction).")]
        [Range(0f, 1f)] public float GuardDamageMultiplier = 0.25f;

        [Header("Visual")]
        [Tooltip("Transform to rotate for spin effect (leave null to spin this object).")]
        public Transform SpinTransform;
        
        [Tooltip("Spin speed in degrees per second.")]
        public float SpinSpeed = 720f;

        // Flight configuration (set by CleanserBrain when spawned)
        [HideInInspector] public bool UseStraightPath;
        [HideInInspector] public bool ReturnsLikeBoomerang;
        [HideInInspector] public bool UseCurvedPath;
        [HideInInspector] public bool UseHoming;
        [HideInInspector] public float HomingUpdateInterval;
        [HideInInspector] public float HomingStrength;
        [HideInInspector] public float Speed;
        [HideInInspector] public float CurveWidth;
        [HideInInspector] public float OvershootDistance;
        [HideInInspector] public bool DestroyOnWallHit;

        // Runtime state
        private Transform target;
        private Transform origin;
        private Vector3 targetPosition;
        private Vector3 originPosition;
        private ProjectilePhase phase = ProjectilePhase.Outbound;
        private float flightTime;
        private float lastHomingUpdate;
        private bool hasHitPlayer;
        private Vector3 curveDirection; // Perpendicular direction for curved path
        private float totalFlightDistance;
        private float currentTravelDistance;
        
        // Magnetic return system reference
        private CleanserDualWieldSystem dualWieldSystem;
        private bool shouldTriggerMagneticReturn = false;

        // Bezier control points for curved path
        private Vector3 bezierP0, bezierP1, bezierP2, bezierP3;
        private float bezierT;

        // Pre-allocated array for physics queries (avoids GC allocation)
        private static readonly Collider[] hitBuffer = new Collider[4];

        /// <summary>
        /// Initializes the projectile with its flight configuration.
        /// </summary>
        public void Initialize(Transform playerTarget, Transform spawnOrigin, SpareTossConfig config)
        {
            Initialize(playerTarget, spawnOrigin, config, null);
        }

        /// <summary>
        /// Initializes the projectile with its flight configuration and dual wield system for magnetic return.
        /// </summary>
        public void Initialize(Transform playerTarget, Transform spawnOrigin, SpareTossConfig config, CleanserDualWieldSystem dualWield)
        {
            target = playerTarget;
            origin = spawnOrigin;
            dualWieldSystem = dualWield;
            
            UseStraightPath = config.UseStraightPath;
            ReturnsLikeBoomerang = config.ReturnsOnStraightPath;
            UseCurvedPath = config.UseCurvedBoomerang;
            UseHoming = config.UseHoming;
            HomingUpdateInterval = config.HomingUpdateInterval;
            HomingStrength = config.HomingStrength;
            DestroyOnWallHit = config.DestroyOnWallHit;
            WallLayerMask = config.WallLayerMask;
            
            Speed = Random.Range(config.ProjectileSpeedRange.x, config.ProjectileSpeedRange.y);
            CurveWidth = Random.Range(config.CurveWidthRange.x, config.CurveWidthRange.y);
            OvershootDistance = Random.Range(config.OvershootDistanceRange.x, config.OvershootDistanceRange.y);

            // Cache positions
            targetPosition = target != null ? target.position : transform.position + transform.forward * 10f;
            originPosition = origin != null ? origin.position : transform.position;
            
            // Calculate curve direction (perpendicular to throw direction)
            Vector3 throwDir = (targetPosition - originPosition).normalized;
            curveDirection = Vector3.Cross(throwDir, Vector3.up).normalized;
            
            // Randomize curve side (left or right)
            if (Random.value > 0.5f)
                curveDirection = -curveDirection;

            // Setup bezier curve if using curved path
            if (UseCurvedPath)
            {
                SetupBezierCurve();
            }

            totalFlightDistance = Vector3.Distance(originPosition, targetPosition) + OvershootDistance;
            phase = ProjectilePhase.Outbound;
            hasHitPlayer = false;
            flightTime = 0f;
            bezierT = 0f;
            lastHomingUpdate = Time.time;

            StartCoroutine(FlightLoop());
        }

        private void SetupBezierCurve()
        {
            // P0 = start, P1 = control point 1, P2 = control point 2, P3 = end
            bezierP0 = originPosition;
            
            Vector3 toTarget = targetPosition - originPosition;
            float dist = toTarget.magnitude;
            Vector3 midPoint = originPosition + toTarget * 0.5f;
            
            // Outbound: curve goes wide then to overshoot point
            Vector3 overshootPoint = targetPosition + toTarget.normalized * OvershootDistance;
            bezierP1 = midPoint + curveDirection * CurveWidth; // Control point curves outward
            bezierP2 = overshootPoint + curveDirection * (CurveWidth * 0.5f);
            bezierP3 = overshootPoint;
        }

        private void SetupReturnBezierCurve()
        {
            // Return path: from current position back to origin, curving the other way
            bezierP0 = transform.position;
            
            Vector3 toOrigin = originPosition - transform.position;
            float dist = toOrigin.magnitude;
            Vector3 midPoint = transform.position + toOrigin * 0.5f;
            
            // Return: curve the opposite direction
            bezierP1 = midPoint - curveDirection * (CurveWidth * 0.7f);
            bezierP2 = originPosition - curveDirection * (CurveWidth * 0.3f);
            bezierP3 = originPosition;
            
            bezierT = 0f;
        }

        private IEnumerator FlightLoop()
        {
            float startTime = Time.time;
            
            while (flightTime < MaxLifetime && !hasHitPlayer)
            {
                flightTime = Time.time - startTime;
                
                // Update homing target position
                if (UseHoming && target != null && Time.time - lastHomingUpdate >= HomingUpdateInterval)
                {
                    UpdateHomingTarget();
                    lastHomingUpdate = Time.time;
                }

                // Move based on path type
                if (UseCurvedPath)
                {
                    MoveBezier();
                }
                else
                {
                    MoveStraight();
                }

                // Spin visual
                RotateVisual();
                
                // Check for hits
                CheckHits();
                
                // Check for wall collision
                if (CheckWallCollision())
                {
                    yield break;
                }
                
                yield return null;
            }

            // Lifetime expired or completed flight - trigger magnetic return
            OnProjectileFlightComplete();
        }

        /// <summary>
        /// Called when the projectile completes its flight (reached origin or lifetime expired).
        /// Triggers magnetic return if dual wield system is available.
        /// </summary>
        private void OnProjectileFlightComplete()
        {
            // If we have a dual wield system reference, notify it to start magnetic return
            // The projectile visual is destroyed, but the actual weapon returns magnetically
            if (dualWieldSystem != null && shouldTriggerMagneticReturn)
            {
                // Find which weapon was thrown (if any) and trigger its return
                // This is a visual-only projectile, so we just destroy it
                // The actual weapon system handles the magnetic return
            }
            
            Destroy(gameObject);
        }

        private void MoveStraight()
        {
            Vector3 moveTarget;
            
            if (phase == ProjectilePhase.Outbound)
            {
                // Move toward target + overshoot
                Vector3 overshootPoint = targetPosition + (targetPosition - originPosition).normalized * OvershootDistance;
                moveTarget = overshootPoint;
                
                transform.position = Vector3.MoveTowards(transform.position, moveTarget, Speed * Time.deltaTime);
                Vector3 dir = (moveTarget - transform.position).normalized;
                if (dir.sqrMagnitude > 0.001f)
                    transform.forward = dir;
                
                // Check if reached overshoot point
                if (Vector3.Distance(transform.position, overshootPoint) < 0.5f)
                {
                    if (ReturnsLikeBoomerang)
                    {
                        phase = ProjectilePhase.Returning;
#if UNITY_EDITOR
                        EnemyBehaviorDebugLogBools.Log(nameof(CleanserProjectile), "[CleanserProjectile] Reached overshoot, returning.");
#endif
                    }
                    else
                    {
                        // Straight path, no return - trigger magnetic return for actual weapon
                        shouldTriggerMagneticReturn = true;
                        OnProjectileFlightComplete();
                    }
                }
            }
            else // Returning
            {
                moveTarget = originPosition;
                transform.position = Vector3.MoveTowards(transform.position, moveTarget, Speed * Time.deltaTime);
                Vector3 dir = (moveTarget - transform.position).normalized;
                if (dir.sqrMagnitude > 0.001f)
                    transform.forward = dir;
                
                // Check if returned to origin (boss catches it or lets it pass)
                if (Vector3.Distance(transform.position, originPosition) < 1f)
                {
                    // Returned to boss - can catch or let it continue to rest position
                    shouldTriggerMagneticReturn = true;
                    OnProjectileFlightComplete();
                }
            }
        }

        private void MoveBezier()
        {
            // Calculate bezier speed (normalize to maintain consistent speed regardless of curve length)
            float curveLength = EstimateBezierLength();
            float tIncrement = (Speed * Time.deltaTime) / Mathf.Max(curveLength, 0.1f);
            
            bezierT += tIncrement;
            
            if (bezierT >= 1f)
            {
                if (phase == ProjectilePhase.Outbound)
                {
                    // Reached end of outbound curve, start return
                    phase = ProjectilePhase.Returning;
                    SetupReturnBezierCurve();
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.Log(nameof(CleanserProjectile), "[CleanserProjectile] Completed outbound curve, returning.");
#endif
                }
                else
                {
                    // Completed return - trigger magnetic return for actual weapon
                    shouldTriggerMagneticReturn = true;
                    OnProjectileFlightComplete();
                    return;
                }
            }

            // Calculate position on bezier curve
            Vector3 newPos = CalculateBezierPoint(bezierT);
            
            // Face movement direction
            Vector3 moveDir = (newPos - transform.position).normalized;
            if (moveDir.sqrMagnitude > 0.001f)
            {
                transform.forward = moveDir;
            }
            
            transform.position = newPos;
        }

        private Vector3 CalculateBezierPoint(float t)
        {
            // Cubic bezier: B(t) = (1-t)³P0 + 3(1-t)²tP1 + 3(1-t)t²P2 + t³P3
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            
            Vector3 point = uuu * bezierP0;
            point += 3f * uu * t * bezierP1;
            point += 3f * u * tt * bezierP2;
            point += ttt * bezierP3;
            
            return point;
        }

        private float EstimateBezierLength()
        {
            // Rough estimate by sampling the curve
            float length = 0f;
            Vector3 prev = bezierP0;
            int samples = 10;
            
            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 current = CalculateBezierPoint(t);
                length += Vector3.Distance(prev, current);
                prev = current;
            }
            
            return length;
        }

        private void UpdateHomingTarget()
        {
            if (target == null)
                return;

            // Smoothly adjust target position
            targetPosition = Vector3.Lerp(targetPosition, target.position, HomingStrength);
            
            // Recalculate bezier curves if using curved path
            if (UseCurvedPath && phase == ProjectilePhase.Outbound)
            {
                // Partial recalculation - adjust end point while maintaining curve shape
                Vector3 currentPos = transform.position;
                Vector3 overshootPoint = targetPosition + (targetPosition - originPosition).normalized * OvershootDistance;
                
                // Only update P3 (end point) smoothly
                bezierP3 = Vector3.Lerp(bezierP3, overshootPoint, HomingStrength * 0.5f);
            }
        }

        private void RotateVisual()
        {
            Transform toRotate = SpinTransform != null ? SpinTransform : transform;
            toRotate.Rotate(Vector3.forward, SpinSpeed * Time.deltaTime, Space.Self);
        }

        private void CheckHits()
        {
            if (hasHitPlayer)
                return;

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, HitRadius, hitBuffer, PlayerLayerMask);
            
            for (int i = 0; i < hitCount; i++)
            {
                var hit = hitBuffer[i];
                if (hit.CompareTag("Player"))
                {
                    // Wing-type damage: Can be guarded (reduced damage), cannot be parried
                    if (Utilities.Combat.CombatManager.isGuarding)
                    {
                        float mitigatedDamage = Damage * GuardDamageMultiplier;
                        ApplyDamage(hit, mitigatedDamage);
#if UNITY_EDITOR
                        EnemyBehaviorDebugLogBools.Log(nameof(CleanserProjectile), $"[CleanserProjectile] Hit blocked, dealt {mitigatedDamage} damage (guard multiplier: {GuardDamageMultiplier}).");
#endif
                    }
                    else
                    {
                        ApplyDamage(hit, Damage);
#if UNITY_EDITOR
                        EnemyBehaviorDebugLogBools.Log(nameof(CleanserProjectile), $"[CleanserProjectile] Hit player, dealt {Damage} damage.");
#endif
                    }
                    
                    hasHitPlayer = true;
                    
                    // Don't destroy on hit if it's a boomerang - it can hit multiple times
                    if (!ReturnsLikeBoomerang && !UseCurvedPath)
                    {
                        Destroy(gameObject);
                    }
                    else
                    {
                        // Brief invulnerability window to prevent multi-hit spam
                        StartCoroutine(HitCooldown());
                    }
                    
                    return;
                }
            }
        }

        private IEnumerator HitCooldown()
        {
            hasHitPlayer = true;
            yield return new WaitForSeconds(0.5f);
            hasHitPlayer = false;
        }

        private void ApplyDamage(Collider playerCollider, float amount)
        {
            if (playerCollider.TryGetComponent<IHealthSystem>(out var health))
            {
                health.LoseHP(amount);
            }
            else
            {
                var parentHealth = playerCollider.GetComponentInParent<IHealthSystem>();
                parentHealth?.LoseHP(amount);
            }
        }

        private bool CheckWallCollision()
        {
            if (Physics.CheckSphere(transform.position, HitRadius * 0.5f, WallLayerMask))
            {
                if (DestroyOnWallHit)
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.Log(nameof(CleanserProjectile), "[CleanserProjectile] Hit wall, destroying.");
#endif
                    Destroy(gameObject);
                    return true;
                }
                else
                {
                    // Stop or bounce (simplified: just stop)
                    Speed = 0f;
                }
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, HitRadius);
            
            if (UseCurvedPath && Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Vector3 prev = bezierP0;
                for (int i = 1; i <= 20; i++)
                {
                    float t = i / 20f;
                    Vector3 current = CalculateBezierPoint(t);
                    Gizmos.DrawLine(prev, current);
                    prev = current;
                }
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(bezierP1, 0.3f);
                Gizmos.DrawSphere(bezierP2, 0.3f);
            }
        }
    }
}
