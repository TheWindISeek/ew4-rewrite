@echo off
setlocal
for /f "delims=" %%D in ('dir /b /ad /o-d "%~dp0build\release-*" 2^>nul') do (
    if exist "%~dp0build\%%D\web\Ew4.Backend.Web.exe" if exist "%~dp0build\%%D\web\Start-Web.cmd" (
        call "%~dp0build\%%D\web\Start-Web.cmd"
        exit /b
    )
)
echo No complete Windows Web package was found under:
echo %~dp0build\release-*\web
echo Download and extract the Windows Web ZIP, then run its Start-Web.cmd.
pause
exit /b 1
