@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "APP=%~dp0publish\YandexMusicController.exe"
set "PROJECT=%~dp0YandexMusicController.csproj"

rem Prefer already published EXE.
if exist "%APP%" (
  start "" "%APP%" --background
  exit /b 0
)
if exist "%~dp0YandexMusicController.exe" (
  start "" "%~dp0YandexMusicController.exe" --background
  exit /b 0
)

where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK was not found. Install a .NET SDK, then run start.bat again.
  echo https://dotnet.microsoft.com/download
  pause
  exit /b 1
)

if not exist "%PROJECT%" (
  echo Project file was not found:
  echo "%PROJECT%"
  pause
  exit /b 1
)

echo Detected .NET SDK:
dotnet --version
if errorlevel 1 (
  echo The .NET CLI could not select an SDK.
  pause
  exit /b 1
)

rem Remove leftover obsolete files that break the build if an old extract was merged.
if exist "%~dp0HistoryWindow.xaml" del /q "%~dp0HistoryWindow.xaml"
if exist "%~dp0HistoryWindow.xaml.cs" del /q "%~dp0HistoryWindow.xaml.cs"
if exist "%~dp0obj" rd /s /q "%~dp0obj"
if exist "%~dp0bin" rd /s /q "%~dp0bin"

echo.
echo First launch: restoring packages and publishing the Windows application...
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0publish"
if errorlevel 1 (
  echo.
  echo Publish failed. Review the build errors above.
  pause
  exit /b 1
)

if not exist "%APP%" (
  echo Publish completed, but the expected EXE was not found:
  echo "%APP%"
  pause
  exit /b 1
)

start "" "%APP%" --background
endlocal
