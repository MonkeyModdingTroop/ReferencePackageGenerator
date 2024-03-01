using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Packaging;
using NuGet.Versioning;
using Mono.Cecil;

namespace MonkeyLoader.ReferencePackageGenerator
{
    internal class Program
    {
        private static readonly JsonSerializer _jsonSerializer = new();

        private static void GenerateNuGetPackage(string target, AssemblyDefinition assembly)
        {
            var builder = new PackageBuilder();
            builder.Id = Path.GetFileNameWithoutExtension(target);
            builder.Version = new NuGetVersion(assembly.Name.Version);

            builder.Title = $"Publicized {Path.GetFileNameWithoutExtension(target)} Reference";
            builder.Description = $"Publicized reference package for {Path.GetFileName(target)}.";

            builder.Authors.Add("Config Authors");
            //builder.DependencyGroups.Add(new PackageDependencyGroup(
            //    targetFramework: NuGetFramework.Parse("netstandard1.4"),
            //    packages: new[]
            //    {
            //        new PackageDependency("Newtonsoft.Json", VersionRange.Parse("10.0.1"))
            //    }));
            builder.AddFiles("", target, "ref/net462/");

            using var outputStream = new FileStream(Path.Combine(Path.GetDirectoryName(target)!, $"{Path.GetFileNameWithoutExtension(target)}.nupkg"), FileMode.Create);
            builder.Save(outputStream);

            Console.WriteLine($"Saved a package to {outputStream.Name}");
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
                    Directory.CreateDirectory(config.TargetPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create target directory: {config.TargetPath}");
                    Console.WriteLine(ex.ToString());
                    continue;
                }

                Console.WriteLine($"Publicizing matching assembly files from: {config.SourcePath}");

                foreach (var source in config.Search())
                {
                    var target = Path.Combine(config.TargetPath, Path.GetFileName(source));

                    try
                    {
                        var assembly = publicizer.CreatePublicAssembly(source, target);
                        Console.WriteLine($"Publicized {Path.GetFileName(source)} to {Path.GetFileName(target)}");

                        GenerateNuGetPackage(target, assembly);
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