Set-StrictMode -Version Latest

function Get-ValidationSourceHash {
    param([Parameter(Mandatory)][string]$ProjectPath)
    $root=[IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
    $files=[Collections.Generic.List[string]]::new()
    foreach ($directory in @('Assets/_SsalMuk','ProjectSettings','tools/validation')) {
        $path=Join-Path $root $directory
        if (Test-Path -LiteralPath $path) {
            foreach ($file in Get-ChildItem -LiteralPath $path -Recurse -File) { $files.Add($file.FullName) }
        }
    }
    foreach ($relative in @('Assets/_SsalMuk.meta','Packages/manifest.json','Packages/packages-lock.json')) {
        $file=Join-Path $root $relative
        if (Test-Path -LiteralPath $file) { $files.Add([IO.Path]::GetFullPath($file)) }
    }
    $relativeNames=[string[]]@($files | ForEach-Object { $_.Substring($root.Length+1).Replace('\','/') })
    [Array]::Sort($relativeNames,[StringComparer]::Ordinal)
    $text=[Text.StringBuilder]::new()
    foreach ($relative in $relativeNames) {
        $digest=(Get-FileHash -LiteralPath (Join-Path $root $relative) -Algorithm SHA256).Hash.ToLowerInvariant()
        [void]$text.Append($relative).Append([char]0).Append($digest).Append("`n")
    }
    $sha=[Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text.ToString()))).ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Write-ValidationJson {
    param([string]$Path, $Value)
    $temporary=$Path+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
    [IO.File]::WriteAllText($temporary,($Value | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temporary,$Path,$true)
}
