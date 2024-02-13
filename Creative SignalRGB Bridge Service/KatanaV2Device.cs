using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Microsoft.Extensions.Logging;
using System.IO.Ports;

namespace CreativeSignalRGBBridge;

public partial class KatanaV2Device : CreativeDevice, ICreativeDevice
{
    public sealed override string DeviceName { get; protected set; }
    public static string DeviceSelector => SerialDevice.GetDeviceSelectorFromUsbVidPid(Vid, Pid);
    public override string ProductUUID { get; } = "KatanaV2";
    
    [GeneratedRegex(@"[\w\d&]+$")]
    // ReSharper disable once InconsistentNaming
    private static partial Regex UUIDRegex();

    private const ushort Vid = 0x041E; //0x041E
    private const ushort Pid = 0x3260; //0x3260
    private SerialPort? _device;
    private readonly ILogger _logger;
    private bool writingToDevice = false;

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
        var deviceTask = DeviceInformation.FindAllAsync(
            $"System.Devices.DeviceInstanceId:=\"{deviceInformation.Properties["System.Devices.DeviceInstanceId"]}\"",
            propertiesToQuery,
            DeviceInformationKind.Device).AsTask();
        deviceTask.Wait();
        var device = deviceTask.Result;

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
        if (_device is not { IsOpen: true } || !DeviceConnected || writingToDevice)
        {
            _logger.LogWarning("Command was sent to {DeviceName} when it should not have been.", DeviceName);
            return false;
        }
        //TODO: Check if command sent successfully  
        try
        {
            writingToDevice = true;
            _device.Write(command, 0, command.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to {DeviceName}", DeviceName);
            writingToDevice = false;
            return false;
        }

        writingToDevice = false;
        return true;
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
        
        // Since we need to use the Win32 we need the port name and the only way (if using the device watcher) is to open the device using the WinRT API (which won't work for commands since we are not uwp)
        SerialDevice tempDevice = await SerialDevice.FromIdAsync(DeviceInstancePath);
        string portName = tempDevice.PortName;
        tempDevice.Dispose(); // Close device opened with  WinRT

        _device = new SerialPort(portName);
        _device.Open();

        DeviceConnected = true;
        // Turn on LEDs (if they are off)
        await SendCommandAsync("SW_MODE1\r\n"u8.ToArray());
        await SendCommandAsync(new byte[] { 0x5a, 0x3a, 0x02, 0x25, 0x01 });
        SendCommandAsync(new byte[] { 0x5a, 0x3a, 0x02, 0x26, 0x01 });


        //TODO: Check if device was actually connected.
        
        _logger.LogInformation("Plugin successfully connected to {DeviceName}", DeviceName);
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
                _logger.LogInformation("Plugin disconnected from {DeviceName}", DeviceName);
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