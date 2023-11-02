// This is the Creative SignalRGB Bridge Plugin/Service.
// Copyright © 2023 Harrison Boyd
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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CreativeSignalRGBBridge;

// ReSharper disable once InconsistentNaming
public partial class CreativeSignalRGBBridgeService : BackgroundService
{


    private const int ListenPort = 12346;
    private const string Header = "Creative Bridge Plugin";
    private UdpClient? _listener;
    private readonly ILogger _logger;

    CreativeSignalRGBBridgeService(ILogger logger)
    {
        this._logger = logger;
    }

    private readonly List<IDeviceManager> _deviceManagers = new()
    {
        new DeviceManager<AE5_Device>(),
        new DeviceManager<KatanaV2Device>()
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            //System.Diagnostics.Debugger.Launch();
            _listener = new UdpClient(ListenPort);
            var endPoint = new IPEndPoint(IPAddress.Broadcast, ListenPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                HandleReceivedMessage(_listener.Receive(ref endPoint));
            }
        }
        catch (TaskCanceledException)
        {
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);

            Environment.Exit(1);
        }
    }


    private void HandleReceivedMessage(byte[] udpMessage)
    {
        //---Message Format---
        // Line 0: Header/Identification (Creative SignalRGB Service or Creative SignalRGB Plugin)
        // Line 1: Command
        // Line 2..n: Data

        var messageArray = Encoding.UTF8.GetString(udpMessage).Split('\n');

        if (!messageArray[0].Trim().Equals(Header))
        {
            return;
        }

        switch (messageArray[1].Trim())
        {
            case "DEVICES":
                // Add each device to a response with their name followed by their UUID
                StringBuilder responseBuilder = new("Creative SignalRGB Service\nDEVICES");

                foreach (var deviceManager in _deviceManagers)
                {
                    foreach (var device in deviceManager.Devices)
                    {
                        responseBuilder.Append($"\n{device.DeviceName},{device.UUID}");
                        device.ConnectToDeviceAsync();
                    }
                }

                var responseData = Encoding.UTF8.GetBytes(responseBuilder.ToString());
                var loopback = new IPEndPoint(IPAddress.Loopback, 12347);
                _listener.Send(responseData, responseData.Length, loopback);
                break;

            case "SETRGB":
                // ReSharper disable once InconsistentNaming
                var UUID = messageArray[2].Trim();
                foreach (var deviceManager in _deviceManagers)
                {
                    CreativeDevice device;
                    if ((device = deviceManager.Devices.Find(device => device.UUID.Equals(UUID))) is null) continue;
                    var bytes = Convert.FromBase64String(messageArray[3]);
                    device.SendCommandAsync(bytes);


                }
                break;
        }

    }

}