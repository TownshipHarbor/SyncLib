$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out  = Join-Path $here 'build'

& dotnet build (Join-Path $here 'quest\SyncLib.csproj') -c Release -o $out -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
$dll = Join-Path $out 'SyncLib.dll'
Write-Host ("{0} {1}" -f $dll, (Get-FileHash $dll -Algorithm SHA256).Hash.Substring(0, 8).ToLower())
