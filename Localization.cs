using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace PriorityManagerX
{
    public enum AppLanguage
    {
        Russian,
        Ukrainian,
        English
    }

    public enum StartupScopeMode
    {
        Disabled,
        CurrentUser,
        AllUsers
    }

    public enum CoreEngineStartupMode
    {
        Disabled,
        WithGui,
        CurrentUser,
        AllUsers,
        ServiceLike
    }

    public enum MultiUserProcessScope
    {
        CurrentSessionOnly,
        AllAccessibleProcesses
    }

    public enum DateDisplayFormat
    {
        Europe,
        Usa,
        Asia
    }

    public enum UpdateCheckPeriod
    {
        Never,
        Hours12,
        Day1,
        Day2,
        Week1,
        Week2
    }

    public sealed class AppSettings
    {
        public string Language { get; set; } = "";
        public bool AutoApplyOnStartup { get; set; } = true;
        public bool AutoRefreshProcesses { get; set; } = true;
        public int ProcessRefreshSeconds { get; set; } = 5;
        public bool ConfirmBeforeApplyRules { get; set; } = false;
        public bool AutoStartWithWindows { get; set; } = false;
        public bool RunAsAdministrator { get; set; } = false;
        public StartupScopeMode GuiStartupMode { get; set; } = StartupScopeMode.Disabled;
        public CoreEngineStartupMode CoreEngineStartupMode { get; set; } = CoreEngineStartupMode.WithGui;
        public bool RestartCoreEngineIfStopped { get; set; } = true;
        public MultiUserProcessScope ProcessScope { get; set; } = MultiUserProcessScope.AllAccessibleProcesses;
        public bool UseSharedDataLocation { get; set; } = true;
        public string SharedConfigPath { get; set; } = "";
        public string SharedLogsPath { get; set; } = "";
        public DateDisplayFormat DateFormat { get; set; } = DateDisplayFormat.Europe;
        public bool CheckUpdatesOnStartup { get; set; } = true;
        public bool IncludePrereleaseUpdates { get; set; } = false;
        public UpdateCheckPeriod UpdatePeriod { get; set; } = UpdateCheckPeriod.Day1;
        public DateTime LastUpdateCheckUtc { get; set; } = DateTime.MinValue;
    }

    public static class AppSettingsStore
    {
        static readonly string SettingsFile = Path.Combine(AppContext.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                AppSettings settings;
                if (!File.Exists(SettingsFile))
                {
                    settings = new AppSettings();
                }
                else
                {
                    var json = File.ReadAllText(SettingsFile);
                    settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var hasAutoApply = doc.RootElement.TryGetProperty(nameof(AppSettings.AutoApplyOnStartup), out _);
                        if (!hasAutoApply)
                            settings.AutoApplyOnStartup = true;

                        var hasGuiStartup = doc.RootElement.TryGetProperty(nameof(AppSettings.GuiStartupMode), out _);
                        if (!hasGuiStartup)
                            settings.GuiStartupMode = settings.AutoStartWithWindows ? StartupScopeMode.CurrentUser : StartupScopeMode.Disabled;

                        var hasCoreMode = doc.RootElement.TryGetProperty(nameof(AppSettings.CoreEngineStartupMode), out _);
                        if (!hasCoreMode)
                            settings.CoreEngineStartupMode = settings.AutoStartWithWindows ? CoreEngineStartupMode.WithGui : CoreEngineStartupMode.Disabled;
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrWhiteSpace(settings.Language))
                    settings.Language = L10n.DetectSystemLanguageCode();

                if (string.IsNullOrWhiteSpace(settings.SharedConfigPath))
                    settings.SharedConfigPath = GetDefaultSharedConfigPath();

                if (string.IsNullOrWhiteSpace(settings.SharedLogsPath))
                    settings.SharedLogsPath = GetDefaultSharedLogsPath();

                return settings;
            }
            catch
            {
                return new AppSettings { Language = L10n.DetectSystemLanguageCode() };
            }
        }

        public static void Save(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.SharedConfigPath))
                settings.SharedConfigPath = GetDefaultSharedConfigPath();
            if (string.IsNullOrWhiteSpace(settings.SharedLogsPath))
                settings.SharedLogsPath = GetDefaultSharedLogsPath();

            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static string ResolveConfigDirectory(AppSettings settings)
        {
            if (settings.UseSharedDataLocation && !string.IsNullOrWhiteSpace(settings.SharedConfigPath))
                return settings.SharedConfigPath;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "PriorityManagerX", "config");
        }

        public static string ResolveLogDirectory(AppSettings settings)
        {
            if (settings.UseSharedDataLocation && !string.IsNullOrWhiteSpace(settings.SharedLogsPath))
                return settings.SharedLogsPath;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "PriorityManagerX", "logs");
        }

        static string GetDefaultSharedConfigPath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PriorityManagerX", "config");

        static string GetDefaultSharedLogsPath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PriorityManagerX", "logs");
    }

    public static class L10n
    {
        public static AppLanguage CurrentLanguage { get; set; } = AppLanguage.Russian;

        public static AppLanguage ParseLanguage(string? value)
        {
            if (string.Equals(value, "uk", StringComparison.OrdinalIgnoreCase))
                return AppLanguage.Ukrainian;
            if (string.Equals(value, "en", StringComparison.OrdinalIgnoreCase))
                return AppLanguage.English;

            return AppLanguage.Russian;
        }

        public static string ToCode(AppLanguage language)
            => language switch
            {
                AppLanguage.Ukrainian => "uk",
                AppLanguage.English => "en",
                _ => "ru"
            };

        public static string DetectSystemLanguageCode()
        {
            var twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            return twoLetter switch
            {
                "uk" => "uk",
                "ru" => "ru",
                _ => "en"
            };
        }

        public static string Text(string ru, string uk, string en)
            => CurrentLanguage switch
            {
                AppLanguage.Ukrainian => uk,
                AppLanguage.English => en,
                _ => ru
            };

        public static string AppTitle => "Priority Manager X";

        public static string Settings => Text("Настройки", "Налаштування", "Settings");
        public static string SettingsButton => "⚙";
        public static string HelpMenu => Text("Справка", "Довідка", "Help");
        public static string HelpInstructionItem => Text("Инструкция", "Інструкція", "Instructions");
        public static string HelpVisitSiteItem => Text("Посетить сайт CybLight.org", "Відвідати сайт CybLight.org", "Visit CybLight.org");
        public static string HelpAboutItem => Text("О программе PMX", "Про програму PMX", "About PMX");
        public static string HelpDonateItem => Text("Пожертвовать", "Пожертвувати", "Donate");
        public static string DonateQuick => Text("💚 Донат", "💚 Донат", "💚 Donate");
        public static string HelpInstructionText => Text(
            "1) Базовая настройка\n" +
            "   • Откройте вкладку 'Правила'.\n" +
            "   • Добавьте процесс (например: game.exe) и выберите класс приоритета.\n" +
            "   • Нажмите 'Добавить', затем 'Сохранить' в настройках при необходимости.\n\n" +
            "2) Применение правил\n" +
            "   • 'Применить выбранное' — применяет только выбранное правило.\n" +
            "   • 'Применить все' — применяет все сохранённые правила к уже запущенным процессам.\n" +
            "   • Если включено автоприменение, правила будут применяться автоматически при старте PMX/движка.\n\n" +
            "3) Контекстное меню .exe\n" +
            "   • Для быстрого назначения приоритета используйте пункт 'Priority Manager X' по правому клику на .exe.\n" +
            "   • Если пункта нет, переустановите/восстановите интеграцию через установщик PMX.\n" +
            "   • После изменений меню может потребоваться перезапуск Проводника Windows.\n\n" +
            "4) Движок и автозапуск\n" +
            "   • В 'Настройки -> Интеграция с Windows' выберите запуск GUI и запуск основного движка.\n" +
            "   • Для стабильной фоновой работы включите автовосстановление движка.\n" +
            "   • Если движок завершить вручную, watchdog может автоматически его восстановить.\n\n" +
            "5) Многопользовательский режим\n" +
            "   • Выберите область управления: только текущий сеанс или все доступные процессы.\n" +
            "   • При необходимости укажите общие пути конфигурации и журнала.\n\n" +
            "6) Диагностика\n" +
            "   • Если правило не применяется: проверьте имя процесса (точно *.exe), права администратора и наличие процесса в списке.\n" +
            "   • Для системных/защищённых процессов может требоваться запуск PMX от имени администратора.\n" +
            "   • После изменения параметров автозапуска перезайдите в систему для проверки.",
            "1) Базове налаштування\n" +
            "   • Відкрийте вкладку 'Правила'.\n" +
            "   • Додайте процес (наприклад: game.exe) і виберіть клас пріоритету.\n" +
            "   • Натисніть 'Додати', потім 'Зберегти' у налаштуваннях за потреби.\n\n" +
            "2) Застосування правил\n" +
            "   • 'Застосувати вибране' — застосовує лише вибране правило.\n" +
            "   • 'Застосувати всі' — застосовує всі збережені правила до вже запущених процесів.\n" +
            "   • Якщо увімкнено автозастосування, правила застосовуються автоматично під час запуску PMX/рушія.\n\n" +
            "3) Контекстне меню .exe\n" +
            "   • Для швидкого призначення пріоритету використовуйте пункт 'Priority Manager X' у меню правої кнопки на .exe.\n" +
            "   • Якщо пункту немає, перевстановіть/відновіть інтеграцію через інсталятор PMX.\n" +
            "   • Після змін меню може знадобитися перезапуск Провідника Windows.\n\n" +
            "4) Рушій і автозапуск\n" +
            "   • У 'Налаштування -> Інтеграція з Windows' виберіть запуск GUI і запуск основного рушія.\n" +
            "   • Для стабільної фонової роботи увімкніть автовідновлення рушія.\n" +
            "   • Якщо рушій завершити вручну, watchdog може автоматично його відновити.\n\n" +
            "5) Багатокористувацький режим\n" +
            "   • Виберіть область керування: лише поточний сеанс або всі доступні процеси.\n" +
            "   • За потреби вкажіть спільні шляхи конфігурації та журналу.\n\n" +
            "6) Діагностика\n" +
            "   • Якщо правило не застосовується: перевірте ім'я процесу (точно *.exe), права адміністратора і наявність процесу у списку.\n" +
            "   • Для системних/захищених процесів може бути потрібний запуск PMX від імені адміністратора.\n" +
            "   • Після зміни параметрів автозапуску перезайдіть у систему для перевірки.",
            "1) Basic setup\n" +
            "   • Open the 'Rules' tab.\n" +
            "   • Add a process (for example: game.exe) and choose a priority class.\n" +
            "   • Click 'Add', then save settings if needed.\n\n" +
            "2) Applying rules\n" +
            "   • 'Apply selected' applies only the selected rule.\n" +
            "   • 'Apply all' applies all saved rules to currently running processes.\n" +
            "   • With auto-apply enabled, rules are applied automatically when PMX/engine starts.\n\n" +
            "3) .exe context menu\n" +
            "   • For quick assignment, use the 'Priority Manager X' item on right-click for .exe files.\n" +
            "   • If the item is missing, reinstall/repair integration using the PMX installer.\n" +
            "   • Explorer restart may be required after menu changes.\n\n" +
            "4) Engine and startup\n" +
            "   • In 'Settings -> Windows integration', select GUI startup and core engine startup mode.\n" +
            "   • Enable engine auto-recovery for stable background operation.\n" +
            "   • If engine is terminated manually, watchdog can restore it automatically.\n\n" +
            "5) Multi-user mode\n" +
            "   • Choose scope: current session only or all accessible processes.\n" +
            "   • Optionally set shared config/log locations.\n\n" +
            "6) Troubleshooting\n" +
            "   • If a rule is not applied: verify process name (*.exe), admin rights, and process visibility in the list.\n" +
            "   • System/protected processes may require running PMX as administrator.\n" +
            "   • After changing startup settings, sign out/in to validate behavior.");
        public static string AboutShortInfo => Text(
            "Priority Manager X\nУправление приоритетами процессов и автоприменение правил для приложений Windows.",
            "Priority Manager X\nКерування пріоритетами процесів та автозастосування правил для застосунків Windows.",
            "Priority Manager X\nProcess priority management with auto-apply rules for Windows applications.");
        public static string AboutVersion(string version)
            => Text($"Версия: {version}", $"Версія: {version}", $"Version: {version}");
        public static string AboutEnterDevCode => Text("Ввести код разработчика", "Ввести код розробника", "Enter developer code");
        public static string DevCodeAccepted => Text("Код принят. Дополнительная функция активирована.", "Код прийнято. Додаткову функцію активовано.", "Code accepted. Extra feature activated.");
        public static string DevCodeInvalid => Text("Неверный код разработчика.", "Невірний код розробника.", "Invalid developer code.");
        public static string LanguageLabel => Text("Язык:", "Мова:", "Language:");
        public static string LanguageRu => Text("Русский", "Російська", "Russian");
        public static string LanguageUk => Text("Украинский", "Українська", "Ukrainian");
        public static string LanguageEn => Text("Английский", "Англійська", "English");

        public static string ProcessesTab => Text("Процессы", "Процеси", "Processes");
        public static string RulesTab => Text("Правила", "Правила", "Rules");

        public static string RunningProcesses => Text("Запущенные процессы", "Запущені процеси", "Running processes");
        public static string RunningProcessesWithCount(int count)
            => Text($"Всего запущенных процессов: {count}", $"Усього запущених процесів: {count}", $"Total running processes: {count}");
        public static string ProcessCounters(int processes, int applications, int runningApplications, int background, int active)
            => Text(
                $"Процессов: {processes} | Приложений: {applications} | Запущенных приложений: {runningApplications} | Фоновых: {background} | Активных: {active}",
                $"Процесів: {processes} | Застосунків: {applications} | Запущених застосунків: {runningApplications} | Фонових: {background} | Активних: {active}",
                $"Processes: {processes} | Applications: {applications} | Running apps: {runningApplications} | Background: {background} | Active: {active}");
        public static string SectionWithCount(string label, int count) => $"{label} ({count})";
        public static string ProcessSectionApplications => Text("Приложения", "Застосунки", "Applications");
        public static string ProcessSectionBackground => Text("Фоновые процессы", "Фонові процеси", "Background processes");
        public static string ProcessSectionWindows => Text("Процессы Windows", "Процеси Windows", "Windows processes");
        public static string RunningAppsLabel => Text("Запущенные приложения", "Запущені застосунки", "Running applications");
        public static string AllAppsLabel => Text("Все приложения", "Усі застосунки", "All applications");
        public static string RefreshProcesses => Text("Обновить", "Оновити", "Refresh");
        public static string OpenTaskManager => Text("Открыть Диспетчер задач", "Відкрити Диспетчер завдань", "Open Task Manager");
        public static string SearchLabel => Text("Поиск:", "Пошук:", "Search:");
        public static string SearchProcessesPlaceholder => Text("Поиск по процессам...", "Пошук по процесах...", "Search processes...");
        public static string SearchRulesPlaceholder => Text("Поиск по правилам...", "Пошук по правилах...", "Search rules...");

        public static string ProcessColumnName => Text("Имя", "Назва", "Name");
        public static string ProcessColumnPid => "PID";
        public static string ProcessColumnPriority => Text("Приоритет", "Пріоритет", "Priority");
        public static string ProcessColumnCpu => Text("ЦП (%)", "ЦП (%)", "CPU (%)");
        public static string ProcessColumnMemory => Text("Память (MB)", "Пам'ять (MB)", "Memory (MB)");
        public static string ProcessColumnDisk => Text("Диск (MB/s)", "Диск (MB/s)", "Disk (MB/s)");
        public static string ProcessColumnNetwork => Text("Сеть (Mb/s)", "Мережа (Mb/s)", "Network (Mb/s)");
        public static string ProcessColumnThreads => Text("Потоки", "Потоки", "Threads");
        public static string ProcessColumnPath => Text("Путь", "Шлях", "Path");

        public static string CtxRefresh => Text("Обновить", "Оновити", "Refresh");
        public static string CtxPriorityCurrent => Text("Приоритет ЦП (текущий)", "Пріоритет ЦП (поточний)", "CPU priority (current)");
        public static string CtxPriorityAlways => Text("Приоритет ЦП (всегда)", "Пріоритет ЦП (завжди)", "CPU priority (always)");
        public static string CtxRemoveSavedPriority => Text("Удалить сохранённый приоритет", "Видалити збережений пріоритет", "Remove saved priority");
        public static string CtxAffinity => Text("Набор ЦП (текущий)", "Набір ЦП (поточний)", "CPU affinity (current)");
        public static string CtxAffinityAll => Text("Все ядра", "Усі ядра", "All CPUs");
        public static string CtxAddRule => Text("Добавить правило для процесса", "Додати правило для процесу", "Add rule for process");
        public static string CtxApplyRulesNow => Text("Применить все правила сейчас", "Застосувати всі правила зараз", "Apply all rules now");
        public static string CtxRestartProcess => Text("Перезапустить процесс", "Перезапустити процес", "Restart process");
        public static string CtxKillProcess => Text("Завершить процесс", "Завершити процес", "Terminate process");
        public static string CtxOpenLocation => Text("Открыть расположение файла", "Відкрити розташування файлу", "Open file location");
        public static string CtxCopyName => Text("Копировать имя процесса", "Копіювати ім'я процесу", "Copy process name");
        public static string CtxCopyPath => Text("Копировать путь", "Копіювати шлях", "Copy path");
        public static string InstallMenu => Text("Установить меню .exe", "Встановити меню .exe", "Install .exe menu");
        public static string RemoveMenu => Text("Удалить меню .exe", "Видалити меню .exe", "Remove .exe menu");

        public static string RuleEditorTitle => Text("Редактор правила", "Редактор правила", "Rule editor");
        public static string ProcessPlaceholder => "process.exe";
        public static string PriorityLabel => Text("Приоритет:", "Пріоритет:", "Priority:");
        public static string PriorityNotAvailable => Text("Н/Д", "Н/Д", "N/A");

        public static string PriorityDisplay(string canonical)
        {
            return canonical switch
            {
                "Idle" => Text("Низкий", "Низький", "Idle"),
                "BelowNormal" => Text("Ниже обычного", "Нижче звичайного", "Below Normal"),
                "Normal" => Text("Обычный", "Звичайний", "Normal"),
                "AboveNormal" => Text("Выше обычного", "Вище звичайного", "Above Normal"),
                "High" => Text("Высокий", "Високий", "High"),
                "RealTime" => Text("Реального времени", "Реального часу", "Real Time"),
                "N/A" => PriorityNotAvailable,
                _ => canonical
            };
        }

        public static string NormalizePriority(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Normal";

            var trimmed = value.Trim();
            return trimmed switch
            {
                "Idle" or "Низкий" or "Низький" => "Idle",
                "BelowNormal" or "Below Normal" or "Ниже обычного" or "Нижче звичайного" => "BelowNormal",
                "Normal" or "Обычный" or "Звичайний" => "Normal",
                "AboveNormal" or "Above Normal" or "Выше обычного" or "Вище звичайного" => "AboveNormal",
                "High" or "Высокий" or "Високий" => "High",
                "RealTime" or "Real Time" or "Реального времени" or "Реального часу" => "RealTime",
                _ => trimmed
            };
        }
        public static string AddRule => Text("Добавить", "Додати", "Add");
        public static string UpdateRule => Text("Изменить", "Змінити", "Update");
        public static string DeleteRule => Text("Удалить", "Видалити", "Delete");
        public static string DuplicateRule => Text("Дублировать", "Дублювати", "Duplicate");
        public static string MoveUpRule => Text("Вверх", "Вгору", "Up");
        public static string MoveDownRule => Text("Вниз", "Вниз", "Down");
        public static string ApplySelectedRule => Text("Применить выбранное", "Застосувати вибране", "Apply selected");
        public static string ApplyAllRules => Text("Применить все", "Застосувати всі", "Apply all");
        public static string DeleteAllRules => Text("Удалить все", "Видалити всі", "Delete all");
        public static string ExportRules => Text("Экспорт правил", "Експорт правил", "Export rules");
        public static string ImportRules => Text("Импорт правил", "Імпорт правил", "Import rules");

        public static string RuleColumnProcess => Text("Процесс", "Процес", "Process");
        public static string RuleColumnPriority => Text("Приоритет", "Пріоритет", "Priority");
        public static string RuleColumnCpuMatch => Text("Соответствие процессу", "Відповідність процесу", "Process match");
        public static string RuleColumnCpuSets => Text("Наборы ЦП", "Набори ЦП", "CPU sets");
        public static string RuleColumnPriorityClass => Text("Класс приоритета", "Клас пріоритету", "Priority class");
        public static string RuleColumnIoPriority => Text("Приоритет ввода/вывода", "Пріоритет вводу/виводу", "I/O priority");
        public static string RuleColumnGpuPriority => Text("Приоритет ГП", "Пріоритет ГП", "GPU priority");
        public static string RuleColumnEfficiencyMode => Text("Эффективный режим", "Ефективний режим", "Efficiency mode");
        public static string RuleColumnPerformanceMode => Text("Производительный режим", "Продуктивний режим", "Performance mode");

        public static string MsgSpecifyProcess => Text("Укажите process.exe", "Вкажіть process.exe", "Specify process.exe");
        public static string MsgSelectPriority => Text("Выберите корректный приоритет", "Оберіть коректний пріоритет", "Select a valid priority");
        public static string MsgRuleAdded => Text("Правило добавлено.", "Правило додано.", "Rule added.");
        public static string MsgRuleUpdated => Text("Правило изменено.", "Правило змінено.", "Rule updated.");
        public static string MsgRuleDeleted => Text("Правило удалено.", "Правило видалено.", "Rule deleted.");
        public static string MsgRuleDuplicated => Text("Правило дублировано.", "Правило дубльовано.", "Rule duplicated.");
        public static string MsgRulesDeletedAll => Text("Все правила удалены.", "Усі правила видалені.", "All rules deleted.");
        public static string MsgRulesExported(string filePath)
            => Text($"Правила экспортированы: {filePath}", $"Правила експортовано: {filePath}", $"Rules exported: {filePath}");
        public static string MsgRulesExportFailed(string error)
            => Text($"Не удалось экспортировать правила. {error}", $"Не вдалося експортувати правила. {error}", $"Failed to export rules. {error}");
        public static string MsgRulesImported(int count)
            => Text($"Правила импортированы: {count}", $"Правила імпортовано: {count}", $"Rules imported: {count}");
        public static string MsgRulesImportFailed(string error)
            => Text($"Не удалось импортировать правила. {error}", $"Не вдалося імпортувати правила. {error}", $"Failed to import rules. {error}");
        public static string MsgRuleInputInvalid => Text("Введите process.exe и выберите приоритет.", "Введіть process.exe і оберіть пріоритет.", "Enter process.exe and select priority.");
        public static string MsgSelectRuleFirst => Text("Сначала выберите правило.", "Спочатку оберіть правило.", "Select a rule first.");
        public static string MsgApplyConfirm => Text("Применить все сохранённые правила к текущим процессам?", "Застосувати всі збережені правила до поточних процесів?", "Apply all saved rules to current processes?");
        public static string MsgDeleteAllRulesConfirm => Text("Удалить все правила?", "Видалити всі правила?", "Delete all rules?");
        public static string MsgSettingsSaved => Text("Настройки сохранены.", "Налаштування збережено.", "Settings saved.");
        public static string MsgNoRunningProcesses => Text("Нет доступных процессов.", "Немає доступних процесів.", "No processes available.");
        public static string MsgSavedFor(string process, string priority, string scope)
            => Text($"Сохранено для {process}: {priority} ({scope})", $"Збережено для {process}: {priority} ({scope})", $"Saved for {process}: {priority} ({scope})");
        public static string MsgSaveFailed(string error)
            => Text($"Не удалось сохранить приоритет. {error}", $"Не вдалося зберегти пріоритет. {error}", $"Failed to save priority. {error}");
        public static string MsgMenuInstalled => Text("Контекстное меню для .exe установлено.", "Контекстне меню для .exe встановлено.", "Context menu for .exe has been installed.");
        public static string MsgMenuInstallFailed(string error)
            => Text($"Не удалось установить контекстное меню. {error}", $"Не вдалося встановити контекстне меню. {error}", $"Failed to install context menu. {error}");
        public static string MsgMenuRemoved => Text("Контекстное меню для .exe удалено.", "Контекстне меню для .exe видалено.", "Context menu for .exe has been removed.");
        public static string MsgMenuRemoveFailed(string error)
            => Text($"Не удалось удалить контекстное меню. {error}", $"Не вдалося видалити контекстне меню. {error}", $"Failed to remove context menu. {error}");
        public static string MsgAutoStartUpdated => Text("Параметры автозапуска обновлены.", "Параметри автозапуску оновлено.", "Autostart settings updated.");
        public static string MsgAutoStartFailed(string error)
            => Text($"Не удалось обновить автозапуск. {error}", $"Не вдалося оновити автозапуск. {error}", $"Failed to update autostart. {error}");
        public static string MsgRunAsAdminFailed(string error)
            => Text($"Не удалось обновить запуск от администратора. {error}", $"Не вдалося оновити запуск від адміністратора. {error}", $"Failed to update run-as-admin setting. {error}");
        public static string MsgUpdateCheckFailed(string error)
            => Text($"Не удалось проверить обновления. {error}", $"Не вдалося перевірити оновлення. {error}", $"Failed to check updates. {error}");
        public static string MsgUpdateAvailableAsk(string currentVersion, string latestVersion)
            => Text(
                $"Доступна новая версия PMX: {latestVersion} (текущая: {currentVersion}).\nСкачать и установить сейчас?",
                $"Доступна нова версія PMX: {latestVersion} (поточна: {currentVersion}).\nЗавантажити й встановити зараз?",
                $"A new PMX version is available: {latestVersion} (current: {currentVersion}).\nDownload and install now?");
        public static string MsgUpdateInstallerNotFound
            => Text("В релизе не найден файл установщика (.exe).", "У релізі не знайдено файл інсталятора (.exe).", "No installer file (.exe) was found in the release.");
        public static string MsgUpdateDownloadStarted(string fileName)
            => Text($"Загрузка обновления: {fileName}", $"Завантаження оновлення: {fileName}", $"Downloading update: {fileName}");
        public static string MsgUpdateDownloadFailed(string error)
            => Text($"Не удалось скачать обновление. {error}", $"Не вдалося завантажити оновлення. {error}", $"Failed to download update. {error}");
        public static string MsgNoUpdatesFound => Text("Новых обновлений не найдено.", "Нових оновлень не знайдено.", "No updates found.");
        public static string MsgApplyDone => Text("Приоритет применён к запущенным процессам.", "Пріоритет застосовано до запущених процесів.", "Priority applied to running processes.");
        public static string MsgNoProcessSelected => Text("Сначала выберите процесс в таблице.", "Спочатку оберіть процес у таблиці.", "Select a process in the table first.");
        public static string MsgProcessActionFailed(string error)
            => Text($"Не удалось выполнить действие с процессом. {error}", $"Не вдалося виконати дію з процесом. {error}", $"Failed to perform process action. {error}");
        public static string MsgPathUnavailable => Text("Путь к файлу процесса недоступен.", "Шлях до файлу процесу недоступний.", "Process file path is unavailable.");

        public static string MsgUnknownPriority(string priority)
            => Text($"Неизвестный приоритет: {priority}", $"Невідомий пріоритет: {priority}", $"Unknown priority: {priority}");
        public static string MsgSavedCli(string exe, string priority, string scope)
            => Text($"Сохранено: {exe} -> {priority} ({scope})", $"Збережено: {exe} -> {priority} ({scope})", $"Saved: {exe} -> {priority} ({scope})");
        public static string MsgRemovedCli(string exe)
            => Text($"Удалено: {exe}", $"Видалено: {exe}", $"Removed: {exe}");
        public static string MsgRemoveFailedCli(string error)
            => Text($"Не удалось удалить сохранённый приоритет. {error}", $"Не вдалося видалити збережений пріоритет. {error}", $"Failed to remove saved priority. {error}");
        public static string MsgMenuInstalledCli => Text("Контекстное меню установлено.", "Контекстне меню встановлено.", "Context menu installed.");
        public static string MsgMenuRemovedCli => Text("Контекстное меню удалено.", "Контекстне меню видалено.", "Context menu removed.");

        public static string ErrSpecifyProcessOrPath => Text("Укажите имя процесса или путь к .exe", "Вкажіть ім'я процесу або шлях до .exe", "Specify process name or path to .exe");
        public static string ErrNotFoundSavedPriority => Text("Сохранённый приоритет не найден в HKLM/HKCU.", "Збережений пріоритет не знайдено в HKLM/HKCU.", "Saved priority was not found in HKLM/HKCU.");
        public static string ErrCreateMenuKey => Text("Не удалось создать ключ контекстного меню.", "Не вдалося створити ключ контекстного меню.", "Failed to create context menu key.");
        public static string ErrOpenPerfOptions => Text("Не удалось открыть ключ PerfOptions.", "Не вдалося відкрити ключ PerfOptions.", "Failed to open PerfOptions key.");
        public static string ErrAdminRequiredForMainMenu => Text("Для добавления в основное меню Windows 11 запустите установку меню от имени администратора.", "Для додавання до основного меню Windows 11 запустіть встановлення меню від імені адміністратора.", "To add to the Windows 11 main menu, run menu installation as administrator.");

        public static string MenuRoot => "Priority Manager X";
        public static string MenuSavePriority(string canonicalPriority)
            => Text(
                $"Сохранить приоритет: {PriorityDisplay(canonicalPriority)}",
                $"Зберегти пріоритет: {PriorityDisplay(canonicalPriority)}",
                $"Save priority: {PriorityDisplay(canonicalPriority)}");
        public static string MenuSaveIdle => MenuSavePriority("Idle");
        public static string MenuSaveBelowNormal => MenuSavePriority("BelowNormal");
        public static string MenuSaveNormal => MenuSavePriority("Normal");
        public static string MenuSaveAboveNormal => MenuSavePriority("AboveNormal");
        public static string MenuSaveHigh => MenuSavePriority("High");
        public static string MenuSaveRealTime => MenuSavePriority("RealTime");
        public static string MenuRemoveSaved => Text("Удалить сохранённый приоритет", "Видалити збережений пріоритет", "Remove saved priority");

        public static string SettingsTitle => Text("Настройки приложения", "Налаштування застосунку", "Application settings");
        public static string SettingsInterface => Text("Интерфейс", "Інтерфейс", "Interface");
        public static string SettingsBehavior => Text("Поведение", "Поведінка", "Behavior");
        public static string SettingsIntegration => Text("Интеграция с Windows", "Інтеграція з Windows", "Windows integration");
        public static string SettingsAutoApply => Text("Автоприменение правил при запуске", "Автозастосування правил при запуску", "Auto-apply rules on startup");
        public static string SettingsConfirmApply => Text("Подтверждать 'Применить сейчас'", "Підтверджувати 'Застосувати зараз'", "Confirm 'Apply now'");
        public static string SettingsAutoRefresh => Text("Автообновлять список процессов", "Автооновлювати список процесів", "Auto-refresh process list");
        public static string SettingsRefreshSeconds => Text("Интервал обновления (сек):", "Інтервал оновлення (сек):", "Refresh interval (sec):");
        public static string SettingsInstallMenuHint => Text("Контекстное меню для .exe в Проводнике:", "Контекстне меню для .exe у Провіднику:", "Explorer context menu for .exe:");
        public static string SettingsAutoStartTask => Text("Запускать PMX в фоне при входе в систему (задача планировщика)", "Запускати PMX у фоні при вході в систему (завдання планувальника)", "Run PMX in background at sign-in (scheduled task)");
        public static string SettingsRunAsAdmin => Text("Запускать PMX от имени администратора", "Запускати PMX від імені адміністратора", "Run PMX as administrator");
        public static string SettingsStartupGuiGroup => Text("Запуск консоли управления (GUI)", "Запуск консолі керування (GUI)", "Management console startup (GUI)");
        public static string SettingsStartupEngineGroup => Text("Запуск основного движка", "Запуск основного рушія", "Core engine startup");
        public static string SettingsStartupAllUsers => Text("Запуск при входе для всех пользователей", "Запуск при вході для всіх користувачів", "Start at sign-in for ALL users");
        public static string SettingsStartupCurrentUser => Text("Запуск при входе только для текущего пользователя", "Запуск при вході тільки для поточного користувача", "Start at sign-in for current user only");
        public static string SettingsStartupDisabled => Text("Не запускать при входе в систему", "Не запускати при вході в систему", "Do not start at sign-in");
        public static string SettingsStartupEngineServiceLike => Text("Запускать основной движок как службу при загрузке системы", "Запускати основний рушій як службу при завантаженні системи", "Start core engine like a service at system boot");
        public static string SettingsStartupEngineWithGui => Text("Запускать основной движок вместе с GUI", "Запускати основний рушій разом із GUI", "Start core engine together with GUI");
        public static string SettingsEngineAutoRecover => Text("Автовосстановление движка, если его завершили", "Автовідновлення рушія, якщо його завершили", "Auto-recover engine if it is terminated");
        public static string SettingsTipGuiStartupAllUsers => Text("GUI запускается при входе в систему для каждого пользователя Windows.", "GUI запускається при вході в систему для кожного користувача Windows.", "GUI starts at sign-in for every Windows user.");
        public static string SettingsTipGuiStartupCurrentUser => Text("GUI запускается при входе в систему только для текущего пользователя.", "GUI запускається при вході в систему лише для поточного користувача.", "GUI starts at sign-in for the current user only.");
        public static string SettingsTipGuiStartupDisabled => Text("GUI не запускается автоматически при входе в систему.", "GUI не запускається автоматично при вході в систему.", "GUI does not start automatically at sign-in.");
        public static string SettingsTipEngineStartupAllUsers => Text("Сторожевой процесс движка запускается при входе для всех пользователей.", "Сторожовий процес рушія запускається при вході для всіх користувачів.", "Engine watchdog starts at sign-in for all users.");
        public static string SettingsTipEngineStartupCurrentUser => Text("Сторожевой процесс движка запускается при входе только для текущего пользователя.", "Сторожовий процес рушія запускається при вході лише для поточного користувача.", "Engine watchdog starts at sign-in for current user only.");
        public static string SettingsTipEngineStartupServiceLike => Text("Движок запускается на старте системы через планировщик как системная задача, до входа пользователя.", "Рушій запускається під час старту системи через планувальник як системне завдання, до входу користувача.", "Engine starts at system boot via Task Scheduler as a system task, before user sign-in.");
        public static string SettingsTipEngineStartupWithGui => Text("Основной движок запускается вместе с GUI и работает, пока открыт GUI/его автостарт.", "Основний рушій запускається разом із GUI і працює, доки відкритий GUI/його автозапуск.", "Core engine starts together with GUI and runs while GUI (or its autostart) is active.");
        public static string SettingsTipEngineStartupDisabled => Text("Основной движок не запускается автоматически.", "Основний рушій не запускається автоматично.", "Core engine does not start automatically.");
        public static string SettingsTipEngineAutoRecover => Text("Если процесс движка завершится, сторожевой процесс попытается запустить его снова.", "Якщо процес рушія завершиться, сторожовий процес спробує запустити його знову.", "If engine process is terminated, watchdog will try to start it again.");
        public static string SettingsTipRunAsAdmin => Text("Добавляет флаг совместимости RUNASADMIN для PMX. При запуске будет запрашиваться UAC.", "Додає прапорець сумісності RUNASADMIN для PMX. Під час запуску буде запит UAC.", "Adds RUNASADMIN compatibility flag for PMX. Launch will require UAC consent.");
        public static string SettingsMultiUserGroup => Text("Настройки многопользовательского окружения", "Налаштування багатокористувацького середовища", "Multi-user environment settings");
        public static string SettingsProcessScopeCurrent => Text("Управлять только процессами текущего сеанса", "Керувати тільки процесами поточного сеансу", "Manage only current-session processes");
        public static string SettingsProcessScopeAll => Text("Управлять всеми процессами, к которым PMX имеет доступ", "Керувати всіма процесами, до яких PMX має доступ", "Manage all processes PMX can access");
        public static string SettingsUseSharedLocation => Text("Использовать общее расположение конфигов и журнала", "Використовувати спільне розташування конфігів і журналу", "Use shared config and log location");
        public static string SettingsConfigPath => Text("Файл/папка конфигурации:", "Файл/папка конфігурації:", "Config folder:");
        public static string SettingsLogsPath => Text("Папка журнала:", "Папка журналу:", "Log folder:");
        public static string SettingsDateFormat => Text("Формат даты:", "Формат дати:", "Date format:");
        public static string SettingsDateEurope => Text("Европа (дд-мм-гггг)", "Європа (дд-мм-рррр)", "Europe (dd-mm-yyyy)");
        public static string SettingsDateUsa => Text("США (мм-дд-гггг)", "США (мм-дд-рррр)", "USA (mm-dd-yyyy)");
        public static string SettingsDateAsia => Text("Азия (гггг-мм-дд)", "Азія (рррр-мм-дд)", "Asia (yyyy-mm-dd)");
        public static string SettingsUpdates => Text("Обновления", "Оновлення", "Updates");
        public static string SettingsUpdatesGroup => Text("Обновления через GitHub Releases", "Оновлення через GitHub Releases", "Updates via GitHub Releases");
        public static string SettingsCheckUpdatesOnStartup => Text("Автоматически проверять обновления при запуске", "Автоматично перевіряти оновлення під час запуску", "Automatically check for updates at startup");
        public static string SettingsIncludePrerelease => Text("Включать pre-release версии", "Включати pre-release версії", "Include pre-release versions");
        public static string UpdatesButton => Text("Обновления", "Оновлення", "Updates");
        public static string UpdatesCheckNow => Text("Проверить сейчас", "Перевірити зараз", "Check now");
        public static string UpdatesPeriod => Text("Период проверки", "Період перевірки", "Check period");
        public static string UpdatesIncludeBeta => Text("Включать беты", "Включати бети", "Include betas");
        public static string UpdatesAuto => Text("Автообновления", "Автооновлення", "Auto updates");
        public static string UpdatesPeriodNever => Text("Никогда", "Ніколи", "Never");
        public static string UpdatesPeriod12h => Text("12 часов", "12 годин", "12 hours");
        public static string UpdatesPeriod1d => Text("1 день", "1 день", "1 day");
        public static string UpdatesPeriod2d => Text("2 дня", "2 дні", "2 days");
        public static string UpdatesPeriod1w => Text("1 неделя", "1 тиждень", "1 week");
        public static string UpdatesPeriod2w => Text("2 недели", "2 тижні", "2 weeks");
        public static string SettingsOk => Text("Сохранить", "Зберегти", "Save");
        public static string SettingsCancel => Text("Отмена", "Скасувати", "Cancel");
    }
}
