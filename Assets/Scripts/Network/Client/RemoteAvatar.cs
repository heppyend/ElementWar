using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.AI;

namespace ElementWar.Net
{
    /// <summary>
    /// 远端玩家表现：缓冲服务器快照，落后一小段插值播放（避免一格一跳），
    /// 驱动 Animator 参数 + 受击/死亡/重生表现。由 NetClient 创建/销毁。
    /// </summary>
    public class RemoteAvatar : MonoBehaviour
    {
        private struct BufferedState
        {
            public int serverTick;
            public Vector3 pos;
            public float yaw;
            public float speedBlend;
            public float verticalSpeed;
            public bool isGrounded;
            public Vector3 aim;
            public int moveState;
        }

        [Tooltip("远端渲染落后服务器的时间；PVP 默认 50ms，降低冲刺/滑铲可见延迟")]
        public float interpolationDelay = 0.05f;

        private const int BufferCapacity = 16;   // 60Hz 下 ≈0.27s 缓冲（30Hz 时代 8 个不够）

        private readonly List<BufferedState> _buffer = new();
        private Animator _animator;
        private PlayerModel _model;
        private PlayerHealthBar _healthBar;
        private int _lastServerTick;
        private float _tickRate = 30f;    // 服务器 tick 率（Setup 传入，不硬编码 30）
        private float _renderTickF;       // 渲染游标（tick 浮点）：按真实时间推进，避免快照成批到达时逐帧跳 tick → 远端瞬移
        private bool _renderInited;
        private int _animState = -1;   // 0=Idle 1=Move 2=Aiming 3=Hover
        private Vector3 _lastRenderPos;   // 用于推瞄准混合（AimingX/Y）的字符局部移动方向
        private bool _hasLastRenderPos;
        private Transform _aimTarget;
        private MultiAimConstraint[] _aimConstraints;
        private TwoBoneIKConstraint _hipIK;
        private float _aimWeight;
        private bool _settlementPlayback;
        private float _settlementElapsed;
        private float _settlementDuration;
        private float _settlementScale;
        private Vector3 _settlementVelocity;

        public int PlayerId { get; private set; }
        public bool IsDead { get; private set; }
        public Vector3 LatestAuthoritativePosition { get; private set; }

        /// <summary>比赛结束后的本地视觉回放：从最后两帧估算速度并逐渐减速。</summary>
        public void BeginSettlementPlayback(float duration, float scale)
        {
            _settlementPlayback = true;
            _settlementElapsed = 0f;
            _settlementDuration = Mathf.Max(0.1f, duration);
            _settlementScale = Mathf.Clamp(scale, 0.05f, 1f);
            _settlementVelocity = Vector3.zero;
            if (_buffer.Count >= 2)
            {
                var a = _buffer[_buffer.Count - 2];
                var b = _buffer[_buffer.Count - 1];
                float dt = Mathf.Max(0.001f, (b.serverTick - a.serverTick) / Mathf.Max(1f, _tickRate));
                _settlementVelocity = (b.pos - a.pos) / dt;
                _settlementVelocity.y = 0f;
            }
            if (_animator != null) _animator.speed = _settlementScale;
        }

        public void Setup(int playerId, GameObject characterPrefab, Vector3 spawnPos, float yawDeg, float tickRate)
        {
            PlayerId = playerId;
            _tickRate = tickRate;
            transform.position = spawnPos;
            // 角色作为子物体：插值移动的是本容器，角色随之移动（动画不产生根位移）。
            // ⚠️ 子物体必须 Quaternion.identity：容器 rotation 每帧被 LateUpdate 设为插值 yaw，
            //    若子物体再带出生 yaw → 双重旋转（总朝向 = 出生yaw + 插值yaw），远端永远背错方向。
            // 在失活容器中先关闭 prefab 自带 NavMeshAgent，避免 PVP 无 NavMesh 时 Agent OnEnable 报错。
            var staging = new GameObject("PVP_RemoteSpawnStaging");
            staging.SetActive(false);
            staging.transform.SetParent(transform, false);
            var go = Instantiate(characterPrefab, staging.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.name = $"Remote_{playerId}";
            // 复用角色预制体，但禁用状态机（插值直接驱动 transform）
            _model = go.GetComponent<PlayerModel>();
            if (_model != null)
            {
                _model.disableStateMachine = true;
            }
            foreach (var agent in go.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
            go.transform.SetParent(transform, false);
            go.SetActive(true);
            Destroy(staging);
            _animator = _model != null ? _model.animator : go.GetComponentInChildren<Animator>();
            _healthBar = _model != null ? _model.playerHealthBar : null;
            if (_healthBar != null) _healthBar.alwaysShowHealthBar = true;
            WireAimTarget(go);
        }

        public void ApplyPlayerState(PlayerSnapshotMessage s, int snapServerTick)
        {
            _lastServerTick = Mathf.Max(_lastServerTick, snapServerTick);
            var st = new BufferedState
            {
                serverTick = snapServerTick,
                pos = new Vector3(s.x, s.y, s.z),
                yaw = s.bodyYawDeg,
                speedBlend = s.speedBlend,
                verticalSpeed = s.verticalSpeed,
                isGrounded = s.isGrounded,
                aim = new Vector3(s.aimX, s.aimY, s.aimZ),
                moveState = s.moveState,
            };
            LatestAuthoritativePosition = st.pos;

            // 按 tick 排序插入（去重同 tick）
            int i = _buffer.Count - 1;
            while (i >= 0 && _buffer[i].serverTick > snapServerTick) i--;
            if (i >= 0 && _buffer[i].serverTick == snapServerTick)
                _buffer[i] = st;
            else
                _buffer.Insert(i + 1, st);

            while (_buffer.Count > BufferCapacity) _buffer.RemoveAt(0);

            // 渲染游标初始化：首个数据到来时对齐到缓冲开头，之后 LateUpdate 按真实时间推进
            if (!_renderInited)
            {
                _renderTickF = _buffer[0].serverTick;
                _renderInited = true;
            }

            if (!IsDead && !s.isAlive) ApplyDeath();
        }

        public void ApplyDeath()
        {
            IsDead = true;
            // 角色当前 Animator 没有统一的 Dead 状态时，不强行 CrossFade 到不存在的状态，
            // 否则每次死亡事件都会产生 Animator.GotoState 警告并扰乱表现层。
            if (_animator != null && _model != null && !string.IsNullOrEmpty(_model.deadAnimationName))
            {
                _animator.CrossFadeInFixedTime(_model.deadAnimationName, 0.1f);
            }
            // 简单死亡表现：躺下（禁用移动，让动画播放）
        }

        public void ApplyRespawn(ReliableEventMessage e)
        {
            IsDead = false;
            transform.position = new Vector3(e.x, e.y, e.z);
            _buffer.Clear();
            _renderInited = false;   // 重生：重新对齐渲染游标到新缓冲开头
        }

        public void ApplyHealth(int health, int maxHealth)
        {
            if (_healthBar != null) _healthBar.SetHealth((float)health / maxHealth);
        }

        public void PlayFire(Vector3 target)
        {
            if (_model != null && _model.weapon != null)
                _model.weapon.PlayVisualFire(target);
        }

        public void ApplyAmmo(int magazineAmmo, int reserveAmmo, bool isReloading)
        {
            if (_model != null && _model.weapon != null)
                _model.weapon.ApplyAuthoritativeAmmo(magazineAmmo, reserveAmmo, isReloading);
        }

        public void ApplyHit(Vector3 hitPoint)
        {
            if (_model == null || _model.weapon == null) return;
            var bulletPrefab = _model.weapon.bulletEffectPrefab;
            if (bulletPrefab != null && bulletPrefab.impactPrefab != null)
                EffectPool.INSTANCE.GetEffect(bulletPrefab.impactPrefab, hitPoint, Quaternion.identity);
        }

        private void WireAimTarget(GameObject avatar)
        {
            if (_model != null && _model.animator != null)
                _model.animator.applyRootMotion = true;
            _aimConstraints = avatar.GetComponentsInChildren<MultiAimConstraint>(true);
            _hipIK = avatar.GetComponentInChildren<TwoBoneIKConstraint>(true);
            if (_aimConstraints.Length == 0) return;

            var targetObject = new GameObject("AimTarget_PVP_Remote");
            targetObject.transform.SetParent(avatar.transform, false);
            targetObject.transform.localPosition = new Vector3(0f, 1.5f, 5f);
            _aimTarget = targetObject.transform;
            foreach (var constraint in _aimConstraints)
            {
                ref var data = ref constraint.data;
                var sources = data.sourceObjects;
                if (sources.Count > 0)
                {
                    sources.SetTransform(0, _aimTarget);
                    data.sourceObjects = sources;
                }
                constraint.weight = 0f;
            }
            if (_hipIK != null) _hipIK.weight = 1f;
            var rig = avatar.GetComponentInChildren<RigBuilder>(true);
            if (rig != null) { rig.Clear(); rig.Build(); }
        }

        private void LateUpdate()
        {
            if (_settlementPlayback)
            {
                UpdateSettlementPlayback();
                return;
            }
            if (_buffer.Count < 2 || !_renderInited) return;

            // 渲染游标按真实时间推进（一帧只走 Time.deltaTime*tickRate 个 tick）。
            // ⚠️ 不能再用"最新快照 tick - 延迟"当 renderTick：高帧率下每帧会吞 2-4 个快照，
            //    renderTick 一帧跳多 tick → 远端位置一帧瞬移近 1m（冲刺）= 用户看到的"飞来飞去"。
            _renderTickF += Time.deltaTime * _tickRate;
            // 上限：不能超前于 最新快照-插值延迟（保证缓冲里始终有可插值的未来帧）。
            // ⚠️ 不要加"下限钳制到 _buffer[0]"：高帧率下每帧到达多个快照，_buffer[0] 每帧前进多 tick，
            //    而 _renderTickF 按真实时间每帧只前进一点点 → 每帧被拽回缓冲最旧端 → 渲染钉在 16 tick 后（0.27s），
            //    移动中落后 1-2m、停下时猛追 = "位置不同步 + 飞来飞去"（08-21 build 日志实证）。
            //    重生清空缓冲由 ApplyRespawn 置 _renderInited=false 重新对齐处理，不需要这个下限。
            float maxRender = _lastServerTick - interpolationDelay * _tickRate;
            if (_renderTickF > maxRender) _renderTickF = maxRender;

            // 找 straddle 两帧
            int idx = -1;
            for (int i = 0; i < _buffer.Count - 1; i++)
            {
                if (_buffer[i].serverTick <= _renderTickF && _buffer[i + 1].serverTick > _renderTickF)
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0) idx = _buffer.Count - 2;

            var a = _buffer[idx];
            var b = _buffer[idx + 1];
            float t = Mathf.Clamp01((_renderTickF - a.serverTick) / Mathf.Max(1, b.serverTick - a.serverTick));

            transform.position = Vector3.Lerp(a.pos, b.pos, t);
            transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(a.yaw, b.yaw, t), 0f);

            if (_animator != null && _buffer.Count > 0)
            {
                // ⚠️ 动画用【最新快照】状态驱动，不用渲染游标 a：渲染游标滞后 0.1s + 切换 0.25s
                //    = 角色开始移动后动画还停 Idle → 站姿被容器拖着滑 = "乱跑/飞来飞去"（08-21 日志实证
                //    ms=1 但 anim=Idle/0.66 大量出现）。用最新状态：动画立即反应移动，
                //    位置照常 0.1s 插值，角色先跑起来、位移随后追上。
                var latest = _buffer[_buffer.Count - 1];
                if (_aimTarget != null) _aimTarget.position = latest.aim;
                bool aiming = latest.moveState == 3;
                _aimWeight = Mathf.MoveTowards(_aimWeight, aiming ? 1f : 0f, 8f * Time.deltaTime);
                if (_aimConstraints != null)
                    foreach (var constraint in _aimConstraints) constraint.weight = _aimWeight;
                if (_hipIK != null) _hipIK.weight = 1f - _aimWeight;
                float blend = latest.speedBlend;
                _animator.SetFloat(PlayerModel.SpeedHash, blend);
                _animator.SetBool(PlayerModel.IsGroundedHash, latest.isGrounded);
                _animator.SetBool(PlayerModel.IsSprintingHash, blend > 0.75f);
                _animator.SetFloat(PlayerModel.VerticalSpeedHash, latest.verticalSpeed);

                // 瞄准混合参数（AimingX/Y）：远端没有相机相对输入，从插值位移推字符局部移动方向。
                // ⚠️ 不设的话 Aiming 状态（AimingBlend 树靠 AimingX/Y 驱动）停在 0/0
                //   → 静态瞄准姿势 + 容器平移 = 用户看到的"敌人平移/滑行"
                Vector3 mv = new Vector3(transform.position.x - _lastRenderPos.x, 0f,
                    transform.position.z - _lastRenderPos.z);
                if (_hasLastRenderPos && mv.sqrMagnitude > 1e-6f)
                {
                    mv.Normalize();
                    Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
                    Vector3 right = transform.right; right.y = 0f; right.Normalize();
                    _animator.SetFloat(PlayerModel.AimingXHash, Vector3.Dot(mv, right));
                    _animator.SetFloat(PlayerModel.AimingYHash, Vector3.Dot(mv, fwd));
                }
                else
                {
                    // 远端静止瞄准时清零八向混合参数，避免沿用上一帧移动方向，
                    // 导致对方看到角色腿部在原地循环走动。
                    _animator.SetFloat(PlayerModel.AimingXHash, 0f);
                    _animator.SetFloat(PlayerModel.AimingYHash, 0f);
                }
                _lastRenderPos = transform.position;
                _hasLastRenderPos = true;

                // 状态切换（与 PVE 一致：0 idle 1 move 2 sprint 3 aim 4 hover 5 slide），否则远端只在 Idle 平移
                if (!IsDead)
                {
                    int desired;
                    switch (latest.moveState)
                    {
                        case 1:
                        case 2: desired = 1; break;   // Move（Sprint 靠 Speed→1 走 Dash 段）
                        case 3: desired = latest.speedBlend > 0.01f ? 2 : 0; break; // 静止瞄准保持待机，IK仍单独生效
                        case 4: desired = 3; break;   // Hover
                        case 5: desired = 4; break;   // Slide
                        default: desired = 0; break;  // Idle
                    }
                    if (desired != _animState)
                    {
                        _animState = desired;
                        string stateName = desired == 4 ? "RunningSlide"
                            : desired == 3 ? "Hover"
                            : desired == 2 ? "Aiming"
                            : desired == 1 ? "Move"
                            : "Idle";
                        _animator.CrossFadeInFixedTime(stateName, 0.15f);
                    }
                }
            }
        }

        private void UpdateSettlementPlayback()
        {
            float dt = Time.unscaledDeltaTime;
            _settlementElapsed += dt;
            float t = Mathf.Clamp01(_settlementElapsed / _settlementDuration);
            float damping = 1f - Mathf.SmoothStep(0f, 1f, t);
            Vector3 next = transform.position + _settlementVelocity * (dt * _settlementScale * damping);
            next = PvpCollisionWorld.ResolveMovement(transform.position, next, PvPMotor.CollisionRadius,
                PvPMotor.ArenaHalfExtent, PvPMotor.GroundY);
            transform.position = next;

            if (_settlementElapsed >= _settlementDuration)
            {
                _settlementPlayback = false;
                _settlementVelocity = Vector3.zero;
                if (_animator != null) _animator.speed = 1f;
            }
        }

        public void Teardown()
        {
            if (_model != null) Destroy(_model.gameObject);
            Destroy(gameObject);
        }
    }
}
