#requires -Version 7.0
param([string]$UnityCli = (Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe'))
$ErrorActionPreference='Stop'
if (-not (Test-Path -LiteralPath $UnityCli -PathType Leaf)) { throw 'Official Unity CLI is missing. Install it or pass -UnityCli with its absolute path.' }
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$env:UNITY_NO_CONSENT_PROMPT='1'
# A stdio MCP client owns this process; Codex normally starts the configured CLI directly.
& $UnityCli mcp --project-path $root
exit $LASTEXITCODE
