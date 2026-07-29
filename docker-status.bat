@echo off
:: Thin wrapper — canonical script lives in scripts\docker\
call "%~dp0scripts\docker\docker-status.bat" %*
