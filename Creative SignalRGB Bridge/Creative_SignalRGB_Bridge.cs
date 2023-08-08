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
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Text;
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.IO;

namespace Creative_SignalRGB_Bridge
{
    public partial class Creative_SignalRGB_Bridge : ServiceBase
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, ref uint requiredSize, IntPtr deviceInfoData);

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid interfaceClassGuid;
            public int flags;
            private IntPtr reserved;
        }
        const uint DIGCF_PRESENT = 0x02;
        const uint DIGCF_DEVICEINTERFACE = 0x10;


        private const int ListenPort = 12345; 
        private const string SearchMessage = "Z-SEARCH *";
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
            Guid deviceInterfaceClassGuid = new Guid("c37acb87-d563-4aa0-b761-996e7864af79");

            IntPtr deviceInfoSet = SetupDiGetClassDevs(ref deviceInterfaceClassGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (deviceInfoSet != IntPtr.Zero)
            {
                SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
                deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);

                if (SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref deviceInterfaceClassGuid, 0, ref deviceInterfaceData))
                {
                    uint requiredSize = 0;
                    SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, IntPtr.Zero, 0, ref requiredSize, IntPtr.Zero);

                    IntPtr detailDataBuffer = Marshal.AllocHGlobal((int)requiredSize);

                    Marshal.WriteInt32(detailDataBuffer, (IntPtr.Size == 4) ? (4 + Marshal.SystemDefaultCharSize) : 8);

                    if (SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, detailDataBuffer, requiredSize, ref requiredSize, IntPtr.Zero))
                    {
                        IntPtr pDevicePathName = new IntPtr(detailDataBuffer.ToInt64() + 4);
                        string devicePath = Marshal.PtrToStringAuto(pDevicePathName);


                        Console.WriteLine("Device path: " + devicePath);
                        Console.ReadKey();
                        return true;
                        //openDevice(devicePath);
                    }
            

                    Marshal.FreeHGlobal(detailDataBuffer);
                }
            }
            return false;
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

            Debug.WriteLine(message);

            if (message.Trim().Equals(SearchMessage))
            {
                Debug.WriteLine("Recieved Search Message");
                string responseMessage = "Your response here";
                byte[] responseData = Encoding.UTF8.GetBytes(responseMessage);
                listener.Send(responseData, responseData.Length, result.RemoteEndPoint);
            }
        }
    }
}
