using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace MonkeyLoader.ReferencePackageGenerator
{
    [JsonObject]
    public class Config
    {
        [JsonProperty($"{nameof(DocumentationPath)}s", NullValueHandling = NullValueHandling.Ignore)]
        private string[]? _documentationPaths;

        [JsonProperty($"{nameof(SourcePath)}s", NullValueHandling = NullValueHandling.Ignore)]
        private string[] _sourcePaths = [Environment.CurrentDirectory];

        public string[] Authors { get; set; } = [];

        [JsonIgnore]
        public string DocumentationPath => _documentationPaths?.FirstOrDefault(Directory.Exists) ?? SourcePath;

        public string[] ExcludePatterns
        {
            get => [.. Excludes.Select(regex => regex.ToString())];

            [MemberNotNull(nameof(Excludes))]
            set => Excludes = value?.Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase)).ToArray() ?? [];
        }

        [JsonIgnore]
        public Regex[] Excludes { get; private set; } = [];

        [JsonIgnore]
        [MemberNotNullWhen(true, nameof(NupkgTargetPath))]
        public bool GenerateNugetPackages => !string.IsNullOrWhiteSpace(NupkgTargetPath);

        [JsonIgnore]
        [MemberNotNullWhen(true, nameof(PublicizedAssembliesTargetPath))]
        public bool GeneratePublicizedAssemblies => !string.IsNullOrWhiteSpace(PublicizedAssembliesTargetPath);

        [JsonIgnore]
        [MemberNotNullWhen(true, nameof(StrippedAssembliesTargetPath))]
        public bool GenerateStrippedAssemblies => !string.IsNullOrWhiteSpace(StrippedAssembliesTargetPath);

        public string IconPath { get; set; }

        public string IconUrl { get; set; }

        public string[] IncludePatterns
        {
            get => [.. Includes.Select(regex => regex.ToString())];

            [MemberNotNull(nameof(Includes))]
            set => Includes = value?.Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase)).ToArray() ?? [];
        }

        [JsonIgnore]
        public Regex[] Includes { get; private set; }

        public string? NupkgTargetPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "Packages");
        public string PackageIdPrefix { get; set; } = string.Empty;
        public string? ProjectUrl { get; set; }
        public string? PublicizedAssembliesTargetPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "Publicized");
        public NuGetPublishTarget? PublishTarget { get; set; }
        public bool Recursive { get; set; } = false;
        public string? RepositoryUrl { get; set; }

        [JsonIgnore]
        public string SourcePath => _sourcePaths.First(Directory.Exists);

        public string? StrippedAssembliesTargetPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "Stripped");
        public string[] Tags { get; set; } = [];
        public string TargetFramework { get; set; }
        public bool UseMockMethodBodies { get; set; } = false;

        [JsonIgnore]
        public Version VersionBoost { get; set; } = new();

        [JsonIgnore]
        public Dictionary<string, Version> VersionOverrides { get; set; }

        public string VersionReleaseLabel { get; set; }

        [JsonProperty(nameof(VersionBoost))]
        private string? VersionBoostString
        {
            get => VersionBoost.ToString();
            set => VersionBoost = value is null ? new() : new(value);
        }

        [JsonProperty(nameof(VersionOverrides))]
        private Dictionary<string, string> VersionOverrideStrings
        {
            get => VersionOverrides?.ToDictionary(entry => entry.Key, entry => entry.Value.ToString(), StringComparer.OrdinalIgnoreCase)!;
            set => VersionOverrides = value?.ToDictionary(entry => entry.Key, entry => new Version(entry.Value), StringComparer.OrdinalIgnoreCase) ?? new(StringComparer.OrdinalIgnoreCase);
        }

        public Config()
        {
            IncludePatterns = [@".+\.dll$", @".+\.exe$"];
            ExcludePatterns = [@"^Microsoft\..+", @"^System\..+", @"^Mono\..+", @"^UnityEngine\..+"];
        }

        public IEnumerable<string> Search()
        {
            foreach (var path in Directory.EnumerateFiles(SourcePath, "*", Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(path);

                if (Includes.Length != 0 && !Includes.Any(regex => regex.IsMatch(fileName)))
                    continue;

                if (Excludes.Length != 0 && Excludes.Any(regex => regex.IsMatch(fileName)))
                    continue;

                yield return path;
            }
        }
    }
}