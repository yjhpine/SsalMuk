#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'source-state.ps1')
. (Join-Path $PSScriptRoot 'read-results.ps1')
$health=Invoke-RestMethod 'http://127.0.0.1:8080/health' -TimeoutSec 5
if ($health.status -ne 'healthy' -or $health.version -ne '10.2.0') { throw 'Start the pinned Unity MCP server first.' }
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$directory=Join-Path $root 'Logs/Validation'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$id=[guid]::NewGuid().ToString('N')
Write-ValidationJson (Join-Path $directory 'connect-mcp.json') @{requestId=$id;requestedUtc=[DateTime]::UtcNow.ToString('O')}
$deadline=[DateTime]::UtcNow.AddSeconds(90)
while ([DateTime]::UtcNow -lt $deadline) {
    $path=Join-Path $directory 'mcp-connection.json'
    if (Test-Path -LiteralPath $path) {
        $result=Read-ValidationJson $path -IfAvailable
        if ($null -ne $result -and $result.requestId -eq $id) {
            if ($result.status -ne 'Connected') { throw $result.message }
            $result | ConvertTo-Json -Compress
            exit 0
        }
    }
    Start-Sleep -Milliseconds 300
}
throw 'Unity did not process the connection request; check compilation and refresh the open Editor.'
