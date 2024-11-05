# .NET for iOS, tvOS, macOS and Mac Catalyst samples

This repository contains .NET samples targeting iOS, tvOS, macOS and Mac Catalyst. The samples demonstrate usage of various
Apple API wrappers from C#. Visit the [Sample Gallery](https://docs.microsoft.com/samples/browse/?term=dotnet-macios)
to download individual samples.

See the [.NET MAUI Installation docs](https://docs.microsoft.com/en-us/dotnet/maui/get-started/installation) for setup instructions.

## Galleries

We love samples! Application samples show off our platform and provide a great way for people to learn our stuff. And we even promote them as a first-class feature of the docs site. You can find the sample galleries here:

- [MAUI Samples](https://learn.microsoft.com/samples/browse/?term=maui)

- [Android Samples](https://docs.microsoft.com/samples/browse/?term=dotnet-android)

- [iOS, tvOS, macOS and Mac Catalyst Samples](https://docs.microsoft.com/samples/browse/?term=dotnet-macios)

## Sample Requirements

We welcome sample submissions, please start by creating an issue with your proposal.

Because the sample galleries are powered by the github sample repos, each sample needs to have the following things:

- **Screenshots** - a folder called Screenshots that has at least one screen shot of the sample on each platform (preferably a screen shot for every page or every major piece of functionality).

- **Readme** - a `README.md` file that explains the sample, and contains metadata to help customers find it. The README file should begin with a YAML header (delimited by `---`) with the following keys/values:

    - **name** - must begin with `.NET for [iOS|tvOS|macOS|Mac Catalyst] -`

    - **description** - brief description of the sample (&lt; 150 chars) that appears in the sample code browser search

    - **page_type** - must be the string `sample`.

    - **languages** - coding language/s used in the sample, such as: `csharp` or `fsharp`

    - **products**: should be `dotnet-macios` for every sample in this repo

    - **urlFragment**: although this can be auto-generated, please supply an all-lowercase value that represents the sample's path in this repo, except directory separators are replaced with dashes (`-`) and no other punctuation.

    Here is an example:

    ```yaml
    ---
    name: .NET for iOS - iOS Demo 7000
    description: "Demonstrates new features in iOS 7000"
    page_type: sample
    languages:
    - csharp
    products:
    - dotnet-macios
    urlFragment: macios-iosdemo7000
    ---
    # Heading 1

    rest of README goes here, including screenshot images and requirements/instructions to get it running
    ```

    > NOTE: This must be valid YAML, so some characters in the name or description will require the entire string to be surrounded by " or ' quotes (to be safe, just quote all name and description values).

- **Buildable solution and .csproj file** - the project _must_ build and have the appropriate project scaffolding (solution + .csproj files).

This approach ensures that all samples integrate with the Microsoft [sample code browser](https://learn.microsoft.com/samples/browse/?term=dotnet-macios).


## Tips for .NET Migration

The goal here is to fully "modernize" the template for .NET and the latest version of C#.

Compare a `dotnet new ios` template named the same as the existing project.

1. If the root namespace doesn't match the project name, to get the
   existing code to compile, you may need:

```xml
<RootNamespace>Xamarin.MyDemo</RootNamespace>
```

2. Update any dependencies, NuGet packages, etc.

3. Remove unnecessary entries in the Info.list and project file, migrating Info.plist
   items that have an equivalent MSBuild property when possible.

4. Remove all unused using statements, since we now have
   `ImplicitUsings=enable`.

5. Fix all namespace declarations to use C# 10 file-scoped namespaces.

6. Build using `dotnet build`. Fix any warnings related to nullable reference types (`Nullable=enable`).

7. Run the app and ensure the sample still works.

## Porting to .NET

When porting a legacy sample to .NET, please make sure to preserve as
much history of the original sample as possible.  Some samples have
their project, source and resource files in the same directory where
the readme file, screenshot folder and other files not directly
related to the sample code reside.  Since .NET defaults to importing
all the files in the project directory as if they were part of the
project, the application code must first be moved to a subdirectory
(with the exception of the .sln file).

The new subdirectory should use the same name as the solution file,
without the .sln extension.  After creating it **first** move all the
relevant files and directories (source code, project file(s), the
`Properties` and `Resources` directories etc), using the `git mv`
command to the newly created directory, modify the .sln file to update
project file path(s) and **commit** these changes.  This ensures that
further changes will preserve commit history.

Now the sample is ready for porting.  After creating new project file
(using `dotnet new <platform> -n SampleName`) in a separate directory,
copy any necessary package and project references from the old
project, updating them as needed and after that replace the old
project file with the new one.  

A handful of useful tips (copied from the `dotnet` branch's README in
this repository):

  1. If the root namespace doesn't match the project name, to get the existing code to compile, you may need:

``` xml
<RootNamespace>Xamarin.MyDemo</RootNamespace>

```
  2. Update any dependencies, NuGet packages, etc.
  3. Remove unnecessary entries in the Info.list and project file, migrating Info.plist
     items that have an equivalent MSBuild property when possible.
  4. Remove all unused using statements, since we now have ImplicitUsings=enable.
  5. Fix all namespace declarations to use C# 10 file-scoped namespaces.
  6. Build. Fix any warnings related to nullable reference types (Nullable=enable).
  7. Run the app and ensure the sample still works.

## License

.NET (including the macios-samples repo) is licensed under the [MIT license](./LICENSE).

You may add their own copyright header, e.g. `// Copyright (c) A. N. Other.`, to your original contributions, as well as to files you modify and contribute back to the Microsoft OSS project, so long as you keep all pre-existing copyright notices intact in the files you have modified. Original contributions will be licensed using the [MIT license](./LICENSE) and should include the license header, e.g. `// Licensed under the MIT License.`.

## Trademarks

This project may contain trademarks or logos for projects, products, or
services. Authorized use of Microsoft trademarks or logos is subject to and
must follow Microsoft’s Trademark & Brand Guidelines. Use of Microsoft
trademarks or logos in modified versions of this project must not cause
confusion or imply Microsoft sponsorship. Any use of third-party trademarks or
logos are subject to those third-party’s policies.

# Contributing

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
