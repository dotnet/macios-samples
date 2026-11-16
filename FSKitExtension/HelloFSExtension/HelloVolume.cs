// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Versioning;

using Darwin;
using FSKit;

namespace HelloFSExtension;

// A read-only volume with a couple of text files, all stored in memory.
public class HelloVolume : FSVolume, IFSVolumeOperations, IFSVolumeReadWriteOperations {
	public const string VolumeName = "HelloFS";

	// A fixed identifier, so the volume has the same identity every time it's mounted.
	public static readonly NSUuid ContainerUuid = new NSUuid ("6A3C9E52-7F0B-4E5D-9B21-5D1C8F2A4E10");

	// Directory enumerations stay valid as long as this value doesn't change.
	// The contents never change, so a constant works.
	const ulong DirectoryVerifier = 1;

	const uint UnknownId = 99; // macOS shows files owned by user/group 99 as owned by the current user.

	readonly HelloItem root;
	readonly TimeSpec creationTime;

	public HelloVolume ()
		: base (new FSVolumeIdentifier { Uuid = ContainerUuid }, FSFileName.Create (VolumeName))
	{
		var now = DateTimeOffset.UtcNow;
		creationTime = new TimeSpec { Seconds = (nint) now.ToUnixTimeSeconds () };

		root = HelloItem.CreateRootDirectory ();
		root.AddFile ((FSItemId) 3, "hello.txt", "Hello from a file system written in C#!\n");
		root.AddFile ((FSItemId) 4, "about.txt",
			$"This file system is implemented with FSKit and .NET {Environment.Version}.\n" +
			$"It was mounted at {now:u}.\n");
	}

	// FSVolumeCommonOperations

	public FSVolumeSupportedCapabilities SupportedVolumeCapabilities => new () {
		SupportsPersistentObjectIds = true,
		Supports64BitObjectIds = true,
		SupportsHiddenFiles = true,
		DoesNotSupportSettingFilePermissions = true,
		CaseFormat = FSVolumeCaseFormat.InsensitiveCasePreserving,
	};

	public FSStatFSResult VolumeStatistics {
		get {
			var usedBytes = (ulong) root.Children.Sum (v => v.Contents.Length);
			return new FSStatFSResult (VolumeName) {
				BlockSize = 4096,
				IoSize = 4096,
				TotalBytes = usedBytes,
				UsedBytes = usedBytes,
				TotalFiles = (ulong) root.Children.Count + 1,
			};
		}
	}

	// Ask FSKit to mount the volume read-only.
	[SupportedOSPlatform ("macos26.4")]
	public FSMountOptions RequestedMountOptions => FSMountOptions.ReadOnly;

	public void Mount (FSTaskOptions options, FSVolumeOperationsMountHandler reply) => reply (null);

	public void Unmount (Action reply) => reply ();

	public void Synchronize (FSSyncFlags flags, FSVolumeOperationsSynchronizeHandler reply) => reply (null);

	public void Reclaim (FSItem item, FSVolumeOperationsReclaimHandler reply) => reply (null);

	// FSVolumePathConfOperations

	public nint MaximumLinkCount => 1;

	public nint MaximumNameLength => 255;

	public bool RestrictsOwnershipChanges => true;

	public bool TruncatesLongNames => false;

	// FSVolumeOperations

	public void Activate (FSTaskOptions options, FSVolumeOperationsActivateHandler reply) => reply (root, null);

	public void Deactivate (FSDeactivateOptions options, FSVolumeOperationsDeactivateHandler reply) => reply (null);

	public void GetAttributes (FSItemGetAttributesRequest desiredAttributes, FSItem item, FSVolumeOperationsAttributesHandler reply)
	{
		reply (GetAttributes ((HelloItem) item), null);
	}

	public void LookupItem (FSFileName name, FSItem directory, FSVolumeOperationsLookupItemHandler reply)
	{
		var dir = (HelloItem) directory;
		if (dir.Type != FSItemType.Directory) {
			reply (null, null, Error (Errno.ENOTDIR));
			return;
		}
		var item = name.String is null ? null : dir.FindChild (name.String);
		if (item is null) {
			reply (null, null, Error (Errno.ENOENT));
			return;
		}
		reply (item, FSFileName.Create (item.Name), null);
	}

	public void EnumerateDirectory (FSItem directory, ulong startingAt, ulong verifier, FSItemGetAttributesRequest? attributes, FSDirectoryEntryPacker packer, FSVolumeOperationsEnumerateDirectoryHandler reply)
	{
		var dir = (HelloItem) directory;
		if (dir.Type != FSItemType.Directory) {
			reply (DirectoryVerifier, Error (Errno.ENOTDIR));
			return;
		}
		if (verifier != FSConstants.FSDirectoryVerifierInitial && verifier != DirectoryVerifier) {
			reply (DirectoryVerifier, NSError.FromDomain (FSErrorCode.InvalidDirectoryCookie.GetDomain ()!, (nint) (long) FSErrorCode.InvalidDirectoryCookie));
			return;
		}

		// When FSKit doesn't ask for attributes, the listing must include the "." and ".." entries.
		var entries = new List<(string Name, HelloItem Item)> ();
		if (attributes is null) {
			entries.Add ((".", dir));
			entries.Add (("..", dir.Parent ?? dir));
		}
		foreach (var child in dir.Children)
			entries.Add ((child.Name, child));

		// The cookie is the index of the next entry to return.
		for (var i = (int) startingAt; i < entries.Count; i++) {
			var (name, item) = entries [i];
			var itemAttributes = attributes is null ? null : GetAttributes (item);
			if (!packer.PackEntry (FSFileName.Create (name), item.Type, item.Id, (ulong) (i + 1), itemAttributes))
				break; // The packer is full; FSKit calls again with the last cookie we packed.
		}
		reply (DirectoryVerifier, null);
	}

	public void ReadSymbolicLink (FSItem item, FSVolumeOperationsReadSymbolicLinkHandler reply) => reply (null, Error (Errno.EINVAL));

	// The volume is read-only, so all operations that would modify it fail.

	public void SetAttributes (FSItemSetAttributesRequest newAttributes, FSItem item, FSVolumeOperationsAttributesHandler reply) => reply (null, Error (Errno.EROFS));

	public void CreateItem (FSFileName name, FSItemType type, FSItem directory, FSItemSetAttributesRequest newAttributes, FSVolumeOperationsCreateItemHandler reply) => reply (null, null, Error (Errno.EROFS));

	public void CreateSymbolicLink (FSFileName name, FSItem directory, FSItemSetAttributesRequest newAttributes, FSFileName contents, FSVolumeOperationsCreateItemHandler reply) => reply (null, null, Error (Errno.EROFS));

	public void CreateLink (FSItem item, FSFileName name, FSItem directory, FSVolumeOperationsCreateLinkHandler reply) => reply (null, Error (Errno.EROFS));

	public void RemoveItem (FSItem item, FSFileName name, FSItem directory, FSVolumeOperationsRemoveItemHandler reply) => reply (Error (Errno.EROFS));

	public void RenameItem (FSItem item, FSItem sourceDirectory, FSFileName sourceName, FSFileName destinationName, FSItem destinationDirectory, FSItem? overItem, FSVolumeOperationsRenameItemHandler reply) => reply (null, Error (Errno.EROFS));

	// FSVolumeReadWriteOperations

	public void Read (FSItem item, long offset, nuint length, FSMutableFileDataBuffer buffer, FSVolumeReadWriteOperationsReadHandler reply)
	{
		var file = (HelloItem) item;
		if (file.Type != FSItemType.File) {
			reply (0, Error (Errno.EISDIR));
			return;
		}
		if (offset < 0) {
			reply (0, Error (Errno.EINVAL));
			return;
		}
		var contents = file.Contents;
		if (offset >= contents.Length) {
			reply (0, null);
			return;
		}
		var count = (int) Math.Min (Math.Min ((ulong) length, (ulong) buffer.Length), (ulong) (contents.Length - offset));
		System.Runtime.InteropServices.Marshal.Copy (contents, (int) offset, buffer.MutableBytes, count);
		reply ((nuint) count, null);
	}

	public void Write (NSData contents, FSItem item, long offset, FSVolumeReadWriteOperationsWriteHandler reply) => reply (0, Error (Errno.EROFS));

	FSItemAttributes GetAttributes (HelloItem item)
	{
		const uint S_IFDIR = 0x4000; // 0040000
		const uint S_IFREG = 0x8000; // 0100000
		var isDirectory = item.Type == FSItemType.Directory;
		var size = (ulong) item.Contents.Length;
		return new FSItemAttributes {
			Type = item.Type,
			Mode = isDirectory ? S_IFDIR | 0x16D /* 0555 */ : S_IFREG | 0x124 /* 0444 */,
			LinkCount = isDirectory ? 2u : 1u,
			Uid = UnknownId,
			Gid = UnknownId,
			Size = size,
			AllocSize = size,
			FileId = item.Id,
			ParentId = item.Parent?.Id ?? FSItemId.ParentOfRoot,
			AccessTime = creationTime,
			ModifyTime = creationTime,
			ChangeTime = creationTime,
			BirthTime = creationTime,
		};
	}

	static NSError? Error (int posixError) => FSKitFunctions.GetErrorForPosixError (posixError);
}
