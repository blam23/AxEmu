﻿using AxEmu.GBC;

Emulator gbc = new();

//
// Setup - Modify these
//
// TODO: Load from cmd line
//gbc.LoadROM("D:\\Test\\GBC\\tetris.gb");
//gbc.LoadROM(@"D:\Test\GBC\game-boy-test-roms-v5.1\blargg\cpu_instrs\individual\03-op sp,hl.gb");
gbc.LoadROM(@"D:\Test\GBC\game-boy-test-roms-v5.1\blargg\cpu_instrs\individual\07-jr,jp,call,ret,rst.gb");

var readKey = false;
var printCpu = true;
var gbDoctorLog = true;

//
// Code
//
StreamWriter? logWriter = null;
if (gbDoctorLog)
{
    File.Delete("D:\\Test\\GBC\\log.txt");
    logWriter = new(File.OpenWrite("D:\\Test\\GBC\\log.txt"));
    gbc.debug.SetupGBDoctorMode();
}

ulong i = 0;
while (true)
{
    logWriter?.Write(gbc.debug.CPUStatusGBDoctor() + "\n");

    if (printCpu)
        Console.WriteLine(gbc.debug.CPUStatus());

    if (readKey)
    {
        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Escape)
            break;

        if(key.Key == ConsoleKey.M)
            Console.WriteLine(gbc.debug.SurroundingMemory());
    }

    try
    {
        gbc.Clock();
    }
    catch(Exception ex)
    {
        Console.WriteLine();
        using (_ = new AxEmu.ConsoleColour(ConsoleColor.DarkRed, ConsoleColor.White))
            Console.WriteLine($"\n <ERROR> {ex.Message}\n");

        Console.WriteLine(gbc.debug.SurroundingMemory());
        
        throw;
    }

    i++;
}

logWriter?.Dispose();
