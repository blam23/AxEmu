using System.Text;

namespace AxEmu.GBC;


internal class Cart
{
    public enum State
    {
        Unloaded,
        Loaded,
        FailedToOpen,
        Invalid,
    }

    internal readonly byte[] rom = Array.Empty<byte>();
    internal readonly byte[] ram = new byte[0x2000];
    private readonly State state = State.Unloaded;
    public State LoadState => state;

    public bool CGB = false;
    public bool SGBFlag  = false;
    public byte CartType = 0;
    public byte ROMSize  = 0;
    public byte RAMSize  = 0;

    private string GetNullStr(ReadOnlySpan<byte> data)
    {
        var end = 0;

        while (end < data.Length)
        {
            if (data[end] == 0)
                break;
            end++;
        }

        return Encoding.ASCII.GetString(data.Slice(0, end).ToArray());
    }

    public int GetRAMinKB()
    {
        return RAMSize switch
        {
            01 =>   2,
            02 =>   8,
            03 =>  32,
            04 => 128,
            05 =>  64,

            _ => 0,
        };
    }

    private bool validate()
    {
        ReadOnlySpan<byte> data = rom;

        var title = GetNullStr(data.Slice(0x0134, 0x0143 - 0x0134));

        if (data[0x0146] == 0x3)
            SGBFlag = true;

        CartType = data[0x0147];
        ROMSize  = data[0x0148];
        RAMSize  = data[0x0149];

        // CGB flag can be either 0x80 or 0xC0, bit 6 is ignored.
        CGB = (data[0x0143] | 0x40) == 0xC0; 

        Console.WriteLine($"Title:    '{title}'");
        Console.WriteLine($"CGB Game: {(CGB ? 'T' : 'F')}");
        Console.WriteLine($"CartType: {CartType:X2}");
        Console.WriteLine($"ROMSize:  {ROMSize:X2} ({(1024*32) << ROMSize:X}KB)");
        Console.WriteLine($"RAMSize:  {RAMSize:X2} ({GetRAMinKB()}KB)");
        Console.WriteLine($"SGBFlag:  {(SGBFlag ? 'T' : 'F')}");

        return true;
    }

    public Cart(string FileLocation) 
    {
        try
        {
            rom = File.ReadAllBytes(FileLocation);
        }
        catch
        {
            state = State.FailedToOpen;
            return;
        }

        if (!validate())
        {
            state = State.Invalid;
            return;
        }

        state = State.Loaded;
    }

    public Cart()
    {
    }
}
