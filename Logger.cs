using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PI_Data_Mover
{
    public static class Logger
    {
        //This class handles logging to the Windows Event Log

        #region Properties

        private static EventLog PIDMEventLog;

        #endregion

        #region Methods

        public static void Initialize()
        {
            //This initializes the event logs for the application
            PIDMEventLog = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("PI Data Mover"))
            {
                System.Diagnostics.EventLog.CreateEventSource("PI Data Mover", "PI Data Mover");
            }

            PIDMEventLog.Source = "PI Data Mover";
            PIDMEventLog.Log = "PI Data Mover";
        }//Initialize

        public static void Log(string message, EventLogEntryType type)
        {
            //Logs the message to the event log
            PIDMEventLog.WriteEntry(message, type);
        }//Log

        public static void StoreErroredValue(string PIPointName, string value)
        {
            //This method will accept a PIPointName/Value combination and store it in a log file for later reference.

            //TODO: Should I pull from config file?
            string fileName = @".\ValuesInError.txt";

            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(fileName, true))
            {
                file.WriteLine($"{PIPointName}:{value}\n");
            }
        }//StoreErroredValues

        #endregion

    }
}
