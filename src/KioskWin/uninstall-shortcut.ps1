# Remove the KioskWin startup shortcut from the current user's Startup folder.
# Usage: Run powershell -ExecutionPolicy Bypass -File .\uninstall-shortcut.ps1
# from the publish output directory.
$ErrorActionPreference = 'Stop'

$startup = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startup 'KioskWin.lnk'

if (Test-Path $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
    Write-Host "Removed startup shortcut: $shortcutPath"
} else {
    Write-Host "Startup shortcut not found: $shortcutPath"
}
