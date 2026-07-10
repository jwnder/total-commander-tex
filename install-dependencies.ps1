param(
    [switch] $IncludePoppler,
    [switch] $SkipMiKTeX,
    [switch] $SkipGhostscript
)

$ErrorActionPreference = "Stop"

function Install-WingetPackage {
    param(
        [string] $Id,
        [string] $Name
    )

    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw "winget.exe was not found. Install App Installer from Microsoft Store, or install $Name manually."
    }

    Write-Host "Installing $Name..."
    & winget install --id $Id --exact --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "winget failed while installing $Name."
    }
}

function Test-Tool {
    param([string] $Name)

    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    return $null
}

if (-not $SkipMiKTeX) {
    Install-WingetPackage -Id "MiKTeX.MiKTeX" -Name "MiKTeX"
}

if (-not $SkipGhostscript) {
    Install-WingetPackage -Id "ArtifexSoftware.Ghostscript" -Name "Ghostscript"
}

if ($IncludePoppler) {
    Install-WingetPackage -Id "oschwartz10612.Poppler" -Name "Poppler"
}

Write-Host ""
Write-Host "Dependency check:"

$xelatex = Test-Tool "xelatex.exe"
$pdftoppm = Test-Tool "pdftoppm.exe"
$gs64 = Test-Tool "gswin64c.exe"
$gs32 = Test-Tool "gswin32c.exe"

Write-Host ("  xelatex.exe: " + ($(if ($xelatex) { $xelatex } else { "NOT FOUND" })))
Write-Host ("  pdftoppm.exe: " + ($(if ($pdftoppm) { $pdftoppm } else { "NOT FOUND" })))
Write-Host ("  Ghostscript: " + ($(if ($gs64) { $gs64 } elseif ($gs32) { $gs32 } else { "NOT FOUND" })))
Write-Host ""
Write-Host "Restart Total Commander and any open terminals after installing dependencies."
