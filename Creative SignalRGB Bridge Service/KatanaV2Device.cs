// This is the Creative SignalRGB Bridge Plugin/Service.
// Copyright © 2023-2024 Harrison Boyd
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

using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;

namespace CreativeSignalRGBBridge;

public partial class KatanaV2Device : CreativeDevice, ICreativeDevice
{
    public override sealed string DeviceName
    {
        get; protected set;
    }
    public static string DeviceSelector => SerialDevice.GetDeviceSelectorFromUsbVidPid(Vid, Pid);
    public override string ProductUUID { get; } = "KatanaV2";

    [GeneratedRegex(@"[\w\d&]+$")]
    // ReSharper disable once InconsistentNaming
    private static partial Regex UUIDRegex();

    private const ushort Vid = 0x041E;
    private const ushort Pid = 0x3260;
    private SerialPort? _device;
    private readonly ILogger _logger;

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



    public async override Task<bool> SendCommandAsync(byte[] command)
    {
        if (_device is not { IsOpen: true } || !DeviceConnected)
        {
            _logger.LogWarning("Command was sent to {DeviceName} when it should not have been.", DeviceName);
            return false;
        }
        //TODO: Check if command sent successfully  
        try
        {
            _device.Write(command, 0, command.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to {DeviceName}", DeviceName);
            return false;
        }

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

    public async override Task<bool> ConnectToDeviceAsync()
    {
        if (DeviceConnected) return false;

        if (!await UnlockDevice()) return false;

        // Since we need to use the Win32 we need the port name and the only way (if using the device watcher) is to open the device using the WinRT API (which won't work for commands since we are not uwp)
        var tempDevice = await SerialDevice.FromIdAsync(DeviceInstancePath);
        var portName = tempDevice.PortName;
        tempDevice.Dispose(); // Close device opened with  WinRT

        _device = new SerialPort(portName);
        _device.Open();

        DeviceConnected = true;
        // Switch mode
        await SendCommandAsync("SW_MODE1\r\n"u8.ToArray());
        // Turn on LEDs (if they are off)
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