using AxEmuTest.NESTest.Mocks;

namespace AxEmuTest.NESTest
{
    [TestClass]
    public class CPUTest
    {
        MockMemory mem;
        AxEmu.NES.System system;

        [TestInitialize]
        public void Init()
        {
            mem = new();
            mem.TestSetResponse(AxEmu.NES.CPU.RESET_VECTOR, 0x0);
            mem.TestSetResponse(AxEmu.NES.CPU.RESET_VECTOR+1, 0x0);

            system = new AxEmu.NES.System(mem);
            system.cpu.Init();
        }

        [TestMethod]
        public void IterateTest()
        {
            mem.TestSetResponse(0x0, 0xA9); // LDA Imm - BA
            mem.TestSetResponse(0x1, 0xBA);
            mem.TestSetResponse(0x2, 0x85); // STA ZP  - F0
            mem.TestSetResponse(0x3, 0xF0);

            system.cpu.Iterate(); // LDA

            Assert.AreEqual(0xBA, system.cpu.a);
            Assert.AreEqual(0x2, system.cpu.pc);
            Assert.AreEqual(0x2ul, system.cpu.clock);
            Assert.IsTrue(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);

            system.cpu.Iterate(); // STA

            Assert.AreEqual(0xBA, mem.Written[0xF0]);
            Assert.AreEqual(0x4, system.cpu.pc);
            Assert.AreEqual(0x5ul, system.cpu.clock);
            Assert.IsTrue(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);
        }

        [TestMethod]
        public void Load_Immediate()
        {
            mem.TestSetResponse(0x1, 0xEE);

            var res = system.cpu.Load(AxEmu.NES.CPU.Mode.IMM);
            Assert.AreEqual(0xEE, res);
            Assert.AreEqual(0x2, system.cpu.pc);
            Assert.AreEqual(0x2ul, system.cpu.clock);
            Assert.IsTrue(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);
        }

        [TestMethod]
        public void Load_ZP()
        {
            mem.TestSetResponse(0x1, 0x10);
            mem.TestSetResponse(0x10, 0xAF);

            var res = system.cpu.Load(AxEmu.NES.CPU.Mode.ZP);
            Assert.AreEqual(0xAF, res);
            Assert.AreEqual(0x2, system.cpu.pc);
            Assert.AreEqual(0x3ul, system.cpu.clock);
            Assert.IsTrue(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);
        }

        [TestMethod]
        public void Load_ZPX()
        {
            mem.TestSetResponse(0x1, 0x10);
            mem.TestSetResponse(0x20, 0x01);
            system.cpu.x = 0x10;

            var res = system.cpu.Load(AxEmu.NES.CPU.Mode.ZPX);
            Assert.AreEqual(0x01, res);
            Assert.AreEqual(0x2, system.cpu.pc);
            Assert.AreEqual(0x4ul, system.cpu.clock);
            Assert.IsFalse(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);
        }

        [TestMethod]
        public void Load_ZPY()
        {
            mem.TestSetResponse(0x1, 0x0A);
            mem.TestSetResponse(0xFA, 0xFF);
            system.cpu.y = 0xF0;

            var res = system.cpu.Load(AxEmu.NES.CPU.Mode.ZPY);
            Assert.AreEqual(0xFF, res);
            Assert.AreEqual(0x2, system.cpu.pc);
            Assert.AreEqual(0x4ul, system.cpu.clock);
            Assert.IsTrue(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);
        }

        [TestMethod]
        public void Load_ABS()
        {
            mem.TestSetResponse(0x1, 0xFA);
            mem.TestSetResponse(0x2, 0xFA);
            mem.TestSetResponse(0xFAFA, 0x02);

            var res = system.cpu.Load(AxEmu.NES.CPU.Mode.ABS);
            Assert.AreEqual(0x02, res);
            Assert.AreEqual(0x3, system.cpu.pc);
            Assert.AreEqual(0x4ul, system.cpu.clock);
            Assert.IsFalse(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);
        }

        [TestMethod]
        public void Load_ABSX()
        {
            mem.TestSetResponse(0x1, 0xFA);
            mem.TestSetResponse(0x2, 0xFA);
            mem.TestSetResponse(0xFB0A, 0xAA);
            system.cpu.x = 0x10;

            var res = system.cpu.Load(AxEmu.NES.CPU.Mode.ABSX);
            Assert.AreEqual(0xAA, res);
            Assert.AreEqual(0x3, system.cpu.pc);
            Assert.AreEqual(0x5ul, system.cpu.clock);
            Assert.IsTrue(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);
        }

        [TestMethod]
        public void Load_ABSY()
        {
            mem.TestSetResponse(0x1, 0x00);
            mem.TestSetResponse(0x2, 0x05);
            mem.TestSetResponse(0x06, 0x32);
            system.cpu.y = 0x1;

            var res = system.cpu.Load(AxEmu.NES.CPU.Mode.ABSY);
            Assert.AreEqual(0x32, res);
            Assert.AreEqual(0x3, system.cpu.pc);
            Assert.AreEqual(0x4ul, system.cpu.clock);
            Assert.IsFalse(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);
        }

        [TestMethod]
        public void Load_INDX()
        {
            mem.TestSetResponse(0x1, 0x10);
            mem.TestSetResponse(0x20, 0xAA);
            mem.TestSetResponse(0x21, 0xBB);
            mem.TestSetResponse(0xAABB, 0x12);
            system.cpu.x = 0x10;

            var res = system.cpu.Load(AxEmu.NES.CPU.Mode.INDX);
            Assert.AreEqual(0x12, res);
            Assert.AreEqual(0x2, system.cpu.pc);
            Assert.AreEqual(0x6ul, system.cpu.clock);
            Assert.IsFalse(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);
        }

        [TestMethod]
        public void Load_INDY()
        {
            mem.TestSetResponse(0x1, 0x54);
            mem.TestSetResponse(0x54, 0xBB);
            mem.TestSetResponse(0x55, 0xAA);
            mem.TestSetResponse(0xBBCA, 0xFF);
            system.cpu.y = 0x20;

            var res = system.cpu.Load(AxEmu.NES.CPU.Mode.INDY);
            Assert.AreEqual(0xFF, res);
            Assert.AreEqual(0x2, system.cpu.pc);
            Assert.AreEqual(0x6ul, system.cpu.clock);
            Assert.IsTrue(system.cpu.status.Negative);
            Assert.IsFalse(system.cpu.status.Zero);
        }
    }
}