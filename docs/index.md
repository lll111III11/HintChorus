---
title: HintChorus
---

# HintChorus

**SCP:SL 动态 UI 隔离底层** —— 让互不相识的插件共用同一块屏幕，插件无需改写法。

> Dynamic UI isolation layer for SCP:SL — one independent hint slot per caller, no plugin changes needed.

> **Beta 1.1.0 — “Wait, is it really entering beta?” … “Yep, it really is!”**
>
> HintChorus is now in Beta: usable on regular production servers, but keep an eye on it. Trimmed unnecessary hint-interception paths and experimentally merged native hints into the composite render. New `Hint orvs` console fallback, detailed UI log (off by default), about 80% of known bugs fixed. Multiple hint plugins can coexist without conflicts.
> If something breaks, run `Hint orvs` or remove/delete HintChorus.
>
> **HintChorus 要进入 Beta 了，真的假的？我去，真的！** 已可应用于正常的生产服务器，但仍需观察使用；出问题时用 `Hint orvs` 退回，或直接移出/删除 HintChorus。

## 语言 / Language

- **中文（主）**：[完整技术文档](zh/) ｜ [仓库首页](https://github.com/lll111III11/HintChorus)
- **English**: [README](en/) ｜ [Repository](https://github.com/lll111III11/HintChorus)

## 下载 / Download

- [HintChorus.dll](https://github.com/lll111III11/HintChorus/releases/latest)

## 文档 / Docs

- [默认配置 default-config.yml](default-config.yml)
- [补丁目标核验报告 patch-target-report.txt](patch-target-report.txt)