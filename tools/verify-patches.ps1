<#
.SYNOPSIS
  验证 HintChorus 的所有 Harmony 补丁能否**真正装上**。

.DESCRIPTION
  编译通过 ≠ 补丁装得上。

  Harmony 是按【参数名】把补丁参数绑定到目标参数的（名字对不上并不总是回退到按类型匹配），
  签名或参数名不符会抛 `Patching exception in method ...`；而插件在运行时只会记一行错误就过去了，
  表面上看是"某个通道莫名失效"。这类失败编译期完全看不出来。

  本脚本在本地加载游戏程序集 + 0Harmony，对插件里每个带 [HarmonyPatch] 的类
  逐个执行 CreateClassProcessor(...).Patch()，并打印最内层异常，把问题提前暴露。

  背景：2026-10 试机时 `Broadcast.TargetAddElement` / `RpcAddElement` 两个补丁
  就因参数名写成 message/duration/type（目标实为 data/time/flags）而完全装不上，
  导致广播拦截整体失效、自愈循环重试 3 次后放弃。

.EXAMPLE
  pwsh -File tools/verify-patches.ps1

.EXAMPLE
  pwsh -File tools/verify-patches.ps1 -Plugin .\src\bin\Release\net48\HintChorus.dll -Managed "D:\SteamLibrary\...\SCPSL_Data\Managed"
#>
param(
	[string]$Plugin = "",
	[string]$Managed = ""
)

$ErrorActionPreference = "Stop"

# ── 1. 定位插件 DLL ─────────────────────────────────────────────────────────
if (-not $Plugin) {
	$candidates = @(
		(Join-Path $PSScriptRoot "..\src\bin\Release\net48\HintChorus.dll"),
		(Join-Path $PSScriptRoot "..\src\bin\Debug\net48\HintChorus.dll")
	)
	$Plugin = ($candidates | Where-Object { Test-Path $_ } | Select-Object -First 1)
}
if (-not $Plugin -or -not (Test-Path $Plugin)) {
	Write-Host "找不到 HintChorus.dll，请先 build，或用 -Plugin 指定路径。" -ForegroundColor Red
	exit 2
}

# ── 2. 定位游戏 Managed 目录 ────────────────────────────────────────────────
if (-not $Managed) {
	$roots = @()
	if ($env:SCPSL_MANAGED) { $roots += $env:SCPSL_MANAGED }
	$roots += @(
		"D:\SteamLibrary\steamapps\common\SCP Secret Laboratory Dedicated Server\SCPSL_Data\Managed",
		"C:\Program Files (x86)\Steam\steamapps\common\SCP Secret Laboratory Dedicated Server\SCPSL_Data\Managed",
		"C:\SteamLibrary\steamapps\common\SCP Secret Laboratory Dedicated Server\SCPSL_Data\Managed"
	)
	$Managed = ($roots | Where-Object { Test-Path (Join-Path $_ "Assembly-CSharp.dll") } | Select-Object -First 1)
}
if (-not $Managed -or -not (Test-Path (Join-Path $Managed "Assembly-CSharp.dll"))) {
	Write-Host "找不到游戏 Managed 目录（需含 Assembly-CSharp.dll），请用 -Managed 指定。" -ForegroundColor Red
	exit 2
}

Write-Host "插件 : $Plugin"
Write-Host "游戏 : $Managed"
Write-Host ""

# ── 3. 让游戏与框架程序集都能解析到 ─────────────────────────────────────────
$resolver = [System.ResolveEventHandler] {
	param($s, $e)
	$n = ($e.Name -split ',')[0] + ".dll"
	$p = Join-Path $Managed $n
	if (Test-Path $p) { return [System.Reflection.Assembly]::LoadFrom($p) }
	return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

$harmonyAsm = [System.Reflection.Assembly]::LoadFrom((Join-Path $Managed "0Harmony.dll"))
$null = [System.Reflection.Assembly]::LoadFrom((Join-Path $Managed "Assembly-CSharp.dll"))
$null = [System.Reflection.Assembly]::LoadFrom((Join-Path $Managed "LabApi.dll"))
$pluginAsm = [System.Reflection.Assembly]::LoadFrom($Plugin)

Write-Host ("Harmony: {0}" -f $harmonyAsm.GetName().Version)
Write-Host ""

# ── 4. 逐个补丁类安装 ───────────────────────────────────────────────────────
$harmonyType = $harmonyAsm.GetType("HarmonyLib.Harmony")
$ok = 0
$fail = 0

foreach ($t in $pluginAsm.GetTypes()) {
	$isPatch = $false
	foreach ($a in $t.GetCustomAttributes($false)) {
		if ($a.GetType().Name -like "HarmonyPatch*") { $isPatch = $true }
	}
	if (-not $isPatch) { continue }

	$instance = [Activator]::CreateInstance($harmonyType, @("verify." + $t.Name))
	try {
		$processor = $harmonyType.GetMethod("CreateClassProcessor").Invoke($instance, @($t))
		$null = $processor.GetType().GetMethod("Patch").Invoke($processor, $null)
		Write-Host ("  [OK]   {0}" -f $t.Name) -ForegroundColor Green
		$ok++
	}
	catch {
		$ex = $_.Exception
		while ($ex.InnerException -ne $null) { $ex = $ex.InnerException }
		Write-Host ("  [FAIL] {0}" -f $t.Name) -ForegroundColor Red
		Write-Host ("         {0}" -f $ex.Message) -ForegroundColor Red
		$fail++
	}
}

Write-Host ""
Write-Host ("结果: 成功 {0} / 失败 {1}" -f $ok, $fail)
if ($fail -gt 0) {
	Write-Host "常见原因: 参数名与目标不一致(按名绑定) / 签名不符(如误加 ref) / 目标方法找不到" -ForegroundColor Yellow
	exit 1
}
exit 0
