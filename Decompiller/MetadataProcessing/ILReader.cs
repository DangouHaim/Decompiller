using Decompiller.Extentions;
using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using static Decompiller.MetadataProcessing.MetadataProcessor;

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

public class ILReader : IEnumerable<string>
{
    private readonly byte[] _il;
    private readonly AssemblyReader _reader;
    private readonly List<string> _locals = new List<string>();
    private static readonly OpCode[] singleByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] multiByteOpCodes = new OpCode[0x100];

    static ILReader()
    {
        foreach (var fi in typeof(OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (fi.GetValue(null) is OpCode op)
            {
                ushort value = (ushort)op.Value;
                if (value < 0x100)
                    singleByteOpCodes[value] = op;
                else if ((value & 0xFF00) == 0xFE00)
                    multiByteOpCodes[value & 0xFF] = op;
            }
        }
    }

    public static List<string> DecodeLocals(StandaloneSignatureHandle localSig, AssemblyReader reader)
    {
        var locals = new List<string>();
        if (localSig.IsNil) return locals;

        var sig = reader.Reader.GetStandaloneSignature(localSig);
        var blobReader = reader.Reader.GetBlobReader(sig.Signature);
        var decoder = new SignatureDecoder<string, object>(
            new LocalTypeProvider(reader),
            reader.Reader,
            null
        );

        locals.AddRange(decoder.DecodeLocalSignature(ref blobReader));
        return locals;
    }

    public ILReader(byte[] il, AssemblyReader reader, StandaloneSignatureHandle? localSignature = null)
    {
        _il = il ?? throw new ArgumentNullException(nameof(il));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));

        if (localSignature.HasValue && !localSignature.Value.IsNil)
        {
            var sig = _reader.Reader.GetStandaloneSignature(localSignature.Value);
            var blob = _reader.Reader.GetBlobBytes(sig.Signature);
            _locals.AddRange(DecodeLocals(localSignature.Value, _reader));
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

    public IEnumerator<string> GetEnumerator()
    {
        int pos = 0;

        if (_locals.Count > 0)
        {
            yield return ".locals init (";
            for (int i = 0; i < _locals.Count; i++)
            {
                string comma = (i < _locals.Count - 1) ? "," : "";
                yield return $"    [{i}] {_locals[i]}{comma}";
            }
            yield return ")";
        }

        while (pos < _il.Length)
        {
            int offset = pos;
            OpCode opCode;

            byte code = _il[pos++];
            opCode = code == 0xFE ? multiByteOpCodes[_il[pos++]] : singleByteOpCodes[code];

            string operandStr = "";

            try
            {
                switch (opCode.OperandType)
                {
                    case OperandType.InlineNone: break;

                    case OperandType.InlineString:
                        {
                            int token = BitConverter.ToInt32(_il, pos);
                            pos += 4;
                            try
                            {
                                var handle = MetadataTokens.UserStringHandle(token);
                                operandStr = !handle.IsNil ? $"\"{_reader.GetUserString(handle)}\"" : "\"<external>\"";
                            }
                            catch
                            {
                                operandStr = "\"<external>\"";
                            }
                        }
                        break;

                    case OperandType.InlineMethod:
                        {
                            int token = BitConverter.ToInt32(_il, pos);
                            pos += 4;
                            operandStr = ResolveMemberReference(token, _reader);
                        }
                        break;
                    case OperandType.InlineField:
                        {
                            int token = BitConverter.ToInt32(_il, pos);
                            pos += 4;

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
                                    string parentTypeName = _reader.GetString(parentType.Name);
                                    string parentNamespace = _reader.GetString(parentType.Namespace);
                                    string fullParentName = string.IsNullOrEmpty(parentNamespace) ? parentTypeName : parentNamespace + "." + parentTypeName;

                                    // Get field type
                                    var sigReader = _reader.Reader.GetBlobReader(field.Signature);
                                    var typeProvider = new LocalTypeProvider(_reader);
                                    string fieldType = _reader.DecodeFieldSignature(ref sigReader, typeProvider).SanitizeName();

                                    // Generate IL op code representation
                                    operandStr = $"{fieldType} {fullParentName}::{fieldName}";
                                }
                                else if (handle.Kind == HandleKind.MemberReference)
                                {
                                    // MemberReference тоже можно разобрать аналогично
                                    var mr = _reader.Reader.GetMemberReference((MemberReferenceHandle)handle);
                                    string fieldName = _reader.GetString(mr.Name);
                                    string typeName = "<external>";

                                    if (mr.Parent.Kind == HandleKind.TypeReference)
                                    {
                                        var tr = _reader.Reader.GetTypeReference((TypeReferenceHandle)mr.Parent);
                                        string ns = _reader.GetString(tr.Namespace);
                                        string n = _reader.GetString(tr.Name);
                                        typeName = string.IsNullOrEmpty(ns) ? n : ns + "." + n;
                                    }
                                    operandStr = $"{typeName}::{fieldName}";
                                }
                                else
                                {
                                    operandStr = "<external>";
                                }
                            }
                            catch
                            {
                                operandStr = "<external>";
                            }

                            break;
                        }

                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                        {
                            int token = BitConverter.ToInt32(_il, pos);
                            pos += 4;
                            operandStr = ResolveMemberReference(token, _reader);
                        }
                        break;

                    case OperandType.ShortInlineI:
                        operandStr = _il[pos++].ToString();
                        break;

                    case OperandType.InlineI:
                        operandStr = BitConverter.ToInt32(_il, pos).ToString();
                        pos += 4;
                        break;

                    case OperandType.InlineR:
                        {
                            double val = BitConverter.ToDouble(_il, pos);
                            operandStr = val.ToString(CultureInfo.InvariantCulture);
                            pos += 8;
                            break;
                        }
                    case OperandType.ShortInlineVar:
                    case OperandType.InlineVar:
                        {
                            int index = opCode.OperandType == OperandType.ShortInlineVar
                                ? _il[pos++]
                                : BitConverter.ToUInt16(_il, pos);

                            if (opCode.Name.StartsWith("ldarg") ||
                                opCode.Name.StartsWith("starg") ||
                                opCode.Name.StartsWith("ldarga"))
                            {
                                operandStr = index.ToString(); // Get arguments by index
                            }
                            else
                            {
                                operandStr = index.ToString(); // Get locals by index
                            }

                            if (opCode.OperandType == OperandType.InlineVar)
                                pos += 2;
                            break;
                        }

                    case OperandType.InlineSwitch:
                        {
                            int count = BitConverter.ToInt32(_il, pos);
                            pos += 4;
                            int[] targets = new int[count];
                            for (int i = 0; i < count; i++)
                            {
                                targets[i] = BitConverter.ToInt32(_il, pos);
                                pos += 4;
                            }
                            operandStr = string.Join(",", targets);
                        }
                        break;

                    default:
                        int size = OperandSize(opCode.OperandType);
                        if (size > 0) pos += size;
                        operandStr = "<unknown>";
                        break;
                }
            }
            catch
            {
                operandStr = "<invalid>";
            }

            yield return $"IL_{offset:X4}: {opCode.Name.ToLower()} {operandStr}".TrimEnd();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static string ResolveMemberReference(int token, AssemblyReader reader)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            if (handle.IsNil) return "<external>";

            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                    var method = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    var name = reader.GetString(method.Name);
                    var declaringType = reader.GetTypeDefinition(method.GetDeclaringType());
                    var typeName = reader.GetString(declaringType.Name);
                    return $"instance void {typeName.SanitizeName()}::{name.SanitizeName()}()";

                case HandleKind.MemberReference:
                    var mr = reader.Reader.GetMemberReference((MemberReferenceHandle)handle);
                    string methodName = reader.GetString(mr.Name);
                    typeName = "<external>";
                    if (mr.Parent.Kind == HandleKind.TypeReference)
                    {
                        var tr = reader.Reader.GetTypeReference((TypeReferenceHandle)mr.Parent);
                        string ns = reader.GetString(tr.Namespace);
                        name = reader.GetString(tr.Name);
                        typeName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                    }
                    return $"void [{typeName.SanitizeName()}] {typeName.SanitizeName()}::{methodName.SanitizeName()}(string)";

                case HandleKind.TypeReference:
                    var tRef = reader.Reader.GetTypeReference((TypeReferenceHandle)handle);
                    return reader.GetString(tRef.Name);

                case HandleKind.TypeDefinition:
                    var tDef = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                    return reader.GetString(tDef.Name);

                default:
                    return "<external>";
            }
        }
        catch
        {
            return "<external>";
        }
    }
}
