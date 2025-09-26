using Decompiller.MetadataProcessing;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Decompiller.Providers
{
    public class LocalTypeProvider : ISignatureTypeProvider<string, object>
    {
        private readonly AssemblyReader _reader;

        public LocalTypeProvider(AssemblyReader assemblyReader)
        {
            _reader = assemblyReader;
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.SByte => "int8",
            PrimitiveTypeCode.Byte => "uint8",
            PrimitiveTypeCode.Int16 => "int16",
            PrimitiveTypeCode.UInt16 => "uint16",
            PrimitiveTypeCode.Int32 => "int32",
            PrimitiveTypeCode.UInt32 => "uint32",
            PrimitiveTypeCode.Int64 => "int64",
            PrimitiveTypeCode.UInt64 => "uint64",
            PrimitiveTypeCode.Single => "float32",
            PrimitiveTypeCode.Double => "float64",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.Object => "object",
            PrimitiveTypeCode.Void => "void",
            _ => "object"
        };

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var def = reader.GetTypeDefinition(handle);
            var ns = reader.GetString(def.Namespace);
            var name = reader.GetString(def.Name);
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

            var asm = reader.GetAssemblyDefinition();
            var asmName = reader.GetString(asm.Name);

            return $"class [{asmName}]{fullName}";
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var tr = reader.GetTypeReference(handle);
            var ns = reader.GetString(tr.Namespace);
            var name = reader.GetString(tr.Name);
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

            string asmName = "UnknownAssembly";
            if (tr.ResolutionScope.Kind == HandleKind.AssemblyReference)
            {
                var aref = reader.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope);
                asmName = reader.GetString(aref.Name);
            }

            return $"class [{asmName}]{fullName}";
        }

        public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            var ts = reader.GetTypeSpecification(handle);
            var blob = reader.GetBlobReader(ts.Signature);
            var decoder = new SignatureDecoder<string, object>(this, reader, genericContext);
            return decoder.DecodeType(ref blob);
        }

        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetPinnedType(string elementType) => elementType;
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[]";

        public string GetFunctionPointerType(MethodSignature<string> signature) => "methodptr";

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            => $"{genericType}<{string.Join(",", typeArguments)}>";

        public string GetGenericMethodParameter(object genericContext, int index) => "!!" + index;
        public string GetGenericTypeParameter(object genericContext, int index) => "!" + index;
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    }
}
