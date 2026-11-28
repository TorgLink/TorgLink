; TorgLink.WinForms (.NET Framework 4.7.2) installer. Defines from nsis_winforms_*.ps1:
;   APP_VERSION APP_PRODUCT_VERSION ARCH SOURCE_DIR OUT_FILE
Unicode true
SetCompressor /SOLID lzma

!ifndef APP_NAME
  !define APP_NAME "TorgLink WinForms"
!endif
!ifndef APP_ID
  !define APP_ID "TorgLinkWinForms"
!endif
!ifndef APP_VERSION
  !define APP_VERSION "0.2.0"
!endif
!ifndef APP_PRODUCT_VERSION
  !define APP_PRODUCT_VERSION "0.2.0.0"
!endif
!ifndef ARCH
  !define ARCH "x86"
!endif
!ifndef EXE_NAME
  !define EXE_NAME "TorgLink.WinForms.exe"
!endif
!ifndef SOURCE_DIR
  !error "SOURCE_DIR is required"
!endif
!ifndef OUT_FILE
  !error "OUT_FILE is required"
!endif

!define UNINSTALL_REG "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"
; NDP v4 Full Release: 461808 = 4.7.2 (4.8 has a higher value and is accepted).
!define NET472_RELEASE 461808
!define NET472_URL "https://dotnet.microsoft.com/download/dotnet-framework/net472"

!include "MUI2.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "LogicLib.nsh"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "${OUT_FILE}"
!if "${ARCH}" == "x64"
  InstallDir "$PROGRAMFILES64\${APP_NAME}"
!else
  InstallDir "$PROGRAMFILES\${APP_NAME}"
!endif
InstallDirRegKey HKLM "${UNINSTALL_REG}" "InstallLocation"
RequestExecutionLevel admin
BrandingText "${APP_NAME} ${APP_VERSION}  ${ARCH}"

VIProductVersion "${APP_PRODUCT_VERSION}"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"

!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Russian"

Function .onInit
  !if "${ARCH}" == "x64"
    ${IfNot} ${RunningX64}
      MessageBox MB_OK|MB_ICONSTOP "This installer is for 64-bit Windows only. / Этот установщик только для 64-bit Windows."
      Abort
    ${EndIf}
    SetRegView 64
  !else
    ${If} ${RunningX64}
      SetRegView 64
    ${EndIf}
  !endif

  ${IfNot} ${AtLeastWin7}
    MessageBox MB_OK|MB_ICONSTOP "Windows 7 SP1 or later is required. / Нужен Windows 7 SP1 или новее."
    Abort
  ${EndIf}

  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  IntCmp $0 ${NET472_RELEASE} net472_ok net472_missing net472_ok
net472_missing:
  MessageBox MB_YESNO|MB_ICONSTOP "This application requires .NET Framework 4.7.2.$\r$\nНужен .NET Framework 4.7.2.$\r$\n$\r$\nOpen download page? / Открыть страницу загрузки?" IDYES open_net472
  Abort
open_net472:
  ExecShell "open" "${NET472_URL}"
  Abort
net472_ok:
FunctionEnd

Section "Install"
  SetOutPath "$INSTDIR"
  File /r "${SOURCE_DIR}\*.*"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${EXE_NAME}"
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${EXE_NAME}"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_REG}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${UNINSTALL_REG}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${UNINSTALL_REG}" "Publisher" "TorgLink"
  WriteRegStr HKLM "${UNINSTALL_REG}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_REG}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${UNINSTALL_REG}" "DisplayIcon" "$INSTDIR\${EXE_NAME}"
  WriteRegDWORD HKLM "${UNINSTALL_REG}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_REG}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  !if "${ARCH}" == "x64"
    SetRegView 64
  !else
    ${If} ${RunningX64}
      SetRegView 64
    ${EndIf}
  !endif
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "${UNINSTALL_REG}"
SectionEnd
