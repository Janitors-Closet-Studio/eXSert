using UnityEngine;

/// <summary>
/// Opt-in marker that designates the collider on this GameObject as the only valid
/// hurtbox for the enemy hierarchy it belongs to.
///
/// When ANY <see cref="EnemyHurtbox"/> exists anywhere under an enemy root,
/// <c>HitboxDamageManager</c> will ONLY register hits through colliders that carry
/// this component. All other child colliders (weapon hitboxes, rig bones, etc.)
/// are silently ignored.
///
/// Enemies with no <see cref="EnemyHurtbox"/> anywhere in their hierarchy keep
/// existing behaviour unchanged (fully backward-compatible).
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyHurtbox : MonoBehaviour { }
