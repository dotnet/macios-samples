// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

using FSKit;

namespace HelloFSExtension;

// A file or directory in the volume.
//
// FSKit identifies items by the FSItem instances the volume hands out,
// so every item must keep returning the same instance.
public class HelloItem : FSItem {
	readonly List<HelloItem> children = new ();

	public FSItemId Id { get; }
	public string Name { get; }
	public FSItemType Type { get; }
	public HelloItem? Parent { get; private set; }
	public byte [] Contents { get; private set; } = Array.Empty<byte> ();
	public IReadOnlyList<HelloItem> Children => children;

	HelloItem (FSItemId id, string name, FSItemType type)
	{
		Id = id;
		Name = name;
		Type = type;
	}

	public static HelloItem CreateRootDirectory ()
	{
		return new HelloItem (FSItemId.RootDirectory, "/", FSItemType.Directory);
	}

	public HelloItem AddFile (FSItemId id, string name, string contents)
	{
		var file = new HelloItem (id, name, FSItemType.File) { Parent = this, Contents = Encoding.UTF8.GetBytes (contents) };
		children.Add (file);
		return file;
	}

	public HelloItem? FindChild (string name)
	{
		// The volume is case insensitive (see FSfileObjectsAreCaseSensitive in Info.plist).
		return children.Find (v => string.Equals (v.Name, name, StringComparison.OrdinalIgnoreCase));
	}
}
