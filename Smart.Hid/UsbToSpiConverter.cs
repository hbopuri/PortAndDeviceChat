using System;
using System.Text;
using mcp2210_dll_m;
namespace Smart.Hid
{
    public class UsbToSpiConverter
    {
        /**** Constants ****/
        public const ushort DEFAULT_VID = 0x4d8;
        public const ushort DEFAULT_PID = 0xde;

        public static int Init()
        {
            int devCount = MCP2210.M_Mcp2210_GetConnectedDevCount(DEFAULT_VID, DEFAULT_PID);
            Console.WriteLine(devCount + " devices found");
            if (devCount > 0)
            {
                StringBuilder path = new StringBuilder();
                IntPtr deviceHandle = new IntPtr();
                int res;

                deviceHandle = MCP2210.M_Mcp2210_OpenByIndex(DEFAULT_VID, DEFAULT_PID, 0, path); //try to open the first device
                res = MCP2210.M_Mcp2210_GetLastError();
                if (res != MCP2210.M_E_SUCCESS)
                {
                    Console.WriteLine("Failed to open connection");
                    return -1;
                }


                // set the SPI xfer params for I/O expander
                uint pbaudRate2 = 1000000;
                uint pidleCsVal2 = 0x1ff;
                uint pactiveCsVal2 = 0x1ee; // GP4 and GP0 set as active low CS
                uint pcsToDataDly2 = 0;
                uint pdataToDataDly2 = 0;
                uint pdataToCsDly2 = 0;
                uint ptxferSize2 = 4;     // I/O expander xfer size set to 4
                byte pspiMd2 = 0;
                uint csmask4 = 0x10;  // set GP4 as CS

                byte[] txData = new byte[4], rxData = new byte[4];
                // set the expander config params
                txData[0] = 0x40;
                txData[1] = 0x0A;
                txData[2] = 0xff;
                txData[3] = 0x00;
                // send the data
                // use the extended SPI xfer API first time in order to set all the parameters
                // the subsequent xfers with the same device may use the simple API in order to save CPU cycles
                res = MCP2210.M_Mcp2210_xferSpiDataEx(deviceHandle, txData, rxData, ref pbaudRate2, ref ptxferSize2, csmask4, ref pidleCsVal2, ref pactiveCsVal2, ref pcsToDataDly2,
                    ref pdataToCsDly2, ref pdataToDataDly2, ref pspiMd2);
                if (res != MCP2210.M_E_SUCCESS)
                {
                    MCP2210.M_Mcp2210_Close(deviceHandle);
                    Console.WriteLine(" Transfer error: " + res);
                    return res;
                }

                ptxferSize2 = 3;          // set the txfer size to 3 -> don't write to mcp23s08 IODIR again
                uint csmask_nochange = 0x10000000;   // preserve the CS selection and skip the GP8CE fix -> data xfer optimization
                for (byte i = 0; i < 255; i++)
                {
                    txData[2] = i;
                    // we don't need to change all SPI params so we can start using the faster xfer API
                    res = MCP2210.M_Mcp2210_xferSpiData(deviceHandle, txData, rxData, ref pbaudRate2, ref ptxferSize2, csmask_nochange);
                    if (res != MCP2210.M_E_SUCCESS)
                    {
                        MCP2210.M_Mcp2210_Close(deviceHandle);
                        return res;
                    }
                }

                // turn off the leds -> set the mcp23s08 iodir to 0xFF;
                txData[0] = 0x40;
                txData[1] = 0x00;
                txData[2] = 0xFF;
                res = MCP2210.M_Mcp2210_xferSpiData(deviceHandle, txData, rxData, ref pbaudRate2, ref ptxferSize2, csmask_nochange);
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