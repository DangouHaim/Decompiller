using Decompiller.MetadataProcessing;
using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Decompiller.Providers
{
    public class LocalTypeProvider : ISignatureTypeProvider<string, object>
    {
        private readonly AssemblyReader _reader;

        public LocalTypeProvider(AssemblyReader reader) => _reader = reader;

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
            => reader.GetString(reader.GetTypeDefinition(handle).Name);

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            => reader.GetString(reader.GetTypeReference(handle).Name);

        public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            => "object";

        public string GetTypeFromSpecification(object genericContext, BlobReader blobReader, byte rawTypeKind)
            => "object";

        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetPinnedType(string elementType) => elementType;
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[]";
        public string GetFunctionPointerType(MethodSignature<string> signature) => "methodptr";
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType;
        public string GetGenericMethodParameter(object genericContext, int index) => "!!" + index;
        public string GetGenericTypeParameter(object genericContext, int index) => "!" + index;
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    }
}
