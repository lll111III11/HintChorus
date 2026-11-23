using System;

namespace HintChorus.Mapping;

/// <summary>一条模板记录: 只描述"用哪个模板、哪种语言、哪种布局", 不含具体样式实现。</summary>
public sealed class TemplateDescriptor
{
    public string Name { get; internal set; } = "";

    public string Language { get; internal set; } = "zh";

    public string Layout { get; internal set; } = "rows";

    public string Description { get; internal set; } = "";

    public string SourceFile { get; internal set; } = "(builtin)";

    public bool IsCustom { get; internal set; }

    public override string ToString()
    {
        return Name + " (" + Language + ", " + Layout + (IsCustom ? ", custom" : "") + ")";
    }
}
