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

        private static string GenerateIgnoresAccessChecksToFile(string target)
        {
            var text =
$@"using System.Runtime.CompilerServices;

[assembly: IgnoresAccessChecksTo(""{Path.GetFileNameWithoutExtension(target)}"")]";

            var csFile = ChangeFileExtension(target, ".cs");
            File.WriteAllText(csFile, text);

            return csFile;
        }

        private static async Task GenerateNuGetPackageAsync(Config config, string target, AssemblyDefinition assembly)
        {
            var version = config.VersionOverrides.TryGetValue(Path.GetFileName(target), out var versionOverride)
            ? versionOverride
            : assembly.Name.Version;

            version = CombineVersions(version, config.VersionBoost);

            var builder = new PackageBuilder
            {
                Id = $"{config.PackageIdPrefix}{Path.GetFileNameWithoutExtension(target)}",
                Version = new NuGetVersion(version),

                Title = $"Publicized {Path.GetFileNameWithoutExtension(target)} Reference",
                Description = $"Publicized reference package for {Path.GetFileName(target)}.",

                IconUrl = string.IsNullOrWhiteSpace(config.IconUrl) ? null : new Uri(config.IconUrl),
                ProjectUrl = string.IsNullOrWhiteSpace(config.ProjectUrl) ? null : new Uri(config.ProjectUrl),
                Repository = new RepositoryMetadata(Path.GetExtension(config.RepositoryUrl)?.TrimStart('.'), config.RepositoryUrl, null, null)
            };

            builder.Authors.AddRange(config.Authors);

            builder.Tags.AddRange(config.Tags);
            builder.Tags.Add("MonkeyLoader");
            builder.Tags.Add("ReferencePackageGenerator");

            //builder.DependencyGroups.Add(new PackageDependencyGroup(
            //    targetFramework: NuGetFramework.Parse("netstandard1.4"),
            //    packages: new[]
            //    {
            //        new PackageDependency("Newtonsoft.Json", VersionRange.Parse("10.0.1"))
            //    }));

            var destinationPath = $"ref/{config.TargetFramework}/";
            builder.AddFiles("", target, destinationPath);

            var docFile = ChangeFileDirectoryAndExtension(target, config.DocumentationPath, ".xml");
            if (File.Exists(docFile))
            {
                builder.AddFiles("", docFile, destinationPath);
            }
            else
            {
                docFile = ChangeFileDirectoryAndExtension(target, config.SourcePath, ".xml");
                if (File.Exists(docFile))
                    builder.AddFiles("", docFile, destinationPath);
            }

            if (File.Exists(config.IconPath))
            {
                var iconName = Path.GetFileName(config.IconPath);
                builder.AddFiles("", config.IconPath, iconName);
                builder.Icon = iconName;
            }

            var ignoreAccessChecksToPath = GenerateIgnoresAccessChecksToFile(target);
            builder.AddFiles("", ignoreAccessChecksToPath, "contentFiles/any/any/IgnoresAccessChecksTo/");
            builder.AddFiles("", ignoreAccessChecksToPath, "content/IgnoresAccessChecksTo/");

            builder.ContentFiles.Add(new ManifestContentFiles
            {
                Include = $"any/any/IgnoresAccessChecksTo/{Path.GetFileNameWithoutExtension(target)}.cs",
                BuildAction = "content",
                Flatten = "true",
                CopyToOutput = "true"
            });

            var packagePath = Path.Combine(config.NupkgTargetPath, $"{config.PackageIdPrefix}{Path.GetFileNameWithoutExtension(target)}.nupkg");
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
                await resource.Push(new List<string>() { packagePath }, null, 20, false, source => config.PublishTarget.ApiKey, source => null, false, true, null, ConsoleLogger.Instance);
                Console.WriteLine("Finished publishing package!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to publish package!");
                Console.WriteLine(ex.ToString());
            }
        }

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

                try
                {
                    Directory.CreateDirectory(config.DllTargetPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create DLL target directory: {config.DllTargetPath}");
                    Console.WriteLine(ex.ToString());
                    continue;
                }

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

                Console.WriteLine($"Publicizing matching assembly files from: {config.SourcePath}");

                foreach (var source in config.Search())
                {
                    var target = ChangeFileDirectory(source, config.DllTargetPath);

                    try
                    {
                        var assembly = publicizer.CreatePublicAssembly(source, target);
                        Console.WriteLine($"Publicized {Path.GetFileName(source)} to {Path.GetFileName(target)}");

                        GenerateNuGetPackageAsync(config, target, assembly).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to publicize assembly: {Path.GetFileName(source)}");
                        Console.WriteLine(ex.ToString());
                        continue;
                    }
                }
            }
        }
    }
}