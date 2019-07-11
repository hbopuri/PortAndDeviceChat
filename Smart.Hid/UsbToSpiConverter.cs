using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using mcp2210_dll_m;
using Smart.Log;
using Smart.Log.Enum;
using Smart.Log.Helper;

namespace Smart.Hid
{
    public class UsbToSpiConverter
    {
        /**** Constants ****/
        public const ushort DefaultVid = 0x4d8;
        public const ushort DefaultPid = 0xde;
        private static IntPtr _deviceHandle;
        private static int _response;
        private static bool _printRequest;
        private static int _deviceCount;
        private static int _value = 0x0080; //MIdValue: 0x0080; 0x0063;

        public static async Task<int> Init(bool printRequest)
        {
            _printRequest = printRequest;
            _deviceCount = MCP2210.M_Mcp2210_GetConnectedDevCount(DefaultVid, DefaultPid);
            SmartLog.WriteLine(_deviceCount + " SPI Module Device(s) found");
            if (_deviceCount <= 0) return _response;
            StringBuilder path = new StringBuilder();
            _deviceHandle = MCP2210.M_Mcp2210_OpenByIndex(DefaultVid, DefaultPid, 0, path);
            _response = MCP2210.M_Mcp2210_GetLastError();
            if (_response != MCP2210.M_E_SUCCESS)
            {
                SmartLog.WriteErrorLine("Failed to open connection");
                return await Task.FromResult(-1);
            }

            return _response;
        }

        public static void Close()
        {
            MCP2210.M_Mcp2210_Close(_deviceHandle);
            _deviceCount = 0;
        }

        public static async Task<int> IncrementOrDecrementStrain(SgAdjust sgAdjust)
        {
            if (_deviceCount == 0)
            {
                await Init(_printRequest);
            }


            // set the SPI xFer params for I/O expander
            uint baudRate2 = 250000; //1000000
            uint idleCsVal2 = 0x1ff;
            uint activeCsVal2 = 0x1fd; //GP1 CS PIN, remaining pins are GP //0x1ee GP4 and GP0 set as active low CS
            uint csToDataDly2 = 0;
            uint dataToDataDly2 = 0;
            uint dataToCsDly2 = 0;
            uint txFerSize2 = 3; // I/O expander xFer size set to 4
            byte spiMd2 = 0;
            uint csMask4 = 0x02; //GP1 as CS  // 0x10 set GP4 as CS

            byte[] txData = new byte[3], rxData = new byte[3];
            switch (sgAdjust)
            {
                // set the expander config params
                case SgAdjust.Increment:
                    txData[0] = 0x60;
                    break;
                case SgAdjust.Decrement:
                    txData[0] = 0xE0;
                    break;
                case SgAdjust.Save:
                    txData[0] = 0x20;
                    //SmartLog.WriteLine(
                    //    $"txData:{txData.ToHex()}\nrxData:{rxData.ToHex()}\nbaudRate2:{baudRate2}\ntxFerSize2:{txFerSize2}\ncsMask4:{csMask4}\nidleCsVal2:{idleCsVal2}\nactiveCsVal2:{activeCsVal2}\ncsToDataDly2:{csToDataDly2}\ndataToCsDly2:{dataToCsDly2}\ndataToDataDly2:{dataToDataDly2}\nspiMd2:{spiMd2}");
                    break;
            }

            txData[1] = 0x00;
            txData[2] = 0x00;
            // send the data
            // use the extended SPI xFer API first time in order to set all the parameters
            // the subsequent xFer with the same device may use the simple API in order to save CPU cycles
            _response = MCP2210.M_Mcp2210_xferSpiDataEx(_deviceHandle, txData, rxData, ref baudRate2, ref txFerSize2,
                csMask4, ref idleCsVal2, ref activeCsVal2, ref csToDataDly2,
                ref dataToCsDly2, ref dataToDataDly2, ref spiMd2);
            if (_printRequest)
                SmartLog.WriteHighlight($"Strain req: " + txData.ToHex());
            if (_response != MCP2210.M_E_SUCCESS)
            {
                MCP2210.M_Mcp2210_Close(_deviceHandle);
                SmartLog.WriteErrorLine($"Sg ({sgAdjust.ToString()}) transfer error: " + _response);
                _deviceCount = 0;
                return _response;
            }

            SmartLog.WriteLine($"Sg ({sgAdjust.ToString()}) transfer response: " + _response);
            return 0;
        }

        public static async Task<int> IncrementOrDecrementAx(AxAdjust axAdjust)
        {
            if (_deviceCount == 0)
            {
                await Init(_printRequest);
            }

            // set the SPI xFer params for I/O expander
            uint baudRate2 = 250000; //1000000
            uint idleCsVal2 = 0x01;
            uint activeCsVal2 = 0x1fe; //GP0 CS PIN, remaining pins are GP //0x1ee GP4 and GP0 set as active low CS
            uint csToDataDly2 = 0;
            uint dataToDataDly2 = 0;
            uint dataToCsDly2 = 0;
            uint txFerSize2 = 2; // I/O expander xFer size set to 4
            byte spiMd2 = 0;
            uint csMask4 = 0x01; //GP0 as CS  // 0x10 set GP4 as CS

            byte[] txData = new byte[2], rxData = new byte[2];
            switch (axAdjust)
            {
                // set the expander config params
                case AxAdjust.Min:
                    _value--;
                    txData = _value.ToString("X4").ToByteArray();
                    //txData[0] = 0x00;
                    //txData[1] = 0x00;
                    break;
                case AxAdjust.Mid:
                    txData[0] = 0x00;
                    txData[1] = 0x80;
                    break;
                case AxAdjust.Max:
                    _value++;
                    txData = _value.ToString("X4").ToByteArray();
                    //txData[0] = 0x03;
                    //txData[1] = 0xFF;
                    break;
                case AxAdjust.HitTheNail:

                    txData[0] = 0x00;
                    txData[1] = 0x5F;
                    break;
                case AxAdjust.ToDesiredRange:
                    txData[0] = 0x00;
                    txData[1] = 0x63;
                    break;
                case AxAdjust.Save:
                    txData[0] = 0x20;
                    txData[1] = _value.ToString("X4").ToByteArray()[1];
                    //SmartLog.WriteErrorLine($"Value: {_value:X4}");
                    //SmartLog.WriteLine(
                    //    $"txData:{txData.ToHex()}\nrxData:{rxData.ToHex()}\nbaudRate2:{baudRate2}\ntxFerSize2:{txFerSize2}\ncsMask4:{csMask4}\nidleCsVal2:{idleCsVal2}\nactiveCsVal2:{activeCsVal2}\ncsToDataDly2:{csToDataDly2}\ndataToCsDly2:{dataToCsDly2}\ndataToDataDly2:{dataToDataDly2}\nspiMd2:{spiMd2}");
                    break;
            }

            // send the data
            // use the extended SPI xFer API first time in order to set all the parameters
            // the subsequent xFer with the same device may use the simple API in order to save CPU cycles
            _response = MCP2210.M_Mcp2210_xferSpiDataEx(_deviceHandle, txData, rxData, ref baudRate2, ref txFerSize2,
                csMask4, ref idleCsVal2, ref activeCsVal2, ref csToDataDly2,
                ref dataToCsDly2, ref dataToDataDly2, ref spiMd2);
            if(_printRequest)
                SmartLog.WriteHighlight($"Ax req: " + txData.ToHex());
            if (_response != MCP2210.M_E_SUCCESS)
            {
                MCP2210.M_Mcp2210_Close(_deviceHandle);
                SmartLog.WriteErrorLine($"Ax ({axAdjust.ToString()}) transfer error: " + _response);
                _deviceCount = 0;
                return _response;
            }

            SmartLog.WriteLine($"Ax ({axAdjust.ToString()}) transfer response: " + _response);
            return 0;
        }
    }
}