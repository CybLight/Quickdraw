using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PriorityManagerX
{
    public class Rule
    {
        public string Process { get; set; } = "";
        public string Priority { get; set; } = "Normal";
        public string CpuMatch { get; set; } = "Все";
        public string CpuSets { get; set; } = "Все";
        public string IoPriority { get; set; } = "—";
        public string GpuPriority { get; set; } = "—";
        public string EfficiencyMode { get; set; } = "—";
        public string PerformanceMode { get; set; } = "—";
    }

    public class RuleFile
    {
        public List<Rule> Rules { get; set; } = new();
    }

    public static class RuleStore
    {
        static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static string GetRulesFilePath()
        {
            var legacyPath = Path.Combine(AppContext.BaseDirectory, "rules.json");
            var settings = AppSettingsStore.Load();
            var configDir = AppSettingsStore.ResolveConfigDirectory(settings);
            var currentPath = Path.Combine(configDir, "rules.json");

            if (File.Exists(legacyPath) && !File.Exists(currentPath))
                return legacyPath;

            return currentPath;
        }

        public static bool TryAddOrUpdateRule(string processOrPath, string priority, out string error)
        {
            error = string.Empty;
            var process = NormalizeProcessName(processOrPath);
            if (string.IsNullOrWhiteSpace(process))
            {
                error = "Process name is empty.";
                return false;
            }

            var normalizedPriority = L10n.NormalizePriority(priority);
            if (!Enum.TryParse<System.Diagnostics.ProcessPriorityClass>(normalizedPriority, true, out _))
            {
                error = $"Unknown priority '{priority}'.";
                return false;
            }

            try
            {
                var filePath = GetRulesFilePath();
                var ruleFile = LoadRuleFile(filePath);
                var existing = ruleFile.Rules.FirstOrDefault(r =>
                    string.Equals(NormalizeProcessName(r.Process), process, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                    ruleFile.Rules.Add(new Rule { Process = process, Priority = normalizedPriority });
                else
                    existing.Priority = normalizedPriority;

                SaveRuleFile(filePath, ruleFile);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static List<Rule> LoadAllRules()
        {
            try
            {
                var filePath = GetRulesFilePath();
                var ruleFile = LoadRuleFile(filePath);
                return ruleFile.Rules
                    .Select(r => new Rule
                    {
                        Process = NormalizeProcessName(r.Process),
                        Priority = L10n.NormalizePriority(r.Priority)
                    })
                    .Where(r => !string.IsNullOrWhiteSpace(r.Process))
                    .ToList();
            }
            catch
            {
                return new List<Rule>();
            }
        }

        public static bool TryRemoveRule(string processOrPath, out string error)
        {
            error = string.Empty;
            var process = NormalizeProcessName(processOrPath);
            if (string.IsNullOrWhiteSpace(process))
            {
                error = "Process name is empty.";
                return false;
            }

            try
            {
                var filePath = GetRulesFilePath();
                var ruleFile = LoadRuleFile(filePath);
                var removed = ruleFile.Rules.RemoveAll(r =>
                    string.Equals(NormalizeProcessName(r.Process), process, StringComparison.OrdinalIgnoreCase));

                if (removed > 0)
                    SaveRuleFile(filePath, ruleFile);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static RuleFile LoadRuleFile(string filePath)
        {
            if (!File.Exists(filePath))
                return new RuleFile();

            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new RuleFile();

            return JsonSerializer.Deserialize<RuleFile>(json) ?? new RuleFile();
        }

        static void SaveRuleFile(string filePath, RuleFile ruleFile)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(ruleFile, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        static string NormalizeProcessName(string value)
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
    }
}
