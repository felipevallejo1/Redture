<#
.SYNOPSIS
    Builds Redture and installs it for the current user.

.DESCRIPTION
    Publishes a single self-contained executable into
    %LOCALAPPDATA%\Programs\Redture and puts a shortcut in the Start menu, so
    Redture behaves like any other desktop application: searchable, pinnable,
    and launchable without a terminal.

    Per user rather than into Program Files, so it needs no administrator
    rights. Self-contained, so it runs on a machine with no .NET installed.

    This location matters for more than tidiness. The "start with Windows"
    switch registers whatever executable is running at the moment it is turned
    on, so enabling it from a build output directory records a path that breaks
    the next time the project is rebuilt or moved. Enable it from the installed
    copy.

.PARAMETER Desktop
    Also place a shortcut on the desktop.

.PARAMETER Uninstall
    Remove the installed copy and its shortcuts. Leaves settings and logs in
    %APPDATA%\Redture alone.

.EXAMPLE
    .\scripts\install.ps1
    .\scripts\install.ps1 -Desktop
    .\scripts\install.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [switch]$Desktop,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\Redture'
$exePath = Join-Path $installDir 'Redture.exe'
$startMenuLink = Join-Path ([Environment]::GetFolderPath('Programs')) 'Redture.lnk'
$desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Redture.lnk'

function Stop-Redture {
    # Publishing over a running executable fails, and so does deleting it.
    Get-Process -Name Redture -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  stopping the running instance (pid $($_.Id))"
        $_.Kill()
        $_.WaitForExit(5000) | Out-Null
    }
}

function New-Shortcut([string]$linkPath, [string]$target) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($linkPath)
    $shortcut.TargetPath = $target
    $shortcut.WorkingDirectory = Split-Path -Parent $target
    $shortcut.IconLocation = "$target,0"
    $shortcut.Description = 'Colour temperature and below-minimum brightness'
    $shortcut.Save()
    Write-Host "  shortcut: $linkPath"
}

if ($Uninstall) {
    Write-Host 'Removing Redture...'
    Stop-Redture

    foreach ($link in @($startMenuLink, $desktopLink)) {
        if (Test-Path $link) {
            Remove-Item $link -Force
            Write-Host "  removed $link"
        }
    }

    if (Test-Path $installDir) {
        Remove-Item $installDir -Recurse -Force
        Write-Host "  removed $installDir"
    }

    Write-Host ''
    Write-Host 'Done. Settings and logs were left in %APPDATA%\Redture.'
    Write-Host 'If "start with Windows" was on, turn it off before uninstalling next time,'
    Write-Host 'or remove the Redture value under HKCU Run by hand.'
    return
}

Write-Host 'Building Redture...'
Stop-Redture

# Self-contained and single-file: one executable that runs anywhere, at the
# cost of size. For a tool people are meant to install and forget, not needing
# a runtime first is worth more than the megabytes.
dotnet publish (Join-Path $repoRoot 'src\Redture.App\Redture.App.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    --output $installDir `
    --nologo | Select-Object -Last 3

if (-not (Test-Path $exePath)) {
    throw "Publish finished but $exePath is missing."
}

$sizeMb = [math]::Round((Get-Item $exePath).Length / 1MB, 1)

Write-Host ''
Write-Host "Installed to $installDir ($sizeMb MB)"

New-Shortcut $startMenuLink $exePath
if ($Desktop) {
    New-Shortcut $desktopLink $exePath
}

Write-Host ''
Write-Host 'Redture is now in the Start menu. Launch it from there, and turn on'
Write-Host '"start with Windows" from that copy so the registered path is the'
Write-Host 'installed one rather than a build output directory.'
