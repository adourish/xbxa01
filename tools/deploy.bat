@echo off
:: ============================================================
:: deploy.bat  —  Build Unity APK and deploy to Pixel 9
:: ============================================================
:: Prerequisites:
::   1. Unity 2022.3 LTS or Unity 6 installed
::   2. Android Build Support + Android SDK installed in Unity Hub
::   3. Android SDK platform-tools in PATH (adb.exe)
::   4. Pixel 9 connected via USB with ADB debugging enabled
::      (Settings > Developer Options > USB Debugging)
::
:: Usage:
::   deploy.bat [debug|release]   (default: debug)
:: ============================================================

setlocal

set BUILD_TYPE=%1
if "%BUILD_TYPE%"=="" set BUILD_TYPE=debug

set APK_NAME=xbxa01.apk
set PACKAGE=com.xbxa01.glassesvr
set UNITY_PATH=C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe

:: -- Adjust this path to your Unity installation --
if not exist "%UNITY_PATH%" (
    echo [ERROR] Unity not found at: %UNITY_PATH%
    echo Edit UNITY_PATH in this script to match your install.
    exit /b 1
)

:: Check ADB
where adb >nul 2>&1
if errorlevel 1 (
    echo [ERROR] adb not found in PATH.
    echo Add Android SDK platform-tools to PATH:
    echo   %LOCALAPPDATA%\Android\Sdk\platform-tools
    exit /b 1
)

:: Check device
adb devices | findstr /R "device$" >nul
if errorlevel 1 (
    echo [ERROR] No Android device detected via ADB.
    echo Check: Settings ^> Developer Options ^> USB Debugging = ON
    exit /b 1
)

echo [INFO] Device found.
for /f "tokens=1" %%d in ('adb devices ^| findstr /R "device$"') do (
    echo [INFO] Device: %%d
    adb -s %%d shell getprop ro.product.model
    adb -s %%d shell getprop ro.build.version.release
)

:: ---- Unity headless build ----
echo [INFO] Building Unity APK (%BUILD_TYPE%)...

:: This script lives in tools/; the Unity project root is the repo root.
set PROJECT_PATH=%~dp0..
set BUILD_DIR=%~dp0..\build
set OUTPUT_PATH=%BUILD_DIR%\%APK_NAME%
set BUILD_METHOD=BuildScript.BuildAndroid%BUILD_TYPE%

if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"

"%UNITY_PATH%" ^
    -batchmode ^
    -quit ^
    -logFile "%BUILD_DIR%\unity_build.log" ^
    -projectPath "%PROJECT_PATH%" ^
    -executeMethod %BUILD_METHOD% ^
    -outputPath "%OUTPUT_PATH%"

if errorlevel 1 (
    echo [ERROR] Unity build failed. Check %BUILD_DIR%\unity_build.log
    exit /b 1
)

echo [INFO] Build complete: %OUTPUT_PATH%

:: ---- Install APK ----
echo [INFO] Installing APK...
adb install -r "%OUTPUT_PATH%"
if errorlevel 1 (
    echo [ERROR] APK install failed.
    exit /b 1
)

:: ---- Launch app ----
echo [INFO] Launching app...
adb shell am start -n %PACKAGE%/com.unity3d.player.UnityPlayerActivity

echo.
echo [SUCCESS] App deployed and launched.
echo.
echo Useful commands:
echo   adb logcat -s Unity:V          ^<-- Unity log output
echo   adb shell dumpsys display      ^<-- display info
echo   adb shell dumpsys sensorservice ^<-- sensor list
echo   adb shell dumpsys window displays ^<-- window/display state

endlocal
