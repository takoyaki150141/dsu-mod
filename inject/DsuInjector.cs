using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DsuMod
{
    internal static class Program
    {
        private const uint ProcessCreateThread = 0x0002;
        private const uint ProcessQueryInformation = 0x0400;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint ProcessVmOperation = 0x0008;
        private const uint ProcessVmWrite = 0x0020;
        private const uint ProcessVmRead = 0x0010;
        private const uint MemCommit = 0x1000;
        private const uint MemReserve = 0x2000;
        private const uint MemRelease = 0x8000;
        private const uint PageReadWrite = 0x04;

        private static readonly string LocalRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DsuMod");
        private static readonly string LocalInject = Path.Combine(LocalRoot, "inject");
        private static readonly string InjectorLogPath = Path.Combine(LocalInject, "injector.log");
        private static readonly string InjectorStatusPath = Path.Combine(LocalInject, "injector.status");

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, IntPtr size, uint allocationType, uint protect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, IntPtr size, uint freeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, IntPtr size, out IntPtr written);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr process, IntPtr threadAttributes, IntPtr stackSize, IntPtr startAddress, IntPtr parameter, uint creationFlags, IntPtr threadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr thread, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process(IntPtr process, out bool wow64);

        private static int Main(string[] args)
        {
            if (IsInsideTarget())
            {
                return InjectorPayload.RunInjected(args);
            }

            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Log("fatal: " + ex);
                Console.Error.WriteLine("[ERROR] " + ex.Message);
                return 1;
            }
        }

        private static bool IsInsideTarget()
        {
            try
            {
                Process current = Process.GetCurrentProcess();
                string path = current.MainModule.FileName;
                return current.ProcessName.Equals("dynabookSupportUtility", StringComparison.OrdinalIgnoreCase)
                    && path.IndexOf("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static int Run(string[] args)
        {
            int processId = 0;
            string pluginSource = null;
            bool noLaunch = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--pid" && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out processId);
                }
                else if (args[i] == "--plugin" && i + 1 < args.Length)
                {
                    pluginSource = args[++i];
                }
                else if (args[i] == "--no-launch")
                {
                    noLaunch = true;
                }
                else if (args[i] == "--help" || args[i] == "-h" || args[i] == "/?")
                {
                    PrintUsage();
                    return 0;
                }
            }

            Directory.CreateDirectory(LocalInject);
            File.Delete(Path.Combine(LocalInject, "payload.status"));
            File.Delete(InjectorStatusPath);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string bootstrapSource = Path.Combine(baseDir, "DsuBootstrap.dll");
            if (pluginSource == null)
            {
                pluginSource = Path.Combine(baseDir, "DsuExt.dll");
                if (!File.Exists(pluginSource))
                {
                    pluginSource = Path.Combine(baseDir, "..", "dist", "DsuExt.dll");
                }
            }

            if (!File.Exists(bootstrapSource)) { Fail("DsuBootstrap.dll was not found: " + bootstrapSource); return 1; }
            if (!File.Exists(pluginSource)) { Fail("DsuExt.dll was not found: " + pluginSource); return 1; }

            Process target = processId > 0 ? Process.GetProcessById(processId) : FindTarget();
            if (target == null && !noLaunch)
            {
                LaunchStoreApp();
                target = WaitForTarget(30);
            }
            if (target == null)
            {
                Fail("dynabookSupportUtility.exe was not found.");
                return 1;
            }

            Log("target pid=" + target.Id + " path=" + SafePath(target));

            string injectedMarker = Path.Combine(LocalInject, "injected_" + target.Id + ".txt");
            if (File.Exists(injectedMarker))
            {
                File.WriteAllText(InjectorStatusPath, "OK:ALREADY");
                Console.WriteLine("Plugin is already injected into pid " + target.Id + ".");
                return 0;
            }

            string selfPath = Process.GetCurrentProcess().MainModule.FileName;
            string localSelf = Path.Combine(LocalInject, "DsuInjector.exe");
            string localBootstrap = Path.Combine(LocalInject, "DsuBootstrap.dll");
            string localPlugin = Path.Combine(LocalRoot, "DsuExt.dll");
            if (!string.Equals(selfPath, localSelf, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(selfPath, localSelf, true);
            }
            File.Copy(bootstrapSource, localBootstrap, true);
            File.Copy(pluginSource, localPlugin, true);

            if (!Inject(target, localBootstrap))
            {
                Fail("DLL injection failed.");
                return 1;
            }

            int result = WaitForPayloadResult(90);
            bool marker = File.Exists(Path.Combine(LocalRoot, "DsuExt.loaded"));
            Log("payload result=" + result + " marker=" + marker);

            if (result == 0 || result == 10)
            {
                File.WriteAllText(InjectorStatusPath, "OK:" + result + ":marker=" + marker);
                Console.WriteLine("Injection succeeded. result=" + result + " marker=" + marker);
                return 0;
            }

            File.WriteAllText(InjectorStatusPath, "FAILED:" + result + ":marker=" + marker);
            Console.Error.WriteLine("Injection failed. result=" + result);
            Console.Error.WriteLine("See: " + InjectorLogPath);
            Console.Error.WriteLine("See: " + Path.Combine(LocalInject, "bootstrap.log"));
            Console.Error.WriteLine("See: " + Path.Combine(LocalInject, "payload.log"));
            return 1;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("DsuInjector");
            Console.WriteLine("  --pid <id>       Inject into the specified process");
            Console.WriteLine("  --plugin <file>  Plugin DLL");
            Console.WriteLine("  --no-launch      Do not launch the Store app");
        }

        private static Process FindTarget()
        {
            Process[] processes = Process.GetProcessesByName("dynabookSupportUtility");
            for (int i = 0; i < processes.Length; i++)
            {
                try
                {
                    string path = processes[i].MainModule.FileName;
                    if (path.EndsWith("dynabookSupportUtility.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        return processes[i];
                    }
                }
                catch
                {
                }
            }
            return processes.Length > 0 ? processes[0] : null;
        }

        private static Process WaitForTarget(int seconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(seconds);
            while (DateTime.Now < deadline)
            {
                Process target = FindTarget();
                if (target != null)
                {
                    return target;
                }
                Thread.Sleep(500);
            }
            return null;
        }

        private static void LaunchStoreApp()
        {
            string info = RunPowerShell(
                "$p=Get-AppxPackage -Name '7906AAC0.dynabookSupportUtility'|Sort-Object Version -Descending|Select-Object -First 1; if($p){ Write-Output ($p.InstallLocation + '|' + $p.PackageFamilyName) }");
            if (string.IsNullOrEmpty(info))
            {
                throw new InvalidOperationException("Store package information was not found.");
            }

            string[] parts = info.Trim().Split('|');
            string installLocation = parts[0];
            string familyName = parts.Length > 1 ? parts[1] : null;
            Log("launch package=" + installLocation + " family=" + familyName);

            if (!string.IsNullOrEmpty(familyName))
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = "explorer.exe";
                start.Arguments = "shell:AppsFolder\\" + familyName + "!dynabook.SupportUtility";
                start.UseShellExecute = true;
                Process.Start(start);
            }
            else
            {
                string exe = Path.Combine(installLocation, "dynabook Support Utility", "dynabookSupportUtility.exe");
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = exe;
                start.Arguments = "/hide";
                start.WorkingDirectory = Path.GetDirectoryName(exe);
                start.UseShellExecute = true;
                Process.Start(start);
            }
        }

        private static bool Inject(Process target, string bootstrapPath)
        {
            IntPtr process = OpenProcess(
                ProcessCreateThread | ProcessQueryInformation | ProcessQueryLimitedInformation | ProcessVmOperation | ProcessVmWrite | ProcessVmRead,
                false,
                target.Id);
            if (process == IntPtr.Zero)
            {
                Fail("OpenProcess failed: " + Marshal.GetLastWin32Error());
                return false;
            }

            try
            {
                bool wow64;
                if (IsWow64Process(process, out wow64) && (wow64 || IntPtr.Size != 8))
                {
                    Fail("Target/injector architecture mismatch.");
                    return false;
                }

                byte[] pathBytes = Encoding.Unicode.GetBytes(bootstrapPath + "\0");
                IntPtr remotePath = VirtualAllocEx(process, IntPtr.Zero, new IntPtr(pathBytes.Length), MemCommit | MemReserve, PageReadWrite);
                if (remotePath == IntPtr.Zero)
                {
                    Fail("VirtualAllocEx failed: " + Marshal.GetLastWin32Error());
                    return false;
                }

                try
                {
                    IntPtr written;
                    if (!WriteProcessMemory(process, remotePath, pathBytes, new IntPtr(pathBytes.Length), out written))
                    {
                        Fail("WriteProcessMemory failed: " + Marshal.GetLastWin32Error());
                        return false;
                    }

                    IntPtr loadLibrary = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
                    if (loadLibrary == IntPtr.Zero)
                    {
                        Fail("GetProcAddress(LoadLibraryW) failed.");
                        return false;
                    }

                    IntPtr thread = CreateRemoteThread(process, IntPtr.Zero, IntPtr.Zero, loadLibrary, remotePath, 0, IntPtr.Zero);
                    if (thread == IntPtr.Zero)
                    {
                        Fail("CreateRemoteThread failed: " + Marshal.GetLastWin32Error());
                        return false;
                    }

                    try
                    {
                        uint wait = WaitForSingleObject(thread, 10000);
                        uint exitCode;
                        GetExitCodeThread(thread, out exitCode);
                        Log("remote thread wait=" + wait + " module=0x" + exitCode.ToString("X"));
                        if (wait != 0 || exitCode == 0)
                        {
                            Fail("LoadLibraryW in target failed.");
                            return false;
                        }
                    }
                    finally
                    {
                        CloseHandle(thread);
                    }
                }
                finally
                {
                    VirtualFreeEx(process, remotePath, IntPtr.Zero, MemRelease);
                }
            }
            finally
            {
                CloseHandle(process);
            }

            return true;
        }

        private static int WaitForPayloadResult(int seconds)
        {
            string path = Path.Combine(LocalInject, "payload.status");
            DateTime deadline = DateTime.Now.AddSeconds(seconds);
            while (DateTime.Now < deadline)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        string text = File.ReadAllText(path).Trim();
                        int result;
                        if (int.TryParse(text, out result))
                        {
                            return result;
                        }
                    }
                    catch
                    {
                    }
                }
                Thread.Sleep(500);
            }
            return -1;
        }

        private static string RunPowerShell(string command)
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "powershell.exe";
            info.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + command + "\"";
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output.Trim();
            }
        }

        private static string SafePath(Process process)
        {
            try
            {
                return process.MainModule.FileName;
            }
            catch
            {
                return "";
            }
        }

        private static void Fail(string message)
        {
            Log("error: " + message);
            Console.Error.WriteLine("[ERROR] " + message);
        }

        private static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(LocalInject);
                File.AppendAllText(InjectorLogPath, DateTime.Now.ToString("o") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}

