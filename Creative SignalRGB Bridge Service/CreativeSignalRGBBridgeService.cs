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

public partial class CreativeSignalRGBBridgeService : BackgroundService
{

    private const int ListenPort = 12346; 
    private const string Header = "Creative Bridge Plugin";
    private UdpClient listener = null;

    private AE5_Device ae5;
    private KatanaV2Device katanaV2;
    private int ledsendcommandmax = 0;

    private readonly ILogger<CreativeSignalRGBBridgeService> _logger;

    public CreativeSignalRGBBridgeService(ILogger<CreativeSignalRGBBridgeService> logger)
    {
        _logger = logger;
        _logger.LogError("INIT");
        ae5 = new AE5_Device();
        _logger.LogError("AE5 Class");
        ae5.Logger = _logger;
        _logger.LogError("AE5 Logger");
        katanaV2 = new KatanaV2Device();
        _logger.LogError("Katana Class");
        katanaV2._logger = _logger;
        _logger.LogError("Katana Logger");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            //System.Diagnostics.Debugger.Launch();
            listener = new UdpClient(ListenPort);
            IPEndPoint groupEP = new IPEndPoint(IPAddress.Broadcast, ListenPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                HandleReceivedMessage(listener.Receive(ref groupEP));
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

        var message = Encoding.UTF8.GetString(udpMessage).Split('\n');

        if (!message[0].Trim().Equals(Header))
        {
            return;
        }

        if (message[1].Trim().Equals("DEVICES"))
        {
            // Add each device that is connected to the response and try to connect to those that are found but not connected
            StringBuilder responseMessage = new("Creative SignalRGB Service\nDEVICES");
            List<Task> deviceConnectionTasks = new List<Task>();

            if (ae5.DeviceFound)
            {
                if (!ae5.DeviceConnected)
                {
                    deviceConnectionTasks.Append(
                        ae5.ConnectToDevice().ContinueWith(t =>
                        {
                            if (t.Result) responseMessage.Append($"\n{ae5.DeviceName},{ae5.UUID}");
                        }));
                } else
                {
                    responseMessage.Append($"\n{ae5.DeviceName},{ae5.UUID}");
                }
            }

            if (katanaV2.DeviceFound)
            {
                if (!katanaV2.DeviceConnected)
                {
                    deviceConnectionTasks.Append(
                        katanaV2.ConnectToDevice().ContinueWith(t =>
                        {
                            if (t.Result) responseMessage.Append($"\n{katanaV2.DeviceName},{katanaV2.UUID}");
                        }));
                } else
                {
                    responseMessage.Append($"\n{katanaV2.DeviceName},{katanaV2.UUID}");
                }
            }

            Task.WaitAll(deviceConnectionTasks.ToArray());
            var responseData = Encoding.UTF8.GetBytes(responseMessage.ToString());
            var loopback = new IPEndPoint(IPAddress.Loopback, 12347);
            listener.Send(responseData, responseData.Length, loopback);

        } else if (message[1].Trim().Contains("SETRGB")) {
            if (ae5.DeviceConnected && message[2].Contains(ae5.DeviceName))
            {
                var bytes = Convert.FromBase64String(message[3]);
                //_logger.LogError("COMMAND: " + Convert.ToHexString(bytes));
                _ = ae5.SendCommand(bytes);
            }

            if (katanaV2.DeviceConnected && message[2].StartsWith(katanaV2.DeviceName))
            {
                var bytes = Convert.FromBase64String(message[3]);
                _ = katanaV2.SendCommand(bytes);
            }
        }
    }
}