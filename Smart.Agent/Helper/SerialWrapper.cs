using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Smart.Agent.Helper
{
    public class SerialWrapper : IDisposable
    {
        #region Enum
        public enum StopBits
        {
            None,
            One,
            Two,
            OnePointFive
        }

        public enum Parity
        {
            None,
            Odd,
            Even,
            Mark,
            Space
        }
        #endregion
        #region Fields
        /// <summary>
        /// The baud rate at which the communications device operates.
        /// </summary>
        private readonly int _iBaudRate;

        /// <summary>
        /// The number of bits in the bytes to be transmitted and received.
        /// </summary>
        private readonly byte _byteSize;

        /// <summary>
        /// The system handle to the serial port connection ('file' handle).
        /// </summary>
        private IntPtr _pHandle = IntPtr.Zero;

        /// <summary>
        /// The parity scheme to be used.
        /// </summary>
        private readonly Parity _parity;

        /// <summary>
        /// The name of the serial port to connect to.
        /// </summary>
        private readonly string _sPortName;

        /// <summary>
        /// The number of bits in the bytes to be transmitted and received.
        /// </summary>
        private readonly StopBits _stopBits;
        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new instance of SerialCom.
        /// </summary>
        /// <param>The name of the serial port to connect to</param>
        /// <param>The baud rate at which the communications device operates</param>
        /// <param>The number of stop bits to be used</param>
        /// <param>The parity scheme to be used</param>
        /// <param>The number of bits in the bytes to be transmitted and received</param>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="stopBits"></param>
        /// <param name="parity"></param>
        /// <param name="byteSize"></param>
        public SerialWrapper(string portName, int baudRate, StopBits stopBits, Parity parity, byte byteSize)
        {
            if (stopBits == StopBits.None)
                throw new ArgumentException("stopBits cannot be StopBits.None", "stopBits");
            if (byteSize < 5 || byteSize > 8)
                throw new ArgumentOutOfRangeException("The number of data bits must be 5 to 8 bits.", "byteSize");
            if (baudRate < 110 || baudRate > 256000)
                throw new ArgumentOutOfRangeException("Invalid baud rate specified.", "baudRate");
            if ((byteSize == 5 && stopBits == StopBits.Two) || (stopBits == StopBits.OnePointFive && byteSize > 5))
                throw new ArgumentException("The use of 5 data bits with 2 stop bits is an invalid combination, " +
                    "as is 6, 7, or 8 data bits with 1.5 stop bits.");

            _sPortName = portName;
            _iBaudRate = baudRate;
            _byteSize = byteSize;
            _stopBits = stopBits;
            _parity = parity;
        }

        /// <summary>
        /// Creates a new instance of SerialCom.
        /// </summary>
        /// <param>The name of the serial port to connect to</param>
        /// <param>The baud rate at which the communications device operates</param>
        /// <param>The number of stop bits to be used</param>
        /// <param>The parity scheme to be used</param>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="stopBits"></param>
        /// <param name="parity"></param>
        public SerialWrapper(string portName, int baudRate, StopBits stopBits, Parity parity)
          : this(portName, baudRate, stopBits, parity, 8)
        {

        }
        #endregion

        #region Open
        /// <summary>
        /// Opens and initializes the serial connection.
        /// </summary>
        /// <returns>Whether or not the operation succeeded</returns>
        public bool Open()
        {
            _pHandle = CreateFile(_sPortName, FileAccess.ReadWrite, FileShare.None,
                IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
            if (_pHandle == IntPtr.Zero) return false;

            if (ConfigureSerialPort()) return true;
            Dispose();
            return false;
        }
        #endregion

        #region Write

        /// <summary>
        /// Transmits the specified array of bytes.
        /// </summary>
        /// <param>The bytes to write</param>
        /// <param name="data"></param>
        /// <returns>The number of bytes written (-1 if error)</returns>
        public int Write(byte[] data)
        {
            FailIfNotConnected();
            if (data == null) return 0;

            if (WriteFile(_pHandle, data, data.Length, out var bytesWritten, 0))
                return bytesWritten;
            return -1;
        }

        /// <summary>
        /// Transmits the specified string.
        /// </summary>
        /// <param>The string to write</param>
        /// <param name="data"></param>
        /// <returns>The number of bytes written (-1 if error)</returns>
        public int Write(string data)
        {
            FailIfNotConnected();

            // convert the string to bytes
            byte[] bytes;
            if (data == null)
            {
                bytes = null;
            }
            else
            {
                bytes = Encoding.UTF8.GetBytes(data);
            }

            return Write(bytes);
        }

        /// <summary>
        /// Transmits the specified string and appends the carriage return to the end
        /// if it does not exist.
        /// </summary>
        /// <remarks>
        /// Note that the string must end in '\r\n' before any serial device will interpret the data
        /// sent. For ease of programmability, this method should be used instead of Write() when you
        /// want to automatically execute the specified command string.
        /// </remarks>
        /// <param>The string to write</param>
        /// <param name="data"></param>
        /// <returns>The number of bytes written (-1 if error)</returns>
        public int WriteLine(string data)
        {
            if (data != null && !data.EndsWith("\r\n"))
                data += "\r\n";
            return Write(data);
        }
        #endregion

        #region ReadDataPortConfig

        /// <summary>
        /// Reads any bytes that have been received and writes them to the specified array.
        /// </summary>
        /// <param>The array to write the read data to</param>
        /// <param name="data"></param>
        /// <returns>The number of bytes read (-1 if error)</returns>
        public int Read(byte[] data)
        {
            FailIfNotConnected();
            if (data == null) return 0;

            if (ReadFile(_pHandle, data, data.Length, out var bytesRead, 0))
                return bytesRead;
            return -1;
        }

        /// <summary>
        /// Reads any data that has been received as a string.
        /// </summary>
        /// <param>The maximum number of bytes to read</param>
        /// <param name="maxBytesToRead"></param>
        /// <returns>The data received (null if no data)</returns>
        public string ReadString(int maxBytesToRead)
        {
            if (maxBytesToRead < 1) throw new ArgumentOutOfRangeException("maxBytesToRead");

            byte[] bytes = new byte[maxBytesToRead];
            int numBytes = Read(bytes);
            //string data = ASCIIEncoding.ASCII.GetString(bytes, 0, numBytes);
            string data = Encoding.UTF8.GetString(bytes, 0, numBytes);
            return data;
        }
        #endregion

        #region Dispose Utils
        /// <summary>
        /// Disconnects and disposes of the SerialCom instance.
        /// </summary>
        public void Dispose()
        {
            if (_pHandle != IntPtr.Zero)
            {
                CloseHandle(_pHandle);
                _pHandle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Flushes the serial I/O buffers.
        /// </summary>
        /// <returns>Whether or not the operation succeeded</returns>
        public bool Flush()
        {
            FailIfNotConnected();

            const int purgeRxClear = 0x0008; // input buffer
            const int purgeTxClear = 0x0004; // output buffer
            return PurgeComm(_pHandle, purgeRxClear | purgeTxClear);
        }
        #endregion

        #region Private Helpers
        /// <summary>
        /// Configures the serial device based on the connection parameters pased in by the user.
        /// </summary>
        /// <returns>Whether or not the operation succeeded</returns>
        private bool ConfigureSerialPort()
        {
            Dcb serialConfig = new Dcb();
            if (GetCommState(_pHandle, ref serialConfig))
            {
                // setup the DCB struct with the serial settings we need
                serialConfig.BaudRate = (uint)_iBaudRate;
                serialConfig.ByteSize = _byteSize;
                serialConfig.fBinary = 1; // must be true
                serialConfig.fDtrControl = 1; // DTR_CONTROL_ENABLE "Enables the DTR line when the device is opened and leaves it on."
                serialConfig.fAbortOnError = 0; // false
                serialConfig.fTXContinueOnXoff = 0; // false

                serialConfig.fParity = 1; // true so that the Parity member is looked at
                switch (_parity)
                {
                    case Parity.Even:
                        serialConfig.Parity = 2;
                        break;
                    case Parity.Mark:
                        serialConfig.Parity = 3;
                        break;
                    case Parity.Odd:
                        serialConfig.Parity = 1;
                        break;
                    case Parity.Space:
                        serialConfig.Parity = 4;
                        break;
                    // ReSharper disable once RedundantCaseLabel
                    case Parity.None:
                    default:
                        serialConfig.Parity = 0;
                        break;
                }
                switch (_stopBits)
                {
                    case StopBits.One:
                        serialConfig.StopBits = 0;
                        break;
                    case StopBits.OnePointFive:
                        serialConfig.StopBits = 1;
                        break;
                    case StopBits.Two:
                        serialConfig.StopBits = 2;
                        break;
                    // ReSharper disable once RedundantCaseLabel
                    case StopBits.None:
                    default:
                        throw new ArgumentException("stopBits cannot be StopBits.None");
                }

                if (SetCommState(_pHandle, ref serialConfig))
                {
                    // set the serial connection timeouts
                    CommTimeout timeouts = new CommTimeout();
                    timeouts.ReadIntervalTimeout = 1;
                    timeouts.ReadTotalTimeoutMultiplier = 0;
                    timeouts.ReadTotalTimeoutConstant = 0;
                    timeouts.WriteTotalTimeoutMultiplier = 0;
                    timeouts.WriteTotalTimeoutConstant = 0;
                    if (SetCommTimeouts(_pHandle, ref timeouts))
                    {
                        return true;
                    }

                    return false;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Helper that throws a InvalidOperationException if we don't have a serial connection.
        /// </summary>
        private void FailIfNotConnected()
        {
            if (_pHandle == IntPtr.Zero)
                throw new InvalidOperationException("You must be connected to the serial port before performing this operation.");
        }
        #endregion

        #region Native Helpers
        #region Native structures
        /// <summary>
        /// Contains the time-out parameters for a communications device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        struct CommTimeout
        {
            public uint ReadIntervalTimeout;
            public uint ReadTotalTimeoutMultiplier;
            public uint ReadTotalTimeoutConstant;
            public uint WriteTotalTimeoutMultiplier;
            public uint WriteTotalTimeoutConstant;
        }

        /// <summary>
        /// Defines the control setting for a serial communications device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        struct Dcb
        {
            public readonly int DcbLength;
            public uint BaudRate;
            public readonly uint Flags;
            public readonly ushort wReserved;
            public readonly ushort XonLim;
            public readonly ushort XoffLim;
            public byte ByteSize;
            public byte Parity;
            public byte StopBits;
            public readonly sbyte XonChar;
            public readonly sbyte XoffChar;
            public readonly sbyte ErrorChar;
            public readonly sbyte EofChar;
            public readonly sbyte EvtChar;
            public readonly ushort wReserved1;
            public uint fBinary;
            public uint fParity;
            public readonly uint fOutxCtsFlow;
            public readonly uint fOutxDsrFlow;
            public uint fDtrControl;
            public readonly uint fDsrSensitivity;
            public uint fTXContinueOnXoff;
            public readonly uint fOutX;
            public readonly uint fInX;
            public readonly uint fErrorChar;
            public readonly uint fNull;
            public readonly uint fRtsControl;
            public uint fAbortOnError;
        }
        #endregion

        #region Native Methods
        // Used to get a handle to the serial port so that we can read/write to it.
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr CreateFile(string fileName,
           [MarshalAs(UnmanagedType.U4)] FileAccess fileAccess,
           [MarshalAs(UnmanagedType.U4)] FileShare fileShare,
           IntPtr securityAttributes,
           [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
           int flags,
           IntPtr template);

        // Used to close the handle to the serial port.
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        // Used to get the state of the serial port so that we can configure it.
        [DllImport("kernel32.dll")]
        static extern bool GetCommState(IntPtr hFile, ref Dcb lpDcb);

        // Used to configure the serial port.
        [DllImport("kernel32.dll")]
        static extern bool SetCommState(IntPtr hFile, [In] ref Dcb lpDcb);

        // Used to set the connection timeouts on our serial connection.
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetCommTimeouts(IntPtr hFile, ref CommTimeout lpCommTimeouts);

        // Used to read bytes from the serial connection.
        [DllImport("kernel32.dll")]
        static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer,
           int nNumberOfBytesToRead, out int lpNumberOfBytesRead, int lpOverlapped);

        // Used to write bytes to the serial connection.
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer,
            int nNumberOfBytesToWrite, out int lpNumberOfBytesWritten, int lpOverlapped);

        // Used to flush the I/O buffers.
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool PurgeComm(IntPtr hFile, int dwFlags);
        #endregion
        #endregion
    }
}