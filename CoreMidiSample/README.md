# CoreMIDI Sample

This sample demonstrates the CoreMIDI framework bindings for .NET, including both legacy and modern (MIDI 2.0/UMP) APIs.

## Features Demonstrated

### Device Enumeration
- Enumerate all MIDI devices, entities, and endpoints
- List external devices
- Find objects by unique ID
- Get dictionary properties

### Virtual Endpoints
- Create virtual MIDI sources (legacy and protocol-based)
- Create virtual MIDI destinations (legacy and protocol-based)
- Create input and output ports
- External device management (create, add, remove)

### MIDI Data (Legacy)
- Create `MidiPacket` instances with raw byte data
- Packet construction with byte arrays and ranges

### MIDI Data (Modern / UMP)
- Create `MidiEventList` with Protocol 1.0 and 2.0
- Add Universal MIDI Packets (UMP) to event lists
- Iterate packets using `IEnumerable<MidiEventPacket>` and zero-copy `Iterate` callback
- Handle timestamp-based packet merging
- Work with `MidiEventPacket` structs

### Happy Birthday Melody
- Complete "Happy Birthday to You" encoded as MIDI 1.0 UMP messages
- Demonstrates Note On/Off creation, timestamp management, and iteration

### MIDI 2.0 Structs
- `Midi2DeviceManufacturer` - manufacturer SysEx IDs
- `Midi2DeviceRevisionLevel` - device revision levels
- `MidiCIProfileId` - standard and manufacturer-specific CI profiles
- `MidiBluetoothDriver` - Bluetooth MIDI connection management

## Requirements

- macOS 14.0 or later
- .NET 10.0 (preview) with the macOS workload installed
- Requires the updated CoreMIDI bindings (MidiEventList, MidiDevice.Create, MidiSetup, etc.)
  that ship with .NET for iOS/macOS after the CoreMIDI update from
  [dotnet/macios#4452](https://github.com/dotnet/macios/issues/4452) and
  [dotnet/macios#12489](https://github.com/dotnet/macios/issues/12489).

## Building

```bash
cd macOS
dotnet build
dotnet run
```

To immediately play Happy Birthday through the speakers on launch:

```bash
dotnet run -- --play-happy-birthday
```
