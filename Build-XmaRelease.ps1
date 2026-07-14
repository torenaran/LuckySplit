[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$Version = '1.0.0'
$Root = $PSScriptRoot
$Solution = Join-Path $Root 'LuckySplit.sln'
$ProjectOutput = Join-Path $Root "LuckySplit\bin\x64\$Configuration"
$Dist = Join-Path $Root 'dist'
$Stage = Join-Path $Dist "LuckySplit-v$Version"
$Archive = Join-Path $Dist "LuckySplit-v$Version-XMA.zip"

Write-Host "Building Lucky Split $Version ($Configuration)..." -ForegroundColor Cyan
& dotnet restore $Solution
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

& dotnet build $Solution -c $Configuration -p:Platform=x64 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

$Dll = Join-Path $ProjectOutput 'LuckySplit.dll'
$Manifest = Join-Path $ProjectOutput 'LuckySplit.json'
if (-not (Test-Path $Dll)) { throw "Missing build output: $Dll" }
if (-not (Test-Path $Manifest)) { throw "Missing generated manifest: $Manifest" }

New-Item $Dist -ItemType Directory -Force | Out-Null
if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force }
New-Item $Stage -ItemType Directory -Force | Out-Null

Copy-Item $Dll $Stage
Copy-Item $Manifest $Stage

$OptionalFiles = @(
    'LuckySplit.deps.json',
    'LuckySplit.runtimeconfig.json'
)
foreach ($Name in $OptionalFiles) {
    $Path = Join-Path $ProjectOutput $Name
    if (Test-Path $Path) { Copy-Item $Path $Stage }
}

Copy-Item (Join-Path $Root 'INSTALLATION.txt') $Stage
Copy-Item (Join-Path $Root 'LICENSE') $Stage
Copy-Item (Join-Path $Root 'CHANGELOG.md') $Stage

if (Test-Path $Archive) { Remove-Item $Archive -Force }
Compress-Archive -Path (Join-Path $Stage '*') -DestinationPath $Archive -CompressionLevel Optimal

Write-Host "" 
Write-Host "Release package created:" -ForegroundColor Green
Write-Host $Archive
Write-Host "" 
Write-Host "Do not add PDB or source files to the public install archive." -ForegroundColor Yellow
