using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PriorityManagerX
{
    public partial class MainForm : Form
    {
        string file = RuleStore.GetRulesFilePath();
        readonly Timer processTimer = new();
        AppSettings settings;
        RuleFile rules = new();

        readonly Panel topPanel = new();
        readonly Button settingsButton = new();
        readonly Button helpButton = new();
        readonly ContextMenuStrip helpMenu = new();

        readonly TabControl tabs = new();
        readonly TabPage processesTab = new();
        readonly TabPage rulesTab = new();

        readonly Label runningLabel = new();
        readonly TabControl processSections = new();
        readonly TabPage processApplicationsTab = new();
        readonly TabPage processBackgroundTab = new();
        readonly TabPage processWindowsTab = new();
        readonly SplitContainer applicationsSplit = new();
        readonly Label runningAppsLabel = new();
        readonly Label allAppsLabel = new();
        readonly DataGridView runningAppsGrid = new();
        readonly DataGridView allAppsGrid = new();
        readonly DataGridView backgroundGrid = new();
        readonly DataGridView windowsGrid = new();
        DataGridView? currentProcessGrid;
        readonly HashSet<string> expandedRunningAppGroups = new(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> expandedAllAppGroups = new(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> expandedBackgroundGroups = new(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> expandedWindowsGroups = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<int, ProcessRuntimeSample> previousRuntimeSamples = new();
        readonly Dictionary<DataGridView, ProcessSortColumn> processSortColumns = new();
        readonly Dictionary<DataGridView, ProcessHeatScale> processHeatScales = new();
        readonly Dictionary<string, FileMetadata> fileMetadataCache = new(StringComparer.OrdinalIgnoreCase);
        readonly Image defaultProcessIcon = SystemIcons.Application.ToBitmap();
        readonly int currentSessionId = Process.GetCurrentProcess().SessionId;
        bool isRefreshingProcesses;
        List<ProcessRow> latestSnapshotRows = new();
        static readonly System.Drawing.Color[] GroupRowPalette =
        {
            System.Drawing.Color.FromArgb(224, 246, 255),
            System.Drawing.Color.FromArgb(224, 255, 238),
            System.Drawing.Color.FromArgb(255, 242, 220),
            System.Drawing.Color.FromArgb(240, 230, 255),
            System.Drawing.Color.FromArgb(255, 232, 240),
            System.Drawing.Color.FromArgb(228, 252, 252)
        };

        int totalProcessCount;
        int totalApplicationCount;
        int runningApplicationCount;
        int backgroundProcessCount;
        int windowsProcessCount;

        readonly Panel processSearchBox = new();
        readonly TextBox processSearch = new();
        readonly Label processSearchIcon = new();
        readonly Button processSearchClear = new();
        readonly Button refreshProcesses = new();
        readonly Button openTaskManager = new();
        readonly Button donateQuickButton = new();

        readonly Label ruleEditorLabel = new();
        readonly Panel rulesSearchBox = new();
        readonly TextBox rulesSearch = new();
        readonly Label rulesSearchIcon = new();
        readonly Button rulesSearchClear = new();
        readonly ComboBox processInput = new();
        readonly ComboBox priorityInput = new();
        readonly DataGridView rulesGrid = new();
        readonly Button addRule = new();
        readonly Button updateRule = new();
        readonly Button deleteRule = new();
        readonly Button duplicateRule = new();
        readonly Button moveUpRule = new();
        readonly Button moveDownRule = new();
        readonly Button applySelectedRule = new();
        readonly Button applyAllRules = new();
        readonly Button deleteAllRules = new();
        readonly Button exportRules = new();
        readonly Button importRules = new();
        readonly ContextMenuStrip ruleCellEditorMenu = new();

        readonly ContextMenuStrip processMenu = new();

        public MainForm(AppSettings initialSettings)
        {
            settings = initialSettings;
            L10n.CurrentLanguage = L10n.ParseLanguage(settings.Language);

            Text = L10n.AppTitle;
            Width = 1180;
            Height = 760;
            MinimumSize = new System.Drawing.Size(1024, 640);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            BuildTopPanel();
            BuildTabs();
            BuildProcessTab();
            BuildRulesTab();
            BuildProcessContextMenu();
            processMenu.Opening += ProcessMenu_Opening;

            processesTab.Resize += (_, _) => UpdateSearchLayout();
            rulesTab.Resize += (_, _) => UpdateSearchLayout();
            tabs.SelectedIndexChanged += (_, _) => UpdateSearchLayout();

            processTimer.Tick += ProcessTimer_Tick;

            Controls.Add(tabs);
            Controls.Add(topPanel);

            LoadRules();
            SyncDefaultsFromRulesSilently();
            RefreshRulesGrid();
            RefreshProcessGrid();
            ApplyLocalization();
            ApplySettingsRuntime();

            if (settings.AutoApplyOnStartup)
                ApplyAllRulesCore();
        }

        void BuildTopPanel()
        {
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 44;
            topPanel.Padding = new Padding(8, 0, 8, 0);

            donateQuickButton.Width = 140;
            donateQuickButton.Height = 30;
            donateQuickButton.Top = 7;
            donateQuickButton.Dock = DockStyle.Right;
            donateQuickButton.Font = new System.Drawing.Font("Segoe UI", 10F);
            donateQuickButton.Click += (_, _) => OpenUrl("https://cyblight.org/donate/");

            settingsButton.Width = 140;
            settingsButton.Height = 30;
            settingsButton.Top = 7;
            settingsButton.Dock = DockStyle.Left;
            settingsButton.Font = new System.Drawing.Font("Segoe UI", 10F);
            settingsButton.Click += SettingsButton_Click;
            helpButton.Width = 120;
            helpButton.Height = 30;
            helpButton.Top = 7;
            helpButton.Dock = DockStyle.Left;
            helpButton.Font = new System.Drawing.Font("Segoe UI", 10F);
            helpButton.Image = SystemIcons.Question.ToBitmap();
            helpButton.ImageAlign = ContentAlignment.MiddleLeft;
            helpButton.TextImageRelation = TextImageRelation.ImageBeforeText;
            helpButton.Click += HelpButton_Click;

            helpMenu.Items.Add(new ToolStripMenuItem(string.Empty, null, (_, _) => ShowInstruction()));
            helpMenu.Items.Add(new ToolStripMenuItem(string.Empty, null, (_, _) => OpenUrl("https://cyblight.org/")));
            helpMenu.Items.Add(new ToolStripMenuItem(string.Empty, null, (_, _) => ShowAboutDialog()));
            helpMenu.Items.Add(new ToolStripMenuItem(string.Empty, null, (_, _) => OpenUrl("https://cyblight.org/donate/")));

            topPanel.Controls.Add(donateQuickButton);
            topPanel.Controls.Add(helpButton);
            topPanel.Controls.Add(settingsButton);
        }

        void BuildTabs()
        {
            tabs.Dock = DockStyle.Fill;
            tabs.Top = 44;

            processesTab.Padding = new Padding(8);
            rulesTab.Padding = new Padding(8);

            tabs.TabPages.Add(processesTab);
            tabs.TabPages.Add(rulesTab);
        }

        void BuildProcessTab()
        {
            runningLabel.Left = 8;
            runningLabel.Top = 8;
            runningLabel.AutoSize = true;

            processSearchBox.Top = 4;
            processSearchBox.Left = 790;
            processSearchBox.Width = 350;
            processSearchBox.Height = 28;
            processSearchBox.BorderStyle = BorderStyle.FixedSingle;
            processSearchBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            ConfigureSearchBox(processSearchBox, processSearch, processSearchIcon, processSearchClear,
                (_, _) =>
                {
                    processSearch.Text = string.Empty;
                    processSearch.Focus();
                });

            processSearch.TextChanged += (_, _) => RebuildProcessViewsFromSnapshot();

            refreshProcesses.Left = 280;
            refreshProcesses.Top = 4;
            refreshProcesses.Width = 120;
            refreshProcesses.Click += RefreshProcesses_Click;

            openTaskManager.Left = 410;
            openTaskManager.Top = 4;
            openTaskManager.Width = 220;
            openTaskManager.Click += OpenTaskManager_Click;

            processSections.Left = 8;
            processSections.Top = 40;
            processSections.Width = 1130;
            processSections.Height = 600;
            processSections.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            processApplicationsTab.Padding = new Padding(6);
            processBackgroundTab.Padding = new Padding(6);
            processWindowsTab.Padding = new Padding(6);

            processSections.TabPages.Add(processApplicationsTab);
            processSections.TabPages.Add(processBackgroundTab);
            processSections.TabPages.Add(processWindowsTab);

            applicationsSplit.Dock = DockStyle.Fill;
            applicationsSplit.Orientation = Orientation.Horizontal;
            applicationsSplit.SplitterDistance = 250;

            allAppsLabel.Dock = DockStyle.Top;
            allAppsLabel.Height = 22;

            ConfigureProcessGrid(runningAppsGrid);
            ConfigureProcessGrid(allAppsGrid);
            ConfigureProcessGrid(backgroundGrid);
            ConfigureProcessGrid(windowsGrid);

            applicationsSplit.Panel1.Controls.Add(runningAppsGrid);
            applicationsSplit.Panel2.Controls.Add(allAppsGrid);
            applicationsSplit.Panel2.Controls.Add(allAppsLabel);

            processApplicationsTab.Controls.Add(applicationsSplit);
            processBackgroundTab.Controls.Add(backgroundGrid);
            processWindowsTab.Controls.Add(windowsGrid);

            currentProcessGrid = runningAppsGrid;

            processesTab.Controls.Add(runningLabel);
            processesTab.Controls.Add(processSearchBox);
            processesTab.Controls.Add(refreshProcesses);
            processesTab.Controls.Add(openTaskManager);
            processesTab.Controls.Add(processSections);

            processSearchBox.BringToFront();
            UpdateSearchLayout();
        }

        void ConfigureProcessGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.AutoGenerateColumns = false;
            grid.CellMouseDown += ProcessGrid_CellMouseDown;
            grid.CellDoubleClick += ProcessGrid_CellDoubleClick;
            grid.CellFormatting += ProcessGrid_CellFormatting;
            grid.ColumnHeaderMouseClick += ProcessGrid_ColumnHeaderMouseClick;
            grid.RowTemplate.Height = 22;

            grid.Columns.Add(new DataGridViewImageColumn
            {
                Name = "Icon",
                DataPropertyName = "Icon",
                Width = 24,
                ImageLayout = DataGridViewImageCellLayout.Zoom
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", DataPropertyName = "Name", Width = 300 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pid", DataPropertyName = "Pid", Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Priority", DataPropertyName = "Priority", Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CpuPercent", DataPropertyName = "CpuPercent", Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MemoryMB", DataPropertyName = "MemoryMB", Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DiskRate", DataPropertyName = "DiskRate", Width = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NetworkRate", DataPropertyName = "NetworkRate", Width = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Threads", DataPropertyName = "Threads", Width = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", DataPropertyName = "Path", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.Name is "CpuPercent" or "MemoryMB" or "DiskRate" or "NetworkRate" or "Threads")
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                col.SortMode = DataGridViewColumnSortMode.Programmatic;
            }
        }

        void ConfigureSearchBox(Panel host, TextBox textBox, Label icon, Button clearButton, EventHandler clearClick)
        {
            host.Controls.Clear();
            var accent = System.Drawing.Color.FromArgb(223, 239, 216);

            host.BackColor = accent;

            icon.Text = "🔍";
            icon.Dock = DockStyle.Right;
            icon.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            icon.ForeColor = System.Drawing.Color.DimGray;
            icon.BackColor = accent;
            icon.Width = 26;
            icon.Font = new System.Drawing.Font("Segoe UI Emoji", 10F);

            clearButton.Text = "✖";
            clearButton.Dock = DockStyle.Right;
            clearButton.Width = 26;
            clearButton.ForeColor = System.Drawing.Color.Firebrick;
            clearButton.BackColor = accent;
            clearButton.FlatStyle = FlatStyle.Flat;
            clearButton.FlatAppearance.BorderSize = 0;
            clearButton.FlatAppearance.MouseOverBackColor = accent;
            clearButton.FlatAppearance.MouseDownBackColor = accent;
            clearButton.Visible = false;
            clearButton.TabStop = false;
            clearButton.Click -= clearClick;
            clearButton.Click += clearClick;

            textBox.BorderStyle = BorderStyle.None;
            textBox.Dock = DockStyle.Fill;
            textBox.BackColor = accent;
            textBox.Margin = new Padding(6, 5, 6, 5);
            textBox.Location = new System.Drawing.Point(6, 6);

            textBox.TextChanged -= SearchTextBox_TextChanged;
            textBox.GotFocus -= SearchTextBox_FocusChanged;
            textBox.LostFocus -= SearchTextBox_FocusChanged;

            textBox.TextChanged += SearchTextBox_TextChanged;
            textBox.GotFocus += SearchTextBox_FocusChanged;
            textBox.LostFocus += SearchTextBox_FocusChanged;

            host.Controls.Add(textBox);
            host.Controls.Add(clearButton);
            host.Controls.Add(icon);

            UpdateSearchVisualState(textBox);
        }

        void SearchTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (sender is TextBox tb)
                UpdateSearchVisualState(tb);
        }

        void SearchTextBox_FocusChanged(object? sender, EventArgs e)
        {
            if (sender is TextBox tb)
                UpdateSearchVisualState(tb);
        }

        void UpdateSearchVisualState(TextBox textBox)
        {
            var hasText = !string.IsNullOrWhiteSpace(textBox.Text);

            if (ReferenceEquals(textBox, processSearch))
            {
                processSearchClear.Visible = hasText;
                processSearchIcon.Visible = !hasText;
            }
            else if (ReferenceEquals(textBox, rulesSearch))
            {
                rulesSearchClear.Visible = hasText;
                rulesSearchIcon.Visible = !hasText;
            }
        }

        void BuildRulesTab()
        {
            ruleEditorLabel.Left = 8;
            ruleEditorLabel.Top = 8;
            ruleEditorLabel.Width = 200;

            processInput.Left = 8;
            processInput.Top = 32;
            processInput.Width = 220;
            processInput.DropDownStyle = ComboBoxStyle.DropDown;
            processInput.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            processInput.AutoCompleteSource = AutoCompleteSource.ListItems;

            priorityInput.Left = 236;
            priorityInput.Top = 32;
            priorityInput.Width = 170;
            priorityInput.DropDownStyle = ComboBoxStyle.DropDownList;
            PopulatePriorityInput("Normal");

            addRule.Left = 416;
            addRule.Top = 30;
            addRule.Width = 95;
            addRule.Click += AddRule_Click;

            updateRule.Left = 516;
            updateRule.Top = 30;
            updateRule.Width = 95;
            updateRule.Click += UpdateRule_Click;

            deleteRule.Left = 616;
            deleteRule.Top = 30;
            deleteRule.Width = 95;
            deleteRule.Click += DeleteRule_Click;

            duplicateRule.Left = 716;
            duplicateRule.Top = 30;
            duplicateRule.Width = 105;
            duplicateRule.Click += DuplicateRule_Click;

            moveUpRule.Left = 826;
            moveUpRule.Top = 30;
            moveUpRule.Width = 80;
            moveUpRule.Click += MoveUpRule_Click;

            moveDownRule.Left = 910;
            moveDownRule.Top = 30;
            moveDownRule.Width = 80;
            moveDownRule.Click += MoveDownRule_Click;

            applySelectedRule.Left = 8;
            applySelectedRule.Top = 64;
            applySelectedRule.Width = 170;
            applySelectedRule.Click += ApplySelectedRule_Click;

            applyAllRules.Left = 184;
            applyAllRules.Top = 64;
            applyAllRules.Width = 140;
            applyAllRules.Click += ApplyAllRules_Click;

            deleteAllRules.Left = 330;
            deleteAllRules.Top = 64;
            deleteAllRules.Width = 140;
            deleteAllRules.Click += DeleteAllRules_Click;

            exportRules.Left = 476;
            exportRules.Top = 64;
            exportRules.Width = 140;
            exportRules.Click += ExportRules_Click;

            importRules.Left = 622;
            importRules.Top = 64;
            importRules.Width = 140;
            importRules.Click += ImportRules_Click;

            rulesSearchBox.Left = 770;
            rulesSearchBox.Top = 64;
            rulesSearchBox.Width = 300;
            rulesSearchBox.Height = 28;
            rulesSearchBox.BorderStyle = BorderStyle.FixedSingle;
            rulesSearchBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            ConfigureSearchBox(rulesSearchBox, rulesSearch, rulesSearchIcon, rulesSearchClear,
                (_, _) =>
                {
                    rulesSearch.Text = string.Empty;
                    rulesSearch.Focus();
                });

            rulesSearch.TextChanged += (_, _) => RefreshRulesGrid();

            rulesGrid.Left = 8;
            rulesGrid.Top = 96;
            rulesGrid.Width = 1130;
            rulesGrid.Height = 560;
            rulesGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rulesGrid.ReadOnly = true;
            rulesGrid.AllowUserToAddRows = false;
            rulesGrid.AllowUserToDeleteRows = false;
            rulesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            rulesGrid.MultiSelect = false;
            rulesGrid.RowHeadersVisible = false;
            rulesGrid.AutoGenerateColumns = false;
            rulesGrid.SelectionChanged += RulesGrid_SelectionChanged;
            rulesGrid.CellDoubleClick += RulesGrid_CellDoubleClick;

            rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleProcess", DataPropertyName = "Process", Width = 280 });
            rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleCpuMatch", DataPropertyName = "CpuMatch", Width = 170 });
            rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleCpuSets", DataPropertyName = "CpuSets", Width = 150 });
            rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RulePriorityClass", DataPropertyName = "PriorityClass", Width = 260 });
            rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleIoPriority", DataPropertyName = "IoPriority", Width = 140 });
            rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleGpuPriority", DataPropertyName = "GpuPriority", Width = 130 });
            rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleEfficiencyMode", DataPropertyName = "EfficiencyMode", Width = 130 });
            rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RulePerformanceMode", DataPropertyName = "PerformanceMode", Width = 150 });

            rulesTab.Controls.Add(ruleEditorLabel);
            rulesTab.Controls.Add(processInput);
            rulesTab.Controls.Add(priorityInput);
            rulesTab.Controls.Add(addRule);
            rulesTab.Controls.Add(updateRule);
            rulesTab.Controls.Add(deleteRule);
            rulesTab.Controls.Add(duplicateRule);
            rulesTab.Controls.Add(moveUpRule);
            rulesTab.Controls.Add(moveDownRule);
            rulesTab.Controls.Add(applySelectedRule);
            rulesTab.Controls.Add(applyAllRules);
            rulesTab.Controls.Add(deleteAllRules);
            rulesTab.Controls.Add(exportRules);
            rulesTab.Controls.Add(importRules);
            rulesTab.Controls.Add(rulesSearchBox);
            rulesTab.Controls.Add(rulesGrid);

            rulesSearchBox.BringToFront();
            RefreshProcessInputOptions();
            UpdateSearchLayout();
        }

        void UpdateSearchLayout()
        {
            if (processesTab.ClientSize.Width > 0)
            {
                refreshProcesses.Left = runningLabel.Right + 12;
                openTaskManager.Left = refreshProcesses.Right + 8;

                processSearchBox.Top = 4;
                var preferredWidth = Math.Max(200, Math.Min(380, processesTab.ClientSize.Width / 3));
                var minLeft = openTaskManager.Right + 12;
                var availableWidth = Math.Max(120, processesTab.ClientSize.Width - minLeft - 12);

                processSearchBox.Width = Math.Min(preferredWidth, availableWidth);
                processSearchBox.Left = processesTab.ClientSize.Width - processSearchBox.Width - 12;

                if (processSearchBox.Left < minLeft)
                    processSearchBox.Left = minLeft;
            }

            if (rulesTab.ClientSize.Width > 0)
            {
                rulesSearchBox.Top = 64;
                rulesSearchBox.Width = Math.Max(220, Math.Min(380, rulesTab.ClientSize.Width / 3));
                rulesSearchBox.Left = rulesTab.ClientSize.Width - rulesSearchBox.Width - 12;
            }
        }

        void BuildProcessContextMenu()
        {
            processMenu.Items.Clear();

            processMenu.Items.Add(new ToolStripMenuItem(L10n.CtxRefresh, null, (_, _) => RefreshProcessGrid()));
            processMenu.Items.Add(new ToolStripSeparator());

            var currentPriority = new ToolStripMenuItem(L10n.CtxPriorityCurrent);
            foreach (var priority in Enum.GetNames(typeof(ProcessPriorityClass)))
            {
                var p = priority;
                currentPriority.DropDownItems.Add(new ToolStripMenuItem(L10n.PriorityDisplay(p), null, (_, _) => SetCurrentPriorityForSelected(p)));
            }

            var alwaysPriority = new ToolStripMenuItem(L10n.CtxPriorityAlways);
            foreach (var priority in Enum.GetNames(typeof(ProcessPriorityClass)))
            {
                var p = priority;
                alwaysPriority.DropDownItems.Add(new ToolStripMenuItem(L10n.PriorityDisplay(p), null, (_, _) => SaveDefaultPriorityForSelected(p)));
            }

            processMenu.Items.Add(currentPriority);
            processMenu.Items.Add(alwaysPriority);
            processMenu.Items.Add(new ToolStripMenuItem(L10n.CtxRemoveSavedPriority, null, (_, _) => RemoveSavedPriorityForSelected()));

            var affinity = new ToolStripMenuItem(L10n.CtxAffinity);
            affinity.DropDownItems.Add(new ToolStripMenuItem(L10n.CtxAffinityAll, null, (_, _) => SetAffinityAllForSelected()));
            for (var i = 0; i < Math.Min(Environment.ProcessorCount, 8); i++)
            {
                var index = i;
                affinity.DropDownItems.Add(new ToolStripMenuItem($"CPU {index}", null, (_, _) => SetAffinitySingleCpuForSelected(index)));
            }
            processMenu.Items.Add(affinity);

            processMenu.Items.Add(new ToolStripSeparator());
            processMenu.Items.Add(new ToolStripMenuItem(L10n.CtxAddRule, null, (_, _) => AddRuleFromSelectedProcess()));
            processMenu.Items.Add(new ToolStripMenuItem(L10n.CtxApplyRulesNow, null, (_, _) => ApplyAllRules_Click(this, EventArgs.Empty)));
            processMenu.Items.Add(new ToolStripSeparator());
            processMenu.Items.Add(new ToolStripMenuItem(L10n.CtxRestartProcess, null, (_, _) => RestartSelectedProcess()));
            processMenu.Items.Add(new ToolStripMenuItem(L10n.CtxKillProcess, null, (_, _) => KillSelectedProcess()));
            processMenu.Items.Add(new ToolStripMenuItem(L10n.CtxOpenLocation, null, (_, _) => OpenLocationForSelected()));
            processMenu.Items.Add(new ToolStripMenuItem(L10n.CtxCopyName, null, (_, _) => CopyNameForSelected()));
            processMenu.Items.Add(new ToolStripMenuItem(L10n.CtxCopyPath, null, (_, _) => CopyPathForSelected()));
        }

        void ProcessMenu_Opening(object? sender, CancelEventArgs e)
        {
            var grid = ResolveActiveProcessGrid();
            if (grid == null || grid.IsDisposed)
            {
                e.Cancel = true;
                return;
            }

            currentProcessGrid = grid;

            var cursorPoint = grid.PointToClient(Cursor.Position);
            var hit = grid.HitTest(cursorPoint.X, cursorPoint.Y);
            if (hit.RowIndex >= 0)
            {
                grid.ClearSelection();
                grid.Rows[hit.RowIndex].Selected = true;
                grid.CurrentCell = grid.Rows[hit.RowIndex].Cells[0];
            }

            if (grid.CurrentRow?.DataBoundItem is not ProcessViewRow)
                e.Cancel = true;
        }

        DataGridView? ResolveActiveProcessGrid()
        {
            if (runningAppsGrid.ContainsFocus)
                return runningAppsGrid;
            if (allAppsGrid.ContainsFocus)
                return allAppsGrid;
            if (backgroundGrid.ContainsFocus)
                return backgroundGrid;
            if (windowsGrid.ContainsFocus)
                return windowsGrid;

            if (currentProcessGrid != null && !currentProcessGrid.IsDisposed)
                return currentProcessGrid;

            if (ReferenceEquals(processSections.SelectedTab, processBackgroundTab))
                return backgroundGrid;
            if (ReferenceEquals(processSections.SelectedTab, processWindowsTab))
                return windowsGrid;

            return runningAppsGrid;
        }

        void SettingsButton_Click(object? sender, EventArgs e)
        {
            var previousSettings = settings;
            using var dialog = new SettingsForm(settings);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            settings = dialog.Settings;

            var startupProfileChanged =
                previousSettings.GuiStartupMode != settings.GuiStartupMode
                || previousSettings.CoreEngineStartupMode != settings.CoreEngineStartupMode
                || previousSettings.RunAsAdministrator != settings.RunAsAdministrator;

            if (startupProfileChanged)
            {
                if (!WindowsIntegration.ConfigureStartupProfile(Application.ExecutablePath, settings, out var startupError))
                {
                    settings.GuiStartupMode = previousSettings.GuiStartupMode;
                    settings.CoreEngineStartupMode = previousSettings.CoreEngineStartupMode;
                    MessageBox.Show(L10n.MsgAutoStartFailed(startupError), L10n.AppTitle);
                }
            }

            if (previousSettings.RunAsAdministrator != settings.RunAsAdministrator)
            {
                if (!WindowsIntegration.SetRunAsAdministratorFlag(Application.ExecutablePath, settings.RunAsAdministrator, out var runAsAdminError))
                {
                    settings.RunAsAdministrator = previousSettings.RunAsAdministrator;
                    MessageBox.Show(L10n.MsgRunAsAdminFailed(runAsAdminError), L10n.AppTitle);
                }
            }

            AppSettingsStore.Save(settings);
            file = RuleStore.GetRulesFilePath();
            CoreEngineHost.ApplyRuntimeProfile(settings);
            L10n.CurrentLanguage = L10n.ParseLanguage(settings.Language);
            ApplyLocalization();
            BuildProcessContextMenu();
            ApplySettingsRuntime();
            RefreshProcessGrid();
            RefreshRulesGrid(SelectedRuleIndex());
            MessageBox.Show(L10n.MsgSettingsSaved, L10n.AppTitle);
        }

        void AddRule_Click(object? sender, EventArgs e)
        {
            if (!TryReadRuleInputs(out var processName, out var priority))
            {
                MessageBox.Show(L10n.MsgRuleInputInvalid, L10n.AppTitle);
                return;
            }

            rules.Rules.Add(new Rule { Process = processName, Priority = priority });
            SaveRules();
            SyncRuleDefaultSilently(processName, priority);
            RefreshRulesGrid();
            MessageBox.Show(L10n.MsgRuleAdded, L10n.AppTitle);
        }

        void UpdateRule_Click(object? sender, EventArgs e)
        {
            var index = SelectedRuleIndex();
            if (index < 0)
            {
                MessageBox.Show(L10n.MsgSelectRuleFirst, L10n.AppTitle);
                return;
            }

            if (!TryReadRuleInputs(out var processName, out var priority))
            {
                MessageBox.Show(L10n.MsgRuleInputInvalid, L10n.AppTitle);
                return;
            }

            rules.Rules[index].Process = processName;
            rules.Rules[index].Priority = priority;
            SaveRules();
            SyncRuleDefaultSilently(processName, priority);
            RefreshRulesGrid(index);
            MessageBox.Show(L10n.MsgRuleUpdated, L10n.AppTitle);
        }

        void DeleteRule_Click(object? sender, EventArgs e)
        {
            var index = SelectedRuleIndex();
            if (index < 0)
            {
                MessageBox.Show(L10n.MsgSelectRuleFirst, L10n.AppTitle);
                return;
            }

            var removed = rules.Rules[index];
            rules.Rules.RemoveAt(index);
            SaveRules();
            RemoveRuleDefaultSilently(removed.Process);
            RefreshRulesGrid();
            MessageBox.Show(L10n.MsgRuleDeleted, L10n.AppTitle);
        }

        void DuplicateRule_Click(object? sender, EventArgs e)
        {
            var index = SelectedRuleIndex();
            if (index < 0)
            {
                MessageBox.Show(L10n.MsgSelectRuleFirst, L10n.AppTitle);
                return;
            }

            var source = rules.Rules[index];
            rules.Rules.Insert(index + 1, new Rule { Process = source.Process, Priority = source.Priority });
            SaveRules();
            SyncRuleDefaultSilently(source.Process, source.Priority);
            RefreshRulesGrid(index + 1);
            MessageBox.Show(L10n.MsgRuleDuplicated, L10n.AppTitle);
        }

        void MoveUpRule_Click(object? sender, EventArgs e)
        {
            var index = SelectedRuleIndex();
            if (index <= 0)
                return;

            (rules.Rules[index - 1], rules.Rules[index]) = (rules.Rules[index], rules.Rules[index - 1]);
            SaveRules();
            RefreshRulesGrid(index - 1);
        }

        void MoveDownRule_Click(object? sender, EventArgs e)
        {
            var index = SelectedRuleIndex();
            if (index < 0 || index >= rules.Rules.Count - 1)
                return;

            (rules.Rules[index + 1], rules.Rules[index]) = (rules.Rules[index], rules.Rules[index + 1]);
            SaveRules();
            RefreshRulesGrid(index + 1);
        }

        void ApplySelectedRule_Click(object? sender, EventArgs e)
        {
            var index = SelectedRuleIndex();
            if (index < 0)
            {
                MessageBox.Show(L10n.MsgSelectRuleFirst, L10n.AppTitle);
                return;
            }

            var rule = rules.Rules[index];
            var result = PriorityEngine.ApplyWithResult(rule.Process, rule.Priority);
            RefreshProcessGrid();
            MessageBox.Show(BuildApplySummary(new[] { result }), L10n.AppTitle);
        }

        void ApplyAllRules_Click(object? sender, EventArgs e)
        {
            if (settings.ConfirmBeforeApplyRules)
            {
                var confirm = MessageBox.Show(L10n.MsgApplyConfirm, L10n.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                    return;
            }

            var results = ApplyAllRulesCore();
            RefreshProcessGrid();
            MessageBox.Show(BuildApplySummary(results), L10n.AppTitle);
        }

        void DeleteAllRules_Click(object? sender, EventArgs e)
        {
            var confirm = MessageBox.Show(L10n.MsgDeleteAllRulesConfirm, L10n.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            var rulesToRemove = rules.Rules.ToList();
            rules.Rules.Clear();
            SaveRules();
            foreach (var rule in rulesToRemove)
                RemoveRuleDefaultSilently(rule.Process);
            RefreshRulesGrid();
            MessageBox.Show(L10n.MsgRulesDeletedAll, L10n.AppTitle);
        }

        void ExportRules_Click(object? sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = "pmx-rules-backup.json",
                Title = L10n.ExportRules
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var payload = JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, payload);
                MessageBox.Show(L10n.MsgRulesExported(dialog.FileName), L10n.AppTitle);
            }
            catch (Exception ex)
            {
                MessageBox.Show(L10n.MsgRulesExportFailed(ex.Message), L10n.AppTitle);
            }
        }

        void ImportRules_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = L10n.ImportRules,
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var imported = JsonSerializer.Deserialize<RuleFile>(File.ReadAllText(dialog.FileName));
                if (imported == null)
                {
                    MessageBox.Show(L10n.MsgRulesImportFailed(L10n.MsgRuleInputInvalid), L10n.AppTitle);
                    return;
                }

                foreach (var rule in imported.Rules)
                {
                    rule.Process = NormalizeProcessInput(rule.Process);
                    rule.Priority = L10n.NormalizePriority(rule.Priority);
                }

                imported.Rules = imported.Rules
                    .Where(r => !string.IsNullOrWhiteSpace(r.Process))
                    .GroupBy(r => r.Process, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .ToList();

                rules = imported;
                SaveRules();
                SyncDefaultsFromRulesSilently();
                RefreshRulesGrid();
                RefreshProcessInputOptions();
                MessageBox.Show(L10n.MsgRulesImported(imported.Rules.Count), L10n.AppTitle);
            }
            catch (Exception ex)
            {
                MessageBox.Show(L10n.MsgRulesImportFailed(ex.Message), L10n.AppTitle);
            }
        }

        List<PriorityEngine.ApplyResult> ApplyAllRulesCore()
        {
            var results = new List<PriorityEngine.ApplyResult>();
            foreach (var rule in rules.Rules)
                results.Add(PriorityEngine.ApplyWithResult(rule.Process, rule.Priority));

            return results;
        }

        void RefreshProcesses_Click(object? sender, EventArgs e)
        {
            RebuildProcessViewsFromSnapshot();
        }

        void OpenTaskManager_Click(object? sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, L10n.AppTitle);
            }
        }

        void HelpButton_Click(object? sender, EventArgs e)
        {
            helpMenu.Show(helpButton, new System.Drawing.Point(0, helpButton.Height));
        }

        void ShowInstruction()
        {
            MessageBox.Show(L10n.HelpInstructionText, L10n.HelpInstructionItem, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void ShowAboutDialog()
        {
            using var form = new Form
            {
                Text = L10n.HelpAboutItem,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                Width = 560,
                Height = 330
            };

            var logo = new PictureBox
            {
                Left = 16,
                Top = 16,
                Width = 120,
                Height = 120,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = LoadAboutLogo()
            };

            var title = new Label
            {
                Left = 152,
                Top = 20,
                Width = 370,
                AutoSize = false,
                Text = L10n.AppTitle,
                Font = new System.Drawing.Font("Segoe UI", 12F, FontStyle.Bold)
            };

            var version = new Label
            {
                Left = 152,
                Top = 50,
                Width = 370,
                AutoSize = false,
                Text = L10n.AboutVersion(GetAppVersion())
            };

            var description = new Label
            {
                Left = 152,
                Top = 78,
                Width = 370,
                Height = 70,
                Text = L10n.AboutShortInfo
            };

            var site = new LinkLabel
            {
                Left = 152,
                Top = 150,
                Width = 370,
                Text = "https://cyblight.org/"
            };
            site.Click += (_, _) => OpenUrl("https://cyblight.org/");

            var devCode = new Button
            {
                Left = 16,
                Top = 250,
                Width = 170,
                Height = 30,
                Text = L10n.AboutEnterDevCode
            };
            devCode.Click += (_, _) => PromptDeveloperCode();

            var ok = new Button
            {
                Left = 430,
                Top = 250,
                Width = 90,
                Height = 30,
                Text = "OK",
                DialogResult = DialogResult.OK
            };

            form.Controls.Add(logo);
            form.Controls.Add(title);
            form.Controls.Add(version);
            form.Controls.Add(description);
            form.Controls.Add(site);
            form.Controls.Add(devCode);
            form.Controls.Add(ok);
            form.AcceptButton = ok;
            form.ShowDialog(this);
        }

        Image LoadAboutLogo()
        {
            try
            {
                var candidates = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "assets", "PmX.png"),
                    Path.Combine(AppContext.BaseDirectory, "PmX.png")
                };

                var path = candidates.FirstOrDefault(File.Exists);
                if (!string.IsNullOrWhiteSpace(path))
                    return Image.FromFile(path);
            }
            catch
            {
            }

            return Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap() ?? SystemIcons.Application.ToBitmap();
        }

        static string GetAppVersion()
        {
            try
            {
                var version = FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion;
                return string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
            }
            catch
            {
                return "1.0.0";
            }
        }

        void PromptDeveloperCode()
        {
            using var dialog = new Form
            {
                Text = L10n.AboutEnterDevCode,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                Width = 420,
                Height = 160
            };

            var textBox = new TextBox { Left = 12, Top = 14, Width = 380 };
            var ok = new Button { Left = 220, Top = 50, Width = 80, Text = "OK", DialogResult = DialogResult.OK };
            var cancel = new Button { Left = 312, Top = 50, Width = 80, Text = L10n.SettingsCancel, DialogResult = DialogResult.Cancel };

            dialog.Controls.Add(textBox);
            dialog.Controls.Add(ok);
            dialog.Controls.Add(cancel);
            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var code = textBox.Text.Trim();
            if (string.Equals(code, "CYBLIGHT-LOGS", StringComparison.OrdinalIgnoreCase))
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PriorityManagerX");
                Directory.CreateDirectory(logDir);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logDir}\"") { UseShellExecute = true });
                MessageBox.Show(L10n.DevCodeAccepted, L10n.AppTitle);
                return;
            }

            if (string.Equals(code, "CYBLIGHT-DONATE", StringComparison.OrdinalIgnoreCase))
            {
                OpenUrl("https://cyblight.org/donate/");
                MessageBox.Show(L10n.DevCodeAccepted, L10n.AppTitle);
                return;
            }

            MessageBox.Show(L10n.DevCodeInvalid, L10n.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        void ProcessGrid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is not DataGridView sourceGrid)
                return;

            if (e.RowIndex < 0)
                return;

            currentProcessGrid = sourceGrid;

            if (sourceGrid.Rows[e.RowIndex].DataBoundItem is ProcessViewRow viewRow
                && viewRow.IsGroup
                && e.Button == MouseButtons.Left)
            {
                ToggleApplicationGroup(sourceGrid, viewRow.GroupKey);
                return;
            }

            sourceGrid.ClearSelection();
            sourceGrid.Rows[e.RowIndex].Selected = true;
            sourceGrid.CurrentCell = sourceGrid.Rows[e.RowIndex].Cells[0];

            if (e.Button == MouseButtons.Right)
            {
                var localPoint = sourceGrid.PointToClient(Cursor.Position);
                processMenu.Show(sourceGrid, localPoint);
            }
        }

        void ProcessGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (sender is not DataGridView sourceGrid)
                return;

            if (e.RowIndex < 0)
                return;

            currentProcessGrid = sourceGrid;

            if (sourceGrid.Rows[e.RowIndex].DataBoundItem is not ProcessViewRow viewRow)
                return;

            if (viewRow.IsGroup)
            {
                ToggleApplicationGroup(sourceGrid, viewRow.GroupKey);
                return;
            }

            var row = viewRow.Source;
            if (row == null)
                return;

            processInput.Text = row.ExeName;
            tabs.SelectedTab = rulesTab;
        }

        void ProcessGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (sender is not DataGridView grid || e.RowIndex < 0)
                return;

            if (grid.Rows[e.RowIndex].DataBoundItem is not ProcessViewRow row)
                return;

            var style = grid.Rows[e.RowIndex].DefaultCellStyle;
            if (row.IsGroup)
                style.Font = new System.Drawing.Font(grid.Font, System.Drawing.FontStyle.Bold);
            else
                style.Font = grid.Font;

            var baseColor = ResolveGroupColor(row.GroupKey, row.Source?.ExeName ?? row.Name);
            var heatScore = GetHeatScore(grid, row);
            var heatColor = ApplyHeatTint(baseColor, heatScore);
            style.BackColor = row.IsGroup ? DarkenColor(heatColor, 0.90f) : heatColor;
            style.ForeColor = System.Drawing.Color.Black;
            style.SelectionBackColor = row.IsGroup
                ? System.Drawing.Color.FromArgb(0, 120, 215)
                : System.Drawing.Color.FromArgb(25, 118, 210);
            style.SelectionForeColor = System.Drawing.Color.White;
        }

        void ProcessGrid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is not DataGridView grid || e.ColumnIndex < 0)
                return;

            var columnName = grid.Columns[e.ColumnIndex].Name;
            var requestedSort = MapSortColumn(columnName);
            if (requestedSort == ProcessSortColumn.Default)
                return;

            var current = processSortColumns.TryGetValue(grid, out var active)
                ? active
                : ProcessSortColumn.Default;

            if (current == requestedSort)
                processSortColumns.Remove(grid);
            else
                processSortColumns[grid] = requestedSort;

            RefreshProcessGrid();
        }

        void RulesGrid_SelectionChanged(object? sender, EventArgs e)
        {
            var index = SelectedRuleIndex();
            if (index < 0 || index >= rules.Rules.Count)
                return;

            processInput.Text = rules.Rules[index].Process;
            SetPriorityInputSelection(rules.Rules[index].Priority);
        }

        void RulesGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (rulesGrid.Rows[e.RowIndex].DataBoundItem is not RuleRow row)
                return;

            if (!TryBuildRuleCellOptions(row.SourceIndex, rulesGrid.Columns[e.ColumnIndex].Name, out var options, out var current))
                return;

            ruleCellEditorMenu.Items.Clear();
            foreach (var option in options)
            {
                var value = option.Value;
                var item = new ToolStripMenuItem(option.Label)
                {
                    Checked = string.Equals(value, current, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += (_, _) => ApplyRuleCellValue(row.SourceIndex, rulesGrid.Columns[e.ColumnIndex].Name, value);
                ruleCellEditorMenu.Items.Add(item);
            }

            var displayRect = rulesGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            var location = new System.Drawing.Point(displayRect.Left, displayRect.Bottom);
            ruleCellEditorMenu.Show(rulesGrid, location);
        }

        bool TryBuildRuleCellOptions(int sourceIndex, string columnName, out List<(string Label, string Value)> options, out string current)
        {
            options = new List<(string Label, string Value)>();
            current = string.Empty;

            if (sourceIndex < 0 || sourceIndex >= rules.Rules.Count)
                return false;

            var rule = rules.Rules[sourceIndex];

            switch (columnName)
            {
                case "RulePriorityClass":
                    options = Enum.GetNames(typeof(ProcessPriorityClass))
                        .Select(name => (L10n.PriorityDisplay(name), name))
                        .ToList();
                    current = rule.Priority;
                    return true;

                case "RuleCpuMatch":
                    options = new List<(string, string)>
                    {
                        ("Все", "Все"),
                        ("До 90% загрузки ЦП", "До 90% загрузки ЦП"),
                        ("До 75% загрузки ЦП", "До 75% загрузки ЦП"),
                        ("До 50% загрузки ЦП", "До 50% загрузки ЦП")
                    };
                    current = rule.CpuMatch;
                    return true;

                case "RuleCpuSets":
                    options = new List<(string, string)>
                    {
                        ("Все", "Все"),
                        ("P-ядра", "P-ядра"),
                        ("E-ядра", "E-ядра"),
                        ("Пользовательский", "Пользовательский")
                    };
                    current = rule.CpuSets;
                    return true;

                case "RuleIoPriority":
                    options = new List<(string, string)>
                    {
                        ("Очень низкий", "Очень низкий"),
                        ("Низкий", "Низкий"),
                        ("Обычный", "Обычный"),
                        ("Высокий", "Высокий")
                    };
                    current = rule.IoPriority;
                    return true;

                case "RuleGpuPriority":
                    options = new List<(string, string)>
                    {
                        ("Низкий", "Низкий"),
                        ("Обычный", "Обычный"),
                        ("Высокий", "Высокий")
                    };
                    current = rule.GpuPriority;
                    return true;

                case "RuleEfficiencyMode":
                    options = new List<(string, string)>
                    {
                        ("Авто", "Авто"),
                        ("Вкл", "Вкл"),
                        ("Выкл", "Выкл")
                    };
                    current = rule.EfficiencyMode;
                    return true;

                case "RulePerformanceMode":
                    options = new List<(string, string)>
                    {
                        ("Авто", "Авто"),
                        ("Вкл", "Вкл"),
                        ("Выкл", "Выкл")
                    };
                    current = rule.PerformanceMode;
                    return true;

                default:
                    return false;
            }
        }

        void ApplyRuleCellValue(int sourceIndex, string columnName, string value)
        {
            if (sourceIndex < 0 || sourceIndex >= rules.Rules.Count)
                return;

            var rule = rules.Rules[sourceIndex];

            switch (columnName)
            {
                case "RulePriorityClass":
                    rule.Priority = L10n.NormalizePriority(value);
                    SyncRuleDefaultSilently(rule.Process, rule.Priority);
                    break;
                case "RuleCpuMatch":
                    rule.CpuMatch = value;
                    break;
                case "RuleCpuSets":
                    rule.CpuSets = value;
                    break;
                case "RuleIoPriority":
                    rule.IoPriority = value;
                    break;
                case "RuleGpuPriority":
                    rule.GpuPriority = value;
                    break;
                case "RuleEfficiencyMode":
                    rule.EfficiencyMode = value;
                    break;
                case "RulePerformanceMode":
                    rule.PerformanceMode = value;
                    break;
                default:
                    return;
            }

            SaveRules();
            RefreshRulesGrid(sourceIndex);
        }

        void ProcessTimer_Tick(object? sender, EventArgs e)
        {
            if (isRefreshingProcesses)
                return;

            RefreshProcessGrid();
        }

        void LoadRules()
        {
            if (File.Exists(file))
                rules = JsonSerializer.Deserialize<RuleFile>(File.ReadAllText(file)) ?? new RuleFile();

            foreach (var rule in rules.Rules)
                rule.Priority = L10n.NormalizePriority(rule.Priority);

            RefreshProcessInputOptions();
        }

        void SyncDefaultsFromRulesSilently()
        {
            foreach (var rule in rules.Rules)
                SyncRuleDefaultSilently(rule.Process, rule.Priority);
        }

        void SyncRuleDefaultSilently(string processName, string priority)
        {
            if (!Enum.TryParse<ProcessPriorityClass>(priority, true, out var parsedPriority))
                return;

            _ = WindowsIntegration.SetDefaultPriority(processName, parsedPriority, out _, out _, L10n.CurrentLanguage);
        }

        void RemoveRuleDefaultSilently(string processName)
        {
            _ = WindowsIntegration.RemoveDefaultPriority(processName, out _, L10n.CurrentLanguage);
        }

        void SaveRules()
        {
            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(file, JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = true }));
        }

        void RefreshRulesGrid(int selectIndex = -1)
        {
            var filter = rulesSearch.Text.Trim();

            var filteredRows = rules.Rules
                .Select((r, i) => new RuleRow(
                    i,
                    r.Process,
                    r.CpuMatch,
                    r.CpuSets,
                    BuildPriorityClassDisplay(r),
                    r.IoPriority,
                    r.GpuPriority,
                    r.EfficiencyMode,
                    r.PerformanceMode,
                    r.Priority))
                .Where(r => string.IsNullOrWhiteSpace(filter)
                    || ContainsInvariant(r.Process, filter)
                    || ContainsInvariant(r.PriorityClass, filter)
                    || ContainsInvariant(r.CpuMatch, filter)
                    || ContainsInvariant(r.CpuSets, filter)
                    || ContainsInvariant(r.IoPriority, filter)
                    || ContainsInvariant(r.GpuPriority, filter)
                    || ContainsInvariant(r.EfficiencyMode, filter)
                    || ContainsInvariant(r.PerformanceMode, filter)
                    || ContainsInvariant(r.PriorityRaw, filter))
                .ToList();

            rulesGrid.DataSource = null;
            rulesGrid.DataSource = filteredRows;

            if (rulesGrid.Rows.Count == 0)
                return;

            var index = 0;
            if (selectIndex >= 0)
            {
                var found = filteredRows.FindIndex(r => r.SourceIndex == selectIndex);
                if (found >= 0)
                    index = found;
            }

            rulesGrid.Rows[index].Selected = true;
            rulesGrid.CurrentCell = rulesGrid.Rows[index].Cells[0];
        }

        void RefreshProcessGrid()
        {
            if (isRefreshingProcesses)
                return;

            isRefreshingProcesses = true;
            try
            {
                var filter = processSearch.Text.Trim();
                var nowUtc = DateTime.UtcNow;

                var rows = Process.GetProcesses()
                    .Select(p => CreateProcessRow(p, nowUtc))
                    .Where(r => r != null)
                    .Cast<ProcessRow>()
                    .ToList();

                if (settings.ProcessScope == MultiUserProcessScope.CurrentSessionOnly)
                    rows = rows.Where(r => r.SessionId == currentSessionId).ToList();

                latestSnapshotRows = rows;
                RefreshProcessInputOptions(rows);
                TrimRuntimeSamples(rows);
                RebuildProcessViews(rows, filter);
            }
            catch
            {
                latestSnapshotRows = new List<ProcessRow>();
                totalProcessCount = 0;
                totalApplicationCount = 0;
                runningApplicationCount = 0;
                backgroundProcessCount = 0;
                windowsProcessCount = 0;

                runningAppsGrid.DataSource = new List<ProcessViewRow>();
                allAppsGrid.DataSource = new List<ProcessViewRow>();
                backgroundGrid.DataSource = new List<ProcessViewRow>();
                windowsGrid.DataSource = new List<ProcessViewRow>();

                RefreshProcessInputOptions(new List<ProcessRow>());

                processHeatScales.Remove(runningAppsGrid);
                processHeatScales.Remove(allAppsGrid);
                processHeatScales.Remove(backgroundGrid);
                processHeatScales.Remove(windowsGrid);

                UpdateProcessCounterLabels();
            }
            finally
            {
                isRefreshingProcesses = false;
            }
        }

        void RebuildProcessViewsFromSnapshot()
        {
            if (latestSnapshotRows.Count == 0)
            {
                RefreshProcessGrid();
                return;
            }

            var filter = processSearch.Text.Trim();
            RebuildProcessViews(latestSnapshotRows, filter);
        }

        void RebuildProcessViews(List<ProcessRow> sourceRows, string filter)
        {
            var rows = sourceRows
                .Where(r => string.IsNullOrWhiteSpace(filter)
                    || ContainsInvariant(r.Name, filter)
                    || ContainsInvariant(r.AppDisplayName, filter)
                    || ContainsInvariant(r.ExeName, filter)
                    || ContainsInvariant(r.Path, filter)
                    || ContainsInvariant(r.Priority, filter)
                    || r.Pid.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var runningAppExeNames = rows
                .Where(r => r.IsRunningApp)
                .Select(r => r.ExeName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var runningApps = rows
                .Where(r => runningAppExeNames.Contains(r.ExeName))
                .ToList();

            var nonApplicationRows = rows
                .Where(r => !runningAppExeNames.Contains(r.ExeName))
                .ToList();

            var allApps = rows.Where(r => r.IsApplication).ToList();
            var windows = nonApplicationRows.Where(r => r.IsWindowsProcess).ToList();
            var background = nonApplicationRows.Where(r => !r.IsWindowsProcess).ToList();

            totalProcessCount = rows.Count;
            totalApplicationCount = allApps.Select(r => r.ExeName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            runningApplicationCount = runningApps.Select(r => r.ExeName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            backgroundProcessCount = background.Count;
            windowsProcessCount = windows.Count;

            TrimExpandedGroups(expandedRunningAppGroups, runningApps);
            TrimExpandedGroups(expandedAllAppGroups, allApps);
            TrimExpandedGroups(expandedBackgroundGroups, background);
            TrimExpandedGroups(expandedWindowsGroups, windows);

            var runningViewRows = BuildApplicationViewRows(runningApps, expandedRunningAppGroups, CurrentSortForGrid(runningAppsGrid));
            var allViewRows = BuildApplicationViewRows(allApps, expandedAllAppGroups, CurrentSortForGrid(allAppsGrid));
            var backgroundViewRows = BuildApplicationViewRows(background, expandedBackgroundGroups, CurrentSortForGrid(backgroundGrid));
            var windowsViewRows = BuildApplicationViewRows(windows, expandedWindowsGroups, CurrentSortForGrid(windowsGrid));

            runningAppsGrid.DataSource = runningViewRows;
            allAppsGrid.DataSource = allViewRows;
            backgroundGrid.DataSource = backgroundViewRows;
            windowsGrid.DataSource = windowsViewRows;

            UpdateHeatScale(runningAppsGrid, runningViewRows);
            UpdateHeatScale(allAppsGrid, allViewRows);
            UpdateHeatScale(backgroundGrid, backgroundViewRows);
            UpdateHeatScale(windowsGrid, windowsViewRows);

            UpdateProcessCounterLabels();
        }

        void TrimRuntimeSamples(List<ProcessRow> rows)
        {
            var alivePids = rows.Select(r => r.Pid).ToHashSet();
            foreach (var stalePid in previousRuntimeSamples.Keys.Where(pid => !alivePids.Contains(pid)).ToList())
                previousRuntimeSamples.Remove(stalePid);
        }

        void TrimExpandedGroups(HashSet<string> expandedGroups, List<ProcessRow> rows)
        {
            var existing = rows.Select(r => r.ExeName).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
            expandedGroups.RemoveWhere(key => !existing.Contains(key));
        }

        List<ProcessViewRow> BuildApplicationViewRows(List<ProcessRow> rows, HashSet<string> expandedGroups, ProcessSortColumn sortColumn)
        {
            var result = new List<ProcessViewRow>();
            var groupedRows = rows.GroupBy(r => r.ExeName, StringComparer.OrdinalIgnoreCase);
            var orderedGroups = OrderGroups(groupedRows, sortColumn);

            foreach (var group in orderedGroups)
            {
                var groupRows = OrderGroupItems(group.ToList(), sortColumn);
                var singleProcessGroup = groupRows.Count == 1;

                var groupKey = group.Key;
                var expanded = expandedGroups.Contains(groupKey);
                var first = groupRows[0];
                var explorerWindows = GetExplorerWindows(groupRows);
                var groupDisplayCount = explorerWindows.Count > 0 ? explorerWindows.Count : groupRows.Count;
                var groupName = $"{(expanded ? "▼" : "▶")} {first.AppDisplayName} ({groupDisplayCount})";
                var groupCpu = groupRows.Sum(r => r.CpuPercent);
                var groupMemory = groupRows.Sum(r => r.MemoryMB);
                var groupDisk = groupRows.Sum(r => r.DiskMBps);
                var groupNetwork = groupRows.Sum(r => r.NetworkMbps);
                var groupThreads = groupRows.Sum(r => r.Threads);

                result.Add(new ProcessViewRow(
                    first.Icon,
                    groupName,
                    string.Empty,
                    string.Empty,
                    FormatCpu(groupCpu),
                    groupMemory.ToString(),
                    FormatDisk(groupDisk),
                    FormatNetwork(groupNetwork),
                    groupThreads.ToString(),
                    string.Empty,
                    null,
                    true,
                    groupKey));

                if (!expanded)
                    continue;

                if (explorerWindows.Count > 0)
                {
                    foreach (var explorerWindow in explorerWindows)
                    {
                        var source = groupRows.FirstOrDefault(r => r.Pid == explorerWindow.Pid) ?? first;
                        result.Add(new ProcessViewRow(
                            first.Icon,
                            $"    {explorerWindow.Title}",
                            explorerWindow.Pid.ToString(),
                            source.Priority,
                            singleProcessGroup ? string.Empty : FormatCpu(source.CpuPercent),
                            singleProcessGroup ? string.Empty : source.MemoryMB.ToString(),
                            singleProcessGroup ? string.Empty : FormatDisk(source.DiskMBps),
                            singleProcessGroup ? string.Empty : FormatNetwork(source.NetworkMbps),
                            singleProcessGroup ? string.Empty : source.Threads.ToString(),
                            explorerWindow.Title,
                            source,
                            false,
                            string.Empty));
                    }

                    continue;
                }

                foreach (var process in groupRows)
                {
                    result.Add(CreateViewRow(process, $"    {process.AppDisplayName}", first.Icon, singleProcessGroup));
                }
            }

            return result;
        }

        ProcessViewRow CreateViewRow(ProcessRow row, string? nameOverride = null, Image? iconOverride = null, bool suppressResourceMetrics = false)
            => new(
                iconOverride ?? row.Icon,
                nameOverride ?? row.Name,
                row.Pid.ToString(),
                row.Priority,
            suppressResourceMetrics ? string.Empty : FormatCpu(row.CpuPercent),
            suppressResourceMetrics ? string.Empty : row.MemoryMB.ToString(),
            suppressResourceMetrics ? string.Empty : FormatDisk(row.DiskMBps),
            suppressResourceMetrics ? string.Empty : FormatNetwork(row.NetworkMbps),
            suppressResourceMetrics ? string.Empty : row.Threads.ToString(),
                row.Path,
                row,
                false,
                string.Empty);

        static string FormatCpu(double value)
        {
            var normalized = double.IsNaN(value) || double.IsInfinity(value)
                ? 0
                : Math.Max(0, value);
            return normalized.ToString("0.0");
        }

        static string FormatDisk(double mbPerSecond)
        {
            var normalized = double.IsNaN(mbPerSecond) || double.IsInfinity(mbPerSecond)
                ? 0
                : Math.Max(0, mbPerSecond);
            return normalized.ToString("0.00");
        }

        static string FormatNetwork(double mbps)
        {
            var normalized = double.IsNaN(mbps) || double.IsInfinity(mbps)
                ? 0
                : Math.Max(0, mbps);
            return normalized.ToString("0.00");
        }

        ProcessSortColumn CurrentSortForGrid(DataGridView grid)
            => processSortColumns.TryGetValue(grid, out var sortColumn)
                ? sortColumn
                : ProcessSortColumn.Default;

        static ProcessSortColumn MapSortColumn(string columnName)
            => columnName switch
            {
                "Name" => ProcessSortColumn.Name,
                "Pid" => ProcessSortColumn.Pid,
                "Priority" => ProcessSortColumn.Priority,
                "CpuPercent" => ProcessSortColumn.Cpu,
                "MemoryMB" => ProcessSortColumn.Memory,
                "DiskRate" => ProcessSortColumn.Disk,
                "NetworkRate" => ProcessSortColumn.Network,
                "Threads" => ProcessSortColumn.Threads,
                "Path" => ProcessSortColumn.Path,
                _ => ProcessSortColumn.Default
            };

        static IEnumerable<IGrouping<string, ProcessRow>> OrderGroups(IEnumerable<IGrouping<string, ProcessRow>> groups, ProcessSortColumn sortColumn)
            => sortColumn switch
            {
                ProcessSortColumn.Name => groups.OrderBy(g => g.First().AppDisplayName, StringComparer.OrdinalIgnoreCase),
                ProcessSortColumn.Pid => groups.OrderBy(g => g.Min(r => r.Pid)),
                ProcessSortColumn.Priority => groups.OrderBy(g => g.First().PriorityRaw, StringComparer.OrdinalIgnoreCase),
                ProcessSortColumn.Cpu => groups.OrderByDescending(g => g.Sum(r => r.CpuPercent)),
                ProcessSortColumn.Memory => groups.OrderByDescending(g => g.Sum(r => r.MemoryMB)),
                ProcessSortColumn.Disk => groups.OrderByDescending(g => g.Sum(r => r.DiskMBps)),
                ProcessSortColumn.Network => groups.OrderByDescending(g => g.Sum(r => r.NetworkMbps)),
                ProcessSortColumn.Threads => groups.OrderByDescending(g => g.Sum(r => r.Threads)),
                ProcessSortColumn.Path => groups.OrderBy(g => g.First().Path, StringComparer.OrdinalIgnoreCase),
                _ => groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            };

        static List<ProcessRow> OrderGroupItems(List<ProcessRow> groupRows, ProcessSortColumn sortColumn)
        {
            IOrderedEnumerable<ProcessRow> ordered = sortColumn switch
            {
                ProcessSortColumn.Name => groupRows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
                ProcessSortColumn.Pid => groupRows.OrderBy(r => r.Pid),
                ProcessSortColumn.Priority => groupRows.OrderBy(r => r.PriorityRaw, StringComparer.OrdinalIgnoreCase),
                ProcessSortColumn.Cpu => groupRows.OrderByDescending(r => r.CpuPercent),
                ProcessSortColumn.Memory => groupRows.OrderByDescending(r => r.MemoryMB),
                ProcessSortColumn.Disk => groupRows.OrderByDescending(r => r.DiskMBps),
                ProcessSortColumn.Network => groupRows.OrderByDescending(r => r.NetworkMbps),
                ProcessSortColumn.Threads => groupRows.OrderByDescending(r => r.Threads),
                ProcessSortColumn.Path => groupRows.OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase),
                _ => groupRows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            };

            return ordered.ThenBy(r => r.Pid).ToList();
        }

        List<ExplorerWindowItem> GetExplorerWindows(List<ProcessRow> groupRows)
        {
            if (groupRows.Count == 0)
                return new List<ExplorerWindowItem>();

            var first = groupRows[0];
            if (!first.ExeName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                return new List<ExplorerWindowItem>();

            var pidSet = groupRows
                .Select(r => r.Pid)
                .Distinct()
                .ToHashSet();

            return EnumerateExplorerWindows(pidSet);
        }

        List<ExplorerWindowItem> EnumerateExplorerWindows(HashSet<int> pidSet)
        {
            var windows = new List<ExplorerWindowItem>();

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd) || IsWindowCloaked(hWnd))
                    return true;

                if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
                    return true;

                var className = GetWindowClassName(hWnd);
                if (!className.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase)
                    && !className.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase))
                    return true;

                GetWindowThreadProcessId(hWnd, out var pidRaw);
                var pid = unchecked((int)pidRaw);
                if (pidSet.Contains(pid))
                {
                    var title = GetWindowCaption(hWnd);
                    if (!string.IsNullOrWhiteSpace(title))
                        windows.Add(new ExplorerWindowItem(pid, title));
                }

                return true;
            }, IntPtr.Zero);

            return windows
                .GroupBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static string GetWindowClassName(IntPtr hWnd)
        {
            var buffer = new System.Text.StringBuilder(256);
            var length = GetClassName(hWnd, buffer, buffer.Capacity);
            return length > 0 ? buffer.ToString() : string.Empty;
        }

        static string GetWindowCaption(IntPtr hWnd)
        {
            var length = GetWindowTextLength(hWnd);
            if (length <= 0)
                return string.Empty;

            var buffer = new System.Text.StringBuilder(length + 1);
            _ = GetWindowText(hWnd, buffer, buffer.Capacity);
            return buffer.ToString().Trim();
        }

        void ToggleApplicationGroup(DataGridView grid, string groupKey)
        {
            var groupState = GetExpandedGroupState(grid);
            if (groupState == null)
                return;

            if (groupState.Contains(groupKey))
                groupState.Remove(groupKey);
            else
                groupState.Add(groupKey);

            RebuildProcessViewsFromSnapshot();
        }

        HashSet<string>? GetExpandedGroupState(DataGridView grid)
        {
            if (ReferenceEquals(grid, runningAppsGrid))
                return expandedRunningAppGroups;
            if (ReferenceEquals(grid, allAppsGrid))
                return expandedAllAppGroups;
            if (ReferenceEquals(grid, backgroundGrid))
                return expandedBackgroundGroups;
            if (ReferenceEquals(grid, windowsGrid))
                return expandedWindowsGroups;

            return null;
        }

        ProcessRow? CreateProcessRow(Process p, DateTime nowUtc)
        {
            try
            {
                string path;
                try
                {
                    path = p.MainModule?.FileName ?? string.Empty;
                }
                catch
                {
                    path = string.Empty;
                }

                string priority;
                try
                {
                    priority = p.PriorityClass.ToString();
                }
                catch
                {
                    priority = "N/A";
                }

                long memoryMb;
                try
                {
                    memoryMb = p.WorkingSet64 / (1024 * 1024);
                }
                catch
                {
                    memoryMb = 0;
                }

                var cpuPercent = 0d;
                var diskMbPerSecond = 0d;
                var networkMbps = 0d;
                var runtimeSample = TryCaptureRuntimeSample(p, nowUtc);
                if (runtimeSample != null)
                {
                    if (previousRuntimeSamples.TryGetValue(p.Id, out var previous))
                    {
                        var elapsedSeconds = (runtimeSample.Value.TimestampUtc - previous.TimestampUtc).TotalSeconds;
                        if (elapsedSeconds > 0.1)
                        {
                            var cpuDeltaMs = (runtimeSample.Value.TotalProcessorTime - previous.TotalProcessorTime).TotalMilliseconds;
                            cpuPercent = (cpuDeltaMs / (elapsedSeconds * 1000d * Environment.ProcessorCount)) * 100d;

                            var diskBytesDelta = ((double)runtimeSample.Value.ReadBytes - previous.ReadBytes)
                                + ((double)runtimeSample.Value.WriteBytes - previous.WriteBytes);
                            var diskBytesPerSecond = diskBytesDelta / elapsedSeconds;
                            diskMbPerSecond = diskBytesPerSecond / (1024d * 1024d);

                            var otherBytesDelta = (double)runtimeSample.Value.OtherBytes - previous.OtherBytes;
                            var networkBitsPerSecond = (otherBytesDelta / elapsedSeconds) * 8d;
                            networkMbps = networkBitsPerSecond / (1024d * 1024d);
                        }
                    }

                    previousRuntimeSamples[p.Id] = runtimeSample.Value;
                }

                string mainWindowTitle;
                try
                {
                    mainWindowTitle = p.MainWindowTitle;
                }
                catch
                {
                    mainWindowTitle = string.Empty;
                }

                var hasMainWindow = !string.IsNullOrWhiteSpace(mainWindowTitle);
                hasMainWindow = IsUserVisibleMainWindow(p) && hasMainWindow;
                var isKnownApplication = IsKnownApplicationProcess(p.ProcessName, path);
                var metadata = ResolveFileMetadata(path);
                var isWindowsProcess = DetermineIsWindowsProcess(p, path, hasMainWindow, metadata.CompanyName);
                var appDisplayName = ResolveAppDisplayName(p.ProcessName, metadata);
                var icon = ResolveProcessIcon(path, metadata);

                return new ProcessRow(
                    p.ProcessName,
                    appDisplayName,
                    icon,
                    p.ProcessName + ".exe",
                    p.Id,
                    p.SessionId,
                    L10n.PriorityDisplay(priority),
                    priority,
                    memoryMb,
                    Math.Max(0, cpuPercent),
                    Math.Max(0, diskMbPerSecond),
                    Math.Max(0, networkMbps),
                    p.Threads.Count,
                    path,
                    hasMainWindow || isKnownApplication,
                    !string.IsNullOrWhiteSpace(path) || hasMainWindow,
                    !hasMainWindow,
                    isWindowsProcess
                );
            }
            catch
            {
                return null;
            }
        }

        static ProcessRuntimeSample? TryCaptureRuntimeSample(Process process, DateTime nowUtc)
        {
            try
            {
                var totalProcessorTime = process.TotalProcessorTime;
                ulong readBytes = 0;
                ulong writeBytes = 0;
                ulong otherBytes = 0;

                try
                {
                    if (GetProcessIoCounters(process.Handle, out var ioCounters))
                    {
                        readBytes = ioCounters.ReadTransferCount;
                        writeBytes = ioCounters.WriteTransferCount;
                        otherBytes = ioCounters.OtherTransferCount;
                    }
                }
                catch
                {
                }

                return new ProcessRuntimeSample(nowUtc, totalProcessorTime, readBytes, writeBytes, otherBytes);
            }
            catch
            {
                return null;
            }
        }

        static System.Drawing.Color ResolveGroupColor(string groupKey, string fallbackKey)
        {
            var key = string.IsNullOrWhiteSpace(groupKey) ? fallbackKey : groupKey;
            if (string.IsNullOrWhiteSpace(key))
                return System.Drawing.Color.White;

            var index = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(key)) % GroupRowPalette.Length;
            return GroupRowPalette[index];
        }

        static System.Drawing.Color DarkenColor(System.Drawing.Color color, float factor)
        {
            var r = Math.Clamp((int)(color.R * factor), 0, 255);
            var g = Math.Clamp((int)(color.G * factor), 0, 255);
            var b = Math.Clamp((int)(color.B * factor), 0, 255);
            return System.Drawing.Color.FromArgb(r, g, b);
        }

        void UpdateHeatScale(DataGridView grid, List<ProcessViewRow> rows)
        {
            var maxCpu = rows.Select(GetCpuValue).DefaultIfEmpty(0d).Max();
            var maxMemory = rows.Select(GetMemoryValue).DefaultIfEmpty(0d).Max();
            processHeatScales[grid] = new ProcessHeatScale(maxCpu, maxMemory);
        }

        double GetHeatScore(DataGridView grid, ProcessViewRow row)
        {
            if (!processHeatScales.TryGetValue(grid, out var scale))
                return 0;

            var cpu = GetCpuValue(row);
            var memory = GetMemoryValue(row);

            var cpuRatio = scale.MaxCpu > 0 ? cpu / scale.MaxCpu : 0;
            var memoryRatio = scale.MaxMemory > 0 ? memory / scale.MaxMemory : 0;
            var ratio = Math.Clamp(Math.Max(cpuRatio, memoryRatio), 0, 1);

            if (ratio < 0.20)
                return 0;

            return Math.Clamp((ratio - 0.20) / 0.80, 0, 1);
        }

        static double GetCpuValue(ProcessViewRow row)
        {
            if (row.Source != null)
                return Math.Max(0, row.Source.CpuPercent);

            return ParseDoubleOrZero(row.CpuPercent);
        }

        static double GetMemoryValue(ProcessViewRow row)
        {
            if (row.Source != null)
                return Math.Max(0, row.Source.MemoryMB);

            return ParseDoubleOrZero(row.MemoryMB);
        }

        static double ParseDoubleOrZero(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsedCurrent))
                return parsedCurrent;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedInvariant))
                return parsedInvariant;

            return 0;
        }

        static System.Drawing.Color ApplyHeatTint(System.Drawing.Color baseColor, double heatScore)
        {
            if (heatScore <= 0)
                return baseColor;

            var warmColor = heatScore switch
            {
                < 0.33 => System.Drawing.Color.FromArgb(255, 214, 128),
                < 0.66 => System.Drawing.Color.FromArgb(255, 158, 92),
                _ => System.Drawing.Color.FromArgb(255, 96, 96)
            };

            var alpha = 0.58 + (0.36 * heatScore);
            return BlendColors(baseColor, warmColor, alpha);
        }

        static System.Drawing.Color BlendColors(System.Drawing.Color from, System.Drawing.Color to, double alpha)
        {
            var clamped = Math.Clamp(alpha, 0, 1);
            var r = (int)(from.R + ((to.R - from.R) * clamped));
            var g = (int)(from.G + ((to.G - from.G) * clamped));
            var b = (int)(from.B + ((to.B - from.B) * clamped));
            return System.Drawing.Color.FromArgb(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
        }

        static bool DetermineIsWindowsProcess(Process process, string path, bool hasMainWindow, string companyName)
        {
            if (hasMainWindow)
                return false;

            if (process.Id <= 4)
                return true;

            string processName;
            try
            {
                processName = process.ProcessName;
            }
            catch
            {
                processName = string.Empty;
            }

            if (WindowsCoreProcessNames.Contains(processName))
                return true;

            var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var isWindowsPath = !string.IsNullOrWhiteSpace(path)
                && !string.IsNullOrWhiteSpace(windowsDir)
                && path.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase);

            if (!isWindowsPath)
                return false;

            var isMicrosoftCompany = companyName.Contains("microsoft", StringComparison.OrdinalIgnoreCase)
                || companyName.Contains("windows", StringComparison.OrdinalIgnoreCase);
            if (!isMicrosoftCompany)
                return false;

            try
            {
                if (process.SessionId == 0)
                    return true;
            }
            catch
            {
            }

            if (path.IndexOf("\\SystemApps\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (path.IndexOf("\\System32\\", StringComparison.OrdinalIgnoreCase) >= 0
                && processName.EndsWith("host", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        FileMetadata ResolveFileMetadata(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new FileMetadata(string.Empty, string.Empty, string.Empty, defaultProcessIcon);

            if (fileMetadataCache.TryGetValue(path, out var cached))
                return cached;

            var description = string.Empty;
            var product = string.Empty;
            var company = string.Empty;
            Image icon = defaultProcessIcon;

            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                description = (info.FileDescription ?? string.Empty).Trim();
                product = (info.ProductName ?? string.Empty).Trim();
                company = (info.CompanyName ?? string.Empty).Trim();
            }
            catch
            {
            }

            try
            {
                using var extracted = Icon.ExtractAssociatedIcon(path);
                if (extracted != null)
                    icon = extracted.ToBitmap();
            }
            catch
            {
            }

            var metadata = new FileMetadata(description, product, company, icon);
            fileMetadataCache[path] = metadata;
            return metadata;
        }

        Image ResolveProcessIcon(string path, FileMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return defaultProcessIcon;

            return metadata.Icon;
        }

        static string ResolveAppDisplayName(string processName, FileMetadata metadata)
        {
            var fallback = HumanizeProcessName(processName);
            var description = metadata.Description;
            var product = metadata.Product;

            if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(product))
                return fallback;

            if (string.IsNullOrWhiteSpace(description))
                return product;

            if (string.IsNullOrWhiteSpace(product))
                return description;

            if (product.Contains("Developer Edition", StringComparison.OrdinalIgnoreCase)
                && !description.Contains("Developer Edition", StringComparison.OrdinalIgnoreCase))
                return product;

            if (description.Equals(processName, StringComparison.OrdinalIgnoreCase)
                || description.Equals(processName + ".exe", StringComparison.OrdinalIgnoreCase))
                return product;

            return description;
        }

        static string HumanizeProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return processName;

            var spaced = Regex.Replace(processName, "([a-z0-9])([A-Z])", "$1 $2");
            return spaced.Replace('_', ' ').Trim();
        }

        static bool IsUserVisibleMainWindow(Process process)
        {
            try
            {
                var handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                    return false;

                if (!IsWindowVisible(handle))
                    return false;

                if (IsWindowCloaked(handle))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool IsKnownApplicationProcess(string processName, string path)
        {
            if (string.Equals(processName, "explorer", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(path) && path.EndsWith("\\explorer.exe", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        static bool IsWindowCloaked(IntPtr hWnd)
        {
            try
            {
                var result = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
                return result == 0 && cloaked != 0;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetProcessIoCounters(IntPtr hProcess, out IoCounters lpIoCounters);

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        const uint GW_OWNER = 4;

        const int DWMWA_CLOAKED = 14;

        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        static readonly HashSet<string> WindowsCoreProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "System",
            "Registry",
            "Idle",
            "smss",
            "csrss",
            "wininit",
            "winlogon",
            "services",
            "lsass",
            "dwm",
            "sihost",
            "fontdrvhost",
            "spoolsv",
            "audiodg",
            "taskhostw",
            "ctfmon",
            "runtimebroker",
            "conhost",
            "wudfhost",
            "SearchIndexer",
            "WmiPrvSE",
            "svchost",
            "SecurityHealthService"
        };

        ProcessRow? SelectedProcessRow()
        {
            var grid = ResolveActiveProcessGrid();
            if (grid == null || grid.IsDisposed)
                return null;

            if (grid.CurrentRow?.DataBoundItem is ProcessViewRow row)
                return row.Source;

            return null;
        }

        int SelectedRuleIndex()
        {
            if (rulesGrid.CurrentRow?.DataBoundItem is not RuleRow row)
                return -1;

            return row.SourceIndex;
        }

        static bool ContainsInvariant(string source, string value)
            => source?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        bool TryReadRuleInputs(out string processName, out string priority)
        {
            processName = processInput.SelectedItem is ProcessInputOption selected
                ? selected.ExeName
                : processInput.Text.Trim();
            priority = SelectedPriorityCanonical();

            processName = NormalizeProcessInput(processName);

            if (string.IsNullOrWhiteSpace(processName))
                return false;

            return !string.IsNullOrWhiteSpace(priority);
        }

        static string NormalizeProcessInput(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().Trim('"');
            normalized = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            if (!normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                normalized += ".exe";

            return normalized;
        }

        void RefreshProcessInputOptions(List<ProcessRow>? currentRows = null)
        {
            var previousText = processInput.Text;
            var processNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (currentRows != null)
            {
                foreach (var row in currentRows)
                    processNames.Add(row.ExeName);
            }

            foreach (var rule in rules.Rules)
            {
                var normalized = NormalizeProcessInput(rule.Process);
                if (!string.IsNullOrWhiteSpace(normalized))
                    processNames.Add(normalized);
            }

            var options = processNames
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => new ProcessInputOption(n))
                .Cast<object>()
                .ToArray();

            processInput.BeginUpdate();
            processInput.Items.Clear();
            processInput.Items.AddRange(options);
            processInput.EndUpdate();

            if (!string.IsNullOrWhiteSpace(previousText))
                processInput.Text = previousText;
        }

        string BuildApplySummary(IEnumerable<PriorityEngine.ApplyResult> results)
        {
            var list = results.ToList();
            var matched = list.Sum(r => r.MatchedCount);
            var applied = list.Sum(r => r.AppliedCount);
            var failed = list.Sum(r => r.ErrorMessages.Count);

            var header = L10n.Text(
                $"Применение завершено. Найдено: {matched}, применено: {applied}, ошибок: {failed}.",
                $"Застосування завершено. Знайдено: {matched}, застосовано: {applied}, помилок: {failed}.",
                $"Apply completed. Matched: {matched}, applied: {applied}, errors: {failed}.");

            if (failed == 0)
                return header;

            var topErrors = list
                .SelectMany(r => r.ErrorMessages.Select(e => $"{r.ProcessName}: {e}"))
                .Take(5)
                .ToList();

            return header + Environment.NewLine + string.Join(Environment.NewLine, topErrors);
        }

        void PopulatePriorityInput(string? canonicalSelected = null)
        {
            var current = canonicalSelected ?? SelectedPriorityCanonical();
            priorityInput.BeginUpdate();
            priorityInput.Items.Clear();

            foreach (var name in Enum.GetNames(typeof(ProcessPriorityClass)))
                priorityInput.Items.Add(new PriorityOption(name, L10n.PriorityDisplay(name)));

            priorityInput.EndUpdate();
            SetPriorityInputSelection(string.IsNullOrWhiteSpace(current) ? "Normal" : current);
        }

        void SetPriorityInputSelection(string canonicalPriority)
        {
            var canonical = L10n.NormalizePriority(canonicalPriority);
            for (var i = 0; i < priorityInput.Items.Count; i++)
            {
                if (priorityInput.Items[i] is PriorityOption option && option.Value == canonical)
                {
                    priorityInput.SelectedIndex = i;
                    return;
                }
            }

            if (priorityInput.Items.Count > 0)
                priorityInput.SelectedIndex = 0;
        }

        string SelectedPriorityCanonical()
        {
            if (priorityInput.SelectedItem is PriorityOption option)
                return option.Value;

            return string.Empty;
        }

        void SetCurrentPriorityForSelected(string priorityText)
        {
            var row = SelectedProcessRow();
            if (row == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            if (!Enum.TryParse<ProcessPriorityClass>(priorityText, true, out var priority))
            {
                MessageBox.Show(L10n.MsgSelectPriority, L10n.AppTitle);
                return;
            }

            if (!PriorityEngine.TryApplyToPid(row.Pid, priority, out var error))
            {
                MessageBox.Show(L10n.MsgProcessActionFailed(error), L10n.AppTitle);
                return;
            }

            RefreshProcessGrid();
        }

        void SaveDefaultPriorityForSelected(string priorityText)
        {
            var row = SelectedProcessRow();
            if (row == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            if (!Enum.TryParse<ProcessPriorityClass>(priorityText, true, out var priority))
            {
                MessageBox.Show(L10n.MsgSelectPriority, L10n.AppTitle);
                return;
            }

            if (WindowsIntegration.SetDefaultPriority(row.ExeName, priority, out var scope, out var error, L10n.CurrentLanguage))
                MessageBox.Show(L10n.MsgSavedFor(row.ExeName, L10n.PriorityDisplay(priority.ToString()), scope), L10n.AppTitle);
            else
                MessageBox.Show(L10n.MsgSaveFailed(error), L10n.AppTitle);
        }

        void RemoveSavedPriorityForSelected()
        {
            var row = SelectedProcessRow();
            if (row == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            if (WindowsIntegration.RemoveDefaultPriority(row.ExeName, out var error, L10n.CurrentLanguage))
                MessageBox.Show(L10n.MsgRemovedCli(row.ExeName), L10n.AppTitle);
            else
                MessageBox.Show(L10n.MsgRemoveFailedCli(error), L10n.AppTitle);
        }

        void SetAffinityAllForSelected()
        {
            var row = SelectedProcessRow();
            if (row == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            var cpuCount = Math.Min(Environment.ProcessorCount, 63);
            long mask = 0;
            for (var i = 0; i < cpuCount; i++)
                mask |= 1L << i;

            if (!PriorityEngine.TrySetAffinity(row.Pid, mask, out var error))
                MessageBox.Show(L10n.MsgProcessActionFailed(error), L10n.AppTitle);
        }

        void SetAffinitySingleCpuForSelected(int cpuIndex)
        {
            var row = SelectedProcessRow();
            if (row == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            var mask = 1L << cpuIndex;
            if (!PriorityEngine.TrySetAffinity(row.Pid, mask, out var error))
                MessageBox.Show(L10n.MsgProcessActionFailed(error), L10n.AppTitle);
        }

        void AddRuleFromSelectedProcess()
        {
            if (!TryGetSelectedProcessForRule(out var processExe, out var priorityRaw))
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            var priority = Enum.GetNames(typeof(ProcessPriorityClass)).Contains(priorityRaw)
                ? priorityRaw
                : "Normal";

            rules.Rules.Add(new Rule { Process = processExe, Priority = priority });
            SaveRules();
            RefreshRulesGrid(rules.Rules.Count - 1);
            tabs.SelectedTab = rulesTab;
            MessageBox.Show(L10n.MsgRuleAdded, L10n.AppTitle);
        }

        bool TryGetSelectedProcessForRule(out string processExe, out string priorityRaw)
        {
            processExe = string.Empty;
            priorityRaw = "Normal";

            var grid = currentProcessGrid;
            if (grid == null || grid.IsDisposed)
                grid = runningAppsGrid;

            if (grid.CurrentRow?.DataBoundItem is not ProcessViewRow viewRow)
                return false;

            if (viewRow.Source != null)
            {
                processExe = viewRow.Source.ExeName;
                priorityRaw = viewRow.Source.PriorityRaw;
                return !string.IsNullOrWhiteSpace(processExe);
            }

            if (!viewRow.IsGroup || string.IsNullOrWhiteSpace(viewRow.GroupKey))
                return false;

            processExe = viewRow.GroupKey.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? viewRow.GroupKey
                : viewRow.GroupKey + ".exe";

            if (grid.DataSource is IEnumerable<ProcessViewRow> rows)
            {
                var sample = rows.FirstOrDefault(r => r.Source != null && string.Equals(r.GroupKey, viewRow.GroupKey, StringComparison.OrdinalIgnoreCase));
                if (sample?.Source != null)
                    priorityRaw = sample.Source.PriorityRaw;
            }

            return true;
        }

        void RestartSelectedProcess()
        {
            var viewRow = SelectedProcessViewRow();
            if (viewRow == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            if (IsExplorerViewRow(viewRow))
            {
                if (!TryRestartExplorerShell(out var explorerError))
                    MessageBox.Show(L10n.MsgProcessActionFailed(explorerError), L10n.AppTitle);

                RefreshProcessGrid();
                return;
            }

            var row = viewRow.Source;
            if (row == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            if (string.IsNullOrWhiteSpace(row.Path) || !File.Exists(row.Path))
            {
                MessageBox.Show(L10n.MsgPathUnavailable, L10n.AppTitle);
                return;
            }

            try
            {
                using var process = Process.GetProcessById(row.Pid);
                process.Kill(true);
                process.WaitForExit(3000);
                Process.Start(new ProcessStartInfo(row.Path) { UseShellExecute = true });
                RefreshProcessGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(L10n.MsgProcessActionFailed(ex.Message), L10n.AppTitle);
            }
        }

        ProcessViewRow? SelectedProcessViewRow()
        {
            var grid = ResolveActiveProcessGrid();
            if (grid == null || grid.IsDisposed)
                return null;

            return grid.CurrentRow?.DataBoundItem as ProcessViewRow;
        }

        static bool IsExplorerViewRow(ProcessViewRow viewRow)
        {
            if (viewRow.Source?.ExeName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) == true)
                return true;

            return viewRow.IsGroup
                && viewRow.GroupKey.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase);
        }

        static bool TryRestartExplorerShell(out string error)
        {
            error = string.Empty;
            try
            {
                var kill = new ProcessStartInfo("taskkill.exe", "/F /IM explorer.exe")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var killProcess = Process.Start(kill);
                if (killProcess == null)
                {
                    error = "Failed to start taskkill.";
                    return false;
                }

                killProcess.WaitForExit(10000);
                if (!killProcess.HasExited)
                {
                    error = "Explorer stop timeout.";
                    return false;
                }

                if (killProcess.ExitCode != 0 && killProcess.ExitCode != 128)
                {
                    error = $"taskkill exit code: {killProcess.ExitCode}";
                    return false;
                }

                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        void KillSelectedProcess()
        {
            var row = SelectedProcessRow();
            if (row == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            try
            {
                using var process = Process.GetProcessById(row.Pid);
                process.Kill(true);
                process.WaitForExit(3000);
                RefreshProcessGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(L10n.MsgProcessActionFailed(ex.Message), L10n.AppTitle);
            }
        }

        void OpenLocationForSelected()
        {
            var row = SelectedProcessRow();
            if (row == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            if (string.IsNullOrWhiteSpace(row.Path) || !File.Exists(row.Path))
            {
                MessageBox.Show(L10n.MsgPathUnavailable, L10n.AppTitle);
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{row.Path}\"") { UseShellExecute = true });
        }

        void CopyNameForSelected()
        {
            var row = SelectedProcessRow();
            if (row == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            Clipboard.SetText(row.ExeName);
        }

        void CopyPathForSelected()
        {
            var row = SelectedProcessRow();
            if (row == null)
            {
                MessageBox.Show(L10n.MsgNoProcessSelected, L10n.AppTitle);
                return;
            }

            if (string.IsNullOrWhiteSpace(row.Path))
            {
                MessageBox.Show(L10n.MsgPathUnavailable, L10n.AppTitle);
                return;
            }

            Clipboard.SetText(row.Path);
        }

        void ApplySettingsRuntime()
        {
            var interval = settings.ProcessRefreshSeconds;
            if (interval < 2)
                interval = 2;
            if (interval > 60)
                interval = 60;

            settings.ProcessRefreshSeconds = interval;
            processTimer.Interval = interval * 1000;

            if (settings.AutoRefreshProcesses)
                processTimer.Start();
            else
                processTimer.Stop();
        }

        void ApplyLocalization()
        {
            Text = L10n.AppTitle;

            settingsButton.Text = $"{L10n.SettingsButton} {L10n.Settings}";
            settingsButton.AccessibleName = L10n.Settings;
            helpButton.Text = L10n.HelpMenu;
            donateQuickButton.Text = L10n.DonateQuick;

            if (helpMenu.Items.Count >= 4)
            {
                helpMenu.Items[0].Text = L10n.HelpInstructionItem;
                helpMenu.Items[1].Text = L10n.HelpVisitSiteItem;
                helpMenu.Items[2].Text = L10n.HelpAboutItem;
                helpMenu.Items[3].Text = L10n.HelpDonateItem;
            }

            processesTab.Text = L10n.ProcessesTab;
            rulesTab.Text = L10n.RulesTab;
            UpdateProcessCounterLabels();
            processSearch.PlaceholderText = L10n.SearchProcessesPlaceholder;
            refreshProcesses.Text = L10n.RefreshProcesses;
            openTaskManager.Text = L10n.OpenTaskManager;

            ApplyProcessGridHeaders(runningAppsGrid);
            ApplyProcessGridHeaders(allAppsGrid);
            ApplyProcessGridHeaders(backgroundGrid);
            ApplyProcessGridHeaders(windowsGrid);

            ruleEditorLabel.Text = L10n.RuleEditorTitle;
            PopulatePriorityInput();
            addRule.Text = L10n.AddRule;
            updateRule.Text = L10n.UpdateRule;
            deleteRule.Text = L10n.DeleteRule;
            duplicateRule.Text = L10n.DuplicateRule;
            moveUpRule.Text = L10n.MoveUpRule;
            moveDownRule.Text = L10n.MoveDownRule;
            applySelectedRule.Text = L10n.ApplySelectedRule;
            applyAllRules.Text = L10n.ApplyAllRules;
            deleteAllRules.Text = L10n.DeleteAllRules;
            exportRules.Text = L10n.ExportRules;
            importRules.Text = L10n.ImportRules;
            rulesSearch.PlaceholderText = L10n.SearchRulesPlaceholder;

            UpdateSearchVisualState(processSearch);
            UpdateSearchVisualState(rulesSearch);
            UpdateSearchLayout();

            rulesGrid.Columns["RuleProcess"].HeaderText = L10n.RuleColumnProcess;
            rulesGrid.Columns["RuleCpuMatch"].HeaderText = L10n.RuleColumnCpuMatch;
            rulesGrid.Columns["RuleCpuSets"].HeaderText = L10n.RuleColumnCpuSets;
            rulesGrid.Columns["RulePriorityClass"].HeaderText = L10n.RuleColumnPriorityClass;
            rulesGrid.Columns["RuleIoPriority"].HeaderText = L10n.RuleColumnIoPriority;
            rulesGrid.Columns["RuleGpuPriority"].HeaderText = L10n.RuleColumnGpuPriority;
            rulesGrid.Columns["RuleEfficiencyMode"].HeaderText = L10n.RuleColumnEfficiencyMode;
            rulesGrid.Columns["RulePerformanceMode"].HeaderText = L10n.RuleColumnPerformanceMode;
        }

        string BuildPriorityClassDisplay(Rule rule)
        {
            if (WindowsIntegration.TryGetSavedPriority(rule.Process, out var savedPriority, out var scope))
                return $"{L10n.PriorityDisplay(savedPriority.ToString())} (реестр: {scope})";

            return $"{L10n.PriorityDisplay(rule.Priority)} (правило)";
        }

        void UpdateProcessCounterLabels()
        {
            runningLabel.Text = L10n.RunningProcessesWithCount(totalProcessCount);
            processApplicationsTab.Text = L10n.SectionWithCount(L10n.ProcessSectionApplications, runningApplicationCount);
            processBackgroundTab.Text = L10n.SectionWithCount(L10n.ProcessSectionBackground, backgroundProcessCount);
            processWindowsTab.Text = L10n.SectionWithCount(L10n.ProcessSectionWindows, windowsProcessCount);
            allAppsLabel.Text = L10n.SectionWithCount(L10n.AllAppsLabel, totalApplicationCount);
            UpdateSearchLayout();
        }

        void ApplyProcessGridHeaders(DataGridView grid)
        {
            grid.Columns["Icon"].HeaderText = string.Empty;
            grid.Columns["Name"].HeaderText = L10n.ProcessColumnName;
            grid.Columns["Pid"].HeaderText = L10n.ProcessColumnPid;
            grid.Columns["Priority"].HeaderText = L10n.ProcessColumnPriority;
            grid.Columns["CpuPercent"].HeaderText = L10n.ProcessColumnCpu;
            grid.Columns["MemoryMB"].HeaderText = L10n.ProcessColumnMemory;
            grid.Columns["DiskRate"].HeaderText = L10n.ProcessColumnDisk;
            grid.Columns["NetworkRate"].HeaderText = L10n.ProcessColumnNetwork;
            grid.Columns["Threads"].HeaderText = L10n.ProcessColumnThreads;
            grid.Columns["Path"].HeaderText = L10n.ProcessColumnPath;
        }

        sealed record ProcessRow(
            string Name,
            string AppDisplayName,
            Image Icon,
            string ExeName,
            int Pid,
            int SessionId,
            string Priority,
            string PriorityRaw,
            long MemoryMB,
            double CpuPercent,
            double DiskMBps,
            double NetworkMbps,
            int Threads,
            string Path,
            bool IsRunningApp,
            bool IsApplication,
            bool IsBackground,
            bool IsWindowsProcess
        );
        sealed record ProcessViewRow(
            Image Icon,
            string Name,
            string Pid,
            string Priority,
            string CpuPercent,
            string MemoryMB,
            string DiskRate,
            string NetworkRate,
            string Threads,
            string Path,
            ProcessRow? Source,
            bool IsGroup,
            string GroupKey
        );
        readonly record struct ProcessRuntimeSample(DateTime TimestampUtc, TimeSpan TotalProcessorTime, ulong ReadBytes, ulong WriteBytes, ulong OtherBytes);
        readonly record struct ProcessHeatScale(double MaxCpu, double MaxMemory);
        enum ProcessSortColumn
        {
            Default,
            Name,
            Pid,
            Priority,
            Cpu,
            Memory,
            Disk,
            Network,
            Threads,
            Path
        }
        [StructLayout(LayoutKind.Sequential)]
        struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }
        sealed record ExplorerWindowItem(int Pid, string Title);
        sealed record FileMetadata(string Description, string Product, string CompanyName, Image Icon);
        sealed record RuleRow(
            int SourceIndex,
            string Process,
            string CpuMatch,
            string CpuSets,
            string PriorityClass,
            string IoPriority,
            string GpuPriority,
            string EfficiencyMode,
            string PerformanceMode,
            string PriorityRaw);

        sealed class PriorityOption
        {
            public string Value { get; }
            public string Display { get; }

            public PriorityOption(string value, string display)
            {
                Value = value;
                Display = display;
            }

            public override string ToString() => Display;
        }

        sealed class ProcessInputOption
        {
            public string ExeName { get; }

            public ProcessInputOption(string exeName)
            {
                ExeName = exeName;
            }

            public override string ToString() => ExeName;
        }
    }
}
