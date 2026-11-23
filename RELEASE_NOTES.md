# HintChorus Beta 1.1.2 — Patch Shield

> 发布标题 / Release title:
> **HintChorus Beta 1.1.2 — Patch Shield**
>
> 中文副标题 / Chinese subtitle:
> **HintChorus 1.1.2 版本更新：新增「补丁护盾」**

---

## English (primary)

**Beta status** — HintChorus is still Beta. Usable on regular production servers, but keep an eye on it. If anything goes wrong, run `Hint orvs native`, or remove/delete HintChorus and restart.

### New: Patch Shield

Some hint frameworks (for example HintServiceMeow) call `Harmony.Unpatch(HintDisplay.Show, HarmonyPatchType.All)` while installing. That removes **every other plugin's patches** from the method — once you are off the table, Harmony priority no longer means anything.

The shield checks `HintDisplay.Show` every **0.25 seconds**. If our prefix is missing it reinstalls immediately and re-sends the composite to every player, so the UI comes back in the same frame instead of staying blank.

Verified against a real HarmonyX harness:

| Strategy | Result |
| --- | --- |
| Priority only (`int.MaxValue`) | Loses — removed by `Unpatch All` |
| Hook `Harmony.Unpatch` with a postfix | Never fires (Harmony's internal path bypasses it) |
| Detect-and-reinstall | Wins — our prefix runs first after reinstall |

The shield only reinstalls our own patches. It never blocks another plugin from unloading or patching normally.

### Also in this release

- **Snapshot Rollback & Takeover**: third-party hints are recorded per player before absorption; if another plugin modifies the channel after us, HintChorus rolls it back through the snapshot and re-takes over.
- **`Hint orvs` mainstream detection**: no mainstream hint plugin → option **1** = restore vanilla + replay pre-override data. Mainstream plugins present → numbered per-plugin rollback with hot-applied last data.
- Existing commands: `Hint orvs native`, `Hint orvs all`, `Hint orvs reset`.
- Multiple hint plugins can coexist without conflicts.

### Rollback

- Console: `Hint orvs` / `Hint orvs native` / `Hint orvs reset`.
- Or remove/delete `HintChorus.dll` (and `0HintChorus.Bootstrap.dll`) and restart the server.

> Target build: current public branch 14.2.x. Patch targets will be re-verified after SCP:SL 15.0 “Megapatch III” ships.
> Release asset: **`HintChorus.dll`** (1.1.2.0).

---

## 中文（辅助）

**版本更新：新增「补丁护盾」。**

部分 Hint 框架（例如 HintServiceMeow）在安装时会执行 `Harmony.Unpatch(HintDisplay.Show, HarmonyPatchType.All)`，把别人挂在这个方法上的补丁**整体摘掉**。补丁一旦不在表里，Harmony 优先级再高也没有意义。

护盾每 **0.25 秒**检查一次 `HintDisplay.Show` 上是否还有本底层的补丁；发现被摘掉就立即重装，并向所有玩家重发复合块 —— UI 在同一帧内回来，而不是一直空白。

用真实 HarmonyX 沙盒验证过：

| 策略 | 结果 |
| --- | --- |
| 只靠优先级（`int.MaxValue`） | 输 —— 被 `Unpatch All` 摘掉 |
| 给 `Harmony.Unpatch` 挂后置钩子 | 不触发（Harmony 内部路径绕过） |
| 检测到被摘掉后立即重装 | 赢 —— 重装后我们的前缀先执行 |

护盾只重装自己的补丁，不阻止其它插件正常卸载或打补丁。

其它内容：

- **快照拦回**：第三方提示在被吸收前按玩家记录快照；外部插件改动提示通道后，通过快照退回并重新接管。
- **`Hint orvs` 主流插件检测**：没有主流插件时提供编号 **1** = 退回原生 + 恢复覆盖前数据；有则按编号单独回退并热应用其最后数据。
- **多个 Hint 插件依然可以共存、互不冲突。**

### 退回方式

- 服务器后台控制台输入 `Hint orvs`，按编号选择；或 `Hint orvs native` / `Hint orvs reset`。
- 或者直接移出/删除 `HintChorus.dll`（以及 `0HintChorus.Bootstrap.dll`）后重启服务器。

> 目标版本：当前公开分支 14.2.x；15.0「Megapatch III」正式发布后会重新核对拦截落点。
> 本次发布资产请使用 **`HintChorus.dll`**（1.1.2.0）。
