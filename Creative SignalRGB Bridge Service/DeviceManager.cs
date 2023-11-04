using Microsoft.Extensions.DependencyInjection;
using Windows.Devices.Enumeration;
using Microsoft.Extensions.Logging;

namespace CreativeSignalRGBBridge;

public class DeviceManager<T> : IDeviceManager where T : CreativeDevice, ICreativeDevice
{
    public List<CreativeDevice> Devices { get; }

    private readonly DeviceWatcher _deviceWatcher;
    private ILogger Logger;
    public DeviceManager(ILogger<CreativeSignalRGBBridgeService> logger)
    {
        Logger = logger;
        Devices = new List<CreativeDevice>();
        _deviceWatcher = DeviceInformation.CreateWatcher(T.DeviceSelector);
        _deviceWatcher.Added += DeviceAddedEvent;
        _deviceWatcher.Removed += DeviceRemovedEvent;
        _deviceWatcher.Start();
    }

    private void DeviceRemovedEvent(DeviceWatcher sender, DeviceInformationUpdate deviceInfo)
    {
        Devices.RemoveAll(device => device.Equals(deviceInfo));
    }

    private void DeviceAddedEvent(DeviceWatcher sender, DeviceInformation deviceInfo)
    {
        // Inefficient (uses reflection)
        // TODO: Use dependency injection instead of passing logger
        var device = Activator.CreateInstance(typeof(T), Logger, deviceInfo) as T ?? throw new InvalidOperationException();
        Devices.Add(device);
    }
}