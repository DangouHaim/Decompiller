using Decompiller.MetadataProcessing.Resolvers;
using Decompiller.Providers;
using System.Collections;
using System.Globalization;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using static Decompiller.MetadataProcessing.MetadataProcessor;

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
        var typeResolver = new OperandTypeResolver(_reader);

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
                        operandStr = typeResolver.InlineString(_il, ref pos);
                        break;

                    case OperandType.InlineField:
                        {
                            operandStr = typeResolver.InlineField(_il, ref pos);
                            break;
                        }

                    case OperandType.InlineMethod:
                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                        operandStr = typeResolver.InlineType(_il, ref pos);
                        break;

                    case OperandType.ShortInlineI:
                        operandStr = typeResolver.ShortInlineI(_il, ref pos);
                        break;

                    case OperandType.InlineI:
                        operandStr = typeResolver.InlineI(_il, ref pos);
                        break;

                    case OperandType.InlineR:
                            operandStr = typeResolver.InlineR(_il, ref pos);
                            break;

                    case OperandType.ShortInlineVar:
                        operandStr = typeResolver.ShortInlineVar(_il, ref pos);
                        break;

                    case OperandType.InlineVar:
                        operandStr = typeResolver.InlineVar(_il, ref pos);
                        break;

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
            catch (Exception ex)
            {
                operandStr = "<invalid>";
            }

            yield return $"IL_{offset:X4}: {opCode.Name.ToLower()} {operandStr}".TrimEnd();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
