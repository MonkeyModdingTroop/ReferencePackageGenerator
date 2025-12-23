using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Packaging;
using NuGet.Versioning;
using Mono.Cecil;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;

namespace MonkeyLoader.ReferencePackageGenerator
{
    internal class Program
    {
        private static readonly JsonSerializer _jsonSerializer = new();

        private static string ChangeFileDirectory(string file, string newDirectory)
            => Path.Combine(newDirectory, Path.GetFileName(file));

        private static string ChangeFileDirectoryAndExtension(string file, string newDirectory, string newExtension)
            => Path.Combine(newDirectory, $"{Path.GetFileNameWithoutExtension(file)}{(newExtension.StartsWith('.') ? "" : ".")}{newExtension}");

        private static string ChangeFileExtension(string file, string newExtension)
            => Path.Combine(Path.GetDirectoryName(file)!, $"{Path.GetFileNameWithoutExtension(file)}{(newExtension.StartsWith('.') ? "" : ".")}{newExtension}");

        private static Version CombineVersions(Version primary, Version boost)
        {
            var primaries = new[] { primary.Major, primary.Minor, primary.Build, primary.Revision };
            var boosts = new[] { boost.Major, boost.Minor, boost.Build, boost.Revision };

            var merged = primaries.Zip(boosts, CombineVersionSegments).TakeWhile(segment => segment > -1).ToArray();

            return merged.Length switch
            {
                2 => new Version(merged[0], merged[1]),
                3 => new Version(merged[0], merged[1], merged[2]),
                4 => new Version(merged[0], merged[1], merged[2], merged[3]),
                _ => throw new InvalidOperationException("Need at least two segments in version!")
            };
        }

        private static int CombineVersionSegments(int primary, int boost)
        {
            if (boost == -1)
                return primary;

            if (primary == -1)
                return boost;

            return primary + boost;
        }

        private static void GenerateIgnoresAccessChecksToFile(string target)
        {
            var text =
$@"using System.Runtime.CompilerServices;

[assembly: IgnoresAccessChecksTo(""{Path.GetFileNameWithoutExtension(target)}"")]";

            File.WriteAllText(target, text);
        }

        private static async Task GenerateNuGetPackageAsync(Config config, string targetAssembly, Version version)
        {
            // This should add a package.props target that adds an assembly attribute like
            /*
                <AssemblyAttribute Include="System.Runtime.CompilerServices.IgnoresAccessChecksTo">
                    <_Parameter1>AssemblyName</_Parameter1>
                </AssemblyAttribute>
            */
            GenerateIgnoresAccessChecksToFile(ChangeFileExtension(targetAssembly, ".cs"));

            var documentationPath = ChangeFileExtension(targetAssembly, ".xml");
            var ignoreAccessChecksToPath = ChangeFileExtension(targetAssembly, ".cs");
            var isPublicized = File.Exists(ignoreAccessChecksToPath);

            var builder = new PackageBuilder
            {
                Id = $"{config.PackageIdPrefix}{Path.GetFileNameWithoutExtension(targetAssembly)}",
                Version = new NuGetVersion(version, config.VersionReleaseLabel),

                Title = $"{(isPublicized ? "Publicized " : "")}{Path.GetFileNameWithoutExtension(targetAssembly)} Reference",
                Description = $"{(isPublicized ? "Publicized r" : "R")}eference package for {Path.GetFileName(targetAssembly)}.",

                IconUrl = string.IsNullOrWhiteSpace(config.IconUrl) ? null : new Uri(config.IconUrl),
                ProjectUrl = string.IsNullOrWhiteSpace(config.ProjectUrl) ? null : new Uri(config.ProjectUrl),
                Repository = new RepositoryMetadata(Path.GetExtension(config.RepositoryUrl)?.TrimStart('.'), config.RepositoryUrl, null, null)
            };

            builder.Authors.AddRange(config.Authors);

            builder.Tags.AddRange(config.Tags);
            builder.Tags.Add("MonkeyLoader");
            builder.Tags.Add("ReferencePackageGenerator");

            // How-to dependencies:
            //builder.DependencyGroups.Add(new PackageDependencyGroup(
            //    targetFramework: NuGetFramework.Parse("netstandard1.4"),
            //    packages: new[]
            //    {
            //        new PackageDependency("Newtonsoft.Json", VersionRange.Parse("10.0.1"))
            //    }));

            var destinationPath = $"ref/{config.TargetFramework}/";
            builder.AddFiles("", targetAssembly, destinationPath);

            if (File.Exists(documentationPath))
                builder.AddFiles("", documentationPath, destinationPath);

            if (File.Exists(config.IconPath))
            {
                var iconName = Path.GetFileName(config.IconPath);
                builder.AddFiles("", config.IconPath, iconName);
                builder.Icon = iconName;
            }

            if (isPublicized)
            {
                builder.AddFiles("", ignoreAccessChecksToPath, "contentFiles/cs/any/IgnoresAccessChecksTo/");
                builder.AddFiles("", ignoreAccessChecksToPath, "content/IgnoresAccessChecksTo/");

                builder.ContentFiles.Add(new ManifestContentFiles
                {
                    Include = $"cs/any/IgnoresAccessChecksTo/{GetFilenameWithChangedFileExtension(targetAssembly, ".cs")}",
                    BuildAction = "compile",
                    Flatten = "false",
                    CopyToOutput = "false"
                });
            }

            var packagePath = Path.Combine(config.NupkgTargetPath!, $"{config.PackageIdPrefix}{GetFilenameWithChangedFileExtension(targetAssembly, ".nupkg")}");
            using (var outputStream = new FileStream(packagePath, FileMode.Create))
                builder.Save(outputStream);

            Console.WriteLine($"Saved package to {packagePath}");

            if (config.PublishTarget is null || !config.PublishTarget.Publish)
            {
                Console.WriteLine("No PublishTarget defined or publishing disabled, skipping package upload.");
                return;
            }

            Console.WriteLine($"Publishing package to {config.PublishTarget.Source}");

            var cache = new SourceCacheContext();
            var repository = Repository.Factory.GetCoreV3(config.PublishTarget.Source);
            var resource = await repository.GetResourceAsync<PackageUpdateResource>();

            try
            {
                await resource.Push([packagePath], null, 20, false, source => config.PublishTarget.ApiKey, source => null, false, true, null, ConsoleLogger.Instance);
                Console.WriteLine("Finished publishing package!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to publish package!");
                Console.WriteLine(ex.ToString());
            }
        }

        private static string GetFilenameWithChangedFileExtension(string file, string newExtension)
        => $"{Path.GetFileNameWithoutExtension(file)}{(newExtension.StartsWith('.') ? "" : ".")}{newExtension}";

        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: ReferencePackageGenerator.exe configPaths...");
                Console.WriteLine("There can be any number of config paths, which will be handled one by one.");
                Console.WriteLine("Missing config files will be generated.");
                return;
            }

            foreach (var configPath in args)
            {
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"Generating missing config file: {configPath}");

                    try
                    {
                        using var file = File.OpenWrite(configPath);
                        using var streamWriter = new StreamWriter(file);
                        using var jsonTextWriter = new JsonTextWriter(streamWriter);
                        jsonTextWriter.Formatting = Formatting.Indented;

                        _jsonSerializer.Serialize(jsonTextWriter, new Config());

                        file.SetLength(file.Position);
                        jsonTextWriter.Flush();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to generate config file!");
                        Console.WriteLine(ex.ToString());
                    }

                    continue;
                }

                Config config;

                try
                {
                    using var file = File.OpenRead(configPath);
                    using var streamReader = new StreamReader(file);
                    using var jsonTextReader = new JsonTextReader(streamReader);

                    config = _jsonSerializer.Deserialize<Config>(jsonTextReader)!;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to read config file: {configPath}");
                    Console.WriteLine(ex.ToString());
                    continue;
                }

                if (config is null)
                {
                    Console.WriteLine($"Failed to read config file: {configPath}");
                    continue;
                }

                var publicizer = new Publicizer();
                publicizer.Resolver.AddSearchDirectory(config.SourcePath);

                if (config.GenerateStrippedAssemblies)
                {
                    try
                    {
                        Directory.CreateDirectory(config.StrippedAssembliesTargetPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create stripped DLL target directory: {config.StrippedAssembliesTargetPath}");
                        Console.WriteLine(ex.ToString());
                        continue;
                    }
                }

                if (config.GeneratePublicizedAssemblies)
                {
                    try
                    {
                        Directory.CreateDirectory(config.PublicizedAssembliesTargetPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create publicized DLL target directory: {config.PublicizedAssembliesTargetPath}");
                        Console.WriteLine(ex.ToString());
                        continue;
                    }
                }

                if (config.GenerateNugetPackages)
                {
                    try
                    {
                        Directory.CreateDirectory(config.NupkgTargetPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create nupkg target directory: {config.NupkgTargetPath}");
                        Console.WriteLine(ex.ToString());
                        continue;
                    }
                }

                Console.WriteLine($"Publicizing matching assembly files from: {config.SourcePath}");

                foreach (var source in config.Search())
                {
                    string? packageTarget = null;
                    var relativeSource = Path.GetRelativePath(Environment.CurrentDirectory, source);

                    var docFile = ChangeFileDirectoryAndExtension(source, config.DocumentationPath, ".xml");
                    if (!File.Exists(docFile))
                    {
                        docFile = ChangeFileDirectoryAndExtension(source, config.SourcePath, ".xml");
                        if (!File.Exists(docFile))
                            docFile = null;
                    }

                    try
                    {
                        var assembly = publicizer.LoadAssembly(source);
                        assembly = publicizer.StripMethodBodies(assembly);

                        if (config.GenerateStrippedAssemblies)
                        {
                            var target = ChangeFileDirectory(source, config.StrippedAssembliesTargetPath);

                            assembly.Write(target);
                            packageTarget = target;

                            if (docFile is not null)
                                File.Copy(docFile, ChangeFileDirectory(docFile, config.StrippedAssembliesTargetPath), true);

                            Console.WriteLine($"Stripped {relativeSource} to {Path.GetRelativePath(Environment.CurrentDirectory, target)}");
                        }

                        if (config.GeneratePublicizedAssemblies)
                        {
                            var target = ChangeFileDirectory(source, config.PublicizedAssembliesTargetPath);

                            assembly = publicizer.Publicize(assembly);
                            assembly.Write(target);
                            packageTarget = target;

                            if (docFile is not null)
                                File.Copy(docFile, ChangeFileDirectory(docFile, config.PublicizedAssembliesTargetPath), true);

                            Console.WriteLine($"Publicized {relativeSource} to {Path.GetRelativePath(Environment.CurrentDirectory, target)}");
                        }

                        if (packageTarget is null || !config.GenerateNugetPackages)
                            continue;

                        var version = config.VersionOverrides.TryGetValue(Path.GetFileName(source), out var versionOverride)
                            ? versionOverride
                            : assembly.Name.Version;

                        version = CombineVersions(version, config.VersionBoost);

                        GenerateNuGetPackageAsync(config, packageTarget, version).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to process assembly: {Path.GetFileName(source)}");
                        Console.WriteLine(ex.ToString());
                        continue;
                    }
                }
            }
        }
    }
}