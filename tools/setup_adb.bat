@echo off
:: ============================================================
:: setup_adb.bat  —  Find adb.exe and print PATH instructions
:: ============================================================
echo Looking for adb.exe...

set FOUND=0

:: Common Android SDK locations
for %%P in (
    "%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe"
    "%APPDATA%\Android\Sdk\platform-tools\adb.exe"
    "C:\Android\Sdk\platform-tools\adb.exe"
    "%ProgramFiles%\Android\android-sdk\platform-tools\adb.exe"
    "%ProgramFiles(x86)%\Android\android-sdk\platform-tools\adb.exe"
) do (
    if exist %%P (
        echo Found: %%P
        set ADB_PATH=%%~dpP
        set FOUND=1
        goto :found
    )
)

:notfound
echo.
echo adb.exe not found. Install Android SDK Platform Tools:
echo   1. Download: https://developer.android.com/tools/releases/platform-tools
echo   2. Extract to: %LOCALAPPDATA%\Android\Sdk\platform-tools\
echo   3. Re-run this script.
echo.
echo   OR install via Android Studio:
echo      Android Studio > SDK Manager > SDK Tools > Android SDK Platform-Tools
exit /b 1

:found
echo.
echo To add adb permanently to PATH, run this in PowerShell (as Admin):
echo.
echo   [Environment]::SetEnvironmentVariable("PATH", $env:PATH + ";%ADB_PATH%", "User")
echo.
echo Or add this to your current session:
echo   set PATH=%PATH%;%ADB_PATH%
echo.

:: Add to current session
set PATH=%PATH%;%ADB_PATH%
echo Added to current session PATH.
echo Testing adb...
"%ADB_PATH%adb.exe" devices

echo.
echo To check display (glasses must be connected):
echo   adb shell dumpsys display
echo.
echo To check sensors:
echo   adb shell dumpsys sensorservice
