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
