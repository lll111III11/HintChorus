---
title: HintChorus
---

# HintChorus

**SCP:SL 动态 UI 隔离底层** —— 让互不相识的插件共用同一块屏幕，插件无需改写法。

> Dynamic UI isolation layer for SCP:SL — one independent hint slot per caller, no plugin changes needed.

> **Beta 1.1.2 — Patch Arbiter, Snapshot Rollback & Takeover**
>
> HintChorus is in Beta: usable on regular production servers, but keep an eye on it. It now runs its own patch middle layer: Harmony's five uninstall entry points are instrumented, so when another framework clears a method with `Unpatch All`, HintChorus restores its patches synchronously in the same call stack and re-sends the UI; a 0.25 s shield remains as a backstop. Third-party hints are snapshotted per player before absorption, and when another hint plugin modifies the channel after us, HintChorus rolls it back through the snapshot and re-takes over. `Hint orvs` detects mainstream plugins: if none exist it offers option **1** = restore vanilla + replay; otherwise it lists them by number and hot-applies the chosen plugin's last data.
> If something breaks, run `Hint orvs native` or remove/delete HintChorus.
>
> **HintChorus 1.1.2 版本更新：补丁仲裁层 + 快照拦回。** 不再只靠优先级：Harmony 的 5 个卸载入口全部受控，外部用 `Unpatch All` 清场后，本底层补丁会在同一个调用栈内恢复并重发 UI，护盾每 0.25 秒兜底。第三方提示在被吸收前先按玩家记录快照；外部 Hint 插件在我们之后修改提示通道时，通过快照退回并重新接管。`Hint orvs` 会先检测主流插件：没有就提供编号 1 退回原生并恢复覆盖前数据。

## 简洁 / 详细 (Concise / Detailed)

> **语言说明 / Language note**：运行时只内嵌联合国五种语言（`zh` / `en` / `fr` / `ru` / `es`）；其余 20+ 语言以外部译文与文档形式提供，缺行回退英文。 / The runtime embeds only the five UN languages; the other 20+ languages are external translations/docs and fall back to English when a line is missing.

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