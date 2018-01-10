using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OSIsoft.AF.PI;
using System.IO;
using OSIsoft.AF;

namespace PI_Data_Mover
{
    public static class PIPoints
    {
        //This class holds the interface's current PI points as well as related functions

        #region Properties

        public enum ErrorTypes
        {
            None,
            NoPIPointsFile,
            NoPIPointsLoaded_Source,
            NoPIPointsLoaded_Destination,
            PIPointFailedLoad,
            Unknown
        }
        public static Dictionary<ErrorTypes, string> Errors;
        public static List<PIPoint> CurrentPIPoints_Destination;
        public static List<PIPoint> CurrentPIPoints_Source;
        public static int Count = 0;
        public static PIPointList CurrentPIPoints_Destination_PIPointList = new PIPointList();
        public static PIPointList CurrentPIPoints_Source_PIPointList = new PIPointList();
        public static Dictionary<string, PIPoint> CurrentPIPoints_Dictionary = new Dictionary<string, PIPoint>();

        private static string[] PIPoints_from_File;
        private enum ServerType
        {
            Source,
            Destination
        }

        #endregion

        #region Methods

        public static void Initialize()
        {
            //Initializes component
        }//Initialize

        public static ErrorTypes LoadPIPointsFromFile()
        {
            //This method loads all PI points from configuration file into memory
            PIPoints_from_File = File.ReadAllLines(ConfigurationParameters.PIPointsFile);

            AFKeyedResults<string, PIPoint> loadedPoints_Source = LoadPIPointsFromServer(ServerType.Source);
            AFKeyedResults<string, PIPoint> loadedPoints_Destination = LoadPIPointsFromServer(ServerType.Destination);

            if (loadedPoints_Source.Count == 0)
            {
                Logger.Log(Errors[ErrorTypes.NoPIPointsLoaded_Source], System.Diagnostics.EventLogEntryType.Error);
                return ErrorTypes.NoPIPointsLoaded_Source;
            }

            if (loadedPoints_Destination.Count == 0)
            {
                Logger.Log(Errors[ErrorTypes.NoPIPointsLoaded_Destination], System.Diagnostics.EventLogEntryType.Error);
                return ErrorTypes.NoPIPointsLoaded_Destination;
            }

            //Create the list of PIPoint objects from the actually loaded PI points
            CurrentPIPoints_Destination = loadedPoints_Destination.Select(p => p).ToList();
            CurrentPIPoints_Source = loadedPoints_Source.Select(p => p).ToList();

            //Populate the rest of the PIPoints properties
            Count = CurrentPIPoints_Destination.Count;
            CurrentPIPoints_Source_PIPointList = new PIPointList(CurrentPIPoints_Source);
            CurrentPIPoints_Destination_PIPointList = new PIPointList(CurrentPIPoints_Destination);

            //Create the point mapping dictionary 
            //TODO: There has to be a more efficient way to do this
            Parallel.ForEach(CurrentPIPoints_Destination_PIPointList, point => CurrentPIPoints_Dictionary.Add(point.Name, point));

            //TODO: Add source server check
            Logger.Log("The application successfully loaded " + Count + " PI points.", System.Diagnostics.EventLogEntryType.Information);
            return ErrorTypes.None;

        }//LoadPIPointsFromFile

        private static AFKeyedResults<string, PIPoint> LoadPIPointsFromServer(ServerType serverType)
        {
            //This method pulls a list of PI points from the designated server
            List<string> lstPIPoints_from_File = new List<string>();

            //Load the points from the point configuration file
            switch (serverType)
            {
                case ServerType.Source:
                    lstPIPoints_from_File = PIPoints_from_File.Select(x => @"\\" + ConfigurationParameters.SourcePIDataArchive.Name + @"\" + x).ToList();
                    break;

                case ServerType.Destination:
                    lstPIPoints_from_File = PIPoints_from_File.Select(x => @"\\" + ConfigurationParameters.DestinationPIDataArchive.Name + @"\" + x).ToList();
                    break;
            }

            //Load the points from the target PI Data Archive
            AFKeyedResults<string, PIPoint> findPointsResult = PIPoint.FindPIPointsByPath(lstPIPoints_from_File);

            //Perform error checking/logging
            CheckPIPointsForErrors(findPointsResult);

            return findPointsResult;
        }//LoadPIPointsFromServer

        private static void CheckPIPointsForErrors(AFKeyedResults<string, PIPoint> findPointsResult)
        {
            //This method will run error checks on the list of PI points returned by the FindPIPoints call
            if (findPointsResult.HasErrors)
            {
                //List<string> errorPIPoints = new List<string>();
                foreach (var ePoint in findPointsResult.Errors)
                {
                    //errorPIPoints.Add(ePoint.Key);
                    Logger.Log("The application failed to load the following PI point: " + ePoint.Key + " with the following error: " + ePoint.Value, System.Diagnostics.EventLogEntryType.Warning);
                }
            }
        }//CheckPIPointsForErrors

        private static void _FindPIPoints(ServerType serverType)
        {
            //
        }//_FindPIPoints

        private static void InitializeErrors()
        {
            //Builds error dictionary
            Errors = new Dictionary<ErrorTypes, string>
            {
                {ErrorTypes.NoPIPointsLoaded_Source, "No PI points were successfully loaded from the source PI Data Archive." },
                {ErrorTypes.NoPIPointsLoaded_Destination, "No PI points were successfully loaded from the destination PI Data Archive." }
            };
        }

        #endregion

    }//PIPoints
}
