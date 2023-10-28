using Windows.Devices.Enumeration;

namespace CreativeSignalRGBBridge;

public abstract class CreativeDevice : IEquatable<DeviceInformationUpdate>
{
    public string DeviceInstancePath { get; protected set; }
    public bool DeviceConnected { get; protected set; }
    public string UUID { get; protected set; }

    public abstract string DeviceName { get; protected set; }
    public abstract Task<bool> SendCommandAsync(byte[] command);
    public abstract Task<bool> ConnectToDeviceAsync();
    public abstract bool DisconnectFromDevice();

    public bool Equals(DeviceInformationUpdate? other)
    {
        return other != null && other.Id.Equals(DeviceInstancePath);
    }



    public override bool Equals(object? obj)
    {
        return Equals(obj as CreativeDevice);
    }

    public bool Equals(CreativeDevice? device)
    {
        if (device is null) return false;

        if (ReferenceEquals(this, device)) return true;

        return DeviceInstancePath == device.DeviceInstancePath;
    }


    public override int GetHashCode()
    {
        return DeviceInstancePath.GetHashCode();
    }

    public static implicit operator CreativeDevice(bool v)
    {
        throw new NotImplementedException();
    }
}