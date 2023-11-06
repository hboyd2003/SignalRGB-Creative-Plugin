using System.Text.RegularExpressions;
using Windows.Devices.Custom;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Buffer = Windows.Storage.Streams.Buffer;

namespace CreativeSignalRGBBridge;

// ReSharper disable once InconsistentNaming
public partial class AE5_Device : CreativeDevice, ICreativeDevice
{
    public sealed override string DeviceName { get; protected set; } = "SoundblasterX AE-5";
    public static readonly Guid InterfaceGuid = new("{c37acb87-d563-4aa0-b761-996e7864af79}");
    public static string DeviceSelector => CustomDevice.GetDeviceSelector(InterfaceGuid);
    private readonly ILogger _logger;

    private CustomDevice? _device;
    [GeneratedRegex(@"(?<=\d{4}\\)[\w\d&]+")]
    // ReSharper disable once InconsistentNaming
    private static partial Regex UUIDRegex();

    public AE5_Device(ILogger<CreativeSignalRGBBridgeService> logger, DeviceInformation deviceInformation)
    {
        _logger = logger;

        DeviceInstancePath = deviceInformation.Id;
        UUID = UUIDRegex().Match((string)deviceInformation.Properties["System.Devices.DeviceInstanceId"]).Value;


        // Gets the name of the device
        // Unfortunately it seems impossible to get the actual CreativeDevice instead of the DeviceInterface using the VID and PID.
        // So we use the CreativeDevice ID we find to get the parent device which has the Actual Name of the device.
        var propertiesToQuery = new List<string>
        {
            "System.ItemNameDisplay",
            "System.Devices.DeviceInstanceId",
            "System.Devices.Parent",
            "System.Devices.LocationPaths",
            "System.Devices.Children"
        };
        var device = DeviceInformation.FindAllAsync(
            $"System.Devices.DeviceInstanceId:=\"{deviceInformation.Properties["System.Devices.DeviceInstanceId"]}\"",
            propertiesToQuery,
            DeviceInformationKind.Device).GetResults();
        if (device.Count > 0)
            DeviceName = device[0].Name;
        else
            _logger?.LogWarning("Unable to get device name from device.\nUsing default name of {DeviceName}", DeviceName);
    }


    public override async Task<bool> SendCommandAsync(byte[] command)
    {
        if (!DeviceConnected || _device == null) return false;

        var inputBuffer = CryptographicBuffer.CreateFromByteArray(command);
        var outputBuffer = new Buffer(1044);
        uint success;
        try
        {
            success = await _device.SendIOControlAsync(new IOCTLControlCode(), inputBuffer, outputBuffer);
        }
        catch (Exception)
        {
            _logger?.LogError("Error sending command to {DeviceName}", DeviceName);
            _ = DisconnectFromDevice();
            return false;
        }


        return success == 0;
    }

    public override async Task<bool> ConnectToDeviceAsync()
    {
        if (DeviceConnected) return false;

        try
        {
            _device = await CustomDevice.FromIdAsync(DeviceInstancePath, DeviceAccessMode.ReadWrite,
                DeviceSharingMode.Shared);
        }
        catch (Exception)
        {
            return false;
        }

        DeviceConnected = true;
        _logger.LogInformation("Successfully connected to {DeviceName}", DeviceName);
        return true;
    }

    public override bool DisconnectFromDevice()
    {
        try
        {
            if (DeviceConnected)
            {
                _device = null;
                DeviceConnected = false;
                _logger.LogInformation("Disconnected from {DeviceName}", DeviceName);
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    // Needed as the second custom flag needs to be set to one and the IOControlCode constructor can't
    // See https://learn.microsoft.com/en-us/windows-hardware/drivers/kernel/defining-i-o-control-codes
    // ReSharper disable once InconsistentNaming
    private class IOCTLControlCode : IIOControlCode
    {
        IOControlAccessMode IIOControlCode.AccessMode { get; } = IOControlAccessMode.ReadWrite;


        public IOControlBufferingMethod BufferingMethod { get; } = IOControlBufferingMethod.Buffered;

        public uint ControlCode { get; } = 0x77772400;

        public ushort DeviceType { get; } = 0x7777;

        public ushort Function { get; } = 0x100;
    }
}