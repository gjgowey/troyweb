# Restore pinned runtime assets. Requires PowerShell 7.
$ErrorActionPreference = 'Stop'
$solutionRoot = Split-Path -Parent $PSScriptRoot
$assets = Get-Content -LiteralPath (Join-Path $solutionRoot 'client-assets.json') -Raw | ConvertFrom-Json
foreach ($asset in $assets) {
    $temporaryFile = [IO.Path]::GetTempFileName()
    try {
        Invoke-WebRequest -Uri $asset.Url -OutFile $temporaryFile
        $actual = 'sha384-' + [Convert]::ToBase64String([Security.Cryptography.SHA384]::HashData([IO.File]::ReadAllBytes($temporaryFile)))
        if ($actual -cne $asset.Hash) { throw "Integrity mismatch for $($asset.Url)" }
        $destination = Join-Path $solutionRoot ('TroyWebProperty.Web/wwwroot/lib/' + $asset.File)
        Copy-Item -LiteralPath $temporaryFile -Destination $destination -Force
        Write-Output "Verified and restored $($asset.File)"
    }
    finally { Remove-Item -LiteralPath $temporaryFile -ErrorAction SilentlyContinue }
}
