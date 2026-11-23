# HintChorus Beta 1.1.1 — Snapshot Rollback & Takeover

> 发布标题 / Release title:
> **HintChorus Beta 1.1.1 — Snapshot Rollback & Takeover**
>
> 中文副标题 / Chinese subtitle:
> **HintChorus 1.1.1 版本更新：新增「快照拦回」**

---

## English (primary)

**Beta status** — HintChorus is still Beta. Usable on regular production servers, but keep an eye on it. If anything goes wrong, run `Hint orvs native`, or remove/delete HintChorus and restart.

### New: Snapshot Rollback & Takeover

Third-party hint calls are now recorded as per-player snapshots **before** they are absorbed or suppressed: text, duration and owning plugin. When another hint plugin modifies the hint channel after us, HintChorus rolls the channel back through that snapshot and takes the patch back over.

Two implementation paths:

1. **Contention first** — `HintDisplay.Show` is patched at the highest Harmony priority, so the takeover runs before mainstream hint plugins and they are not given control of the channel.
2. **Rollback after the fact** — if another plugin already took the channel first, a postfix fallback detects that our prefix was skipped, pushes the snapshot back out for that player, and re-takes over the whole channel.

### Also in this release

- **`Hint orvs` mainstream detection**: no mainstream hint plugin detected → the command says so and offers option **1** = restore vanilla and replay the pre-override data. Mainstream plugins present → listed by number; selecting one rolls that plugin back to vanilla and hot-applies its last recorded data per player; `merge` restores it.
- New sub-commands: `Hint orvs native`, `Hint orvs all`, `Hint orvs reset`.
- New config key: `ForceTakeover` (default `true`). Turn it off to go back to observe-and-yield behaviour.
- Multiple hint plugins can still coexist without conflicts.

### Rollback

- Console: `Hint orvs` / `Hint orvs native` / `Hint orvs reset`.
- Or remove/delete `HintChorus.dll` (and `0HintChorus.Bootstrap.dll`) and restart the server.

> Target build: current public branch 14.2.x. Patch targets will be re-verified after SCP:SL 15.0 “Megapatch III” ships.
> Release asset: **`HintChorus.dll`** (1.1.1.0).

---

## 中文（辅助）

**版本更新：新增「快照拦回」。**

本版本新增快照拦回功能。第三方提示在被吸收或压制之前，会先按玩家记录一份快照（文本、时长、所属插件）。当外部 Hint 插件在我们之后修改了提示通道时，HintChorus 会通过这份快照把通道退回，并重新接管补丁。

具体实现有两种方式：

1. **抢占在先**：把 `HintDisplay.Show` 挂在 Harmony 最高优先级，抢在主流 Hint 插件之前接管，不让它们拿到提示通道。
2. **事后拦回**：如果对方已经抢先接管，则由 Postfix 兜底发现我们的前缀被跳过，按快照把该玩家这一份重新下发，再整体接管回来。

其它更新：

- **`Hint orvs` 主流插件检测**：没有检测到主流 Hint 插件时，明确告诉服主，并提供编号 **1** = 退回原生并恢复覆盖前数据；检测到主流插件时按编号列出，选中后单独回退并把该插件最后一条数据热应用；`merge` 恢复合并。
- 新增子命令：`Hint orvs native`、`Hint orvs all`、`Hint orvs reset`。
- 新配置：`ForceTakeover`（默认开）。关掉即退回“只观测、遇冲突让路”的行为。
- 多个 Hint 插件依然可以同时存在、互不冲突。

### 退回方式

- 服务器后台控制台输入 `Hint orvs`，按编号选择；或 `Hint orvs native` / `Hint orvs reset`。
- 或者直接移出/删除 `HintChorus.dll`（以及 `0HintChorus.Bootstrap.dll`）后重启服务器。

> 目标版本：当前公开分支 14.2.x；15.0「Megapatch III」正式发布后会重新核对拦截落点。
> 本次发布资产请使用 **`HintChorus.dll`**（1.1.1.0）。
