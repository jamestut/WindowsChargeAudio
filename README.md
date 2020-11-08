# WindowsChargeAudio

This Windows service will play a user-defined notification sound when a Windows 10 device is plugged in to the charger.

This service requires .NET framework 4.6.1.

## Installation

Use .NET's `installutil.exe` to install or uninstall this service. By default this service will be installed with `LocalSystem` privileges.

The binary in the releases page are compiled with as a x86 binary, therefore 32 bit version of .NET's `installutil.exe` should be used instead. Ensure the `NAudio.dll` is also in the same folder as the `ChargeAudio.exe`.

Example command to **install** this service:

`C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe C:\path\to\ChargeAudio.exe`

Example command to **uninstall** this service:

`C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /u C:\path\to\ChargeAudio.exe`

Run those commands with administrator privilege.

After finishing the installation procedure, a new service named "ChargeAudio" should appear in the services list. This service will fail to start if not configured properly.

## Configuration

All configuration data are stored in registry with the following path: ```HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\ChargeAudio\Config```.

Please note that you may need to create the `Config` key first.

The registry entries are as follows:

- `AudioFile` (Required)  
Type: `REG_SZ`  
Absolute path to the audio file to be played when the device is plugged in to the charger. Do not enclose this value with quotes.
- `TargetVolume` (Optional | Experimental)  
Type: `REG_DWORD`  
A percentage value (0 to 100) that will be used as the volume threshold. If this registry setting exists, and the currently active audio output device master volume is set below this level and/or muted, then when the device is being charged, this service will mute all users' active audio sessions, raise the master volume of the active audio output, play the specified charging sound, and then restore back the master volume together and unmute the active audio sessions.

## Known Issues and Limitations

- Charging audio will always be played in the currently active audio output device.
- Charging sound will be played if the device enter from "battery charging" state to "plugged in" mode (for example: when device's battery is removed/inserted while the device is plugged in, or when the device battery is full and the device stops charging).