param(
    [ValidateSet("Release", "Debug")]
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$distDir = Join-Path $PSScriptRoot "dist"
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

dotnet publish -c $Configuration -r win-x64
dotnet publish -c $Configuration -r win-x86

$publishDir64 = Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows\win-x64\publish"
$publishDir32 = Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows\win-x86\publish"

Copy-Item -Force -Path (Join-Path $publishDir64 "TexListerPlugin.dll") -Destination (Join-Path $distDir "texview.wlx64")
Copy-Item -Force -Path (Join-Path $publishDir32 "TexListerPlugin.dll") -Destination (Join-Path $distDir "texview.wlx")
Copy-Item -Force -Path (Join-Path $PSScriptRoot "pluginst.inf") -Destination (Join-Path $distDir "pluginst.inf")
Copy-Item -Force -Path (Join-Path $PSScriptRoot "README.md") -Destination (Join-Path $distDir "README.md")
Copy-Item -Force -Path (Join-Path $PSScriptRoot "install-dependencies.ps1") -Destination (Join-Path $distDir "install-dependencies.ps1")

Write-Host "Built $distDir\texview.wlx64"
Write-Host "Built $distDir\texview.wlx"
