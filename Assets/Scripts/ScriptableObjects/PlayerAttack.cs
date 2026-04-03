using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utilities.Combat.Attacks
{
    public enum AttackType
    {
        LightSingle,
        LightAOE,
        LightAerial,
        HeavySingle,
        HeavyAOE,
        HeavyAerial
    }
#pragma warning restore CS0414

    [Serializable]
    [CreateAssetMenu(fileName = "Player Attack", menuName = "Attacks/Player Attack")]
    public class PlayerAttack : ScriptableObject
    {
        private enum AttackCategory
        {
            Single,
            AOE,
            Aerial
        }
        private enum AttackWeight
        {
            Light,
            Heavy
        }

        // ---------------------------------------------------------------------------------------------

        [Header("General Properties")]
        [SerializeField, Tooltip("Attack ID for combo system (e.g., SX1, AX2, AerialX1, AerialY1)")]
        private string _attackId = "";
        public string attackId { get => _attackId != "" ? _attackId : this.name; }

        [SerializeField, Tooltip("Name of the attack shown in UI and logs")]
        private string _attackName;

        // If no custom name is given, use the scriptable object's name
        public string attackName { get => _attackName != "" ? _attackName : this.name; }

        [Space, Header("Combo Metadata")]
        [SerializeField, Tooltip("Explicit combo stage index (1-3) for validation and progression.")]
        private int _comboStage = 1;
        public int comboStage { get => Mathf.Clamp(_comboStage, 1, 3); }

        [SerializeField, Tooltip("Marks this attack as a finisher that forces a combo reset.")]
        private bool _isFinisher = false;
        public bool isFinisher { get => _isFinisher; }

        [SerializeField, Tooltip("Type of attack, single target or area of effect")]
        private AttackCategory _attackType = AttackCategory.Single;
        [SerializeField, Tooltip("Weight of the attack, light or heavy")]
        private AttackWeight _attackWeight = AttackWeight.Light;

        // Combines the two private enums into one public enum for easier use
        // uses a switch expression to return the correct AttackType based on the private enums selected
        public AttackType attackType
        {
            get
            {
                switch (_attackWeight, _attackType)
                {
                    case (AttackWeight.Light, AttackCategory.Single):
                        return AttackType.LightSingle;

                    case (AttackWeight.Light, AttackCategory.AOE):
                        return AttackType.LightAOE;

                    case (AttackWeight.Light, AttackCategory.Aerial):
                        return AttackType.LightAerial;

                    case (AttackWeight.Heavy, AttackCategory.Single):
                        return AttackType.HeavySingle;

                    case (AttackWeight.Heavy, AttackCategory.AOE):
                        return AttackType.HeavyAOE;

                    case (AttackWeight.Heavy, AttackCategory.Aerial):
                        return AttackType.HeavyAerial;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private set { }
        }

        // ---------------------------------------------------------------------------------------------

        [Space, Header("Damage Properties")]

        [SerializeField, Tooltip("Damage dealt by this attack")]
        private int _damage = 5;
        public int damage { get => _damage; }

        [SerializeField, Tooltip("Wether the attack should launch the enemy on hit")]
        private bool _launch = false;
        public bool launch { get => _launch; }
        [SerializeField, Tooltip("The amount of knockback applied to enemies hit")]
        private float _knockback = 3;
        public float knockback { get => _knockback; }
        [SerializeField, Tooltip("The angle at which enemies are knocked back. 0 is parallel to the ground")]
        private float _knockbackAngle = 0;
        public float knockbackAngle { get => _knockbackAngle; }

        // ---------------------------------------------------------------------------------------------

        [Space, Header("Range Properties")]

        [SerializeField, Tooltip("Whether or not the attack should use a custom range or the default range of 1.5")]
        private bool _customRange = false;
        private static readonly float defaultRange = 1.5f;
        [SerializeField] private float _range = 1.5f;
        public float range { get => _customRange ? _range : defaultRange; }

        [SerializeField, Tooltip("Whether or not the attack should use a custom width or the default width of 1")]
        private bool _customWidth = false;
        private static readonly float defaultWidth = 1f;
        [SerializeField] private float _width = 1f;
        public float width { get => _customWidth ? _width : defaultWidth; }

        [SerializeField, Tooltip("Whether to use the default distance from player or a custom one")]
        private bool _defaultDistanceFromPlayer = true;
        [SerializeField, Tooltip("Custom distance from player if not using default")]
        private float _customAttackDistanceFromPlayer = 0.75f;
        public float distanceFromPlayer { get => _defaultDistanceFromPlayer ? 1f : range / 2f; }

        [SerializeField, Tooltip("Vertical offset for hitbox spawn height (0 = ground level, 1 = chest height, etc.)")]
        private float _hitboxHeightOffset = 1f;
        public float hitboxHeightOffset { get => _hitboxHeightOffset; }

        [SerializeField, Tooltip("Maximum distinct enemies this attack can damage per activation (0 = unlimited). Singles default to 1, AOE can be higher.")]
        private int _maxTargetsPerActivation = 1;
        public int maxTargetsPerActivation => Mathf.Max(0, _maxTargetsPerActivation);

        // Returns the dimensions of the area of effect attack, or zero vector if not AOE
        public Vector2 areaOfEffectDimensions
        {
            get
            {
                if (_attackType == AttackCategory.AOE)
                    return new Vector2(width, range);
                else
                    return Vector2.zero;
            }
        }

        // ---------------------------------------------------------------------------------------------

        [Space, Header("Movement")]

        [SerializeField, Tooltip("Forward distance to nudge the player when this heavy attack starts.")]
        private float _forwardMoveDistance = 0f;
        public float forwardMoveDistance { get => Mathf.Max(0f, _forwardMoveDistance); }

        [SerializeField, Tooltip("Seconds to smoothly move the player forward for this heavy attack.")]
        private float _forwardMoveDuration = 0.12f;
        public float forwardMoveDuration { get => Mathf.Max(0f, _forwardMoveDuration); }

        // ---------------------------------------------------------------------------------------------

        [Space, Header("Timing / Animation Reference")]

        [SerializeField, Tooltip("Animation clip associated with this attack (reference for event placement).")]
        private AnimationClip _animationClip;
        public AnimationClip animationClip { get => _animationClip; }

        [SerializeField, Tooltip("If true, player may continue holding the current stance idle even when no buffered input exists after the cancel window.")]
        private bool _allowLateChain = true;
        public bool allowLateChain { get => _allowLateChain; }

        [SerializeField, Tooltip("How long the hitbox stays active once spawned.")]
        private float _hitboxDuration = 0.1f;
        public float hitboxDuration { get => Mathf.Max(0f, _hitboxDuration); }

        [SerializeField, Tooltip("If greater than zero, reapplies damage every interval during the hitbox duration (useful for flamethrowers).")]
        private float _tickInterval = 0f;
        public float tickInterval { get => Mathf.Max(0f, _tickInterval); }

        [Space, Header("Impact VFX")]
        [SerializeField, Tooltip("Optional legacy VFX prefab spawned when the hitbox activates.")]
        private GameObject _hitVfxPrefab;
        public GameObject hitVfxPrefab => _hitVfxPrefab;

        [SerializeField, Tooltip("ID of the VFX anchor on the player rig. Leave blank to spawn at the hitbox origin.")]
        private string _vfxAnchorId;
        public string vfxAnchorId => _vfxAnchorId;

        [SerializeField, Tooltip("Seconds before the spawned VFX is destroyed. 0 = immediate, -1 = never destroy (handled by prefab).")]
        private float _vfxLifetime = 1f;
        public float vfxLifetime => _vfxLifetime < 0f ? -1f : Mathf.Max(0f, _vfxLifetime);

        [Serializable]
        public struct VfxEntry
        {
            [SerializeField] private GameObject prefab;
            [SerializeField, Tooltip("Anchor ID resolved via PlayerVfxAnchorRegistry. Leave blank to use hitbox pose.")]
            private string anchorId;
            [SerializeField, Tooltip("Seconds before this VFX is destroyed. 0 = immediate, -1 = never destroy (prefab handles cleanup).")]
            private float lifetime;

            public GameObject Prefab => prefab;
            public string AnchorId => anchorId;
            public float Lifetime => lifetime < 0f ? -1f : Mathf.Max(0f, lifetime);

            public VfxEntry(GameObject prefab, string anchorId, float lifetime)
            {
                this.prefab = prefab;
                this.anchorId = anchorId;
                this.lifetime = lifetime;
            }
        }

        [SerializeField, Tooltip("Optional list of additional VFX to spawn alongside the legacy fields above.")]
        private VfxEntry[] _additionalVfxEntries;
        public IReadOnlyList<VfxEntry> additionalVfxEntries => _additionalVfxEntries ?? Array.Empty<VfxEntry>();

        public IEnumerable<VfxEntry> EnumerateAllVfx()
        {
            if (_hitVfxPrefab != null)
            {
                yield return new VfxEntry(_hitVfxPrefab, _vfxAnchorId, _vfxLifetime);
            }

            if (_additionalVfxEntries == null)
                yield break;

            foreach (var entry in _additionalVfxEntries)
            {
                if (entry.Prefab == null)
                    continue;

                yield return entry;
            }
        }

        // ---------------------------------------------------------------------------------------------

        [Space, Header("Sound Effects")]

        [SerializeField, Tooltip("Sound effect played when the attack is performed")]
        private AudioClip _attackSFX;
        public AudioClip attackSFX { get => _attackSFX; }

        [SerializeField, Tooltip("Sound effect played when the attack lands/hits an enemy")]
        private AudioClip _hitSFX;
        public AudioClip hitSFX { get => _hitSFX; }

        // ---------------------------------------------------------------------------------------------

        // Creates and returns a hitbox GameObject for this attack at the specified position and forward direction
        public void GetHitboxPose(Vector3 playerPosition, Vector3 playerForward, out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            spawnPosition = playerPosition + (playerForward * distanceFromPlayer) + (Vector3.up * hitboxHeightOffset);
            spawnRotation = Quaternion.LookRotation(playerForward);
        }

        public GameObject createHitBox(Vector3 playerPosition, Vector3 playerForward)
        {
            GetHitboxPose(playerPosition, playerForward, out var spawnPosition, out var spawnRotation);
            return CreateHitBoxAt(spawnPosition, spawnRotation);
        }

        public GameObject CreateHitBoxAt(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            GameObject hitbox = new GameObject(attackName + " Hitbox");
            hitbox.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            BoxCollider hb = hitbox.AddComponent<BoxCollider>();
            hb.isTrigger = true;
            hb.size = new Vector3(width, 1f, range);

            HitboxDamageManager damageManager = hitbox.AddComponent<HitboxDamageManager>();
            damageManager.Configure(attackName, damage, maxTargetsPerActivation);

            var debugVisual = hitbox.AddComponent<AttackHitboxVisualizer>();
            debugVisual.width = width;
            debugVisual.range = range;
            debugVisual.attackName = attackName;

            return hitbox;
        }
    }
}
