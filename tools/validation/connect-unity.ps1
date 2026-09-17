#requires -Version 7.5
param([string]$UnityCli = (Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe'))
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'source-state.ps1')
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not (Test-Path -LiteralPath $UnityCli -PathType Leaf)) { throw 'Official Unity CLI is missing. Install it or pass -UnityCli with its absolute path.' }
$env:UNITY_NO_CONSENT_PROMPT='1'
$arguments=@('--project-path',$root,'--caller','plugin','--skill','unity-cli','--format','json','--non-interactive')
$rawStatus=& $UnityCli command editor_status @arguments
if ($LASTEXITCODE -ne 0) { throw 'Official Unity connection failed. Open this project in its matching Editor and check the Pipeline package.' }
$statusReply=($rawStatus -join "`n") | ConvertFrom-Json
if (-not $statusReply.success -or $statusReply.data.result.status -notin @('ready','playing') -or $statusReply.data.result.compiling -or $statusReply.data.result.domainReloadInProgress) { throw 'The target Editor is not ready; check compilation or an open dialog.' }
$code='return new { projectPath = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), unityVersion = UnityEngine.Application.unityVersion, isCompiling = UnityEditor.EditorApplication.isCompiling };'
$rawIdentity=& $UnityCli command eval --code $code @arguments
if ($LASTEXITCODE -ne 0) { throw 'The official Unity Editor identity read failed.' }
$identityReply=($rawIdentity -join "`n") | ConvertFrom-Json
if (-not $identityReply.success -or -not $identityReply.data.result.success) { throw 'The Editor could not evaluate the identity read.' }
$identity=$identityReply.data.result.result
$expectedVersion=((Get-Content -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion: ').Line -split ':',2)[1].Trim()
$actualRoot=[IO.Path]::GetFullPath($identity.projectPath).TrimEnd('\','/')
if (-not [string]::Equals($actualRoot,$root.TrimEnd('\','/'),[StringComparison]::OrdinalIgnoreCase) -or $identity.unityVersion -cne $expectedVersion -or $identity.isCompiling) { throw 'Unity project path, version, or compilation state does not match this project.' }
$directory=Join-Path $root 'Logs/Validation'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$result=@{requestId=[guid]::NewGuid().ToString('N');checkedUtc=[DateTime]::UtcNow.ToString('O');status='Connected';provider='Unity official CLI / Pipeline';projectPath=$actualRoot;unityVersion=$identity.unityVersion;editorState=$statusReply.data.result.status;playMode=$statusReply.data.result.playMode}
Write-ValidationJson (Join-Path $directory 'unity-connection.json') $result
$result | ConvertTo-Json -Compress
