using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using EasyDNS.Forms;

namespace EasyDNS
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Automatically request Administrator elevation if not already elevated
            if (!IsRunAsAdmin())
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    return; // Terminate non-elevated instance
                }
                catch
                {
                    // User declined UAC prompt
                    MessageBox.Show("EasyDNS requires Administrator privileges to modify network and DNS settings.\nPlease allow the Administrator prompt to run.", "EasyDNS - Administrator Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show("An unexpected error occurred: " + ex.Message, "EasyDNS Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsRunAsAdmin()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
