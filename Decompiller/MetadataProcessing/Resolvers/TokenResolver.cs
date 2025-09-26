using Decompiller.MetadataProcessing.Enums;

namespace Decompiller.MetadataProcessing.Resolvers
{
    public class TokenResolver
    {
        public T ResolveToken<T>(byte[] il, ref int pos) where T : struct
        {
            try
            {
                if (typeof(T) == typeof(int))
                {
                    return (T)(object)ResolveInt32Token(il, ref pos);
                }
                else if (typeof(T) == typeof(byte))
                {
                    return (T)(object)ResolveByteToken(il, ref pos);
                }
                else if (typeof(T) == typeof(double))
                {
                    return (T)(object)ResolveDoubleToken(il, ref pos);
                }
                else if (typeof(T) == typeof(ushort))
                {
                    return (T)(object)ResolveUShortToken(il, ref pos);
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    return (T)(object)ResolveSByteToken(il, ref pos);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to resolve token.", ex);
            }

            return default;
        }

        private byte ResolveByteToken(byte[] il, ref int pos)
        {
            var token = il[pos];
            pos += (int)OffsetType.Byte;

            return token;
        }

        private int ResolveInt32Token(byte[] il, ref int pos)
        {
            int token = BitConverter.ToInt32(il, pos);
            pos += (int)OffsetType.Int32;

            return token;
        }

        private double ResolveDoubleToken(byte[] il, ref int pos)
        {
            double token = BitConverter.ToDouble(il, pos);
            pos += (int)OffsetType.Double;

            return token;
        }

        private ushort ResolveUShortToken(byte[] il, ref int pos)
        {
            ushort token = BitConverter.ToUInt16(il, pos);
            pos += (int)OffsetType.Short;

            return token;
        }

        private sbyte ResolveSByteToken(byte[] il, ref int pos)
        {
            sbyte token = (sbyte)il[pos];
            pos += 1;

            return token;
        }
    }
}
