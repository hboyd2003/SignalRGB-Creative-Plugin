using Windows.Devices.Enumeration;
using Microsoft.Extensions.Logging;

namespace CreativeSignalRGBBridge;

public class DeviceManager<T> : IDeviceManager where T : CreativeDevice, ICreativeDevice
{
    public List<CreativeDevice> Devices { get; }

    private readonly DeviceWatcher _deviceWatcher;
    private readonly ILogger _logger;
    public DeviceManager(ILogger<CreativeSignalRGBBridgeService> logger)
    {
        _logger = logger;
        Devices = new List<CreativeDevice>();
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
            _logger.LogWarning("A matching device that just disconnected but does not exist in list of devices.");
            return;
        }
        Devices.Remove(deviceToRemove);
        _logger.LogInformation("Creative device {deviceToRemove.DeviceName} has disconnected", deviceToRemove.DeviceName);
    }

    private void DeviceAddedEvent(DeviceWatcher sender, DeviceInformation deviceInfo)
    {
        // Inefficient (uses reflection)
        // TODO: Use dependency injection instead of passing logger
        var device = Activator.CreateInstance(typeof(T), _logger, deviceInfo) as T ?? throw new InvalidOperationException();
        Devices.Add(device);
        _logger.LogInformation("Creative device {device.DeviceName} has connected/discovered", device.DeviceName);
    }
}