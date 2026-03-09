using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace PriorityManagerX
{
    public static class CoreEngineHost
    {
        const string CoreEngineHostExeName = "PMX.CoreEngine.exe";
        const string WatchdogHostExeName = "PMX.EngineWatchdog.exe";
        const string CoreMutexNameGlobal = @"Global\PriorityManagerX.CoreEngine";
        const string CoreMutexNameLocal = @"Local\PriorityManagerX.CoreEngine";
        const string WatchdogMutexNameGlobal = @"Global\PriorityManagerX.EngineWatchdog";
        const string WatchdogMutexNameLocal = @"Local\PriorityManagerX.EngineWatchdog";

        public static bool IsCoreEngineRunning()
            => IsAnyMutexPresent(CoreMutexNameGlobal, CoreMutexNameLocal);

        public static bool IsWatchdogRunning()
            => IsAnyMutexPresent(WatchdogMutexNameGlobal, WatchdogMutexNameLocal);

        public static void EnsureBackgroundComponents(AppSettings settings)
        {
            if (settings.CoreEngineStartupMode == CoreEngineStartupMode.Disabled)
                return;

            if (settings.CoreEngineStartupMode == CoreEngineStartupMode.WithGui)
            {
                if (settings.RestartCoreEngineIfStopped)
                    EnsureWatchdogRunning();
                else
                    EnsureCoreRunning();
                return;
            }

            // For non-GUI startup modes we still start watchdog when app is opened manually.
            if (settings.RestartCoreEngineIfStopped)
                EnsureWatchdogRunning();
            else
                EnsureCoreRunning();
        }

        public static void ApplyRuntimeProfile(AppSettings settings)
        {
            if (settings.CoreEngineStartupMode == CoreEngineStartupMode.Disabled)
                return;

            EnsureBackgroundComponents(settings);
        }

        public static void RunCoreEngine()
        {
            using var mutex = CreateSingleInstanceMutex(CoreMutexNameGlobal, CoreMutexNameLocal, out var createdNew);
            if (!createdNew)
                return;

            ApplicationConfiguration.Initialize();
            Application.Run(new CoreEngineContext());
        }

        public static void RunWatchdog()
        {
            using var mutex = CreateSingleInstanceMutex(WatchdogMutexNameGlobal, WatchdogMutexNameLocal, out var createdNew);
            if (!createdNew)
                return;

            ApplicationConfiguration.Initialize();
            Application.Run(new EngineWatchdogContext());
        }

        static void EnsureCoreRunning()
        {
            if (IsCoreEngineRunning())
                return;

            StartDetached(GetCoreEngineHostPath(), "--core-engine");
        }

        static void EnsureWatchdogRunning()
        {
            if (IsWatchdogRunning())
                return;

            StartDetached(GetWatchdogHostPath(), "--engine-watchdog");
        }

        static string GetCoreEngineHostPath()
            => Path.Combine(AppContext.BaseDirectory, CoreEngineHostExeName);

        static string GetWatchdogHostPath()
            => Path.Combine(AppContext.BaseDirectory, WatchdogHostExeName);

        internal static string GetMainAppPath()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "PriorityManagerX.exe");
            return File.Exists(path) ? path : Application.ExecutablePath;
        }

        static void StartDetached(string executablePath, string fallbackArgs)
        {
            try
            {
                if (File.Exists(executablePath))
                {
                    Process.Start(new ProcessStartInfo(executablePath)
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                    return;
                }

                Process.Start(new ProcessStartInfo(GetMainAppPath(), fallbackArgs)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch
            {
            }
        }

        static bool IsAnyMutexPresent(params string[] names)
        {
            foreach (var name in names)
            {
                if (TryOpenMutex(name))
                    return true;
            }

            return false;
        }

        static System.Threading.Mutex CreateSingleInstanceMutex(string globalName, string localName, out bool createdNew)
        {
            try
            {
                return new System.Threading.Mutex(false, globalName, out createdNew);
            }
            catch (UnauthorizedAccessException)
            {
                return new System.Threading.Mutex(false, localName, out createdNew);
            }
        }

        static bool TryOpenMutex(string name)
        {
            try
            {
                using var existing = System.Threading.Mutex.OpenExisting(name);
                return existing != null;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    sealed class CoreEngineContext : ApplicationContext
    {
        readonly System.Windows.Forms.Timer timer = new();

        public CoreEngineContext()
        {
            ApplyAllRules();

            var settings = AppSettingsStore.Load();
            var interval = Math.Clamp(settings.ProcessRefreshSeconds, 2, 60) * 1000;
            timer.Interval = interval;
            timer.Tick += (_, _) => ApplyAllRules();
            timer.Start();
        }

        void ApplyAllRules()
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

    sealed class EngineWatchdogContext : ApplicationContext
    {
        readonly System.Windows.Forms.Timer timer = new();

        public EngineWatchdogContext()
        {
            timer.Interval = 4000;
            timer.Tick += (_, _) => EnsureCore();
            EnsureCore();
            timer.Start();
        }

        void EnsureCore()
        {
            try
            {
                var settings = AppSettingsStore.Load();
                if (settings.CoreEngineStartupMode == CoreEngineStartupMode.Disabled)
                    return;

                if (!CoreEngineHost.IsCoreEngineRunning())
                {
                    var coreHost = Path.Combine(AppContext.BaseDirectory, "PMX.CoreEngine.exe");
                    if (File.Exists(coreHost))
                    {
                        Process.Start(new ProcessStartInfo(coreHost)
                        {
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        });
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo(CoreEngineHost.GetMainAppPath(), "--core-engine")
                        {
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        });
                    }
                }
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
