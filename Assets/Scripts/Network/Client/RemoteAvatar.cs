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

        private readonly List<BufferedState> _buffer = new();
        private Animator _animator;
        private PlayerModel _model;
        private PlayerHealthBar _healthBar;
        private int _lastServerTick;
        private int _animState = -1;   // 0=Idle 1=Move 2=Aiming 3=Hover

        public int PlayerId { get; private set; }
        public bool IsDead { get; private set; }

        public void Setup(int playerId, GameObject characterPrefab, Vector3 spawnPos, float yawDeg)
        {
            PlayerId = playerId;
            transform.position = spawnPos;
            // 角色作为子物体：插值移动的是本容器，角色随之移动（动画不产生根位移）
            var go = Instantiate(characterPrefab, Vector3.zero, Quaternion.Euler(0f, yawDeg, 0f), transform);
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

            while (_buffer.Count > 8) _buffer.RemoveAt(0);

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
        }

        public void ApplyHealth(int health, int maxHealth)
        {
            if (_healthBar != null) _healthBar.SetHealth((float)health / maxHealth);
        }

        private void LateUpdate()
        {
            if (_buffer.Count < 2) return;

            // renderTick：最新 tick - 插值延迟
            float delayTicks = interpolationDelay * 30f;
            float renderTick = _lastServerTick - delayTicks;

            // 找 straddle 两帧
            int idx = -1;
            for (int i = 0; i < _buffer.Count - 1; i++)
            {
                if (_buffer[i].serverTick <= renderTick && _buffer[i + 1].serverTick > renderTick)
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0) idx = _buffer.Count - 2;

            var a = _buffer[idx];
            var b = _buffer[idx + 1];
            float t = Mathf.Clamp01((renderTick - a.serverTick) / Mathf.Max(1, b.serverTick - a.serverTick));

            transform.position = Vector3.Lerp(a.pos, b.pos, t);
            transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(a.yaw, b.yaw, t), 0f);

            if (_animator != null)
            {
                float blend = Mathf.Lerp(a.speedBlend, b.speedBlend, t);
                _animator.SetFloat(PlayerModel.SpeedHash, blend);
                _animator.SetBool(PlayerModel.IsGroundedHash, a.isGrounded);
                _animator.SetFloat(PlayerModel.VerticalSpeedHash, Mathf.Lerp(a.verticalSpeed, b.verticalSpeed, t));

                // 状态切换（与 PVE 一致：0 idle 1 move 2 sprint 3 aim 4 hover），否则远端只在 Idle 平移
                if (!IsDead)
                {
                    int desired;
                    switch (a.moveState)
                    {
                        case 1:
                        case 2: desired = 1; break;   // Move（Sprint 靠 Speed→1 走 Dash 段）
                        case 3: desired = 2; break;   // Aiming
                        case 4: desired = 3; break;   // Hover
                        default: desired = 0; break;  // Idle
                    }
                    if (desired != _animState)
                    {
                        _animState = desired;
                        string stateName = desired == 3 ? "Hover" : desired == 2 ? "Aiming" : desired == 1 ? "Move" : "Idle";
                        _animator.CrossFadeInFixedTime(stateName, 0.25f);
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
