using Decompiller.MetadataProcessing;
using Decompiller.MetadataProcessing.Enums;
using Decompiller.MetadataProcessing.Resolvers;
using Decompiller.Providers;
using System.Collections;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

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

    public IEnumerator<string> GetEnumerator()
    {
        var typeResolver = new OperandTypeResolver(_reader);

        var pos = 0;

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
            var offset = pos;
            var code = _il[pos++];

            var opCode = code == (byte)ByteOpCodeType.MultiByteOpCode
                ? multiByteOpCodes[_il[pos++]]
                : singleByteOpCodes[code];

            var operand = typeResolver.Resolve(opCode, _il, ref pos);
            

            yield return $"IL_{offset:X4}: {opCode.Name?.ToLower()} {operand}".TrimEnd() + $"   // {opCode.OperandType} = {(int)opCode.OperandType}";
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
