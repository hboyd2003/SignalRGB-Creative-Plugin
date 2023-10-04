using System.Runtime.InteropServices;
using Windows.Gaming.Input.Custom;
using Microsoft.Win32.SafeHandles;
using Microsoft.Extensions.Logging;
using Windows.Devices.Enumeration;
using System.Security.Cryptography;
using Windows.Devices.Custom;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;
using Windows.Security.Cryptography;

namespace CreativeSignalRGBBridge;
    // ReSharper disable once InconsistentNaming
public class AE5_Device : ICreativeDevice
{
    private CustomDevice? _device = null;
    private static readonly Guid interfaceGUID = new("{c37acb87-d563-4aa0-b761-996e7864af79}");
    private static readonly string DeviceSelector = CustomDevice.GetDeviceSelector(interfaceGUID);
    private DeviceWatcher _deviceWatcher;


    //TODO: Get the AE-5's actual display name
    public string DeviceName
    {
        get;
        private set;
    } = "SoundblasterX AE-5";

    public bool DeviceConnected
    {
        get;
        private set;
    }

    public bool DeviceFound
    {
        get;
        private set;
    }

    public string? DeviceId
    {
        get;
        private set;
    }

    public ILogger<CreativeSignalRGBBridgeService>? Logger
    {
        get;
        set;
    }

    public AE5_Device()
    {
        _deviceWatcher = DeviceInformation.CreateWatcher(DeviceSelector);
        _deviceWatcher.Added += DeviceAddedEvent;
        _deviceWatcher.Removed += DeviceRemovedEvent;
        _deviceWatcher.Start();
    }

    // Needed as the second custom flag needs to be set to one and the IOControlCode constructor can't
    // See https://learn.microsoft.com/en-us/windows-hardware/drivers/kernel/defining-i-o-control-codes
    private class IOCTLControlCode : IIOControlCode
    {
        IOControlAccessMode IIOControlCode.AccessMode
        {
            get;
        } = IOControlAccessMode.ReadWrite;


        public IOControlBufferingMethod BufferingMethod
        {
            get;
        } = IOControlBufferingMethod.Buffered;

        public uint ControlCode
        {
            get;
        } = 0x77772400;

        public ushort DeviceType
        {
            get;
        } = 0x7777;

        public ushort Function
        {
            get;
        } = 0x100;
    }

    // TODO: Proper async for AE5's DeviceAddedEvent
    private async void DeviceAddedEvent(DeviceWatcher sender, DeviceInformation deviceInfo)
    {
        Logger?.LogError("Device Found!");
        if (!DeviceConnected && !DeviceFound)
        {
            DeviceFound = true;
            DeviceId = deviceInfo.Id;
        }

    }

    // TODO: Proper async for AE5's DeviceRemovedEvent
    private async void DeviceRemovedEvent(DeviceWatcher sender, DeviceInformationUpdate deviceInfo)
    {
        Logger?.LogError("Device Removed!");
        if (deviceInfo.Id != DeviceId)
        {
            return;
        }

        if (DeviceFound && deviceInfo.Id == DeviceId)
        {
            DeviceId = null;
            DeviceFound = false;
        }

        if (DeviceConnected)
        {
            //DisconnectFromDevice();
        }
    }

    // TODO: Proper async for AE5's SendCommand
    public async Task<bool> SendCommand(byte[] command)
    {
        if (DeviceId == null || !DeviceFound || !DeviceConnected || _device == null)
        {
            return false;
        }
        //Logger?.LogError("Sending Command!");
        var inputBuffer = CryptographicBuffer.CreateFromByteArray(command);
        var outputBuffer = new Buffer(1044);
        uint success;
        try
        {
            success = await _device.SendIOControlAsync(new IOCTLControlCode(), inputBuffer, outputBuffer);
        }
        catch (Exception)
        {
            Logger?.LogError("Error sending command disconnecting!");
            DisconnectFromDevice();
            return false;
        }
        //Logger?.LogError("Sent command!");
        return success == 0;

    }

    // TODO: Proper async for AE5's ConnectToDevice
    public async Task<bool> ConnectToDevice()
    {
        Logger?.LogError("Connecting to the Device!");
        if (DeviceId == null || !DeviceFound || DeviceConnected)
        {
            return false;
        }

        try
        {
            _device = await CustomDevice.FromIdAsync(DeviceId, DeviceAccessMode.ReadWrite, DeviceSharingMode.Shared);
        }
        catch (Exception)
        {
            return false;
        }

        DeviceConnected = true;
        Logger?.LogError("Device connected!");
        return true;
    }

    // TODO: Proper async for AE5's DisconnectFromDevice
    public async Task<bool> DisconnectFromDevice()
    {
        try
        {
            if (DeviceConnected)
            {
                _device = null;
                DeviceConnected = false;
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