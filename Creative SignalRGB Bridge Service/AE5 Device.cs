﻿// This is the Creative SignalRGB Bridge Plugin/Service.
// Copyright © 2023-2025 Harrison Boyd
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

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Windows.Devices.Custom;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Buffer = Windows.Storage.Streams.Buffer;

namespace CreativeSignalRGBBridge;

// ReSharper disable once InconsistentNaming
public partial class AE5_Device : CreativeDevice, ICreativeDevice
{
    public override string ProductUUID => "AE5";
    public sealed override string DeviceName { get; init; } = "SoundblasterX AE-5";

    public static string DeviceSelector =>
        CustomDevice.GetDeviceSelector(new Guid("{c37acb87-d563-4aa0-b761-996e7864af79}"));

    private readonly ILogger _logger;
    private CustomDevice? _device;

    [GeneratedRegex(@"[\w\d&]+$")]
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
        var deviceTask = DeviceInformation.FindAllAsync(
            $"System.Devices.DeviceInstanceId:=\"{deviceInformation.Properties["System.Devices.DeviceInstanceId"]}\"",
            propertiesToQuery,
            DeviceInformationKind.Device).AsTask();
        deviceTask.Wait();
        var device = deviceTask.Result;

        if (device.Count > 0)
            DeviceName = device[0].Name;
        else
            _logger.LogWarning("Unable to get device name from device.\nUsing default name of {DeviceName}", DeviceName);
    }


    public override async Task<bool> SendCommandAsync(byte[] command)
    {
        if (!DeviceConnected || _device == null) return false;
        var paddedCommand = new byte[1044];
        command.CopyTo(paddedCommand, 0);
        var inputBuffer = CryptographicBuffer.CreateFromByteArray(command);
        var outputBuffer = new Buffer(1044);
        uint success;
        try
        {
            success = await _device.SendIOControlAsync(new IOCTLControlCode(), inputBuffer, outputBuffer);
        }
        catch (Exception)
        {
            _logger.LogError("Error sending command to {DeviceName}", DeviceName);
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
        _logger.LogInformation("Plugin successfully connected to {DeviceName}", DeviceName);
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

    // The class is necessary since the IOControlCode constructor does not allow for the setting of the custom flag (byte 13) in the control code.
    // See https://learn.microsoft.com/en-us/windows-hardware/drivers/kernel/defining-i-o-control-codes
    // ReSharper disable once InconsistentNaming
    private class IOCTLControlCode : IIOControlCode
    {
        IOControlAccessMode IIOControlCode.AccessMode => IOControlAccessMode.ReadWrite;

        public IOControlBufferingMethod BufferingMethod => IOControlBufferingMethod.Buffered;

        public uint ControlCode => 0x77772400;

        public ushort DeviceType => 0x7777;

        public ushort Function => 0x100;
    }
}