using System;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using PlayerRoles.Voice;
using UnityEngine;

namespace HintIsolation.Core.Compat;

public static class IntercomGuard
{
	private static long _errorCount;

	private static string _accepted = string.Empty;

	private static float _lastAcceptedAt;

	private static bool _seeded;

	public static bool Enabled { get; set; } = true;

	public static float ThrottleInterval { get; set; }

	public static long SerializeCount { get; private set; }

	public static long ChangedCount { get; private set; }

	public static long RollbackCount { get; private set; }

	public static void Reset()
	{
		SerializeCount = 0L;
		ChangedCount = 0L;
		RollbackCount = 0L;
		_accepted = string.Empty;
		_lastAcceptedAt = 0f;
		_seeded = false;
	}

	internal static void OnSerialize(IntercomDisplay? display)
	{
		if (!Enabled || display == null)
		{
			return;
		}
		try
		{
			SerializeCount++;
			string text = display.Network_overrideText ?? string.Empty;
			float time = Time.time;
			if (!_seeded)
			{
				_seeded = true;
				_accepted = text;
				_lastAcceptedAt = time;
			}
			else if (!string.Equals(text, _accepted, StringComparison.Ordinal))
			{
				ChangedCount++;
				if (ThrottleInterval <= 0f)
				{
					_accepted = text;
					_lastAcceptedAt = time;
				}
				else if (time - _lastAcceptedAt < ThrottleInterval)
				{
					display.Network_overrideText = _accepted;
					RollbackCount++;
				}
				else
				{
					_accepted = text;
					_lastAcceptedAt = time;
				}
			}
		}
		catch (Exception e)
		{
			// 这个 catch 原本是空的, 而对讲机守卫的**全部**逻辑都在这个 try 里:
			// 任何持续异常都会让它静默退化成空操作(回滚数恒为 0), 日志与诊断却毫无线索。
			// 限流上报(前 3 次 + 之后每 200 次一次), 既看得见又不刷屏。
			long n = ++_errorCount;
			if (n <= 3 || (n % 200) == 0)
			{
				Logger.Error($"[HintIsolation] 对讲机守卫异常(第 {n} 次, 已跳过本次处理): {e}");
			}
		}
	}
}
