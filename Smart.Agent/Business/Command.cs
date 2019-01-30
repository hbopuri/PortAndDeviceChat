using System.Linq;
using Smart.Agent.Helper;

namespace Smart.Agent.Business
{
    public class Command
    {
        private byte BoardId { get; set; }

        public Command(byte id)
        {
            BoardId = id;
        }
        
        public byte[] If()
        {
            var writeBuffer = new byte[4];
            writeBuffer[0] = BoardId;
            writeBuffer[1] = 0x03;
            writeBuffer[2] = 0x03;
            writeBuffer[3] = writeBuffer.CheckSum();
            return writeBuffer;
        }

        public  byte[] PowerOn()
        {
            var writeBuffer = new byte[5];
            writeBuffer[0] = BoardId;
            writeBuffer[1] = 0x04;
            writeBuffer[2] = 0x02;
            writeBuffer[3] = 0x01;
            writeBuffer[4] = writeBuffer.CheckSum();
            return writeBuffer;
        }

        public  byte[] Connect()
        {
            var writeBuffer = new byte[13];
            writeBuffer[0] = BoardId;
            writeBuffer[1] = 0x0C;
            writeBuffer[2] = 0x05;
            writeBuffer[3] = 0x04;
            writeBuffer[4] = 0x04;
            writeBuffer[5] = 0x06;
            writeBuffer[6] = 0xAB;
            writeBuffer[7] = 0xCD;
            writeBuffer[8] = 0x00;
            writeBuffer[9] = 0x00;
            writeBuffer[10] = 0x00;
            writeBuffer[11] = 0x00;
            writeBuffer[12] = writeBuffer.CheckSum();
            return writeBuffer;
        }

        public byte[] ReadDataPortConfig()
        {
            var writeBuffer = new byte[13];
            writeBuffer[0] = BoardId;
            writeBuffer[1] = 0x0C;
            writeBuffer[2] = 0x08;
            writeBuffer[3] = 0x02;
            writeBuffer[4] = 0xAB;
            writeBuffer[5] = 0xCD;
            writeBuffer[6] = 0x04;
            writeBuffer[7] = 0x00;
            writeBuffer[8] = 0x00;
            writeBuffer[9] = 0x0F;
            writeBuffer[10] = 0x00;
            writeBuffer[11] = 0x00;
            writeBuffer[12] = writeBuffer.CheckSum();
            return writeBuffer;
        }

        public byte[] PowerOff()
        {
            var writeBuffer = new byte[5];
            writeBuffer[0] = BoardId;
            writeBuffer[1] = 0x04;
            writeBuffer[2] = 0x02;
            writeBuffer[3] = 0x02;
            writeBuffer[4] = writeBuffer.CheckSum();
            return writeBuffer;
        }

        public byte[] Collect()
        {
            var writeBuffer = new byte[13];
            writeBuffer[0] = BoardId;
            writeBuffer[1] = 0x0C;
            writeBuffer[2] = 0x0A;
            writeBuffer[3] = 0x02;
            writeBuffer[4] = 0xAB;
            writeBuffer[5] = 0xCD;
            writeBuffer[6] = 0x04;
            writeBuffer[7] = 0x00;
            writeBuffer[8] = 0x00;
            writeBuffer[9] = 0x0A;
            writeBuffer[10] = 0x00;
            writeBuffer[11] = 0x00;
            writeBuffer[12] = writeBuffer.CheckSum();
            return writeBuffer;
        }

        public byte[] WriteDataPortConfig()
        {
            return null;
        }
    }
}