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

using System.Diagnostics;
using CreativeSignalRGBBridge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


// TODO: Display warning to user that it is not running as a service and give option to continue
// Exit if not running as a windows service
if (Environment.UserInteractive && !Debugger.IsAttached) return;


var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Creative SignalRGB Bridge";
});
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Trace);

builder.Logging.AddEventLog(config =>
{
    config.SourceName = "Creative SignalRGB Bridge";
});



//builder.Services.AddSingleton(typeof(ILogger<CreativeSignalRGBBridgeService>), typeof(ILogger<CreativeSignalRGBBridgeService>));

builder.Services.AddSingleton(typeof(DeviceManager<>), typeof(DeviceManager<>));
builder.Services.AddHostedService<CreativeSignalRGBBridgeService>();


var host = builder.Build();

host.Run();
