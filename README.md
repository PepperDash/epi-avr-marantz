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
            "method": "tcpIp"
            "tcpSshProperties": {
                "port": 23,
                "address": "",
                "autoReconnect": true,
                "autoReconnectInterval": 10000
            }        
        },
        "enableZone2": true
    }
}
```

| Property | Type | Description |
| -------- | ----- | ----------- |
| enableZone2 | boolean | `true` -> Enable Zone 2 control |



## License

Provided under MIT license
