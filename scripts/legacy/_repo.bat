@echo off
:: Shared env for legacy host scripts.
:: Sets REPO_ROOT (Regkasse checkout) and LOG_DIR (always C:\Scripts\logs).
:: Usage: call "%~dp0_repo.bat"

set "REPO_ROOT=%~dp0..\.."
pushd "%REPO_ROOT%" >nul
set "REPO_ROOT=%CD%"
popd >nul

set "LOG_DIR=C:\Scripts\logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
