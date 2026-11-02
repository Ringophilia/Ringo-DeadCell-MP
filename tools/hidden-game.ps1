# Dot-source this. Start-TestGame runs the DCCM game on a Windows desktop of its
# own, so a test never shows a window or takes focus from whatever the user is doing.
# The games are bound to a job: when the script ends, however it ends, they end too,
# and nothing is left running where nobody can see it.
# RINGO_VISIBLE=1 runs them on the normal desktop instead, for looking at them.
# DEADCELLS_DIR says where the game is installed, if not in the default Steam library.

$GameDir = if ($env:DEADCELLS_DIR) { $env:DEADCELLS_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\Dead Cells' }

Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class HiddenGame
{
    const uint GenericAll = 0x10000000;
    const uint CreateNoWindow = 0x08000000;
    const uint BelowNormalPriority = 0x00004000;
    const uint KillOnJobClose = 0x2000;
    const int ExtendedLimitInformation = 9;
    const string DesktopName = "RingoTests";

    static IntPtr desktop;
    static IntPtr job;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfo
    {
        public int cb;
        public string lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ProcessInformation
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ExtendedLimits
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateDesktopW(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr security);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessW(string app, string commandLine, IntPtr processSecurity, IntPtr threadSecurity,
        bool inheritHandles, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation info);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateJobObjectW(IntPtr security, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimits info, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);

    static void Check(bool ok)
    {
        if (!ok) throw new Win32Exception();
    }

    public static Process Start(string exe, string arguments, string directory)
    {
        if (desktop == IntPtr.Zero)
        {
            desktop = CreateDesktopW(DesktopName, IntPtr.Zero, IntPtr.Zero, 0, GenericAll, IntPtr.Zero);
            Check(desktop != IntPtr.Zero);
            job = CreateJobObjectW(IntPtr.Zero, null);
            Check(job != IntPtr.Zero);
            var limits = new ExtendedLimits { LimitFlags = KillOnJobClose };
            Check(SetInformationJobObject(job, ExtendedLimitInformation, ref limits, Marshal.SizeOf(limits)));
        }
        var startup = new StartupInfo { cb = Marshal.SizeOf(typeof(StartupInfo)), lpDesktop = "WinSta0\\" + DesktopName };
        ProcessInformation info;
        Check(CreateProcessW(exe, "\"" + exe + "\" " + arguments, IntPtr.Zero, IntPtr.Zero, false,
            CreateNoWindow | BelowNormalPriority, IntPtr.Zero, directory, ref startup, out info));
        Check(AssignProcessToJobObject(job, info.hProcess));
        var process = Process.GetProcessById(info.dwProcessId);
        CloseHandle(info.hThread);
        CloseHandle(info.hProcess);
        return process;
    }
}
'@

function Start-TestGame([string]$Startup, [string]$Arguments = '') {
    $exe = "$Startup\DeadCellsModding.exe"
    if ($env:RINGO_VISIBLE) { return Start-Process $exe -ArgumentList $Arguments -WorkingDirectory $Startup -PassThru }
    [HiddenGame]::Start($exe, $Arguments, $Startup)
}

# Only ever stops the games given to it: the user may be playing on this machine.
function Stop-TestGame($Games) {
    @($Games) | Where-Object { $_ -and -not $_.HasExited } | ForEach-Object { $_.Kill(); $null = $_.WaitForExit(5000) }
}
