---
title: HintChorus
---

# HintChorus

**SCP:SL 动态 UI 隔离底层** —— 让互不相识的插件共用同一块屏幕，插件无需改写法。

> Dynamic UI isolation layer for SCP:SL — one independent hint slot per caller, no plugin changes needed.

> **Beta 1.1.1 — Snapshot Rollback & Takeover**
>
> HintChorus is in Beta: usable on regular production servers, but keep an eye on it. Third-party hints are snapshotted per player before absorption. When another hint plugin modifies the hint channel after us, HintChorus rolls it back through the snapshot and re-takes over — either by running at the highest Harmony priority first, or by a postfix fallback when the prefix was skipped. `Hint orvs` detects mainstream plugins: if none exist it offers option **1** = restore vanilla + replay; otherwise it lists them by number and hot-applies the chosen plugin's last data.
> If something breaks, run `Hint orvs native` or remove/delete HintChorus.
>
> **HintChorus 1.1.1 版本更新：新增「快照拦回」。** 第三方提示在被吸收前先按玩家记录快照；外部 Hint 插件在我们之后修改提示通道时，通过快照退回并重新接管。实现分两种：抢占在先（最高优先级，抢在主流插件之前接管）与事后拦回（对方已抢先时按快照退回再整体接管）。`Hint orvs` 会先检测主流插件：没有就提供编号 1 退回原生并恢复覆盖前数据。

## 语言 / Language

- **中文（主）**：[完整技术文档](zh/) ｜ [仓库首页](https://github.com/lll111III11/HintChorus)
- **English**: [README](en/) ｜ [Repository](https://github.com/lll111III11/HintChorus)

## 下载 / Download

- [HintChorus.dll](https://github.com/lll111III11/HintChorus/releases/latest)

## 文档 / Docs

- [默认配置 default-config.yml](default-config.yml)
- [补丁目标核验报告 patch-target-report.txt](patch-target-report.txt)