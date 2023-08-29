using System;
using System.Threading.Tasks;

namespace Creative_SignalRGB_Bridge_Service
{
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

        Task<bool> DiscoverDeviceAsync();

        Task<bool> SendCommand(byte[] command);

        Task<bool> SetColors(byte[][] colors);


    }
}