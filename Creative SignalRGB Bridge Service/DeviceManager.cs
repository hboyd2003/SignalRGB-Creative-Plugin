using System;
using Windows.Devices.Custom;
using Windows.Devices.Enumeration;

namespace CreativeSignalRGBBridge;

public class DeviceManager<T> : IDeviceManager where T : CreativeDevice, ICreativeDevice
{
    private readonly DeviceWatcher _deviceWatcher;
    public List<CreativeDevice> Devices { get; private set; }

    public DeviceManager()
    {
        Devices = new List<CreativeDevice>();
        _deviceWatcher = DeviceInformation.CreateWatcher(T.DeviceSelector);
        _deviceWatcher.Added += DeviceAddedEvent;
        _deviceWatcher.Removed += DeviceRemovedEvent;
        _deviceWatcher.Start();
    }

    private async void DeviceRemovedEvent(DeviceWatcher sender, DeviceInformationUpdate deviceInfo)
    {
        Devices.RemoveAll(device => device.Equals(deviceInfo));
    }

    private async void DeviceAddedEvent(DeviceWatcher sender, DeviceInformation deviceInfo)
    {
        // Inefficient (uses reflection)
        var device = Activator.CreateInstance(typeof(T), deviceInfo) as T ?? throw new InvalidOperationException();
        Devices.Add(device);
    }
}