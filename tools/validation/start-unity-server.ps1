#requires -Version 7.0
$ErrorActionPreference='Stop'
$health=$null
try { $health=Invoke-RestMethod 'http://127.0.0.1:8080/health' -TimeoutSec 3 } catch { }
if ($null -ne $health) {
    if ($health.status -ne 'healthy' -or $health.version -ne '10.2.0') { throw 'Port 8080 has a different server; leave it running and resolve the conflict.' }
    Write-Output 'Unity MCP 10.2.0 is already running.'
    exit 0
}
# Keep this in a managed terminal; do not create visible background windows.
& uv tool run --from mcpforunityserver==10.2.0 mcp-for-unity --transport http --http-host 127.0.0.1 --http-port 8080 --default-instance SsalMuk
exit $LASTEXITCODE
