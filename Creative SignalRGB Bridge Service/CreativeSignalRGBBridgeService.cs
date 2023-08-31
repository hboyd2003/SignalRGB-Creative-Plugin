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
using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;
using System.Text;
using System.Net.Sockets;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CreativeSignalRGBBridge;

public partial class CreativeSignalRGBBridgeService : BackgroundService
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
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
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

    private AE5_Device ae5;

    private readonly ILogger<CreativeSignalRGBBridgeService> _logger;

    public CreativeSignalRGBBridgeService(ILogger<CreativeSignalRGBBridgeService> logger)
    {
        _logger = logger;
        ae5 = new AE5_Device();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            //System.Diagnostics.Debugger.Launch();

            
            if (!DiscoverDevices())
            {
                Environment.Exit(0);
            }

            listener = new UdpClient(ListenPort);
            IPEndPoint groupEP = new IPEndPoint(IPAddress.Broadcast, ListenPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                HandleReceivedMessage(listener.Receive(ref groupEP));
            }
        }
        catch (TaskCanceledException)
        {
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);

            Environment.Exit(1);
        }
    }


    private bool DiscoverDevices()
    {
        // ReSharper disable once InconsistentNaming
        return ae5.DiscoverDeviceAsync().Result; 
    }


    private void HandleReceivedMessage(byte[] udpMessage)
    {
        var message = Encoding.UTF8.GetString(udpMessage);

        //Debug.WriteLine("Recieved Message: " + bytes);

        if (message.Trim().Equals(SearchMessage))
        {
            //Debug.WriteLine("Recieved Search Message");
            var responseMessage = ae5.DeviceName;   
            var responseData = Encoding.UTF8.GetBytes(responseMessage);
            var loopback = new IPEndPoint(IPAddress.Loopback, 12347);
            listener.Send(responseData, responseData.Length, loopback);

            if (!ae5.DeviceConnected)
            {
                _ = ae5.ConnectToDevice();
            } 
        } else
        {
            if (!ae5.DeviceConnected)
            {
                return;
            }

            var bytes = Convert.FromBase64String(message);
            if (bytes.Length == 1044 && bytes[0] == 3)
            {
                //Debug.WriteLine("BUFFER!");
                _ = ae5.SendCommand(bytes);
            }
        }
    }
}
