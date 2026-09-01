using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP 本地预测移动。位移只由 NetClient 按服务器固定 tick 调用 SimulateTick，
    /// Update 只负责动画与 IK，避免渲染帧率和服务器 tick 不一致造成持续漂移。
    /// </summary>
    [RequireComponent(typeof(PlayerModel))]
    public class PvPMotor : MonoBehaviour
    {
        // 必须与 Server/GameWorldSettings 保持一致。
        public const float JogSpeed = 5f;
        public const float SprintSpeed = 8f;
        public const float AimMoveSpeed = 2.5f;
        public const float GroundY = 0.05f;
        public const float TurnSpeedDeg = 720f;
        public const float Gravity = -15f;
        public const float JumpVelocity = 6.7f;
        public const float SlideDuration = 0.8f;
        public const float SlideStartSpeed = 7f;
        public const float SlideEndSpeed = 1.5f;
        public const float SprintSlideBoost = 2f;
        public const float ArenaHalfExtent = 40f;

        [Tooltip("当前表现输入（由 NetClient 每帧写入）")]
        public Vector2 moveInput;
        public bool isSprint, isAiming, isJumping, isSlide, isFire;
        public Vector3 worldMove = Vector3.zero;
        public Vector3 aimPoint = Vector3.zero;
        public float bodyYawDeg;

        private PlayerModel _model;
        private Animator _animator;
        private float _verticalSpeed;
        private bool _isGrounded = true;
        private MultiAimConstraint[] _aimConstraints;
        private TwoBoneIKConstraint _hipIK;
        private float _aimIKWeight;
        private float _speedBlend;
        private int _animState = -1;
        private float _groundY = GroundY;
        private bool _isSliding;
        private int _slideTicksRemaining;
        private Vector3 _slideDirection = Vector3.forward;
        private bool _slideSprintBoost;

        public float VerticalSpeed => _verticalSpeed;
        public bool IsGrounded => _isGrounded;
        public float SpeedBlend => _speedBlend;
        public bool IsSliding => _isSliding;
        public int SlideTicksRemaining => _slideTicksRemaining;

        private void Awake()
        {
            _model = GetComponent<PlayerModel>();
            if (_model != null && _model.cc != null)
                _groundY = Mathf.Max(0.02f, _model.cc.height * 0.5f - _model.cc.center.y);
        }

        public void Teleport(Vector3 pos, float yawDeg)
        {
            transform.position = pos;
            bodyYawDeg = yawDeg;
            _verticalSpeed = 0f;
            _isGrounded = true;
            _isSliding = false;
            _slideTicksRemaining = 0;
        }

        /// <summary>用完整权威移动状态建立重放基线。</summary>
        public void ApplyAuthoritativeState(PlayerSnapshotMessage state)
        {
            transform.position = new Vector3(state.x, state.y, state.z);
            bodyYawDeg = state.bodyYawDeg;
            _verticalSpeed = state.verticalSpeed;
            _isGrounded = state.isGrounded;
            _isSliding = state.isSliding;
            _slideTicksRemaining = Mathf.Max(0, state.slideTicksRemaining);
            _slideDirection = new Vector3(state.slideDirectionX, 0f, state.slideDirectionZ);
            if (_slideDirection.sqrMagnitude < 0.0001f)
                _slideDirection = transform.forward;
            else
                _slideDirection.Normalize();
            _slideSprintBoost = state.slideSprintBoost;
            transform.rotation = Quaternion.Euler(0f, bodyYawDeg, 0f);
        }

        /// <summary>按当前输入计算本 tick 要发给服务器的权威朝向。</summary>
        public float CalculateBodyYaw(Vector3 inputWorldMove, bool aiming, float dt)
        {
            if (_isSliding)
            {
                float slideYaw = Mathf.Atan2(_slideDirection.x, _slideDirection.z) * Mathf.Rad2Deg;
                return Mathf.MoveTowardsAngle(bodyYawDeg, slideYaw, TurnSpeedDeg * dt);
            }

            if (aiming)
            {
                Vector3 cameraForward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
                cameraForward.y = 0f;
                if (cameraForward.sqrMagnitude > 0.01f)
                    return Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg;
                return bodyYawDeg;
            }

            Vector3 move = new Vector3(inputWorldMove.x, 0f, inputWorldMove.z);
            if (move.sqrMagnitude <= 0.0001f) return bodyYawDeg;
            move.Normalize();
            float targetYaw = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
            return Mathf.MoveTowardsAngle(bodyYawDeg, targetYaw, TurnSpeedDeg * dt);
        }

        /// <summary>执行一次与服务器相同的固定步进；也用于快照后的未确认输入重放。</summary>
        public void SimulateTick(PlayerInputMessage input, float dt, int serverTickRate)
        {
            if (_model == null || _model.isDead || input == null) return;

            bodyYawDeg = input.bodyYawDeg;
            bool aiming = input.isAiming || input.isFire;

            if (_isGrounded && input.isJumping)
            {
                _verticalSpeed = JumpVelocity;
                _isGrounded = false;
                _isSliding = false;
                _slideTicksRemaining = 0;
            }

            if (!_isSliding && _isGrounded && input.isSlide)
            {
                _isSliding = true;
                _slideTicksRemaining = Mathf.Max(1, Mathf.RoundToInt(SlideDuration * serverTickRate));
                _slideDirection = new Vector3(input.worldMoveX, 0f, input.worldMoveZ);
                if (_slideDirection.sqrMagnitude <= 0.0001f)
                {
                    float yawRad = bodyYawDeg * Mathf.Deg2Rad;
                    _slideDirection = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
                }
                else
                {
                    _slideDirection.Normalize();
                }
                _slideSprintBoost = input.isSprint;
            }

            if (_isSliding)
            {
                StepSlide(dt, serverTickRate);
                transform.rotation = Quaternion.Euler(0f, bodyYawDeg, 0f);
                return;
            }

            Vector3 inputDir = new Vector3(input.worldMoveX, 0f, input.worldMoveZ);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();
            bool moving = inputDir.magnitude > 0.01f;
            float speed = moving
                ? input.isSprint ? SprintSpeed : aiming ? AimMoveSpeed : JogSpeed
                : 0f;

            Vector3 moveDir;
            if (aiming)
            {
                moveDir = inputDir.sqrMagnitude > 0.0001f ? inputDir.normalized : Vector3.zero;
            }
            else
            {
                float yawRad = bodyYawDeg * Mathf.Deg2Rad;
                moveDir = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            }

            Vector3 newPos = transform.position + moveDir * (speed * dt);
            if (!_isGrounded)
            {
                _verticalSpeed += Gravity * dt;
                newPos.y = transform.position.y + _verticalSpeed * dt;
            }

            if (newPos.y <= _groundY)
            {
                newPos.y = _groundY;
                _verticalSpeed = 0f;
                _isGrounded = true;
            }

            newPos.x = Mathf.Clamp(newPos.x, -ArenaHalfExtent, ArenaHalfExtent);
            newPos.z = Mathf.Clamp(newPos.z, -ArenaHalfExtent, ArenaHalfExtent);
            transform.position = newPos;
            transform.rotation = Quaternion.Euler(0f, bodyYawDeg, 0f);
            _isGrounded = newPos.y <= _groundY + 0.001f;
        }

        private void StepSlide(float dt, int serverTickRate)
        {
            int totalTicks = Mathf.Max(1, Mathf.RoundToInt(SlideDuration * serverTickRate));
            float t = _slideTicksRemaining / (float)totalTicks;
            float speed = Mathf.Lerp(SlideEndSpeed, SlideStartSpeed, t);
            if (_slideSprintBoost) speed += SprintSlideBoost * t;

            Vector3 newPos = transform.position + _slideDirection * (speed * dt);
            newPos.y = _groundY;
            newPos.x = Mathf.Clamp(newPos.x, -ArenaHalfExtent, ArenaHalfExtent);
            newPos.z = Mathf.Clamp(newPos.z, -ArenaHalfExtent, ArenaHalfExtent);
            transform.position = newPos;
            _verticalSpeed = 0f;
            _isGrounded = true;
            _slideTicksRemaining--;
            if (_slideTicksRemaining <= 0) _isSliding = false;
        }

        private void Update()
        {
            if (_model == null || _model.isDead) return;
            if (_animator == null) _animator = _model.animator;

            float dt = Time.deltaTime;
            bool aiming = isAiming || isFire;
            bool moving = new Vector3(worldMove.x, 0f, worldMove.z).magnitude > 0.01f;

            if (_aimConstraints == null)
            {
                _aimConstraints = _model.GetComponentsInChildren<MultiAimConstraint>(true);
                _hipIK = _model.GetComponentInChildren<TwoBoneIKConstraint>(true);
            }
            _aimIKWeight = Mathf.MoveTowards(_aimIKWeight, aiming ? 1f : 0f, 8f * dt);
            foreach (var constraint in _aimConstraints) constraint.weight = _aimIKWeight;
            if (_hipIK != null) _hipIK.weight = 1f - _aimIKWeight;

            int desired = _isSliding ? 4 : !_isGrounded ? 3 : aiming ? 2 : moving ? 1 : 0;
            if (desired != _animState)
            {
                _animState = desired;
                string stateName = desired == 4 ? "RunningSlide"
                    : desired == 3 ? "Hover"
                    : desired == 2 ? "Aiming"
                    : desired == 1 ? "Move"
                    : "Idle";
                if (_animator != null) _animator.CrossFadeInFixedTime(stateName, 0.25f);
            }

            if (_isGrounded)
                _speedBlend = moving ? isSprint ? 1f : aiming ? 0.5f : 0.66f : 0f;

            if (_animator != null)
            {
                _animator.SetFloat(PlayerModel.SpeedHash, _speedBlend);
                _animator.SetFloat(PlayerModel.VerticalSpeedHash, _verticalSpeed);
                _animator.SetBool(PlayerModel.IsGroundedHash, _isGrounded);
                _animator.SetBool(PlayerModel.IsSprintingHash, isSprint);
                _animator.SetFloat(PlayerModel.AimingXHash, moveInput.x);
                _animator.SetFloat(PlayerModel.AimingYHash, moveInput.y);
            }
        }
    }
}
