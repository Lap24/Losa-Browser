@echo off
title Losa Browser Setup

echo ============================================
echo        Losa Browser Installer
echo ============================================
echo.

:: Step 1 — .NET 6.0 Desktop Runtime
echo Checking for .NET 6.0 Desktop Runtime...
dotnet --list-runtimes | findstr /i "Microsoft.WindowsDesktop.App 6." >nul

if %errorlevel% neq 0 (
    echo.
    echo You need .NET 6.0 Desktop Runtime.
    echo Opening download page...
    start https://dotnet.microsoft.com/en-us/download/dotnet/6.0

    echo.
    echo Press ENTER once you have installed .NET 6.0.
    pause
) else (
    echo .NET 6.0 Desktop Runtime is already installed.
)

echo.
echo ============================================
echo Building Losa Browser (Release mode)
echo ============================================
dotnet build -c Release
echo Build complete.
echo.

:: Step 2 — Install to Program Files
set "TARGET=C:\Program Files\LosaBrowser"

echo Installing to:
echo %TARGET%
echo.

echo Creating folder...
mkdir "%TARGET%" >nul 2>&1

echo Copying files...
xcopy ".\LosaBrowser" "%TARGET%" /E /I /Y >nul

echo Files copied successfully.
echo.

:: Step 3 — Create desktop launcher
echo Creating desktop launcher...

set "DESKTOP=%USERPROFILE%\Desktop"
set "SHORTCUT=%DESKTOP%\LosaBrowser.bat"

(
echo @echo off
echo start "" "%TARGET%\Losa Browser.exe"
) > "%SHORTCUT%"

echo Shortcut created:
echo %SHORTCUT%
echo.

echo ============================================
echo Installation complete!
echo You can now launch Losa Browser from:
echo - Desktop shortcut
echo - %TARGET%\Losa Browser.exe
echo ============================================
echo.
pause
exit
