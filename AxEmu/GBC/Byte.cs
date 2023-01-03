using System;

namespace AxEmu.GBC;

public static class Byte
{
    public static byte add(byte a, byte b) => (byte)(a + b);
    public static byte sub(byte a, byte b) => (byte)(a - b);
    public static byte and(byte a, byte b) => (byte)(a & b);
    public static bool bit(byte a, byte b) => (a & b) == b;
    public static bool get(byte value, int index) => (value & (1 << index)) != 0;

    public static byte from(bool b, int shift) => (byte)((b ? 1 : 0) << shift);
    public static byte upper(ushort a) => (byte)(a >> 8);
    public static byte lower(ushort a) => (byte)(a & 0xFF);
    public static ushort combine(byte msb, byte lsb) => (ushort)(msb << 8 | lsb);
    public static ushort combineR(byte lsb, byte msb) => (ushort)(msb << 8 | lsb);

    public static byte swap(byte a) => (byte)(((a >> 4) & 0x0F) | (a << 4));

    public static byte or(params byte[] bytes)
    {
        if (bytes.Length == 0)
            return 0;

        var ret = bytes[0];

        for(int i = 1; i < bytes.Length; i++) { ret |= bytes[i]; }

        return ret;
    }

    public static (byte, bool) ror(byte a, bool carryIn)
    {
        var carryOut = (a & 0x1) == 0x1;
        var res = (byte)(a >> 1);

        if (carryIn)
            res |= 0x80;

        return (res, carryOut);
    }

    public static (byte, bool) rol(byte a, bool carryIn)
    {
        var carryOut = (a & 0x80) == 0x80;
        var res = (byte)(a << 1);

        if (carryIn)
            res |= 0x01;

        return (res, carryOut);
    }

}
