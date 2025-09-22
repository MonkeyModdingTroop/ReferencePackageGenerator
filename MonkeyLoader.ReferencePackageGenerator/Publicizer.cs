using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MonkeyLoader.ReferencePackageGenerator
{
    public class Publicizer
    {
        public AssemblyResolver Resolver { get; } = new();

        public Publicizer()
        {
            Resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(string).Assembly.Location)!);
        }

        public AssemblyDefinition LoadAssembly(string source)
            => AssemblyDefinition.ReadAssembly(source, new() { AssemblyResolver = Resolver });

        public AssemblyDefinition Publicize(AssemblyDefinition assembly)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    type.IsPublic = true;

                    if (type.IsNested)
                        type.IsNestedPublic = true;

                    foreach (var field in type.Fields)
                    {
                        if (!type.Properties.Any(property => property.Name == field.Name) && !type.Events.Any(e => e.Name == field.Name))
                            field.IsPublic = true;
                    }

                    foreach (var method in type.Methods)
                    {
                        method.IsPublic = true;
                    }
                }
            }

            return assembly;
        }

        public AssemblyDefinition StripMethodBodies(AssemblyDefinition assembly, bool mockAssembly = false)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    foreach (var method in type.Methods.Where(method => method.HasBody))
                    {
                        var body = new MethodBody(method);

                        if (mockAssembly)
                        {
                            body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
                            body.Instructions.Add(Instruction.Create(OpCodes.Throw));
                        }

                        method.Body = body;
                    }
                }
            }

            return assembly;
        }
    }
}