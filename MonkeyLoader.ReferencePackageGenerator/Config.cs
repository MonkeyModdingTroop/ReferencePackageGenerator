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
        public string[] ExcludePatterns
        {
            get => Excludes.Select(regex => regex.ToString()).ToArray();

            [MemberNotNull(nameof(Excludes))]
            set => Excludes = value?.Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase)).ToArray() ?? [];
        }

        [JsonIgnore]
        public Regex[] Excludes { get; private set; } = [];

        public string[] IncludePatterns
        {
            get => Includes.Select(regex => regex.ToString()).ToArray();

            [MemberNotNull(nameof(Includes))]
            set => Includes = value?.Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase)).ToArray() ?? [];
        }

        [JsonIgnore]
        public Regex[] Includes { get; private set; }

        public bool Recursive { get; set; } = false;

        [JsonProperty(Required = Required.Always)]
        public string SourcePath { get; set; } = Environment.CurrentDirectory;

        public string Suffix { get; set; } = "";

        [JsonProperty(Required = Required.Always)]
        public string TargetPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "Public");

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