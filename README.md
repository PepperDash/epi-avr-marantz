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
        "enableZone2": true
    }
}
```

| Property | Type | Description |
| -------- | ----- | ----------- |
| enableZone2 | boolean | `true` -> Enable Zone 2 control |



## License

Provided under MIT license
<!-- START Minimum Essentials Framework Versions -->
### Minimum Essentials Framework Versions

- 2.4.4
<!-- END Minimum Essentials Framework Versions -->
<!-- START Config Example -->
### Config Example

```json
{
    "key": "GeneratedKey",
    "uid": 1,
    "name": "GeneratedName",
    "type": "MarantzAvr",
    "group": "Group",
    "properties": {
        "name": "SampleString",
        "inputKey": "SampleString",
        "hideInput": true
    }
}
```
<!-- END Config Example -->
<!-- START Supported Types -->
### Supported Types

- MarantzAvr
<!-- END Supported Types -->
<!-- START Join Maps -->
### Join Maps

#### Digitals

| Join | Type (RW) | Description |
| --- | --- | --- |
| 1 | R | Is Online |
| 2 | R | Power Is On |

#### Serials

| Join | Type (RW) | Description |
| --- | --- | --- |
| 1 | R | Device Name |
<!-- END Join Maps -->
<!-- START Interfaces Implemented -->
### Interfaces Implemented

- ISelectableItems<string>
- IKeyed
- IBasicVolumeWithFeedback
- IHasPowerControlWithFeedback
- IBasicVolumeWithFeedbackAdvanced
- IHasInputs<string>
- IWarmingCooling
- IOnline
- IRouting
- ICommunicationMonitor
- IHasFeedback
- IHasSurroundChannels
- IRoutingSinkWithSwitching
- IDeviceInfoProvider
- IHasSurroundSoundModes<SurroundModes
- ISelectableItems<SurroundModes>
- IKeyName
<!-- END Interfaces Implemented -->
<!-- START Base Classes -->
### Base Classes

- MessengerBase
- EssentialsDevice
- JoinMapBaseAdvanced
- EssentialsBridgeableDevice
- string>
<!-- END Base Classes -->
<!-- START Public Methods -->
### Public Methods

- public void Select()
- public void Select()
- public void VolumeUp(bool pressRelease)
- public void VolumeDown(bool pressRelease)
- public void MuteOn()
- public void MuteOff()
- public void MuteToggle()
- public void SetVolume(ushort level)
- public void ParseResponse(string response)
- public void PowerOn()
- public void PowerOff()
- public void PowerToggle()
- public void VolumeUp(bool pressRelease)
- public void VolumeDown(bool pressRelease)
- public void MuteToggle()
- public void MuteOn()
- public void MuteOff()
- public void SetVolume(ushort level)
- public void SetInput(string input)
- public void SetDefaultChannelLevels()
- public void SendText(string text)
- public void PowerOn()
- public void PowerOff()
- public void PowerToggle()
- public void VolumeUp(bool pressRelease)
- public void VolumeDown(bool pressRelease)
- public void MuteToggle()
- public void MuteOn()
- public void MuteOff()
- public void SetVolume(ushort level)
- public void SetInput(string input)
- public void SetSurroundSoundMode(string surroundMode)
- public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
- public void ExecuteSwitch(object inputSelector)
- public void UpdateDeviceInfo()
- public void Select()
<!-- END Public Methods -->
<!-- START Bool Feedbacks -->
### Bool Feedbacks

- MuteFeedback
- PowerIsOnFeedback
- MuteFeedback
- IsWarmingUpFeedback
- IsCoolingDownFeedback
- IsOnline
- PowerIsOnFeedback
- MuteFeedback
- IsWarmingUpFeedback
- IsCoolingDownFeedback
<!-- END Bool Feedbacks -->
<!-- START Int Feedbacks -->
### Int Feedbacks

- VolumeLevelFeedback
- VolumeLevelFeedback
- VolumeLevelFeedback
<!-- END Int Feedbacks -->
<!-- START String Feedbacks -->
### String Feedbacks

- CurrentSurroundModeStringFeedback
<!-- END String Feedbacks -->
