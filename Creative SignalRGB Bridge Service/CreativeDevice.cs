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

using Windows.Devices.Enumeration;

namespace CreativeSignalRGBBridge;

public abstract class CreativeDevice : IEquatable<DeviceInformationUpdate>
{
    public string DeviceInstancePath { get; protected set; }
    public bool DeviceConnected { get; protected set; }
    public string UUID { get; protected set; }

    public abstract string ProductUUID { get; }
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