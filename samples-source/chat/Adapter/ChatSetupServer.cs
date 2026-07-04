using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chat.Adapter
{
    public class ChatSetupAdapter
    {
        public static Process serverProcess;

        static ChatSetupAdapter()
        {
        }

        public static void Kill()
        {
            if (serverProcess != null)
            {
                try
                {
                    serverProcess.Kill();
                }
                catch (Exception e)
                {
                    Assert.Inconclusive("cannot stop chat server process: " + e.Message);
                }
                serverProcess = null;
            }

        }

        #region ChatSetupAdapter Members

        public static void StartServer()
        {
            // To run on-the-fly testing, please change the serverName to the absolute path of the server program
            string serverName = "server.exe";
            ProcessStartInfo info = new ProcessStartInfo(serverName);
            info.UseShellExecute = true;
            info.CreateNoWindow = false;
            info.WindowStyle = ProcessWindowStyle.Normal;
            serverProcess = new Process();
            serverProcess.StartInfo = info;
            try
            {
                serverProcess.Start();
                System.Threading.Thread.Sleep(1000);
            }
            catch (Exception e)
            {
                serverProcess = null;
                Assert.Inconclusive("cannot start chat server process: " + e.Message);
            }
        }


        #endregion
    }
}
