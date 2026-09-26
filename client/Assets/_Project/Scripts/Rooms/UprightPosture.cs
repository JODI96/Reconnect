using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Straightens the upper body after the animation: the library's clips lean forward (~7° standing, ~11° walking,
    /// a stylised game walk), and retargeted onto the MakeHuman figures the chest hangs ~8° to the right. Spine and
    /// chest are rotated back and to the middle, more forward correction while walking, blended smoothly.
    /// Sits on every avatar prefab (added by AvatarSetup); AvatarView sets <see cref="Walking"/> and <see cref="Sitting"/>.
    /// </summary>
    public sealed class UprightPosture : MonoBehaviour
    {
        public const float StandingCorrection = 4f;
        public const float WalkingCorrection = 7f;
        public const float SidewaysCorrection = 8f;

        private Transform[] _bones = System.Array.Empty<Transform>();
        private Quaternion[] _corrected = System.Array.Empty<Quaternion>();
        private float _walkBlend;
        private float _weight = 1f;

        public bool Walking { get; set; }

        /// <summary>Sitting clips have their own posture: no correction then.</summary>
        public bool Sitting { get; set; }

        private void Awake()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                return;
            }
            _bones = new[] { animator.GetBoneTransform(HumanBodyBones.Spine), animator.GetBoneTransform(HumanBodyBones.Chest) };
            _bones = System.Array.FindAll(_bones, bone => bone != null);
            _corrected = new Quaternion[_bones.Length];
        }

        /// <summary>Jumps to the target posture (teleport, tests) instead of blending.</summary>
        public void Snap()
        {
            _walkBlend = Walking ? 1f : 0f;
            _weight = Sitting ? 0f : 1f;
        }

        private void LateUpdate()
        {
            _walkBlend = Mathf.MoveTowards(_walkBlend, Walking ? 1f : 0f, 4f * Time.deltaTime);
            _weight = Mathf.MoveTowards(_weight, Sitting ? 0f : 1f, 3f * Time.deltaTime);
            var angle = Mathf.Lerp(StandingCorrection, WalkingCorrection, _walkBlend) * _weight;
            if (_weight <= 0.001f || _bones.Length == 0)
            {
                return;
            }
            // Back around the body's own left-right axis, shared between lower and upper spine. A bone the animator
            // did not write this frame (culled off screen) still carries last frame's correction: leave it.
            var share = 1f / _bones.Length;
            var half = Quaternion.AngleAxis(-angle * share, transform.right)
                       * Quaternion.AngleAxis(SidewaysCorrection * _weight * share, transform.forward);
            for (var i = 0; i < _bones.Length; i++)
            {
                if (Quaternion.Angle(_bones[i].rotation, _corrected[i]) < 0.001f)
                {
                    continue;
                }
                _bones[i].rotation = half * _bones[i].rotation;
                _corrected[i] = _bones[i].rotation;
            }
        }
    }
}
