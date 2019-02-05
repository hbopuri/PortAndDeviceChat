namespace Smart.Agent.Constant
{
    public class CommandType
    {
        public const int Collect = 1;
        public const int ReadDataPort = 2;
        public const int StartEdcAllOver = 4;
        public const int IncrementAfeDevice = 5;
        public const int DecrementAfeDevice = 6;
        public const int SaveAfe = 7;
        public const int PowerOff = 8;
        internal const int PowerOn = 0;
        internal const int Connect = 11;
        internal const int WriteDataPort = 12;
        internal const int If = 13;
    }
}