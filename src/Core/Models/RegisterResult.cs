using System;
using System.Collections.Generic;

namespace HintIsolation.Core.Models;

public readonly struct RegisterResult
{
	public bool Success { get; }

	public string ModuleId { get; }

	public string Message { get; }

	public IReadOnlyList<string> Conflicts { get; }

	private RegisterResult(bool success, string moduleId, string message, IReadOnlyList<string> conflicts)
	{
		Success = success;
		ModuleId = moduleId;
		Message = message;
		Conflicts = conflicts;
	}

	public static RegisterResult Ok(string moduleId, string message = "")
	{
		return new RegisterResult(success: true, moduleId, string.IsNullOrEmpty(message) ? "注册成功" : message, Array.Empty<string>());
	}

	public static RegisterResult Fail(string moduleId, string message, IReadOnlyList<string>? conflicts = null)
	{
		return new RegisterResult(success: false, moduleId, message, conflicts ?? Array.Empty<string>());
	}

	public override string ToString()
	{
		if (!Success)
		{
			return "FAIL(" + ModuleId + "): " + Message;
		}
		return "OK(" + ModuleId + ")";
	}
}
