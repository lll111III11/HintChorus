# HintChorus Beta 1.1.2 — Patch Arbiter & Shield

> 发布标题 / Release title:
> **HintChorus Beta 1.1.2 — Patch Arbiter & Shield**
>
> 中文副标题 / Chinese subtitle:
> **HintChorus 1.1.2 版本更新：新增「补丁仲裁层」**

---

## English (primary)

**Beta status** — HintChorus is still Beta. Usable on regular production servers, but keep an eye on it. If anything goes wrong, run `Hint orvs native`, or remove/delete HintChorus and restart.

### New: Patch Arbiter

HintChorus no longer depends on Harmony priority to keep its place. It runs its own patch middle layer: all five Harmony uninstall entry points are instrumented (`Harmony.Unpatch` ×2, `Harmony.UnpatchAll`, `PatchProcessor.Unpatch` ×2).

When another framework calls `Unpatch(original, All)` — which clears **every** plugin's patches from that method — HintChorus detects the loss and restores its patches **synchronously in the same call stack**, then re-sends the composite. Priority is no longer the line of defence; being in the table is, and the arbiter guarantees that.

- Other plugins can still unload and patch normally; the arbiter only restores its own patches.
- If the arbiter's own hooks are wiped by `UnpatchAll(null)`, they are reinstalled during the same restore pass.
- A **0.25 s shield** remains as a backstop for JIT-inlined call sites or any path that bypasses the arbiter.

Verified against a real HarmonyX harness:

| Strategy | Result |
| --- | --- |
| Priority only (`int.MaxValue`) | Loses — removed by `Unpatch All` |
| Hooking only the outer `Harmony.Unpatch` | Misses when the caller is JIT-inlined |
| Arbitration on the deep uninstall entries + hook self-repair | Wins — restored in the same call stack, our prefix runs first |

### Also in this release

- **Load-first bootstrap is now `0.dll`** — shortest name plus `LoadPriority.Highest`, so it is enumerated before every other plugin and installs the interception first.
- **External takeover is denied by default.** Run `Hint orvs yield` on the server terminal console to hand the hint channel to another framework; `Hint orvs take` reclaims it. `Hint` / `hc` are server-console only and validate the sender type — Remote Admin, the query interface and the player client console are rejected.
- **15.0 template-layer skeleton (off by default).** `0.dll` releases `HintChorus.Mapping.dll` + `template-map.ini` into `dependencies/global`; setting `Template` resolves the template and warms the display-layer probe. No rendering change yet.
- **First-run setup wizard (server console, localized zh/en/fr/ru/es).** It asks about auto update, detailed logs, writing the config and RNR; each answer prints a localized explanation.
- **Safe update check.** `Hc update` checks the latest GitHub release; with `auto_update` enabled it only downloads to `HintChorus.dll.new` by default. With `rnr_after_update` enabled it replaces the DLL and sets the game to restart at the end of the current round. Locale detection uses the public IP + network time zone when online and falls back to the local timezone/clock when offline; unknown region falls back to English.
- **Five-language runtime is complete.** Wizard, update messages and `Hint` command help are localized in `zh` / `en` / `fr` / `ru` / `es`. The other 20+ languages are external translation files/docs only, and every doc states that the runtime embeds only these five.
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

**版本更新：新增「补丁仲裁层」。**

HintChorus 不再只靠 Harmony 优先级占位，而是自带一套补丁管理中间层：Harmony 的 5 个卸载入口全部受控（`Harmony.Unpatch` 两个重载、`Harmony.UnpatchAll`、`PatchProcessor.Unpatch` 两个重载）。

当其它框架执行 `Unpatch(original, All)`（把该方法上所有插件的补丁整表清空）时，本底层会检测到丢失，并**在同一个调用栈内**把自己的补丁装回去，然后重发复合块。真正起作用的不是优先级，而是“始终在表里”，这一点由仲裁层保证。

- 其它插件仍可正常卸载、正常打补丁；仲裁层只恢复自己的补丁。
- 仲裁层自身的钩子被 `UnpatchAll(null)` 整表清空后，会在同一次恢复流程中自动补回。
- **0.25 秒护盾**保留为兜底，覆盖调用点被 JIT 内联或绕过仲裁层的极端情况。

用真实 HarmonyX 沙盒验证过：

| 策略 | 结果 |
| --- | --- |
| 只靠优先级（`int.MaxValue`） | 输 —— 被 `Unpatch All` 摘掉 |
| 只挂外层 `Harmony.Unpatch` | 调用点被 JIT 内联时会漏掉 |
| 在深层卸载入口仲裁 + 钩子自修复 | 赢 —— 同一调用栈内恢复，本底层前缀先执行 |

其它内容：

- **抢加载引导器改为最短文件名 `0.dll`**：配合 `LoadPriority.Highest`，在目录枚举里排在所有插件之前，先把拦截层装好。
- **默认拒绝外部接管**：在服务器终端控制台执行 `Hint orvs yield` 才会让出提示通道，`Hint orvs take` 收回；`Hint` / `hc` 只注册在服务器控制台并校验发送方类型，RA 面板、查询接口、玩家客户端控制台一律拒绝。
- **15.0 模板层骨架（默认关闭）**：`0.dll` 会把 `HintChorus.Mapping.dll` + `template-map.ini` 释放到 `dependencies/global`；配置 `Template` 后才会解析模板并预热显示层探测，不改变现有渲染。
- **首次运行配置向导（服务器终端，zh/en/fr/ru/es）**：询问自动更新、详细日志、是否写入配置与更新后回合结束重启（RNR）；每选择一项都会显示当地语言说明。
- **安全更新检查**：`Hc update` 检查 GitHub 最新版本；开启 `auto_update` 时默认只下载到 `HintChorus.dll.new`；开启 `rnr_after_update` 后会替换 DLL 并设置本回合结束后重启；有网时用公网 IP + 网络时区判定语言/时区，无网回退本地时区/时间戳，地区探测失败时语言回退英文。
- **五大语言运行时已补全**：向导、更新提示与 `Hint` 命令帮助全部覆盖 `zh` / `en` / `fr` / `ru` / `es`；其余 20+ 语言仅以外部译文/文档形式提供，每份文档都明确写明运行时只内嵌这五种。
- **快照拦回**：第三方提示在被吸收前按玩家记录快照；外部插件改动提示通道后，通过快照退回并重新接管。
- **`Hint orvs` 主流插件检测**：没有主流插件时提供编号 **1** = 退回原生 + 恢复覆盖前数据；有则按编号单独回退并热应用其最后数据。
- **多个 Hint 插件依然可以共存、互不冲突。**

### 退回方式

- 服务器后台控制台输入 `Hint orvs`，按编号选择；或 `Hint orvs native` / `Hint orvs reset`。
- 或者直接移出/删除 `HintChorus.dll`（以及 `0HintChorus.Bootstrap.dll`）后重启服务器。

> 目标版本：当前公开分支 14.2.x；15.0「Megapatch III」正式发布后会重新核对拦截落点。
> 本次发布资产请使用 **`HintChorus.dll`**（1.1.2.0）。
