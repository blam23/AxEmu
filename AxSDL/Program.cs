using AxEmu.NES;

// Load ROM
var testNes = new AxEmu.NES.System("D:\\Test\\NES\\nestest.nes");

// Initialise Display
var display = new Display(testNes);

// Run Emulator
testNes.Run();

// After Emulator stops - close window.
display.Close();