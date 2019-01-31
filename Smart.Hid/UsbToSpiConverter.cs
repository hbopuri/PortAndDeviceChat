using System;
using System.Text;
using mcp2210_dll_m;
namespace Smart.Hid
{
    public class UsbToSpiConverter
    {
        /**** Constants ****/
        public const ushort DefaultVid = 0x4d8;
        public const ushort DefaultPid = 0xde;

        public static int Init()
        {
            int devCount = MCP2210.M_Mcp2210_GetConnectedDevCount(DefaultVid, DefaultPid);
            Console.WriteLine(devCount + " devices found");
            if (devCount > 0)
            {
                StringBuilder path = new StringBuilder();

                var deviceHandle = MCP2210.M_Mcp2210_OpenByIndex(DefaultVid, DefaultPid, 0, path);
                var res = MCP2210.M_Mcp2210_GetLastError();
                if (res != MCP2210.M_E_SUCCESS)
                {
                    Console.WriteLine("Failed to open connection");
                    return -1;
                }


                // set the SPI xFer params for I/O expander
                uint baudRate2 = 1000000;
                uint idleCsVal2 = 0x1ff;
                uint activeCsVal2 = 0x1ee; // GP4 and GP0 set as active low CS
                uint csToDataDly2 = 0;
                uint dataToDataDly2 = 0;
                uint dataToCsDly2 = 0;
                uint txFerSize2 = 4;     // I/O expander xFer size set to 4
                byte spiMd2 = 0;
                uint csMask4 = 0x10;  // set GP4 as CS

                byte[] txData = new byte[4], rxData = new byte[4];
                // set the expander config params
                txData[0] = 0x40;
                txData[1] = 0x0A;
                txData[2] = 0xff;
                txData[3] = 0x00;
                // send the data
                // use the extended SPI xFer API first time in order to set all the parameters
                // the subsequent xFer with the same device may use the simple API in order to save CPU cycles
                res = MCP2210.M_Mcp2210_xferSpiDataEx(deviceHandle, txData, rxData, ref baudRate2, ref txFerSize2, csMask4, ref idleCsVal2, ref activeCsVal2, ref csToDataDly2,
                    ref dataToCsDly2, ref dataToDataDly2, ref spiMd2);
                if (res != MCP2210.M_E_SUCCESS)
                {
                    MCP2210.M_Mcp2210_Close(deviceHandle);
                    Console.WriteLine(" Transfer error: " + res);
                    return res;
                }

                txFerSize2 = 3;          // set the txFer size to 3 -> don't write to mcp23s08 ioDir again
                uint csMaskNoChange = 0x10000000;   // preserve the CS selection and skip the GP8CE fix -> data xFer optimization
                for (byte i = 0; i < 255; i++)
                {
                    txData[2] = i;
                    // we don't need to change all SPI params so we can start using the faster xFer API
                    res = MCP2210.M_Mcp2210_xferSpiData(deviceHandle, txData, rxData, ref baudRate2, ref txFerSize2, csMaskNoChange);
                    if (res != MCP2210.M_E_SUCCESS)
                    {
                        MCP2210.M_Mcp2210_Close(deviceHandle);
                        return res;
                    }
                }

                // turn off the led -> set the mcp23s08 ioDir to 0xFF;
                txData[0] = 0x40;
                txData[1] = 0x00;
                txData[2] = 0xFF;
                res = MCP2210.M_Mcp2210_xferSpiData(deviceHandle, txData, rxData, ref baudRate2, ref txFerSize2, csMaskNoChange);
                if (res != MCP2210.M_E_SUCCESS)
                {
                    MCP2210.M_Mcp2210_Close(deviceHandle);
                    return res;
                }


                MCP2210.M_Mcp2210_Close(deviceHandle);

            }


            return 0;

        }
    }
}