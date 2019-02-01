using System;

namespace Smart.Log
{
    public static class SmartLog
    {
        public static void WriteLine()
        {
            Console.WriteLine();
        }
        public static void WriteLine(string value)
        {
            Console.WriteLine(value);
        }

        public static void WriteLine(string format, params object[] arg)
        {
            Console.WriteLine(format, arg);
        }
    }
}
