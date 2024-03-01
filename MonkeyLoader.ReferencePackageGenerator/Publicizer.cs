using Mono.Cecil;
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
        private static readonly Type _compilerGeneratedType = typeof(CompilerGeneratedAttribute);

        public AssemblyResolver Resolver { get; } = new();

        public AssemblyDefinition CreatePublicAssembly(string source, string target)
        {
            var assembly = AssemblyDefinition.ReadAssembly(source,
                new ReaderParameters { AssemblyResolver = Resolver });

            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    if (!type.IsNested)
                    {
                        type.IsPublic = true;
                    }
                    else
                    {
                        type.IsPublic = true;
                        type.IsNestedPublic = true;
                    }

                    foreach (var field in type.Fields)
                    {
                        if (!type.Properties.Any(property => property.Name == field.Name) && !type.Events.Any(e => e.Name == field.Name))
                            field.IsPublic = true;
                    }

                    foreach (var method in type.Methods)
                    {
                        if (/*UseEmptyMethodBodies && */method.HasBody)
                        {
                            var emptyBody = new Mono.Cecil.Cil.MethodBody(method);
                            emptyBody.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldnull));
                            emptyBody.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Throw));
                            method.Body = emptyBody;
                        }

                        method.IsPublic = true;
                    }
                }
            }

            assembly.Write(target);
            return assembly;
        }
    }
}