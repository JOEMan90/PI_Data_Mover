using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using OSIsoft.AF.PI;
using OSIsoft.AF.Time;

namespace PI_Data_Mover
{
    public static class ConfigurationParameters
    {
        //This class holds the configuration parameters for the interface.

        #region Properties

        public static string PIPointsFile;
        public static DataCollectionModes DataCollectionMode;
        public static PIServer SourcePIDataArchive;
        public static PIServer DestinationPIDataArchive;
        public static double DestinationSendDataRate;
        public static double SourceReadDataRate;
        public static AFTime HistoryRecoveryStart;
        public static AFTime HistoryRecoveryEnd;
        public static int MaxSnapshotEvents;
        public static int SourceArchiveMaxRate;
        public static int MaxRangeTime;

        public static Dictionary<ErrorTypes, string> Errors;

        public enum ErrorTypes
        {
            None,
            BadMode,
            BadStartTime,
            BadEndTime,
            NullParameter,
            BadDestinationServer,
            BadSourceServer,
            BadMaxSnapshotEvents,
            LoadingPIPoints,
            Unknown
        }

        #endregion

        #region Public Methods

        public static void Initialize()
        {
            //Initialize the static class
            InitializeErrors();
        }

        public static ErrorTypes LoadConfigurationParameters()
        {
            //This method loads all of the configuration parameters from the application configuration file
            PIPointsFile = ConfigurationManager.AppSettings["PIPointsFile"];

            if (!Enum.TryParse(ConfigurationManager.AppSettings["DataCollectionMode"], true, out DataCollectionMode))
            {
                return ErrorTypes.BadMode;
            }

            string strSourcePIDataArchive = ConfigurationManager.AppSettings["SourcePIDataArchive"];
            SourcePIDataArchive = PIServer.FindPIServer(strSourcePIDataArchive);

            string strDestinationPIDataArchive = ConfigurationManager.AppSettings["DestinationPIDataArchive"];

            try
            {
                DestinationPIDataArchive = PIServer.FindPIServer(strDestinationPIDataArchive);
            }
            catch
            {
                return ErrorTypes.BadDestinationServer;
            }

            if (!Double.TryParse(ConfigurationManager.AppSettings["DestinationSendDataRate"], out DestinationSendDataRate))
            {
                //TODO Throw error
            }

            if (DataCollectionMode == DataCollectionModes.Real)
            {
                if (!Double.TryParse(ConfigurationManager.AppSettings["SourceReadDataRate"], out SourceReadDataRate))
                {
                    //TODO Throw error
                }
            }//DataCollectionMode == Real

            if (DataCollectionMode == DataCollectionModes.Historical)
            {
                try
                {
                    DateTime HistoryRecoveryStart_Local = Convert.ToDateTime(ConfigurationManager.AppSettings["HistoryRecoveryStart"]);
                    HistoryRecoveryStart = HistoryRecoveryStart_Local.ToUniversalTime();
                }
                catch
                {
                    return ErrorTypes.BadStartTime;
                }

                try
                {
                    DateTime HistoryRecoveryEnd_Local = Convert.ToDateTime(ConfigurationManager.AppSettings["HistoryRecoveryEnd"]);
                    HistoryRecoveryEnd = HistoryRecoveryEnd_Local.ToUniversalTime();
                }
                catch
                {
                    return ErrorTypes.BadEndTime;
                }
            }//DataCollectionMode == HR

            if (!Int32.TryParse(ConfigurationManager.AppSettings["MaxSnapshotEvents"], out MaxSnapshotEvents))
            {
                return ErrorTypes.BadMaxSnapshotEvents;
            }

            if (!Int32.TryParse(ConfigurationManager.AppSettings["SourceArchiveMaxRate"], out SourceArchiveMaxRate))
            {
                SourceArchiveMaxRate = 1000000;
            }

            if (!Int32.TryParse(ConfigurationManager.AppSettings["MaxRangeTime"], out MaxRangeTime))
            {
                MaxRangeTime = 600;
            }

            PIPoints.ErrorTypes PIPointLoadingResult = PIPoints.LoadPIPointsFromFile();

            if (PIPointLoadingResult != PIPoints.ErrorTypes.None)
            {
                return ErrorTypes.LoadingPIPoints; 
            }

            return ErrorTypes.None;

        }//LoadConfigurationParameters

        #endregion

        #region Private Methods

        private static void InitializeErrors()
        {
            //This method initializes the local Errors dictionary

            Errors = new Dictionary<ErrorTypes, string>
            {
                {ErrorTypes.Unknown, "An unknown error has occurred while processing configuration errors." },
                {ErrorTypes.BadDestinationServer, "There was an issue locating the supplied Destination PI Data Archive." },
                {ErrorTypes.BadSourceServer, "There was an issue locating the supplied Source PI Data Archive." },
                {ErrorTypes.BadMode, "An incorrect data collection mode was supplied in the configuration file." },
                {ErrorTypes.BadStartTime, "An incorrect start time was supplied for history recovery." },
                {ErrorTypes.BadEndTime, "An incorrect end time was supplied for history recovery." },
                {ErrorTypes.NullParameter, "One of the configuration parameters supplied was empty. All parameters must have a value to be valid." },
                {ErrorTypes.BadMaxSnapshotEvents, "The argument for MaxSnapshotEvents is not valid." },
                {ErrorTypes.LoadingPIPoints, "An error occurred while loading the PI points. Please review log for more details." },
                {ErrorTypes.None, "No error has occurred." }
            };

        }//InitializeErrors

        #endregion
    }//ConfigurationParameters
}
