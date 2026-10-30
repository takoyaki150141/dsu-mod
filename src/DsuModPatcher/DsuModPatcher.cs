using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DsuMod
{
    internal static class Program
    {
        private const string PluginFileName = "DsuExt.dll";
        private const string PluginFileStem = "DsuExt";
        private const string PluginClassName = "DsuExt.DsuExtPlugin";
        private const string ModAppName = "Dynabook Support Utility Mod";
        private const string AppFolderName = "dynabook Support Utility";
        private const string ExeFileName = "dynabookSupportUtility.exe";

        private static string BackupDir;
        private static string TempDir;

        private static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ERROR] " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static int Run(string[] args)
        {
            string source = null;
            string dest = null;
            string plugin = null;
            bool restore = false;
            bool launch = true;
            bool hide = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--help" || arg == "-h" || arg == "/?")
                {
                    PrintUsage();
                    return 0;
                }
                if (arg == "--restore")
                {
                    restore = true;
                    continue;
                }
                if (arg == "--no-launch")
                {
                    launch = false;
                    continue;
                }
                if (arg == "--hide")
                {
                    hide = true;
                    continue;
                }

                string key;
                string value;
                if (!TrySplitOption(arg, out key, out value))
                {
                    Console.Error.WriteLine("[ERROR] Unknown argument: " + arg);
                    PrintUsage();
                    return 2;
                }
                if (value == null && i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    value = args[++i];
                }
                if (value == null)
                {
                    Console.Error.WriteLine("[ERROR] Missing value for " + key);
                    return 2;
                }

                switch (key.ToLowerInvariant())
                {
                    case "--source":
                        source = value;
                        break;
                    case "--dest":
                        dest = value;
                        break;
                    case "--plugin":
                        plugin = value;
                        break;
                    case "--backup-dir":
                        BackupDir = value;
                        break;
                    case "--temp-dir":
                        TempDir = value;
                        break;
                    default:
                        Console.Error.WriteLine("[ERROR] Unknown option: " + key);
                        return 2;
                }
            }

            if (dest == null)
            {
                dest = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DsuMod",
                    "App");
            }
            dest = Path.GetFullPath(dest);
            BackupDir = Path.GetFullPath(BackupDir ?? dest);
            TempDir = Path.GetFullPath(TempDir ?? Path.Combine(Path.GetTempPath(), "DsuMod"));
            Directory.CreateDirectory(BackupDir);
            Directory.CreateDirectory(TempDir);

            if (IsModAppRunning(dest))
            {
                if (restore)
                {
                    Console.Error.WriteLine("[ERROR] The modded app is running. Close it before restoring.");
                    return 1;
                }
                Console.WriteLine("[INFO] The modded app is already running. Nothing to do.");
                return 0;
            }

            if (restore)
            {
                return Restore(dest);
            }

            if (source == null)
            {
                source = FindSource();
            }
            if (source == null)
            {
                Console.Error.WriteLine("[ERROR] dynabook Support Utility package was not found.");
                Console.Error.WriteLine("        Specify it with --source \"C:\\Program Files\\WindowsApps\\...\\dynabook Support Utility\".");
                return 1;
            }
            source = Path.GetFullPath(source);

            if (plugin == null)
            {
                plugin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PluginFileName);
            }
            plugin = Path.GetFullPath(plugin);

            if (!File.Exists(Path.Combine(source, ExeFileName)))
            {
                Console.Error.WriteLine("[ERROR] " + ExeFileName + " was not found in: " + source);
                return 1;
            }
            if (!File.Exists(plugin))
            {
                Console.Error.WriteLine("[ERROR] Plugin was not found: " + plugin);
                return 1;
            }

            bool inPlace = string.Equals(
                Path.GetFullPath(source).TrimEnd('\\'),
                Path.GetFullPath(dest).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);

            Console.WriteLine("[1/4] Source : " + source);
            Console.WriteLine("      Dest   : " + dest);
            CopyDirectory(source, dest);
            if (inPlace)
            {
                ClearReadOnlyFile(Path.Combine(dest, ExeFileName));
            }
            else
            {
                ClearReadOnly(dest);
            }

            string exePath = Path.Combine(dest, ExeFileName);
            Console.WriteLine("[2/4] Checking patch state...");
            if (!IsPatched(exePath))
            {
                string backup = Path.Combine(BackupDir, ExeFileName + ".orig");
                File.Copy(exePath, backup, true);
                Console.WriteLine("[3/4] Patching " + ExeFileName + " ...");
                PatchExecutable(exePath);
            }
            else
            {
                Console.WriteLine("[3/4] Already patched. Skipping executable patch.");
            }

            string pluginDir = Path.Combine(dest, "Plugins");
            Directory.CreateDirectory(pluginDir);
            string pluginTarget = Path.Combine(pluginDir, PluginFileName);
            ClearReadOnlyFile(pluginTarget);
            File.Copy(plugin, pluginTarget, true);
            Console.WriteLine("[4/4] Installed plugin: " + pluginTarget);

            if (launch)
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = exePath;
                info.WorkingDirectory = dest;
                info.UseShellExecute = true;
                if (hide)
                {
                    info.Arguments = "/hide";
                }
                Process.Start(info);
                Console.WriteLine("Launched: " + exePath);
            }
            else
            {
                Console.WriteLine("Launch skipped (--no-launch).");
            }

            return 0;
        }

        private static bool TrySplitOption(string arg, out string key, out string value)
        {
            key = null;
            value = null;
            if (!arg.StartsWith("--"))
            {
                return false;
            }
            int eq = arg.IndexOf('=');
            if (eq < 0)
            {
                key = arg;
                return true;
            }
            key = arg.Substring(0, eq);
            value = arg.Substring(eq + 1);
            return true;
        }

        private static string FindSource()
        {
            string fromAppx = FindSourceFromAppx();
            if (fromAppx != null)
            {
                return fromAppx;
            }

            try
            {
                string windowsApps = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "WindowsApps");
                if (!Directory.Exists(windowsApps))
                {
                    return null;
                }

                string[] candidates = Directory.GetDirectories(
                    windowsApps,
                    "7906AAC0.dynabookSupportUtility_*_x64__*");
                if (candidates.Length == 0)
                {
                    return null;
                }

                string best = null;
                Version bestVersion = null;
                for (int i = 0; i < candidates.Length; i++)
                {
                    Version version = GetPackageVersion(Path.GetFileName(candidates[i]));
                    if (best == null || (version != null && (bestVersion == null || version > bestVersion)))
                    {
                        best = candidates[i];
                        bestVersion = version;
                    }
                }

                string app = Path.Combine(best, AppFolderName);
                return Directory.Exists(app) ? app : null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string FindSourceFromAppx()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "powershell.exe";
                info.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"(Get-AppxPackage -Name '7906AAC0.dynabookSupportUtility' | Sort-Object Version -Descending | Select-Object -First 1).InstallLocation\"";
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;

                using (Process process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    output = output.Trim();
                    if (output.Length == 0 || !Directory.Exists(output))
                    {
                        return null;
                    }
                    string app = Path.Combine(output, AppFolderName);
                    return Directory.Exists(app) ? app : null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static Version GetPackageVersion(string folderName)
        {
            string[] parts = folderName.Split('_');
            if (parts.Length < 2)
            {
                return null;
            }
            Version version;
            return Version.TryParse(parts[1], out version) ? version : null;
        }

        private static void CopyDirectory(string source, string dest)
        {
            string sourceFull = Path.GetFullPath(source).TrimEnd('\\');
            string destFull = Path.GetFullPath(dest).TrimEnd('\\');
            if (string.Equals(sourceFull, destFull, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("      Source and destination are the same. Copy skipped.");
                return;
            }

            Directory.CreateDirectory(destFull);

            string[] directories = Directory.GetDirectories(sourceFull, "*", SearchOption.AllDirectories);
            for (int i = 0; i < directories.Length; i++)
            {
                string relative = directories[i].Substring(sourceFull.Length).TrimStart('\\');
                Directory.CreateDirectory(Path.Combine(destFull, relative));
            }

            string[] files = Directory.GetFiles(sourceFull, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(sourceFull.Length).TrimStart('\\');
                string target = Path.Combine(destFull, relative);
                File.Copy(files[i], target, true);
            }
        }

        private static void ClearReadOnly(string root)
        {
            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                FileAttributes attributes = File.GetAttributes(files[i]);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(files[i], attributes & ~FileAttributes.ReadOnly);
                }
            }
        }

        private static void ClearReadOnlyFile(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }

        private static bool IsPatched(string exePath)
        {
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(exePath);
            try
            {
                MethodDefinition pluginMethod = FindCalcPluginClassName(assembly);
                MethodDefinition mainMethod = FindMethod(assembly, "TSS.Program", "Main");
                return ContainsLdstr(pluginMethod, PluginClassName)
                    && ContainsLdstr(mainMethod, ModAppName);
            }
            finally
            {
                assembly.Dispose();
            }
        }

        private static bool ContainsLdstr(MethodDefinition method, string value)
        {
            if (method == null || !method.HasBody)
            {
                return false;
            }
            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Ldstr
                    && string.Equals((string)instruction.Operand, value, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void PatchExecutable(string exePath)
        {
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(exePath);
            string tempPath = Path.Combine(
                TempDir,
                ExeFileName + "." + Guid.NewGuid().ToString("N") + ".patched");
            try
            {
                MethodDefinition method = FindCalcPluginClassName(assembly);
                if (method == null || !method.HasBody)
                {
                    throw new InvalidOperationException("TSS.MainWindow.CalcPluginClassName was not found.");
                }

                Mono.Cecil.Cil.MethodBody body = method.Body;
                int toStringIndex = -1;
                for (int i = 0; i < body.Instructions.Count; i++)
                {
                    Instruction instruction = body.Instructions[i];
                    if (instruction.OpCode == OpCodes.Callvirt)
                    {
                        MethodReference reference = instruction.Operand as MethodReference;
                        if (reference != null && reference.Name == "ToString")
                        {
                            toStringIndex = i;
                            break;
                        }
                    }
                }
                if (toStringIndex <= 0)
                {
                    throw new InvalidOperationException("Patch target instruction was not found.");
                }

                Instruction target = body.Instructions[toStringIndex - 1];
                ILProcessor il = body.GetILProcessor();

                MethodReference equalsRef = assembly.MainModule.ImportReference(
                    typeof(string).GetMethod("Equals", new Type[] { typeof(string), typeof(StringComparison) }));
                MethodReference appendRef = assembly.MainModule.ImportReference(
                    typeof(System.Text.StringBuilder).GetMethod("Append", new Type[] { typeof(string) }));

                Instruction first = il.Create(OpCodes.Ldloc_2);
                Instruction branch = il.Create(OpCodes.Brfalse, target);

                Instruction lastConditional = FindLastConditionalBranch(body, target);
                if (lastConditional != null)
                {
                    lastConditional.Operand = first;
                }

                il.InsertBefore(target, first);
                il.InsertBefore(target, il.Create(OpCodes.Ldstr, PluginFileStem));
                il.InsertBefore(target, il.Create(OpCodes.Ldc_I4_5));
                il.InsertBefore(target, il.Create(OpCodes.Callvirt, equalsRef));
                il.InsertBefore(target, branch);
                il.InsertBefore(target, il.Create(OpCodes.Ldloc_3));
                il.InsertBefore(target, il.Create(OpCodes.Ldstr, PluginClassName));
                il.InsertBefore(target, il.Create(OpCodes.Callvirt, appendRef));
                il.InsertBefore(target, il.Create(OpCodes.Pop));

                PatchSingletonName(assembly);

                assembly.Write(tempPath, new WriterParameters { WriteSymbols = false });
            }
            finally
            {
                assembly.Dispose();
            }

            File.Copy(tempPath, exePath, true);
            File.Delete(tempPath);
        }

        private static Instruction FindLastConditionalBranch(Mono.Cecil.Cil.MethodBody body, Instruction target)
        {
            Instruction result = null;
            foreach (Instruction instruction in body.Instructions)
            {
                if (object.ReferenceEquals(instruction.Operand, target)
                    && instruction.OpCode.FlowControl == FlowControl.Cond_Branch)
                {
                    result = instruction;
                }
            }
            return result;
        }

        private static void PatchSingletonName(AssemblyDefinition assembly)
        {
            MethodDefinition mainMethod = FindMethod(assembly, "TSS.Program", "Main");
            if (mainMethod == null || !mainMethod.HasBody)
            {
                throw new InvalidOperationException("TSS.Program.Main was not found.");
            }

            IList<Instruction> instructions = mainMethod.Body.Instructions;
            for (int i = 1; i < instructions.Count; i++)
            {
                MethodReference reference = instructions[i].Operand as MethodReference;
                if (reference == null
                    || reference.Name != "Run"
                    || reference.DeclaringType.FullName != "Utility.SingletonApp"
                    || reference.Parameters.Count != 3)
                {
                    continue;
                }

                MethodReference titleCall = instructions[i - 1].Operand as MethodReference;
                if (titleCall != null && titleCall.Name == "get_IDS_TITLE_TSS")
                {
                    instructions[i - 1].OpCode = OpCodes.Ldstr;
                    instructions[i - 1].Operand = ModAppName;
                    return;
                }
            }

            throw new InvalidOperationException("Singleton application name was not found.");
        }

        private static MethodDefinition FindMethod(AssemblyDefinition assembly, string typeName, string methodName)
        {
            foreach (TypeDefinition type in assembly.MainModule.Types)
            {
                if (type.FullName != typeName)
                {
                    continue;
                }
                foreach (MethodDefinition method in type.Methods)
                {
                    if (method.Name == methodName)
                    {
                        return method;
                    }
                }
            }
            return null;
        }

        private static MethodDefinition FindCalcPluginClassName(AssemblyDefinition assembly)
        {
            foreach (TypeDefinition type in assembly.MainModule.Types)
            {
                if (type.FullName != "TSS.MainWindow")
                {
                    continue;
                }
                foreach (MethodDefinition method in type.Methods)
                {
                    if (method.Name == "CalcPluginClassName")
                    {
                        return method;
                    }
                }
            }
            return null;
        }

        private static bool IsModAppRunning(string dest)
        {
            string expected = Path.Combine(Path.GetFullPath(dest), ExeFileName);
            Process[] processes = Process.GetProcessesByName("dynabookSupportUtility");
            try
            {
                for (int i = 0; i < processes.Length; i++)
                {
                    try
                    {
                        string path = processes[i].MainModule.FileName;
                        if (string.Equals(path, expected, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }
                return false;
            }
            finally
            {
                for (int i = 0; i < processes.Length; i++)
                {
                    processes[i].Dispose();
                }
            }
        }

        private static int Restore(string dest)
        {
            string exePath = Path.Combine(dest, ExeFileName);
            string backup = Path.Combine(BackupDir, ExeFileName + ".orig");
            if (!File.Exists(backup))
            {
                Console.Error.WriteLine("[ERROR] Backup was not found: " + backup);
                return 1;
            }

            ClearReadOnlyFile(exePath);
            File.Copy(backup, exePath, true);
            string plugin = Path.Combine(dest, "Plugins", PluginFileName);
            if (File.Exists(plugin))
            {
                ClearReadOnlyFile(plugin);
                File.Delete(plugin);
            }
            Console.WriteLine("Restored: " + exePath);
            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("DsuModPatcher");
            Console.WriteLine("  --source <dir>   Installed app dir (auto-detected by default)");
            Console.WriteLine("  --dest <dir>     Working copy (default: %LOCALAPPDATA%\\DsuMod\\App)");
            Console.WriteLine("  --plugin <file>  Plugin DLL (default: DsuExt.dll next to this exe)");
            Console.WriteLine("  --backup-dir <dir>  Backup directory (default: dest)");
            Console.WriteLine("  --temp-dir <dir>    Temp directory (default: %TEMP%\\DsuMod)");
            Console.WriteLine("  --hide           Launch with /hide");
            Console.WriteLine("  --no-launch      Copy and patch only");
            Console.WriteLine("  --restore        Restore the backed-up exe and remove the plugin");
        }
    }
}

