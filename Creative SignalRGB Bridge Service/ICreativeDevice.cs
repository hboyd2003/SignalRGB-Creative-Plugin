namespace CreativeSignalRGBBridge;

public interface ICreativeDevice
{
    string DeviceName
    {
        get;
    }

    bool DeviceConnected
    {
        get;
    }

    bool DeviceFound
    {
        get;
    }

    string? DeviceId
    {
        get;
    }

    Task<bool> SendCommand(byte[] command);

    Task<bool> ConnectToDevice();
}