using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace PriorityManagerX
{
    public static class WindowsIntegration
    {
        static readonly string IfeoPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
        static readonly string ExplorerMenuPath = @"Software\Classes\exefile\shell\PriorityManagerX";
        static readonly string LegacyExplorerMenuPath = @"Software\Classes\*\shell\PriorityManagerX";
        static readonly string LegacyFileMenuPath = @"Software\Classes\.exe\shell\PriorityManagerX";
        static readonly string SystemFileAssocMenuPath = @"Software\Classes\SystemFileAssociations\.exe\shell\PriorityManagerX";
        static readonly string ModernMenuPath = @"Software\Classes\*\shell\PriorityManagerXWin11";
        static readonly string CommandStorePath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell";
        const string ExplorerCommandHandlerClsid = "{B1B7D9D0-6A69-4A5D-9A89-9A8F59A17C22}";
        const string PriorityManagerPrimaryClsid = "{4F6D1E31-87BA-4E31-9D2D-B9CA22A6E6D2}";
        const string StartupTaskName = "PriorityManagerX AutoApply";
        const string GuiStartupTaskName = "PriorityManagerX GUI";
        const string CoreStartupTaskName = "PriorityManagerX Core Engine";
        const string GuiRunValueName = "PriorityManagerX.GUI";
        const string CoreRunValueName = "PriorityManagerX.CoreWatchdog";
        static readonly string CurrentUserRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        static readonly string AppCompatLayersPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        const uint SHCNE_ASSOCCHANGED = 0x08000000;
        const uint SHCNF_IDLIST = 0x0000;

        public static bool SetDefaultPriority(string processOrPath, ProcessPriorityClass priority, out string scope, out string error, AppLanguage language = AppLanguage.Russian)
        {
            error = string.Empty;
            var exe = NormalizeExeName(processOrPath);
            if (string.IsNullOrWhiteSpace(exe))
            {
                scope = string.Empty;
                error = WithLanguage(language, () => L10n.ErrSpecifyProcessOrPath);
                return false;
            }

            var value = ToCpuPriorityClass(priority);

            if (TryWriteCpuPriority(Registry.LocalMachine, exe, value, out error, language))
            {
                scope = "HKLM";
                return true;
            }

            scope = string.Empty;
            return false;
        }

        public static bool RemoveDefaultPriority(string processOrPath, out string error, AppLanguage language = AppLanguage.Russian)
        {
            error = string.Empty;
            var exe = NormalizeExeName(processOrPath);
            if (string.IsNullOrWhiteSpace(exe))
            {
                error = WithLanguage(language, () => L10n.ErrSpecifyProcessOrPath);
                return false;
            }

            var removed = false;
            removed |= TryDeleteCpuPriority(Registry.LocalMachine, exe, out _);
            removed |= TryDeleteCpuPriority(Registry.CurrentUser, exe, out _);

            if (!removed)
                error = WithLanguage(language, () => L10n.ErrNotFoundSavedPriority);

            return removed;
        }

        public static bool TryGetSavedPriority(string processOrPath, out ProcessPriorityClass priority, out string scope)
        {
            priority = ProcessPriorityClass.Normal;
            scope = string.Empty;

            var exe = NormalizeExeName(processOrPath);
            if (string.IsNullOrWhiteSpace(exe))
                return false;

            if (TryReadCpuPriority(Registry.LocalMachine, exe, out var localMachinePriority))
            {
                priority = localMachinePriority;
                scope = "HKLM";
                return true;
            }

            if (TryReadCpuPriority(Registry.CurrentUser, exe, out var currentUserPriority))
            {
                priority = currentUserPriority;
                scope = "HKCU";
                return true;
            }

            return false;
        }

        public static bool ConfigureStartupTask(string appPath, bool enabled, bool runAsAdministrator, out string error)
        {
            error = string.Empty;

            if (!enabled)
                return DeleteStartupTask(out error);

            return CreateOrUpdateStartupTask(appPath, runAsAdministrator, out error);
        }

        public static bool ConfigureStartupProfile(string appPath, AppSettings settings, out string error)
        {
            error = string.Empty;
            var exePath = ResolvePreferredMachineAppPath(appPath);

            if (!DeleteRunEntry(GuiRunValueName, out error))
                return false;
            if (!DeleteRunEntry(CoreRunValueName, out error))
                return false;
            if (!DeleteTaskIfExists(StartupTaskName, out error))
                return false;
            if (!DeleteTaskIfExists(GuiStartupTaskName, out error))
                return false;
            if (!DeleteTaskIfExists(CoreStartupTaskName, out error))
                return false;

            if (!ConfigureGuiStartup(exePath, settings.GuiStartupMode, out error))
                return false;
            if (!ConfigureCoreStartup(exePath, settings.CoreEngineStartupMode, out error))
                return false;

            return true;
        }

        public static bool SetRunAsAdministratorFlag(string appPath, bool enabled, out string error)
        {
            error = string.Empty;
            try
            {
                var exePath = ResolvePreferredMachineAppPath(appPath);
                using var layers = Registry.CurrentUser.CreateSubKey(AppCompatLayersPath, true);
                if (layers == null)
                {
                    error = "Failed to open AppCompat Layers key.";
                    return false;
                }

                if (enabled)
                    layers.SetValue(exePath, "RUNASADMIN", RegistryValueKind.String);
                else if (Array.Exists(layers.GetValueNames(), v => string.Equals(v, exePath, StringComparison.OrdinalIgnoreCase)))
                    layers.DeleteValue(exePath, false);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static bool CreateOrUpdateStartupTask(string appPath, bool runAsAdministrator, out string error)
        {
            error = string.Empty;
            try
            {
                var exePath = ResolvePreferredMachineAppPath(appPath);
                var runLevel = runAsAdministrator ? "HIGHEST" : "LIMITED";
                var taskRun = $"\"{exePath}\" --background-autoapply";
                var args = $"/Create /F /SC ONLOGON /TN \"{StartupTaskName}\" /TR \"{taskRun}\" /RL {runLevel}";
                return RunSchtasks(args, true, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static bool ConfigureGuiStartup(string exePath, StartupScopeMode mode, out string error)
        {
            error = string.Empty;
            var command = $"\"{exePath}\"";
            return mode switch
            {
                StartupScopeMode.Disabled => true,
                StartupScopeMode.CurrentUser => SetRunEntry(GuiRunValueName, command, out error),
                StartupScopeMode.AllUsers => CreateOrUpdateLogonTask(GuiStartupTaskName, command, false, false, out error),
                _ => true
            };
        }

        static bool ConfigureCoreStartup(string exePath, CoreEngineStartupMode mode, out string error)
        {
            error = string.Empty;
            var watchdogPath = ResolveWatchdogHostPath(exePath);
            var watchdogCommand = $"\"{watchdogPath}\"";
            return mode switch
            {
                CoreEngineStartupMode.Disabled => true,
                CoreEngineStartupMode.WithGui => true,
                CoreEngineStartupMode.CurrentUser => SetRunEntry(CoreRunValueName, watchdogCommand, out error),
                CoreEngineStartupMode.AllUsers => CreateOrUpdateLogonTask(CoreStartupTaskName, watchdogCommand, false, false, out error),
                CoreEngineStartupMode.ServiceLike => CreateOrUpdateBootTask(CoreStartupTaskName, watchdogCommand, out error),
                _ => true
            };
        }

        static string ResolveWatchdogHostPath(string appPath)
        {
            var appDir = Path.GetDirectoryName(appPath) ?? AppContext.BaseDirectory;
            var path = Path.Combine(appDir, "PMX.EngineWatchdog.exe");
            return File.Exists(path) ? path : appPath;
        }

        static bool SetRunEntry(string valueName, string command, out string error)
        {
            error = string.Empty;
            try
            {
                using var run = Registry.CurrentUser.CreateSubKey(CurrentUserRunPath, true);
                if (run == null)
                {
                    error = "Failed to open HKCU Run key.";
                    return false;
                }

                run.SetValue(valueName, command, RegistryValueKind.String);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static bool DeleteRunEntry(string valueName, out string error)
        {
            error = string.Empty;
            try
            {
                using var run = Registry.CurrentUser.CreateSubKey(CurrentUserRunPath, true);
                if (run == null)
                    return true;

                if (Array.Exists(run.GetValueNames(), n => string.Equals(n, valueName, StringComparison.OrdinalIgnoreCase)))
                    run.DeleteValue(valueName, false);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static bool CreateOrUpdateLogonTask(string taskName, string command, bool runAsAdministrator, bool runAsSystem, out string error)
        {
            error = string.Empty;
            try
            {
                var runLevel = runAsAdministrator ? "HIGHEST" : "LIMITED";
                var action = BuildTaskAction(command);
                var args = new StringBuilder($"/Create /F /SC ONLOGON /TN \"{taskName}\" /TR {action} /RL {runLevel}");
                if (runAsSystem)
                    args.Append(" /RU SYSTEM");

                return RunSchtasks(args.ToString(), true, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static bool CreateOrUpdateBootTask(string taskName, string command, out string error)
        {
            var action = BuildTaskAction(command);
            var args = $"/Create /F /SC ONSTART /TN \"{taskName}\" /TR {action} /RU SYSTEM /RL HIGHEST";
            return RunSchtasks(args, true, out error);
        }

        static string BuildTaskAction(string command)
        {
            var safe = (command ?? string.Empty).Trim();
            if (safe.Length == 0)
                return "\"\"";

            // schtasks /TR expects embedded quotes escaped as \" inside the action string.
            safe = safe.Replace("\"", "\\\"");
            return $"\"{safe}\"";
        }

        static bool DeleteTaskIfExists(string taskName, out string error)
        {
            var args = $"/Delete /F /TN \"{taskName}\"";
            if (RunSchtasks(args, true, out error))
                return true;

            if (IsTaskNotFoundError(error))
            {
                error = string.Empty;
                return true;
            }

            return false;
        }

        static bool DeleteStartupTask(out string error)
        {
            var args = $"/Delete /F /TN \"{StartupTaskName}\"";
            if (RunSchtasks(args, true, out error))
                return true;

            if (IsTaskNotFoundError(error))
            {
                error = string.Empty;
                return true;
            }

            return false;
        }

        static bool RunSchtasks(string args, bool elevated, out string error)
        {
            error = string.Empty;
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var shouldUseRunAs = elevated && !IsRunningAsAdministrator();
                if (shouldUseRunAs)
                {
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                }
                else
                {
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardError = true;
                    psi.RedirectStandardOutput = true;
                    var oemCodePage = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
                    psi.StandardOutputEncoding = Encoding.GetEncoding(oemCodePage);
                    psi.StandardErrorEncoding = Encoding.GetEncoding(oemCodePage);
                }

                using var process = Process.Start(psi);
                if (process == null)
                {
                    error = "Failed to start schtasks.exe.";
                    return false;
                }

                process.WaitForExit(20000);
                if (!process.HasExited)
                {
                    error = "schtasks timeout.";
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    var stdOut = string.Empty;
                    var stdErr = string.Empty;
                    if (!psi.UseShellExecute)
                    {
                        try { stdOut = process.StandardOutput.ReadToEnd(); } catch { }
                        try { stdErr = process.StandardError.ReadToEnd(); } catch { }
                    }

                    var details = (stdErr + " " + stdOut).Trim();
                    error = string.IsNullOrWhiteSpace(details)
                        ? $"schtasks exit code: {process.ExitCode}"
                        : $"schtasks exit code: {process.ExitCode}. {details}";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static bool IsTaskNotFoundError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return false;

            return error.Contains("cannot find", StringComparison.OrdinalIgnoreCase)
                || error.Contains("не удается найти", StringComparison.OrdinalIgnoreCase)
                || error.Contains("the system cannot find the file specified", StringComparison.OrdinalIgnoreCase)
                || error.Contains("указанный файл не найден", StringComparison.OrdinalIgnoreCase);
        }

        public static bool InstallExplorerContextMenu(string appPath, AppLanguage language, out string error)
        {
            error = string.Empty;
            try
            {
                CleanupMenuKeys(Registry.CurrentUser);
                CleanupMenuKeys(Registry.LocalMachine);

                var machineAppPath = ResolvePreferredMachineAppPath(appPath);
                var iconPath = BuildIconValue(machineAppPath);
                var entries = BuildMenuEntries(machineAppPath, language);
                var machineSubCommands = BuildSubCommands(entries);

                var machineShellRegistered = TryRegisterShellExtension(machineAppPath, false, out _);
                // Legacy (Win10 / \"Показать дополнительные параметры\" в Win11): чисто реестровое меню без ExplorerCommandHandler.
                // Это надёжно показывает подпункты на всех версиях Windows.
                bool machineLegacyInstalled =
                    TryInstallCommandStore(Registry.LocalMachine, entries, iconPath)
                    && TryInstallRoot(Registry.LocalMachine, ExplorerMenuPath, iconPath, machineSubCommands, language, includeExplorerCommandHandler: true)
                    && TryInstallLegacyChildren(Registry.LocalMachine, ExplorerMenuPath, entries, iconPath);
                // Современное меню Win11 требует app identity (MSIX/sparse package), которого у приложения нет.
                // Поэтому здесь сознательно ничего не регистрируем: пункт будет только в классическом меню.
                bool machineModernInstalled = true;

                if (machineLegacyInstalled && machineModernInstalled)
                {
                    RefreshExplorerShell();
                    return true;
                }

                var userIconPath = BuildIconValue(appPath);
                var userEntries = BuildMenuEntries(appPath, language);
                var userSubCommands = BuildSubCommands(userEntries);

                var userShellRegistered = TryRegisterShellExtension(appPath, false, out _);
                bool userLegacyInstalled =
                    TryInstallCommandStore(Registry.CurrentUser, userEntries, userIconPath)
                    && TryInstallRoot(Registry.CurrentUser, ExplorerMenuPath, userIconPath, userSubCommands, language, includeExplorerCommandHandler: true)
                    && TryInstallLegacyChildren(Registry.CurrentUser, ExplorerMenuPath, userEntries, userIconPath);
                // Аналогично для пользовательской установки: только классическое меню.
                bool userModernInstalled = true;

                if (!userLegacyInstalled || !userModernInstalled)
                {
                    error = WithLanguage(language, () => L10n.ErrCreateMenuKey);
                    return false;
                }

                RefreshExplorerShell();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool InstallExplorerContextMenuMachine(string appPath, AppLanguage language, out string error)
        {
            error = string.Empty;
            try
            {
                CleanupMenuKeys(Registry.CurrentUser);
                CleanupMenuKeys(Registry.LocalMachine);

                var machineAppPath = ResolvePreferredMachineAppPath(appPath);
                var iconPath = BuildIconValue(machineAppPath);
                var entries = BuildMenuEntries(machineAppPath, language);
                var subCommands = BuildSubCommands(entries);

                var shellRegistered = TryRegisterShellExtension(machineAppPath, false, out var registerError);
                bool legacyInstalled =
                    TryInstallCommandStore(Registry.LocalMachine, entries, iconPath)
                    && TryInstallRoot(Registry.LocalMachine, ExplorerMenuPath, iconPath, subCommands, language, includeExplorerCommandHandler: true)
                    && TryInstallLegacyChildren(Registry.LocalMachine, ExplorerMenuPath, entries, iconPath);
                bool modernInstalled = true;

                if (!legacyInstalled || !modernInstalled)
                {
                    error = string.IsNullOrWhiteSpace(registerError)
                        ? WithLanguage(language, () => L10n.ErrCreateMenuKey)
                        : registerError;
                    return false;
                }

                RefreshExplorerShell();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool UninstallExplorerContextMenu(out string error)
        {
            error = string.Empty;
            try
            {
                var appPath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(appPath))
                    TryRegisterShellExtension(appPath, true, out _);

                CleanupMenuKeys(Registry.CurrentUser);
                CleanupMenuKeys(Registry.LocalMachine);

                RefreshExplorerShell();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static string BuildSetCommand(string appPath, string priority)
            => $"\"{appPath}\" --set-default \"%1\" {priority}";

        static string BuildRemoveCommand(string appPath)
            => $"\"{appPath}\" --remove-default \"%1\"";

        static string BuildIconValue(string appPath)
            => $"{appPath},0";

        static string BuildSubCommands(List<MenuEntry> entries)
            => string.Join(";", entries.Select(e => e.CommandId));

        static string ResolvePreferredMachineAppPath(string appPath)
        {
            if (string.IsNullOrWhiteSpace(appPath))
                return appPath;

            var normalized = Path.GetFullPath(appPath);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles)
                && normalized.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
                return normalized;

            var defaultInstalled = Path.Combine(programFiles, "Priority Manager X", "PriorityManagerX.exe");
            if (File.Exists(defaultInstalled))
                return defaultInstalled;

            return normalized;
        }

        static void CleanupMenuKeys(RegistryKey hive)
        {
            TryDeleteSubKeyTree(hive, ExplorerMenuPath);
            TryDeleteSubKeyTree(hive, LegacyExplorerMenuPath);
            TryDeleteSubKeyTree(hive, LegacyFileMenuPath);
            TryDeleteSubKeyTree(hive, SystemFileAssocMenuPath);
            TryDeleteSubKeyTree(hive, ModernMenuPath);
            TryDeleteSubKeyTree(hive, @"Software\Classes\exefile\shell\PriorityManagerXQuick");
            TryDeleteSubKeyTree(hive, @"Software\Classes\exefile\shell\PMXSimple");
            TryDeleteSubKeyTree(hive, @"Software\Classes\exefile\shell\PMXTest");
            TryDeleteSubKeyTree(hive, @"Software\Classes\*\shell\PMXSimple");
            TryDeleteSubKeyTree(hive, @"Software\Classes\*\shell\PMXTest");
            TryDeleteSubKeyTree(hive, $"{CommandStorePath}\\PMX.SaveIdle");
            TryDeleteSubKeyTree(hive, $"{CommandStorePath}\\PMX.SaveBelowNormal");
            TryDeleteSubKeyTree(hive, $"{CommandStorePath}\\PMX.SaveNormal");
            TryDeleteSubKeyTree(hive, $"{CommandStorePath}\\PMX.SaveAboveNormal");
            TryDeleteSubKeyTree(hive, $"{CommandStorePath}\\PMX.SaveHigh");
            TryDeleteSubKeyTree(hive, $"{CommandStorePath}\\PMX.SaveRealTime");
            TryDeleteSubKeyTree(hive, $"{CommandStorePath}\\PMX.RemoveSaved");
            TryDeleteSubKeyTree(hive, $@"Software\Classes\CLSID\{ExplorerCommandHandlerClsid}");
            TryDeleteSubKeyTree(hive, $@"Software\Classes\CLSID\{PriorityManagerPrimaryClsid}");
        }

        static bool TryInstallCommandStore(RegistryKey hive, List<MenuEntry> entries, string iconPath)
        {
            try
            {
                foreach (var entry in entries)
                {
                    using var command = hive.CreateSubKey($"{CommandStorePath}\\{entry.CommandId}", true);
                    if (command == null)
                        return false;

                    command.SetValue("MUIVerb", entry.MenuText, RegistryValueKind.String);
                    command.SetValue("Icon", iconPath, RegistryValueKind.String);
                    using var cmd = command.CreateSubKey("command", true);
                    cmd?.SetValue(string.Empty, entry.Command, RegistryValueKind.String);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool TryInstallRoot(
            RegistryKey hive,
            string rootPath,
            string iconPath,
            string subCommands,
            AppLanguage language,
            bool includeExplorerCommandHandler,
            bool appliesToExe = false,
            string? customTitle = null,
            bool legacyDisable = false)
        {
            try
            {
                using var root = hive.CreateSubKey(rootPath, true);
                if (root == null)
                    return false;

                root.SetValue("MUIVerb", customTitle ?? WithLanguage(language, () => L10n.MenuRoot), RegistryValueKind.String);
                root.SetValue("Icon", iconPath, RegistryValueKind.String);
                if (!string.IsNullOrWhiteSpace(subCommands))
                    root.SetValue("SubCommands", subCommands, RegistryValueKind.String);
                else if (Array.Exists(root.GetValueNames(), n => string.Equals(n, "SubCommands", StringComparison.OrdinalIgnoreCase)))
                    root.DeleteValue("SubCommands", false);

                if (appliesToExe)
                    root.SetValue("AppliesTo", "System.FileExtension:=\".exe\"", RegistryValueKind.String);
                else if (Array.Exists(root.GetValueNames(), n => string.Equals(n, "AppliesTo", StringComparison.OrdinalIgnoreCase)))
                    root.DeleteValue("AppliesTo", false);

                if (includeExplorerCommandHandler)
                    root.SetValue("ExplorerCommandHandler", ExplorerCommandHandlerClsid, RegistryValueKind.String);
                else if (Array.Exists(root.GetValueNames(), n => string.Equals(n, "ExplorerCommandHandler", StringComparison.OrdinalIgnoreCase)))
                    root.DeleteValue("ExplorerCommandHandler", false);

                if (legacyDisable)
                    root.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                else if (Array.Exists(root.GetValueNames(), n => string.Equals(n, "LegacyDisable", StringComparison.OrdinalIgnoreCase)))
                    root.DeleteValue("LegacyDisable", false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool TryInstallLegacyChildren(RegistryKey hive, string rootPath, List<MenuEntry> entries, string iconPath)
        {
            try
            {
                using var root = hive.CreateSubKey(rootPath, true);
                if (root == null)
                    return false;

                foreach (var entry in entries)
                {
                    using var item = root.CreateSubKey($@"shell\{entry.LegacyId}", true);
                    item?.SetValue("MUIVerb", entry.MenuText, RegistryValueKind.String);
                    item?.SetValue("Icon", iconPath, RegistryValueKind.String);

                    using var command = item?.CreateSubKey("command", true);
                    command?.SetValue(string.Empty, entry.Command, RegistryValueKind.String);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool TryRegisterShellExtension(string appPath, bool unregister, out string error)
        {
            error = string.Empty;
            try
            {
                var appDir = Path.GetDirectoryName(appPath) ?? string.Empty;
                var comHostPath = Path.Combine(appDir, "PriorityManagerX.ShellExtension.comhost.dll");
                if (!File.Exists(comHostPath))
                {
                    error = $"COM host not found: {comHostPath}";
                    WriteInstallLog($"TryRegisterShellExtension: COM host missing, unregister={unregister}, path='{comHostPath}'");
                    return false;
                }

                var args = unregister
                    ? $"/u /s \"{comHostPath}\""
                    : $"/s \"{comHostPath}\"";

                var elevated = IsRunningAsAdministrator();
                var psi = new ProcessStartInfo("regsvr32.exe", args)
                {
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                if (elevated)
                {
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                }
                else
                {
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                }

                WriteInstallLog($"TryRegisterShellExtension: start unregister={unregister}, elevated={elevated}, args='{args}'");

                using var process = Process.Start(psi);
                if (process == null)
                {
                    error = "Failed to start regsvr32.";
                    WriteInstallLog("TryRegisterShellExtension: failed to start regsvr32");
                    return false;
                }

                process.WaitForExit(20000);
                if (!process.HasExited)
                {
                    error = "regsvr32 timeout.";
                    WriteInstallLog("TryRegisterShellExtension: timeout waiting regsvr32");
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    error = $"regsvr32 exit code: {process.ExitCode}";
                    WriteInstallLog($"TryRegisterShellExtension: regsvr32 exit code {process.ExitCode}");
                    return false;
                }

                WriteInstallLog("TryRegisterShellExtension: success");

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                WriteInstallLog($"TryRegisterShellExtension: exception {ex.Message}");
                return false;
            }
        }

        static void WriteInstallLog(string message)
        {
            try
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(baseDir))
                    return;

                var dir = Path.Combine(baseDir, "PriorityManagerX");
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "menu-install.log");
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(logPath, line, Encoding.UTF8);
            }
            catch
            {
            }
        }

        static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        static void TryDeleteSubKeyTree(RegistryKey hive, string subKey)
        {
            try
            {
                hive.DeleteSubKeyTree(subKey, false);
            }
            catch
            {
            }
        }

        static void RefreshExplorerShell()
        {
            try
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
            }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        static List<MenuEntry> BuildMenuEntries(string appPath, AppLanguage language)
            => new()
            {
                new MenuEntry("pmx_idle", "PMX.SaveIdle", WithLanguage(language, () => L10n.MenuSaveIdle), BuildSetCommand(appPath, "Idle")),
                new MenuEntry("pmx_below", "PMX.SaveBelowNormal", WithLanguage(language, () => L10n.MenuSaveBelowNormal), BuildSetCommand(appPath, "BelowNormal")),
                new MenuEntry("pmx_normal", "PMX.SaveNormal", WithLanguage(language, () => L10n.MenuSaveNormal), BuildSetCommand(appPath, "Normal")),
                new MenuEntry("pmx_above", "PMX.SaveAboveNormal", WithLanguage(language, () => L10n.MenuSaveAboveNormal), BuildSetCommand(appPath, "AboveNormal")),
                new MenuEntry("pmx_high", "PMX.SaveHigh", WithLanguage(language, () => L10n.MenuSaveHigh), BuildSetCommand(appPath, "High")),
                new MenuEntry("pmx_realtime", "PMX.SaveRealTime", WithLanguage(language, () => L10n.MenuSaveRealTime), BuildSetCommand(appPath, "RealTime")),
                new MenuEntry("pmx_remove", "PMX.RemoveSaved", WithLanguage(language, () => L10n.MenuRemoveSaved), BuildRemoveCommand(appPath))
            };

        static bool TryWriteCpuPriority(RegistryKey hive, string exeName, int cpuPriorityClass, out string error)
            => TryWriteCpuPriority(hive, exeName, cpuPriorityClass, out error, AppLanguage.Russian);

        static bool TryWriteCpuPriority(RegistryKey hive, string exeName, int cpuPriorityClass, out string error, AppLanguage language)
        {
            error = string.Empty;
            try
            {
                using var perf = hive.CreateSubKey($"{IfeoPath}\\{exeName}\\PerfOptions", true);
                if (perf == null)
                {
                    error = WithLanguage(language, () => L10n.ErrOpenPerfOptions);
                    return false;
                }

                perf.SetValue("CpuPriorityClass", cpuPriorityClass, RegistryValueKind.DWord);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static bool TryDeleteCpuPriority(RegistryKey hive, string exeName, out string error)
        {
            error = string.Empty;
            try
            {
                using var perf = hive.OpenSubKey($"{IfeoPath}\\{exeName}\\PerfOptions", true);
                if (perf == null)
                    return false;

                if (Array.Exists(perf.GetValueNames(), n => n == "CpuPriorityClass"))
                    perf.DeleteValue("CpuPriorityClass", false);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static bool TryReadCpuPriority(RegistryKey hive, string exeName, out ProcessPriorityClass priority)
        {
            priority = ProcessPriorityClass.Normal;
            try
            {
                using var perf = hive.OpenSubKey($"{IfeoPath}\\{exeName}\\PerfOptions", false);
                if (perf == null)
                    return false;

                var raw = perf.GetValue("CpuPriorityClass");
                if (raw == null)
                    return false;

                var value = raw switch
                {
                    int intValue => intValue,
                    long longValue => unchecked((int)longValue),
                    _ => Convert.ToInt32(raw)
                };

                if (!TryParseCpuPriorityClass(value, out priority))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        static string NormalizeExeName(string processOrPath)
        {
            if (string.IsNullOrWhiteSpace(processOrPath))
                return string.Empty;

            var trimmed = processOrPath.Trim().Trim('"');
            var name = Path.GetFileName(trimmed);
            if (string.IsNullOrWhiteSpace(name))
                name = trimmed;

            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name += ".exe";

            return name;
        }

        static int ToCpuPriorityClass(ProcessPriorityClass priority) => priority switch
        {
            ProcessPriorityClass.Idle => 1,
            ProcessPriorityClass.Normal => 2,
            ProcessPriorityClass.High => 3,
            ProcessPriorityClass.RealTime => 4,
            ProcessPriorityClass.BelowNormal => 5,
            ProcessPriorityClass.AboveNormal => 6,
            _ => 2
        };

        static bool TryParseCpuPriorityClass(int value, out ProcessPriorityClass priority)
        {
            priority = value switch
            {
                1 => ProcessPriorityClass.Idle,
                2 => ProcessPriorityClass.Normal,
                3 => ProcessPriorityClass.High,
                4 => ProcessPriorityClass.RealTime,
                5 => ProcessPriorityClass.BelowNormal,
                6 => ProcessPriorityClass.AboveNormal,
                _ => ProcessPriorityClass.Normal
            };

            return value is >= 1 and <= 6;
        }

        static string WithLanguage(AppLanguage language, Func<string> valueFactory)
        {
            var previous = L10n.CurrentLanguage;
            L10n.CurrentLanguage = language;
            var result = valueFactory();
            L10n.CurrentLanguage = previous;
            return result;
        }

        sealed record MenuEntry(string LegacyId, string CommandId, string MenuText, string Command);
    }
}
