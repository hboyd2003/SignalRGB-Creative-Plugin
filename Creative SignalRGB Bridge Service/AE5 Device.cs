using System.Runtime.InteropServices;
using System;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Creative_SignalRGB_Bridge_Service
{
    // ReSharper disable once InconsistentNaming
    public class AE5_Device : ICreativeDevice
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, ref uint requiredSize, IntPtr deviceInfoData);

        [StructLayout(LayoutKind.Sequential)]
        // ReSharper disable once InconsistentNaming
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid interfaceClassGuid;
            public int flags;
            private readonly IntPtr reserved;
        }

        const uint DIGCF_PRESENT = 0x02;
        const uint DIGCF_DEVICEINTERFACE = 0x10;


        private SafeFileHandle _deviceHandle = null;
        private IntPtr _lpInBuffer;
        private IntPtr _lpOutBuffer;

        public string DevicePath
        {
            get;
            private set;
        }

        public string DeviceName
        {
            get;
            private set;
        } = "SoundblasterX AE-5";

        public bool DeviceConnected
        {
            get;
            private set;
        }

        public async Task<bool> DiscoverDeviceAsync()
        {
            var deviceInterfaceClassGuid = new Guid("c37acb87-d563-4aa0-b761-996e7864af79"); //Unknown if the Interface Class GUID is unique to AE-5.

            var deviceInfoSet = SetupDiGetClassDevs(ref deviceInterfaceClassGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (deviceInfoSet == IntPtr.Zero)
            {
                return false;
            }

            var deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
            deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);

            if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref deviceInterfaceClassGuid, 0,
                    ref deviceInterfaceData))
            {
                return false;
            }

            uint requiredSize = 0;
            SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, IntPtr.Zero, 0, ref requiredSize, IntPtr.Zero);

            var detailDataBuffer = Marshal.AllocHGlobal((int)requiredSize);

            Marshal.WriteInt32(detailDataBuffer, (IntPtr.Size == 4) ? (4 + Marshal.SystemDefaultCharSize) : 8);

            if (SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, detailDataBuffer, requiredSize, ref requiredSize, IntPtr.Zero))
            {
                var pDevicePathName = new IntPtr(detailDataBuffer.ToInt64() + 4);
                var devicePath = Marshal.PtrToStringAuto(pDevicePathName);


                //Debug.WriteLine("Device path: " + devicePath);
                DevicePath = devicePath;
                return true;
                //openDevice(devicePath);
            }


            Marshal.FreeHGlobal(detailDataBuffer);
            return false;
        }

        public async Task<bool> SendCommand(byte[] command)
        {
            if (!DeviceConnected)
            {
                return false;
            }

            uint IOCTL_CODE = 0x77772400; // Believed to be the set RGB Code
            uint nInBufferSize = 1044;
            Marshal.Copy(command, 0, _lpInBuffer, command.Length);
            var success = DeviceIoControl(_deviceHandle, IOCTL_CODE, _lpInBuffer, nInBufferSize, _lpOutBuffer, 1044,
                out var bytesReturned, IntPtr.Zero);
            return success;

        }


        public async Task<bool> ConnectToDevice()
        {
            _deviceHandle = CreateFile(DevicePath, 0xc0000000, 0x00000003, IntPtr.Zero, 0x00000003, 0, IntPtr.Zero);
            if (_deviceHandle.IsInvalid)
            {
                //Send out error
                return false;
            }
            //Create buffers

            var nInputBuffer = new byte[1044];
            var inputHandle = GCHandle.Alloc(nInputBuffer, GCHandleType.Pinned);
            _lpInBuffer = inputHandle.AddrOfPinnedObject();
            //Marshal.Copy(nInputBuffer, 0, lpInBuffer, nInputBuffer.Length);

            var nOutBuffer = new byte[1044];
            var outputHandle = GCHandle.Alloc(nOutBuffer, GCHandleType.Pinned);
            _lpOutBuffer = outputHandle.AddrOfPinnedObject();
            Marshal.Copy(_lpOutBuffer, nOutBuffer, 0, nOutBuffer.Length);

            DeviceConnected = true;

            return true;
        }
    }
}