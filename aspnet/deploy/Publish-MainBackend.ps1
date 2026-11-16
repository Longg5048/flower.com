param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repository 'artifacts/main-backend' }
$project = Join-Path $repository 'aspnet/src/FlowerFlow.Api/FlowerFlow.Api.csproj'
dotnet publish $project --configuration Release --output $OutputDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'ASP.NET publish failed.' }
foreach ($asset in @('FlowerFlow.Api.dll', 'wwwroot/index.html', 'wwwroot/js/order/order-store.js', 'wwwroot/css', 'wwwroot/images')) {
    if (!(Test-Path -LiteralPath (Join-Path $OutputDirectory $asset))) { throw "Missing published asset: $asset" }
}
foreach ($privatePath in @('wwwroot/api', 'wwwroot/backend', 'wwwroot/lib', 'wwwroot/.env', 'wwwroot/firestore.rules', 'wwwroot/node_modules')) {
    if (Test-Path -LiteralPath (Join-Path $OutputDirectory $privatePath)) { throw "Private source was published: $privatePath" }
}
Write-Output "Published backend and frontend: $OutputDirectory"
Write-Output 'Supply deployment secrets before starting. Check /health/ready before routing traffic.'
