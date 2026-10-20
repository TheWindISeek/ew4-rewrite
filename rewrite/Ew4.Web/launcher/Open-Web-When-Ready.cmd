@echo off
for /l %%N in (1,1,60) do (
    curl.exe --noproxy "*" --fail --silent --max-time 1 "%EW4_WEB_URL%api/mods" >nul 2>nul
    if not errorlevel 1 (
        start "" "%EW4_WEB_URL%"
        exit /b 0
    )
    timeout /t 1 /nobreak >nul 2>nul
)
echo Browser was not opened because the service did not become ready.
echo Check the service error in this window. Address: %EW4_WEB_URL%
exit /b 1
