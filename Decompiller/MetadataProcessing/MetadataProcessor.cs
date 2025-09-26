using Decompiller.MetadataProcessing.Resolvers;
using System.IO;
using System.Text;

namespace Decompiller.MetadataProcessing
{
    public class MetadataProcessor
    {
        //private const string FilePath = @"locals\E48.exe";
        //private const string FilePath = @"console\Empty.dll";
        //private const string FilePath = @"class\Empty.dll";
        //private const string FilePath = @"task\Empty.dll";
        //private const string FilePath = @"types\Empty.dll";
        //private const string FilePath = @"action\Empty.dll";
        private const string FilePath = @"await\Empty.dll";
        
        //private const string FilePath = @"patterns\Patterns.dll";

        public string LoadAssembly(string filePath = FilePath)
        {
            var result = new StringBuilder();

            var reader = new AssemblyReader(filePath);
            var methodResolver = new MethodDefinitionResolver(reader);

            var assemblyName = Path.GetFileNameWithoutExtension(filePath);
            var moduleName = Path.GetFileName(filePath);

            // 🔹 Добавляем ссылки на внешние сборки
            foreach (var referenceHandle in reader.Reader.AssemblyReferences)
            {
                var reference = reader.Reader.GetAssemblyReference(referenceHandle);
                var referenceName = reader.GetString(reference.Name);
                result.AppendLine($".assembly extern {referenceName} {{}}");
            }

            result.AppendLine($".assembly {assemblyName} {{}}");
            result.AppendLine($".module {moduleName}");

            // 🔹 Типы
            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(typeHandle);
                var typeName = reader.GetString(type.Name);
                var ns = reader.GetString(type.Namespace);

                var fullName = string.IsNullOrEmpty(ns) ? typeName : ns + "." + typeName;

                result.AppendLine($".class public auto ansi beforefieldinit {fullName}");
                result.AppendLine("       extends [System.Runtime]System.Object");
                result.AppendLine("{");

                foreach (var methodHandle in type.GetMethods())
                {
                    var methodSignature = methodResolver.ResolveMethodSignature(methodHandle);

                    result.AppendLine($"    {methodSignature}");
                    result.AppendLine("    {");

                    if (methodResolver.IsBodyDefined(methodHandle))
                    {
                        var maxStack = methodResolver.ResolveMaxStack(methodHandle);
                        result.AppendLine($"        {maxStack}");

                        foreach (var line in methodResolver.GetBody(methodHandle))
                        {
                            result.AppendLine("        " + line);
                        }
                    }
                    else
                    {
                        result.AppendLine("        // abstract or extern, no body");
                    }

                    result.AppendLine("    }");
                }

                result.AppendLine("}");
            }

            return result.ToString();
        }

    }
}
