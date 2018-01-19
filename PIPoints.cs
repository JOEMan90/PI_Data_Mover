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
            bool isDestination = false;

            //Load the points from the point configuration file
            switch (serverType)
            {
                case ServerType.Source:
                    lstPIPoints_from_File = PIPoints_from_File.Select(x => @"\\" + ConfigurationParameters.SourcePIDataArchive.Name + @"\" + x).ToList();
                    break;

                case ServerType.Destination:
                    lstPIPoints_from_File = PIPoints_from_File.Select(x => @"\\" + ConfigurationParameters.DestinationPIDataArchive.Name + @"\" + x).ToList();
                    isDestination = true;
                    break;
            }

            //Load the points from the target PI Data Archive
            AFKeyedResults<string, PIPoint> findPointsResult = PIPoint.FindPIPointsByPath(lstPIPoints_from_File);

            //Perform error checking/logging
            CheckPIPointsForErrors(findPointsResult, isDestination);

            return findPointsResult;
        }//LoadPIPointsFromServer

        private static void CheckPIPointsForErrors(AFKeyedResults<string, PIPoint> findPointsResult, bool isDestination = false)
        {
            //This method will run error checks on the list of PI points returned by the FindPIPoints call
            if (findPointsResult.HasErrors)
            {
                List<string> pointsToBeCreated = new List<string>();

                Parallel.ForEach(findPointsResult.Errors, (ePoint) =>
                {
                    if (isDestination && ePoint.Value.HResult == -2146232969)
                    {
                        //This should mean that the point was not found, and thus, we will see if we should create it.

                        //Pull out the PI point name from the full path used to find the point
                        string pointName = ExtractPointName(ePoint.Key);

                        Logger.Log("The application failed to load PI point <" + pointName + "> from the destination PI Data Archive but will attempt to create it. The error received during loading was: " + ePoint.Value.HResult + " | " + ePoint.Value.Message + " | " + ePoint.Value.InnerException + ".", System.Diagnostics.EventLogEntryType.Warning);

                        pointsToBeCreated.Add(pointName);
                    }
                    else
                    {
                        //Remove the point from the point list
                        string pointName = ExtractPointName(ePoint.Key);
                        PIPoints_from_File = PIPoints_from_File.Where(s => s != PIPoints_from_File[Array.IndexOf(PIPoints_from_File, pointName)]).ToArray();
                        Logger.Log("The application failed to load the PI point <" + pointName + "> from the source PI Data Archive. Therefore, no data will be collected for this point. The error received is as follows: " + ePoint.Value.HResult + " | " + ePoint.Value.Message + " | " + ePoint.Value.InnerException + ".", System.Diagnostics.EventLogEntryType.Error);
                    }
                });//Parallel.ForEach

                //Check if there are pointed to be created and if so, create them.
                if (pointsToBeCreated.Count != 0 && ConfigurationParameters.EnablePointCreation)
                {
                    CreatePIPoints(pointsToBeCreated);
                }

            }//if hasErrors
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

        private static void CreatePIPoints(List<string> pointsToBeCreated)
        {
            //This method will create the associated PI points on the desintation PI Data Archive

            //Create the PI points
            var result = ConfigurationParameters.DestinationPIDataArchive.CreatePIPoints(pointsToBeCreated);

            //Check for errors and log them
            if (result.HasErrors)
            {
                Parallel.ForEach(result.Errors, (eResult) =>
                {
                    string logMsg = "The application failed to create the following PI point on the destination PI Data Archive: ";
                    logMsg += eResult.Key;
                    logMsg += ". The following error occurred: ";
                    logMsg += eResult.Value + ".";
                    Logger.Log(logMsg, System.Diagnostics.EventLogEntryType.Error);
                });
            }
        }

        private static string ExtractPointName(string pointNameWithPath, bool isDestination = false)
        {
            //This method takes a PI point name which includes the path (Such as \\MyPIDataArchive\MyTagName) and extracts the tag name

            int serverNameLength = 0;

            if (isDestination)
            {
                serverNameLength = ConfigurationParameters.DestinationPIDataArchive.Name.Length;
            }
            else
            {
                serverNameLength = ConfigurationParameters.SourcePIDataArchive.Name.Length;
            }

            //Name should have two preceeding '\' and one '\' as a separator for the tag name. This should be an illegal character for PI point name.
            int serverNameLengthWithExtras = 3 + serverNameLength;

            string pointName = pointNameWithPath.Substring(serverNameLengthWithExtras, pointNameWithPath.Length - serverNameLengthWithExtras);

            return pointName;

        }//ExtractPointName

        #endregion

    }//PIPoints
}
