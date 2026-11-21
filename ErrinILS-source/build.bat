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
    "%CSC%" /nologo /target:winexe /optimize+ /debug- ^
        /win32icon:"%ROOT%\ErrinILS-source\icon.ico" ^
        /win32manifest:"%ROOT%\ErrinILS-source\app.manifest" ^
        /r:System.Drawing.dll,System.Windows.Forms.dll ^
        /out:"%ROOT%\dist\ErrinILS.exe" ^
        !SOURCES!
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
