// This is the Creative SignalRGB Bridge Plugin/Service.
// Copyright © 2023-2025 Harrison Boyd
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

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CreativeSignalRGBBridge;

// ReSharper disable once InconsistentNaming
public class CreativeSignalRGBBridgeService : BackgroundService
{
    private const int ListenPort = 12346;
    private const string Header = "Creative Bridge Plugin";
    private readonly UdpClient _listener;
    private readonly ILogger _logger;
    private readonly List<IDeviceManager> _deviceManagers;

    public CreativeSignalRGBBridgeService(ILogger<CreativeSignalRGBBridgeService> logger,
        DeviceManager<AE5_Device> ae5DeviceManager, DeviceManager<KatanaV2Device> katanaDeviceManager)
    {
        _deviceManagers =
        [
            ae5DeviceManager,
            katanaDeviceManager
        ];
        _logger = logger;
        _listener = new UdpClient(ListenPort);
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit; // Handle process exit
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = await _listener.ReceiveAsync(stoppingToken);
                _ = HandleReceivedMessageAsync(result);
            }
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException) {
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected exception occurred: {Message}", ex.Message);
            Environment.Exit(1);
        }
    }


    private async Task HandleReceivedMessageAsync(UdpReceiveResult udpMessage)
    {
        // ---Message Format---
        // Line 0: Header/Identification (Creative SignalRGB Service or Creative SignalRGB Plugin)
        // Line 1: Command
        // Line 2…n: Data

        var messageArray = Encoding.UTF8.GetString(udpMessage.Buffer).Split('\n');

        if (!messageArray[0].Trim().Equals(Header))
        {
            return;
        }

        switch (messageArray[1].Trim())
        {
            case "DEVICES":
                // Add each device to a response with their name followed by their UUID
                StringBuilder responseBuilder = new("Creative SignalRGB Service\nDEVICES");

                List<Task> connectionTasks = [];
                foreach (var deviceManager in _deviceManagers)
                {
                    foreach (var device in deviceManager.Devices)
                    {
                        responseBuilder.Append($"\n{device.ProductUUID},{device.DeviceName},{device.UUID}");
                        connectionTasks.Add(device.ConnectToDeviceAsync());
                    }
                }

                await Task.WhenAll(connectionTasks);

                var responseData = Encoding.UTF8.GetBytes(responseBuilder.ToString());
                var loopback = new IPEndPoint(udpMessage.RemoteEndPoint.Address, 12347);
                _listener.Send(responseData, responseData.Length, loopback);
                break;

            case "SETRGB":
                // ReSharper disable once InconsistentNaming
                var UUID = messageArray[2].Trim();
                foreach (var deviceManager in _deviceManagers)
                {
                    CreativeDevice device;
                    if ((device = deviceManager.Devices.Find(deviceMatched =>
                            deviceMatched.UUID.Equals(UUID))!) is null) continue;
                    var bytes = Convert.FromBase64String(messageArray[3]);
                    _ = device.SendCommandAsync(bytes); // Fire and forget
                }

                break;
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        foreach (var device in _deviceManagers.SelectMany(deviceManager => deviceManager.Devices))
        {
            device.DisconnectFromDevice();
        }

        _listener.Close();
        _listener.Dispose();
    }
}