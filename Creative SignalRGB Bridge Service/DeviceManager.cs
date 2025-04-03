// This is the Creative SignalRGB Bridge Plugin/Service.
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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Windows.Devices.Enumeration;

namespace CreativeSignalRGBBridge;

public class
    DeviceManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T> : IDeviceManager
    where T : CreativeDevice, ICreativeDevice
{
    public List<CreativeDevice> Devices { get; }

    private readonly DeviceWatcher _deviceWatcher;
    private readonly ILogger _logger;

    public DeviceManager(ILogger<CreativeSignalRGBBridgeService> logger)
    {
        _logger = logger;
        Devices = [];
        _deviceWatcher = DeviceInformation.CreateWatcher(T.DeviceSelector);
        _deviceWatcher.Added += DeviceAddedEvent;
        _deviceWatcher.Removed += DeviceRemovedEvent;
        _deviceWatcher.Start();
    }

    private void DeviceRemovedEvent(DeviceWatcher sender, DeviceInformationUpdate deviceInfo)
    {
        var deviceToRemove = Devices.FirstOrDefault(device => device.Equals(deviceInfo));
        if (deviceToRemove == null)
        {
            _logger.LogWarning("A matching device has just disconnected but it was not known to the program!");
            return;
        }

        Devices.Remove(deviceToRemove);
        _logger.LogInformation("Creative device {deviceToRemove.DeviceName} has disconnected from the computer",
            deviceToRemove.DeviceName);
    }

    private void DeviceAddedEvent(DeviceWatcher sender, DeviceInformation deviceInfo)
    {
        // Inefficient (uses reflection)
        // TODO: Use dependency injection instead of passing logger
        var device = Activator.CreateInstance(typeof(T), _logger, deviceInfo) as T ??
                     throw new InvalidOperationException();
        Devices.Add(device);
        _logger.LogInformation("Discovered Creative device {device.DeviceName}", device.DeviceName);
    }
}