using Decompiller.Extentions;
using Decompiller.MetadataProcessing.Enums;
using Decompiller.Providers;
using System.Globalization;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Decompiller.MetadataProcessing.Resolvers
{
    public class OperandTypeResolver
    {
        private AssemblyReader _reader;
        private TokenResolver _tokenResolver = new TokenResolver();

        public OperandTypeResolver(AssemblyReader reader)
        {
            _reader = reader;
        }

        public string InlineString(byte[] il, ref int pos)
        {
            var operandStr = string.Empty;

            var tokenValue = _tokenResolver.ResolveToken<int>(il, ref pos);

            return ResolveUserString(tokenValue);
        }

        public string InlineType(byte[] il, ref int pos)
        {
            var tokenValue = _tokenResolver.ResolveToken<int>(il, ref pos);

            return ResolveMemberReference(tokenValue);
        }

        public string InlineField(byte[] il, ref int pos)
        {
            var tokenValue = _tokenResolver.ResolveToken<int>(il, ref pos);

            return ResolveInlineField(tokenValue);
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

        public string InlineSwitch(byte[] il, ref int pos)
        {
            var tokensCount = _tokenResolver.ResolveToken<int>(il, ref pos);
            var tokenValues = new int[tokensCount];

            for (int i = 0; i < tokensCount; i++)
            {
                tokenValues[i] = _tokenResolver.ResolveToken<int>(il, ref pos);
            }

            return string.Join(",", tokenValues);
        }

        public string Resolve(OpCode opCode, byte[] _il, ref int pos)
        {
            var operand = string.Empty;
            try
            {
                switch (opCode.OperandType)
                {
                    case OperandType.InlineNone: break;

                    case OperandType.InlineString:
                        operand = InlineString(_il, ref pos);
                        break;

                    case OperandType.InlineField:
                            operand = InlineField(_il, ref pos);
                            break;

                    case OperandType.InlineMethod:
                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                        operand = InlineType(_il, ref pos);
                        break;

                    case OperandType.ShortInlineI:
                        operand = ShortInlineI(_il, ref pos);
                        break;

                    case OperandType.InlineI:
                        operand = InlineI(_il, ref pos);
                        break;

                    case OperandType.InlineR:
                        operand = InlineR(_il, ref pos);
                        break;

                    case OperandType.ShortInlineVar:
                        operand = ShortInlineVar(_il, ref pos);
                        break;

                    case OperandType.InlineVar:
                        operand = InlineVar(_il, ref pos);
                        break;

                    case OperandType.InlineSwitch:
                            operand = InlineSwitch(_il, ref pos);
                        break;

                    default:
                        int size = OperandSize(opCode.OperandType);
                        if (size > 0) pos += size;
                        operand = Fallback.Unknown;
                        break;
                }
            }
            catch (Exception ex)
            {
                operand = Fallback.Invalid;
            }

            return operand;
        }

        private string ResolveUserString(int token)
        {
            var operandStr = string.Empty;
            try
            {
                var handle = MetadataTokens.UserStringHandle(token);
                operandStr = !handle.IsNil ? $"\"{_reader.GetUserString(handle)}\"" : Fallback.External;
            }
            catch
            {
                operandStr = Fallback.External;
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

                    operandStr = $"{fieldType} {fullParentName}::{fieldName}";
                }
                else if (handle.Kind == HandleKind.MemberReference)
                {
                    var mr = _reader.Reader.GetMemberReference((MemberReferenceHandle)handle);
                    var fieldName = _reader.GetString(mr.Name);
                    var typeName = Fallback.External;

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
                    operandStr = Fallback.External;
                }
            }
            catch
            {
                operandStr = Fallback.External;
            }

            return operandStr;
        }

        private string ResolveMemberReference(int token)
        {
            try
            {
                var handle = MetadataTokens.EntityHandle(token);
                if (handle.IsNil) return Fallback.External;

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
                            var typeName = Fallback.External;

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
                        return Fallback.External;
                }
            }
            catch
            {
                return Fallback.External;
            }
        }

        private static int OperandSize(OperandType type) => type switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget => 1,
            OperandType.ShortInlineI => 1,
            OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget => 4,
            OperandType.InlineI => 4,
            OperandType.InlineI8 => 8,
            OperandType.InlineR => 8,
            OperandType.ShortInlineR => 4,
            OperandType.InlineString => 4,
            OperandType.InlineField => 4,
            OperandType.InlineMethod => 4,
            OperandType.InlineTok => 4,
            OperandType.InlineType => 4,
            OperandType.InlineSig => 4,
            OperandType.InlineSwitch => -1,
            _ => 0
        };
    }
}
