export function Name() { return "Creative PCIE"; }

export function Version() { return "2.0.0"; }

export function Type() { return "network"; }

export function Publisher() { return "Prismo"; }

export function DefaultPosition() { return [75, 70]; }

export function DeviceMessage() {
    return [
        "This device requires the Creative SignalRGB Service.",
        "Make sure the service is installed, running and set to automatic startup."
    ];
}

export function DefaultScale() { return 10.0; }

export function SubdeviceController() { return false; }

export function DefaultComponentBrand() { return "CompGen"; }
/* global
controller:readonly
discovery: readonly
*/

let creativePCIEDevice;
var library = {
    "AE5":
    {
        InternalLEDCount: 5,
        LedPositions: [[0, 0], [1, 0], [2, 0], [3, 0], [4, 0]],
        Size: [5, 1],
        ExternalHeader: true,
        ExternalLEDLimit: 100,
        FlippedRGB: false,
        ImageURL: "https://img.creative.com/images/products/large/pdt_23095.1.png",
        LogoURL: "https://img.creative.com/soundblaster/blasterx/images/logo_ae5pe.png",
        AlphaAtBeginning: false,
        GetHeader: function(ledCount, isExternal) {
            return [
                isExternal ? 0x02 : 0x03, //Internal = 3, External = 2
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                ledCount, //Number of LEDs
                0x00, 0x00, 0x00,
                0x14, //Unknown
                0x00, 0x00, 0x00
            ];
        }
    },
    "KatanaV2":
    {
        InternalLEDCount: 7,
        LedPositions: [[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0]],
        Size: [7, 1],
        ExternalHeader: false,
        ExternalLEDLimit: 0,
        FlippedRGB: true,
        ImageURL: "https://img.creative.com/images/products/large/pdt_23766.png",
        LogoURL: "https://img.creative.com/inline/products/23766/logo-katana-v2.png",
        AlphaAtBeginning: true,
        GetHeader: function(ledCount, isExternal) {
            return [
                0x5a, // Magic byte
                0x3a, // LED command
                0x20, // Command length (after this)
                0x2b, // Set LEDs Sub-command
                0x00, 0x01, 0x01
            ];
        }
    }
};


// Called when a new controller is created by the Discovery Service.
export function Initialize() {
    device.log("INIT");
    
    device.setName(controller.name);

    device.logoURL = library[controller.productUUID].LogoURL;
    device.addFeature("udp");
    device.addFeature("base64");

    creativePCIEDevice = new CreativePCIEDevice(controller);
}

export function Shutdown() {

}

export function Render() {
    
    creativePCIEDevice.sendColors(); // Internal RGB
}


//Represents each individual device.
class CreativePCIEDevice {
    constructor(controller) {
        this.id = controller.id;
        this.name = controller.name;
        this.internalLEDCount = library[controller.productUUID].InternalLEDCount;
        this.externalHeader = library[controller.productUUID].ExternalHeader;
        this.ExternalLEDLimit = library[controller.productUUID].ExternalLEDLimit;
        device.setImageFromUrl(library[controller.productUUID].ImageURL);
        this.flippedRGB = library[controller.productUUID].FlippedRGB;
        this.alphaAtBeginning = library[controller.productUUID].AlphaAtBeginning;
        device.SetLedLimit(this.ExternalLEDLimit);
        device.setSize(library[controller.productUUID].Size);
        const names = [];
        for (let i = 0; i < this.internalLEDCount; i++) {
            names.push(`Led ${i}`);
        }
        device.setControllableLeds(names, library[controller.productUUID].LedPositions);
        //device.SetName(controller.name);
        if (this.externalHeader) {
            device.addChannel("External ARGB Header", this.ExternalLEDLimit);
        }
    }

    sendColors() {
            //device.log("Sending colors to " + controller.ip + " with the name of " + this.name);
            udp.send(controller.ip,
                controller.port,
                `Creative Bridge Plugin\nSETRGB\n${this.id}\n${base64.Encode(
                    creativePCIEDevice.createInternalRGBPacket())
                }`);
            if (this.externalHeader) {
                udp.send(controller.ip,
                    controller.port,
                    `Creative Bridge Plugin\nSETRGB\n${this.id}\n${base64.Encode(
                        creativePCIEDevice.createExternalRGBPacket())}`);
            }
        }

    createInternalRGBPacket() {
        const header = library[controller.productUUID].GetHeader(this.internalLEDCount, false);
        const colors = [];
        for (let i = 0; i < this.internalLEDCount; i++) {
            const color = device.color(i, 0);
            colors.push(color[0]);
            colors.push(color[1]);
            colors.push(color[2]);
        }

        return this.createPacket(header, colors, this.internalLEDCount);

    }

    createExternalRGBPacket() {
        const ledCount = device.channel("External ARGB Header").LedCount();
        const header = library[controller.productUUID].GetHeader(ledCount, true);
        const colors = device.channel("External ARGB Header").getColors("Inline", "RGB");

        return this.createPacket(header, colors, ledCount);
    }

    createPacket(header, colors, ledCount) {
        const totalCommandLength = (ledCount * 4) + header.length;
        const packet = new Array(totalCommandLength).fill(0); // Initializes the array with zeroes

        header.forEach((value, index) => {
            packet[index] = value;
        });
        for (let i = 0; i < ledCount; i++) {

            const commandPos = header.length + (i * 4);
            const colorPos = i * 3;

            packet[commandPos + (this.alphaAtBeginning ? 1 : 0)] = colors[colorPos + (this.flippedRGB ? 2 : 0)]; //Red or Blue
            packet[commandPos + (this.alphaAtBeginning ? 1 : 0) + 1] = colors[colorPos + 1]; //Green
            packet[commandPos + (this.alphaAtBeginning ? 1 : 0) + 2] = colors[colorPos + (this.flippedRGB ? 0 : 2)]; //Blue or Red
            packet[commandPos + (this.alphaAtBeginning ? 0 : 3)] = 0xFF; //Alpha
        }
        //device.log(logstring + "]")
        return packet;
    }

}

// -------------------------------------------<( Discovery Service )>--------------------------------------------------
export function DiscoveryService() {
    this.IconUrl =
        "https://img.creative.com/images/corporate/logos/logo_creative_color.png"; // Icon for the Service page in SignalRGB

    // Listen to local broadcast address, on port 12345
    this.UdpBroadcastPort = 12346;
    this.UdpListenPort = 12347;
    this.UdpBroadcastAddress = "127.0.0.1";

    this.timeSinceLastReq = 0;

    this.Initialize = function() {
        this.windowsServices = {};
    };

    this.Update = function() {
        if (this.timeSinceLastReq <= 0) {
            // Look for new services
            service.broadcast("Creative Bridge Plugin\nDEVICES");
            service.log("Broadcasting...");
            // Look for new change in device
            //for (const [ip, service] of Object.entries(this.services)) {
            //    service.requestDevices();
            //}
            this.timeSinceLastReq = 60;
            Object.entries(this.windowsServices).forEach(([key, windowsService]) => {
                var secondsSinceSeen = (Date.now() - windowsService.lastSeen) / 1000;
                if (secondsSinceSeen >= 122) {
                    service.log(
                        `Removing Windows service at ${windowsService.ip} since it has not been seen for ${
                        secondsSinceSeen} seconds!`);
                    windowsService.removeAllDevices();
                    delete this.windowsServices[windowsService.ip];
                }
            });
        }
        this.timeSinceLastReq--;

    };

    this.ResponseStringToObj = function(sResponse) {
        const response = {};
        const sResp = sResponse.toString().split("\r\n");

        for (const sLine of sResp) {
            const vPair = sLine.split("=");

            if (vPair.length === 2) {
                response[vPair[0].toString().toLowerCase()] = vPair[1].toString();
            }
        }

        return response;
    };

    // Handles responses (not just for discoveries!)
    this.Discovered = function(value) {

        // Response should be "Creative SignalRGB Service" as first line followed by a command then devices (if needed).
        const response = value.response.toString().split("\n");
        if (response[0] !== "Creative SignalRGB Service") {
            // Recieved response can be from anything (Including itself!)
            return;
        } else if (response[1].trim().includes("DEVICES")) {
            if (!(value.ip in this.windowsServices)) {
                service.log(`Found new windows service at ${value.ip}!`);
                const creativeService = new CreativeService(value);
                this.windowsServices[value.ip] = creativeService;
            }
            service.log(`Recieved response from ${value.ip} with ${response.length - 2} devices!`);
            response.splice(0, 2); // Remove the header and command
            this.windowsServices[value.ip].updateDevices(response);
        } else if (response[1].trim() === "STOPPING") {
            this.windowsServices[value.ip].removeAllDevices();
        } else {
            service.log(`The command ${response[1]} is not a recognized command!`);
        }
    };

}

// Represents each windows service
class CreativeService {
    constructor(value) {
        this.ip = value.ip;
        this.port = 12346;
        this.devices = [];
        this.lastSeen = Date.now();
    }

    updateDevices(foundDevices) {
        this.lastSeen = Date.now();
        // Remove missing devices
        for (let device of this.devices) {
            idk += device.id + ", ";
            if (!foundDevices.some(str => str.includes(device.id))) {
                service.log(`Device (controller) ${device.name} is no longer connected, removing!`);
                this.devices.splice(this.devices.indexOf(device), 1);
                service.removeController(device);
            }
        }

        // Add new device
        for (let foundDevice of foundDevices) {
            const deviceInfo = foundDevice.split(",");
            service.log(foundDevice);
            if (!this.devices.find(e => e.id === deviceInfo[2])) {
                // Create and add new device (controller).

                service.log(`Adding new device: ${deviceInfo[1]}`);
                const creativePCIEBridge = new CreativePCIEBridge(deviceInfo);
                this.devices.push(creativePCIEBridge);
                service.addController(creativePCIEBridge);
                service.announceController(creativePCIEBridge);

            }
        }
    }

    removeAllDevices() {
        for (let device of this.devices) {
            service.removeController(device);
        }
    }

    requestDevices() {
        udp.send(this.ip, this.port, "LIST");
    }
}

// Controller for each device (each device has its own controller)
// Only here b/c its needed
class CreativePCIEBridge {
    constructor(deviceInfo) {
        service.log(deviceInfo[1]);
        this.id = deviceInfo[2];
        this.productUUID = deviceInfo[0];
        this.ip = "127.0.0.1";
        this.port = 12346;
        this.name = deviceInfo[1];
        this.logoURL = library[deviceInfo[0]].LogoURL; // Logo for the device in the Service pane.
    }


    updateWithValue(value) {
        this.ip = value.ip;
        this.port = 12346;
        service.updateController(this);
    }

    update() {
        if (!this.initialized) {
            this.initialized = true;
            service.updateController(this);
            service.announceController(this);
        }
    }

}