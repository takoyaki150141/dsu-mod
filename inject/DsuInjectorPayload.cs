using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DsuMod
{
    public static class InjectorPayload
    {
        private static readonly string BaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DsuMod",
            "inject");

        private static readonly string StatusPath = Path.Combine(BaseDir, "payload.status");
        private static readonly string LogPath = Path.Combine(BaseDir, "payload.log");

        public static int RunInjected(string[] args)
        {
            string pluginPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DsuMod",
                "DsuExt.dll");
            if (args != null && args.Length > 0)
            {
                pluginPath = args[0];
            }
            return Inject(pluginPath);
        }

        public static int Inject(string pluginPath)
        {
            int result = 99;
            try
            {
                result = WaitAndInject(pluginPath);
            }
            catch (Exception ex)
            {
                Log("exception: " + ex);
                result = 98;
            }

            try
            {
                Directory.CreateDirectory(BaseDir);
                File.WriteAllText(StatusPath, result.ToString());
                if (result == 0 || result == 10)
                {
                    int processId = Process.GetCurrentProcess().Id;
                    File.WriteAllText(Path.Combine(BaseDir, "injected_" + processId + ".txt"), result.ToString());
                }
            }
            catch
            {
            }

            return result;
        }

        private static int WaitAndInject(string pluginPath)
        {
            for (int i = 0; i < 80; i++)
            {
                Application application = Application.Current;
                if (application != null)
                {
                    int result = 3;
                    try
                    {
                        application.Dispatcher.Invoke(
                            new Action(delegate { result = InjectOnUiThread(application, pluginPath); }),
                            DispatcherPriority.Normal,
                            null);
                    }
                    catch (Exception ex)
                    {
                        Log("dispatcher exception: " + ex);
                        result = 4;
                    }

                    if (result == 0 || result == 10)
                    {
                        return result;
                    }
                    Log("inject attempt " + i + " result=" + result);
                }

                Thread.Sleep(500);
            }

            return 5;
        }

        private static int InjectOnUiThread(Application application, string pluginPath)
        {
            object mainWindow = null;
            foreach (Window window in application.Windows)
            {
                if (window != null && window.GetType().FullName == "TSS.MainWindow")
                {
                    mainWindow = window;
                    break;
                }
            }

            if (mainWindow == null)
            {
                return 2;
            }

            Type mainType = mainWindow.GetType();
            FieldInfo pluginsField = mainType.GetField("m_aPlugins", BindingFlags.Instance | BindingFlags.NonPublic);
            if (pluginsField == null)
            {
                return 6;
            }

            IList plugins = pluginsField.GetValue(mainWindow) as IList;
            if (plugins == null)
            {
                return 7;
            }

            foreach (object existing in plugins)
            {
                if (existing != null && existing.GetType().FullName == "DsuExt.DsuExtPlugin")
                {
                    return 10;
                }
            }

            MethodInfo addPlugin = mainType.GetMethod(
                "AddPlugin",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string), typeof(string) },
                null);
            if (addPlugin == null)
            {
                return 8;
            }

            int before = plugins.Count;
            addPlugin.Invoke(mainWindow, new object[] { pluginPath, "DsuExt.DsuExtPlugin" });
            if (plugins.Count <= before)
            {
                return 9;
            }

            object plugin = null;
            foreach (object item in plugins)
            {
                if (item != null && item.GetType().FullName == "DsuExt.DsuExtPlugin")
                {
                    plugin = item;
                    break;
                }
            }
            if (plugin == null)
            {
                return 11;
            }

            InvokeIfPresent(plugin, "ProcessServerData", new object[] { "" });
            InvokeIfPresent(plugin, "Start", null);

            MethodInfo showPluginButtons = mainType.GetMethod(
                "ShowPluginButtons",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (showPluginButtons != null)
            {
                showPluginButtons.Invoke(mainWindow, null);
            }

            MethodInfo[] methods = mainType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == "PluginReady" && methods[i].GetParameters().Length == 1)
                {
                    methods[i].Invoke(mainWindow, new object[] { plugin });
                    break;
                }
            }

            return 0;
        }

        private static void InvokeIfPresent(object target, string name, object[] arguments)
        {
            try
            {
                MethodInfo method = target.GetType().GetMethod(name);
                if (method != null)
                {
                    method.Invoke(target, arguments);
                }
            }
            catch (Exception ex)
            {
                Log(name + " exception: " + ex);
            }
        }

        private static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(BaseDir);
                File.AppendAllText(LogPath, DateTime.Now.ToString("o") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}

