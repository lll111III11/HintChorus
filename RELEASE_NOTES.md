# HintChorus 14.2.9.4 — 50 ms Collection, UI Retention, Security & System Snapshots

## 中文

- **50ms 收集窗口**：`hint_refresh_interval` 默认改为 `0.05`；窗口内所有请求收集后合并成一份，每个玩家只发一次，内容没变不发。
- **新旧 UI 保留时间**：`min_ui_retain_seconds`（默认 60）/ `max_ui_retain_seconds`（默认 180），旧内容至少留 60 秒、最多停留 180 秒。
- **主动刷新**：`allow_active_refresh` + `Hint refresh`，跳过窗口立即刷新。
- **安全配置**：`enable_network_security` 与提示 / 广播 / CASSIE / RPC 四类包的放行开关；`close_rich_text_tags` 自动补齐富文本闭合标签，防止颜色与字号泄漏到控制台。
- **系统快照（Beta）**：每小时、最多 72 份、启动先存一份；包含配置、插件主体与依赖；`Hint snap list|create|restore|restart` 回溯后提示重启。
- **⚠️ 快照注意**：含插件 DLL 时单份约 30 MB，满额约 2 GB+（控制台会自动告警）；回溯后**必须重启**才生效；正在运行的 `HintChorus.dll` 会被跳过，重启后手动替换。

## English

- **50 ms collection window**: `hint_refresh_interval` now defaults to `0.05`; requests inside the window are merged and sent once per player, and nothing is sent when the content did not change.
- **UI retention**: `min_ui_retain_seconds` (60) / `max_ui_retain_seconds` (180) keep old content for at least a minute and never longer than three.
- **Active refresh**: `allow_active_refresh` plus `Hint refresh` to skip the window and redraw immediately.
- **Security**: `enable_network_security` with per-packet allow switches for hint / broadcast / CASSIE / RPC, and `close_rich_text_tags` to auto-close rich-text tags so colours and sizes cannot leak into the console.
- **System snapshots (Beta)**: hourly, max 72, one taken at startup; includes configs, plugin assemblies and dependencies; `Hint snap list|create|restore|restart` restores files and prompts for a restart.
- **⚠️ Snapshot notice**: with plugin DLLs each snapshot is ~30 MB, up to ~2 GB+ when full (the console warns automatically); a restore only writes files and **requires a restart**; the running `HintChorus.dll` is skipped and must be replaced manually after restarting.

---

# HintChorus 14.2.9.3 — Pre-op Enforcement After Late-Loading Frameworks

## 中文

- **预更新实操作持续生效。** `PreOpTakeover: true` 或 `Hint preop on` 生效期间，每 5 秒幂等复查一次，把插件启用后才加载的第三方 Hint 框架（HintServiceMeow / RueI 等）补上的 Harmony 补丁继续摘除。
- 修复：插件先于第三方框架启用时，首次 Engage 只能摘到 0 组补丁。
- **实机验证**：启动 7 秒摘除 2 组，第二次复查再摘 3 组（框架自愈重挂），之后稳定，无异常。

## English

- **Pre-op takeover now keeps enforcing.** While `PreOpTakeover: true` (or `Hint preop on`) is active, a 5-second idempotent sweep detaches Harmony patches that third-party hint frameworks (HintServiceMeow / RueI) install *after* this plugin was enabled.
- Fixes the case where the first Engage ran before those frameworks loaded and detached 0 patch groups.
- **Verified on a live server**: 2 groups detached 7 s after boot, 3 more on the next sweep (frameworks had self-repatched), then stable with no errors.

---

# HintChorus 14.2.9.1 — Pre-op Takeover & Full UI Packet Management

## 中文

- **预更新实操作。** 输入 `Hint preop`（或配置 `PreOpTakeover: true`）后：摘掉其它 Hint 框架挂在 `HintDisplay.Show` / LabAPI `Player.SendHint` 上的补丁，开启全网络包管理，并按快照瞬间重发每个玩家的 UI。
- **全网络包管理**：Mirror 汇流点上的提示 / 广播 / CASSIE 包只放行本底层自己发出的那一份；其它插件直发的包在内存中被判为无效并丢弃，改动不会到达客户端。
- 只影响本次运行，不写配置；`Hint preop off` 关闭过滤，重启后其它框架补丁自然恢复。
- 向导新增第 5 项“是否保留 HintChorus 并执行全接管”；详细配置顺延为第 6 项。

## English

- **Pre-op takeover.** `Hint preop` (or `PreOpTakeover: true`) detaches other hint frameworks' patches on `HintDisplay.Show` / LabAPI `Player.SendHint`, enables full packet management, and instantly re-sends every player's UI from the snapshot.
- **Full UI packet management**: at the Mirror send point, hint / broadcast / CASSIE packets are only allowed from this layer; packets sent directly by other plugins are voided in memory and never reach the client.
- Runtime only, never written to config. `Hint preop off` stops the filter; detached foreign patches come back after a restart.
- Setup wizard gained item 5 "keep HintChorus with full takeover"; detailed configuration moved to item 6.

---

# HintChorus 14.2.9.0 — Version Aligned with SCP:SL

## 中文

- **版本号与游戏对齐。** 此后版本为 `<游戏 Major>.<游戏 Minor>.<游戏 Revision>.<插件构建号>`；当前游戏 SCP:SL 14.2.9，本期发布即 `14.2.9.0`。游戏更新后前三段跟随新版本、第四段从 0 重新计数。
- 旧的 `15.0.x` 版本号仅作历史保留，不再沿用。
- **更新检查按四位版本比较**：同游戏版本下第 4 位构建号不再被截断，避免误判“已是最新”。
- **启动报告新增游戏版本行**：`游戏: SCP:SL 14.2.9`，与插件版本并列显示。

## English

- **Version now follows the game.** Releases use `<game major>.<game minor>.<game revision>.<plugin build>`; the current game is SCP:SL 14.2.9, so this release is `14.2.9.0`. When the game updates, the first three parts follow it and the build counter restarts at 0.
- Older `15.0.x` numbers remain for history only.
- **Update check compares all four parts**, so a higher plugin build within the same game version is no longer reported as "already latest".
- **Startup report adds a game version line**: `game: SCP:SL 14.2.9` next to the plugin version.

---

# HintChorus 15.0.2 — Detailed Config Wizard & Auto Compatibility Mode

## 中文

- **第 5 项：详细配置向导。** 首次运行向导在第 4 项之后新增“是否开启详细的所有配置文件设置”；选择“是”后，除前 4 项外的所有配置参数逐个询问，显示参数名、类型、当前值、默认值与说明。
- 详细配置支持：`Hc setup default`（剩余全部设为默认）、`Hc setup skip`（跳过剩余）、`Hc setup save`（保存配置与进度，下次启动从该参数继续）。
- 支持 bool / int / float / byte / ushort / string / 枚举 / `Dictionary<string,string>`；`PositionOverrides` 用 `Key=Value;Key2=Value2` 输入，留空或 `default` 清空。
- **兼容模式三态 `CompatMode`**：`Auto`=检测到主流 Hint 插件时自动切原生直通（默认）；`Notify`=只提醒不切换；`Off`=关闭检测。兼容检测下若仍出现外部清场，控制台会提示 `Hint orvs` 查看编号、`Hint orvs <编号>` 单插件回退、`Hint orvs native` 整体退回。
- **配置项变化自动重弹向导。** `setup.state` 记录 `ConfigSchema` 指纹；新增/删除/改名/改类型配置项时自动重新进入向导。

## English

- **Step 5: detailed config wizard.** After the first four items, the first-run wizard asks whether to walk through every remaining config parameter one by one, showing name, type, current value, default and description.
- Detailed mode supports `Hc setup default` (all remaining to defaults), `Hc setup skip` (skip the rest), and `Hc setup save` (save config and progress; resumes at that parameter on the next start).
- Supports bool / int / float / byte / ushort / string / enums / `Dictionary<string,string>`; `PositionOverrides` accepts `Key=Value;Key2=Value2`, empty or `default` clears it.
- **Three-state `CompatMode`**: `Auto` switches detected mainstream hint plugins to native pass-through (default); `Notify` only warns; `Off` disables detection. If external patch clearing persists, the console advises `Hint orvs`, `Hint orvs <n>`, or `Hint orvs native`.
- **Config changes reopen the wizard.** `setup.state` stores a `ConfigSchema` fingerprint, so adding, removing, renaming or retyping a config key reopens the wizard automatically.

---

# HintChorus 15.0.1 — Console-only HC & Step-by-Step Setup

## 中文

- `HC` 只注册到服务器后台控制台；RA 面板、查询接口与玩家客户端不再注册，也不会再出现“只允许服务器终端”的误导提示。
- 服务器终端判定放宽：`GameCore.ConsoleCommandSender`、`GAME CONSOLE` 或 `FullPermissions` 均放行。
- 首次运行向导改为逐项询问：一次只显示当前一项，回答“是/否”后才显示下一项；顺序为自动更新 → 详细 UI 日志 → RNR → 是否写入配置。
- 支持 `Hc setup 是|否`、`Hc 是|否`，以及 `Hc setup <1-4> on|off` 指定单项。
- 语言包与文档同步（5 语 × 195 key）。

## English

- `HC` is now registered only on the server console. The Remote Admin panel, query interface and player client consoles no longer expose it, so the misleading "server console only" reply is gone.
- Server console detection also accepts `FullPermissions`, so a real terminal is never rejected by mistake.
- The first-run wizard asks one item at a time and shows the next only after a yes/no answer. Order: auto update → detailed UI log → RNR → write config.
- `Hc setup yes|no`, `Hc yes|no`, and `Hc setup <1-4> on|off` are supported.
- Language packs and docs updated (5 languages × 195 keys).

---

# HintChorus 15.0.0 — UI Middle Layer & Real Language Packs

> 发布标题 / Release title:
> **HintChorus 15.0.0 — UI Middle Layer & Real Language Packs**
>
> 中文副标题 / Chinese subtitle:
> **HintChorus 15.0.0：真正的语言包 + UI 中间层**

---

## English (primary)

**Beta status** — HintChorus is still Beta. Usable on regular production servers, but keep an eye on it. If anything goes wrong, run `Hint orvs native`, or remove/delete HintChorus and restart.

### Real language packs

Every user-facing runtime string is now loaded from language packs instead of being hardcoded:

- `src/Resources/langpack/{en,zh,fr,ru,es}.lang` — **5 languages × 193 keys**, embedded into `HintChorus.dll`.
- Covers the first-run wizard, `Hint` command help/responses, update messages, template bridge, patch arbiter/self-heal/shield, merge broker, SSS registry, text metrics and the language command.
- `configs/<port>/HintChorus/lang/<code>.lang` overrides the embedded values **key by key** — no rebuild, no code change.
- Any language is supported: drop `de.lang`, `pt_BR.lang`, etc. Missing keys fall back in order: requested language → base language (`pt_BR` → `pt`) → English → Chinese.
- `Hint lang` lists packs, key counts and sources; `Hint lang reload` hot-reloads; `Hint lang path` prints the directory.
- `ServerLocale.Normalize` no longer forces unknown languages to English; the pack decides whether a translation exists, then falls back per key.

### Hint pipeline anatomy

New document: [docs/15.0-hint-internals.md](docs/15.0-hint-internals.md)

- Full call chain: `HintDisplay.Show` → `HintMessage` → Mirror → client TMP.
- Byte-level wire format: hint type byte, duration scalar, effect array, parameter array; `TextHint` / `TranslationHint` payloads.
- All 20 `HintParameter` types and 3 `HintEffect` types, including which ones are client-localized and which are dynamic (countdown).
- `HintDisplay.SuppressedReceivers` — the official per-connection mute switch, as an alternative to patch races.
- 15.0 verification checklist (`ilspycmd` commands) to run on release day.

### Also in this release

- Startup report now includes a language-pack summary (`key count` + `builtin`/`external`/`both` per language).
- **Bootstrap release fixed.** On Windows the running `0.dll` / `HintChorus.dll` are user-mapped and cannot be overwritten in-process. The installer now compares assembly versions, skips same-version rebuilds, and stages `0.dll.new` / `HintChorus.Mapping.dll.new` with a clear stop-and-replace message when an update is genuinely needed. All three projects now build deterministically.
- **Single-file distribution + permanent shim.** The main DLL embeds the permanent shim, bootstrap payload and mapping. On first run it extracts everything and asks for one restart; after restart `0.dll` (fixed 1.0.0) loads `HintChorus.Bootstrap.payload` and the payload performs the early install. Future updates only replace the payload; `0.dll` never changes and no `.new` files need to be moved by hand.
- Version aligned to **15.0.0**; existing patch arbiter, snapshot rollback, 0.25 s shield, load-first `0.dll` and SSS isolation are unchanged.
- Defaults are unchanged: third-party interception stays off by default; any failure degrades instead of blocking plugin load.

### Upgrade

- Existing `config.yml` works as-is. `Language` still accepts `auto` / `zh` / `en` / `fr` / `ru` / `es`, and now also any external pack code.
- To change a line: copy the embedded pack to `configs/<port>/HintChorus/lang/<code>.lang`, keep only the lines you want to override, then run `Hint lang reload`.

### Rollback

- Console: `Hint orvs` / `Hint orvs native` / `Hint orvs reset`.
- Or remove/delete `HintChorus.dll` (and `0HintChorus.Bootstrap.dll`) and restart the server.

> Release asset: **`HintChorus.dll`** (15.0.0.0).

---

## 中文

**Beta 状态** —— HintChorus 仍是 Beta：可以进正常生产服务器，但要盯着。出问题时先执行 `Hint orvs native`，或删除 HintChorus 后重启。

### 真正的语言包

所有用户可见的运行时文案不再硬编码，统一从语言包读取：

- `src/Resources/langpack/{en,zh,fr,ru,es}.lang` —— **5 语 × 193 key**，编进 `HintChorus.dll`。
- 覆盖首次运行向导、`Hint` 命令帮助与回复、更新提示、模板桥、补丁仲裁/自愈/护盾、合并核心、SSS 端口、文字度量与语言命令。
- `configs/<端口>/HintChorus/lang/<code>.lang` **逐 key** 覆盖内嵌值，不需要重新编译，也不需要改代码。
- 任意语言都支持：放入 `de.lang`、`pt_BR.lang` 等即可。缺失 key 按「请求语言 → 语言主代码（`pt_BR` → `pt`）→ 英文 → 中文」逐条回退。
- `Hint lang` 查看语言包、key 数量与来源；`Hint lang reload` 热重载；`Hint lang path` 显示目录。
- `ServerLocale.Normalize` 不再把未知语言强制改成英文；有没有译文由语言包决定，再逐条回退。

### 提示管道解剖

新增文档：[docs/15.0-hint-internals.md](docs/15.0-hint-internals.md)

- 完整调用链：`HintDisplay.Show` → `HintMessage` → Mirror → 客户端 TMP。
- 逐字节线格式：提示类型、时长倍率、效果数组、参数数组；`TextHint` / `TranslationHint` 负载。
- 20 种 `HintParameter` 与 3 种 `HintEffect`，标出哪些是客户端本地化、哪些是动态倒计时。
- `HintDisplay.SuppressedReceivers`：官方按连接静音开关，可作为补丁竞争之外的另一条路。
- 15.0 发布当天核对清单（`ilspycmd` 命令）。

### 本次还包括

- 启动报告新增语言包摘要（每个语言的 key 数与 `builtin`/`external`/`both` 来源）。
- **修复引导器释放失败**：Windows 上正在运行的 `0.dll` / `HintChorus.dll` 是 user-mapped，进程内无法覆盖。现在按程序集版本判断：同版本不重写；确实需要更新但文件被占用时，暂存为 `0.dll.new` / `HintChorus.Mapping.dll.new` 并提示停服替换。三个工程都改为 deterministic 构建，同一份源码重建字节稳定。
- **单文件发放 + 永久 shim**：主 DLL 内嵌永久 shim、引导 payload 与映射组件。首次运行全部释放，提示重启一次；重启后 `0.dll`（固定 1.0.0）载入 `HintChorus.Bootstrap.payload`，payload 再做抢先安装。以后更新只替换 payload，`0.dll` 永远不动，不再需要手动搬 `.new`。
- 版本号对齐 **15.0.0**；既有补丁仲裁、快照拦回、0.25 秒护盾、抢加载 `0.dll`、SSS 端口隔离保持不变。
- 默认值不变：第三方拦截默认关闭；任何失败只降级，不阻止插件加载。

### 升级

- 旧 `config.yml` 直接可用；`Language` 仍支持 `auto` / `zh` / `en` / `fr` / `ru` / `es`，现在也接受任意外部语言包代码。
- 想改文案：把内嵌语言包复制到 `configs/<端口>/HintChorus/lang/<code>.lang`，只保留要覆盖的行，然后 `Hint lang reload`。

### 回退

- 控制台：`Hint orvs` / `Hint orvs native` / `Hint orvs reset`。
- 或删除 `HintChorus.dll`（以及 `0HintChorus.Bootstrap.dll`）后重启服务器。

> 本次发布资产请使用 **`HintChorus.dll`**（15.0.0.0）。
