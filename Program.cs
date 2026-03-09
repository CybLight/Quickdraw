
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;

namespace PriorityManagerX
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var settings = AppSettingsStore.Load();
            L10n.CurrentLanguage = L10n.ParseLanguage(settings.Language);

            if (HandleArgs(args))
                return;

            CoreEngineHost.EnsureBackgroundComponents(settings);
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(settings));
        }

        static bool HandleArgs(string[] args)
        {
            if (args.Length == 0)
                return false;

            try
            {
                if (args.Length >= 3 && args[0].Equals("--set-default", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsRunningAsAdministrator())
                    {
                        if (TryStartElevatedWithArgs(args))
                            return true;

                        MessageBox.Show(L10n.MsgSaveFailed(L10n.ErrAdminRequiredForMainMenu));
                        return true;
                    }

                    var exePath = args[1];
                    var priorityText = args[2];
                    if (!Enum.TryParse<ProcessPriorityClass>(priorityText, true, out var priority))
                    {
                        MessageBox.Show(L10n.MsgUnknownPriority(priorityText));
                        return true;
                    }

                    if (!WindowsIntegration.SetDefaultPriority(exePath, priority, out var scope, out var error, L10n.CurrentLanguage))
                    {
                        MessageBox.Show(L10n.MsgSaveFailed(error));
                        return true;
                    }

                    _ = RuleStore.TryAddOrUpdateRule(exePath, priority.ToString(), out _);
                    _ = PriorityEngine.ApplyWithResult(exePath, priority.ToString());
                    MessageBox.Show(L10n.MsgSavedCli(Path.GetFileName(exePath), priority.ToString(), scope));

                    return true;
                }

                if (args.Length >= 2 && args[0].Equals("--remove-default", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsRunningAsAdministrator())
                    {
                        if (TryStartElevatedWithArgs(args))
                            return true;

                        MessageBox.Show(L10n.MsgRemoveFailedCli(L10n.ErrAdminRequiredForMainMenu));
                        return true;
                    }

                    var exePath = args[1];
                    if (!WindowsIntegration.RemoveDefaultPriority(exePath, out var error, L10n.CurrentLanguage))
                    {
                        MessageBox.Show(L10n.MsgRemoveFailedCli(error));
                        return true;
                    }

                    _ = RuleStore.TryRemoveRule(exePath, out _);
                    MessageBox.Show(L10n.MsgRemovedCli(Path.GetFileName(exePath)));

                    return true;
                }

                if (args.Length >= 3 && args[0].Equals("--apply-now", StringComparison.OrdinalIgnoreCase))
                {
                    PriorityEngine.Apply(args[1], args[2]);
                    MessageBox.Show(L10n.MsgApplyDone);
                    return true;
                }

                if (args[0].Equals("--background-autoapply", StringComparison.OrdinalIgnoreCase))
                {
                    ApplicationConfiguration.Initialize();
                    Application.Run(new BackgroundAutoApplyContext());
                    return true;
                }

                if (args[0].Equals("--core-engine", StringComparison.OrdinalIgnoreCase))
                {
                    CoreEngineHost.RunCoreEngine();
                    return true;
                }

                if (args[0].Equals("--engine-watchdog", StringComparison.OrdinalIgnoreCase))
                {
                    CoreEngineHost.RunWatchdog();
                    return true;
                }

                if (args[0].Equals("--install-menu", StringComparison.OrdinalIgnoreCase))
                {
                    var systemLanguage = L10n.ParseLanguage(L10n.DetectSystemLanguageCode());
                    if (TryStartElevatedMenuInstall())
                        return true;

                    if (WindowsIntegration.InstallExplorerContextMenu(Application.ExecutablePath, systemLanguage, out var error))
                        MessageBox.Show(L10n.MsgMenuInstalledCli);
                    else
                        MessageBox.Show(L10n.MsgMenuInstallFailed(error));

                    return true;
                }

                if (args[0].Equals("--install-menu-machine", StringComparison.OrdinalIgnoreCase))
                {
                    var systemLanguage = L10n.ParseLanguage(L10n.DetectSystemLanguageCode());
                    if (WindowsIntegration.InstallExplorerContextMenuMachine(Application.ExecutablePath, systemLanguage, out var error))
                        MessageBox.Show(L10n.MsgMenuInstalledCli);
                    else
                        MessageBox.Show(L10n.MsgMenuInstallFailed(error));

                    return true;
                }

                if (args[0].Equals("--uninstall-menu", StringComparison.OrdinalIgnoreCase))
                {
                    if (WindowsIntegration.UninstallExplorerContextMenu(out var error))
                        MessageBox.Show(L10n.MsgMenuRemovedCli);
                    else
                        MessageBox.Show(L10n.MsgMenuRemoveFailed(error));

                    return true;
                }

                if (args[0].Equals("--repair-menu", StringComparison.OrdinalIgnoreCase))
                {
                    var systemLanguage = L10n.ParseLanguage(L10n.DetectSystemLanguageCode());
                    if (!IsRunningAsAdministrator() && TryStartElevatedMenuRepair())
                        return true;

                    _ = WindowsIntegration.UninstallExplorerContextMenu(out _);

                    if (WindowsIntegration.InstallExplorerContextMenuMachine(Application.ExecutablePath, systemLanguage, out var machineError))
                    {
                        MessageBox.Show(L10n.MsgMenuInstalledCli);
                        return true;
                    }

                    if (WindowsIntegration.InstallExplorerContextMenu(Application.ExecutablePath, systemLanguage, out var userError))
                    {
                        MessageBox.Show(L10n.MsgMenuInstalledCli);
                        return true;
                    }

                    var error = string.IsNullOrWhiteSpace(machineError)
                        ? userError
                        : machineError;
                    MessageBox.Show(L10n.MsgMenuInstallFailed(error));
                    return true;
                }

                if (args[0].Equals("--repair-menu-machine", StringComparison.OrdinalIgnoreCase))
                {
                    var systemLanguage = L10n.ParseLanguage(L10n.DetectSystemLanguageCode());
                    _ = WindowsIntegration.UninstallExplorerContextMenu(out _);

                    if (WindowsIntegration.InstallExplorerContextMenuMachine(Application.ExecutablePath, systemLanguage, out var error))
                        MessageBox.Show(L10n.MsgMenuInstalledCli);
                    else
                        MessageBox.Show(L10n.MsgMenuInstallFailed(error));

                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, L10n.AppTitle);
                return true;
            }

            return false;
        }

        static bool TryStartElevatedMenuInstall()
        {
            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath, "--install-menu-machine")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(psi);
                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        static bool TryStartElevatedMenuRepair()
        {
            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath, "--repair-menu-machine")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(psi);
                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        static bool TryStartElevatedWithArgs(string[] args)
        {
            try
            {
                var encodedArgs = string.Join(" ", args.Select(QuoteArg));
                var psi = new ProcessStartInfo(Application.ExecutablePath, encodedArgs)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(psi);
                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        static string QuoteArg(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";

            if (!arg.Any(char.IsWhiteSpace) && !arg.Contains('"'))
                return arg;

            return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
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
    }

    sealed class BackgroundAutoApplyContext : ApplicationContext
    {
        readonly Timer timer = new();

        public BackgroundAutoApplyContext()
        {
            var settings = AppSettingsStore.Load();
            var seconds = Math.Clamp(settings.ProcessRefreshSeconds, 2, 60);
            timer.Interval = seconds * 1000;
            timer.Tick += (_, _) => ApplyRulesSilently();
            ApplyRulesSilently();
            timer.Start();
        }

        void ApplyRulesSilently()
        {
            try
            {
                foreach (var rule in RuleStore.LoadAllRules())
                    _ = PriorityEngine.ApplyWithResult(rule.Process, rule.Priority);
            }
            catch
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop();
                timer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
