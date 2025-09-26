using Decompiller.Extentions;
using Decompiller.MetadataProcessing.Enums;
using Decompiller.Providers;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Decompiller.MetadataProcessing.Resolvers
{
    public class ReferenceTypeResolver
    {
        private AssemblyReader _reader;
        private readonly MethodDefinitionResolver _methodResolver;

        public ReferenceTypeResolver(AssemblyReader reader)
        {
            _reader = reader;
            _methodResolver = new MethodDefinitionResolver(reader);
        }
        public string ResolveUserString(int token)
        {
            var operandStr = string.Empty;
            try
            {
                var stringReference = MetadataTokens.UserStringHandle(token);
                operandStr = !stringReference.IsNil ? $"\"{_reader.GetUserString(stringReference)}\"" : Fallback.External;
            }
            catch
            {
                operandStr = Fallback.External;
            }

            return operandStr;
        }

        public string ResolveInlineField(int token)
        {
            var operandStr = string.Empty;

            try
            {
                var handle = MetadataTokens.EntityHandle(token);

                if (handle.Kind == HandleKind.FieldDefinition)
                {
                    var fieldReference = _reader.Reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                    var fieldName = _reader.GetString(fieldReference.Name);

                    // Get parent type
                    var parentTypeHandle = fieldReference.GetDeclaringType();
                    var parentType = _reader.Reader.GetTypeDefinition(parentTypeHandle);
                    var parentTypeName = _reader.GetString(parentType.Name);
                    var parentNamespace = _reader.GetString(parentType.Namespace);
                    var fullParentName = string.IsNullOrEmpty(parentNamespace) ? parentTypeName : parentNamespace + "." + parentTypeName;

                    // Get field type
                    var sigReader = _reader.Reader.GetBlobReader(fieldReference.Signature);
                    var typeProvider = new LocalTypeProvider(_reader);
                    var fieldType = _reader.DecodeFieldSignature(ref sigReader, typeProvider).SanitizeName();

                    operandStr = $"{fieldType} {fullParentName}::{fieldName}";
                }
                else if (handle.Kind == HandleKind.MemberReference)
                {
                    var memberReference = _reader.Reader.GetMemberReference((MemberReferenceHandle)handle);
                    var memberName = _reader.GetString(memberReference.Name);
                    var typeName = Fallback.External;

                    if (memberReference.Parent.Kind == HandleKind.TypeReference)
                    {
                        var typeReference = _reader.Reader.GetTypeReference((TypeReferenceHandle)memberReference.Parent);
                        var TypeNamespace = _reader.GetString(typeReference.Namespace);
                        var name = _reader.GetString(typeReference.Name);
                        typeName = string.IsNullOrEmpty(TypeNamespace) ? name : TypeNamespace + "." + name;
                    }
                    operandStr = $"{typeName}::{memberName}";
                }
                else
                {
                    operandStr = Fallback.External;
                }
            }
            catch
            {
                operandStr = Fallback.External;
            }

            return operandStr;
        }

        public string ResolveMemberReference(int token)
        {
            try
            {
                var handle = MetadataTokens.EntityHandle(token);
                if (handle.IsNil) return Fallback.External;

                switch (handle.Kind)
                {
                    case HandleKind.MethodDefinition:
                        return _methodResolver.ResolveMethodDefinition((MethodDefinitionHandle)handle);

                    case HandleKind.MethodSpecification:
                        return _methodResolver.ResolveMethodSpecification((MethodSpecificationHandle)handle);

                    case HandleKind.MemberReference:
                        {
                            var memberReference = _reader.Reader.GetMemberReference((MemberReferenceHandle)handle);
                            var methodName = _reader.GetString(memberReference.Name);
                            var typeName = Fallback.External;

                            if (memberReference.Parent.Kind == HandleKind.TypeReference)
                            {
                                var typeReference = _reader.Reader.GetTypeReference((TypeReferenceHandle)memberReference.Parent);
                                var typeNamespace = _reader.GetString(typeReference.Namespace);
                                var name = _reader.GetString(typeReference.Name);
                                typeName = string.IsNullOrEmpty(typeNamespace) ? name : typeNamespace + "." + name;
                            }

                            return $"instance void {typeName.SanitizeName()}::{methodName.SanitizeName()}()";
                        }

                    case HandleKind.TypeReference:
                        {
                            var typeReference = _reader.Reader.GetTypeReference((TypeReferenceHandle)handle);

                            return _reader.GetString(typeReference.Name);
                        }

                    case HandleKind.TypeDefinition:
                        {
                            var typeDefinition = _reader.GetTypeDefinition((TypeDefinitionHandle)handle);

                            return _reader.GetString(typeDefinition.Name);
                        }

                    default:
                        return Fallback.External;
                }
            }
            catch(Exception ex)
            {
                return Fallback.External;
            }
        }

        public string ResolveShortInlineBrTarget(sbyte tokenValue, int pos)
        {
            var target = tokenValue + pos;

            return $"IL_{target:X4}";
        }
    }
}
