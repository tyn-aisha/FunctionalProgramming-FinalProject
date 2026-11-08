@echo off
cd /d "%~dp0"
if not exist runtimes\win-x64 mkdir runtimes\win-x64
cl /nologo /std:c11 /O2 /W4 /WX /LD ijc_native.c /Fe:runtimes\win-x64\ijc_native.dll
if errorlevel 1 exit /b 1
echo built native\runtimes\win-x64\ijc_native.dll
