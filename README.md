![PepperDash Essentials Pluign Logo](/images/essentials-plugin-blue.png)

# Denon/Marantz AVR Plugin

This plugin provides control of most Denon or Marantz AVRs, including surround modes, volume control, power control, input selection, and
zone 2 controls via IP connection

## Configuration

```
{
    "key": "avr",
    "uid": 1,
    "name": "AVR",
    "type": "marantzAvr",
    "properties": {
        "control": {
            "method": "tcpIp",
            "tcpSshProperties": {
                "port": 23,
                "address": "",
                "autoReconnect": true,
                "autoReconnectInterval": 10000
            }        
        },
        "enableZone2": true,
        "zone2FollowsMainPower": true
    }
}
```

| Property | Type | Description |
| -------- | ----- | ----------- |
| enableZone2 | boolean | `true` -> Enable Zone 2 control |
| zone2FollowsMainPower | boolean | Optional, default `false`. With `enableZone2`, main zone power on/off also turns Zone 2 on/off (`Z2ON`/`Z2OFF`, sent 1 s after `ZMON`/`ZMOFF`) |

## Mock Receiver (bench testing without hardware)

The `mockMarantzAvr` type builds the real Marantz device on an in-process mock receiver instead of a TCP connection. Power, Zone 2, volume, mute, inputs, surround modes, channel levels and polling all run through the same code as with real hardware, so the room and the touchpanel behave the same way. To switch an existing receiver to the mock, change only `type`. Any `control` block is ignored, and `enableZone2`, `inputs`, `surroundModes` and the other properties work as usual.

```
{
    "key": "avr",
    "name": "AVR",
    "type": "mockMarantzAvr",
    "properties": {
        "enableZone2": true,
        "mock": {                     // optional; these are the defaults
            "responseDelayMs": 30,    // delay before each reply line
            "powerOn": false,         // starting main zone power
            "input": "SAT/CBL",
            "surroundMode": "STEREO",
            "volume": 450,            // 0-980 (450 = 45.0 dB)
            "maxVolume": 98           // the MVMAX the receiver reports
        }
    }
}
```

Like a real receiver, the mock reports every state change straight back, and it ignores what a real unit ignores: unknown inputs and surround modes, and volume levels that are not whole or half dB.

Use the `mockmarantz` console command to make changes at the receiver, or to take it offline. A change made this way applies after any commands the receiver is still answering, then is reported to the driver, as a real receiver does with its own remote; the command answers `queued`, and `status` shows the result.

```
mockmarantz avr status
mockmarantz avr power on|off
mockmarantz avr input SAT/CBL|DVD|BD|TV|GAME|AUX1|NET|...
mockmarantz avr volume 0-980
mockmarantz avr mute on|off
mockmarantz avr fault none|silent    // silent: stop answering (goes offline)
```



## License

Provided under MIT license
