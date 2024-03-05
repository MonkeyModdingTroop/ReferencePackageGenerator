using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonkeyLoader.ReferencePackageGenerator
{
    [JsonObject]
    public class Config
    {
        [JsonProperty(nameof(DocumentationPath))]
        private string? _documentationPath = null;

        public string[] Authors { get; set; } = [];

        public string DllTargetPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "Public");

        [JsonIgnore]
        public string DocumentationPath => _documentationPath ?? SourcePath;

        public string[] ExcludePatterns
        {
            get => Excludes.Select(regex => regex.ToString()).ToArray();

            [MemberNotNull(nameof(Excludes))]
            set => Excludes = value?.Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase)).ToArray() ?? [];
        }

        [JsonIgnore]
        public Regex[] Excludes { get; private set; } = [];

        public string IconPath { get; set; }

        public string IconUrl { get; set; }

        public string[] IncludePatterns
        {
            get => Includes.Select(regex => regex.ToString()).ToArray();

            [MemberNotNull(nameof(Includes))]
            set => Includes = value?.Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase)).ToArray() ?? [];
        }

        [JsonIgnore]
        public Regex[] Includes { get; private set; }

        public string NupkgTargetPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "Packages");

        public string PackageIdPrefix { get; set; } = string.Empty;

        public string? ProjectUrl { get; set; }
        public NuGetPublishTarget? PublishTarget { get; set; }

        public bool Recursive { get; set; } = false;

        public string? RepositoryUrl { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string SourcePath { get; set; } = Environment.CurrentDirectory;

        public string[] Tags { get; set; } = [];

        public string TargetFramework { get; set; }

        [JsonIgnore]
        public Version VersionBoost { get; set; } = new();

        [JsonIgnore]
        public Dictionary<string, Version> VersionOverrides { get; set; }

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
            IncludePatterns = [@".+\.dll", @".+\.exe"];
            ExcludePatterns = [@"Microsoft\..+", @"System\..+", @"Mono\..+", @"UnityEngine\..+"];
        }

        public IEnumerable<string> Search()
        {
            foreach (var path in Directory.EnumerateFiles(SourcePath, "*", Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                if (Includes.Length != 0 && !Includes.Any(regex => regex.IsMatch(path)))
                    continue;

                if (Excludes.Length != 0 && Excludes.Any(regex => regex.IsMatch(path)))
                    continue;

                yield return path;
            }
        }
    }
}