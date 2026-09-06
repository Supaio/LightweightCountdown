using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("轻量倒计时")]
[assembly: AssemblyDescription("简约、低占用的 Windows 倒计时工具")]
[assembly: AssemblyCompany("Local")]
[assembly: AssemblyProduct("轻量倒计时")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: ComVisible(false)]

namespace LightweightCountdown
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool startedWithWindows = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--startup", StringComparison.OrdinalIgnoreCase))
                {
                    startedWithWindows = true;
                    break;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WidgetForm(startedWithWindows));
        }
    }
}
