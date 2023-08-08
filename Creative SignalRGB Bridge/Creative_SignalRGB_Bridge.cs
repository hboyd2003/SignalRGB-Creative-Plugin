using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

using System;
using System.ServiceProcess;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Creative_SignalRGB_Bridge
{
    public partial class Creative_SignalRGB_Bridge : ServiceBase
    {
        private const int ListenPort = 12345; 
        private const string SearchMessage = "service.broadcast(\"Z-SEARCH * \\r\\n\")";
        private UdpClient listener = null;

        public Creative_SignalRGB_Bridge()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            System.Diagnostics.Debugger.Launch();
            
            StartListening();
        }

        protected override void OnStop()
        {
            if (listener != null)
            {
                listener.Close();
            }
        }

        private bool discoverDevice()
        {
            return true;
        }

        private async void StartListening()
        {
            try
            {
                while (true)
                {
                    UdpReceiveResult result = await listener.ReceiveAsync();
                    HandleReceivedMessage(result);
                }
            }
            catch (SocketException se)
            {
                // Handle exceptions accordingly, e.g., write to an event log
            }
            catch (ObjectDisposedException)
            {
                // This exception will be thrown when stopping the service and closing the listener.
                // It can be safely ignored or used for logging.
            }
        }

        private void HandleReceivedMessage(UdpReceiveResult result)
        {
            string message = Encoding.UTF8.GetString(result.Buffer);

            System.Diagnostics.Debug.WriteLine(message);

            if (message.Trim().Equals(SearchMessage))
            {
                string responseMessage = "Your response here";
                byte[] responseData = Encoding.UTF8.GetBytes(responseMessage);
                listener.Send(responseData, responseData.Length, result.RemoteEndPoint);
            }
        }
    }
}
