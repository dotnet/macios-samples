// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Foundation;

namespace XpcService;

static class Program {
	static async Task<int> Main ()
	{
		Console.WriteLine ("Sending one request to the bundled XPC service...");
		using var connection = new NSXpcConnection ("com.microsoft.samples.xpcservice.echo");
		var completion = new TaskCompletionSource<string> (TaskCreationOptions.RunContinuationsAsynchronously);
		connection.RemoteInterface = NSXpcInterface.Create (typeof (IEchoProtocol));
		connection.InterruptionHandler = () => completion.TrySetException (new IOException ("The XPC service was interrupted."));
		connection.InvalidationHandler = () => completion.TrySetException (new IOException ("The XPC connection was invalidated."));
		connection.Activate ();

		try {
			using var proxy = connection.CreateRemoteObjectProxy<IEchoProtocol> (error =>
				completion.TrySetException (new NSErrorException (error)));
			const string message = "Hello from the host";
			proxy.Echo (message, reply => completion.TrySetResult (reply));
			var response = await completion.Task.WaitAsync (TimeSpan.FromSeconds (10)).ConfigureAwait (false);
			if (response != message)
				throw new InvalidDataException ($"Unexpected reply: '{response}'.");
			Console.WriteLine ($"XPC reply: {response}");
			return 0;
		} catch (Exception ex) when (ex is IOException or NSErrorException or TimeoutException) {
			Console.Error.WriteLine ($"XPC request failed: {ex.Message}");
			return 1;
		} finally {
			connection.Invalidate ();
		}
	}
}
