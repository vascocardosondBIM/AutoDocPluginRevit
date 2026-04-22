; Instalador do add-in Revit — compilado via Nuke (alvo Installer).
; Requer Inno Setup 6: https://jrsoftware.org/isdl.php

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef RevitYear
  #define RevitYear "2025"
#endif
#ifndef StagingDir
  #define StagingDir "..\artifacts\staging"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define MyAppName "AutoDocumentation"

[Setup]
AppId={{A1C2E3F4-5B6D-7890-ABCD-EF1234567890}
AppName=Assistente de parâmetros (Revit {#RevitYear})
AppVersion={#MyAppVersion}
AppPublisher=AutoDocumentation
DefaultDirName={userappdata}\Autodesk\Revit\Addins\{#RevitYear}\{#MyAppName}
OutputDir={#OutputDir}
OutputBaseFilename=AutoDocumentation-Revit{#RevitYear}-{#MyAppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes
DisableDirPage=yes
UninstallDisplayIcon={app}\AutoDocumentation.dll

[Files]
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "AutoDocumentation.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\{#RevitYear}"; Flags: ignoreversion
