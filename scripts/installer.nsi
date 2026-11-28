; TorgLink unpackaged MAUI installer. Defines are passed from nsis_win_*.ps1:
;   APP_NAME APP_VERSION APP_PRODUCT_VERSION WINVER ARCH EXE_NAME SOURCE_DIR OUT_FILE
Unicode true
SetCompressor /SOLID lzma

!ifndef APP_NAME
  !define APP_NAME "TorgLink"
!endif
!ifndef APP_VERSION
  !define APP_VERSION "0.2.0"
!endif
!ifndef APP_PRODUCT_VERSION
  !define APP_PRODUCT_VERSION "0.2.0.0"
!endif
!ifndef WINVER
  !define WINVER "10"
!endif
!ifndef ARCH
  !define ARCH "x64"
!endif
!ifndef EXE_NAME
  !define EXE_NAME "TorgLink.Maui.exe"
!endif
!ifndef SOURCE_DIR
  !error "SOURCE_DIR is required"
!endif
!ifndef OUT_FILE
  !error "OUT_FILE is required"
!endif

!define UNINSTALL_REG "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

!include "MUI2.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "LogicLib.nsh"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "${OUT_FILE}"
!if "${ARCH}" == "x32"
  InstallDir "$PROGRAMFILES\${APP_NAME}"
!else
  InstallDir "$PROGRAMFILES64\${APP_NAME}"
!endif
InstallDirRegKey HKLM "${UNINSTALL_REG}" "InstallLocation"
RequestExecutionLevel admin
BrandingText "${APP_NAME} ${APP_VERSION}  Windows ${WINVER} ${ARCH}"

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
  !if "${ARCH}" != "x32"
    ${IfNot} ${RunningX64}
      MessageBox MB_OK|MB_ICONSTOP "This installer is for 64-bit Windows only. / Этот установщик только для 64-bit Windows."
      Abort
    ${EndIf}
    SetRegView 64
  !endif

  !if "${WINVER}" == "7"
    ${IfNot} ${AtLeastWin7}
      MessageBox MB_OK|MB_ICONSTOP "Windows 7 or later is required. / Нужен Windows 7 или новее."
      Abort
    ${EndIf}
  !else if "${WINVER}" == "8"
    ${IfNot} ${AtLeastWin8}
      MessageBox MB_OK|MB_ICONSTOP "Windows 8 or later is required. / Нужен Windows 8 или новее."
      Abort
    ${EndIf}
  !else if "${WINVER}" == "11"
    ${IfNot} ${AtLeastWin10}
      MessageBox MB_OK|MB_ICONSTOP "Windows 11 (or Windows 10) is required. / Нужен Windows 11 (или Windows 10)."
      Abort
    ${EndIf}
  !else
    ${IfNot} ${AtLeastWin10}
      MessageBox MB_OK|MB_ICONSTOP "Windows 10 or later is required. / Нужен Windows 10 или новее."
      Abort
    ${EndIf}
  !endif
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
  WriteRegStr HKLM "${UNINSTALL_REG}" "Publisher" "${APP_NAME}"
  WriteRegStr HKLM "${UNINSTALL_REG}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_REG}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${UNINSTALL_REG}" "DisplayIcon" "$INSTDIR\${EXE_NAME}"
  WriteRegDWORD HKLM "${UNINSTALL_REG}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_REG}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  !if "${ARCH}" != "x32"
    SetRegView 64
  !endif
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "${UNINSTALL_REG}"
SectionEnd
