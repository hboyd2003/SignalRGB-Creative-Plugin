// This is the Creative SignalRGB Bridge Plugin/Service.
// Copyright © 2023 Harrison Boyd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>

using System;
using System.ServiceProcess;
using System.Text;
using System.Net.Sockets;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading.Tasks;

namespace Creative_SignalRGB_Bridge_Service
{
    public partial class Creative_SignalRGB_Bridge_Service : ServiceBase
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
            private readonly IntPtr reserved;
        }
        const uint DIGCF_PRESENT = 0x02;
        const uint DIGCF_DEVICEINTERFACE = 0x10;


        private const int ListenPort = 12346; 
        private const string SearchMessage = "LIST DEVICES";
        private UdpClient listener = null;
        private string devicePath;
        SafeFileHandle deviceHandle = null;
        
        IntPtr lpInBuffer;
        IntPtr lpOutBuffer;


        public Creative_SignalRGB_Bridge_Service()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Task.Run(() => Start());
            
        }

        protected override void OnStop()
        {
            listener?.Close();
        }

        private void Start()
        {
            //System.Diagnostics.Debugger.Launch();

            if (DiscoverDevice())
            {
                listener = new UdpClient(ListenPort);
                IPEndPoint groupEP = new IPEndPoint(IPAddress.Broadcast, ListenPort);

                while (true)
                {
                    HandleReceivedMessage(listener.Receive(ref groupEP));
                }

            }
            else
            {
                Stop();
            }
        }

        private bool DiscoverDevice()
        {
            //TODO: Add support for more devices than just the AE-5
            Guid deviceInterfaceClassGuid = new Guid("c37acb87-d563-4aa0-b761-996e7864af79"); //Unknown if the Interface Class GUID is unique to AE-5.

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


                        //Debug.WriteLine("Device path: " + devicePath);
                        this.devicePath = devicePath;
                        return true;
                        //openDevice(devicePath);
                    }
            

                    Marshal.FreeHGlobal(detailDataBuffer);
                }
            }
            return false;
        }

        private bool OpenDevice(string devicePath)
        {
            deviceHandle = CreateFile(devicePath, 0xc0000000, 0x00000003, IntPtr.Zero, 0x00000003, 0, IntPtr.Zero);
            if (deviceHandle.IsInvalid)
            {
                //Send out error
                return false;
            }
            //Create buffers
           
            byte[] nInputBuffer = new byte[1044];
            GCHandle inputHandle = GCHandle.Alloc(nInputBuffer, GCHandleType.Pinned);
            this.lpInBuffer = inputHandle.AddrOfPinnedObject();
            //Marshal.Copy(nInputBuffer, 0, lpInBuffer, nInputBuffer.Length);

            byte[] nOutBuffer = new byte[1044];
            GCHandle outputHandle = GCHandle.Alloc(nOutBuffer, GCHandleType.Pinned);
            this.lpOutBuffer = outputHandle.AddrOfPinnedObject();
            Marshal.Copy(lpOutBuffer, nOutBuffer, 0, nOutBuffer.Length);

            return true;
        }

        private void SendCommand(byte[] nInputBuffer)
        {
            uint IOCTL_CODE = 0x77772400; // Believed to be the set RGB Code
            uint nInBufferSize = 1044;
            Marshal.Copy(nInputBuffer, 0, lpInBuffer, nInputBuffer.Length);
            DeviceIoControl(deviceHandle, IOCTL_CODE, lpInBuffer, nInBufferSize, lpOutBuffer, 1044, out uint bytesReturned, IntPtr.Zero);
        }

        private void HandleReceivedMessage(byte[] udpMessage)
        {
            string message = Encoding.UTF8.GetString(udpMessage);

            //Debug.WriteLine("Recieved Message: " + bytes);

            if (message.Trim().Equals(SearchMessage))
            {
                //Debug.WriteLine("Recieved Search Message");
                string responseMessage = "Soundblaster AE-5";
                byte[] responseData = Encoding.UTF8.GetBytes(responseMessage);
                IPEndPoint loopback = new IPEndPoint(IPAddress.Loopback, 12347);
                listener.Send(responseData, responseData.Length, loopback);
            }
            if (deviceHandle == null)
            {
                OpenDevice(devicePath);
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(message);
                if (bytes.Length == 1044 && bytes[0] == 3)
                {
                    //Debug.WriteLine("BUFFER!");
                    SendCommand(bytes);
                }
            } catch { }
        }
    }
}
