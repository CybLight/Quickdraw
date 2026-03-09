using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PriorityManagerX
{
    public sealed class SettingsForm : Form
    {
        readonly ComboBox language = new();
        readonly CheckBox autoApply = new();
        readonly CheckBox confirmApply = new();
        readonly CheckBox autoRefresh = new();
        readonly NumericUpDown refreshSeconds = new();
        readonly CheckBox runAsAdmin = new();
        readonly Button installMenu = new();
        readonly Button removeMenu = new();

        readonly RadioButton guiStartupAllUsers = new();
        readonly RadioButton guiStartupCurrentUser = new();
        readonly RadioButton guiStartupDisabled = new();

        readonly RadioButton engineStartupAllUsers = new();
        readonly RadioButton engineStartupCurrentUser = new();
        readonly RadioButton engineStartupServiceLike = new();
        readonly RadioButton engineStartupWithGui = new();
        readonly RadioButton engineStartupDisabled = new();
        readonly CheckBox engineAutoRecover = new();

        readonly RadioButton processScopeCurrentSession = new();
        readonly RadioButton processScopeAllAccessible = new();
        readonly CheckBox useSharedLocation = new();
        readonly TextBox configPath = new();
        readonly TextBox logsPath = new();
        readonly Button browseConfig = new();
        readonly Button browseLogs = new();
        readonly RadioButton dateEurope = new();
        readonly RadioButton dateUsa = new();
        readonly RadioButton dateAsia = new();
        readonly CheckBox checkUpdatesOnStartup = new();
        readonly CheckBox includePrereleaseUpdates = new();

        readonly Button ok = new();
        readonly Button cancel = new();
        readonly ToolTip startupTooltips = new();

        public AppSettings Settings { get; }

        public SettingsForm(AppSettings settings)
        {
            Settings = new AppSettings
            {
                Language = settings.Language,
                AutoApplyOnStartup = settings.AutoApplyOnStartup,
                AutoRefreshProcesses = settings.AutoRefreshProcesses,
                ProcessRefreshSeconds = settings.ProcessRefreshSeconds,
                ConfirmBeforeApplyRules = settings.ConfirmBeforeApplyRules,
                AutoStartWithWindows = settings.AutoStartWithWindows,
                RunAsAdministrator = settings.RunAsAdministrator,
                GuiStartupMode = settings.GuiStartupMode,
                CoreEngineStartupMode = settings.CoreEngineStartupMode,
                RestartCoreEngineIfStopped = settings.RestartCoreEngineIfStopped,
                ProcessScope = settings.ProcessScope,
                UseSharedDataLocation = settings.UseSharedDataLocation,
                SharedConfigPath = settings.SharedConfigPath,
                SharedLogsPath = settings.SharedLogsPath,
                DateFormat = settings.DateFormat,
                CheckUpdatesOnStartup = settings.CheckUpdatesOnStartup,
                IncludePrereleaseUpdates = settings.IncludePrereleaseUpdates,
                UpdatePeriod = settings.UpdatePeriod,
                LastUpdateCheckUtc = settings.LastUpdateCheckUtc
            };

            AutoScaleMode = AutoScaleMode.Dpi;
            Text = L10n.SettingsTitle;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(760, 620);
            ClientSize = new Size(900, 760);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill
            };

            tabs.TabPages.Add(NewTab(L10n.SettingsInterface, BuildInterfacePage(settings)));
            tabs.TabPages.Add(NewTab(L10n.SettingsBehavior, BuildBehaviorPage(settings)));
            tabs.TabPages.Add(NewTab(L10n.SettingsIntegration, BuildStartupPage(settings)));
            tabs.TabPages.Add(NewTab(L10n.SettingsMultiUserGroup, BuildMultiUserPage(settings)));
            tabs.TabPages.Add(NewTab(L10n.SettingsUpdates, BuildUpdatesPage(settings)));
            tabs.TabPages.Add(NewTab(L10n.SettingsInstallMenuHint, BuildIntegrationPage()));

            var buttonsPanel = BuildBottomButtons();
            buttonsPanel.Dock = DockStyle.Bottom;

            Controls.Add(tabs);
            Controls.Add(buttonsPanel);

            AcceptButton = ok;
            CancelButton = cancel;

            startupTooltips.AutoPopDelay = 12000;
            startupTooltips.InitialDelay = 350;
            startupTooltips.ReshowDelay = 120;
            startupTooltips.ShowAlways = true;
        }

        static TabPage NewTab(string title, Control body)
        {
            var tab = new TabPage(title) { AutoScroll = true };
            tab.Controls.Add(body);
            return tab;
        }

        Control BuildInterfacePage(AppSettings settings)
        {
            var group = NewGroup(L10n.SettingsInterface);
            group.Dock = DockStyle.Top;

            var grid = NewGrid(2);
            grid.Dock = DockStyle.Top;

            var languageLabel = new Label
            {
                Text = L10n.LanguageLabel,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 8, 8)
            };

            language.DropDownStyle = ComboBoxStyle.DropDownList;
            language.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            language.Width = 280;
            language.Items.AddRange(new object[]
            {
                new LanguageItem("ru", L10n.LanguageRu),
                new LanguageItem("uk", L10n.LanguageUk),
                new LanguageItem("en", L10n.LanguageEn)
            });
            language.SelectedIndex = settings.Language switch
            {
                "uk" => 1,
                "en" => 2,
                _ => 0
            };

            grid.Controls.Add(languageLabel, 0, 0);
            grid.Controls.Add(language, 1, 0);
            group.Controls.Add(grid);

            return group;
        }

        Control BuildBehaviorPage(AppSettings settings)
        {
            var group = NewGroup(L10n.SettingsBehavior);
            group.Dock = DockStyle.Top;

            var panel = NewGrid(2);
            panel.Dock = DockStyle.Top;

            autoApply.Text = L10n.SettingsAutoApply;
            autoApply.AutoSize = true;
            autoApply.Checked = settings.AutoApplyOnStartup;

            confirmApply.Text = L10n.SettingsConfirmApply;
            confirmApply.AutoSize = true;
            confirmApply.Checked = settings.ConfirmBeforeApplyRules;

            autoRefresh.Text = L10n.SettingsAutoRefresh;
            autoRefresh.AutoSize = true;
            autoRefresh.Checked = settings.AutoRefreshProcesses;

            var refreshLabel = new Label
            {
                Text = L10n.SettingsRefreshSeconds,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };

            refreshSeconds.Minimum = 2;
            refreshSeconds.Maximum = 60;
            refreshSeconds.Value = Math.Clamp(settings.ProcessRefreshSeconds, 2, 60);
            refreshSeconds.Anchor = AnchorStyles.Left;

            panel.Controls.Add(autoApply, 0, 0);
            panel.SetColumnSpan(autoApply, 2);
            panel.Controls.Add(confirmApply, 0, 1);
            panel.SetColumnSpan(confirmApply, 2);
            panel.Controls.Add(autoRefresh, 0, 2);
            panel.SetColumnSpan(autoRefresh, 2);
            panel.Controls.Add(refreshLabel, 0, 3);
            panel.Controls.Add(refreshSeconds, 1, 3);

            group.Controls.Add(panel);
            return group;
        }

        Control BuildStartupPage(AppSettings settings)
        {
            var host = new Panel { Dock = DockStyle.Top, Height = 520 };

            var startupGuiGroup = NewGroup(L10n.SettingsStartupGuiGroup);
            startupGuiGroup.Dock = DockStyle.Top;

            var guiPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown
            };

            guiStartupAllUsers.Text = L10n.SettingsStartupAllUsers;
            guiStartupCurrentUser.Text = L10n.SettingsStartupCurrentUser;
            guiStartupDisabled.Text = L10n.SettingsStartupDisabled;
            guiStartupAllUsers.AutoSize = true;
            guiStartupCurrentUser.AutoSize = true;
            guiStartupDisabled.AutoSize = true;
            guiPanel.Controls.Add(guiStartupAllUsers);
            guiPanel.Controls.Add(guiStartupCurrentUser);
            guiPanel.Controls.Add(guiStartupDisabled);
            startupTooltips.SetToolTip(guiStartupAllUsers, L10n.SettingsTipGuiStartupAllUsers);
            startupTooltips.SetToolTip(guiStartupCurrentUser, L10n.SettingsTipGuiStartupCurrentUser);
            startupTooltips.SetToolTip(guiStartupDisabled, L10n.SettingsTipGuiStartupDisabled);
            startupGuiGroup.Controls.Add(guiPanel);

            switch (settings.GuiStartupMode)
            {
                case StartupScopeMode.AllUsers: guiStartupAllUsers.Checked = true; break;
                case StartupScopeMode.CurrentUser: guiStartupCurrentUser.Checked = true; break;
                default: guiStartupDisabled.Checked = true; break;
            }

            var startupEngineGroup = NewGroup(L10n.SettingsStartupEngineGroup);
            startupEngineGroup.Dock = DockStyle.Top;

            var enginePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown
            };

            engineStartupAllUsers.Text = L10n.SettingsStartupAllUsers;
            engineStartupCurrentUser.Text = L10n.SettingsStartupCurrentUser;
            engineStartupServiceLike.Text = L10n.SettingsStartupEngineServiceLike;
            engineStartupWithGui.Text = L10n.SettingsStartupEngineWithGui;
            engineStartupDisabled.Text = L10n.SettingsStartupDisabled;
            engineAutoRecover.Text = L10n.SettingsEngineAutoRecover;
            engineStartupAllUsers.AutoSize = true;
            engineStartupCurrentUser.AutoSize = true;
            engineStartupServiceLike.AutoSize = true;
            engineStartupWithGui.AutoSize = true;
            engineStartupDisabled.AutoSize = true;
            engineAutoRecover.AutoSize = true;

            enginePanel.Controls.Add(engineStartupAllUsers);
            enginePanel.Controls.Add(engineStartupCurrentUser);
            enginePanel.Controls.Add(engineStartupServiceLike);
            enginePanel.Controls.Add(engineStartupWithGui);
            enginePanel.Controls.Add(engineStartupDisabled);
            enginePanel.Controls.Add(engineAutoRecover);
            startupTooltips.SetToolTip(engineStartupAllUsers, L10n.SettingsTipEngineStartupAllUsers);
            startupTooltips.SetToolTip(engineStartupCurrentUser, L10n.SettingsTipEngineStartupCurrentUser);
            startupTooltips.SetToolTip(engineStartupServiceLike, L10n.SettingsTipEngineStartupServiceLike);
            startupTooltips.SetToolTip(engineStartupWithGui, L10n.SettingsTipEngineStartupWithGui);
            startupTooltips.SetToolTip(engineStartupDisabled, L10n.SettingsTipEngineStartupDisabled);
            startupTooltips.SetToolTip(engineAutoRecover, L10n.SettingsTipEngineAutoRecover);
            startupEngineGroup.Controls.Add(enginePanel);

            switch (settings.CoreEngineStartupMode)
            {
                case CoreEngineStartupMode.AllUsers: engineStartupAllUsers.Checked = true; break;
                case CoreEngineStartupMode.CurrentUser: engineStartupCurrentUser.Checked = true; break;
                case CoreEngineStartupMode.ServiceLike: engineStartupServiceLike.Checked = true; break;
                case CoreEngineStartupMode.WithGui: engineStartupWithGui.Checked = true; break;
                default: engineStartupDisabled.Checked = true; break;
            }
            engineAutoRecover.Checked = settings.RestartCoreEngineIfStopped;

            runAsAdmin.Text = L10n.SettingsRunAsAdmin;
            runAsAdmin.AutoSize = true;
            runAsAdmin.Dock = DockStyle.Top;
            runAsAdmin.Checked = settings.RunAsAdministrator;
            startupTooltips.SetToolTip(runAsAdmin, L10n.SettingsTipRunAsAdmin);

            host.Controls.Add(runAsAdmin);
            host.Controls.Add(startupEngineGroup);
            host.Controls.Add(startupGuiGroup);
            return host;
        }

        Control BuildMultiUserPage(AppSettings settings)
        {
            var group = NewGroup(L10n.SettingsMultiUserGroup);
            group.Dock = DockStyle.Top;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            processScopeCurrentSession.Text = L10n.SettingsProcessScopeCurrent;
            processScopeAllAccessible.Text = L10n.SettingsProcessScopeAll;
            processScopeCurrentSession.AutoSize = true;
            processScopeAllAccessible.AutoSize = true;
            if (settings.ProcessScope == MultiUserProcessScope.CurrentSessionOnly)
                processScopeCurrentSession.Checked = true;
            else
                processScopeAllAccessible.Checked = true;

            useSharedLocation.Text = L10n.SettingsUseSharedLocation;
            useSharedLocation.AutoSize = true;
            useSharedLocation.Checked = settings.UseSharedDataLocation;
            useSharedLocation.CheckedChanged += (_, _) => UpdateSharedLocationEnabledState();

            configPath.Text = string.IsNullOrWhiteSpace(settings.SharedConfigPath)
                ? AppSettingsStore.ResolveConfigDirectory(settings)
                : settings.SharedConfigPath;
            logsPath.Text = string.IsNullOrWhiteSpace(settings.SharedLogsPath)
                ? AppSettingsStore.ResolveLogDirectory(settings)
                : settings.SharedLogsPath;
            configPath.Width = 580;
            logsPath.Width = 580;

            browseConfig.Text = "...";
            browseLogs.Text = "...";
            browseConfig.Click += (_, _) => BrowseFolder(configPath);
            browseLogs.Click += (_, _) => BrowseFolder(logsPath);

            dateEurope.Text = L10n.SettingsDateEurope;
            dateUsa.Text = L10n.SettingsDateUsa;
            dateAsia.Text = L10n.SettingsDateAsia;
            dateEurope.AutoSize = true;
            dateUsa.AutoSize = true;
            dateAsia.AutoSize = true;
            switch (settings.DateFormat)
            {
                case DateDisplayFormat.Usa: dateUsa.Checked = true; break;
                case DateDisplayFormat.Asia: dateAsia.Checked = true; break;
                default: dateEurope.Checked = true; break;
            }

            var row = 0;
            root.Controls.Add(processScopeCurrentSession, 0, row++);
            root.SetColumnSpan(processScopeCurrentSession, 3);
            root.Controls.Add(processScopeAllAccessible, 0, row++);
            root.SetColumnSpan(processScopeAllAccessible, 3);
            root.Controls.Add(useSharedLocation, 0, row++);
            root.SetColumnSpan(useSharedLocation, 3);

            root.Controls.Add(new Label { Text = L10n.SettingsConfigPath, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            root.Controls.Add(configPath, 1, row);
            root.Controls.Add(browseConfig, 2, row++);
            root.Controls.Add(new Label { Text = L10n.SettingsLogsPath, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            root.Controls.Add(logsPath, 1, row);
            root.Controls.Add(browseLogs, 2, row++);

            root.Controls.Add(new Label { Text = L10n.SettingsDateFormat, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row++);
            root.Controls.Add(dateEurope, 0, row++);
            root.SetColumnSpan(dateEurope, 3);
            root.Controls.Add(dateUsa, 0, row++);
            root.SetColumnSpan(dateUsa, 3);
            root.Controls.Add(dateAsia, 0, row++);
            root.SetColumnSpan(dateAsia, 3);

            group.Controls.Add(root);
            UpdateSharedLocationEnabledState();
            return group;
        }

        Control BuildIntegrationPage()
        {
            var group = NewGroup(L10n.SettingsInstallMenuHint);
            group.Dock = DockStyle.Top;

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            installMenu.Text = L10n.InstallMenu;
            installMenu.AutoSize = true;
            installMenu.Click += InstallMenu_Click;

            removeMenu.Text = L10n.RemoveMenu;
            removeMenu.AutoSize = true;
            removeMenu.Click += RemoveMenu_Click;

            panel.Controls.Add(installMenu);
            panel.Controls.Add(removeMenu);
            group.Controls.Add(panel);
            return group;
        }

        Control BuildUpdatesPage(AppSettings settings)
        {
            var group = NewGroup(L10n.SettingsUpdatesGroup);
            group.Dock = DockStyle.Top;

            var root = NewGrid(2);
            root.Dock = DockStyle.Top;

            checkUpdatesOnStartup.Text = L10n.SettingsCheckUpdatesOnStartup;
            checkUpdatesOnStartup.AutoSize = true;
            checkUpdatesOnStartup.Checked = settings.CheckUpdatesOnStartup;

            includePrereleaseUpdates.Text = L10n.SettingsIncludePrerelease;
            includePrereleaseUpdates.AutoSize = true;
            includePrereleaseUpdates.Checked = settings.IncludePrereleaseUpdates;

            var row = 0;
            root.Controls.Add(checkUpdatesOnStartup, 0, row++);
            root.SetColumnSpan(checkUpdatesOnStartup, 2);
            root.Controls.Add(includePrereleaseUpdates, 0, row++);
            root.SetColumnSpan(includePrereleaseUpdates, 2);

            group.Controls.Add(root);
            return group;
        }

        FlowLayoutPanel BuildBottomButtons()
        {
            var panel = new FlowLayoutPanel
            {
                Height = 44,
                Padding = new Padding(8, 6, 8, 6),
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            cancel.Text = L10n.SettingsCancel;
            cancel.AutoSize = true;
            cancel.DialogResult = DialogResult.Cancel;

            ok.Text = L10n.SettingsOk;
            ok.AutoSize = true;
            ok.DialogResult = DialogResult.OK;
            ok.Click += Ok_Click;

            panel.Controls.Add(cancel);
            panel.Controls.Add(ok);
            return panel;
        }

        void Ok_Click(object? sender, EventArgs e)
        {
            var languageItem = language.SelectedItem as LanguageItem;
            Settings.Language = languageItem?.Code ?? "ru";
            Settings.AutoApplyOnStartup = autoApply.Checked;
            Settings.ConfirmBeforeApplyRules = confirmApply.Checked;
            Settings.AutoRefreshProcesses = autoRefresh.Checked;
            Settings.ProcessRefreshSeconds = (int)refreshSeconds.Value;
            Settings.RunAsAdministrator = runAsAdmin.Checked;

            Settings.GuiStartupMode = guiStartupAllUsers.Checked
                ? StartupScopeMode.AllUsers
                : guiStartupCurrentUser.Checked
                    ? StartupScopeMode.CurrentUser
                    : StartupScopeMode.Disabled;

            if (engineStartupAllUsers.Checked)
                Settings.CoreEngineStartupMode = CoreEngineStartupMode.AllUsers;
            else if (engineStartupCurrentUser.Checked)
                Settings.CoreEngineStartupMode = CoreEngineStartupMode.CurrentUser;
            else if (engineStartupServiceLike.Checked)
                Settings.CoreEngineStartupMode = CoreEngineStartupMode.ServiceLike;
            else if (engineStartupWithGui.Checked)
                Settings.CoreEngineStartupMode = CoreEngineStartupMode.WithGui;
            else
                Settings.CoreEngineStartupMode = CoreEngineStartupMode.Disabled;

            Settings.RestartCoreEngineIfStopped = engineAutoRecover.Checked;
            Settings.ProcessScope = processScopeCurrentSession.Checked
                ? MultiUserProcessScope.CurrentSessionOnly
                : MultiUserProcessScope.AllAccessibleProcesses;
            Settings.UseSharedDataLocation = useSharedLocation.Checked;
            Settings.SharedConfigPath = configPath.Text.Trim();
            Settings.SharedLogsPath = logsPath.Text.Trim();

            Settings.DateFormat = dateUsa.Checked
                ? DateDisplayFormat.Usa
                : dateAsia.Checked
                    ? DateDisplayFormat.Asia
                    : DateDisplayFormat.Europe;

            Settings.CheckUpdatesOnStartup = checkUpdatesOnStartup.Checked;
            Settings.IncludePrereleaseUpdates = includePrereleaseUpdates.Checked;

            Settings.AutoStartWithWindows = Settings.GuiStartupMode != StartupScopeMode.Disabled;
        }

        void UpdateSharedLocationEnabledState()
        {
            var enabled = useSharedLocation.Checked;
            configPath.Enabled = enabled;
            logsPath.Enabled = enabled;
            browseConfig.Enabled = enabled;
            browseLogs.Enabled = enabled;
        }

        static void BrowseFolder(TextBox target)
        {
            using var dialog = new FolderBrowserDialog
            {
                SelectedPath = target.Text
            };

            if (dialog.ShowDialog() == DialogResult.OK)
                target.Text = dialog.SelectedPath;
        }

        static GroupBox NewGroup(string title)
            => new()
            {
                Text = title,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10)
            };

        static TableLayoutPanel NewGrid(int columns)
        {
            var grid = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = columns
            };

            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (var i = 1; i < columns; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            return grid;
        }

        void InstallMenu_Click(object? sender, EventArgs e)
        {
            var selectedLanguage = L10n.ParseLanguage(L10n.DetectSystemLanguageCode());

            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath, "--install-menu-machine")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(psi);
                return;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
            }
            catch
            {
            }

            if (WindowsIntegration.InstallExplorerContextMenu(Application.ExecutablePath, selectedLanguage, out var error))
                MessageBox.Show(L10n.MsgMenuInstalled, L10n.AppTitle);
            else
                MessageBox.Show(L10n.MsgMenuInstallFailed(error), L10n.AppTitle);
        }

        void RemoveMenu_Click(object? sender, EventArgs e)
        {
            if (WindowsIntegration.UninstallExplorerContextMenu(out var error))
                MessageBox.Show(L10n.MsgMenuRemoved, L10n.AppTitle);
            else
                MessageBox.Show(L10n.MsgMenuRemoveFailed(error), L10n.AppTitle);
        }

        sealed class LanguageItem
        {
            public string Code { get; }
            public string Title { get; }

            public LanguageItem(string code, string title)
            {
                Code = code;
                Title = title;
            }

            public override string ToString() => Title;
        }
    }
}
