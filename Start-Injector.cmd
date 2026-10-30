@echo off
"%~dp0inject\DsuInjector.exe" %*
if errorlevel 1 pause
