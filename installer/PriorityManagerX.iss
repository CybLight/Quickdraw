; Priority Manager X Installer (Inno Setup 6)

#define MyAppName "Priority Manager X"
#define MyAppExeName "PriorityManagerX.exe"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Priority Manager X"
#define MyAppURL "https://example.local/priority-manager-x"
#ifndef AppArch
	#define AppArch "x64"
#endif

#if AppArch == "x86"
	#define DotNetRuntimeArch "x86"
	#define DotNetRuntimeDownloadUrl "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x86.exe"
#else
	#define DotNetRuntimeArch "x64"
	#define DotNetRuntimeDownloadUrl "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
#endif

#define DotNetRuntimeOfflineFile "dotnet-desktop-runtime-8-" + DotNetRuntimeArch + ".exe"

[Setup]
AppId={{2A5C15BC-0228-4BA4-BCFB-BD8E7B55A891}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableProgramGroupPage=yes
LicenseFile=
InfoBeforeFile=
InfoAfterFile=
OutputDir=output
OutputBaseFilename=Priority Manager X-Setup-{#AppArch}
	SetupIconFile=..\assets\pmx-sharp.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
#if AppArch == "x86"
ArchitecturesAllowed=x86
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
ShowLanguageDialog=auto

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "uk"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "{cm:TaskAutoStart}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "runasadmin"; Description: "{cm:TaskRunAsAdmin}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion
Source: "offline-runtime\{#DotNetRuntimeOfflineFile}"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PriorityManagerX"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; ValueType: string; ValueName: "{app}\{#MyAppExeName}"; ValueData: "RUNASADMIN"; Tasks: runasadmin; Flags: uninsdeletevalue

[CustomMessages]
en.TaskAutoStart=Start Priority Manager X with Windows
en.TaskRunAsAdmin=Always run Priority Manager X as administrator
en.CtxSaveIdle=Save priority: Idle
en.CtxSaveBelowNormal=Save priority: Below Normal
en.CtxSaveNormal=Save priority: Normal
en.CtxSaveAboveNormal=Save priority: Above Normal
en.CtxSaveHigh=Save priority: High
en.CtxSaveRealTime=Save priority: Real Time
en.CtxRemoveSaved=Remove saved priority
en.RuntimeRequiredTitle=.NET Runtime Required
en.RuntimeRequiredBody=Priority Manager X requires Microsoft .NET 8 Windows Desktop Runtime ({#DotNetRuntimeArch}). Install it now?%n%nThe installer will use bundled offline runtime first. If unavailable, it will try downloading from Microsoft.
en.RuntimeInstallFailed=Runtime installation failed. Setup cannot continue.
en.RuntimeNotInstalled=Required runtime is still not detected after installation.

ru.TaskAutoStart=Запускать Priority Manager X вместе с Windows
ru.TaskRunAsAdmin=Всегда запускать Priority Manager X от имени администратора
ru.CtxSaveIdle=Сохранить приоритет: Низкий
ru.CtxSaveBelowNormal=Сохранить приоритет: Ниже обычного
ru.CtxSaveNormal=Сохранить приоритет: Обычный
ru.CtxSaveAboveNormal=Сохранить приоритет: Выше обычного
ru.CtxSaveHigh=Сохранить приоритет: Высокий
ru.CtxSaveRealTime=Сохранить приоритет: Реального времени
ru.CtxRemoveSaved=Удалить сохранённый приоритет
ru.RuntimeRequiredTitle=Требуется .NET Runtime
ru.RuntimeRequiredBody=Для Priority Manager X требуется Microsoft .NET 8 Windows Desktop Runtime ({#DotNetRuntimeArch}). Установить сейчас?%n%nСначала будет использован встроенный оффлайн runtime. Если он недоступен, установщик попробует скачать его с сайта Microsoft.
ru.RuntimeInstallFailed=Не удалось установить runtime. Установка будет прервана.
ru.RuntimeNotInstalled=После установки runtime не обнаружен. Установка будет прервана.

uk.TaskAutoStart=Запускати Priority Manager X разом із Windows
uk.TaskRunAsAdmin=Завжди запускати Priority Manager X від імені адміністратора
uk.CtxSaveIdle=Зберегти пріоритет: Низький
uk.CtxSaveBelowNormal=Зберегти пріоритет: Нижче звичайного
uk.CtxSaveNormal=Зберегти пріоритет: Звичайний
uk.CtxSaveAboveNormal=Зберегти пріоритет: Вище звичайного
uk.CtxSaveHigh=Зберегти пріоритет: Високий
uk.CtxSaveRealTime=Зберегти пріоритет: Реального часу
uk.CtxRemoveSaved=Видалити збережений пріоритет
uk.RuntimeRequiredTitle=Потрібен .NET Runtime
uk.RuntimeRequiredBody=Для Priority Manager X потрібен Microsoft .NET 8 Windows Desktop Runtime ({#DotNetRuntimeArch}). Встановити зараз?%n%nСпочатку буде використано вбудований офлайн runtime. Якщо він недоступний, інсталятор спробує завантажити його з Microsoft.
uk.RuntimeInstallFailed=Не вдалося встановити runtime. Встановлення буде перервано.
uk.RuntimeNotInstalled=Після встановлення runtime не виявлено. Встановлення буде перервано.

[Run]
Filename: "{cmd}"; Parameters: "/C powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""{app}\install-sparse-package.ps1"" -MsixPath ""{app}\PriorityManagerX.Sparse.msix"" -StoreScope LocalMachine"; Flags: runhidden waituntilterminated; Check: FileExists(ExpandConstant('{app}\install-sparse-package.ps1')) and FileExists(ExpandConstant('{app}\PriorityManagerX.Sparse.msix'))
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""{app}\uninstall-sparse-package.ps1"" -StoreScope LocalMachine"; RunOnceId: "PMXSparseUninstall"; Flags: runhidden waituntilterminated; Check: FileExists(ExpandConstant('{app}\uninstall-sparse-package.ps1'))
Filename: "{cmd}"; Parameters: "/C reg delete ""HKLM\Software\Classes\exefile\shell\PriorityManagerX"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKCU\Software\Classes\exefile\shell\PriorityManagerX"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKLM\Software\Classes\*\shell\PriorityManagerX"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKCU\Software\Classes\*\shell\PriorityManagerX"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKLM\Software\Classes\.exe\shell\PriorityManagerX"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKCU\Software\Classes\.exe\shell\PriorityManagerX"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKLM\Software\Classes\SystemFileAssociations\.exe\shell\PriorityManagerX"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKCU\Software\Classes\SystemFileAssociations\.exe\shell\PriorityManagerX"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKLM\Software\Classes\exefile\shell\PMXSimple"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKCU\Software\Classes\exefile\shell\PMXSimple"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKLM\Software\Classes\exefile\shell\PMXTest"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKCU\Software\Classes\exefile\shell\PMXTest"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKLM\Software\Classes\CLSID\{{4F6D1E31-87BA-4E31-9D2D-B9CA22A6E6D2}}"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKLM\Software\Classes\CLSID\{{B1B7D9D0-6A69-4A5D-9A89-9A8F59A17C22}}"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKCU\Software\Classes\CLSID\{{4F6D1E31-87BA-4E31-9D2D-B9CA22A6E6D2}}"" /f"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C reg delete ""HKCU\Software\Classes\CLSID\{{B1B7D9D0-6A69-4A5D-9A89-9A8F59A17C22}}"" /f"; Flags: runhidden waituntilterminated

[Code]
const
	SHCNE_ASSOCCHANGED = $08000000;
	SHCNF_IDLIST = $0000;

procedure SHChangeNotify(wEventId: Cardinal; uFlags: Cardinal; dwItem1: Integer; dwItem2: Integer);
	external 'SHChangeNotify@shell32.dll stdcall';

function GetDotNetInstallLocation(var InstallLocation: string): Boolean;
begin
#if DotNetRuntimeArch == "x86"
	Result := RegQueryStringValue(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x86', 'InstallLocation', InstallLocation);
#else
	Result := RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64', 'InstallLocation', InstallLocation);
#endif
end;

function CheckDotNetDesktopRuntimeInPath(const BasePath: string): Boolean;
var
	sharedRuntimeDir: string;
	findRec: TFindRec;
begin
	Result := False;
	sharedRuntimeDir := AddBackslash(BasePath) + 'shared\Microsoft.WindowsDesktop.App';
	if not DirExists(sharedRuntimeDir) then
		exit;
	if FindFirst(sharedRuntimeDir + '\8.*', findRec) then
	begin
		Result := True;
		FindClose(findRec);
	end;
end;

function IsDotNetDesktopRuntimeInstalled: Boolean;
var
	installLocation: string;
begin
	Result := False;
	if GetDotNetInstallLocation(installLocation) and (installLocation <> '') then
		Result := CheckDotNetDesktopRuntimeInPath(installLocation);
	if not Result then
	begin
#if DotNetRuntimeArch == "x86"
		Result := CheckDotNetDesktopRuntimeInPath(ExpandConstant('{pf32}\dotnet'));
#else
		Result := CheckDotNetDesktopRuntimeInPath(ExpandConstant('{pf}\dotnet'));
#endif
	end;
end;

function DownloadAndInstallDotNetDesktopRuntime: Boolean;
var
	runtimeInstaller: string;
	powershellArgs: string;
	exitCode: Integer;
begin
	runtimeInstaller := ExpandConstant('{tmp}\dotnet-desktop-runtime-8-{#DotNetRuntimeArch}.exe');
	powershellArgs :=
		'-NoProfile -ExecutionPolicy Bypass -Command "' +
		'$ProgressPreference = ''SilentlyContinue''; ' +
		'Invoke-WebRequest -Uri ''{#DotNetRuntimeDownloadUrl}'' -OutFile ''' + runtimeInstaller + '''"';

	if (not Exec(ExpandConstant('{cmd}'), '/C powershell.exe ' + powershellArgs, '', SW_HIDE, ewWaitUntilTerminated, exitCode)) or (exitCode <> 0) then
	begin
		Result := False;
		exit;
	end;

	if (not Exec(runtimeInstaller, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, exitCode)) then
	begin
		Result := False;
		exit;
	end;

	Result := (exitCode = 0) or (exitCode = 3010);
end;

function InstallBundledDotNetDesktopRuntime: Boolean;
var
	runtimeInstaller: string;
	exitCode: Integer;
begin
	Result := False;
	runtimeInstaller := ExpandConstant('{tmp}\{#DotNetRuntimeOfflineFile}');

	try
		ExtractTemporaryFile('{#DotNetRuntimeOfflineFile}');
	except
		exit;
	end;

	if not Exec(runtimeInstaller, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, exitCode) then
		exit;

	Result := (exitCode = 0) or (exitCode = 3010);
end;

function InitializeSetup(): Boolean;
var
	retries: Integer;
begin
	if IsDotNetDesktopRuntimeInstalled then
	begin
		Result := True;
		exit;
	end;

	if MsgBox(ExpandConstant('{cm:RuntimeRequiredBody}'), mbConfirmation, MB_YESNO) <> IDYES then
	begin
		Result := False;
		exit;
	end;

	if (not InstallBundledDotNetDesktopRuntime) and (not DownloadAndInstallDotNetDesktopRuntime) then
	begin
		MsgBox(ExpandConstant('{cm:RuntimeInstallFailed}'), mbCriticalError, MB_OK);
		Result := False;
		exit;
	end;

	// Runtime installer may update registry/filesystem with a delay; retry detection up to 5 times
	Result := False;
	retries := 5;
	while (retries > 0) and (not Result) do
	begin
		Result := IsDotNetDesktopRuntimeInstalled;
		if not Result then
		begin
			Sleep(2500);
			retries := retries - 1;
		end;
	end;
	if not Result then
		MsgBox(ExpandConstant('{cm:RuntimeNotInstalled}'), mbCriticalError, MB_OK);
end;

procedure RefreshExplorerShell;
begin
	SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, 0, 0);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
	if CurStep = ssPostInstall then
		RefreshExplorerShell;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
	if CurUninstallStep = usPostUninstall then
		RefreshExplorerShell;
end;
