using Decompiller.Extentions;
using Decompiller.Providers;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;

namespace Decompiller.MetadataProcessing
{
    public class MetadataProcessor
    {
        //private const string FilePath = @"test3\E48.exe";
        //private const string FilePath = @"console\Empty.dll";
        //private const string FilePath = @"await\Empty.dll";
        private const string FilePath = @"types\Empty.dll";
        //private const string FilePath = @"test3\E48.exe";

        public string LoadAssembly(string filePath = FilePath)
        {
            string result = string.Empty;

            var reader = new AssemblyReader(filePath);
            string assemblyName = Path.GetFileNameWithoutExtension(filePath);
            string moduleName = Path.GetFileName(filePath);
            result += $".assembly {assemblyName} {{}}\n";
            result += $".module {moduleName}\n";

            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(typeHandle);
                var typeName = reader.GetString(type.Name);
                var ns = reader.GetString(type.Namespace);

                string fullName = string.IsNullOrEmpty(ns) ? typeName : ns + "." + typeName;

                result += $".class public auto ansi beforefieldinit {fullName}\n";
                result += "       extends [System.Runtime]System.Object\n";
                result += "{\n";

                foreach (var methodHandle in type.GetMethods())
                {
                    var methodDef = reader.GetMethodDefinition(methodHandle);
                    string methodName = reader.GetString(methodDef.Name);

                    bool isStatic = (methodDef.Attributes & MethodAttributes.Static) != 0;
                    bool isConstructor = methodName == ".ctor" || methodName == ".cctor";

                    string staticOrInstance = isConstructor
                        ? (isStatic ? "static" : "instance")
                        : (isStatic ? "static" : "");

                    string returnType = isConstructor ? "void" : "void";

                    result += $"    .method public hidebysig {staticOrInstance} {returnType} {methodName}() cil managed\n";
                    result += "    {\n";

                    if (methodDef.RelativeVirtualAddress != 0)
                    {
                        var body = reader.PEReader.GetMethodBody(methodDef.RelativeVirtualAddress);
                        result += $"        .maxstack {body.MaxStack}\n";

                        var ilBytes = body.GetILBytes();
                        var ilReader = new ILReader(ilBytes, reader, body.LocalSignature);

                        foreach (var line in ilReader)
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
