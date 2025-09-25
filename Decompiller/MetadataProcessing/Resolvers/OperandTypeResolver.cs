using Decompiller.Extentions;
using Decompiller.Providers;
using System.Globalization;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using static Decompiller.MetadataProcessing.MetadataProcessor;

namespace Decompiller.MetadataProcessing.Resolvers
{
    public class OperandTypeResolver
    {
        private const string ExternalFallback = "\"<external>\"";
        private const string InvalidFallback = "\"<invalid>\"";
        private AssemblyReader _reader;
        private TokenResolver _tokenResolver = new TokenResolver();

        public OperandTypeResolver(AssemblyReader reader)
        {
            _reader = reader;
        }

        public string InlineString(byte[] il, ref int pos)
        {
            var operandStr = string.Empty;

            var token = _tokenResolver.ResolveToken<int>(il, ref pos);

            return ResolveUserString(token);
        }

        public string InlineType(byte[] il, ref int pos)
        {
            var token = _tokenResolver.ResolveToken<int>(il, ref pos);

            return ResolveMemberReference(token);
        }

        public string InlineField(byte[] il, ref int pos)
        {
            var token = _tokenResolver.ResolveToken<int>(il, ref pos);

            return ResolveInlineField(token);
        }

        public string ShortInlineI(byte[] il, ref int pos)
        {
            return _tokenResolver.ResolveToken<byte>(il, ref pos).ToString();
        }

        public string InlineI(byte[] il, ref int pos)
        {
            return _tokenResolver.ResolveToken<int>(il, ref pos).ToString();
        }

        public string InlineR(byte[] il, ref int pos)
        {
            return _tokenResolver.ResolveToken<double>(il, ref pos).ToString(CultureInfo.InvariantCulture);
        }

        public string InlineVar(byte[] il, ref int pos)
        {
            return _tokenResolver.ResolveToken<ushort>(il, ref pos).ToString();
        }

        public string ShortInlineVar(byte[] il, ref int pos)
        {
            return _tokenResolver.ResolveToken<byte>(il, ref pos).ToString();
        }

        private string ResolveUserString(int token)
        {
            var operandStr = string.Empty;
            try
            {
                var handle = MetadataTokens.UserStringHandle(token);
                operandStr = !handle.IsNil ? $"\"{_reader.GetUserString(handle)}\"" : ExternalFallback;
            }
            catch
            {
                operandStr = ExternalFallback;
            }

            return operandStr;
        }

        private string ResolveInlineField(int token)
        {
            var operandStr = string.Empty;

            try
            {
                var handle = MetadataTokens.EntityHandle(token);

                if (handle.Kind == HandleKind.FieldDefinition)
                {
                    var field = _reader.Reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                    var fieldName = _reader.GetString(field.Name);

                    // Get parent type
                    var parentTypeHandle = field.GetDeclaringType();
                    var parentType = _reader.Reader.GetTypeDefinition(parentTypeHandle);
                    var parentTypeName = _reader.GetString(parentType.Name);
                    var parentNamespace = _reader.GetString(parentType.Namespace);
                    var fullParentName = string.IsNullOrEmpty(parentNamespace) ? parentTypeName : parentNamespace + "." + parentTypeName;

                    // Get field type
                    var sigReader = _reader.Reader.GetBlobReader(field.Signature);
                    var typeProvider = new LocalTypeProvider(_reader);
                    var fieldType = _reader.DecodeFieldSignature(ref sigReader, typeProvider).SanitizeName();

                    // Generate IL op code representation
                    operandStr = $"{fieldType} {fullParentName}::{fieldName}";
                }
                else if (handle.Kind == HandleKind.MemberReference)
                {
                    // MemberReference тоже можно разобрать аналогично
                    var mr = _reader.Reader.GetMemberReference((MemberReferenceHandle)handle);
                    var fieldName = _reader.GetString(mr.Name);
                    var typeName = ExternalFallback;

                    if (mr.Parent.Kind == HandleKind.TypeReference)
                    {
                        var tr = _reader.Reader.GetTypeReference((TypeReferenceHandle)mr.Parent);
                        var ns = _reader.GetString(tr.Namespace);
                        var n = _reader.GetString(tr.Name);
                        typeName = string.IsNullOrEmpty(ns) ? n : ns + "." + n;
                    }
                    operandStr = $"{typeName}::{fieldName}";
                }
                else
                {
                    operandStr = ExternalFallback;
                }
            }
            catch
            {
                operandStr = ExternalFallback;
            }

            return operandStr;
        }

        private string ResolveMemberReference(int token)
        {
            try
            {
                var handle = MetadataTokens.EntityHandle(token);
                if (handle.IsNil) return ExternalFallback;

                switch (handle.Kind)
                {
                    case HandleKind.MethodDefinition:
                        {
                            var method = _reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                            var name = _reader.GetString(method.Name);
                            var declaringType = _reader.GetTypeDefinition(method.GetDeclaringType());
                            var typeName = _reader.GetString(declaringType.Name);

                            return $"instance void {typeName.SanitizeName()}::{name.SanitizeName()}()";
                        }

                    case HandleKind.MemberReference:
                        {
                            var mr = _reader.Reader.GetMemberReference((MemberReferenceHandle)handle);
                            var methodName = _reader.GetString(mr.Name);
                            var typeName = ExternalFallback;

                            if (mr.Parent.Kind == HandleKind.TypeReference)
                            {
                                var tr = _reader.Reader.GetTypeReference((TypeReferenceHandle)mr.Parent);
                                var ns = _reader.GetString(tr.Namespace);
                                var name = _reader.GetString(tr.Name);
                                typeName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                            }

                            return $"void [{typeName.SanitizeName()}] {typeName.SanitizeName()}::{methodName.SanitizeName()}(string)";
                        }

                    case HandleKind.TypeReference:
                        {
                            var tRef = _reader.Reader.GetTypeReference((TypeReferenceHandle)handle);

                            return _reader.GetString(tRef.Name);
                        }

                    case HandleKind.TypeDefinition:
                        {
                            var tDef = _reader.GetTypeDefinition((TypeDefinitionHandle)handle);

                            return _reader.GetString(tDef.Name);
                        }

                    default:
                        return ExternalFallback;
                }
            }
            catch
            {
                return ExternalFallback;
            }
        }
    }
}
