using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace PriorityManagerX.ShellExtension;

[ComVisible(true)]
[Guid("4F6D1E31-87BA-4E31-9D2D-B9CA22A6E6D2")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class PriorityManagerPrimaryCommand : IExplorerCommand
{
    const int S_OK = 0;
    const int S_FALSE = 1;
    const int E_NOTIMPL = unchecked((int)0x80004001);

    static readonly Guid CanonicalId = new("CB8D572E-9D9B-4A55-95C7-8DAE8AE7A1A1");

    public int GetTitle(IShellItemArray? psiItemArray, out IntPtr ppszName)
    {
        ppszName = Marshal.StringToCoTaskMemUni("Priority Manager X");
        return S_OK;
    }

    public int GetIcon(IShellItemArray? psiItemArray, out IntPtr ppszIcon)
    {
        ppszIcon = IntPtr.Zero;
        return E_NOTIMPL;
    }

    public int GetToolTip(IShellItemArray? psiItemArray, out IntPtr ppszInfotip)
    {
        ppszInfotip = IntPtr.Zero;
        return E_NOTIMPL;
    }

    public int GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = CanonicalId;
        return S_OK;
    }

    public int GetState(IShellItemArray? psiItemArray, int fOkToBeSlow, out EXPCMDSTATE pCmdState)
    {
        pCmdState = EXPCMDSTATE.ECS_ENABLED;
        return S_OK;
    }

    public int Invoke(IShellItemArray? psiItemArray, IBindCtx? pbc)
    {
        var app = ResolveAppPath();
        var target = ShellSelection.GetFirstPath(psiItemArray);
        if (string.IsNullOrWhiteSpace(app) || !File.Exists(app))
            return S_FALSE;

        var args = string.IsNullOrWhiteSpace(target) ? string.Empty : $"\"{target}\"";
        try
        {
            Process.Start(new ProcessStartInfo(app, args)
            {
                UseShellExecute = true,
                CreateNoWindow = true
            });
            return S_OK;
        }
        catch
        {
            return S_FALSE;
        }
    }

    public int GetFlags(out EXPCMDFLAGS pFlags)
    {
        pFlags = EXPCMDFLAGS.ECF_DEFAULT;
        return S_OK;
    }

    public int EnumSubCommands(out IEnumExplorerCommand? ppEnum)
    {
        ppEnum = null;
        return E_NOTIMPL;
    }

    static string ResolveAppPath()
    {
        var dir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(dir, "PriorityManagerX", "PriorityManagerX.exe"),
            Path.Combine(dir, "PriorityManagerX.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Priority Manager X", "PriorityManagerX.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return string.Empty;
    }
}

[ComVisible(true)]
[Guid("B1B7D9D0-6A69-4A5D-9A89-9A8F59A17C22")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class PriorityManagerExplorerCommand : IExplorerCommand
{
    const int S_OK = 0;
    const int S_FALSE = 1;
    const int E_NOTIMPL = unchecked((int)0x80004001);

    static readonly Guid CanonicalId = new("8B2F8D72-2CB2-4D9D-B2D2-9A65D551A01B");

    public int GetTitle(IShellItemArray? psiItemArray, out IntPtr ppszName)
    {
        DiagnosticLog.Write("Root.GetTitle");
        ppszName = Marshal.StringToCoTaskMemUni("Priority Manager X");
        return S_OK;
    }

    public int GetIcon(IShellItemArray? psiItemArray, out IntPtr ppszIcon)
    {
        DiagnosticLog.Write("Root.GetIcon");
        var iconPath = FindIconPath();
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            ppszIcon = IntPtr.Zero;
            return E_NOTIMPL;
        }

        var icon = $"{iconPath},0";
        ppszIcon = Marshal.StringToCoTaskMemUni(icon);
        return S_OK;
    }

    public int GetToolTip(IShellItemArray? psiItemArray, out IntPtr ppszInfotip)
    {
        DiagnosticLog.Write("Root.GetToolTip");
        ppszInfotip = Marshal.StringToCoTaskMemUni(Text(
            "Настроить приоритет процесса",
            "Налаштувати пріоритет процесу",
            "Configure process priority"));
        return S_OK;
    }

    public int GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = CanonicalId;
        return S_OK;
    }

    public int GetState(IShellItemArray? psiItemArray, int fOkToBeSlow, out EXPCMDSTATE pCmdState)
    {
        DiagnosticLog.Write("Root.GetState");
        pCmdState = EXPCMDSTATE.ECS_ENABLED;
        return S_OK;
    }

    public int Invoke(IShellItemArray? psiItemArray, IBindCtx? pbc)
    {
        var target = ShellSelection.GetFirstPath(psiItemArray);
        var app = FindAppExecutable();
        DiagnosticLog.Write($"Root.Invoke: target='{target}', app='{app}'");

        if (string.IsNullOrWhiteSpace(app) || !File.Exists(app))
        {
            DiagnosticLog.Write("Root.Invoke: app missing");
            return S_FALSE;
        }

        var args = string.IsNullOrWhiteSpace(target) ? string.Empty : $"\"{target}\"";
        var psi = new ProcessStartInfo(app, args)
        {
            UseShellExecute = true,
            CreateNoWindow = true
        };

        try
        {
            Process.Start(psi);
            DiagnosticLog.Write("Root.Invoke: started");
            return S_OK;
        }
        catch
        {
            DiagnosticLog.Write("Root.Invoke: start failed");
            return S_FALSE;
        }
    }

    public int GetFlags(out EXPCMDFLAGS pFlags)
    {
        DiagnosticLog.Write("Root.GetFlags");
        pFlags = EXPCMDFLAGS.ECF_HASSUBCOMMANDS | EXPCMDFLAGS.ECF_HASSPLITBUTTON;
        return S_OK;
    }

    public int EnumSubCommands(out IEnumExplorerCommand? ppEnum)
    {
        DiagnosticLog.Write("Root.EnumSubCommands");
        var commands = new IExplorerCommand[]
        {
            new PriorityActionCommand("Idle", Text("Сохранить приоритет: Низкий", "Зберегти пріоритет: Низький", "Save priority: Idle"), new Guid("5DE328E0-153A-41A5-A7AB-9C4D3906D911")),
            new PriorityActionCommand("BelowNormal", Text("Сохранить приоритет: Ниже обычного", "Зберегти пріоритет: Нижче звичайного", "Save priority: Below Normal"), new Guid("7A6008EB-2800-4FEE-BB29-8D3D357F632D")),
            new PriorityActionCommand("Normal", Text("Сохранить приоритет: Обычный", "Зберегти пріоритет: Звичайний", "Save priority: Normal"), new Guid("DAAE80C3-48D6-4A0D-826E-9D0AFDA756C9")),
            new PriorityActionCommand("AboveNormal", Text("Сохранить приоритет: Выше обычного", "Зберегти пріоритет: Вище звичайного", "Save priority: Above Normal"), new Guid("31EC6026-E84C-49A5-B22B-AD16F0B1EC38")),
            new PriorityActionCommand("High", Text("Сохранить приоритет: Высокий", "Зберегти пріоритет: Високий", "Save priority: High"), new Guid("85641833-42C9-4B77-B286-5E6A4C03181A")),
            new PriorityActionCommand("RealTime", Text("Сохранить приоритет: Реального времени", "Зберегти пріоритет: Реального часу", "Save priority: Real Time"), new Guid("C5D46FD8-6E0D-4899-A163-5D0C9D2D3270")),
            new RemovePriorityCommand(Text("Удалить сохранённый приоритет", "Видалити збережений пріоритет", "Remove saved priority"), new Guid("CCEA3DFA-C927-4E2E-8B9F-D29C093A1E4F"))
        };
        ppEnum = new ExplorerCommandEnumerator(commands);
        return S_OK;
    }

    static string Text(string ru, string uk, string en)
    {
        var twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return twoLetter switch
        {
            "uk" => uk,
            "ru" => ru,
            _ => en
        };
    }

    static string FindAppExecutable()
    {
        var dir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(dir, "PriorityManagerX", "PriorityManagerX.exe"), // sparse package layout
            Path.Combine(dir, "PriorityManagerX.exe"),                      // same directory
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Priority Manager X", "PriorityManagerX.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return string.Empty;
    }

    static string FindIconPath()
    {
        var app = FindAppExecutable();
        if (!string.IsNullOrWhiteSpace(app) && File.Exists(app))
            return app;

        var comHost = Path.Combine(AppContext.BaseDirectory, "PriorityManagerX.ShellExtension.comhost.dll");
        if (File.Exists(comHost))
            return comHost;

        return string.Empty;
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    sealed class PriorityActionCommand(string priority, string title, Guid canonicalId) : BaseActionCommand(title, canonicalId)
    {
        protected override string BuildArgs(string selectedPath) => $"--set-default \"{selectedPath}\" {priority}";
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    sealed class RemovePriorityCommand(string title, Guid canonicalId) : BaseActionCommand(title, canonicalId)
    {
        protected override string BuildArgs(string selectedPath) => $"--remove-default \"{selectedPath}\"";
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    abstract class BaseActionCommand(string title, Guid canonicalId) : IExplorerCommand
    {
        const int S_OK = 0;
        const int S_FALSE = 1;
        const int E_NOTIMPL = unchecked((int)0x80004001);

        public int GetTitle(IShellItemArray? psiItemArray, out IntPtr ppszName)
        {
            ppszName = Marshal.StringToCoTaskMemUni(title);
            return S_OK;
        }

        public int GetIcon(IShellItemArray? psiItemArray, out IntPtr ppszIcon)
        {
            var iconPath = FindIconPath();
            if (string.IsNullOrWhiteSpace(iconPath))
            {
                ppszIcon = IntPtr.Zero;
                return E_NOTIMPL;
            }

            ppszIcon = Marshal.StringToCoTaskMemUni($"{iconPath},0");
            return S_OK;
        }

        public int GetToolTip(IShellItemArray? psiItemArray, out IntPtr ppszInfotip)
        {
            ppszInfotip = IntPtr.Zero;
            return E_NOTIMPL;
        }

        public int GetCanonicalName(out Guid pguidCommandName)
        {
            pguidCommandName = canonicalId;
            return S_OK;
        }

        public int GetState(IShellItemArray? psiItemArray, int fOkToBeSlow, out EXPCMDSTATE pCmdState)
        {
            pCmdState = EXPCMDSTATE.ECS_ENABLED;
            return S_OK;
        }

        public int Invoke(IShellItemArray? psiItemArray, IBindCtx? pbc)
        {
            var target = ShellSelection.GetFirstPath(psiItemArray);
            if (string.IsNullOrWhiteSpace(target))
            {
                DiagnosticLog.Write("Action.Invoke: empty target");
                return S_FALSE;
            }

            var app = FindAppExecutable();
            if (string.IsNullOrWhiteSpace(app) || !File.Exists(app))
            {
                DiagnosticLog.Write($"Action.Invoke: app missing '{app}'");
                return S_FALSE;
            }

            var args = BuildArgs(target);
            DiagnosticLog.Write($"Action.Invoke: app='{app}', args='{args}'");
            var psi = new ProcessStartInfo(app, args)
            {
                UseShellExecute = true,
                CreateNoWindow = true
            };

            try
            {
                Process.Start(psi);
                DiagnosticLog.Write("Action.Invoke: started");
                return S_OK;
            }
            catch
            {
                DiagnosticLog.Write("Action.Invoke: start failed");
                return S_FALSE;
            }
        }

        public int GetFlags(out EXPCMDFLAGS pFlags)
        {
            pFlags = EXPCMDFLAGS.ECF_DEFAULT;
            return S_OK;
        }

        public int EnumSubCommands(out IEnumExplorerCommand? ppEnum)
        {
            ppEnum = null;
            return E_NOTIMPL;
        }

        protected abstract string BuildArgs(string selectedPath);
    }
}

static class DiagnosticLog
{
    static readonly object Sync = new();

    public static void Write(string message)
    {
        try
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir))
                return;

            var dir = Path.Combine(baseDir, "PriorityManagerX");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "shell-extension.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";

            lock (Sync)
            {
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch
        {
        }
    }
}

static class ShellSelection
{
    const int S_OK = 0;

    public static string? GetFirstPath(IShellItemArray? shellItemArray)
    {
        if (shellItemArray == null)
            return null;

        if (shellItemArray.GetCount(out var count) != S_OK || count == 0)
            return null;

        if (shellItemArray.GetItemAt(0, out var item) != S_OK || item == null)
            return null;

        if (item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var ptr) != S_OK || ptr == IntPtr.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(ptr);
        }
    }
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
sealed class ExplorerCommandEnumerator(IReadOnlyList<IExplorerCommand> commands) : IEnumExplorerCommand
{
    const int S_OK = 0;
    const int S_FALSE = 1;

    int index;

    public int Next(uint celt, out IExplorerCommand? pUICommand, out uint pceltFetched)
    {
        pUICommand = null;
        pceltFetched = 0;

        if (celt == 0)
            return S_FALSE;

        if (index >= commands.Count)
            return S_FALSE;

        pUICommand = commands[index++];
        pceltFetched = 1;
        return S_OK;
    }

    public int Skip(uint celt)
    {
        index = Math.Min(index + (int)celt, commands.Count);
        return index < commands.Count ? S_OK : S_FALSE;
    }

    public int Reset()
    {
        index = 0;
        return S_OK;
    }

    public int Clone(out IEnumExplorerCommand? ppEnum)
    {
        ppEnum = new ExplorerCommandEnumerator(commands) { index = index };
        return S_OK;
    }
}

[ComImport]
[Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IExplorerCommand
{
    [PreserveSig] int GetTitle(IShellItemArray? psiItemArray, out IntPtr ppszName);
    [PreserveSig] int GetIcon(IShellItemArray? psiItemArray, out IntPtr ppszIcon);
    [PreserveSig] int GetToolTip(IShellItemArray? psiItemArray, out IntPtr ppszInfotip);
    [PreserveSig] int GetCanonicalName(out Guid pguidCommandName);
    [PreserveSig] int GetState(IShellItemArray? psiItemArray, int fOkToBeSlow, out EXPCMDSTATE pCmdState);
    [PreserveSig] int Invoke(IShellItemArray? psiItemArray, IBindCtx? pbc);
    [PreserveSig] int GetFlags(out EXPCMDFLAGS pFlags);
    [PreserveSig] int EnumSubCommands(out IEnumExplorerCommand? ppEnum);
}

[ComImport]
[Guid("A88826F8-186F-4987-AADE-EA0CEF8FBFE8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IEnumExplorerCommand
{
    [PreserveSig] int Next(uint celt, out IExplorerCommand? pUICommand, out uint pceltFetched);
    [PreserveSig] int Skip(uint celt);
    [PreserveSig] int Reset();
    [PreserveSig] int Clone(out IEnumExplorerCommand? ppEnum);
}

[ComImport]
[Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItemArray
{
    [PreserveSig] int BindToHandler(IBindCtx pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);
    [PreserveSig] int GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int GetPropertyDescriptionList(ref PropertyKey keyType, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int GetAttributes(SIATTRIBFLAGS dwAttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
    [PreserveSig] int GetCount(out uint pdwNumItems);
    [PreserveSig] int GetItemAt(uint dwIndex, out IShellItem? ppsi);
    [PreserveSig] int EnumItems(out IntPtr ppenumShellItems);
}

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem
{
    [PreserveSig] int BindToHandler(IBindCtx pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int GetParent(out IShellItem? ppsi);
    [PreserveSig] int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
    [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    [PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
}

[StructLayout(LayoutKind.Sequential)]
public struct PropertyKey
{
    public Guid fmtid;
    public uint pid;
}

public enum SIATTRIBFLAGS
{
    SIATTRIBFLAGS_AND = 0x00000001,
    SIATTRIBFLAGS_OR = 0x00000002,
    SIATTRIBFLAGS_APPCOMPAT = 0x00000003,
    SIATTRIBFLAGS_MASK = 0x00000003,
    SIATTRIBFLAGS_ALLITEMS = 0x00004000
}

public enum SIGDN : uint
{
    SIGDN_FILESYSPATH = 0x80058000
}

[Flags]
public enum EXPCMDFLAGS
{
    ECF_DEFAULT = 0x000,
    ECF_HASSUBCOMMANDS = 0x001,
    ECF_HASSPLITBUTTON = 0x002
}

public enum EXPCMDSTATE
{
    ECS_ENABLED = 0x00000000,
    ECS_DISABLED = 0x00000001,
    ECS_HIDDEN = 0x00000002,
    ECS_CHECKBOX = 0x00000004,
    ECS_CHECKED = 0x00000008,
    ECS_RADIOCHECK = 0x00000010
}
