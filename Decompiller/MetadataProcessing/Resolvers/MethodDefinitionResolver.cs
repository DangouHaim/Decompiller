using Decompiller.Extentions;
using Decompiller.MetadataProcessing.Enums;
using Decompiller.Providers;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

namespace Decompiller.MetadataProcessing.Resolvers
{
    public class MethodDefinitionResolver
    {
        private readonly AssemblyReader _reader;

        public MethodDefinitionResolver(AssemblyReader reader)
        {
            _reader = reader;
        }

        public string ResolveMethodSignature(MethodDefinitionHandle methodDefinitionHandle)
        {
            var methodDefinition = _reader.GetMethodDefinition(methodDefinitionHandle);
            var methodName = _reader.GetString(methodDefinition.Name);

            // Access modifiers
            string access = methodDefinition.Attributes switch
            {
                var a when (a & MethodAttributes.Public) != 0 => "public",
                var a when (a & MethodAttributes.Private) != 0 => "private",
                var a when (a & MethodAttributes.Family) != 0 => "family",
                var a when (a & MethodAttributes.Assembly) != 0 => "assembly",
                var a when (a & MethodAttributes.FamORAssem) != 0 => "famorassem",
                var a when (a & MethodAttributes.FamANDAssem) != 0 => "famandassem",
                _ => "private"
            };

            // Other modifiers
            var modifiers = new List<string>();
            if ((methodDefinition.Attributes & MethodAttributes.Static) != 0) modifiers.Add("static");
            if ((methodDefinition.Attributes & MethodAttributes.Virtual) != 0) modifiers.Add("virtual");
            if ((methodDefinition.Attributes & MethodAttributes.Abstract) != 0) modifiers.Add("abstract");
            if ((methodDefinition.Attributes & MethodAttributes.Final) != 0) modifiers.Add("final");
            if ((methodDefinition.Attributes & MethodAttributes.HideBySig) != 0) modifiers.Add("hidebysig");

            string staticOrInstance = modifiers.Contains("static") ? "static" : "instance";

            // Signature
            var signature = methodDefinition.DecodeSignature(new LocalTypeProvider(_reader), null);

            string returnType = (methodName == ".ctor" || methodName == ".cctor") ? "void" : signature.ReturnType;

            // Parameters
            StringBuilder paramList = new();
            for (int i = 0; i < signature.ParameterTypes.Length; i++)
            {
                if (i > 0) paramList.Append(", ");
                paramList.Append(signature.ParameterTypes[i]);
            }

            string allModifiers = access + " " + string.Join(" ", modifiers);
            return $".method {allModifiers} {returnType} {methodName}({paramList}) cil managed";
        }

        public string ResolveMethodDefinition(MethodDefinitionHandle handle)
        {
            var methodDef = _reader.GetMethodDefinition(handle);
            var methodName = _reader.GetString(methodDef.Name);

            var declaringTypeDef = _reader.GetTypeDefinition(methodDef.GetDeclaringType());
            var typeName = _reader.GetString(declaringTypeDef.Name);
            var typeNamespace = _reader.GetString(declaringTypeDef.Namespace);
            string fullTypeName = string.IsNullOrEmpty(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}";

            var assemblyName = _reader.GetString(_reader.Reader.GetAssemblyDefinition().Name);

            bool isStatic = (methodDef.Attributes & MethodAttributes.Static) != 0;
            bool isConstructor = methodName == ".ctor" || methodName == ".cctor";
            var staticOrInstance = isStatic ? "static" : "instance";

            var signature = methodDef.DecodeSignature(new LocalTypeProvider(_reader), null);

            string returnType = isConstructor ? "void" : signature.ReturnType;

            StringBuilder paramList = new();
            for (int i = 0; i < signature.ParameterTypes.Length; i++)
            {
                if (i > 0) paramList.Append(", ");
                paramList.Append(signature.ParameterTypes[i]);
            }

            return $"{staticOrInstance} {returnType} [{assemblyName}]{fullTypeName}::{methodName}({paramList})";
        }

        public string ResolveMemberReference(MemberReferenceHandle handle)
        {
            var memberReference = _reader.Reader.GetMemberReference(handle);
            var methodName = _reader.GetString(memberReference.Name);

            string typeName = Fallback.External;

            var assemblyDef = _reader.Reader.GetAssemblyDefinition();
            string assemblyName = _reader.GetString(assemblyDef.Name);

            switch (memberReference.Parent.Kind)
            {
                case HandleKind.TypeReference:
                    {
                        var typeRef = _reader.Reader.GetTypeReference((TypeReferenceHandle)memberReference.Parent);
                        var ns = _reader.GetString(typeRef.Namespace);
                        var name = _reader.GetString(typeRef.Name);
                        typeName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;

                        var resolutionScope = typeRef.ResolutionScope;
                        if (resolutionScope.Kind == HandleKind.AssemblyReference)
                        {
                            var asmRef = _reader.Reader.GetAssemblyReference((AssemblyReferenceHandle)resolutionScope);
                            assemblyName = _reader.GetString(asmRef.Name);
                        }
                        break;
                    }

                case HandleKind.TypeDefinition:
                    {
                        var typeDef = _reader.GetTypeDefinition((TypeDefinitionHandle)memberReference.Parent);
                        var ns = _reader.GetString(typeDef.Namespace);
                        var name = _reader.GetString(typeDef.Name);
                        typeName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                        break;
                    }
            }

            return $"instance void class [{assemblyName}]{typeName.SanitizeName()}::{methodName.SanitizeName()}()";
        }

        public string ResolveMethodSpecification(MethodSpecificationHandle handle)
        {
            return Fallback.Unsupported;
        }

        public bool IsBodyDefined(MethodDefinitionHandle methodDefinitionHandle)
        {
            var methodDefinition = _reader.GetMethodDefinition(methodDefinitionHandle);

            return methodDefinition.RelativeVirtualAddress != 0;
        }

        public string ResolveMaxStack(MethodDefinitionHandle methodDefinitionHandle)
        {
            var methodDefinition = _reader.GetMethodDefinition(methodDefinitionHandle);
            var body = _reader.PEReader.GetMethodBody(methodDefinition.RelativeVirtualAddress);

            return $".maxstack {body.MaxStack}";
        }

        public ILReader GetBody(MethodDefinitionHandle methodDefinitionHandle)
        {
            var methodDefinition = _reader.GetMethodDefinition(methodDefinitionHandle);

            var body = _reader.PEReader.GetMethodBody(methodDefinition.RelativeVirtualAddress);

            var ilBytes = body.GetILBytes();
            var ilReader = new ILReader(ilBytes, _reader, body.LocalSignature);

            return ilReader;
        }
    }
}
