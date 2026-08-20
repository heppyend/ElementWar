using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

namespace ElementWar.Net
{
    /// <summary>
    /// 客户端网络中枢：连接握手 → 按 tick 上报输入 → 收快照（本地预测校正 + 远端插值）
    /// → 可靠事件（死亡/重生/击杀/对局结束）ACK 去重 → 开火请求。
    /// 架构参考 CalabiYau UdpNetworkClient，传输用 UDP+JSON。
    /// </summary>
    public class NetClient : MonoBehaviour
    {
        [Header("连接")]
        [SerializeField] string serverAddress = "127.0.0.1";
        [SerializeField] int serverPort = 7777;
        [SerializeField] string playerName = "Player";
        [SerializeField] int characterId;

        [Header("生成")]
        public PlayerSpawner spawner;

        [Header("预测校正")]
        [SerializeField] float predictionDeadZone = 0.15f;
        [SerializeField] float hardCorrectionDistance = 1.25f;
        [SerializeField] float smoothCorrectionSpeed = 5f;

        // ---- 运行时状态 ----
        private UdpSocket _socket;
        private bool _connected;
        private int _playerId;
        private int _serverTickRate = 30;
        private int _winScore = 5;
        private int _inputTick;
        private int _lastServerTick;   // 最近快照的服务器 tick（inputTick 对齐用，防漂移被服务器拒绝）
        private int _fireSequence;
        private float _inputAccumulator;
        private float _lastRttSampleTime;
        private float _estimatedRtt;
        private float _fireRequestInterval = 0.15f;   // 客户端开火限速（与 PVE bulletInterval 一致；服务器权威冷却需同步 GameWorldSettings）
        private float _lastFireRequestTime;
        private PVPCameraRig _camRig;                  // 相机震动（懒查找缓存）
        private float _jumpHoldUntil;   // 跳跃锁存截止时间（时间制，跨帧率可靠；撑到首个 30Hz 帧携带）
        private float _slideHoldUntil;  // 滑铲锁存截止时间（同上）

        private MyInputSystem _input;
        private Camera _mainCamera;
        private PlayerModel _localModel;
        private PvPMotor _localMotor;
        private readonly Dictionary<int, RemoteAvatar> _remotes = new();
        private readonly HashSet<long> _recentEventIds = new();
        private readonly Dictionary<int, Vector3> _predictionHistory = new(); // inputTick → 预测位置
        private Vector3 _lastReconcilePos;
        private Transform _localAimTarget;   // PVP 瞄准 IK 目标（预制体约束源是空的，运行时补一个接准心）

        // 输入采样（每帧更新，供 PvPMotor 与上报共用）
        private Vector2 _moveInput;
        private bool _sprint, _aiming, _fire, _jumping, _slide;
        private Vector3 _worldMove;
        private Vector3 _aimPoint = new Vector3(0f, 1.5f, 10f);

        public int PlayerId => _playerId;
        public bool Connected => _connected;
        public PlayerModel LocalModel => _localModel;
        public PvPMotor LocalMotor => _localMotor;
        public int LocalHealth => _localModel != null ? _localModel.currentHealth : 0;

        /// <summary>计分板：playerId → score（由快照更新，PVPHealthUI 读取）。</summary>
        public readonly Dictionary<int, int> Scores = new();
        /// <summary>机器人玩家 id 集合（由快照 isBot 维护，HUD 显示用）。</summary>
        public readonly HashSet<int> BotIds = new();
        /// <summary>本地玩家死亡（UI 弹重生提示）。</summary>
        public event Action OnLocalDied;
        /// <summary>对局结束（UI 弹结束面板），参数为胜者 playerId。</summary>
        public event Action<int> OnMatchEnded;

        private void Awake()
        {
            _input = new MyInputSystem();
            _mainCamera = Camera.main;
            // 兜底：确保左上角信息窗存在（NetworkLauncher 可能因组件缺失提前 return 没创建）
            if (FindObjectOfType<DebugInfoWindow>() == null)
                new GameObject("DebugInfoWindow").AddComponent<DebugInfoWindow>();
        }

        private void OnEnable() { _input.Enable(); }
        private void OnDisable() { _input?.Disable(); }
        private void OnDestroy() { _socket?.Dispose(); }

        /// <summary>配置连接参数（由 NetworkLauncher 从 NetLobbyConfig 注入）。</summary>
        public void Configure(string ip, int port, string name, int charId)
        {
            serverAddress = ip;
            serverPort = port;
            playerName = name;
            characterId = charId;
        }

        /// <summary>建立连接（由 NetworkLauncher 调用）。</summary>
        public void Connect()
        {
            try
            {
                _socket = new UdpSocket(serverAddress, serverPort);
                _socket.SendJson(JsonUtility.ToJson(new ClientHelloMessage
                {
                    playerName = playerName,
                    characterId = characterId,
                }));
                Debug.Log($"[NetClient] 连接 {serverAddress}:{serverPort} 等待 welcome...");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetClient] 连接失败：{e.Message}");
            }
        }

        private void Update()
        {
            if (_socket == null) return;
            DrainSocket();

            if (!_connected) return;

            SampleInput();
            if (_localMotor != null) PushInputToMotor();
            if (_localAimTarget != null) _localAimTarget.position = _aimPoint; // 瞄准 IK 目标跟准心

            // 按 30Hz 上报输入
            // ⚠️ 不做跳跃/滑铲即时上报：即时上报每次多 +1 inputTick → 玩一阵就漂移超前>30 被服务器拒收 → 回弹/抖动。
            // 瞬发触发靠时间制锁存（SampleInput 里 hold 0.12s）撑到下一个 30Hz 帧携带，延迟 ≤33ms 可忽略。
            _inputAccumulator += Time.deltaTime;
            if (_inputAccumulator >= 1f / _serverTickRate)
            {
                _inputAccumulator = 0f;
                SendInput();
            }

            // 开火：按下触发 FireRequest（服务器权威判定）
            if (_fire && _localModel != null && !_localModel.isDead)
            {
                SendFire();
            }
        }

        // ---------------- 输入采样 ----------------

        private void SampleInput()
        {
            _moveInput = _input.Player.Move.ReadValue<Vector2>();
            _moveInput = Vector2.ClampMagnitude(_moveInput, 1f);
            _sprint = _input.Player.IsSprint.IsPressed();
            _aiming = _input.Player.IsAiming.IsPressed();
            _fire = _input.Player.Fire.IsPressed();
            if (_input.Player.IsJumping.triggered) _jumpHoldUntil = Time.time + 0.12f; // 锁存 0.12s（=3 个 30Hz tick，时间制跨帧率可靠）
            _jumping = _jumpHoldUntil > Time.time;
            if (_input.Player.IsSlide.triggered) _slideHoldUntil = Time.time + 0.12f;
            _slide = _slideHoldUntil > Time.time;

            // 相机相对 → 世界移动方向（与 PVE PlayerController 一致）
            Vector3 camF = _mainCamera != null ? _mainCamera.transform.forward : Vector3.forward;
            Vector3 camR = _mainCamera != null ? _mainCamera.transform.right : Vector3.right;
            camF.y = 0f; camR.y = 0f;
            camF.Normalize(); camR.Normalize();
            _worldMove = (camF * _moveInput.y + camR * _moveInput.x);
            if (_worldMove.sqrMagnitude > 1f) _worldMove.Normalize();

            // 世界瞄准点：相机中心射线
            // ⚠️ 必须跳过本地玩家自身碰撞体：否则射线命中自己 → 瞄准点在角色背部（朝相机侧）
            // → 角色永远面向玩家 + 开火射线向自己身后打 → 打不中敌人
            if (_mainCamera != null)
            {
                Ray ray = _mainCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                {
                    if (_localModel != null && hit.collider != null && hit.collider.transform.root == _localModel.transform)
                    {
                        // 命中自身：从命中点再往前射，取真正对准的目标
                        Vector3 origin2 = hit.point + ray.direction * 0.5f;
                        if (Physics.Raycast(origin2, ray.direction, out RaycastHit hit2, 500f))
                            _aimPoint = hit2.point;
                        else
                            _aimPoint = ray.origin + ray.direction * 300f;
                    }
                    else
                    {
                        _aimPoint = hit.point;
                    }
                }
                else
                {
                    _aimPoint = ray.origin + ray.direction * 300f;
                }
            }
        }

        private void PushInputToMotor()
        {
            _localMotor.moveInput = _moveInput;
            _localMotor.worldMove = _worldMove;
            _localMotor.aimPoint = _aimPoint;
            _localMotor.isSprint = _sprint;
            _localMotor.isAiming = _aiming;
            _localMotor.isJumping = _jumping;
            _localMotor.isSlide = _slide;
            _localMotor.isFire = _fire;
        }

        // ---------------- 发送 ----------------

        private void SendInput()
        {
            // inputTick 下限跟随服务器快照（lastServerTick+2 前瞻）：防"落后漂移"（低帧率/卡顿导致
            // inputTick 增速 < 服务器 tick 增速 → 被 ServerTick-120 判太旧拒收）。
            // 超前漂移源（跳跃/滑铲即时上报）已在 Update 删除，靠时间锁存只让 30Hz 帧携带一次。
            _inputTick = Math.Max(_inputTick + 1, _lastServerTick + 2);
            var msg = new PlayerInputMessage
            {
                playerId = _playerId,
                inputTick = _inputTick,
                moveX = _moveInput.x,
                moveY = _moveInput.y,
                isSprint = _sprint,
                isAiming = _aiming,
                isFire = _fire,
                isJumping = _jumping,
                isSlide = _slide,
                worldMoveX = _worldMove.x,
                worldMoveY = _worldMove.y,
                worldMoveZ = _worldMove.z,
                bodyYawDeg = _localMotor != null ? _localMotor.bodyYawDeg : 0f,
                aimX = _aimPoint.x,
                aimY = _aimPoint.y,
                aimZ = _aimPoint.z,
                estimatedRttSeconds = _estimatedRtt,
                interpolationDelaySeconds = 0.1f,
            };
            _socket.SendJson(JsonUtility.ToJson(msg));

            // 触发状态只让首个 30Hz 帧携带一次，发完立即清锁存——
            // 否则持续多帧 isJumping=true 会让服务器 JumpQueued 反复锁存 → 落地瞬间自动跳
            _jumpHoldUntil = 0f;
            _slideHoldUntil = 0f;

            // 记录预测位置用于校正
            if (_localModel != null)
                _predictionHistory[_inputTick] = _localModel.transform.position;
            TrimPredictionHistory();
        }

        private void SendFire()
        {
            // 客户端射速限制：与 PVE PlayerWeapon.bulletInterval 一致（0.15s）——
            // 否则按住开火每帧都发 FireRequest + 刷枪口/音效，视觉和带宽都爆
            if (Time.time - _lastFireRequestTime < _fireRequestInterval) return;
            _lastFireRequestTime = Time.time;

            _fireSequence++;
            var msg = new FireRequestMessage
            {
                playerId = _playerId,
                fireSequence = _fireSequence,
                requestTick = _inputTick,
                aimX = _aimPoint.x,
                aimY = _aimPoint.y,
                aimZ = _aimPoint.z,
                estimatedRttSeconds = _estimatedRtt,
                interpolationDelaySeconds = 0.1f,
            };
            _socket.SendJson(JsonUtility.ToJson(msg));

            // 客户端视觉开火：完整复制 PVE——weapon.Fire 走子弹对象池（Rigidbody 弹道 + 命中特效）
            // + 枪口火花（EffectPool）+ Fired 事件触发 WeaponAudio 枪声。伤害由服务器权威判定，
            // 视觉子弹对玩家无效（只对 Enemy 标签结算，PVP 场景无 Enemy）。
            if (_localModel != null && _localModel.weapon != null)
            {
                _localModel.weapon.Fire(_aimPoint);
            }
            // 开火相机震动（与 PVE PlayerAimingState.ShakeCamera 对应）
            if (_camRig == null) _camRig = FindObjectOfType<PVPCameraRig>();
            if (_camRig != null) _camRig.ShakeCamera();
        }

        private void SendAck(long eventId)
        {
            _socket.SendJson(JsonUtility.ToJson(new EventAckMessage { eventId = eventId }));
        }

        // ---------------- 接收 ----------------

        private void DrainSocket()
        {
            string json;
            while ((json = _socket.TryReceive()) != null)
            {
                HandleJson(json);
            }
        }

        private void HandleJson(string json)
        {
            try
            {
                var header = JsonUtility.FromJson<NetMessageHeader>(json);
                switch (header.type)
                {
                    case Msg.Welcome:
                        HandleWelcome(JsonUtility.FromJson<ServerWelcomeMessage>(json));
                        break;
                    case Msg.WelcomeFull:
                        Debug.LogWarning("[NetClient] 房间已满");
                        break;
                    case Msg.Snapshot:
                        HandleSnapshot(JsonUtility.FromJson<WorldSnapshotMessage>(json));
                        break;
                    case Msg.FireReceipt:
                        break; // 视觉开火已即时播放，无需等回执
                    case Msg.Hit:
                        HandleHit(JsonUtility.FromJson<HitEventMessage>(json));
                        break;
                    case Msg.Death:
                        HandleReliableEvent(json);
                        break;
                    case Msg.Respawn:
                        HandleReliableEvent(json);
                        break;
                    case Msg.Kill:
                        HandleReliableEvent(json);
                        break;
                    case Msg.MatchEnd:
                        HandleReliableEvent(json);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetClient] 解析消息失败：{e.Message}");
            }
        }

        private void HandleWelcome(ServerWelcomeMessage w)
        {
            _playerId = w.playerId;
            _serverTickRate = w.serverTickRate;
            _winScore = w.winScore;
            _inputTick = w.serverTick; // 对齐服务器 tick，避免输入被判太旧
            _lastServerTick = w.serverTick;
            _connected = true;

            // 生成本地玩家
            if (spawner != null)
            {
                _localModel = spawner.SpawnLocal(characterId, _playerId);
                if (_localModel != null)
                {
                    _localMotor = _localModel.gameObject.GetComponent<PvPMotor>();
                    if (_localMotor == null) _localMotor = _localModel.gameObject.AddComponent<PvPMotor>();
                    _localModel.disableStateMachine = true;
                    // PVP 无 AI 随从：禁用 NavMeshAgent，避免与 PvPMotor 抢位移
                    if (_localModel.navMeshAgent != null) _localModel.navMeshAgent.enabled = false;
                }
                WireLocalAimTarget(); // PVP 瞄准 IK：约束源空 → 补运行时 AimTarget 接准心
            }

            // 相机由 PVPCameraRig 自动跟随 LocalModel，无需在此绑定

            _lastRttSampleTime = Time.time;
            Debug.Log($"[NetClient] 已连接 playerId={_playerId} tickRate={_serverTickRate}");
        }

        /// <summary>
        /// PVP 瞄准 IK 接线：预制体里 MultiAimConstraint 的源对象是空的（fileID:0），
        /// 又没有 PlayerController 更新 AimTarget → 枪不指向准心、IK 不跟随。
        /// 这里补一个运行时 AimTarget 子物体接进约束，之后每帧把它的位置设成准心射线点。
        /// </summary>
        private void WireLocalAimTarget()
        {
            if (_localModel == null) return;
            // ⚠️ Animation Rigging 约束在 applyRootMotion=false 时【根本不求值】（Unity 已知问题）。
            // PVP 预制体 useFPSMovement=false → PlayerModel.Awake 不会强制开，必须这里补上；
            // 根运动由 PlayerModel.OnAnimatorMove（disableStateMachine 分支）丢弃，不影响 PvPMotor 位移。
            if (_localModel.animator != null)
                _localModel.animator.applyRootMotion = true;

            var aims = _localModel.GetComponentsInChildren<MultiAimConstraint>(true);
            if (aims.Length == 0)
            {
                Debug.LogWarning("[NetClient] PVP 瞄准 IK：未找到 MultiAim 约束（枪无法指向准心）");
                return;
            }

            var aimGo = new GameObject("AimTarget_PVP");
            aimGo.transform.SetParent(_localModel.transform, false);
            aimGo.transform.localPosition = new Vector3(0f, 1.5f, 5f);
            _localAimTarget = aimGo.transform;

            foreach (var c in aims)
            {
                // ⚠️ 08-21 根因修复：sourceObjects 是 WeightedTransformArray【结构体】，get 属性每次返回副本，
                //    直接 `d.sourceObjects.SetTransform(0, t)` 改的是临时副本 → 真实 m_SourceObjects 从没变（诊断 src0=EMPTY_REF）。
                //    必须：读副本 → 改副本 → 经 setter 写回（d 是 ref 到 m_Data，写回才落在真实数据上）。
                ref var d = ref c.data;
                var sources = d.sourceObjects;
                sources.SetTransform(0, _localAimTarget);
                d.sourceObjects = sources;
            }
            // 约束数据改动后重建 Rig，否则新源对象不生效
            var rig = _localModel.GetComponentInChildren<RigBuilder>(true);
            if (rig != null) { rig.Clear(); rig.Build(); }
            // 默认髋部持枪：预制体里 MultiAim m_Weight=1（激活状态），接线后若不重置，
            // 不瞄准时枪也会被 IK 拽着指向 AimTarget。瞄准切换由 PvPMotor 管理。
            foreach (var c in aims) c.weight = 0f;
            var hip = _localModel.GetComponentInChildren<TwoBoneIKConstraint>(true);
            if (hip != null) hip.weight = 1f;
            Debug.Log($"[NetClient] 已接线 PVP 瞄准 IK（{aims.Length} 个 MultiAim → AimTarget_PVP，默认髋部持枪）");
        }

        private void HandleSnapshot(WorldSnapshotMessage snap)
        {
            _lastServerTick = snap.serverTick; // 更新对齐基线（inputTick 用）
            // RTT 粗估：以快照 serverTick 与我们本地 tick 对齐来推（简化：固定值）
            // 自己的状态 → 预测校正
            var alive = new HashSet<int>(); // 本快照在场玩家（修剪 Scores/BotIds 用）
            for (int i = 0; i < snap.players.Length; i++)
            {
                var ps = snap.players[i];
                alive.Add(ps.playerId);
                Scores[ps.playerId] = ps.score; // 计分板
                if (ps.isBot) BotIds.Add(ps.playerId);
                else BotIds.Remove(ps.playerId);
                if (ps.playerId == _playerId)
                {
                    ReconcileLocal(ps);
                }
                else
                {
                    if (!_remotes.TryGetValue(ps.playerId, out var av))
                    {
                        av = CreateRemoteAvatar(ps);
                    }
                    if (av != null)
                    {
                        av.ApplyPlayerState(ps, snap.serverTick);
                        av.ApplyHealth(ps.health, ps.maxHealth);
                    }
                }
            }

            // 修剪：本快照不存在的玩家从计分板/BotIds 移除（bot 被移除、对手离开后不留残项）
            var stale = new List<int>();
            foreach (var id in Scores.Keys)
                if (!alive.Contains(id)) stale.Add(id);
            foreach (var id in stale) { Scores.Remove(id); BotIds.Remove(id); }

            // 销毁不在复制范围的远端
            var replicated = new HashSet<int>(snap.replicatedPlayerIds);
            var toRemove = new List<int>();
            foreach (var kv in _remotes)
            {
                if (!replicated.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }
            foreach (var id in toRemove)
            {
                _remotes[id].Teardown();
                _remotes.Remove(id);
            }
        }

        private RemoteAvatar CreateRemoteAvatar(PlayerSnapshotMessage ps)
        {
            if (spawner == null) return null;
            var prefab = spawner.GetCharacterPrefab(ps.characterId);
            if (prefab == null) return null;

            var go = new GameObject($"RemoteAvatar_{ps.playerId}");
            var av = go.AddComponent<RemoteAvatar>();
            av.Setup(ps.playerId, prefab, new Vector3(ps.x, ps.y, ps.z), ps.bodyYawDeg, _serverTickRate);
            _remotes[ps.playerId] = av;
            return av;
        }

        /// <summary>本地预测误差校正：死区忽略 / 硬阈值瞬移 / 否则平滑。</summary>
        private void ReconcileLocal(PlayerSnapshotMessage ps)
        {
            if (_localModel == null || _localModel.isDead) return;

            Vector3 serverPos = new Vector3(ps.x, ps.y, ps.z);
            float serverYaw = ps.bodyYawDeg;

            // 用 lastProcessedInputTick 对应的预测位置算误差
            Vector3 predictedAtAck = _localModel.transform.position;
            if (_predictionHistory.TryGetValue(ps.lastProcessedInputTick, out var recorded))
                predictedAtAck = recorded;

            // 权威血量同步到本地模型（HUD 读取）
            _localModel.currentHealth = ps.health;
            _localModel.maxHealth = ps.maxHealth;

            Vector3 err = serverPos - predictedAtAck;
            float dist = err.magnitude;

            if (dist > hardCorrectionDistance)
            {
                // 硬校正：瞬移（用 CC 兼容方式：直接设 transform，PvPMotor 驱动所以无 CC）
                if (_localMotor != null) _localMotor.Teleport(serverPos, serverYaw);
                else _localModel.transform.position = serverPos;
            }
            else if (dist > predictionDeadZone)
            {
                // 平滑校正：目标 = 当前位置 + 误差
                Vector3 target = _localModel.transform.position + err;
                Vector3 newPos = Vector3.Lerp(_localModel.transform.position, target, smoothCorrectionSpeed * Time.deltaTime);
                if (_localMotor != null) _localMotor.ApplyState(newPos, serverYaw, _localMotor.VerticalSpeed, _localMotor.IsGrounded);
                else _localModel.transform.position = newPos;
            }
            _lastReconcilePos = serverPos;
        }

        private void TrimPredictionHistory()
        {
            if (_predictionHistory.Count <= 128) return;
            var oldest = int.MaxValue;
            foreach (var k in _predictionHistory.Keys) oldest = Math.Min(oldest, k);
            _predictionHistory.Remove(oldest);
        }

        private void HandleHit(HitEventMessage h)
        {
            if (h.targetPlayerId != _playerId) return;
            // 受击反馈：命中点播受击特效 + 相机震动（对应 PVE TakeDamage → ShakeCamera）
            Debug.Log($"[NetClient] 被 {h.shooterPlayerId} 击中 -{h.damage}");
            if (_localModel != null && _localModel.weapon != null)
            {
                var bulletPrefab = _localModel.weapon.bulletEffectPrefab;
                if (bulletPrefab != null && bulletPrefab.impactPrefab != null)
                    EffectPool.INSTANCE.GetEffect(bulletPrefab.impactPrefab, new Vector3(h.hitX, h.hitY, h.hitZ), Quaternion.identity);
            }
            if (_camRig == null) _camRig = FindObjectOfType<PVPCameraRig>();
            if (_camRig != null) _camRig.ShakeCamera();
        }

        private void HandleReliableEvent(string json)
        {
            var e = JsonUtility.FromJson<ReliableEventMessage>(json);
            if (_recentEventIds.Contains(e.eventId)) return; // 去重
            _recentEventIds.Add(e.eventId);
            if (_recentEventIds.Count > 128) _recentEventIds.Clear();
            SendAck(e.eventId);

            switch (e.type)
            {
                case Msg.Death:
                    if (_remotes.TryGetValue(e.playerId, out var av)) av.ApplyDeath();
                    else if (e.playerId == _playerId) OnLocalDeath(e);
                    break;
                case Msg.Respawn:
                    if (_remotes.TryGetValue(e.playerId, out var av2)) av2.ApplyRespawn(e);
                    else if (e.playerId == _playerId) OnLocalRespawn(e);
                    break;
                case Msg.Kill:
                    Debug.Log($"[NetClient] {e.killerPlayerId} 击杀 {e.victimPlayerId}");
                    break;
                case Msg.MatchEnd:
                    OnMatchEnd(e);
                    break;
            }
        }

        private void OnLocalDeath(ReliableEventMessage e)
        {
            if (_localModel != null && !_localModel.isDead)
                _localModel.ApplyDeathNetwork();
            OnLocalDied?.Invoke();
        }

        private void OnLocalRespawn(ReliableEventMessage e)
        {
            if (_localMotor != null)
                _localMotor.Teleport(new Vector3(e.x, e.y, e.z), 0f);
            if (_localModel != null)
                _localModel.ApplyRespawnNetwork(new Vector3(e.x, e.y, e.z), e.health, e.maxHealth);
        }

        private void OnMatchEnd(ReliableEventMessage e)
        {
            Debug.Log($"[NetClient] 对局结束，胜者 {e.winnerPlayerId}");
            OnMatchEnded?.Invoke(e.winnerPlayerId);
        }

        private void OnApplicationQuit()
        {
            if (_socket != null && _connected)
            {
                try { _socket.SendJson(JsonUtility.ToJson(new ClientGoodbyeMessage())); } catch { }
            }
        }
    }
}
