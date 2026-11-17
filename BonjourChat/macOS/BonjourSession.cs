// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using System.Text;
using CoreFoundation;
using Network;

namespace BonjourChat;

// All methods, callbacks and events run on the main dispatch queue.
sealed class BonjourSession {
	public const string ServiceType = "_bonjourchat._tcp";
	readonly Dictionary<string, NWEndpoint> peers = new Dictionary<string, NWEndpoint> (StringComparer.Ordinal);
	NWListener? listener;
	NWBrowser? browser;
	NWConnection? connection;
	bool sending;

	public string ServiceName { get; } = $"BonjourChat-{Guid.NewGuid ().ToString ("N") [..8]}";
	public string [] PeerNames => peers.Keys.OrderBy (name => name, StringComparer.Ordinal).ToArray ();
	public bool Running => listener is not null;
	public bool Connected { get; private set; }
	public bool Busy => connection is not null;
	public bool CanSend => Connected && !sending;
	public event Action? Changed;
	public event Action<string>? Log;
	public event Action<string>? MessageReceived;

	static NWParameters CreateParameters ()
	{
		var parameters = NWParameters.CreateTcp ();
		parameters.IncludePeerToPeer = true;
		return parameters;
	}

	public void Start ()
	{
		if (Running)
			return;
		using var parameters = CreateParameters ();
		var newListener = NWListener.Create (parameters);
		if (newListener is null) {
			Log?.Invoke ("Could not create a TCP listener. Click Start to retry.");
			return;
		}
		using var advertisement = NWAdvertiseDescriptor.CreateBonjourService (ServiceName, ServiceType, "local.");
		if (advertisement is null) {
			newListener.Dispose ();
			Log?.Invoke ("Could not create a Bonjour advertisement. Click Start to retry.");
			return;
		}
		listener = newListener;
		advertisement.NoAutoRename = true;
		newListener.SetAdvertiseDescriptor (advertisement);
		newListener.SetQueue (DispatchQueue.MainQueue);
		newListener.SetStateChangedHandler ((state, error) => {
			if (state == NWListenerState.Cancelled) {
				newListener.Dispose ();
				return;
			}
			if (listener != newListener)
				return;
			Log?.Invoke ($"Listener: {state}{Describe (error)}");
			if (state == NWListenerState.Ready)
				Log?.Invoke ($"Advertising {ServiceName} on TCP port {newListener.Port}.");
			else if (state == NWListenerState.Failed)
				Stop ();
		});
		newListener.SetNewConnectionHandler (incoming => {
			if (listener != newListener || Busy) {
				Log?.Invoke ("Rejected an incoming connection (stopped or already connected).");
				incoming.Cancel ();
				incoming.Dispose ();
				return;
			}
			Log?.Invoke ("Accepting an incoming connection.");
			StartConnection (incoming);
		});

		using var descriptor = NWBrowserDescriptor.CreateBonjourService (ServiceType, "local.");
		var newBrowser = new NWBrowser (descriptor, parameters);
		browser = newBrowser;
		newBrowser.SetDispatchQueue (DispatchQueue.MainQueue);
		newBrowser.SetStateChangesHandler ((state, error) => {
			if (state == NWBrowserState.Cancelled) {
				newBrowser.Dispose ();
				return;
			}
			if (browser != newBrowser)
				return;
			Log?.Invoke ($"Browser: {state}{Describe (error)}");
			if (state == NWBrowserState.Failed)
				Stop ();
		});
		newBrowser.IndividualChangesDelegate = (oldResult, newResult) => {
			if (browser != newBrowser)
				return;
			if (oldResult is not null) {
				using var endpoint = oldResult.EndPoint;
				if (endpoint.BonjourServiceName is string oldName && peers.Remove (oldName, out var removed))
					removed.Dispose ();
			}
			if (newResult is not null) {
				// EndPoint returns an owned copy; the browse result itself only lives during this callback.
				var endpoint = newResult.EndPoint;
				string? name = endpoint.BonjourServiceName;
				if (name is null) {
					Log?.Invoke ("Ignored a browse result without a Bonjour service name.");
					endpoint.Dispose ();
					return;
				}
				if (string.Equals (name, ServiceName, StringComparison.Ordinal)) {
					endpoint.Dispose ();
				} else {
					if (peers.Remove (name, out var replaced))
						replaced.Dispose ();
					peers.Add (name, endpoint);
				}
			}
			Changed?.Invoke ();
		};
		newListener.Start ();
		newBrowser.Start ();
		Changed?.Invoke ();
	}

	public void Connect (string name)
	{
		if (!Running || Busy || !peers.TryGetValue (name, out var endpoint)) {
			Log?.Invoke ("Cannot connect: select an available peer while disconnected.");
			return;
		}
		using var parameters = CreateParameters ();
		Log?.Invoke ($"Connecting to {name}...");
		StartConnection (new NWConnection (endpoint, parameters));
	}

	void StartConnection (NWConnection current)
	{
		connection = current;
		var protocol = new ChatProtocol ();
		bool receiveStarted = false;
		current.SetQueue (DispatchQueue.MainQueue);
		current.SetStateChangeHandler ((state, error) => {
			if (state == NWConnectionState.Cancelled) {
				current.Dispose ();
				return;
			}
			if (connection != current)
				return;
			Log?.Invoke ($"Connection: {state}{Describe (error)}");
			Connected = state == NWConnectionState.Ready;
			if (Connected && !receiveStarted) {
				receiveStarted = true;
				Receive (current, protocol);
			} else if (state == NWConnectionState.Failed)
				Disconnect ();
			Changed?.Invoke ();
		});
		current.Start ();
		Changed?.Invoke ();
	}

	void Receive (NWConnection current, ChatProtocol protocol)
	{
		current.Receive (1, 4096, (data, size, context, complete, error) => {
			if (connection != current)
				return;
			if (size > 0) {
				var bytes = new byte [checked ((int) size)];
				Marshal.Copy (data, bytes, 0, bytes.Length);
				try {
					protocol.Append (bytes, message => {
						Log?.Invoke ($"Peer: {message}");
						MessageReceived?.Invoke (message);
					});
				} catch (Exception ex) when (ex is InvalidDataException || ex is DecoderFallbackException) {
					Log?.Invoke ($"Invalid peer message: {ex.Message}");
					Disconnect ();
					return;
				}
			}
			if (error is not null || complete) {
				Log?.Invoke (error is not null ? $"Receive failed{Describe (error)}"
					: protocol.HasPartialMessage ? "Peer closed with an incomplete message." : "Peer disconnected.");
				Disconnect ();
			} else if (connection == current) {
				Receive (current, protocol);
			}
		});
	}

	public bool Send (string message)
	{
		var current = connection;
		if (current is null || !CanSend) {
			Log?.Invoke ("Cannot send: wait for a ready connection and the previous send to finish.");
			return false;
		}
		byte [] bytes;
		try {
			bytes = ChatProtocol.Encode (message);
		} catch (Exception ex) when (ex is InvalidDataException || ex is EncoderFallbackException) {
			Log?.Invoke (ex.Message);
			return false;
		}
		sending = true;
		Changed?.Invoke ();
		current.Send (bytes, NWContentContext.DefaultMessage, true, error => {
			if (connection != current)
				return;
			sending = false;
			if (error is not null) {
				Log?.Invoke ($"Send failed{Describe (error)}");
				Disconnect ();
			} else {
				Log?.Invoke ($"You: {message}");
				Changed?.Invoke ();
			}
		});
		return true;
	}

	public void Disconnect ()
	{
		var previous = connection;
		connection = null;
		Connected = false;
		sending = false;
		previous?.Cancel ();
		Changed?.Invoke ();
	}

	public void Stop ()
	{
		Disconnect ();
		var previousBrowser = browser;
		var previousListener = listener;
		browser = null;
		listener = null;
		previousBrowser?.Cancel ();
		previousListener?.Cancel ();
		foreach (var endpoint in peers.Values)
			endpoint.Dispose ();
		peers.Clear ();
		Log?.Invoke ("Networking stopped. Click Start to advertise and browse again.");
		Changed?.Invoke ();
	}

	static string Describe (NWError? error)
	{
		return error is null ? "" : $" ({error.ErrorDomain}: {error.ErrorCode})";
	}
}
