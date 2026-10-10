#!/bin/bash
# Builds ErrinILS (native Windows / .NET Framework 4 WinForms) and its installer.
# Needs:  mono-mcs mono-devel (compiler + .NET 4.0 reference assemblies) and nsis (installer).
#   Ubuntu/Debian:  sudo apt-get install mono-mcs mono-devel nsis
# Output:
#   dist/ErrinILS.exe                    the program (single file)
#   dist/ErrinILS-Setup-<version>.exe    installer (Start-menu + desktop shortcuts, uninstaller)
#   dist/ErrinILS-Windows.zip            portable copy (unzip and run ErrinILS.exe)
set -e
VERSION=1.2.2
cd "$(dirname "$0")"
REF=/usr/lib/mono/4.0-api
CORE="src/Json.cs src/Models.cs src/Marc.cs src/Z3950.cs src/Lib.cs src/Lookup.cs src/Zip.cs src/Auth.cs"
FLAGS="-nostdlib -lib:$REF -r:mscorlib.dll,System.dll,System.Core.dll"

echo "== self-test (MARC, Z39.50, circulation rules) =="
mkdir -p build dist
mcs $FLAGS -out:build/selftest.exe $CORE src/SelfTest.cs
mono build/selftest.exe

echo "== compiling ErrinILS.exe =="
mcs $FLAGS -r:System.Drawing.dll,System.Windows.Forms.dll -target:winexe -optimize+ -debug- \
    -win32icon:icon.ico -out:dist/ErrinILS.exe \
    $CORE src/AssemblyInfo.cs src/Ui.cs src/Simple.cs src/MainForm.cs src/Dialogs.cs src/Program.cs

echo "== installer =="
makensis -V2 -DVERSION=$VERSION installer.nsi

echo "== portable zip =="
rm -rf build/portable && mkdir -p build/portable/ErrinILS
cp dist/ErrinILS.exe README.txt build/portable/ErrinILS/
(cd build/portable && zip -qr ../../dist/ErrinILS-Windows.zip ErrinILS)
ls -la dist
