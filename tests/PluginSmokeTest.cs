using System;
using System.Windows;
using PluginLib;

namespace DsuModTest
{
    internal sealed class DummyContainer : IPluginContainer
    {
        public void EnableCheckForUpdates(Plugin sender, bool enable)
        {
        }

        public void ErrorLog(Plugin sender, SeverityLevel severity, string error)
        {
            Console.WriteLine("LOG " + severity + ": " + error);
        }

        public void PluginReady(Plugin sender)
        {
            Console.WriteLine("READY " + sender.Name);
        }

        public void ShowUserMessage(Plugin sender, string title, string message, UserMessageFlags flags, SeverityLevel severity)
        {
            Console.WriteLine("MESSAGE " + title + ": " + message);
        }

        public void SwitchPlugin(Plugin sender, string newPluginPage)
        {
        }

        public bool Visible()
        {
            return true;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            try
            {
                Application app = new Application();
                DsuExt.DsuExtPlugin plugin = new DsuExt.DsuExtPlugin();
                uint rc = plugin.Init(new DummyContainer());
                Console.WriteLine("Init=" + rc + " Name=" + plugin.Name + " Caption=" + plugin.Caption + " Pages=" + plugin.GetPluginPages().Length);

                rc = plugin.ProcessServerData("");
                Console.WriteLine("ProcessServerData=" + rc);

                rc = plugin.Start();
                Console.WriteLine("Start=" + rc);

                PluginPage[] pages = plugin.GetPluginPages();
                for (int i = 0; i < pages.Length; i++)
                {
                    Console.WriteLine("Page[" + i + "] Caption=" + pages[i].Caption + " Name=" + pages[i].PageName + " HasContent=" + (pages[i].Content != null));
                }

                rc = plugin.Stop(ActionCode.AC_STOP);
                Console.WriteLine("Stop=" + rc);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}
