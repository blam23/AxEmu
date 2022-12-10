namespace AxEmu.GBC
{
    public static class Byte
    {
        public static byte add(byte a, byte b) => (byte)(a + b);
        public static byte sub(byte a, byte b) => (byte)(a - b);
        public static byte and(byte a, byte b) => (byte)(a & b);
        public static bool bit(byte a, byte b) => (a & b) == b;

        public static byte from(bool b, int shift) => (byte)((b ? 1 : 0) << shift);
        public static byte upper(ushort a) => (byte)(a >> 8);
        public static byte lower(ushort a) => (byte)(a & 0xFF);
        public static ushort combine(byte a, byte b) => (ushort)(a << 8 | b);

        public static byte or(params byte[] bytes)
        {
            if (bytes.Length == 0)
                return 0;

            var ret = bytes[0];

            for(int i = 1; i < bytes.Length; i++) { ret |= bytes[i]; }

            return ret;
        }
    }
}
