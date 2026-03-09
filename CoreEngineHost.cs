using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace PriorityManagerX
{
    public static class CoreEngineHost
    {
        const string CoreMutexName = @"Global\PriorityManagerX.CoreEngine";
        const string WatchdogMutexName = @"Global\PriorityManagerX.EngineWatchdog";

        public static bool IsCoreEngineRunning()
            => TryOpenMutex(CoreMutexName);

        public static bool IsWatchdogRunning()
            => TryOpenMutex(WatchdogMutexName);

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
            using var mutex = new System.Threading.Mutex(false, CoreMutexName, out var createdNew);
            if (!createdNew)
                return;

            ApplicationConfiguration.Initialize();
            Application.Run(new CoreEngineContext());
        }

        public static void RunWatchdog()
        {
            using var mutex = new System.Threading.Mutex(false, WatchdogMutexName, out var createdNew);
            if (!createdNew)
                return;

            ApplicationConfiguration.Initialize();
            Application.Run(new EngineWatchdogContext());
        }

        static void EnsureCoreRunning()
        {
            if (IsCoreEngineRunning())
                return;

            StartDetached("--core-engine");
        }

        static void EnsureWatchdogRunning()
        {
            if (IsWatchdogRunning())
                return;

            StartDetached("--engine-watchdog");
        }

        static void StartDetached(string args)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, args)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch
            {
            }
        }

        static bool TryOpenMutex(string name)
        {
            try
            {
                using var existing = System.Threading.Mutex.OpenExisting(name);
                return existing != null;
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
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--core-engine")
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
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
