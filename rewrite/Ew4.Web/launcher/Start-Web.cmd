@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0Ew4.Web.exe" (
    if exist "%~dp0..\Ew4.Web.csproj" if exist "%~dp0..\..\..\Start-Web.cmd" (
        call "%~dp0..\..\..\Start-Web.cmd"
        exit /b
    )
    echo Ew4.Web.exe is missing. Extract the entire Windows Web ZIP first.
    echo Expected location: %~dp0Ew4.Web.exe
    pause
    exit /b 1
)
where curl.exe >nul 2>nul
if errorlevel 1 (
    echo Windows curl.exe is required. See README.md for manual startup.
    pause
    exit /b 1
)
if not defined EW4_WEB_PORT set "EW4_WEB_PORT=5191"
if not defined EW4_WEB_SAVE_ROOT set "EW4_WEB_SAVE_ROOT=%~dp0saves"
set "EW4_WEB_URL="
set "EW4_WEB_REUSE="
set /a EW4_WEB_LAST_PORT=EW4_WEB_PORT+19
for /l %%P in (%EW4_WEB_PORT%,1,%EW4_WEB_LAST_PORT%) do (
    call :check_port %%P
    if defined EW4_WEB_URL goto selected
)
echo No available local port found. Close an unused local server and try again.
pause
exit /b 1

:selected
if defined EW4_WEB_REUSE (
    echo Opening the game already running at %EW4_WEB_URL%
    if not defined EW4_WEB_NO_BROWSER start "" "%EW4_WEB_URL%"
    exit /b 0
)
echo Game: %EW4_WEB_URL%
echo Saves: %EW4_WEB_SAVE_ROOT%
echo Keep this window open while playing. Save your game before stopping.
echo Press Ctrl+C here to stop the game service.
if not defined EW4_WEB_NO_BROWSER start "" /b cmd.exe /d /c call "%~dp0Open-Web-When-Ready.cmd"
"%~dp0Ew4.Web.exe" --urls "%EW4_WEB_URL%" --save-root "%EW4_WEB_SAVE_ROOT%" --contentRoot "%~dp0." --Logging:LogLevel:Default Warning
if errorlevel 1 (
    echo Game service failed. Please check the error above.
    pause
)
exit /b

:check_port
netstat -ano -p tcp | findstr /R /C:":%1 .*LISTENING" >nul
if errorlevel 1 (
    set "EW4_WEB_URL=http://127.0.0.1:%1/"
    exit /b
)
curl.exe --noproxy "*" --fail --silent --max-time 2 "http://127.0.0.1:%1/api/mods" >nul 2>nul
if not errorlevel 1 (
    set "EW4_WEB_URL=http://127.0.0.1:%1/"
    set "EW4_WEB_REUSE=1"
)
exit /b
