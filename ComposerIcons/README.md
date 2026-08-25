# Composer Icons Sample

This sample demonstrates how to use Xcode's **Icon Composer** `.icon` format for app icons in .NET for Apple platform applications (iOS, tvOS, Mac Catalyst, and macOS).

## Overview

Starting with Xcode 26, Apple introduced [Icon Composer](https://developer.apple.com/documentation/Xcode/creating-your-app-icon-using-icon-composer), a new tool for creating layered app icons that support the Liquid Glass design system. Icon Composer outputs `.icon` directories (folder packages) that replace traditional `.appiconset` folders inside `.xcassets` asset catalogs.

## How It Works

1. **Create your icon** using Xcode's Icon Composer tool, which produces an `AppIcon.icon` directory containing:
   - `icon.json` — Icon metadata describing layers and configuration
   - `Assets/` — Image files for the icon layers (PNG or SVG)

2. **Add the `.icon` folder** to your project. The build system automatically includes files inside `.icon` directories as `ImageAsset` items.

3. **Set `AppIcon`** in your project file:
   ```xml
   <PropertyGroup>
       <AppIcon>AppIcon</AppIcon>
   </PropertyGroup>
   ```

4. **Build and run** — the `.icon` folder is passed to `actool` just like an `.xcassets` catalog, and the icon is compiled into `Assets.car`.

## Project Structure

```
ComposerIcons/
├── iOS/
│   ├── ComposerIcons.csproj
│   ├── AppDelegate.cs
│   ├── SceneDelegate.cs
│   ├── Main.cs
│   ├── Info.plist
│   └── AppIcon.icon/          ← Icon Composer output
│       ├── icon.json
│       └── Assets/
│           ├── front.png
│           └── back.png
├── tvOS/
│   ├── ComposerIcons.csproj
│   ├── AppDelegate.cs
│   ├── SceneDelegate.cs
│   ├── Main.cs
│   ├── Info.plist
│   └── AppIcon.icon/
│       ├── icon.json
│       └── Assets/
│           ├── front.png
│           └── back.png
├── MacCatalyst/
│   ├── ComposerIcons.csproj
│   ├── AppDelegate.cs
│   ├── SceneDelegate.cs
│   ├── Main.cs
│   ├── Info.plist
│   └── AppIcon.icon/
│       ├── icon.json
│       └── Assets/
│           ├── front.png
│           └── back.png
└── macOS/
    ├── ComposerIcons.csproj
    ├── AppDelegate.cs
    ├── Main.cs
    ├── Info.plist
    └── AppIcon.icon/
        ├── icon.json
        └── Assets/
            ├── front.png
            └── back.png
```

## Requirements

- .NET 10+
- Xcode 26+
- macOS 26 (Tahoe) or later

## Notes

- The `.icon` format can be used alongside traditional `.xcassets` asset catalogs in the same project.
- Both primary and alternate app icons are supported with the `.icon` format.
- For production apps, create your `.icon` file using Xcode's Icon Composer tool rather than hand-crafting the directory structure.
