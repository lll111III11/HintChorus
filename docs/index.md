---
title: HintChorus
---

# HintChorus

**SCP:SL 动态 UI 隔离底层** —— 让互不相识的插件共用同一块屏幕，插件无需改写法。

> Dynamic UI isolation layer for SCP:SL — one independent hint slot per caller, no plugin changes needed.

> **Beta 1.1.1 — If another hint plugin jumps the queue, it gets kicked back out.**
>
> HintChorus is in Beta: usable on regular production servers, but keep an eye on it. `HintDisplay.Show` now runs at Harmony `int.MaxValue` priority with a postfix fallback, so a foreign prefix that swallows the call no longer leaves the screen blank. Third-party hints are snapshotted before absorption and can be replayed per player. `Hint orvs` detects mainstream plugins: if none exist it offers option **1** = restore vanilla + replay; otherwise it lists them by number and hot-applies the chosen plugin's last data.
> If something breaks, run `Hint orvs native` or remove/delete HintChorus.
>
> **HintChorus 1.1.1：敢站它前面的，直接一脚踹掉。** 已可应用于正常的生产服务器，但仍需观察使用。强接管 + 覆盖前快照；`Hint orvs` 先检测主流插件，没有就提供编号 1 直接退回原生并恢复覆盖前数据；有就按编号单独回退并热应用其最后数据。

## 语言 / Language

- **中文（主）**：[完整技术文档](zh/) ｜ [仓库首页](https://github.com/lll111III11/HintChorus)
- **English**: [README](en/) ｜ [Repository](https://github.com/lll111III11/HintChorus)

## 下载 / Download

- [HintChorus.dll](https://github.com/lll111III11/HintChorus/releases/latest)

## 文档 / Docs

- [默认配置 default-config.yml](default-config.yml)
- [补丁目标核验报告 patch-target-report.txt](patch-target-report.txt)