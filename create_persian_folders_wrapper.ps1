# PowerShell wrapper that reads the script with UTF-8 encoding
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$scriptPath = Join-Path $PSScriptRoot "create_persian_folders.ps1"
$scriptContent = Get-Content -Path $scriptPath -Raw -Encoding UTF8
Invoke-Expression $scriptContent

