using Decompiller.Extentions;
using Decompiller.Providers;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Decompiller.MetadataProcessing
{
    public class MetadataProcessor
    {
        //private const string FilePath = @"test3\E48.exe";
        //private const string FilePath = @"test\Empty.dll";
        //private const string FilePath = @"await\Empty.dll";
        private const string FilePath = @"types\Empty.dll";
        //private const string FilePath = @"test3\E48.exe";

        public class AssemblyReader
        {
            private readonly MetadataReader _reader;
            private readonly PEReader _peReader;

            public TypeDefinitionHandleCollection TypeDefinitions => _reader.TypeDefinitions;
            public PEReader PEReader => _peReader;
            public MetadataReader Reader => _reader;

            public AssemblyReader(string filePath)
            {
                var stream = File.OpenRead(filePath);
                var peReader = new PEReader(stream);
                var reader = peReader.GetMetadataReader();
                _reader = reader;
                _peReader = peReader;
            }

            public TypeDefinition GetTypeDefinition(TypeDefinitionHandle handle)
            {
                return _reader.GetTypeDefinition(handle);
            }

            public MethodDefinition GetMethodDefinition(MethodDefinitionHandle handle)
            {
                return _reader.GetMethodDefinition(handle);
            }

            public string GetString(StringHandle handle)
            {
                return _reader.GetString(handle).SanitizeName();
            }

            public string GetUserString(UserStringHandle handle)
            {
                return _reader.GetUserString(handle);
            }

            public string DecodeFieldSignature(ref BlobReader sigReader, LocalTypeProvider typeProvider)
            {
                return new SignatureDecoder<string, object>(typeProvider, _reader, null).DecodeFieldSignature(ref sigReader).SanitizeName();
            }
        }

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

                if (typeName == "<Module>") continue;

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
