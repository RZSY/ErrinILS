; ErrinILS installer (NSIS). Built by build.sh:  makensis -DVERSION=1.1.0 installer.nsi
Unicode true
!ifndef VERSION
  !define VERSION "1.2.2"
!endif
!define APP "ErrinILS"
!define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ErrinILS"
!include "MUI2.nsh"
!include "x64.nsh"

Name "${APP}"
OutFile "dist\ErrinILS-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES\${APP}"
InstallDirRegKey HKLM "Software\${APP}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "ErrinILS ${VERSION}"
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "FileDescription" "ErrinILS school library system installer"
VIAddVersionKey "CompanyName" "ErrinILS"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "ErrinILS"

!define MUI_ICON "icon.ico"
!define MUI_UNICON "icon.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\ErrinILS.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start ErrinILS now"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ; ErrinILS needs the .NET Framework 4.x (built into Windows 8 and newer; Windows 7 may need it installed once).
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 == ""
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client" "Release"
  ${EndIf}
  ${If} $0 == ""
    MessageBox MB_YESNO|MB_ICONEXCLAMATION "ErrinILS needs the Microsoft .NET Framework 4 (or newer), which was not found on this computer.$\r$\n$\r$\nOn Windows 7 you can install it free from Microsoft (search for $\"NET Framework 4.8 offline installer$\").$\r$\n$\r$\nContinue installing anyway?" IDYES +2
    Abort
  ${EndIf}
FunctionEnd

Section "Install"
  SetOutPath "$INSTDIR"
  ; stop a running copy so the file can be replaced
  nsExec::Exec 'taskkill /F /IM ErrinILS.exe'
  Sleep 300
  File "dist\ErrinILS.exe"
  File "README.txt"
  File "icon.ico"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\${APP}"
  CreateShortcut "$SMPROGRAMS\${APP}\${APP}.lnk" "$INSTDIR\ErrinILS.exe" "" "$INSTDIR\ErrinILS.exe" 0
  CreateShortcut "$SMPROGRAMS\${APP}\Uninstall ${APP}.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\ErrinILS.exe" "" "$INSTDIR\ErrinILS.exe" 0
  WriteRegStr HKLM "Software\${APP}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNKEY}" "DisplayName" "${APP}"
  WriteRegStr HKLM "${UNKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNKEY}" "Publisher" "ErrinILS"
  WriteRegStr HKLM "${UNKEY}" "DisplayIcon" "$INSTDIR\ErrinILS.exe"
  WriteRegStr HKLM "${UNKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNKEY}" "NoRepair" 1
  SectionGetSize 0 $0
  WriteRegDWORD HKLM "${UNKEY}" "EstimatedSize" $0
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM ErrinILS.exe'
  Sleep 300
  Delete "$INSTDIR\ErrinILS.exe"
  Delete "$INSTDIR\README.txt"
  Delete "$INSTDIR\icon.ico"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${APP}\${APP}.lnk"
  Delete "$SMPROGRAMS\${APP}\Uninstall ${APP}.lnk"
  RMDir "$SMPROGRAMS\${APP}"
  Delete "$DESKTOP\${APP}.lnk"
  DeleteRegKey HKLM "${UNKEY}"
  DeleteRegKey HKLM "Software\${APP}"
  ; Library data in %APPDATA%\Errin is deliberately kept.
SectionEnd
