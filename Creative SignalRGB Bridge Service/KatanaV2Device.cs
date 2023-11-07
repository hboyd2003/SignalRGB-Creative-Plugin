using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Microsoft.Extensions.Logging;

namespace CreativeSignalRGBBridge;

public partial class KatanaV2Device : CreativeDevice, ICreativeDevice
{
    public sealed override string DeviceName { get; protected set; }
    public static string DeviceSelector => SerialDevice.GetDeviceSelectorFromUsbVidPid(Vid, Pid);
    private readonly ILogger _logger;
    
    [GeneratedRegex(@"(?<=\d{4}\\)[\w\d&]+")]
    // ReSharper disable once InconsistentNaming
    private static partial Regex UUIDRegex();

    private const ushort Vid = 0x041E;
    private const ushort Pid = 0x3260;
    private SerialDevice? _device;
    private DataWriter? _deviceWriter;

    public KatanaV2Device(ILogger<CreativeSignalRGBBridgeService> logger, DeviceInformation deviceInformation)
    {
        _logger = logger;

        DeviceInstancePath = deviceInformation.Id;
        DeviceName =
            deviceInformation
                .Name; // Although this DeviceInterface represents the serial port its name is that of container device.


        // Gets the serial number
        // Unfortunately it seems impossible to get the actual CreativeDevice instead of the DeviceInterface using the VID and PID.
        // So we use the CreativeDevice ID we find to get the parent device which has the Serial Number in its CreativeDevice ID.
        var propertiesToQuery = new List<string>
        {
            "System.ItemNameDisplay",
            "System.Devices.DeviceInstanceId",
            "System.Devices.Parent"
        };
        var device = DeviceInformation.FindAllAsync(
            $"System.Devices.DeviceInstanceId:=\"{deviceInformation.Properties["System.Devices.DeviceInstanceId"]}\"",
            propertiesToQuery,
            DeviceInformationKind.Device).GetResults();

        UUID = device.Count >= 1
            ? UUIDRegex().Match((string)device[0].Properties["System.Devices.Parent"]).Value
            : "";


        if (!string.IsNullOrEmpty(UUID)) return;
        // Fallback serial number
        logger.LogWarning("Could not find device serial number.\nUsing device instance instead");
        UUID = UUIDRegex().Match((string)deviceInformation.Properties["System.Devices.DeviceInstanceId"]).Value;
    }



    public override async Task<bool> SendCommandAsync(byte[] command)
    {
        if (_deviceWriter == null) return false;

        //TODO: Check if command sent successfully

        _deviceWriter.WriteBytes(command);
        try
        {
            await _deviceWriter.StoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to {DeviceName}", DeviceName);
            return false;
        }
        
        return true;
    }


    private void ErrorReceivedEvent(SerialDevice sender, ErrorReceivedEventArgs eventArgs)
    {
        DisconnectFromDevice();
    }

    public async Task<bool> UnlockDevice()
    {
        if (DeviceConnected)
        {
            _logger.LogWarning("CreativeDevice unlock was called when it should not have been.");
            return false;
        }


        var firmwareUtilityProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(AppContext.BaseDirectory, "cudsp600_firmware_utility.exe"),
                Arguments =
                    $"auto ver /dv{Vid:X} /dp{Pid:X}", // Just gets the version so that it will unlock the device for us but not mess anything up.
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        try
        {
            firmwareUtilityProcess.Start();
        }
        catch (Win32Exception ex)
        {
            if (ex.NativeErrorCode == 2) // File not found error code.
                _logger.LogError(ex, "Could not find cudsp600_firmware_utility.exe");

            _logger.LogError(ex, "Failed to run cudsp600_firmware_utility.exe");
            return false;
        }

        // Check if unlock was successful
        using var output = firmwareUtilityProcess.StandardOutput;
        {
            await firmwareUtilityProcess.WaitForExitAsync();
            var processOutput = await output.ReadToEndAsync();
            if (processOutput.Contains(
                    "unlock_comms [0]")) // Due to the programs poor logging there may be other random stuff before/after
                return true;

            _logger.LogError("Failed to unlock {DeviceName}:\n\nOutput of cudsp600_firmware_utility.exe:\n{processOutput}", DeviceName, processOutput);
            return false;
        }
    }

    public override async Task<bool> ConnectToDeviceAsync()
    {
        if (DeviceConnected) return false;

        if (!await UnlockDevice()) return false;

        _device = await SerialDevice.FromIdAsync(DeviceInstancePath);


        _deviceWriter = new DataWriter(_device.OutputStream);

        //TODO: Check if device was actually connected.
        DeviceConnected = true;


        // Turn on LEDs (if they are off)
        await SendCommandAsync(new byte[] { 0x5a, 0x3a, 0x02, 0x25, 0x01 });
        SendCommandAsync(new byte[] { 0x5a, 0x3a, 0x02, 0x26, 0x01 });

        //var errorReceivedEventHandler = new Windows.Foundation.TypedEventHandler<SerialDevice, ErrorReceivedEventArgs>(this.ErrorReceivedEvent);
        //_device.ErrorReceived += errorReceivedEventHandler;

        _logger.LogInformation("Successfully connected to {DeviceName}", DeviceName);

        return true;
    }

    public override bool DisconnectFromDevice()
    {
        try
        {
            if (DeviceConnected)
            {
                _device?.Dispose();
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
}