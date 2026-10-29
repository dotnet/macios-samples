// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace BonjourChat;

// TCP is a byte stream: a receive can contain part of a line, or several lines.
sealed class ChatProtocol {
	public const int MaxMessageBytes = 4096;
	static readonly UTF8Encoding utf8 = new UTF8Encoding (false, true);
	readonly List<byte> pending = new List<byte> ();

	public static byte [] Encode (string message)
	{
		if (string.IsNullOrWhiteSpace (message) || message.Contains ('\n') || message.Contains ('\r'))
			throw new InvalidDataException ("Enter a nonempty, single-line message.");
		if (utf8.GetByteCount (message) > MaxMessageBytes)
			throw new InvalidDataException ($"Messages must be at most {MaxMessageBytes} UTF-8 bytes.");
		return utf8.GetBytes (message + "\n");
	}

	public void Append (byte [] data, Action<string> received)
	{
		foreach (byte value in data) {
			if (value == (byte) '\n') {
				string message = utf8.GetString (pending.ToArray ());
				// Apply the same validation to received messages as to sent messages.
				Encode (message);
				pending.Clear ();
				received (message);
			} else {
				if (pending.Count == MaxMessageBytes)
					throw new InvalidDataException ("Peer exceeded the message size limit.");
				pending.Add (value);
			}
		}
	}

	public bool HasPartialMessage => pending.Count != 0;
}
