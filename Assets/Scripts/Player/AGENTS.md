# Assets/Scripts/Player — 玩家系统

> 改本目录代码前必读根 `AGENTS.md`。Codex 只改 C#；角色/武器/相机/IK/Animator 的组件拖拽与 Inspector 配置由用户在 Unity GUI 完成。

## 职责

| 文件 | 职责 |
|---|---|
| `PlayerController.cs` | 输入轮询、相机相对运动、双 FreeLook、AimTarget、角色切换、死亡接管、震动。 |
| `PlayerModel.cs` | CharacterController/Animator/状态机、生命、随从、FPS 位移、IK 引用。 |
| `PlayerWeapon*.cs` | 荧/芙宁娜的限速射击、投射物池、防穿透、特效回收。 |
| `WeaponAudio.cs` | 订阅成功开火事件的 3D 枪声；Hunter 不使用。 |
| `State/` | Idle、Move、Sprint、Slide、Hover、Aiming 六个状态。 |

## 不可破坏的行为

- `useFPSMovement=true` 时，状态先写 `horizontalVelocity`，再由 `PlayerModel.LateUpdate` 以 `cc.Move` 位移；不要移回 Update。
- `applyRootMotion` 必须保持 true，FPS 分支通过 `OnAnimatorMove` 丢弃根位移；关闭它会使现有 Animation Rigging 失效。
- 非主控随从由 NavMeshAgent 独占 Transform；不得同时执行其 `cc.Move`。`PlayerStateBase.Update()` 不能在非主控时提前 return。
- `IsHover()` 的 SphereCast 起跳与 `cc.isGrounded` 落地是有意的不对称；`HOVER_STABILITY_FRAMES=5` 防斜坡 flicker。Hover 的 `!IsHover()` 落地兜底必须限制在 `verticalSpeed <= 0`。
- Hunter 的 `weapon` 和所有握枪 IK 可为 null；保留移动/瞄准，任何瞄准或开火修改都需 null-safe。
- `PlayerWeapon.bulletInterval=0.15f`；子弹和特效必须回池。PVP 调用武器仅复用视觉，不能把 PVE 伤害带入网络权威逻辑。

## 状态与参数

`Idle ↔ Move ↔ Sprint`；地面状态可进 `Aiming`；Move/Sprint 可进 Slide；跳跃/跌落进 Hover，着地回 Idle。Animator 参数是 `Speed`、`VerticalSpeed`、`IsGrounded`、`IsSprinting`、`AimingX`、`AimingY`、`HoverClip`。状态机类使用标准拼写 `StateMachine`；仅保留既有 `Destory()` 拼写。

## GUI 交接

需要新增角色、武器、相机、Rig、动画参数或血条引用时，只提供 Hierarchy/Inspector 操作步骤并等待用户确认；不要用 C#、Wizard 或序列化文本替用户写入这些 Unity 配置。
