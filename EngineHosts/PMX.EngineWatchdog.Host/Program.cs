using System;
using System.Text;

namespace PriorityManagerX
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            CoreEngineHost.RunWatchdog();
        }
    }
}
