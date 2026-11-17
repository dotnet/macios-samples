// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FSKit;

namespace HelloFSExtension;

// This is the entry point of the file system extension: the build finds the only subclass of
// FSUnaryFileSystem in the project, and generates the native code needed to start the extension with it.
//
// FSKit calls this class to find out if it can handle a resource (ProbeResource),
// and then to create a volume for it (LoadResource).
public class HelloFileSystem : FSUnaryFileSystem, IFSUnaryFileSystemOperations {
	public void ProbeResource (FSResource resource, FSUnaryFileSystemOperationsProbeResourceResult replyHandler)
	{
		// HelloFS doesn't look at the resource's contents, so any path works.
		if (resource is not FSPathUrlResource) {
			replyHandler (FSProbeResult.NotRecognizedProbeResult, null);
			return;
		}
		replyHandler (FSProbeResult.CreateUsableProbeResult (HelloVolume.VolumeName, new FSContainerIdentifier { Uuid = HelloVolume.ContainerUuid }), null);
	}

	public void LoadResource (FSResource resource, FSTaskOptions options, FSUnaryFileSystemOperationsLoadResourceResult replyHandler)
	{
		if (resource is not FSPathUrlResource) {
			replyHandler (null, FSKitFunctions.GetErrorForPosixError (Errno.EINVAL));
			return;
		}
		ContainerStatus = FSContainerStatus.Ready;
		replyHandler (new HelloVolume (), null);
	}

	public void UnloadResource (FSResource resource, FSTaskOptions options, FSUnaryFileSystemOperationsUnloadResourceResult reply)
	{
		reply (null);
	}
}
