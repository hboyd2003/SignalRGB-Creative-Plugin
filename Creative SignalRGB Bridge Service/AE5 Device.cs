using System.Runtime.InteropServices;
using Windows.Gaming.Input.Custom;
using Microsoft.Win32.SafeHandles;
using Microsoft.Extensions.Logging;
using Windows.Devices.Enumeration;
using System.Security.Cryptography;
using Windows.Devices.Custom;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;
using Windows.Security.Cryptography;
using System.Text.RegularExpressions;

namespace CreativeSignalRGBBridge;
    // ReSharper disable once InconsistentNaming
public partial class AE5_Device : CreativeDevice, ICreativeDevice
{
    private CustomDevice? _device = null;
    public static readonly Guid interfaceGUID = new("{c37acb87-d563-4aa0-b761-996e7864af79}");
    public static string DeviceSelector => CustomDevice.GetDeviceSelector(interfaceGUID);

    [GeneratedRegex(@"(?<=\d{4}\\)[\w\d&]+")]
    // ReSharper disable once InconsistentNaming
    private static partial Regex UUIDRegex();

    public override string DeviceName
    {
        get;
        protected set;
    } = "SoundblasterX AE-5";

    public ILogger<CreativeSignalRGBBridgeService>? Logger
    {
        get;
        set;
    }


    public AE5_Device(DeviceInformation deviceInformation)
    {
        Logger?.LogError("CreativeDevice Found!");

        DeviceInstancePath = deviceInformation.Id;
        UUID = UUIDRegex().Match((string)deviceInformation.Properties["System.Devices.DeviceInstanceId"]).Value;


        // Gets the name of the device
        // Unfortunately it seems impossible to get the actual CreativeDevice instead of the DeviceInterface using the VID and PID.
        // So we use the CreativeDevice ID we find to get the parent device which has the Actual Name of the device.
        var propertiesToQuery = new List<string>() {
            "System.ItemNameDisplay",
            "System.Devices.DeviceInstanceId",
            "System.Devices.Parent",
            "System.Devices.LocationPaths",
            "System.Devices.Children"
        };
        var device = DeviceInformation.FindAllAsync($"System.Devices.DeviceInstanceId:=\"{deviceInformation.Properties["System.Devices.DeviceInstanceId"]}\"", propertiesToQuery,
            DeviceInformationKind.Device).GetResults();
        if (device.Count > 0)
        {
            DeviceName = device[0].Name;
        }
        else
        {
            Logger?.LogWarning("Unable to get device name from device");
        }
    }

    // Needed as the second custom flag needs to be set to one and the IOControlCode constructor can't
    // See https://learn.microsoft.com/en-us/windows-hardware/drivers/kernel/defining-i-o-control-codes
    private class IOCTLControlCode : IIOControlCode
    {
        IOControlAccessMode IIOControlCode.AccessMode
        {
            get;
        } = IOControlAccessMode.ReadWrite;


        public IOControlBufferingMethod BufferingMethod
        {
            get;
        } = IOControlBufferingMethod.Buffered;

        public uint ControlCode
        {
            get;
        } = 0x77772400;

        public ushort DeviceType
        {
            get;
        } = 0x7777;

        public ushort Function
        {
            get;
        } = 0x100;
    }

    // TODO: Proper async for AE5's SendCommand
    public override async Task<bool> SendCommandAsync(byte[] command)
    {
        if (DeviceInstancePath == null || !DeviceConnected || _device == null)
        {
            return false;
        }
        //Logger?.LogError("Sending Command!");
        var inputBuffer = CryptographicBuffer.CreateFromByteArray(command);
        var outputBuffer = new Buffer(1044);
        uint success;
        try
        {
            success = await _device.SendIOControlAsync(new IOCTLControlCode(), inputBuffer, outputBuffer);
        }
        catch (Exception)
        {
            Logger?.LogError("Error sending command disconnecting!");
            _ = DisconnectFromDeviceAsync();
            return false;
        }
        //Logger?.LogError("Sent command!");
        return success == 0;

    }

    // TODO: Proper async for AE5's ConnectToDevice
    public override async Task<bool> ConnectToDeviceAsync()
    {
        Logger?.LogError("Connecting to the CreativeDevice!");
        if (DeviceInstancePath == null || DeviceConnected)
        {
            return false;
        }

        try
        {
            _device = await CustomDevice.FromIdAsync(DeviceInstancePath, DeviceAccessMode.ReadWrite, DeviceSharingMode.Shared);
        }
        catch (Exception)
        {
            return false;
        }

        DeviceConnected = true;
        Logger?.LogError("CreativeDevice connected!");
        return true;
    }

    // TODO: Proper async for AE5's DisconnectFromDevice
    public override async Task<bool> DisconnectFromDeviceAsync()
    {
        try
        {
            if (DeviceConnected)
            {
                _device = null;
                DeviceConnected = false;
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

}