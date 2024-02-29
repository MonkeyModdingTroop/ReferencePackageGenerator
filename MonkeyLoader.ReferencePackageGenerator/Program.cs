using Newtonsoft.Json;

namespace MonkeyLoader.ReferencePackageGenerator
{
    internal class Program
    {
        private static readonly JsonSerializer _jsonSerializer = new();

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
                    var target = Path.Combine(config.TargetPath,
                        $"{Path.GetFileNameWithoutExtension(source)}{config.Suffix}{Path.GetExtension(source)}");

                    try
                    {
                        publicizer.CreatePublicAssembly(source, target);
                        Console.WriteLine($"Publicized {Path.GetFileName(source)} to {Path.GetFileName(target)}");
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