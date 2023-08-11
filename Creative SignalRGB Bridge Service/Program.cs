using System.ServiceProcess;

namespace Creative_SignalRGB_Bridge_Service
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Creative_SignalRGB_Bridge_Service()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
