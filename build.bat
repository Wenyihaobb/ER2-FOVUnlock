@echo off
setlocal
set "SDK=%USERPROFILE%\.dotnet6sdk\dotnet.exe"
if not exist "%SDK%" set "SDK=dotnet"
"%SDK%" build "%~dp0ER2_FovUnlock.csproj" -c Release -p:InstallToGame=true
if errorlevel 1 (
  echo Build FAILED. See errors above.
) else (
  echo Build OK. ER2_FOVUnlock.dll copied to BepInEx\plugins.
)
pause
