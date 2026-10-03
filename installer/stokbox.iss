; Installateur de Stokbox (Inno Setup 6).
;
; Compilé par build-release.ps1, qui fournit :
;   /DAppVersion=<version de Directory.Build.props>
;   /DWithDotNetRedist   quand installer\redist\ndp48-x86-x64-allos-enu.exe est présent.
;
; Les données de la boutique vivent dans %ProgramData%\Stokbox : l'installateur ne les écrase jamais
; (mise à jour) et la désinstallation ne les supprime jamais.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "Stokbox"
#define AppExeName "Stokbox.exe"
#define AppBinaries "..\src\Stokbox.App\bin\Release\net48"
#define DotNetRedist "ndp48-x86-x64-allos-enu.exe"

[Setup]
; Ne jamais changer cet identifiant : c'est lui qui fait d'une nouvelle version une mise à jour de la précédente.
AppId={{7B0F2D6E-3C4A-4E7B-9A51-5D2C8E1F4B90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}

; Program Files, donc droits administrateur. Sur Windows 64 bits : le vrai « Program Files ».
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

; Windows 7 SP1 au minimum (exigence de .NET Framework 4.8).
MinVersion=6.1sp1

OutputDir=..\dist
OutputBaseFilename=Stokbox-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}

; Mise à jour par-dessus une version ouverte : l'installateur propose de fermer Stokbox.
CloseApplications=yes
RestartApplications=no

#ifexist "..\assets\stokbox.ico"
SetupIconFile=..\assets\stokbox.ico
#endif

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"

[Dirs]
; Dossier des données, modifiable par tous les comptes Windows du poste : sans cela, seul le compte qui a
; lancé Stokbox le premier pourrait écrire dans la base. Créé s'il n'existe pas ; son contenu n'est jamais touché.
Name: "{commonappdata}\{#AppName}"; Permissions: users-modify; Flags: uninsneveruninstall

[InstallDelete]
; Mise à jour : les bibliothèques de la version précédente sont retirées avant la copie, pour qu'une
; bibliothèque abandonnée ne reste pas à côté des nouvelles. Seul le dossier du programme est concerné.
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.exe.config"
Type: filesandordirs; Name: "{app}\x86"
Type: filesandordirs; Name: "{app}\x64"

[Files]
; Tout le résultat du build Release, y compris x86\ et x64\ (bibliothèque native de SQLite pour chaque architecture).
Source: "{#AppBinaries}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

#ifdef WithDotNetRedist
; Programme d'installation hors ligne de .NET Framework 4.8 : extrait seulement s'il faut l'exécuter.
Source: "redist\{#DotNetRedist}"; Flags: dontcopy
#endif

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Lancer {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  // Valeur « Release » du registre à partir de laquelle .NET Framework 4.8 est installé.
  DotNet48Release = 528040;

var
  DotNetNeedsRestart: Boolean;

function IsDotNet48Installed(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
    and (Release >= DotNet48Release);
end;

// Appelé juste avant la copie des fichiers : installe .NET Framework 4.8 s'il manque.
// Une chaîne non vide arrête l'installation en affichant ce message.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if IsDotNet48Installed() then
    Exit;

#ifdef WithDotNetRedist
  ExtractTemporaryFile('{#DotNetRedist}');

  if not Exec(ExpandConstant('{tmp}\{#DotNetRedist}'), '/passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Le programme d''installation de .NET Framework 4.8 n''a pas pu être lancé : ' + SysErrorMessage(ResultCode);
    Exit;
  end;

  case ResultCode of
    0:
      ;
    1641, 3010:
      // .NET est installé mais Windows doit redémarrer : Stokbox est installé, puis le redémarrage est proposé.
      DotNetNeedsRestart := True;
    1602:
      Result := 'L''installation de .NET Framework 4.8 a été annulée. Stokbox en a besoin pour fonctionner.';
    5100:
      Result := 'Cet ordinateur ne remplit pas les conditions de .NET Framework 4.8.' + #13#10
        + 'Sous Windows 7, installez d''abord le Service Pack 1 et les mises à jour Windows, puis relancez cette installation.';
  else
    Result := 'L''installation de .NET Framework 4.8 a échoué (code ' + IntToStr(ResultCode) + ').' + #13#10
      + 'Installez .NET Framework 4.8 puis relancez cette installation.';
  end;
#else
  Result := 'Stokbox a besoin de .NET Framework 4.8, qui n''est pas installé sur cet ordinateur.' + #13#10
    + 'Installez .NET Framework 4.8 puis relancez cette installation.';
#endif
end;

function NeedRestart(): Boolean;
begin
  Result := DotNetNeedsRestart;
end;

// La désinstallation retire le programme et les raccourcis, jamais les données, et le dit.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent() then
    MsgBox(
      'Stokbox a été désinstallé.' + #13#10 + #13#10
      + 'Vos données (produits, stock, ventes, paramètres et sauvegardes) ont été conservées dans :' + #13#10
      + ExpandConstant('{commonappdata}\{#AppName}') + #13#10 + #13#10
      + 'Elles seront retrouvées si Stokbox est réinstallé. Pour les effacer définitivement, supprimez ce dossier.',
      mbInformation, MB_OK);
end;
