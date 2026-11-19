; Baby Clash: Windows setup wizard (NSIS 3, Modern UI 2).
; Built by Installer/build-installer.sh, which passes BUILD_DIR, ART_DIR, OUT_FILE, APP_VERSION and VI_VERSION.
; Pages: Welcome -> Install location -> Installing -> Finish (play now, desktop shortcut).

Unicode true
ManifestDPIAware true
SetCompressor /SOLID lzma

!define APP_NAME "Baby Clash"
!define APP_EXE "BabyClash.exe"
!define PUBLISHER "Baby Clash"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\BabyClash"
!define SETTINGS_KEY "Software\Baby Clash\Installer"

!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif
!ifndef VI_VERSION
  !define VI_VERSION "1.0.0.0"
!endif
!ifndef BUILD_DIR
  !define BUILD_DIR "../Builds/Windows"
!endif
!ifndef ART_DIR
  !define ART_DIR "../Builds/Installer/art"
!endif
!ifndef OUT_FILE
  !define OUT_FILE "../Builds/Installer/BabyClash-${APP_VERSION}-Setup.exe"
!endif

Name "${APP_NAME}"
OutFile "${OUT_FILE}"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "${SETTINGS_KEY}" "InstallDir"
RequestExecutionLevel admin
BrandingText "${APP_NAME} ${APP_VERSION}"

VIProductVersion "${VI_VERSION}"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "${PUBLISHER}"

!include "MUI2.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

; --- Look -------------------------------------------------------------------------------------------------------
!define MUI_ICON "${ART_DIR}/BabyClash.ico"
!define MUI_UNICON "${ART_DIR}/BabyClash.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${ART_DIR}/wizard.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "${ART_DIR}/wizard.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "${ART_DIR}/header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "${ART_DIR}/header.bmp"
!define MUI_ABORTWARNING

; --- Installer pages --------------------------------------------------------------------------------------------
!define MUI_WELCOMEPAGE_TITLE "Welcome to ${APP_NAME} Setup"
!define MUI_WELCOMEPAGE_TEXT "This will install ${APP_NAME} ${APP_VERSION} on your computer.$\r$\n$\r$\nTwo babies, two diapers and a playpen full of toys. Fling, dodge and belly-slide until the other baby bursts into tears. Play against the computer, or with a friend on one keyboard or two gamepads.$\r$\n$\r$\nClick Next to continue."
!insertmacro MUI_PAGE_WELCOME

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_TITLE "${APP_NAME} is ready to play"
!define MUI_FINISHPAGE_TEXT "${APP_NAME} has been installed on your computer. You can find it in the Start menu.$\r$\n$\r$\nClick Finish to close Setup."
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Play ${APP_NAME} now"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchGame
!define MUI_FINISHPAGE_SHOWREADME
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Create a desktop shortcut"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION CreateDesktopShortcut
!insertmacro MUI_PAGE_FINISH

; --- Uninstaller pages ------------------------------------------------------------------------------------------
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; --- Install ----------------------------------------------------------------------------------------------------
Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP|MB_OK "${APP_NAME} needs a 64-bit version of Windows."
    Abort
  ${EndIf}
  SetRegView 64
  SetShellVarContext all
FunctionEnd

Section "${APP_NAME}" SecGame
  SectionIn RO
  SetOutPath "$INSTDIR"
  ; Debug-symbol folders Unity writes next to the player are not part of the game, nor is Finder metadata
  ; picked up when the build folder is opened on a Mac.
  File /r /x "*_DoNotShip" /x "*_ButDontShipItWithYourGame" /x ".DS_Store" /x "._*" "${BUILD_DIR}/*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"

  WriteRegStr HKLM "${SETTINGS_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${UNINST_KEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" $0
SectionEnd

; The installer runs elevated; going through Explorer starts the game as the signed-in user instead.
Function LaunchGame
  Exec '"$WINDIR\explorer.exe" "$INSTDIR\${APP_EXE}"'
FunctionEnd

Function CreateDesktopShortcut
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
FunctionEnd

; --- Uninstall --------------------------------------------------------------------------------------------------
Function un.onInit
  SetRegView 64
  SetShellVarContext all
FunctionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\${APP_NAME}.lnk"
  Delete "$DESKTOP\${APP_NAME}.lnk"

  ; Only what Setup put there (listed from the build), so a custom folder that holds other files survives.
  !include "${ART_DIR}/uninstall-files.nsh"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"

  DeleteRegKey HKLM "${UNINST_KEY}"
  DeleteRegKey HKLM "${SETTINGS_KEY}"
  DeleteRegKey /ifempty HKLM "Software\Baby Clash"
SectionEnd
