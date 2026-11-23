# 外部译文 / External translations

运行时只内嵌联合国五种语言：`en` / `zh` / `fr` / `ru` / `es`。

本目录下的 17 个 `<语言代码>.txt` 是其余语言的外部译文：

- 放到 `configs/<端口>/HintChorus/translations/` 后按玩家客户端语言加载；
- 缺行时逐条回退英文，绝不输出半成品；
- 文档与译文和中文/英文同步推进；新增字符串先补齐 `en` + `zh`，再同步 `fr` / `ru` / `es` 与其它语言；
- 运行时不会内嵌这些语言，也不会声称已内置。

The runtime embeds only the five UN languages (`en` / `zh` / `fr` / `ru` / `es`).
The 17 `<lang>.txt` files here are external translations loaded per player from
`configs/<port>/HintChorus/translations/`; missing lines fall back to English.
Docs and translations are kept in sync with Chinese/English, but the runtime
does not embed these languages.
