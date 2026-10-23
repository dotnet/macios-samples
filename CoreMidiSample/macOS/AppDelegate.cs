// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable
#pragma warning disable APL0004 // MidiDevice.Create is experimental
#pragma warning disable CS0618 // Obsolete API usage for demonstration

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using AVFoundation;
using CoreGraphics;
using CoreMidi;
using Foundation;
using AppKit;

namespace CoreMidiSample;

/// <summary>
/// A sample app that demonstrates the CoreMIDI API surface.
/// It creates a window with buttons to exercise various CoreMIDI features:
/// - Enumerating MIDI devices, entities, and endpoints
/// - Creating virtual MIDI sources and destinations
/// - Sending and receiving MIDI data using both legacy (MidiPacket) and modern (MidiEventList) APIs
/// - Creating and iterating MidiEventList packets
/// - Working with MIDI 2.0 structs (Midi2DeviceManufacturer, Midi2DeviceRevisionLevel, MidiCIProfileId)
/// - Using the MidiBluetoothDriver
/// </summary>
public class AppDelegate : NSApplicationDelegate {
	NSWindow? window;
	NSTextView? logView;
	MidiClient? client;
	AVAudioEngine? audioEngine;
	AVAudioUnitSampler? sampler;
	bool happyBirthdayIsPlaying;
	public string [] LaunchArguments { get; set; } = [];

	public override void DidFinishLaunching (NSNotification notification)
	{
		SetupMenu ();
		SetupWindow ();
		InitializeMidi ();

		if (LaunchArguments.Contains ("--play-happy-birthday"))
			PlayHappyBirthday ();
	}

	void SetupMenu ()
	{
		NSMenu mainMenu = NSApplication.SharedApplication.MainMenu = new NSMenu ("CoreMIDI Sample");
		mainMenu.AddItem ("Sub", new ObjCRuntime.Selector ("sub"), "S");
		var subMenu = new NSMenu ("Sub");
		var quit = new NSMenuItem ("Quit", (sender, e) => {
			Cleanup ();
			NSApplication.SharedApplication.Terminate (this);
		});
		quit.Enabled = true;
		quit.KeyEquivalent = "q";
		quit.KeyEquivalentModifierMask = NSEventModifierMask.CommandKeyMask;
		subMenu.AddItem (quit);
		mainMenu.AutoEnablesItems = false;
		mainMenu.SetSubmenu (subMenu, mainMenu.ItemAt (0)!);
	}

	void SetupWindow ()
	{
		window = new NSWindow (new CGRect (100, 100, 900, 700),
			NSWindowStyle.Titled | NSWindowStyle.Resizable | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable,
			NSBackingStore.Buffered, true);
		window.Title = "CoreMIDI Sample";

		var contentView = window.ContentView!;

		// Create log text view
		var scrollView = new NSScrollView (new CGRect (10, 10, 880, 400));
		logView = new NSTextView (scrollView.ContentView.Bounds);
		logView.Editable = false;
		logView.Font = NSFont.MonospacedSystemFont (12, NSFontWeight.Regular)!;
		logView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
		scrollView.DocumentView = logView;
		scrollView.HasVerticalScroller = true;
		scrollView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
		contentView.AddSubview (scrollView);

		// Create buttons
		var buttonY = 620.0;
		var buttonHeight = 30.0;
		var buttonSpacing = 5.0;

		AddButton (contentView, "Enumerate Devices", ref buttonY, buttonHeight, buttonSpacing, EnumerateDevices);
		AddButton (contentView, "Create Virtual Source & Destination", ref buttonY, buttonHeight, buttonSpacing, CreateVirtualEndpoints);
		AddButton (contentView, "Send MIDI (Legacy MidiPacket)", ref buttonY, buttonHeight, buttonSpacing, SendMidiLegacy);
		AddButton (contentView, "Send MIDI (MidiEventList/UMP)", ref buttonY, buttonHeight, buttonSpacing, SendMidiEventList);
		AddButton (contentView, "Play Happy Birthday (MidiEventList)", ref buttonY, buttonHeight, buttonSpacing, PlayHappyBirthday);
		AddButton (contentView, "MIDI 2.0 Structs Demo", ref buttonY, buttonHeight, buttonSpacing, Midi2StructsDemo);
		AddButton (contentView, "Clear Log", ref buttonY, buttonHeight, buttonSpacing, ClearLog);

		window.Center ();
		window.MakeKeyAndOrderFront (this);
	}

	void AddButton (NSView parent, string title, ref double y, double height, double spacing, Action action)
	{
		var button = new NSButton (new CGRect (10, y, 300, height));
		button.Title = title;
		button.BezelStyle = NSBezelStyle.Rounded;
		button.Activated += (s, e) => action ();
		parent.AddSubview (button);
		y -= height + spacing;
	}

	void Log (string message)
	{
		if (logView is null)
			return;

		var text = logView.Value ?? "";
		logView.Value = text + message + "\n";
		logView.ScrollRangeToVisible (new NSRange (logView.Value.Length, 0));
	}

	void ClearLog ()
	{
		if (logView is not null)
			logView.Value = "";
	}

	void InitializeMidi ()
	{
		client = new MidiClient ("CoreMidiSample");
		client.ObjectAdded += (s, e) => Log ($"MIDI object added: {e.Child}");
		client.ObjectRemoved += (s, e) => Log ($"MIDI object removed: {e.Child}");
		client.SetupChanged += (s, e) => Log ("MIDI setup changed");
		Log ("MIDI client initialized.");
		Log ($"Devices: {Midi.DeviceCount}, Sources: {Midi.SourceCount}, Destinations: {Midi.DestinationCount}");
	}

	bool EnsureAudioPlaybackReady ()
	{
		if (audioEngine is not null && sampler is not null && audioEngine.Running)
			return true;

		audioEngine?.Stop ();
		sampler?.Dispose ();
		audioEngine?.Dispose ();

		audioEngine = new AVAudioEngine ();
		sampler = new AVAudioUnitSampler ();
		audioEngine.AttachNode (sampler);
		audioEngine.Connect (sampler, audioEngine.MainMixerNode, null);
		audioEngine.Prepare ();

		if (!audioEngine.StartAndReturnError (out var error)) {
			Log ($"Failed to start audio engine: {error?.LocalizedDescription ?? "unknown error"}");
			return false;
		}

		// Program 0 = Acoustic Grand Piano.
		sampler.SendProgramChange (0, 0);
		return true;
	}

	async Task PlayMelodyOnSpeakers (byte [] melody, ulong [] durations, byte velocity)
	{
		if (!EnsureAudioPlaybackReady () || sampler is null)
			return;

		const byte channel = 0;
		for (int i = 0; i < melody.Length; i++) {
			sampler.StartNote (melody [i], velocity, channel);
			await Task.Delay ((int) durations [i]);
			sampler.StopNote (melody [i], channel);
		}
	}

	/// <summary>
	/// Demonstrates enumerating MIDI devices, entities, and endpoints.
	/// Uses: Midi.DeviceCount, Midi.GetDevice, MidiDevice.EntityCount, MidiDevice.GetEntity,
	///       MidiEntity.Sources, MidiEntity.Destinations, MidiEntity.GetSource, MidiEntity.GetDestination,
	///       MidiEndpoint.GetSource, MidiEndpoint.GetDestination, MidiEndpoint.EndpointName,
	///       Midi.ExternalDeviceCount, Midi.GetExternalDevice, MidiDevice.UniqueID.
	/// </summary>
	void EnumerateDevices ()
	{
		Log ("=== MIDI Device Enumeration ===");
		Log ($"Total devices: {Midi.DeviceCount}");
		Log ($"Total external devices: {Midi.ExternalDeviceCount}");
		Log ($"Total sources: {Midi.SourceCount}");
		Log ($"Total destinations: {Midi.DestinationCount}");
		Log ("");

		for (nint i = 0; i < Midi.DeviceCount; i++) {
			var device = Midi.GetDevice (i);
			if (device is null)
				continue;
			Log ($"Device {i}: UniqueID={device.UniqueID}");

			for (nint j = 0; j < device.EntityCount; j++) {
				var entity = device.GetEntity (j);
				if (entity is null)
					continue;
				Log ($"  Entity {j}: Sources={entity.Sources}, Destinations={entity.Destinations}");

				for (nint k = 0; k < entity.Sources; k++) {
					var src = entity.GetSource (k);
					Log ($"    Source {k}: {src?.EndpointName ?? "(unnamed)"}");
				}
				for (nint k = 0; k < entity.Destinations; k++) {
					var dst = entity.GetDestination (k);
					Log ($"    Destination {k}: {dst?.EndpointName ?? "(unnamed)"}");
				}
			}
		}

		// Enumerate external devices
		Log ("");
		for (nint i = 0; i < Midi.ExternalDeviceCount; i++) {
			var extDevice = Midi.GetExternalDevice (i);
			if (extDevice is null)
				continue;
			Log ($"External Device {i}: UniqueID={extDevice.UniqueID}");
		}

		// FindByUniqueId demo
		if (Midi.DeviceCount > 0) {
			var firstDevice = Midi.GetDevice (0);
			if (firstDevice is not null) {
				var uid = firstDevice.UniqueID;
				var findStatus = MidiObject.FindByUniqueId (uid, out var found);
				Log ($"\nFindByUniqueId({uid}): status={findStatus}, found={found is not null}");
			}
		}
	}

	/// <summary>
	/// Demonstrates creating virtual MIDI sources and destinations.
	/// Uses: MidiClient.CreateVirtualSource (legacy), MidiClient.CreateVirtualSource (Protocol),
	///       MidiClient.CreateVirtualDestination (legacy), MidiClient.CreateVirtualDestination (Protocol),
	///       MidiClient.CreateOutputPort, MidiClient.CreateInputPort, MidiPort.PortName.
	/// </summary>
	void CreateVirtualEndpoints ()
	{
		if (client is null)
			return;

		Log ("=== Virtual Endpoint Creation ===");

		// Legacy API (obsolete but still functional - shown for comparison)
#pragma warning disable CA1422 // Validate platform compatibility (intentionally using obsolete API for demo)
		var legacySource = client.CreateVirtualSource ("SampleLegacySource", out var legacyStatus);
		Log ($"Legacy CreateVirtualSource: status={legacyStatus}");
		legacySource?.Dispose ();

		var legacyDest = client.CreateVirtualDestination ("SampleLegacyDest", out var legacyDestStatus);
		Log ($"Legacy CreateVirtualDestination: status={legacyDestStatus}");
		legacyDest?.Dispose ();
#pragma warning restore CA1422

		// Modern Protocol-based API
		var source = client.CreateVirtualSource ("SampleSource", MidiProtocolId.Protocol_1_0, out var srcStatus);
		Log ($"CreateVirtualSource (Protocol 1.0): status={srcStatus}, name={source?.EndpointName}");

		var dest = client.CreateVirtualSource ("SampleDest_2_0", MidiProtocolId.Protocol_2_0, out var dstStatus);
		Log ($"CreateVirtualSource (Protocol 2.0): status={dstStatus}, name={dest?.EndpointName}");

		// Create ports
		using var outputPort = client.CreateOutputPort ("SampleOutputPort");
		Log ($"CreateOutputPort: name={outputPort?.PortName}");

		using var inputPort = client.CreateInputPort ("SampleInputPort");
		Log ($"CreateInputPort: name={inputPort?.PortName}");

		// Get dictionary properties on the source
		if (source is not null) {
			var props = source.GetDictionaryProperties (false);
			if (props is not null)
				Log ($"Source properties count: {props.Count}");
		}

		// Flush output on destination
		if (source is not null) {
			source.FlushOutput ();
			Log ("FlushOutput called on source.");
		}

		// Create external device
		var extDevice = Midi.CreateExternalDevice ("SampleExtDevice", "SampleMfg", "SampleModel", out var extStatus);
		Log ($"CreateExternalDevice: status={extStatus}");
		if (extDevice is not null) {
			var addStatus = MidiSetup.AddExternalDevice (extDevice);
			Log ($"AddExternalDevice: status={addStatus}");
			var removeStatus = MidiSetup.RemoveExternalDevice (extDevice);
			Log ($"RemoveExternalDevice: status={removeStatus}");
		}

		// MidiDevice.Create (requires driver context, will return paramErr in user-space)
		var device = MidiDevice.Create (null, "SampleDevice", "SampleMfg", "SampleModel", out var devStatus);
		Log ($"MidiDevice.Create (no driver): status={(int) devStatus} (expected -50 paramErr)");

		source?.Dispose ();
		dest?.Dispose ();
	}

	/// <summary>
	/// Demonstrates sending MIDI data using the legacy MidiPacket API.
	/// Uses: MidiPacket constructor, MidiClient.CreateVirtualSource, MidiClient.CreateOutputPort.
	/// </summary>
	void SendMidiLegacy ()
	{
		if (client is null)
			return;

		Log ("=== Legacy MIDI Send (MidiPacket) ===");

		// Create a Note On packet (channel 0, note 60, velocity 100)
		var noteOnData = new byte [] { 0x90, 60, 100 };
		using var noteOnPacket = new MidiPacket (0, noteOnData);
		Log ($"MidiPacket: TimeStamp={noteOnPacket.TimeStamp}, Length={noteOnPacket.Length}");
		Log ($"  Data: [{string.Join (", ", noteOnPacket.ByteArray?.Select (b => $"0x{b:X2}") ?? Array.Empty<string> ())}]");

		// Create a Note Off packet
		var noteOffData = new byte [] { 0x80, 60, 0 };
		using var noteOffPacket = new MidiPacket (1000, noteOffData);
		Log ($"MidiPacket: TimeStamp={noteOffPacket.TimeStamp}, Length={noteOffPacket.Length}");

		// MidiPacket with range
		var fullData = new byte [] { 0xFF, 0x90, 64, 80, 0xFF };
		using var rangePacket = new MidiPacket (2000, fullData, 1, 3);
		Log ($"MidiPacket (range): Length={rangePacket.Length}");

		// Demonstrate Dispose
		var disposablePacket = new MidiPacket (0, (ushort) 3, Marshal.AllocHGlobal (3));
		Log ($"MidiPacket BytePointer before dispose: {disposablePacket.BytePointer != IntPtr.Zero}");
		disposablePacket.Dispose ();
		Log ($"MidiPacket BytePointer after dispose: {disposablePacket.BytePointer != IntPtr.Zero}");
	}

	/// <summary>
	/// Demonstrates sending MIDI data using the modern MidiEventList/UMP API.
	/// Uses: MidiEventList constructor, MidiEventList.Add, MidiEventList.PacketCount,
	///       MidiEventList.Protocol, MidiEventList.Iterate, MidiEventPacket.Timestamp,
	///       MidiEventPacket.WordCount, MidiEventPacket.Words, MidiEventPacket indexer.
	/// </summary>
	void SendMidiEventList ()
	{
		if (client is null)
			return;

		Log ("=== MidiEventList / UMP API ===");

		// Create an event list with Protocol 1.0
		using var list10 = new MidiEventList (MidiProtocolId.Protocol_1_0);
		Log ($"MidiEventList (Protocol 1.0): Protocol={list10.Protocol}, PacketCount={list10.PacketCount}");

		// Create an event list with Protocol 2.0
		using var list20 = new MidiEventList (MidiProtocolId.Protocol_2_0, 2048);
		Log ($"MidiEventList (Protocol 2.0, 2048 bytes): Protocol={list20.Protocol}, PacketCount={list20.PacketCount}");

		// Add UMP Note On: Type 2 (MIDI 1.0 Channel Voice), Group 0, Note On ch0, C4, vel 100
		// Format: 0x2090NNVV
		uint noteOnWord = 0x2090_3C64; // Note C4 (60=0x3C), velocity 100 (0x64)
		var added = list10.Add (0, new uint [] { noteOnWord });
		Log ($"Add NoteOn UMP: success={added}, PacketCount={list10.PacketCount}");

		// Add UMP Note Off
		uint noteOffWord = 0x2080_3C00;
		list10.Add (1000, new uint [] { noteOffWord });
		Log ($"Add NoteOff UMP: PacketCount={list10.PacketCount}");

		// Add multi-word packet (MIDI 2.0 Note On: 4 words)
		uint [] midi2NoteOn = { 0x4090_3C00, 0x0000_FFFF }; // Type 4, Note On, C4, max velocity
		list20.Add (0, midi2NoteOn);
		Log ($"Add MIDI 2.0 NoteOn (2 words): PacketCount={list20.PacketCount}");

		// Iterate using IEnumerable
		Log ("\nIterating MidiEventList (Protocol 1.0) via IEnumerable:");
		foreach (var packet in list10) {
			Log ($"  Packet: Timestamp={packet.Timestamp}, WordCount={packet.WordCount}, Word[0]=0x{packet.Words [0]:X8}");
		}

		// Iterate using callback (zero-copy)
		Log ("\nIterating via Iterate callback (zero-copy):");
		list10.Iterate ((ref MidiEventPacket packet) => {
			Log ($"  Packet: Timestamp={packet.Timestamp}, WordCount={packet.WordCount}");
			for (uint w = 0; w < packet.WordCount; w++)
				Log ($"    Word[{w}] = 0x{packet [(int) w]:X8}");
		});

		// Demonstrate MidiEventPacket struct
		Log ("\nMidiEventPacket struct:");
		var evtPacket = new MidiEventPacket ();
		evtPacket.Timestamp = 12345;
		evtPacket.Words = new uint [] { 0x2090_3C64, 0x2080_3C00 };
		Log ($"  Timestamp={evtPacket.Timestamp}, WordCount={evtPacket.WordCount}");
		Log ($"  Word[0]=0x{evtPacket [0]:X8}, Word[1]=0x{evtPacket [1]:X8}");

		// Demonstrate Add returning false when buffer is full
		using var smallList = new MidiEventList (MidiProtocolId.Protocol_1_0); // minimum size
		int count = 0;
		while (smallList.Add ((ulong) count, new uint [] { 0x2090_3C64 }))
			count++;
		Log ($"\nSmall buffer: fit {count} packets before running out of space (PacketCount={smallList.PacketCount})");
	}

	/// <summary>
	/// Demonstrates creating a Happy Birthday melody using MidiEventList with UMP packets.
	/// This showcases how MIDIEventListAdd merges packets with the same timestamp.
	/// Uses: MidiEventList.Add, MidiEventList.Iterate, MidiEventPacket.Words.
	/// </summary>
	async void PlayHappyBirthday ()
	{
		if (happyBirthdayIsPlaying) {
			Log ("Happy Birthday is already playing.");
			return;
		}

		happyBirthdayIsPlaying = true;
		try {
			Log ("=== Happy Birthday (MidiEventList) ===");

			using var list = new MidiEventList (MidiProtocolId.Protocol_1_0, 4096);

			// Happy Birthday melody (MIDI note numbers)
			// C4=60, D4=62, E4=64, F4=65, G4=67, A4=69, Bb4=70, C5=72
			byte [] melody = {
				60, 60, 62, 60, 65, 64,       // Hap-py Birth-day to You
				60, 60, 62, 60, 67, 65,       // Hap-py Birth-day to You
				60, 60, 72, 69, 65, 64, 62,  // Hap-py Birth-day dear friend
				70, 70, 69, 65, 67, 65        // Hap-py Birth-day to You
			};

			string [] lyrics = {
				"Hap", "py", "Birth", "day", "to", "You",
				"Hap", "py", "Birth", "day", "to", "You",
				"Hap", "py", "Birth", "day", "dear", "fri", "end",
				"Hap", "py", "Birth", "day", "to", "You"
			};

			ulong [] durations = {
				250, 250, 500, 500, 500, 1000,
				250, 250, 500, 500, 500, 1000,
				250, 250, 500, 500, 500, 500, 1000,
				250, 250, 500, 500, 500, 1000
			};

			byte velocity = 100;
			ulong currentTime = 0;

			for (int i = 0; i < melody.Length; i++) {
				// UMP Note On: 0x2090NNVV
				uint noteOn = (uint) (0x20900000 | (melody [i] << 8) | velocity);
				list.Add (currentTime, new uint [] { noteOn });

				// UMP Note Off: 0x2080NN00
				uint noteOff = (uint) (0x20800000 | (melody [i] << 8));
				list.Add (currentTime + durations [i], new uint [] { noteOff });

				currentTime += durations [i];
			}

			// Note: MIDIEventListAdd merges events with the same timestamp
			Log ($"Total packets: {list.PacketCount} (events merged by timestamp)");
			Log ($"Total notes: {melody.Length}");
			Log ("");

			// Print the melody
			int noteIndex = 0;
			list.Iterate ((ref MidiEventPacket packet) => {
				var words = packet.Words;
				for (int w = 0; w < words.Length; w++) {
					uint word = words [w];
					bool isNoteOn = (word & 0x00F00000) == 0x00900000;
					bool isNoteOff = (word & 0x00F00000) == 0x00800000;
					byte note = (byte) ((word >> 8) & 0xFF);
					string noteName = NoteToName (note);

					if (isNoteOn) {
						string lyric = noteIndex < lyrics.Length ? lyrics [noteIndex] : "";
						Log ($"  [{packet.Timestamp,6}] Note On:  {noteName,-4} (MIDI {note,3}) vel={word & 0xFF,3}  \"{lyric}\"");
						noteIndex++;
					} else if (isNoteOff) {
						Log ($"  [{packet.Timestamp,6}] Note Off: {noteName,-4} (MIDI {note,3})");
					}
				}
			});

			Log ("\nPlaying Happy Birthday through speakers...");
			await PlayMelodyOnSpeakers (melody, durations, velocity);
			Log ("Playback finished.");
		} finally {
			happyBirthdayIsPlaying = false;
		}
	}

	static string NoteToName (byte note)
	{
		string [] names = { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" };
		int octave = note / 12 - 1;
		return $"{names [note % 12]}{octave}";
	}

	/// <summary>
	/// Demonstrates the MIDI 2.0 struct types.
	/// Uses: Midi2DeviceManufacturer, Midi2DeviceRevisionLevel,
	///       MidiCIProfileId, MidiCIProfileIdStandard, MidiCIProfileIdManufacturerSpecific.
	/// </summary>
	void Midi2StructsDemo ()
	{
		Log ("=== MIDI 2.0 Structs ===");

		// Midi2DeviceManufacturer
		var mfg = new Midi2DeviceManufacturer ();
		mfg.SysExIdByte = new byte [] { 0x00, 0x21, 0x1C }; // Example: Ableton
		Log ($"Manufacturer SysEx ID: [{string.Join (", ", mfg.SysExIdByte.Select (b => $"0x{b:X2}"))}]");

		// Single-byte manufacturer (padded with zeroes)
		var yamaha = new Midi2DeviceManufacturer ();
		yamaha.SysExIdByte = new byte [] { 0x43, 0x00, 0x00 }; // Yamaha
		Log ($"Yamaha SysEx ID: [{string.Join (", ", yamaha.SysExIdByte.Select (b => $"0x{b:X2}"))}]");

		// Midi2DeviceRevisionLevel
		var rev = new Midi2DeviceRevisionLevel ();
		rev.RevisionLevel = new byte [] { 1, 0, 3, 7 };
		Log ($"Revision Level: [{string.Join (".", rev.RevisionLevel)}]");

		// MidiCIProfileId - Standard profile
		var profileId = new MidiCIProfileId ();
		var standard = new MidiCIProfileIdStandard {
			ProfileIdByte1 = 0x7E,
			ProfileBank = 0x01,
			ProfileNumber = 0x02,
			ProfileVersion = 0x01,
			ProfileLevel = 0x00
		};
		profileId.Standard = standard;
		var readBack = profileId.Standard;
		Log ($"Standard Profile: Bank={readBack.ProfileBank}, Number={readBack.ProfileNumber}, Version={readBack.ProfileVersion}");

		// MidiCIProfileId - Manufacturer-specific profile
		var mfgProfile = new MidiCIProfileIdManufacturerSpecific {
			SysExId1 = 0x00,
			SysExId2 = 0x21,
			SysExId3 = 0x1C,
			Info1 = 0x10,
			Info2 = 0x20
		};
		profileId.ManufacturerSpecific = mfgProfile;
		var readBackMfg = profileId.ManufacturerSpecific;
		Log ($"Manufacturer Profile: SysEx=[0x{readBackMfg.SysExId1:X2}, 0x{readBackMfg.SysExId2:X2}, 0x{readBackMfg.SysExId3:X2}], Info=[0x{readBackMfg.Info1:X2}, 0x{readBackMfg.Info2:X2}]");

		// MidiBluetoothDriver
		Log ("\nMidiBluetoothDriver:");
		Log ($"  ActivateAllConnections returns: {MidiBluetoothDriver.ActivateAllConnections ()}");

		// MidiError enum values
		Log ("\nMidiError values:");
		foreach (MidiError err in Enum.GetValues<MidiError> ())
			Log ($"  {err} = {(int) err}");

		// Midi.Restart
		Log ("\nRestarting MIDI subsystem...");
		Midi.Restart ();
		Log ("MIDI subsystem restarted.");
	}

	void Cleanup ()
	{
		client?.Dispose ();
		client = null;
		audioEngine?.Stop ();
		sampler?.Dispose ();
		audioEngine?.Dispose ();
		sampler = null;
		audioEngine = null;
	}
}
