using System.Reflection;

namespace HintChorus.Core.Interception;

public readonly struct CallerInfo
{
	public bool IsPlugin { get; }

	public string PluginId { get; }

	public Assembly? Assembly { get; }

	public MethodBase? Method { get; }

	public int IlOffset { get; }

	internal CallerInfo(bool isPlugin, string pluginId, Assembly? assembly, MethodBase? method, int ilOffset)
	{
		IsPlugin = isPlugin;
		PluginId = pluginId;
		Assembly = assembly;
		Method = method;
		IlOffset = ilOffset;
	}
}
