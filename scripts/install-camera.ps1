<#
.SYNOPSIS
    Installs the Shybun camera's media source for development, or removes it.
.DESCRIPTION
    Copies the published Shybun.Camera.dll to %ProgramFiles%\Shybun and registers it for the whole machine. Windows
    loads a virtual camera's media source into the Frame Server and its monitor, which run as services, so the DLL
    must be registered in HKLM and sit where those services can read it, never under a user's profile.
.EXAMPLE
    dotnet publish src/Shybun.Camera -c Release -r win-x64, then, elevated: scripts/install-camera.ps1
.EXAMPLE
    scripts/install-camera.ps1 -Uninstall
#>
#Requires -RunAsAdministrator
param([switch] $Uninstall)
$ErrorActionPreference = 'Stop'

$folder = Join-Path $env:ProgramFiles 'Shybun'
$dll = Join-Path $folder 'Shybun.Camera.dll'

function Invoke-Regsvr32([string[]] $Arguments) {
    $p = Start-Process regsvr32.exe -ArgumentList $Arguments -Wait -PassThru -NoNewWindow
    if ($p.ExitCode -ne 0) { throw "regsvr32 $Arguments failed with exit code $($p.ExitCode)." }
}

# The services keep the DLL loaded for good, as a NativeAOT runtime can't unload, so they must stop before it's
# replaced or removed. Windows starts them again when an app next opens a camera.
Stop-Service FrameServer, FrameServerMonitor -Force -ErrorAction SilentlyContinue

if ($Uninstall) {
    if (Test-Path -LiteralPath $dll) {
        Invoke-Regsvr32 '/s', '/u', "`"$dll`""
        Remove-Item -LiteralPath $dll
    }
    if ((Test-Path -LiteralPath $folder) -and -not (Get-ChildItem -LiteralPath $folder)) { Remove-Item -LiteralPath $folder }
    Write-Host 'Removed the Shybun camera.'
    return
}

$built = Join-Path $PSScriptRoot '..\src\Shybun.Camera\bin\Release\net10.0-windows\win-x64\publish\Shybun.Camera.dll'
if (-not (Test-Path -LiteralPath $built)) { throw "Publish it first: dotnet publish src/Shybun.Camera -c Release -r win-x64" }
$null = New-Item -ItemType Directory -Force -Path $folder
Copy-Item -LiteralPath $built -Destination $dll -Force
Invoke-Regsvr32 '/s', "`"$dll`""
Write-Host "Installed the Shybun camera at $dll."
