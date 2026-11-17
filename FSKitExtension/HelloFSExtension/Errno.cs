// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace HelloFSExtension;

// The POSIX error codes this sample uses, from <sys/errno.h>.
static class Errno {
	public const int ENOENT = 2;
	public const int ENOTDIR = 20;
	public const int EISDIR = 21;
	public const int EINVAL = 22;
	public const int EROFS = 30;
}
