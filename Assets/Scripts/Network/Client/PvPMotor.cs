using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections.Generic;
using ElementWar.Rules;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP 本地预测移动。位移只由 NetClient 按服务器固定 tick 调用 SimulateTick，
    /// Update 只负责动画与 IK，避免渲染帧率和服务器 tick 不一致造成持续漂移。
    /// </summary>
    [RequireComponent(typeof(PlayerModel))]
    public class PvPMotor : MonoBehaviour
    {
        // 规则文件加载前的安全默认值；运行时由 GameRules.v1.json 覆盖。
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
        public const float CollisionRadius = 0.4f;
        public const float WalkableStepHeight = 0.65f;

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
        private readonly List<Vector3> _dynamicCollisionPositions = new();
        private bool _isSliding;
        private int _slideTicksRemaining;
        private Vector3 _slideDirection = Vector3.forward;
        private bool _slideSprintBoost;
        private bool _settlementPlayback;
        private float _settlementElapsed;
        private float _settlementDuration;
        private float _settlementScale;
        private Vector3 _settlementVelocity;
        private float _jogSpeed = JogSpeed;
        private float _sprintSpeed = SprintSpeed;
        private float _aimMoveSpeed = AimMoveSpeed;
        private float _turnSpeedDeg = TurnSpeedDeg;
        private float _gravity = Gravity;
        private float _jumpVelocity = JumpVelocity;
        private float _slideDuration = SlideDuration;
        private float _slideStartSpeed = SlideStartSpeed;
        private float _slideEndSpeed = SlideEndSpeed;
        private float _sprintSlideBoost = SprintSlideBoost;
        private float _arenaHalfExtent = ArenaHalfExtent;
        private float _collisionRadius = CollisionRadius;

        public float VerticalSpeed => _verticalSpeed;
        public bool IsGrounded => _isGrounded;
        public float SpeedBlend => _speedBlend;
        public bool IsSliding => _isSliding;
        public int SlideTicksRemaining => _slideTicksRemaining;

        public void ApplyRules(RuleProfile profile)
        {
            if (profile == null) throw new System.ArgumentNullException(nameof(profile));
            _jogSpeed = profile.movement.jogSpeed;
            _sprintSpeed = profile.movement.sprintSpeed;
            _aimMoveSpeed = profile.movement.aimMoveSpeed;
            _turnSpeedDeg = profile.movement.rotationSpeedDeg;
            _gravity = profile.movement.gravity;
            _jumpVelocity = profile.movement.jumpVelocity;
            _slideDuration = profile.movement.slideDurationSeconds;
            _slideStartSpeed = profile.movement.slideStartSpeed;
            _slideEndSpeed = profile.movement.slideEndSpeed;
            _sprintSlideBoost = profile.movement.sprintSlideBoost;
            _arenaHalfExtent = profile.spawn.arenaHalfExtent;
            _collisionRadius = profile.spawn.collisionRadius;
        }

        /// <summary>比赛结束后的本地视觉回放，不再参与权威模拟。</summary>
        public void BeginSettlementPlayback(float duration, float scale)
        {
            _settlementPlayback = true;
            _settlementElapsed = 0f;
            _settlementDuration = Mathf.Max(0.1f, duration);
            _settlementScale = Mathf.Clamp(scale, 0.05f, 1f);

            Vector3 direction = _isSliding ? _slideDirection : worldMove;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                direction.Normalize();
            else
                direction = Vector3.zero;

            float speed = _isSliding ? _slideEndSpeed : isSprint ? _sprintSpeed : (isAiming || isFire ? _aimMoveSpeed : _jogSpeed);
            _settlementVelocity = direction * speed;
            _isSliding = false;
            _slideTicksRemaining = 0;
            if (_animator != null) _animator.speed = _settlementScale;
        }

        /// <summary>由 NetClient 用最近收到的远端权威位置更新动态碰撞参考。</summary>
        public void SetDynamicCollisionPositions(IReadOnlyList<Vector3> positions)
        {
            _dynamicCollisionPositions.Clear();
            if (positions == null) return;
            for (int i = 0; i < positions.Count; i++) _dynamicCollisionPositions.Add(positions[i]);
        }

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
                return Mathf.MoveTowardsAngle(bodyYawDeg, slideYaw, _turnSpeedDeg * dt);
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
                return Mathf.MoveTowardsAngle(bodyYawDeg, targetYaw, _turnSpeedDeg * dt);
        }

        /// <summary>执行一次与服务器相同的固定步进；也用于快照后的未确认输入重放。</summary>
        public void SimulateTick(PlayerInputMessage input, float dt, int serverTickRate)
        {
            if (_model == null || _model.isDead || input == null) return;

            bodyYawDeg = input.bodyYawDeg;
            bool aiming = input.isAiming || input.isFire;

            if (_isGrounded && input.isJumping)
            {
                _verticalSpeed = _jumpVelocity;
                _isGrounded = false;
                _isSliding = false;
                _slideTicksRemaining = 0;
            }

            if (!_isSliding && _isGrounded && input.isSlide)
            {
                _isSliding = true;
                _slideTicksRemaining = Mathf.Max(1, Mathf.RoundToInt(_slideDuration * serverTickRate));
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
                ? input.isSprint ? _sprintSpeed : aiming ? _aimMoveSpeed : _jogSpeed
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
                _verticalSpeed += _gravity * dt;
                newPos.y = transform.position.y + _verticalSpeed * dt;
            }

            newPos = PvpCollisionWorld.ResolveMovement(transform.position, newPos, _collisionRadius, _arenaHalfExtent, _groundY);
            newPos = PvpCollisionWorld.ResolveAgainstPlayers(newPos, _dynamicCollisionPositions, _collisionRadius, _arenaHalfExtent, _groundY);
            if (_verticalSpeed <= 0f && PvpCollisionWorld.TryGetWalkableHeight(newPos.x, newPos.z,
                    newPos.y - _groundY, WalkableStepHeight, out float walkableY))
            {
                newPos.y = walkableY + _groundY;
                _verticalSpeed = 0f;
                _isGrounded = true;
            }
            else if (newPos.y <= _groundY)
            {
                newPos.y = _groundY;
                _verticalSpeed = 0f;
                _isGrounded = true;
            }
            else _isGrounded = false;
            transform.position = newPos;
            transform.rotation = Quaternion.Euler(0f, bodyYawDeg, 0f);
        }

        private void StepSlide(float dt, int serverTickRate)
        {
            int totalTicks = Mathf.Max(1, Mathf.RoundToInt(_slideDuration * serverTickRate));
            float t = _slideTicksRemaining / (float)totalTicks;
            float speed = Mathf.Lerp(_slideEndSpeed, _slideStartSpeed, t);
            if (_slideSprintBoost) speed += _sprintSlideBoost * t;

            Vector3 newPos = transform.position + _slideDirection * (speed * dt);
            newPos.y = transform.position.y;
            newPos = PvpCollisionWorld.ResolveMovement(transform.position, newPos, _collisionRadius, _arenaHalfExtent, _groundY);
            newPos = PvpCollisionWorld.ResolveAgainstPlayers(newPos, _dynamicCollisionPositions, _collisionRadius, _arenaHalfExtent, _groundY);
            if (PvpCollisionWorld.TryGetWalkableHeight(newPos.x, newPos.z, newPos.y - _groundY,
                    WalkableStepHeight, out float walkableY))
                newPos.y = walkableY + _groundY;
            transform.position = newPos;
            _verticalSpeed = 0f;
            _isGrounded = true;
            _slideTicksRemaining--;
            if (_slideTicksRemaining <= 0) _isSliding = false;
        }

        private void Update()
        {
            if (_settlementPlayback)
            {
                UpdateSettlementPlayback();
                if (_model == null || _model.isDead) return;
            }
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

            // 站立瞄准仍保持瞄准 IK，但不要进入八向移动动画；只有实际有水平输入时才切到 Aiming locomotion。
            int desired = _isSliding ? 4 : !_isGrounded ? 3 : aiming && moving ? 2 : moving ? 1 : 0;
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
                _animator.SetFloat(PlayerModel.AimingXHash, moving ? moveInput.x : 0f);
                _animator.SetFloat(PlayerModel.AimingYHash, moving ? moveInput.y : 0f);
            }
        }

        private void UpdateSettlementPlayback()
        {
            float dt = Time.unscaledDeltaTime;
            _settlementElapsed += dt;
            float t = Mathf.Clamp01(_settlementElapsed / _settlementDuration);
            float damping = 1f - Mathf.SmoothStep(0f, 1f, t);
            Vector3 next = transform.position + _settlementVelocity * (dt * _settlementScale * damping);
            next = PvpCollisionWorld.ResolveMovement(transform.position, next, _collisionRadius, _arenaHalfExtent, _groundY);
            transform.position = next;

            if (_settlementVelocity.sqrMagnitude > 0.0001f)
                bodyYawDeg = Mathf.Atan2(_settlementVelocity.x, _settlementVelocity.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, bodyYawDeg, 0f);

            if (_settlementElapsed >= _settlementDuration)
            {
                _settlementPlayback = false;
                _settlementVelocity = Vector3.zero;
                worldMove = Vector3.zero;
                isAiming = false;
                isFire = false;
                if (_animator != null) _animator.speed = 1f;
            }
        }
    }
}
