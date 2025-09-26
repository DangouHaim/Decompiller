using Decompiller.MetadataProcessing.Enums;
using System.Globalization;
using System.Reflection.Emit;

namespace Decompiller.MetadataProcessing.Resolvers
{
    public class OperandTypeResolver
    {
        private TokenResolver _tokenResolver = new TokenResolver();
        private ReferenceTypeResolver _referenceTypeResolver;

        public OperandTypeResolver(AssemblyReader reader)
        {
            _referenceTypeResolver = new ReferenceTypeResolver(reader);
        }

        public string InlineString(byte[] il, ref int pos)
        {
            var operandStr = string.Empty;

            var tokenValue = _tokenResolver.ResolveToken<int>(il, ref pos);

            return _referenceTypeResolver.ResolveUserString(tokenValue);
        }

        public string InlineType(byte[] il, ref int pos)
        {
            var tokenValue = _tokenResolver.ResolveToken<int>(il, ref pos);

            return _referenceTypeResolver.ResolveMemberReference(tokenValue);
        }

        public string InlineField(byte[] il, ref int pos)
        {
            var tokenValue = _tokenResolver.ResolveToken<int>(il, ref pos);

            return _referenceTypeResolver.ResolveInlineField(tokenValue);
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

        public string ShortInlineBrTarget(byte[] il, ref int pos)
        {
            var tokenValue = _tokenResolver.ResolveToken<sbyte>(il, ref pos);

            return _referenceTypeResolver.ResolveShortInlineBrTarget(tokenValue, pos);
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

                    case OperandType.ShortInlineBrTarget:
                        operand = ShortInlineBrTarget(_il, ref pos);
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
