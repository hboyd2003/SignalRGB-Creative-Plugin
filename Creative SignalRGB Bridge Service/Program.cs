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

using CreativeSignalRGBBridge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;


// TODO: Check if running as a service or as a program and display warning.

if (Environment.UserInteractive == false) return;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Creative SignalRGB Bridge";
});


//LoggerProviderOptions.RegisterProviderOptions<
//    EventLogSettings, EventLogLoggerProvider>(builder.Services);

//builder.Services.AddSingleton(typeof(ILogger<CreativeSignalRGBBridgeService>), typeof(ILogger<CreativeSignalRGBBridgeService>));

builder.Services.AddSingleton(typeof(DeviceManager<>), typeof(DeviceManager<>));
builder.Services.AddHostedService<CreativeSignalRGBBridgeService>();


var host = builder.Build();

host.Run();
