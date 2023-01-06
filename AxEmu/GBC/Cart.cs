using AxEmu.GBC.MBC;
using System.Reflection;
using System.Text;

namespace AxEmu.GBC;

internal class Cart
{
    static Cart()
    {
        LoadMBCs();
    }

    private static Dictionary<byte, Type> mbcs = new();
    public static void LoadMBCs()
    {
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            var attrs = type.GetCustomAttributes(typeof(MBCAttribute), true);
            foreach (var a in attrs)
            {
                if (a is MBCAttribute mbc)
                {
                    var num = mbc.CartType;

                    if (mbcs.ContainsKey(num))
                        throw new InvalidDataException($"Duplicate MBC registered for number: 0x{num:X2}");

                    mbcs.Add(num, type);
                }
            }
        }
    }

    public enum State
    {
        Unloaded,
        Loaded,
        FailedToOpen,
        Invalid,
    }

    internal readonly byte[] rom = Array.Empty<byte>();
    internal byte[] ram;
    private readonly State state = State.Unloaded;
    public State LoadState => state;

    public bool CGB = false;
    public bool SGBFlag  = false;
    public byte CartType = 0;
    public byte ROMSize  = 0;
    public byte RAMSize  = 0;

    public string saveFile = "";

    public IMBC CreateMBC()
    {
        if (mbcs.TryGetValue(CartType, out var mbcType))
        {
            if (Activator.CreateInstance(mbcType) is IMBC mbc)
            {
                mbc.CartType = CartType;
                return mbc;
            }

            throw new Exception($"MBC '0x{CartType:X2}' is invalid!");
        }

        throw new Exception($"Cart type: '0x{CartType:X2}' not loaded (likely not supported).");
    }

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

        ram = new byte[GetRAMinKB() * 0x400];

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
            saveFile = FileLocation + ".axsav";
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

    //
    // Saving
    //
    internal void LoadRAM()
    {
        if (!File.Exists(saveFile))
            return;

        using var fs = File.OpenRead(saveFile);
        fs.Read(ram, 0, (int)fs.Length);
    }

    internal void SaveRAM()
    {
        using var fs = File.Create(saveFile);
        fs.Write(ram);
        fs.Flush();
        fs.Close();
    }
}
