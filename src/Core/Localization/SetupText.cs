using System;
using System.Collections.Generic;

namespace HintChorus.Core.Localization;

/// <summary>配置向导与更新提示的多语言文本。支持 zh / en / fr / ru / es, 其它语言回退英文。</summary>
internal static class SetupText
{
    private static readonly Dictionary<string, Dictionary<string, string>> Texts =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["banner"] = "HintChorus 首次运行配置向导",
                ["detected"] = "检测到: 语言={0} | 时区={1} | 地区={2}",
                ["detectedExtra"] = "时区来源: {0} | 时间校验: {1}",
                ["srcNetwork"] = "公网 IP + 网络时区",
                ["srcLocal"] = "本地时区",
                ["clockOk"] = "本地时间可信",
                ["clockSkew"] = "本地时间偏差 {0} 秒",
                ["q1"] = "1) 自动更新: {0}    (Hc setup 1 on / off)",
                ["q2"] = "2) 详细 UI 日志: {0}    (Hc setup 2 on / off)",
                ["q3"] = "3) 现在写入配置文件? {0}    (Hc setup 3 yes / no)",
                ["q4"] = "4) 更新后回合结束重启(RNR): {0}    (Hc setup 4 on / off)",
                ["desc1"] = "自动更新: 从 GitHub Releases 检查新版本; 开启后自动下载, 默认只保存为 HintChorus.dll.new。",
                ["desc2"] = "详细 UI 日志: 记录每个插件的提示内容、屏幕位置与每玩家合成结果, 写入 logs/ui-*.log; IO 开销明显。",
                ["desc3"] = "写入配置: 把当前选择保存到 config.yml, 下次启动继续生效。",
                ["desc4"] = "更新后重启(RNR): 下载完成并替换 DLL 后, 等当前回合结束再彻底重启服务器, 加载新版本。",
                ["commands"] = "Hc setup done 完成 | Hc setup skip 跳过",
                ["on"] = "开",
                ["off"] = "关",
                ["yes"] = "是",
                ["no"] = "否",
                ["saved"] = "已写入配置: {0}",
                ["skipped"] = "已跳过, 本次不做修改。",
                ["done"] = "配置向导完成。",
                ["restart"] = "详细日志等设置会在下次重启后完全生效。",
                ["notActive"] = "配置向导未在运行(已完成或尚未开始)。",
                ["usage"] = "用法: Hc setup | Hc setup 1 on|off | Hc setup 2 on|off | Hc setup 3 yes|no | Hc setup 4 on|off | Hc setup done | Hc setup skip",
                ["helpHeader"] = "[HintChorus] 控制台命令(大小写不敏感):",
                ["helpOrvs"] = "Hint orvs — 检测主流插件并列出编号(无主流时编号 1 = 退回原生)",
                ["helpPick"] = "Hint orvs <编号> — 把该插件切回原生直通, 并热应用其最后一条数据",
                ["helpMerge"] = "Hint orvs <编号> merge — 恢复该插件为合并渲染",
                ["helpNative"] = "Hint orvs native — 整体退回原生, 恢复覆盖前数据",
                ["helpAll"] = "Hint orvs all — 全部退回原生并恢复覆盖前数据",
                ["helpReset"] = "Hint orvs reset — 退出原生模式, 全部恢复合并",
                ["helpYield"] = "Hint orvs yield — 让出提示通道, 允许外部 Hint 插件接管(仅本次运行)",
                ["helpTake"] = "Hint orvs take — 收回提示通道, 恢复本底层接管(默认)",
                ["helpSetup"] = "Hc setup — 首次运行配置向导(自动更新/详细日志/写入配置/RNR)",
                ["helpUpdate"] = "Hc update — 立即检查 GitHub 最新版本",
                ["helpFooter"] = "说明: 支持多个 Hint 插件共存; 某个插件出问题时用 orvs 单独回退, 其余插件不受影响。",
                ["updateStarted"] = "已开始检查更新, 结果见控制台日志。",
                ["updateFound"] = "发现新版本: {0} (当前 {1})",
                ["updateNone"] = "已是最新版本: {0}",
                ["updateFailed"] = "检查更新失败: {0}",
                ["updateDownloaded"] = "已下载新版本到 {0}; 重启前替换正在使用的 DLL。",
                ["updateReplaced"] = "已替换 HintChorus.dll; 将在回合结束后重启加载新版本。",
                ["updateReplaceFailed"] = "无法替换正在使用的 DLL(可能被占用); 已保留 .new, 请手动替换。",
                ["rnrScheduled"] = "已安排在本次回合结束后重启服务器, 加载新版本。"
            },
            ["en"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["banner"] = "HintChorus first-run setup",
                ["detected"] = "Detected: language={0} | timezone={1} | region={2}",
                ["detectedExtra"] = "Time zone source: {0} | Clock check: {1}",
                ["srcNetwork"] = "public IP + network time zone",
                ["srcLocal"] = "local time zone",
                ["clockOk"] = "local clock trusted",
                ["clockSkew"] = "local clock off by {0}s",
                ["q1"] = "1) Auto update: {0}    (Hc setup 1 on / off)",
                ["q2"] = "2) Detailed UI log: {0}    (Hc setup 2 on / off)",
                ["q3"] = "3) Write config now? {0}    (Hc setup 3 yes / no)",
                ["q4"] = "4) Restart after round when updated (RNR): {0}    (Hc setup 4 on / off)",
                ["desc1"] = "Auto update: checks GitHub Releases; when enabled it downloads automatically, but only saves HintChorus.dll.new by default.",
                ["desc2"] = "Detailed UI log: records each plugin's text, screen position and per-player composite into logs/ui-*.log; noticeable IO cost.",
                ["desc3"] = "Write config: saves the current choices to config.yml so they persist across restarts.",
                ["desc4"] = "Restart after round (RNR): after the update is downloaded and the DLL is replaced, fully restart the server at the end of the current round to load it.",
                ["commands"] = "Hc setup done to finish | Hc setup skip to skip",
                ["on"] = "on",
                ["off"] = "off",
                ["yes"] = "yes",
                ["no"] = "no",
                ["saved"] = "Config saved: {0}",
                ["skipped"] = "Skipped. Nothing was changed.",
                ["done"] = "Setup complete.",
                ["restart"] = "Detailed log and similar settings take full effect after restart.",
                ["notActive"] = "The setup wizard is not running (already done or not started).",
                ["usage"] = "Usage: Hc setup | Hc setup 1 on|off | Hc setup 2 on|off | Hc setup 3 yes|no | Hc setup 4 on|off | Hc setup done | Hc setup skip",
                ["helpHeader"] = "[HintChorus] console commands (case-insensitive):",
                ["helpOrvs"] = "Hint orvs — detect mainstream plugins and list numbers (option 1 = native when none)",
                ["helpPick"] = "Hint orvs <n> — switch that plugin to native and replay its last data",
                ["helpMerge"] = "Hint orvs <n> merge — restore that plugin to merged rendering",
                ["helpNative"] = "Hint orvs native — switch everything to native and replay pre-override data",
                ["helpAll"] = "Hint orvs all — switch all plugins to native and replay",
                ["helpReset"] = "Hint orvs reset — leave native mode and restore merged rendering",
                ["helpYield"] = "Hint orvs yield — hand the hint channel to external plugins (this run only)",
                ["helpTake"] = "Hint orvs take — reclaim the hint channel (default)",
                ["helpSetup"] = "Hc setup — first-run setup (auto update / detailed log / write config / RNR)",
                ["helpUpdate"] = "Hc update — check the latest GitHub release now",
                ["helpFooter"] = "Note: multiple hint plugins can coexist; use orvs to roll back one plugin without affecting the others.",
                ["updateStarted"] = "Update check started; see the console log for the result.",
                ["updateFound"] = "New version found: {0} (current {1})",
                ["updateNone"] = "Already up to date: {0}",
                ["updateFailed"] = "Update check failed: {0}",
                ["updateDownloaded"] = "Downloaded the new version to {0}; replace the running DLL before restart.",
                ["updateReplaced"] = "Replaced HintChorus.dll; the server will restart at the end of the round to load it.",
                ["updateReplaceFailed"] = "Could not replace the running DLL (possibly locked); kept the .new file, replace it manually.",
                ["rnrScheduled"] = "Scheduled a full restart at the end of the current round to load the new version."
            },
            ["fr"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["banner"] = "Configuration initiale de HintChorus",
                ["detected"] = "Détecté : langue={0} | fuseau={1} | région={2}",
                ["detectedExtra"] = "Source du fuseau : {0} | Vérification de l'horloge : {1}",
                ["srcNetwork"] = "IP publique + fuseau réseau",
                ["srcLocal"] = "fuseau local",
                ["clockOk"] = "horloge locale fiable",
                ["clockSkew"] = "écart de {0} s",
                ["q1"] = "1) Mise à jour auto : {0}    (Hc setup 1 on / off)",
                ["q2"] = "2) Journal UI détaillé : {0}    (Hc setup 2 on / off)",
                ["q3"] = "3) Écrire la configuration maintenant ? {0}    (Hc setup 3 yes / no)",
                ["q4"] = "4) Redémarrer en fin de manche après mise à jour (RNR) : {0}    (Hc setup 4 on / off)",
                ["desc1"] = "Mise à jour auto : vérifie les GitHub Releases ; une fois activée, elle télécharge automatiquement, mais enregistre seulement HintChorus.dll.new par défaut.",
                ["desc2"] = "Journal UI détaillé : enregistre le texte, la position et le rendu par joueur dans logs/ui-*.log ; coût IO notable.",
                ["desc3"] = "Écrire la configuration : enregistre les choix actuels dans config.yml pour les conserver après redémarrage.",
                ["desc4"] = "Redémarrage en fin de manche (RNR) : après le téléchargement et le remplacement de la DLL, redémarre complètement le serveur à la fin de la manche en cours.",
                ["commands"] = "Hc setup done pour terminer | Hc setup skip pour ignorer",
                ["on"] = "activé",
                ["off"] = "désactivé",
                ["yes"] = "oui",
                ["no"] = "non",
                ["saved"] = "Configuration enregistrée : {0}",
                ["skipped"] = "Ignoré. Aucun changement.",
                ["done"] = "Configuration terminée.",
                ["restart"] = "Le journal détaillé et les réglages similaires prennent effet après redémarrage.",
                ["notActive"] = "L'assistant n'est pas actif (déjà terminé ou pas encore démarré).",
                ["usage"] = "Usage : Hc setup | Hc setup 1 on|off | Hc setup 2 on|off | Hc setup 3 yes|no | Hc setup 4 on|off | Hc setup done | Hc setup skip",
                ["helpHeader"] = "[HintChorus] commandes console (insensible à la casse) :",
                ["helpOrvs"] = "Hint orvs — détecte les plugins courants et liste les numéros (option 1 = natif si aucun)",
                ["helpPick"] = "Hint orvs <n> — bascule ce plugin en natif et rejoue ses dernières données",
                ["helpMerge"] = "Hint orvs <n> merge — rétablit ce plugin en rendu fusionné",
                ["helpNative"] = "Hint orvs native — tout passer en natif et rejouer les données précédentes",
                ["helpAll"] = "Hint orvs all — tous les plugins en natif et rejeu",
                ["helpReset"] = "Hint orvs reset — quitter le mode natif et rétablir le rendu fusionné",
                ["helpYield"] = "Hint orvs yield — céder le canal aux plugins externes (cette exécution uniquement)",
                ["helpTake"] = "Hint orvs take — reprendre le canal (par défaut)",
                ["helpSetup"] = "Hc setup — configuration initiale (maj auto / journal détaillé / écrire config / RNR)",
                ["helpUpdate"] = "Hc update — vérifier la dernière version GitHub maintenant",
                ["helpFooter"] = "Note : plusieurs plugins de hints peuvent coexister ; utilisez orvs pour en rétablir un sans toucher aux autres.",
                ["updateStarted"] = "Vérification des mises à jour lancée ; résultat dans la console.",
                ["updateFound"] = "Nouvelle version : {0} (actuelle {1})",
                ["updateNone"] = "Déjà à jour : {0}",
                ["updateFailed"] = "Échec de la vérification : {0}",
                ["updateDownloaded"] = "Nouvelle version téléchargée vers {0} ; remplacez la DLL avant redémarrage.",
                ["updateReplaced"] = "HintChorus.dll remplacé ; le serveur redémarrera en fin de manche pour le charger.",
                ["updateReplaceFailed"] = "Impossible de remplacer la DLL en cours (verrouillée ?) ; fichier .new conservé, remplacez-le manuellement.",
                ["rnrScheduled"] = "Redémarrage complet prévu à la fin de la manche en cours pour charger la nouvelle version."
            },
            ["ru"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["banner"] = "Первичная настройка HintChorus",
                ["detected"] = "Обнаружено: язык={0} | часовой пояс={1} | регион={2}",
                ["detectedExtra"] = "Источник часового пояса: {0} | Проверка времени: {1}",
                ["srcNetwork"] = "публичный IP + сетевой пояс",
                ["srcLocal"] = "локальный пояс",
                ["clockOk"] = "локальное время достоверно",
                ["clockSkew"] = "отклонение {0} с",
                ["q1"] = "1) Автообновление: {0}    (Hc setup 1 on / off)",
                ["q2"] = "2) Подробный журнал UI: {0}    (Hc setup 2 on / off)",
                ["q3"] = "3) Записать конфигурацию сейчас? {0}    (Hc setup 3 yes / no)",
                ["q4"] = "4) Перезапуск после раунда после обновления (RNR): {0}    (Hc setup 4 on / off)",
                ["desc1"] = "Автообновление: проверяет GitHub Releases; при включении скачивает автоматически, но по умолчанию сохраняет только HintChorus.dll.new.",
                ["desc2"] = "Подробный журнал UI: записывает текст, позицию и итоговый вывод по игрокам в logs/ui-*.log; заметная нагрузка на диск.",
                ["desc3"] = "Записать конфигурацию: сохраняет текущие настройки в config.yml для следующих запусков.",
                ["desc4"] = "Перезапуск после раунда (RNR): после скачивания и замены DLL полностью перезапускает сервер по окончании текущего раунда.",
                ["commands"] = "Hc setup done — завершить | Hc setup skip — пропустить",
                ["on"] = "вкл",
                ["off"] = "выкл",
                ["yes"] = "да",
                ["no"] = "нет",
                ["saved"] = "Конфигурация сохранена: {0}",
                ["skipped"] = "Пропущено. Изменений нет.",
                ["done"] = "Настройка завершена.",
                ["restart"] = "Подробный журнал и подобные настройки полностью вступят в силу после перезапуска.",
                ["notActive"] = "Мастер настройки не запущен (уже завершён или ещё не начат).",
                ["usage"] = "Использование: Hc setup | Hc setup 1 on|off | Hc setup 2 on|off | Hc setup 3 yes|no | Hc setup 4 on|off | Hc setup done | Hc setup skip",
                ["helpHeader"] = "[HintChorus] команды консоли (без учёта регистра):",
                ["helpOrvs"] = "Hint orvs — найти популярные плагины и показать номера (1 = нативный режим, если их нет)",
                ["helpPick"] = "Hint orvs <n> — переключить этот плагин на нативный вывод и повторить последние данные",
                ["helpMerge"] = "Hint orvs <n> merge — вернуть этот плагин в объединённый рендер",
                ["helpNative"] = "Hint orvs native — всё на нативный вывод и повтор данных",
                ["helpAll"] = "Hint orvs all — все плагины на нативный вывод и повтор",
                ["helpReset"] = "Hint orvs reset — выйти из нативного режима и вернуть объединение",
                ["helpYield"] = "Hint orvs yield — отдать канал внешним плагинам (только на этот запуск)",
                ["helpTake"] = "Hint orvs take — вернуть канал (по умолчанию)",
                ["helpSetup"] = "Hc setup — первичная настройка (автообновление / подробный журнал / запись конфига / RNR)",
                ["helpUpdate"] = "Hc update — проверить последнюю версию GitHub сейчас",
                ["helpFooter"] = "Примечание: несколько hint-плагинов могут работать вместе; orvs откатывает один, не затрагивая остальные.",
                ["updateStarted"] = "Проверка обновлений запущена; результат в консоли.",
                ["updateFound"] = "Найдена новая версия: {0} (текущая {1})",
                ["updateNone"] = "Уже последняя версия: {0}",
                ["updateFailed"] = "Ошибка проверки обновлений: {0}",
                ["updateDownloaded"] = "Новая версия загружена в {0}; замените DLL перед перезапуском.",
                ["updateReplaced"] = "HintChorus.dll заменён; сервер перезапустится в конце раунда.",
                ["updateReplaceFailed"] = "Не удалось заменить работающую DLL (возможно, занята); файл .new сохранён, замените вручную.",
                ["rnrScheduled"] = "Запланирован полный перезапуск в конце текущего раунда для загрузки новой версии."
            },
            ["es"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["banner"] = "Configuración inicial de HintChorus",
                ["detected"] = "Detectado: idioma={0} | zona horaria={1} | región={2}",
                ["detectedExtra"] = "Origen de la zona: {0} | Verificación del reloj: {1}",
                ["srcNetwork"] = "IP pública + zona de red",
                ["srcLocal"] = "zona local",
                ["clockOk"] = "reloj local fiable",
                ["clockSkew"] = "desfase de {0} s",
                ["q1"] = "1) Actualización automática: {0}    (Hc setup 1 on / off)",
                ["q2"] = "2) Registro UI detallado: {0}    (Hc setup 2 on / off)",
                ["q3"] = "3) ¿Escribir la configuración ahora? {0}    (Hc setup 3 yes / no)",
                ["q4"] = "4) Reiniciar al terminar la ronda tras actualizar (RNR): {0}    (Hc setup 4 on / off)",
                ["desc1"] = "Actualización automática: consulta GitHub Releases; al activarla descarga automáticamente, pero por defecto solo guarda HintChorus.dll.new.",
                ["desc2"] = "Registro UI detallado: guarda texto, posición y resultado por jugador en logs/ui-*.log; coste de IO notable.",
                ["desc3"] = "Escribir configuración: guarda las opciones actuales en config.yml para conservarlas tras reiniciar.",
                ["desc4"] = "Reinicio al terminar la ronda (RNR): tras descargar y reemplazar la DLL, reinicia el servidor al final de la ronda actual.",
                ["commands"] = "Hc setup done para terminar | Hc setup skip para omitir",
                ["on"] = "activado",
                ["off"] = "desactivado",
                ["yes"] = "sí",
                ["no"] = "no",
                ["saved"] = "Configuración guardada: {0}",
                ["skipped"] = "Omitido. No se cambió nada.",
                ["done"] = "Configuración completada.",
                ["restart"] = "El registro detallado y ajustes similares se aplican tras reiniciar.",
                ["notActive"] = "El asistente no está activo (ya terminó o aún no empezó).",
                ["usage"] = "Uso: Hc setup | Hc setup 1 on|off | Hc setup 2 on|off | Hc setup 3 yes|no | Hc setup 4 on|off | Hc setup done | Hc setup skip",
                ["helpHeader"] = "[HintChorus] comandos de consola (sin distinguir mayúsculas):",
                ["helpOrvs"] = "Hint orvs — detecta plugins populares y lista números (opción 1 = nativo si no hay ninguno)",
                ["helpPick"] = "Hint orvs <n> — cambia ese plugin a nativo y reproduce sus últimos datos",
                ["helpMerge"] = "Hint orvs <n> merge — restaura ese plugin al render combinado",
                ["helpNative"] = "Hint orvs native — todo a nativo y reproducir datos previos",
                ["helpAll"] = "Hint orvs all — todos los plugins a nativo y reproducir",
                ["helpReset"] = "Hint orvs reset — salir del modo nativo y restaurar el render combinado",
                ["helpYield"] = "Hint orvs yield — ceder el canal a plugins externos (solo esta ejecución)",
                ["helpTake"] = "Hint orvs take — recuperar el canal (por defecto)",
                ["helpSetup"] = "Hc setup — configuración inicial (auto actualización / registro detallado / escribir config / RNR)",
                ["helpUpdate"] = "Hc update — comprobar la última versión de GitHub ahora",
                ["helpFooter"] = "Nota: varios plugins de hints pueden coexistir; usa orvs para revertir uno sin afectar a los demás.",
                ["updateStarted"] = "Comprobación de actualizaciones iniciada; resultado en la consola.",
                ["updateFound"] = "Nueva versión: {0} (actual {1})",
                ["updateNone"] = "Ya está actualizado: {0}",
                ["updateFailed"] = "Error al comprobar actualizaciones: {0}",
                ["updateDownloaded"] = "Nueva versión descargada en {0}; reemplaza la DLL antes de reiniciar.",
                ["updateReplaced"] = "HintChorus.dll reemplazada; el servidor se reiniciará al final de la ronda para cargarla.",
                ["updateReplaceFailed"] = "No se pudo reemplazar la DLL en uso (posible bloqueo); se conservó el archivo .new, reemplázalo manualmente.",
                ["rnrScheduled"] = "Reinicio completo programado al final de la ronda actual para cargar la nueva versión."
            }
        };

    internal static string Get(string key, string? language)
    {
        string lang = ServerLocale.Normalize(language);
        if (lang.Length == 0)
        {
            lang = "en";
        }
        Dictionary<string, string>? table;
        if (Texts.TryGetValue(lang, out table) && table.TryGetValue(key, out string? value))
        {
            return value;
        }
        return "[" + key + "]";
    }

    internal static string Format(string key, string? language, params object?[] args)
    {
        string template = Get(key, language);
        try
        {
            return string.Format(template, args);
        }
        catch (Exception)
        {
            return template;
        }
    }
}
