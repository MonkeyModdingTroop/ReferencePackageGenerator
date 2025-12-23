using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonkeyLoader.ReferencePackageGenerator
{
    public class AssemblyResolver : IAssemblyResolver
    {
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

        public void AddSearchDirectory(string directory)
        {
            _directories.Add(directory);

            foreach (var dir in Directory.EnumerateDirectories(directory, "*", new EnumerationOptions() { RecurseSubdirectories = true }))
                _directories.Add(dir);
        }

        public void Dispose()
            => GC.SuppressFinalize(this);

        public AssemblyDefinition Resolve(AssemblyNameReference name)
            => Resolve(name, new ReaderParameters());

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            => SearchDirectory(name, _directories, parameters) ?? throw new AssemblyResolutionException(name);

        private AssemblyDefinition GetAssembly(string file, ReaderParameters parameters)
        {
            parameters.AssemblyResolver ??= this;

            return ModuleDefinition.ReadModule(file, parameters).Assembly;
        }

        private AssemblyDefinition? SearchDirectory(AssemblyNameReference name, IEnumerable<string> directories, ReaderParameters parameters)
        {
            string[] extensions = name.IsWindowsRuntime ? [".winmd", ".dll"] : [".exe", ".dll"];

            foreach (var directory in directories)
            {
                foreach (var extension in extensions)
                {
                    var file = Path.Combine(directory, name.Name + extension);
                    if (!File.Exists(file))
                        continue;

                    try
                    {
                        return GetAssembly(file, parameters);
                    }
                    catch (BadImageFormatException)
                    { }
                }
            }

            return null;
        }
    }
}