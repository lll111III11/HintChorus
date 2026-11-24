namespace HintChorus.Core.Enums;

/// <summary>
/// 第三方 Hint 插件兼容策略。
/// </summary>
public enum CompatibilityPolicy
{
    /// <summary>检测到主流 Hint 插件时, 自动把它切到原生直通(默认)。</summary>
    Auto,

    /// <summary>只提醒, 不改变任何插件的接管状态。</summary>
    Notify,

    /// <summary>关闭检测与兼容处理。</summary>
    Off
}
