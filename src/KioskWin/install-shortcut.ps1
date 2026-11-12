# Create a KioskWin startup shortcut in the current user's Startup folder.
# Usage: Run powershell -ExecutionPolicy Bypass -File .\install-shortcut.ps1
# from the publish output directory containing KioskWin.exe.
$ErrorActionPreference = 'Stop'

$exe = Join-Path $PSScriptRoot 'KioskWin.exe'
if (-not (Test-Path $exe)) {
    throw "Could not find $exe. Run this script from the publish output directory."
}

$startup = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startup 'KioskWin.lnk'

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $PSScriptRoot
$shortcut.WindowStyle = 3   # Maximized
$shortcut.Description = 'KioskWin'
$shortcut.Save()

Write-Host "Created startup shortcut: $shortcutPath"
