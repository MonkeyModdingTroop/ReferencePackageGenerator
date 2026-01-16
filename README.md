# MonkeyLoader Reference Package Generator

This MonkeyLoader tool allows generating stripped and / or publicized reference assemblies to distribute for modding purposes in particular.
Optionally, NuGet packages can be created from them as well, including uploading them NuGet feeds automatically.

Various configuration options for which assemblies to include and how to version them are available.


## Usage

```
dotnet tool ReferencePackageGenerator configPaths...
```

There can be any number of config paths, which will be handled one by one.
Missing config files will be generated.


## Config

Sample json config with all options:

```json
{
  "SourcePaths": [ "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Game" ],
  "DocumentationPaths": [ "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Game" ],
  "Recursive": false,
  "StrippedAssembliesTargetPath": "Stripped",
  "PublicizedAssembliesTargetPath": "Publicized",
  "IgnoreAccessChecksToPath": "IgnoreAccessChecksTo",
  "NupkgTargetPath": "Packages",
  "IncludePatterns": [
    "^Assembly-CSharp.dll$",
    "Game.*.dll$"
  ],
  "ExcludePatterns": [
    "Microsoft\\..+",
    "System\\..+",
    "Mono\\..+",
    "UnityEngine\\..+"
  ],
  "VersionBoost": "0.0.6.7",
  "VersionOverrides": {
    "Assembly-CSharp.dll": "1.0.4.2"
  },
  "PublishTarget": {
    "Source": "https://nuget.pkg.github.com/Me/index.json",
    "ApiKey": "ghp_xxxx",
    "Publish": true
  },
  "PackageIdPrefix": "Me.",
  "TargetFramework": "net10.0",
  "Authors": [
    "Game Publisher",
    "Me"
  ],
  "Tags": [ "Game", "stripped", "public", "reference", "assembly", "assemblies" ],
  "IconPath": "Icons\\GameIcon.png",
  "IconUrl": "https://raw.githubusercontent.com/Me/GameReferencePackages/main/Icons/GameIcon.png",
  "ProjectUrl": "https://github.com/Me/ReferencePackages",
  "RepositoryUrl": "https://github.com/Me/ReferencePackages.git"
}
```