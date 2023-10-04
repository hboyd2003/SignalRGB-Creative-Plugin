using System.Reflection.Metadata;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using ABI.Windows.UI.Input.Inking.Analysis;

namespace CreativeSignalRGBBridge;
internal class KatanaV2Device : ICreativeDevice
{

    //TODO: Get Katana's actual display name
    public string DeviceName
    {
        get;
        private set;
    } = "Katana V2";

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

    public ILogger<CreativeSignalRGBBridgeService>? _logger
    {
        get;
        set;
    }

    private const ushort Vid = 0x041E;
    private const ushort Pid = 0x3260;
    private SerialDevice? _device;
    private DataWriter? _deviceWriter;
    private int deleteme = 0;
    private static readonly string DeviceSelector = SerialDevice.GetDeviceSelectorFromUsbVidPid(Vid, Pid);
    private DeviceWatcher _deviceWatcher;
    public string? DeviceId
    {
        get;
        private set;
    }

    public KatanaV2Device()
    {
        _logger?.LogError("Starting Katanav2 class");
        _deviceWatcher = DeviceInformation.CreateWatcher(DeviceSelector);
        _deviceWatcher.Added += DeviceAddedEvent;
        _deviceWatcher.Removed += DeviceRemovedEvent;
        _deviceWatcher.Start();
        _logger?.LogError("Started search for KatanaV2");
    }

    // TODO: Proper async for KatanaV2's SendCommand
    public async Task<bool> SendCommand(byte[] command)
    {
        if (_deviceWriter == null)
        {
            return false;
        } 

        //TODO: Check if command sent successfully
        if (deleteme < 5)
        {
            deleteme++;
            _logger?.LogError("Sending command");
        }
        _deviceWriter.WriteBytes(command);
        try
        {
            _deviceWriter.StoreAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to send command Exception: " + ex.Message);
            return false;
        }
        if (deleteme < 5)
        {
            deleteme++;
            _logger?.LogError("Sent command");
        }
        return true;
    }

    // TODO: Proper async for KatanaV2's DeviceAddedEvent
    private async void DeviceAddedEvent(DeviceWatcher sender, DeviceInformation deviceInfo)
    {
        _logger?.LogError("Katana V2 Found!");
        // We don't care what devices already exist until we want to connect

        if (!DeviceConnected && !DeviceFound)
        {
            DeviceFound = true;
            DeviceId = deviceInfo.Id;
        }

    }

    // TODO: Proper async for KatanaV2's DeviceRemovedEvent
    private async void DeviceRemovedEvent(DeviceWatcher sender, DeviceInformationUpdate deviceInfo)
    {
        if (deviceInfo.Id != DeviceId)
        {
            return;
        }

        if (DeviceFound)
        {
            DeviceId = null;
            DeviceFound = false;
        }

        if (DeviceConnected)
        {
            DisconnectFromDevice();
        }
    }

    // TODO: Proper async for KatanaV2's ErrorReceivedEvent
    private async void ErrorReceivedEvent(SerialDevice sender, ErrorReceivedEventArgs eventArgs)
    { 
        DisconnectFromDevice();
    }

    // TODO: Proper async for KatanaV2's ConnectToDevice
    public async Task<bool> ConnectToDevice()
    {
        if (DeviceId == null || !DeviceFound || DeviceConnected)
        {
            return false;
        }
        _device = await SerialDevice.FromIdAsync(DeviceId);
        _logger?.LogError("Got serial port object");

        _deviceWriter = new DataWriter(_device.OutputStream);
        _logger?.LogError("Opened serial port");

        //TODO: Check if device was actually connected.
        DeviceConnected = true;

        _logger?.LogError("Turning on LEDs");

        // Turn on LEDs (if they are off)
        await SendCommand(new byte[] { 0x5a, 0x3a, 0x02, 0x25, 0x01 });
        SendCommand(new byte[] { 0x5a, 0x3a, 0x02, 0x26, 0x01 });
        _logger?.LogError("Finished Connecting to device.");

        //var errorReceivedEventHandler = new Windows.Foundation.TypedEventHandler<SerialDevice, ErrorReceivedEventArgs>(this.ErrorReceivedEvent);
        //_device.ErrorReceived += errorReceivedEventHandler;

        return true;
    }

    // TODO: Proper async for KatanaV2's DisconnectFromDevice
    public async Task<bool> DisconnectFromDevice()
    {
        try
        {
            if (DeviceConnected)
            {
                _device?.Dispose();
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
