
using AxEmu.NES;

//AxEmu.NES.System testNes = new("D:\\Test\\NES\\nestest.nes");
AxEmu.NES.System testNes = new("D:\\Test\\NES\\nes-test-roms-master\\instr_misc\\rom_singles\\03-dummy_reads.nes");
//AxEmu.NES.System testNes = new("D:\\Test\\NES\\nes-test-roms-master\\other\\GENIE.nes");
testNes.Run(true, false);
