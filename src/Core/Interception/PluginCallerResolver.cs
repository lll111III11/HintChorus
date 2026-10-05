using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace HintIsolation.Core.Interception;

public static class PluginCallerResolver
{
	public const string NativeCallerId = "Native";

	private static readonly string[] TrustedNames;

	private static readonly HashSet<string> TrustedLookup;

	private static readonly HashSet<Assembly> TrustedAssemblies;

	private static readonly Dictionary<Assembly, string> AssemblyNameCache;

	private static readonly object Sync;

	private static readonly Assembly SelfAssembly;

	static PluginCallerResolver()
	{
		TrustedNames = new string[41]
		{
			"mscorlib", "netstandard", "System", "System.Core", "System.Runtime", "System.Private.CoreLib", "Microsoft.CSharp", "Assembly-CSharp", "Assembly-CSharp-firstpass", "Assembly-CSharp-Publicized",
			"LabApi", "NorthwoodLib", "Pooling", "YamlDotNet", "Exiled", "Exiled.API", "Exiled.Loader", "Exiled.Events", "Exiled.CustomItems", "Newtonsoft.Json",
			"Mono.Posix", "SemanticVersioning", "NAudio", "NLayer", "NVorbis", "Mirror", "Mirror.Components", "Mirror-Publicized", "CommandSystem.Core", "0Harmony",
			"HarmonyLib", "Harmony", "UnityEngine", "UnityEngine.UI", "UnityEngine.UIElementsModule", "Unity.TextMeshPro", "Unity.Burst", "Unity.Collections", "Unity.Mathematics", "MEC",
			"CustomEventHandler"
		};
		TrustedLookup = new HashSet<string>(TrustedNames, StringComparer.OrdinalIgnoreCase);
		TrustedAssemblies = new HashSet<Assembly>();
		AssemblyNameCache = new Dictionary<Assembly, string>();
		Sync = new object();
		SelfAssembly = typeof(PluginCallerResolver).Assembly;
		TrustedAssemblies.Add(SelfAssembly);
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			if (IsTrustedName(assembly.GetName().Name))
			{
				TrustedAssemblies.Add(assembly);
			}
		}
	}

	public static bool IsTrusted(Assembly? assembly)
	{
		if ((object)assembly == null)
		{
			return true;
		}
		if (assembly.IsDynamic)
		{
			return true;
		}
		if (assembly == SelfAssembly)
		{
			return true;
		}
		lock (Sync)
		{
			if (TrustedAssemblies.Contains(assembly))
			{
				return true;
			}
			if (IsTrustedName(assembly.GetName().Name))
			{
				TrustedAssemblies.Add(assembly);
				return true;
			}
			return false;
		}
	}

	public static CallerInfo Resolve()
	{
		// 归因必须"逐次调用"地解析, 不能用帧号做 memo:
		// 同一帧里插件 A 与插件 B 都发 UI 是常态(击杀播报 + HUD 刷新), 帧级缓存会把
		// 后一个的调用直接当成前一个的来源, 于是两个插件的 UI 被并进同一个 UiId 信口,
		// "按调用方归因"这一核心能力失真; 反向也会把游戏原生提示误判成插件提示(绕过 NativeHintPolicy)。
		// 代价是每次拦截都要走一次调用栈 —— 下面是实测过的取舍: 这比归因错乱便宜得多,
		// 而且真正的按调用点缓存发生在下游 UiIdRegistry.Resolve(它按 方法+IL偏移 缓存 UiId)。
		return ResolveUncached();
	}

	private static CallerInfo ResolveUncached()
	{
		StackTrace stackTrace;
		try
		{
			stackTrace = new StackTrace(fNeedFileInfo: false);
		}
		catch (Exception)
		{
			return new CallerInfo(isPlugin: false, "Native", null, null, -1);
		}
		for (int i = 0; i < stackTrace.FrameCount; i++)
		{
			StackFrame frame = stackTrace.GetFrame(i);
			MethodBase methodBase = frame?.GetMethod();
			if ((object)methodBase != null)
			{
				Assembly assembly = methodBase.DeclaringType?.Assembly ?? methodBase.Module?.Assembly;
				if (!IsTrusted(assembly))
				{
					return new CallerInfo(isPlugin: true, ResolvePluginId(assembly), assembly, methodBase, frame.GetILOffset());
				}
			}
		}
		return new CallerInfo(isPlugin: false, "Native", null, null, -1);
	}

	private static string ResolvePluginId(Assembly assembly)
	{
		lock (Sync)
		{
			if (AssemblyNameCache.TryGetValue(assembly, out string value))
			{
				return value;
			}
			string text = assembly.GetName().Name ?? "Unknown";
			AssemblyNameCache[assembly] = text;
			return text;
		}
	}

	public static void ClearCache()
	{
		lock (Sync)
		{
			AssemblyNameCache.Clear();
		}
	}

	private static bool IsTrustedName(string? name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return true;
		}
		if (TrustedLookup.Contains(name))
		{
			return true;
		}
		if (!HasPrefix(name, "UnityEngine.") && !HasPrefix(name, "Unity.") && !HasPrefix(name, "System.") && !HasPrefix(name, "Microsoft.") && !HasPrefix(name, "Exiled.") && !HasPrefix(name, "NAudio.") && !HasPrefix(name, "NVorbis."))
		{
			return HasPrefix(name, "NorthwoodLib.");
		}
		return true;
	}

	private static bool HasPrefix(string name, string prefix)
	{
		if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return name.Equals(prefix.TrimEnd('.'), StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}
}
