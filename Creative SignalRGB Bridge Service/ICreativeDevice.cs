namespace CreativeSignalRGBBridge;

public interface ICreativeDevice
{
    string DevicePath
    {
        get;
    }

    string DeviceName
    {
        get;
    }

    bool DeviceConnected
    {
        get;
    }

    Task<bool> DiscoverDeviceAsync();

    Task<bool> SendCommand(byte[] command);

    Task<bool> ConnectToDevice();
}