---
title: HintChorus
---

# HintChorus

**SCP:SL 动态 UI 隔离底层** —— 让互不相识的插件共用同一块屏幕，插件无需改写法。

> Dynamic UI isolation layer for SCP:SL — one independent hint slot per caller, no plugin changes needed.

> **15.0.0 — UI Middle Layer & Real Language Packs**
>
> HintChorus is in Beta: usable on regular production servers, but keep an eye on it. Every runtime string now comes from embedded language packs (`en/zh/fr/ru/es`, 193 keys each); external `configs/<port>/HintChorus/lang/<code>.lang` packs override any key and can add any language, with per-key fallback. `Hint lang reload` hot-reloads. The new [15.0-hint-internals.md](15.0-hint-internals.md) documents the full server hint pipeline: `HintDisplay.Show` → `HintMessage`, the byte-level wire format, 20 parameter types, 3 effect types and the official `SuppressedReceivers` mute switch. Patch arbiter, snapshot rollback, shield and `Hint orvs` remain unchanged.
> If something breaks, run `Hint orvs native` or remove/delete HintChorus.
>
> **HintChorus 15.0.0 版本更新：真正的语言包 + UI 中间层。** 所有运行时文案进入内嵌语言包（5 语 × 193 key）；外部 `lang/<code>.lang` 可逐 key 覆盖、可新增任意语言，`Hint lang reload` 热重载。新增《服务端提示管道解剖》，把 `HintDisplay.Show` → `HintMessage` 的线格式、20 种参数、3 种效果与官方静音开关全部刨清。补丁仲裁、快照拦回、护盾与 `Hint orvs` 回退保持不变。

## 简洁 / 详细 (Concise / Detailed)

> **语言说明 / Language note**：DLL 内嵌 5 个运行时语言包（`zh` / `en` / `fr` / `ru` / `es`）；任意语言都可用 `configs/<端口>/HintChorus/lang/<code>.lang` 扩展，逐 key 覆盖，缺行回退英文/中文。 / The DLL embeds five runtime language packs; any language can be added with `configs/<port>/HintChorus/lang/<code>.lang`, overriding keys one by one and falling back per line.

- **简洁 / Concise**：单条提示通道拆成按调用方隔离的信口，零改码；内置补丁仲裁、`0.dll` 抢加载与 `Hint orvs` 回退。 / One hint channel becomes per-caller isolated slots with zero code changes; patch arbitration, a load-first `0.dll` and `Hint orvs` rollback are built in.
- **详细 / Detailed**：拦截 Hint、广播、控制台、CASSIE、管理端聊天与命中标记六条通道，按调用方归因并分配独立信口；外部清场后同步恢复补丁；首次运行配置向导与安全更新检查已内置。 / Intercepts hints, broadcasts, console, CASSIE, admin chat and hit markers, attributes each call and gives it an independent slot; patches are restored synchronously after external clears; a first-run setup wizard and safe update check are included.

## 语言 / Language

- **中文（主）**：[完整技术文档](zh/) ｜ [仓库首页](https://github.com/lll111III11/HintChorus)
- **English**: [README](en/) ｜ [Repository](https://github.com/lll111III11/HintChorus)

## 下载 / Download

- [HintChorus.dll](https://github.com/lll111III11/HintChorus/releases/latest)

## 文档 / Docs

- [默认配置 default-config.yml](default-config.yml)
- [补丁目标核验报告 patch-target-report.txt](patch-target-report.txt)