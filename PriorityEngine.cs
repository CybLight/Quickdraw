using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PriorityManagerX
{
    public static class PriorityEngine
    {
        public sealed class ApplyResult
        {
            public string ProcessName { get; init; } = string.Empty;
            public string Priority { get; init; } = string.Empty;
            public int MatchedCount { get; set; }
            public int AppliedCount { get; set; }
            public List<string> ErrorMessages { get; } = new();
        }

        public static void Apply(string process, string priority)
        {
            _ = ApplyWithResult(process, priority);
        }

        public static ApplyResult ApplyWithResult(string process, string priority)
        {
            var normalizedProcess = NormalizeProcessName(process);
            var normalizedPriority = L10n.NormalizePriority(priority);
            var result = new ApplyResult
            {
                ProcessName = normalizedProcess,
                Priority = normalizedPriority
            };

            if (string.IsNullOrWhiteSpace(normalizedProcess))
            {
                result.ErrorMessages.Add("Process name is empty.");
                return result;
            }

            if (!Enum.TryParse<ProcessPriorityClass>(normalizedPriority, true, out var parsedPriority))
            {
                result.ErrorMessages.Add($"Unknown priority '{priority}'.");
                return result;
            }

            var baseName = Path.GetFileNameWithoutExtension(normalizedProcess);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                result.ErrorMessages.Add("Invalid process name.");
                return result;
            }

            var processes = Process.GetProcessesByName(baseName);
            result.MatchedCount = processes.Length;

            foreach (var processInstance in processes)
            {
                try
                {
                    processInstance.PriorityClass = parsedPriority;
                    result.AppliedCount++;
                }
                catch (Exception ex)
                {
                    result.ErrorMessages.Add($"PID {processInstance.Id}: {ex.Message}");
                }
                finally
                {
                    processInstance.Dispose();
                }
            }

            if (result.MatchedCount == 0)
                result.ErrorMessages.Add("No running processes matched this rule.");

            return result;
        }

        static string NormalizeProcessName(string process)
        {
            if (string.IsNullOrWhiteSpace(process))
                return string.Empty;

            var normalized = process.Trim().Trim('"');
            normalized = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            if (!normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                normalized += ".exe";

            return normalized;
        }

        public static bool TryApplyToPid(int pid, ProcessPriorityClass priority, out string error)
        {
            error = string.Empty;
            try
            {
                using var process = Process.GetProcessById(pid);
                process.PriorityClass = priority;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TrySetAffinity(int pid, long affinityMask, out string error)
        {
            error = string.Empty;
            try
            {
                using var process = Process.GetProcessById(pid);
                process.ProcessorAffinity = (IntPtr)affinityMask;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
