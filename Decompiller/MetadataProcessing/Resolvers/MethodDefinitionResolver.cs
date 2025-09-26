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
