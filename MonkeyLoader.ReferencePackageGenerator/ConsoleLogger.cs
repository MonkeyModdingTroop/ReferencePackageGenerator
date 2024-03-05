using NuGet.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonkeyLoader.ReferencePackageGenerator
{
    internal class ConsoleLogger : ILogger
    {
        public static readonly ILogger Instance = new ConsoleLogger();

        private ConsoleLogger()
        { }

        public void Log(LogLevel level, string data) => Console.WriteLine($"[NuGet] [{level}] {data})");

        public void Log(ILogMessage message) => Console.WriteLine(message.ToString());

        public Task LogAsync(LogLevel level, string data) => Task.Run(() => Log(level, data));

        public Task LogAsync(ILogMessage message) => Task.Run(() => Log(message));

        public void LogDebug(string data) => Log(LogLevel.Debug, data);

        public void LogError(string data) => Log(LogLevel.Error, data);

        public void LogInformation(string data) => Log(LogLevel.Information, data);

        public void LogInformationSummary(string data) => Log(LogLevel.Information, data);

        public void LogMinimal(string data) => Log(LogLevel.Minimal, data);

        public void LogVerbose(string data) => Log(LogLevel.Verbose, data);

        public void LogWarning(string data) => Log(LogLevel.Warning, data);
    }
}