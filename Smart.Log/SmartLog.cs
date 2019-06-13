using System;
using System.Diagnostics;

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

        public static void WriteToEvent(string message)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = "AfeCalibration";
                eventLog.WriteEntry(message, EventLogEntryType.Information, 101, 1);
            }
        }
        public static void WriteErrorLine(string value)
        {
            ConsoleColor borderColor = ConsoleColor.Red;
            Console.ForegroundColor = borderColor;
            Console.WriteLine(value);
            borderColor = ConsoleColor.White;
            Console.ForegroundColor = borderColor;
        }


        public static void WriteLine(string format, params object[] arg)
        {
            Console.WriteLine(format, arg);
        }
    }
}
