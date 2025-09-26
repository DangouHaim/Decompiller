using Decompiller.MetadataProcessing.Resolvers;
using System.IO;

namespace Decompiller.MetadataProcessing
{
    public class MetadataProcessor
    {
        //private const string FilePath = @"test3\E48.exe";
        //private const string FilePath = @"console\Empty.dll";
        private const string FilePath = @"await\Empty.dll";
        //private const string FilePath = @"class\Empty.dll";
        //private const string FilePath = @"types\Empty.dll";
        //private const string FilePath = @"test3\E48.exe";

        public string LoadAssembly(string filePath = FilePath)
        {
            var result = string.Empty;

            var reader = new AssemblyReader(filePath);
            var methodResolver = new MethodDefinitionResolver(reader);
            var assemblyName = Path.GetFileNameWithoutExtension(filePath);
            var moduleName = Path.GetFileName(filePath);
            result += $".assembly {assemblyName} {{}}\n";
            result += $".module {moduleName}\n";

            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(typeHandle);
                var typeName = reader.GetString(type.Name);
                var ns = reader.GetString(type.Namespace);

                var fullName = string.IsNullOrEmpty(ns) ? typeName : ns + "." + typeName;

                result += $".class public auto ansi beforefieldinit {fullName}\n";
                result += "       extends [System.Runtime]System.Object\n";
                result += "{\n";

                foreach (var methodHandle in type.GetMethods())
                {
                    var methodSignature = methodResolver.ResolveMethodSignature(methodHandle);

                    result += $"    {methodSignature}\n";
                    result += "    {\n";

                    if (methodResolver.IsBodyDefined(methodHandle))
                    {
                        var maxStack = methodResolver.ResolveMaxStack(methodHandle);
                        result += $"        {maxStack}\n";

                        foreach (var line in methodResolver.GetBody(methodHandle))
                        {
                            result += "        " + line + "\n";
                        }
                    }
                    else
                    {
                        result += "        // abstract or extern, no body\n";
                    }

                    result += "    }\n";
                }

                result += "}\n";
            }

            return result;
        }
    }
}
