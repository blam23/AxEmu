namespace AxEmu.GBC;

internal struct BitField
{
    private byte value;

    public static implicit operator byte(BitField b) => b.value;
    public static implicit operator BitField(byte v) => new() { value = v };

    public bool this[int index] => (value & (1 << index)) != 0;
}
