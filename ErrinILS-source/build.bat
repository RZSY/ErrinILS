@echo off
setlocal enabledelayedexpansion

set ROOT=%~dp0..
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Error: csc.exe not found in Microsoft.NET Framework directory.
    exit /b 1
)

set MODE=%1
if "%MODE%"=="" set MODE=debug

set SOURCES=^
 %ROOT%\ErrinILS-source\src\Json.cs^
 %ROOT%\ErrinILS-source\src\Models.cs^
 %ROOT%\ErrinILS-source\src\Sqlite.cs^
 %ROOT%\ErrinILS-source\src\Marc.cs^
 %ROOT%\ErrinILS-source\src\Z3950.cs^
 %ROOT%\ErrinILS-source\src\Lib.cs^
 %ROOT%\ErrinILS-source\src\Lookup.cs^
 %ROOT%\ErrinILS-source\src\Zip.cs^
 %ROOT%\ErrinILS-source\src\Auth.cs^
 %ROOT%\ErrinILS-source\src\NetHelper.cs^
 %ROOT%\ErrinILS-source\src\Qr.cs^
 %ROOT%\ErrinILS-source\src\CertHelper.cs^
 %ROOT%\ErrinILS-source\src\Updater.cs^
 %ROOT%\ErrinILS-source\src\CloudSync.cs^
 %ROOT%\ErrinILS-source\src\AssemblyInfo.cs^
 %ROOT%\ErrinILS-source\src\Ui.cs^
 %ROOT%\ErrinILS-source\src\Simple.cs^
 %ROOT%\ErrinILS-source\src\Web.cs^
 %ROOT%\ErrinILS-source\src\MainForm.cs^
 %ROOT%\ErrinILS-source\src\Dialogs.cs^
 %ROOT%\ErrinILS-source\src\Program.cs

if /i "%MODE%"=="debug" (
    if not exist "%ROOT%\bin\Debug" mkdir "%ROOT%\bin\Debug"
    "%CSC%" /nologo /target:winexe /optimize- /debug:full ^
        /win32icon:"%ROOT%\ErrinILS-source\icon.ico" ^
        /win32manifest:"%ROOT%\ErrinILS-source\app.manifest" ^
        /r:System.Drawing.dll,System.Windows.Forms.dll ^
        /out:"%ROOT%\bin\Debug\ErrinILS.exe" ^
        !SOURCES!
    exit /b !errorlevel!
)

if /i "%MODE%"=="release" (
    if not exist "%ROOT%\dist" mkdir "%ROOT%\dist"
    if not exist "%ROOT%\ErrinILS-source\dist" mkdir "%ROOT%\ErrinILS-source\dist"
    "%CSC%" /nologo /target:winexe /optimize+ /debug- ^
        /win32icon:"%ROOT%\ErrinILS-source\icon.ico" ^
        /win32manifest:"%ROOT%\ErrinILS-source\app.manifest" ^
        /r:System.Drawing.dll,System.Windows.Forms.dll ^
        /out:"%ROOT%\dist\ErrinILS.exe" ^
        !SOURCES!
    if !errorlevel! neq 0 exit /b !errorlevel!
    copy /y "%ROOT%\dist\ErrinILS.exe" "%ROOT%\ErrinILS-source\dist\ErrinILS.exe" >nul
    copy /y "%ROOT%\dist\ErrinILS.exe" "%ROOT%\ErrinILS-Windows\ErrinILS\ErrinILS.exe" >nul
    exit /b 0
)

if /i "%MODE%"=="installer" (
    call :build_installer
    exit /b !errorlevel!
)

if /i "%MODE%"=="selftest" (
    if not exist "%ROOT%\bin\Debug" mkdir "%ROOT%\bin\Debug"
    set TEST_SOURCES=^
     %ROOT%\ErrinILS-source\src\Json.cs^
     %ROOT%\ErrinILS-source\src\Models.cs^
     %ROOT%\ErrinILS-source\src\Sqlite.cs^
     %ROOT%\ErrinILS-source\src\Marc.cs^
     %ROOT%\ErrinILS-source\src\Z3950.cs^
     %ROOT%\ErrinILS-source\src\Lib.cs^
     %ROOT%\ErrinILS-source\src\Lookup.cs^
     %ROOT%\ErrinILS-source\src\Zip.cs^
     %ROOT%\ErrinILS-source\src\Auth.cs^
     %ROOT%\ErrinILS-source\src\NetHelper.cs^
     %ROOT%\ErrinILS-source\src\Qr.cs^
     %ROOT%\ErrinILS-source\src\CertHelper.cs^
     %ROOT%\ErrinILS-source\src\Updater.cs^
     %ROOT%\ErrinILS-source\src\CloudSync.cs^
     %ROOT%\ErrinILS-source\src\Web.cs^
     %ROOT%\ErrinILS-source\src\SelfTest.cs
    "%CSC%" /nologo /target:exe /optimize- /debug:full ^
        /r:System.Drawing.dll ^
        /out:"%ROOT%\bin\Debug\selftest.exe" ^
        !TEST_SOURCES!
    exit /b !errorlevel!
)

echo Unknown mode: %MODE%
exit /b 1

:build_installer
call "%~f0" release
if !errorlevel! neq 0 exit /b !errorlevel!
set MAKENSIS=makensis.exe
where makensis.exe >nul 2>&1
if !errorlevel! neq 0 (
    if exist "%LOCALAPPDATA%\nsis\nsis-3.10\makensis.exe" set MAKENSIS=%LOCALAPPDATA%\nsis\nsis-3.10\makensis.exe
    if not exist "!MAKENSIS!" if exist "%ProgramFiles%\NSIS\makensis.exe" set MAKENSIS=%ProgramFiles%\NSIS\makensis.exe
)
if not exist "!MAKENSIS!" (
    where makensis >nul 2>&1
    if !errorlevel! neq 0 (
        echo Error: makensis.exe not found.
        exit /b 1
    )
    set MAKENSIS=makensis
)
pushd "%ROOT%\ErrinILS-source"
echo Building installer with !MAKENSIS!...
"!MAKENSIS!" -V2 -DVERSION=1.4.2 installer.nsi
set NSIS_ERR=!errorlevel!
popd
if !NSIS_ERR! neq 0 exit /b !NSIS_ERR!
if exist "%ROOT%\ErrinILS-source\dist\ErrinILS-Setup-1.4.2.exe" (
    copy /y "%ROOT%\ErrinILS-source\dist\ErrinILS-Setup-1.4.2.exe" "%ROOT%\dist\ErrinILS-Setup-1.4.2.exe" >nul
    copy /y "%ROOT%\ErrinILS-source\dist\ErrinILS-Setup-1.4.2.exe" "%ROOT%\ErrinILS-Windows\ErrinILS-Setup-1.4.2.exe" >nul
)
exit /b 0
