# 变更记录 / Changelog

本项目已从 **alpha** 进入 **beta**：可以进入正常的生产服务器，但仍需**谨慎使用**；
一旦出现异常，请立即按本文末尾的「回退」步骤退回上一版本或关闭插件。

## 14.2.9.0 (2026-11-25)

### 版本规则 / Versioning

- **版本号与游戏对齐**：此后版本为 `<游戏 Major>.<游戏 Minor>.<游戏 Revision>.<插件构建号>`，当前游戏 SCP:SL 14.2.9 → 本期 `14.2.9.0`。游戏更新时前三段跟随、第四段归零。旧的 `15.0.x` 保留为历史版本号。
- **更新检查比较四位版本**：`UpdateService` 改用完整程序集版本比较，避免同游戏版本下第 4 位构建号被截断而误判“无更新”。
- **启动报告新增游戏版本行**：`游戏: SCP:SL 14.2.9`，与插件版本并列显示，便于核对版本对齐。

## 15.0.2 (2026-11-25)

- **第 5 项：详细配置向导。** 首次运行向导在第 4 项之后新增第 5 项：“是否开启详细的所有配置文件设置”。选择“是”后，除前 4 项已问过的配置外，所有配置参数按声明顺序逐个询问，显示参数名、类型、当前值、默认值与说明。
- 详细配置中支持：`Hc setup default`（剩余项全部设为默认）、`Hc setup skip`（跳过剩余项）、`Hc setup save`（保存当前配置与进度，下次启动从该参数继续）。
- 进度写入 `configs/<端口>/HintChorus/setup.state` 的 `ResumeMode=detailed` / `ResumeIndex=<n>`；完成或跳过时清除进度并记录 `CompletedVersion`。
- 支持 bool / int / float / byte / ushort / string / 枚举 / `Dictionary<string,string>` 八类参数；`PositionOverrides` 使用 `Key=Value;Key2=Value2` 形式输入，留空或 `default` 表示清空。
- **第三方 Hint 插件自动兼容模式**：新增配置 `CompatMode`：`Auto`=检测到主流 Hint 插件时自动切原生直通（默认）；`Notify`=只提醒、不切换；`Off`=关闭检测。兼容检测下若仍出现外部清场（60 秒内护盾重装 ≥3 次），向控制台报告并提示 `Hint orvs` 查看编号、`Hint orvs <编号>` 单插件回退、`Hint orvs native` 整体退回。
- **配置项变化自动重弹向导**：`setup.state` 记录配置结构指纹 `ConfigSchema`（属性名 + 类型）；新增/删除/改名/改类型配置项时，即使版本号不变也会重新进入向导，方便为新配置项做选择。

## 15.0.1 (2026-11-25)

### 修复 / Fixes

- **`HC` 只注册服务器后台控制台。** 改用 LabAPI `[CommandHandler(typeof(GameConsoleCommandHandler))]` 自动注册，移除手工注册与 5 秒重试；RA 面板、查询接口与玩家客户端不再出现该命令。
- **服务器终端判定放宽。** 除 `GameCore.ConsoleCommandSender` 与 `SENDER ID = GAME CONSOLE` 外，`FullPermissions` 也放行，避免真实后台终端被误判成“只允许服务器终端”。
- **首次运行向导改为逐项询问。** 一次只显示当前一项，回答“是/否”后才显示下一项；顺序为：自动更新 → 详细 UI 日志 → 更新后回合结束重启(RNR) → 是否写入配置。
- 新增 `Hc setup 是|否` 与 `Hc 是|否` 快捷回答；`Hc setup <1-4> on|off` 仍可指定单项。
- 五个语言包同步更新（en/zh/fr/ru/es，195 key/语言），README 与 docs 的向导说明同步。

## 15.0.0 (2026-11-23)

### Release headline / 发布标题

**HintChorus 15.0.0 — UI Middle Layer & Real Language Packs**

- **运行时语言包真正落地。** 用户可见的运行文本（命令、向导、更新、启动、模板、仲裁/自愈/护盾、合并核心、SSS）不再硬编码，统一进入 `src/Resources/langpack/{en,zh,fr,ru,es}.lang`（5 语 × 193 key），编进 `HintChorus.dll`。
- **任意语言可扩展。** 外部语言包放在 `configs/<端口>/HintChorus/lang/<code>.lang`，逐 key 覆盖内嵌值；缺失 key 按「请求语言 → 语言主代码 → en → zh」回退。不需要重新编译，不需要改代码。
- **`Hint lang` 命令。** `Hint lang` 查看语言包/key 数量/来源，`Hint lang reload` 热重载，`Hint lang path` 显示目录。
- **提示管道解剖。** 新增 [docs/15.0-hint-internals.md](docs/15.0-hint-internals.md)：从 `HintDisplay.Show` 到 `HintMessage` 的逐字节线格式、20 种 `HintParameter`、3 种 `HintEffect`、时长倍率语义、`SuppressedReceivers` 官方静音开关，以及 15.0 发布当天的核对清单。
- **语言代码放宽。** `ServerLocale.Normalize` 不再把未知语言强制改成 `en`；任何 `xx` / `xx_YY` 代码都会保留，由语言包决定是否存在对应译文，再逐条回退。
- **启动报告新增语言包摘要**：每个语言的 key 数量与来源（builtin / external / both）写入 `logs/report-*.log`。
- **修复引导器释放失败**：Windows 上正在运行的 `0.dll` / `HintChorus.dll` 是 user-mapped，进程内无法覆盖。现在按程序集版本判断：同版本不重写；确实需要更新但文件被占用时，暂存为 `0.dll.new` / `HintChorus.Mapping.dll.new` 并给出停服替换提示，不再每次启动报错。引导器/映射/主工程全部改为 deterministic 构建，同一份源码重建字节稳定。
- **单文件发放 + 永久 shim（15.0.0 最终形态）**：主 DLL 内嵌 永久 shim、引导 payload 与映射组件。首次运行全部释放并提示重启一次；重启后 `0.dll`（固定 1.0.0）从 `HintChorus.Bootstrap.payload` 载入真正的引导逻辑，payload 再做抢先安装。以后更新只替换 payload，`0.dll` 永远不动，不再需要手动搬 `.new`。
- 版本号对齐 15.0.0；既有补丁仲裁、快照拦回、护盾、抢加载 `0.dll`、SSS 端口隔离等能力保持不变。

### 更新速览

- 运行时文案全部迁入语言包（5 语 × 193 key），DLL 内嵌，开箱即用。
- 外部 `lang/<code>.lang` 可覆盖任意行、可新增任意语言，`Hint lang reload` 热重载。
- 新增 `Hint lang` / `Hint lang reload` / `Hint lang path`。
- 新增 `docs/15.0-hint-internals.md`：Hint 线格式、参数/效果类型表、抑制开关与 15.0 核对清单。
- 语言探测不再把未知语言强制回退英文；没有对应包时仍逐条回退。
- 启动报告的「语言包」一行会列出每个语言的 key 数与来源。

### 升级说明

- 旧版 `config.yml` 无需修改；`Language` 仍支持 `auto` / `zh` / `en` / `fr` / `ru` / `es`，现在也接受外部语言包代码。
- 想改文案：复制 `src/Resources/langpack/<code>.lang` 到 `configs/<端口>/HintChorus/lang/<code>.lang`，只保留要改的行，然后 `Hint lang reload`。
- 语言包解析失败只影响该文件，不会阻止插件加载；缺失 key 显示 `[key]` 并继续运行。

## beta 1.1.2 (2026-11-23)

### Release headline / 发布标题

**HintChorus Beta 1.1.2 — Patch Arbiter & Shield**

- **New: Patch Arbiter.** HintChorus now manages its patches through its own middle layer instead of trusting Harmony priority. All five Harmony uninstall entry points (`Harmony.Unpatch` ×2, `Harmony.UnpatchAll`, `PatchProcessor.Unpatch` ×2) are instrumented: when a framework clears a method with `Unpatch All`, our patches are restored **synchronously in the same call stack** and the composite is re-sent.
- The arbiter only restores its own patches — other plugins can still unload and patch normally. If the arbiter's own hooks are wiped by `UnpatchAll(null)`, they are reinstalled during the same restore pass.
- **Patch Shield stays as the backstop**: a 0.25 s self-check covers JIT-inlined call sites and any path that bypasses the arbiter.
- **Load-first bootstrap is now `0.dll`** (shortest name + `LoadPriority.Highest`), so it is enumerated before every other plugin and installs the interception first.
- **External takeover is denied by default.** Only the server terminal console can run `Hint orvs yield` to hand the hint channel to another framework; `Hint orvs take` reclaims it. `Hint` / `hc` are registered on the server console only and validate the sender type — Remote Admin, the query interface and the player client console are rejected.
- **15.0 template-layer skeleton (off by default).** `0.dll` releases `HintChorus.Mapping.dll` + `template-map.ini` into `dependencies/global`; setting `Template` in the config resolves the template and warms the display-layer probe. No rendering change yet.
- **First-run setup wizard (server console, localized zh/en/fr/ru/es).** After startup it asks about auto update, detailed logs, writing the config and RNR (restart at round end after replacing the DLL); each answer prints a localized explanation.
- **Safe update check.** `Hc update` checks the latest GitHub release; with `auto_update` enabled it only downloads to `HintChorus.dll.new` by default. With `rnr_after_update` enabled it replaces the DLL and sets the game to restart at the end of the current round.
- **Locale detection.** With network access, language/timezone are resolved from the public IP and network time zone, verified against the HTTP Date header; offline it falls back to the local timezone/clock. If the region cannot be detected, the language falls back to English. `network_locale: off` disables network probing.
- **Five-language runtime is complete.** The setup wizard, update messages and `Hint` command help are all localized in `zh` / `en` / `fr` / `ru` / `es`. The other 20+ languages are external translation files/docs only; every doc states that the runtime embeds only these five.
- Verified against a real HarmonyX harness: HSM-style `Unpatch(Show, All)` + takeover prefix → our layer is restored and runs first, HSM's prefix is skipped; `UnpatchAll(null)` → restored; targeted removal of our ID → restored; removing another plugin's own patches → no false restore.

### 更新速览

- **新增「补丁仲裁层」**：不再只依赖 Harmony 优先级，本插件用自己的中间层管理补丁。Harmony 的 5 个卸载入口全部受控；外部框架用 `Unpatch All` 清场后，本底层补丁会**在同一个调用栈内**恢复，并重发复合块。
- 仲裁层只恢复自己的补丁，不影响其它插件正常卸载或打补丁；仲裁层自身的钩子被整表清空（`UnpatchAll(null)`）后会在恢复过程中自动补回。
- **护盾保留为兜底**：每 0.25 秒自检一次，覆盖调用点被 JIT 内联等极端情况。
- **抢加载引导器改为最短文件名 `0.dll`**（配合 `LoadPriority.Highest`），在目录枚举里排在所有插件之前，先把拦截层装好。
- **默认拒绝外部接管**：只有服务器终端控制台可以执行 `Hint orvs yield` 让出提示通道，`Hint orvs take` 收回；`Hint` / `hc` 只注册在服务器控制台并校验发送方类型，RA 面板、查询接口、玩家客户端控制台一律拒绝。
- **15.0 模板层骨架（默认关闭）**：`0.dll` 会把 `HintChorus.Mapping.dll` + `template-map.ini` 释放到 `dependencies/global`；配置 `Template` 后才会解析模板并预热显示层探测，不改变现有渲染。
- **首次运行配置向导（服务器终端，zh/en/fr/ru/es）**：启动文本输出完后询问自动更新、详细日志、是否写入配置、是否在更新后回合结束重启（RNR）；每选择一项都会用当地语言显示说明。
- **安全更新检查**：`Hc update` 检查 GitHub 最新版本；开启 `auto_update` 时默认只下载到 `HintChorus.dll.new`；开启 `rnr_after_update` 后会替换 DLL 并设置本回合结束后重启。
- **本地语言探测**：有网时用公网 IP + 网络时区，并用 HTTP Date 校验本地时钟；无网时回退本地时区/时间戳；地区探测失败时语言回退英文；`network_locale: off` 可关闭网络探测。
- **五大语言运行时已补全**：向导、更新提示与 `Hint` 命令帮助全部覆盖 `zh` / `en` / `fr` / `ru` / `es`。其余 20+ 语言仅以外部译文/文档形式提供；每份文档都明确写明运行时只内嵌这五种。
- 真实 HarmonyX 沙盒验证：HSM 式清场后本底层恢复且先执行；整表清空会恢复；点名卸载会恢复；卸载别人自己的补丁不会误触发。

## beta 1.1.1 (2026-11-23)

### Release headline / 发布标题

**HintChorus Beta 1.1.1 — Snapshot Rollback & Takeover**

- **New: Snapshot Rollback & Takeover.** Third-party hints are recorded as per-player snapshots before absorption or suppression. When another hint plugin modifies the hint channel after us, HintChorus rolls it back through the snapshot and takes the patch back over.
  - Path 1 — contention first: `HintDisplay.Show` runs at the highest Harmony priority, so mainstream hint plugins never get control of the channel.
  - Path 2 — rollback after the fact: if another plugin already took the channel first, a postfix fallback detects that our prefix was skipped, pushes the snapshot back out, and re-takes over.
- **`Hint orvs` mainstream detection**: no mainstream hint plugin → the command says so and offers option **1** = restore vanilla + replay pre-override data. Mainstream plugins present → numbered per-plugin rollback with hot-applied last data.
- New config: `ForceTakeover`. New commands: `Hint orvs native`, `Hint orvs all`, `Hint orvs reset`.

Full bilingual release notes: [RELEASE_NOTES.md](RELEASE_NOTES.md)

### 更新速览

- **新增「快照拦回」**：第三方提示在吸收/压制前先按玩家记录快照；外部插件在我们之后修改提示通道时，通过快照强行退回并重新接管补丁。
  - 方式一：抢占在先 —— `HintDisplay.Show` 使用最高 Harmony 优先级，抢在主流插件之前接管。
  - 方式二：事后拦回 —— 对方已抢先时，由 Postfix 发现前缀被跳过，按快照退回后再整体接管。
- **`Hint orvs` 主流插件检测**：没有主流 Hint 插件时明确提示，并提供编号 **1** = 整体退回原生 + 恢复覆盖前数据；有主流插件时按编号列出，选中后单独回退并热应用其最后一条数据。
- 新配置：`ForceTakeover`；新命令：`Hint orvs native`、`Hint orvs all`、`Hint orvs reset`。

## beta 1.1.0 (2026-11-21)

### Release headline / 发布标题

**HintChorus Beta 1.1.0 — “Wait, is it really entering beta?” … “Yep, it really is!”**

HintChorus is now in Beta. Usable on regular production servers, but keep an eye on it.

- Trimmed unnecessary hint-interception paths, and experimentally merged the game's native hints into the composite render.
- Compatibility fallback: run `Hint orvs` in the server console to return the installed hint framework(s) to the vanilla display, one by one (numbered selection when several are found).
- Detailed logging (off by default) records font size, screen position and owning plugin for every line; default logging keeps ordinary events and exceptions only.
- Fixed about **80%** of the known bugs; multiple hint plugins can coexist without conflicts.

Full bilingual release notes: [RELEASE_NOTES.md](RELEASE_NOTES.md)

### 更新速览

HintChorus 进入 Beta —— 可以应用于正常的生产服务器，但仍需观察使用。

- 本次更新**精简了不必要的 Hint 拦截路径**，并**尝试性地把游戏原版 Hint 融入合并渲染**（按目前观测，有较大概率成立）。
- **兼容兜底**：出现不兼容时，可在服务器后台控制台执行 `Hint orvs`，把已安装的 Hint 底层插件逐个退回原版显示。
- 新增**更详细的日志（默认关闭）**：详细记录每一条文字的字号、屏幕位置与所属插件。
- **默认日志**只记录普通事件与异常事件（崩溃检测）。
- 修复了约 **80%** 的已知问题。

> 出问题时：① 控制台 `Hint orvs` 退回可疑插件；② 或直接把 HintChorus 移出/删除后重启。

> 目标游戏版本：当前公开分支（14.2.x）。15.0「Megapatch III」正式发布后需重新核对 6 条 UI 通道的补丁落点。

### 新增 — 控制台回退 `Hint orvs`（大小写不敏感）

某个 Hint 插件与本底层不兼容时，**不用改配置文件、不用卸载插件**，在服务器后台控制台输入：

| 命令 | 作用 |
| --- | --- |
| `Hint orvs` | 列出已发现的 Hint 插件；只有 1 个时直接切回原生；多个时给编号并要求输入 |
| `Hint orvs <编号>` | 把该插件切回**游戏原版提示**（原生直通），其余插件不受影响 |
| `Hint orvs <编号> merge` | 把该插件恢复为合并渲染 |
| `Hint orvs all` | 全部切回原生 |
| `Hint orvs reset` | 全部恢复合并 |

- **支持多个 Hint 插件同时存在且互不冲突**：只有被选中的插件走原生，其它仍由合并层托管。
- 切换只影响本次运行，**不写配置文件**；重启服务器即恢复默认（全部合并）。
- 所有切换都会写入报告日志，便于事后追查。

### 新增 — 报告日志 / UI 位置日志（默认只开崩溃报告）

日志目录：`configs/<端口>/HintChorus/logs/`。

| 配置键 | 默认 | 说明 |
| --- | --- | --- |
| `EnableCrashReportLog` | **开** | 崩溃/异常报告：插件异常、补丁自愈失败、命令异常、启动报告，写入 `report-yyyyMMdd.log` |
| `EnableDetailedUiLog` | **关** | 详细 UI 日志：每个插件的提示内容、最终屏幕位置、每个信口占几行，写入 `ui-yyyyMMdd.log` |
| `DetailedUiLogPerPlayer` | **关** | 逐玩家记录最终合成文本（需同时打开 `EnableDetailedUiLog`），IO 开销最大 |

默认只保留报告日志（崩溃检测）；要看每个插件的文字运行情况，必须去配置文件里**显式打开**详细日志。

### 新增 — 新 Hint 写法（零配置，Beta 起可用）

不修改任何配置文件，一条调用即可独立发提示，自动与其它插件隔离：

```csharp
// 位置字符串（与 {{hc:...}} 同一套语法）
UiIsolation.SendHint("MyPlugin", "你好", 3f, "top-right");

// 九宫格锚点
UiIsolation.SendHint("MyPlugin", "你好", HintAnchor.MiddleCenter, -90f, 3f);

// 清除
UiIsolation.ClearHint("MyPlugin");
```

下一版本会继续沿用这套写法，并把它作为推荐入口；旧写法（`RegisterHintChannel` / `SetHintPosition` / 文本标记 `{{hc:...}}`）保持兼容。

### 删除 / 弃用

- **删除**：`HintBroker.GlobalEffects`（从未被赋值、始终为 `null`，属于遗留占位）。
- **弃用**：`layout_mode: compact` —— 功能仍在，但位置会随内容增减而漂移，后续版本将移除；请改用 `rows` 或 `offsets`。
- **保留**：`HintIsolation.Shim` 兼容垫片（依赖旧程序集名的插件重编后可删）。
- **命名**：发行文件名不再使用 `alpha-` 前缀，改由版本号与发行页标注阶段。

### 回退（出问题时立即执行）

1. 服务器控制台输入 `Hint orvs`，把可疑插件切回原生；或
2. 把 `config.yml` 的 `InterceptThirdPartyUi` 改为 `false` 并重启；或
3. 直接停用/删除 `HintChorus.dll` 与 `0HintChorus.Bootstrap.dll` 后重启，恢复到未安装状态。

### 已知限制

- 详细 UI 日志会周期性写盘，**只建议排障时开启**。
- `Hint orvs` 的回退状态不持久化（重启重置），避免配置被意外写坏。
- 15.0 正式版尚未发布；私测 Beta 期间不保证补丁落点稳定。

### 兼容垫片：HintIsolation.dll（让依赖旧名的插件不必重编）

- **背景**：改名后，凡是**编译时引用了旧程序集名**的插件会整只加载失败 —— 本机 FullModSquad 即如此
  （报 Missing dependencies: HintIsolation v1.0.0.0）。**仅重启不会恢复**。
- **新增** compat/HintIsolation.Shim：产出程序集名仍为 HintIsolation, Version=1.0.0.0 的小垫片，
  只包含依赖方实际用到的 3 个类型（HintIsolation.UiIsolation 的 RegisterHintSource / UnregisterHintSource、
  HintIsolation.Core.Interfaces.IHintTextSource、HintIsolation.Core.Enums.HintAlignment），
  **全部转发到 HintChorus**，自身不含任何逻辑。
- **部署位置**：LabAPI\dependencies\global\HintIsolation.dll（依赖解析目录，不会被当成插件加载）。
- **验证**：本地按加载器同样的解析路径模拟，FullModSquad.GetTypes() 通过（40 个类型）。
- **何时可删**：依赖方改为引用 HintChorus 并重编后，垫片与 compat/ 目录可一并移除。

## alpha (2026-10-07 · 试机修复 + 位置锁定)

### 修复
- **广播补丁装不上**（试机暴露）：Harmony 按**参数名**绑定补丁参数，目标参数名是 data / 	ime / lags，
  而补丁里写成了 message / duration / 	ype → Parameter "message" not found in method ...
  → 两个广播补丁**完全装不上**、自愈 3 次后放弃、广播拦截整体失效。已按真实参数名修正。
- 本地逐个安装验证：**16/16 补丁类全部成功**（修复前 14/16）。

### 变更 — 位置锁定
- **位置一旦固定即锁定**：归属首次出现时分配一个**锁定行号**，此后永不重排 ——
  别的插件出现/消失/内容增减，都不会推动已经落位的行。
- 同一位置区内改为按**固定行高依次占位**，不再按内容高度浮动（不再"叠加"）。
- 摆放只按锁定行号计算，**基准只锚定该区的第 0 行**，与该区当前有多少行无关。
- 未声明位置的插件不参与锁定摆放，仍走底部自然堆叠 —— 既有观感不变。
- HintBroker.Stop() 时复位锁定状态。

### 新增
- 	ools/verify-patches.ps1：本地加载游戏程序集 + 0Harmony，逐个安装补丁并打印最内层异常，失败退出码 1。
  "编译通过但装不上"这类问题编译期完全看不出来，现固化为可重复检查。

### 文档
- docs/position-spec.{zh,en,fr,ru,es}.txt 各补「位置锁定」一节（UN-5 同步）。

## alpha (2026-10-07)

### 更名

- **项目更名为 `HintChorus`**（原名 `HintIsolation` 过于通用、检索易与其它项目混淆）。
  程序集名、根命名空间、引导器（`0HintChorus.Bootstrap.dll`）、配置目录
  （`configs/<端口>/HintChorus/`）、资源逻辑名与 Harmony ID 全部随动。
  **升级提示**：旧的 `HintIsolation.dll` / `0HintIsolation.Bootstrap.dll` 请从 `plugins/global/` 删除，
  旧配置目录 `configs/<端口>/HintIsolation/` 可改名或按新默认重新生成。

### 新增 — 屏幕位置体系（三类写法合一）

- **① 兼容原有写法**：插件文本里已自带位置标签
  （`<voffset>` / `<pos>` / `<align>` / `<line-height>` / `<margin>` / `<indent>`）时，
  判定为「插件自己摆了位」→ **原样放行、不被本底层重排**。开关 `honor_plugin_position_syntax`。
- **② 自有写法（双通道）**：
  - **文本标记**：`{{hc:top-right}}` 或 `{{hc:pos=middle,offset=-90}}` —— 解析后从文本剥离，
    玩家看不到；与其它框架的 `{0}` 模板不冲突。开关 `enable_position_markers`。
  - **C# API**：`UiIsolation.SetHintPosition("MyPlugin", HintAnchor.MiddleCenter, -90f)` /
    `UiIsolation.ClearHintPosition("MyPlugin")`。可在插件加载阶段先声明（信口尚未创建也生效）。
- **③ 自动排版**：未声明位置的插件按**名称**查「已收录表 → 功能区关键词」推断落点，
  例：`exp` / `level` / `rank` / `经验` / `等级` → **屏幕中部再往下 90**；
  `score` / `board` / `排行` → 右上；`kill` / `feed` / `击杀` → 左上；
  `timer` / `倒计时` → 顶部居中；`team` / `队伍` → 中右；`music` / `点歌` → 右下。
  开关 `auto_layout_by_plugin_name`；可用 `position_overrides` 覆盖。

### 生态兼容（2026-10 于 GitHub 实搜并阅读其源码/文档）

- 把生态内框架的**位置写法**接入为兼容对象——从这些框架出来的 UI，位置一动不动：

| 项目 | 其位置写法 | 处理 |
| --- | --- | --- |
| HintServiceMeow ★74 | `Hint.XCoordinate/YCoordinate`（画布单位，半宽 1200）+ `HintAlignment` / `HintVerticalAlign` | 登记「自带位置体系」→ **原样放行** |
| RueI ★24 | **0–1000 纵向标尺**（源码：`baseline = 755 − 2.14 × pos`）+ `VerticalAlign: Up/Center/Down` | 原样放行；自有写法接受同款标尺 |
| ruei-cm-lab | 同一 0–1000 标尺（`offset = 700 − (1000 − pos) × 1.08`）+ 离屏哨兵行固定基线 | 原样放行 |
| UsefulHints ★18 | 配置里直接写原生 TMP 标签 `<align=left><size=28>` | 识别位置标签 → 原样放行 |

- **新增 0–1000 纵向标尺**：自有写法 `{{hc:pos=750}}` / `{{hc:y=500,offset=-90}}` 与 RueI 的
  `Scaled position` 同义（`2.14 × 1000 = 2140`，正是 `screen_height_units` 默认值），迁移数值可照抄。
- 外来位置标签识别名单对齐 HintServiceMeow 标签白名单中的**位置类**标签
  （`voffset` / `pos` / `align` / `line-height` / `line-indent` / `indent` / `margin`）；
  `size` / `color` / `alpha` 等**纯样式**标签不算位置，仍由本底层摆位。
- 对「自带位置」的文本**连对齐也不覆盖**（保持插件自己的 `<align>`）。

### 自有写法 v1（文本标记与 C# API 统一为一套语法）

- **统一语法**：文本标记 `{{hc:...}}` 与 C# `UiIsolation.SetHintPosition(id, "…")` 解析**同一段字符串**，
  插件配置项也可直接存该字符串。解析收敛到 `HintPosition.TryParse` 一处实现。
- **补齐横向维度**：位置由三个维度描述 ——
  **纵向**（九宫格锚点 **或** 0–1000 标尺）、**横向**（`align=left|center|right`，省略则由锚点列推导）、
  **微调**（`offset`，正 = 上移）。
- 新增 C# 字符串重载 `UiIsolation.SetHintPosition(pluginId, string spec)`（无法识别返回 `-1`）；
  `IUiSlot` 暴露只读 `Position` 供插件自检落点。
- **新增规范文档 [`docs/position-spec.zh.txt`](docs/position-spec.zh.txt)**：
  语法表、别名表、示例、解析优先级、配置项、与 RueI / HSM 的对应关系、常见问题。

### 文档 UN-5 语言适配

- **位置写法规范 v1 一次出齐 5 份（UN-5 语言）**：`docs/position-spec.{zh,en,fr,ru,es}.txt`
  —— 语法表、两个通道、示例、解析优先级、配置项、生态对应、FAQ。
- 各语言 README 增补「位置」章节，并链到**对应语言**的规范：
  `docs/en` / `docs/fr` / `docs/ru` / `docs/es`（中文主 README 已有）。
- [`docs/languages.md`](docs/languages.md) 增加「位置写法规范」索引（5 语言）。
- **修复**：`docs/es/README.md` 自加入仓库起一直是 **0 字节**（当初目录嵌套失误，内容落在了
  `docs/es/es/`）。本次补全西班牙语 README。

### 修复：广播补丁装不上（试机暴露）

- **现象**：试机日志报 `安装表面补丁 BroadcastTargetAddElementPatch 失败: Patching exception in method ...`，
  两个广播补丁都装不上 → 自愈每 5s 重试、连续 3 次失败后放弃 → **广播拦截整体失效**。
- **根因**（本地复现取证）：Harmony 是**按参数名**把补丁参数绑定到目标参数的。目标参数名是
  `data` / `time` / `flags`，而补丁里写成了 `message` / `duration` / `type` →
  内层异常 `Parameter "message" not found in method ...`。签名类型其实是对的，是**名字**不匹配。
- **修复**：两个广播补丁的参数名改回与游戏一致，并在源码注释里写死这条约束。

### 新增：`tools/verify-patches.ps1`（防回归）

- 本地加载游戏程序集 + 0Harmony，对插件里每个 `[HarmonyPatch]` 类逐个执行
  `CreateClassProcessor(...).Patch()`，打印最内层异常；失败时退出码 1。
- 同类问题**编译期完全看不出来**，只有真装一次才会暴露 —— 现固化为可重复执行的检查。

### 实现要点

- 新增 `HintPosition`（九宫格锚点 + 偏移 + 自定位标记）、`PositionSyntax`（标记与外来标签解析）、
  `PluginPositionCatalog`（已收录表 + 功能区推断 + 服主覆盖表）。
- `HintBroker` 改为**多锚点分区合成**：按「档位（底/中/顶）+ 偏移」分区，各自换算绝对位置，
  逐行给 `<voffset>`；**底部区永远排在合成串末尾**，因此「全部默认位置」时的输出与旧版**逐字一致**。
- 新增配置：`honor_plugin_position_syntax` / `auto_layout_by_plugin_name` / `enable_position_markers` /
  `screen_height_units` / `position_overrides`。
- 位置解析优先级：**显式 API → 文本标记 → 自带位置标签 → 名称目录 → 默认**。

## alpha (2026-10-05)

### 修复

- **广播补丁签名错误（致命）**：`TargetAddElement(NetworkConnection, string, ushort, BroadcastFlags)` 首参是
  连接、`duration` 是普通 `ushort`（非 ref）。早期补丁写成 `ref ushort time`，签名不匹配导致补丁**静默挂不上**、
  广播拦截整体失效。现按真实签名重写；节流改为"间隔内纯抑制"（非 ref 参数无法压短时长）。
- **SSS `SendToPlayer(hub, collection, version)` 补丁挂不上（致命）**：该重载的 `collection` 是**非 ref 参数**，
  前缀即使声明成 `ref` 也会因签名不匹配静默失效。改为 **Transpiler**（`ldarg.1` → 合并结果），
  EXILED 的 `UserSettings.SendToPlayer(player, settings)` 从此也被覆盖。
- **瞬态提示过期不清理**：瞬态链表按插入顺序排列而 `Until` 由各自时长决定，"后插入更短命"完全可能；
  只看队首剪枝会把短命提示压在长命提示后面一直留着。改为**整链扫描剔除**。
- **`_leeway` 小于整轮刷新周期导致静态内容空屏**：`_leeway` 改为 `Max(_refresh, resendLeeway)`，
  保证续期提前量至少覆盖一个整轮周期。
- **折叠判死误用 `MinRemaining`**：取所有条目（含已过期）的最小值会把"部分过期"的信口误判成空信口
  而退出折叠，让较新内容被同插件另一信口顶掉。改为按 `AliveCount` 判定。
- **同帧归因污染**：归因结果按帧号缓存会把同帧第二个插件的 UI 并入第一个插件的信口（反向还把
  游戏原生提示误判成插件提示）。改为**逐次调用独立解析**，Native 结果不缓存。
- **SSS `Terminate` 残留孤儿设置项**：早期先 `RewriteArrayLocked`（剔除自己+加回自己=原样写回）再清空
  Modules，卸载后设置项残留并越攒越多。改为**只剔除不加回**（`RemoveAllLocked`）。
- **SSS 回调未按模块归属过滤**：插件 A 会收到插件 B 的 setting，只能靠自己在回调里二次过滤。
  现按 `IsSettingOwnedBy` 过滤，订阅者只收到自己注册口的变化。
- **SSS `ForceMergeBeforeSend` 短路恒为假**：`keep.Count == current.Length` 与"全部在场"互斥，
  导致每次下发都触发整数组重建。短路条件只保留"全部在场"。
- **信任名单漏 RueI**：RueI 的调用被当成未知插件归因。`TrustedNames` 补 `RueI`。
- **`Disable()` 未卸载 AssemblyResolve**：热重载后残留解析器。补 `RuntimeHome.UninstallResolver()`。

### 新增 / 变更

- **原生提示多语言自适应（玩家级）**：内嵌 **5 种联合国常用语**（`en` / `zh` / `fr` / `ru` / `es`）——
  开箱即用；其余客户端语言从自宿主 `translations\` 目录加载（GitHub 仓库
  [`docs/translations/`](docs/translations/) 提供全部 **17 种**外部语言文件，下载放入服务器即生效）。
  每个玩家按 `ReferenceHub.playerPreferences.Language` **自动取自己的语言**（反射探测一次并缓存委托，
  失败回退配置默认 `native_hint_language`）；繁体回退简体、逐条回退英文，绝不输出半成品。
- 语言归一化：`zh_Hans` / `zh_Hans-2` / `zh_Flash_Hans` → `zh`；`zh_Hant` 独立键（外部可精确提供繁体）。
- 多语言文档入口：`docs/languages.md`（22 种语言总表 + 翻译文件下载），FR / RU / ES 全译 README。

### 验证

- 仓库工程 `src/` 0 警告 0 错误（含 layout 排版 `rows` / `offsets` / `compact`）。

## alpha (2026-10-04)

### 修复

- **广播内容会被永久丢弃**：被节流的重复广播此前只写入"广播信口"且原下发被取消，而广播信口并不参与合并渲染
  → 玩家什么都看不到。现改为**压短时长后照样放行**（符合"列表追加 → 节流 + 压缩时长"的设计语义）。
- **同一帧内不同插件被归成同一来源**：归因结果此前按帧号做缓存，导致同帧内第二个插件的 UI 被并入第一个插件的信口
  （反向还会把游戏原生提示误判成插件提示）。现改为**逐次调用独立解析**。
- **补丁自愈有两处盲区**：目标改名/消失时被静默跳过，诊断永远显示"自愈 0 次"；且失败后每 5 秒无退避地重装补丁。
  现区分上报"类型/方法不存在"（标为严重）并为自愈加入失败计数与上限。
- **`uiids.yml` 在 UI 活跃期间被反复整份重写**（每 10 秒，且写盘期间持有全局锁；非原子写有截断风险）。
  现拆分为"结构变更"与"命中统计"两种落盘频率，并改为**临时文件 + 原子替换**。
- **持全局锁执行全服下发**：SSS 设置下发被移出锁（锁内只合并数组）。
- **一次性提示被长命提示压住后不会消失**：瞬态队列改为整链剪枝。
- **自发送守卫用共享非原子标志**：并发下可能永久失效 → 改为线程局部（`[ThreadStatic]`）。
- **两处空 `catch`** 会让对讲机守卫/命中标记归因静默变成空操作 → 改为限流上报。
- **`FrameworkCompat.Detect` 不认 `RueI`**：枚举里存在但检测未登记，会被判成 `Unknown`。

### 新增 / 变更

- **排版模式 `layout_mode`：`rows` / `offsets` / `compact`**（`offsets` 见下），取代原先的布尔开关。
- **`offsets` 排版模式**：每行按槽位计算固定目标位置并用 `<voffset>` 修正，缺席的槽位既不占屏也不推动别人。
- **放行时"让位"**：所有无法吸收的提示放行路径都会调用 `HoldSlot`，把"被原生顶掉后不回来"的窗口
  从最久约 2.35 秒压缩到约 40 毫秒。
- **空文本提示直接吞掉**（此前放行会把合并块清成空白块）。
- 诊断报告新增"补丁落点已找不到目标"与"自愈连续失败"两项。
- `layout_mode` 等新键刻意使用**字符串 + 容错解析**：配置里的枚举一旦拼错，加载器会丢弃整份配置回落默认值。

### 验证

- 排版不变量：`rows` 模式 18 项、`offsets` 模式 11 项断言（离线可重跑）。
- 拦截目标：16 个目标逐条核验通过（见 `docs/patch-target-report.txt`）。
- 默认配置：66 项由编译产物实际序列化得到（`docs/default-config.yml`）。

### 已知限制

- 允许被放行的提示仍会短暂接管提示口（单通道的物理上限）；已把窗口压到约 40 毫秒。
- 预留行/槽位的总量上限同时限制"位置稳定性"：归属数超过上限后，稳定性会退化。
- 与自行接管提示显示的框架互斥（见 README）。