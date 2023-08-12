# SignalRGB Plugin for Creative PCIE Devices

Allows SignalRGB to control the ARGB LEDs on some Creative PCIE devices.

## Suported Devices
| Device Name             | Possibly supported | Supported |
|-------------------------|--------------------|-----------|
| SoundblasterX AE-5      |                    | Yes       |
| SoundblasterX AE-5 Plus | Yes                | No        |

## How to install

### With Installer
1. Download latest installer from [Latest Release](https://github.com/hboyd2003/SignalRGB-Creative-Plugin/releases).
2. Install and restart SignalRGB on completion

### Manual
1. Download the `Creative SignalRGB Bridge.exe`, `Creative PCIE Bridge.qml` and `Creative PCIE Bridge.js` from [Latest Release](https://github.com/hboyd2003/SignalRGB-Creative-Plugin/releases).
2. Navigate to `%USERPROFILE%\Documents\WhirlwindFX\Plugins`
3. Create a folder called `Creative PCIE Bridge` and place the `Creative PCIE Bridge.qml` and `Creative PCIE Bridge.js` files into it.
4. Navigate to `%ProgramFiles%`
5. Create a folder called `Creative SignalRGB Bridge` and place the `Creative SignalRGB Bridge.exe` into it.
8. Open a Administrator Powershell.
9. Run the command `New-Service -Name "Creative SignalRGB Bridge" -BinaryPathName "%ProgramFiles%\Creative SignalRGB Bridge\Creative SignalRGB Bridge.exe"` to add the service. 
10. Finally run the command `Start-Service -Name "Creative SignalRGB Bridge"`
