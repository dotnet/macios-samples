// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BackgroundAssets;
using Foundation;
using ObjCRuntime;

namespace BackgroundAssetsSample;

[Register ("BackgroundDownloadHandler")]
public class BackgroundDownloadHandler : NSObject, IBAManagedDownloaderExtension {
	protected BackgroundDownloadHandler (NativeHandle handle) : base (handle)
	{
	}

	public bool ShouldDownload (BAAssetPack assetPack)
	{
		Console.WriteLine ($"BackgroundDownloadHandler: allowing asset pack {assetPack.Identifier}");
		return true;
	}
}
