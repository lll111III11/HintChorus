# HintChorus Beta 1.1.1 — If another hint plugin jumps the queue, it gets kicked back out.

> 发布标题 / Release title:
> **HintChorus Beta 1.1.1 — If another hint plugin jumps the queue, it gets kicked back out.**
>
> 中文副标题 / Chinese subtitle:
> **HintChorus 1.1.1：敢站它前面的，直接一脚踹掉。**

---

## English (primary)

**Beta status** — HintChorus is still Beta. Usable on regular production servers, but keep an eye on it. If anything goes wrong, run `Hint orvs native`, or remove/delete HintChorus and restart.

### What's changed

- **Force takeover (default on)**: `HintDisplay.Show` now runs at Harmony priority `int.MaxValue`, and a postfix fallback is attached. If another hint plugin's prefix swallows the call so our prefix never runs, HintChorus immediately re-sends its composite block instead of leaving the screen blank.
- **Pre-override snapshots**: every third-party hint is recorded *before* it is absorbed or suppressed (text, duration, owning plugin, per player). Rollback can replay that exact data.
- **`Hint orvs` mainstream detection**:
  - No mainstream hint plugin detected → the command says so explicitly and offers option **1** = restore vanilla and replay the pre-override data.
  - Mainstream plugins present → they are listed by number. Picking one rolls that plugin back to vanilla and **hot-applies its last recorded data per player**; `merge` restores it.
  - New sub-commands: `Hint orvs native` (global vanilla + replay), `Hint orvs all` (all plugins vanilla + replay), `Hint orvs reset` (back to merged).
- New config key: `ForceTakeover` (default `true`). Turn it off to go back to observe-and-yield behaviour.
- Multiple hint plugins can still coexist without conflicts.

### Rollback

- Console: `Hint orvs` / `Hint orvs native` / `Hint orvs reset`.
- Or remove/delete `HintChorus.dll` (and `0HintChorus.Bootstrap.dll`) and restart the server.

> Target build: current public branch 14.2.x. Patch targets will be re-verified after SCP:SL 15.0 “Megapatch III” ships.
> Release asset: **`HintChorus.dll`** (1.1.1.0).

---

## 中文（辅助）

**HintChorus 1.1.1：敢站它前面的，直接一脚踹掉。**

本次更新加入**更强势的补丁接管（内存层）**：`HintDisplay.Show` 提到 Harmony 最高优先级 `int.MaxValue`，并加 Postfix 兜底。别的插件的前缀把调用吞掉、导致我们的前缀根本没跑时，会立即强制重发本底层的复合块 —— 不再出现“别的插件一站前面，UI 直接不显示”。

**覆盖前数据**：第三方提示在被吸收或压制之前，先按玩家记录文本、时长与所属插件。需要回退时，把这份记录原样热应用回去。

**`Hint orvs` 行为更新**：

- 执行后先检测当前是否存在主流 Hint 插件。
- 一个都没有时，用中文明确告诉服主“未检测到主流插件”，并提供编号 **1** 的选项：**直接退回原生，并恢复覆盖前的数据**。
- 检测到主流插件时，按编号列出；选择某个编号后，将该插件单独退回原生，并把它在各玩家上的最后一条数据热应用。
- 新增：`Hint orvs native`（整体退回原生 + 回放）、`Hint orvs all`（全部插件退回 + 回放）、`Hint orvs reset`（退出原生、全部恢复合并）。

**新配置**：`ForceTakeover`（默认开）。关掉即退回“只观测、遇冲突让路”的旧行为。

**多个 Hint 插件依然可以同时塞入、互不冲突**；某个插件出问题也可以单独退回。

### 退回方式

- 服务器后台控制台输入 `Hint orvs`，按编号选择；或 `Hint orvs native` / `Hint orvs reset`。
- 或者直接移出/删除 `HintChorus.dll`（以及 `0HintChorus.Bootstrap.dll`）后重启服务器。

> 目标版本：当前公开分支 14.2.x；15.0「Megapatch III」正式发布后会重新核对拦截落点。
> 本次发布资产请使用 **`HintChorus.dll`**（1.1.1.0）。
