export function Name() { return "Creative PCIE"; }
export function Version() { return "0.7.5"; }
export function Type() { return "network"; }
export function Publisher() { return "Prismo"; }
export function Size() { return [32, 32]; }
export function DefaultPosition() {return [75, 70]; }
export function DeviceMessage() { return ["This device requires the Creative SignalRGB Service.", "Make sure the service is installed, running and set to automatic startup."]; }
export function DefaultScale(){return 1.0;}
export function SubdeviceController(){ return false; }
export function DefaultComponentBrand() { return "CompGen"; }
/* global
controller:readonly
discovery: readonly
*/
const BIG_ENDIAN = 0;
const canStream = false;
let streamingAddress = "";
let streamingPort = "";
let onBoardLeds = 5;
let ledLimit = 100;
let vLedPositions = [[0,0], [0,1], [0,2], [0,3], [0,4]];
let creativePCIEDevice;
var vLedNames = [
	"Led 1",
	"Led 2",
	"Led 3",
	"Led 4",
	"Led 5"
];

export function Initialize() {
	device.setName(controller.name);

	device.addFeature("udp");
	device.addFeature("base64");

	creativePCIEDevice = new CreativePCIEDevice(controller)
	creativePCIEDevice.setupChannel()

	streamingAddress = "127.0.0.1";
	streamingPort = 12346;
}


export function LedNames(){return vLedNames;}

export function LedPositions(){return vLedPositions;}

export function Shutdown() {

}

export function Render() {
	creativePCIEDevice.sendColors(); // Internal RGB
}




export function ImageUrl() {
	return "https://img.creative.com/images/products/large/pdt_23095.1.png";
}

// -------------------------------------------<( Discovery Service )>--------------------------------------------------


export function DiscoveryService() {
	this.IconUrl = "https://img.creative.com/images/corporate/logos/logo_creative_color.png";

	// Listen to local broadcast address, on port 12345
	this.UdpBroadcastPort = 12346;
	this.UdpListenPort = 12347;
	this.UdpBroadcastAddress = "127.0.0.1";

	this.timeSinceLastReq = 0;
	this.discovered = false;

	this.Retries = 5;
	this.RetryCount = 0;

	this.Initialize = function() {

	};

	this.Update = function() {
		if (!this.discovered) {
			if (this.timeSinceLastReq <= 0 && this.RetryCount < this.Retries) {
				service.log("Contacting Service for the " + this.RetryCount + " time");
				service.broadcast("LIST DEVICES \r\n");
				this.timeSinceLastReq = 60;
				this.RetryCount++;
			} else {
				service.log("Unable to contact the Creative SignalRGB Bridge Service after five attempts. Is it installed and running?")
			}
		}

		this.timeSinceLastReq--;
	};

	this.ResponseStringToObj = function(sResponse) {
		const response = {};
		const sResp = sResponse.toString().split("\r\n");

		for(const sLine of sResp){
			const vPair = sLine.split("=");

			if (vPair.length === 2) {
				response[vPair[0].toString().toLowerCase()] = vPair[1].toString();
			}
		}

		return response;
	};

	this.Discovered = function(value) {
		// Convert response to object.
		const response = this.ResponseStringToObj(value.response);
		service.log("Received response from service: "+ value.response);
		this.discovered = true;

		const bIsSoundblaster = value.response == "Soundblaster AE-5";

		if (bIsSoundblaster) {
			service.log("Found " + value.response + " adding!")
			const controller = service.getController(value.id);

			if (controller === undefined) {
				// Create and add new controller.
				const cont = new CreativePCIEBridge(value);
				service.addController(cont);

				// Instantiate device in SignalRGB, and pass 'this' object to device.
				service.announceController(cont);

			} else {
				controller.updateWithValue(value);
			}
		}
	};

}

class CreativePCIEDevice {
	constructor(controller) {
		this.id = controller.id
		this.name = controller.name;
	}
	setupChannel() {
		device.SetLedLimit(ledLimit);
		device.addChannel("External ARGB Header", ledLimit);
	}

	sendColors() {
		udp.send(streamingAddress, streamingPort, base64.Encode(creativePCIEDevice.createSoundblasterInternalPacket()));
		udp.send(streamingAddress, streamingPort, base64.Encode(creativePCIEDevice.createSoundblasterExternalPacket()));
	}


	createSoundblasterInternalPacket() {
		const header = [
			0x03, //Internal = 3, External = 2
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			onBoardLeds, //Number of LEDs
			0x00, 0x00, 0x00,
			0x14, //Unknown
			0x00, 0x00, 0x00
		];

		let packet = new Array(1044).fill(0); // Initializes the array with zeroes
		header.forEach((value, index) => {
			packet[index] = value;
		});


		for (let i = 0; i < vLedPositions.length; i++) {
			let iPxX = vLedPositions[i][0];
			let iPxY = vLedPositions[i][1];
			let color = device.color(iPxX, iPxY);

			packet[20 + (4 * i)] = color[0]; //Red
			packet[21 + (4 * i)] = color[1]; //Green
			packet[22 + (4 * i)] = color[2]; //Blue
			packet[23 + (4 * i)] = 0xFF; //Splitter
		}
		return packet
	}

	createSoundblasterExternalPacket() {
		const header = [
			0x02, //Internal = 3, External = 2
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			device.channel("External ARGB Header").LedCount(), //Number of LEDs
			0x00, 0x00, 0x00,
			0x14, //Unknown
			0x00, 0x00, 0x00
		];

		let packet = new Array(1044).fill(0); // Initializes the array with zeroes
		header.forEach((value, index) => {
			packet[index] = value;
		});

		let colors = device.channel("External ARGB Header").getColors("Inline", "RGB")
		for (let i = 0; i < device.channel("External ARGB Header").LedCount(); i++) {
			let pos = i * 3
			packet[20 + (4 * i)] = colors[pos]; //Red
			packet[21 + (4 * i)] = colors[pos + 1]; //Green
			packet[22 + (4 * i)] = colors[pos + 2]; //Blue
			packet[23 + (4 * i)] = 0xFF; //Splitter
		}
		//service.log("RETURNING");
		return packet
	}

}



class CreativePCIEBridge {
	constructor(value){
		this.ip = "127.0.0.1";
		this.port = 12346;
		this.id = value.id;
		this.name = value.response;

	}

	updateWithValue(value) {
		this.ip = "127.0.0.1";
		this.port = 12346;
		this.id = value.id;
		this.name = value.response;

		service.updateController(this);
	}

	update() {
		if(!this.initialized){
			this.initialized = true;
			service.updateController(this);
			service.announceController(this);
		}
	}

}

