using System;
using System.Windows.Forms;
using System.Diagnostics;

namespace SystemInfoApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Check for multiple instances in the SAME user session only
            var currentProcess = Process.GetCurrentProcess();
            var currentSessionId = currentProcess.SessionId;
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);

            // Count only processes in the same session (allows multiple users to run their own instance)
            int instancesInSession = 0;
            foreach (var process in processes)
            {
                if (process.SessionId == currentSessionId)
                    instancesInSession++;
            }

            if (instancesInSession > 1)
            {
                MessageBox.Show(
                    "System Info is already running in this session.",
                    "Already Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            Application.Run(new TrayApplicationContext());
        }
    }
}
