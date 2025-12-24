@echo off
chcp 65001 >nul
powershell.exe -ExecutionPolicy Bypass -NoProfile -Command "$PSDefaultParameterValues['*:Encoding'] = 'utf8'; & '%~dp0create_persian_folders.ps1'"

