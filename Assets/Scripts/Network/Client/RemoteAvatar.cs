using System.Collections.Generic;
using UnityEngine;

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

        [Tooltip("插值延迟（秒），落后最新快照这么多再播放")]
        public float interpolationDelay = 0.1f;

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

        public int PlayerId { get; private set; }
        public bool IsDead { get; private set; }

        public void Setup(int playerId, GameObject characterPrefab, Vector3 spawnPos, float yawDeg, float tickRate)
        {
            PlayerId = playerId;
            _tickRate = tickRate;
            transform.position = spawnPos;
            // 角色作为子物体：插值移动的是本容器，角色随之移动（动画不产生根位移）。
            // ⚠️ 子物体必须 Quaternion.identity：容器 rotation 每帧被 LateUpdate 设为插值 yaw，
            //    若子物体再带出生 yaw → 双重旋转（总朝向 = 出生yaw + 插值yaw），远端永远背错方向。
            var go = Instantiate(characterPrefab, Vector3.zero, Quaternion.identity, transform);
            go.name = $"Remote_{playerId}";
            // 复用角色预制体，但禁用状态机（插值直接驱动 transform）
            _model = go.GetComponent<PlayerModel>();
            if (_model != null)
            {
                _model.disableStateMachine = true;
                if (_model.navMeshAgent != null) _model.navMeshAgent.enabled = false;
            }
            _animator = _model != null ? _model.animator : go.GetComponentInChildren<Animator>();
            _healthBar = _model != null ? _model.playerHealthBar : null;
            if (_healthBar != null) _healthBar.alwaysShowHealthBar = true;
        }

        public void ApplyPlayerState(PlayerSnapshotMessage s, int snapServerTick)
        {
            _lastServerTick = snapServerTick;
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
            if (_animator != null)
            {
                string deadClip = _model != null && !string.IsNullOrEmpty(_model.deadAnimationName)
                    ? _model.deadAnimationName : "Dead";
                _animator.CrossFadeInFixedTime(deadClip, 0.1f);
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

        private void LateUpdate()
        {
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
                        case 3: desired = 2; break;   // Aiming
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

        public void Teardown()
        {
            if (_model != null) Destroy(_model.gameObject);
            Destroy(gameObject);
        }
    }
}
