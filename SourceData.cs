using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OSIsoft.AF.PI;
using OSIsoft.AF.Time;
using OSIsoft.AF.Asset;

namespace PI_Data_Mover
{
    public class SourceData
    {
        //This class contains all related methods etc. for retrieving source data, both real-time and historical

        #region Properties

        private PIDataPipe snapshotPipe;

        #endregion

        public SourceData(DataCollectionModes dataMode)
        {
            switch(dataMode)
            {
                case DataCollectionModes.Real:
                    snapshotPipe = new PIDataPipe(OSIsoft.AF.Data.AFDataPipeType.Snapshot);
                    InitializeDataPipe();
                    break;

                case DataCollectionModes.Historical:
                    CreateAllHistoricalTimeRanges();
                    break;
            }
        }

        #region Real Time Source Data Methods

        public void StopRealTimeData()
        {
            //This method ends realtime data collection and cleans up the objects.
            KillDataPipe();
        }

        public void GetRealTimeData(Object stateInfo)
        {
            //This method will actually get real time data.
            GetDataFromPipe();
        }

        private void InitializeDataPipe()
        {
            //This method subscribes to the data pipe for the source PI Data Archive

            //Create observer
            PIDataPipeObserver snapshotObserver = new PIDataPipeObserver("Snapshot");

            //Add relevant PI points to the pipe
            snapshotPipe.AddSignups(PIPoints.CurrentPIPoints_Source);

            //Subscribe to daters
            snapshotPipe.Subscribe(snapshotObserver);

        }//InitializeDataPipe

        private void GetDataFromPipe()
        {
            //This method cause events from the data pipe to be sent to the registered observers. 
            bool hasMoreEvents = false;

            try
            {
                do
                {
                    snapshotPipe.GetObserverEvents(ConfigurationParameters.MaxSnapshotEvents, out hasMoreEvents);
                } while (hasMoreEvents);
            }
            catch (ObjectDisposedException odException)
            {
                //TODO
                //Do nothing. According to SO, this is the only reliable solution, LOL
                //(Since there is no really elegant way to check if the object has been disposed or not
            }
            catch (InvalidOperationException ioException)
            {
                //TODO
                //Do nothing, I guess. See above. Classic!
            }
        }//GetDataFromPipe

        private void KillDataPipe()
        {
            //This method properly destroys the data pipe and should be called on shutdown.
            snapshotPipe.Close();
            snapshotPipe.Dispose();
        }//KillDataPipe

        #endregion

        #region Historical Source Data Methods

        public void StartHistoricalDataCollection()
        {
            //This method will actually kick off the various, simultaneous thread tasks for historical history recovery. That is, it actually begins getting historical data.

            //while (Queues.HistoricalRecoveryRange.Count != 0)
            //{
            //    ThreadPool.QueueUserWorkItem(new WaitCallback(GetHistoricalData));
            //}

            int count = Queues.HistoricalRecoveryRange.Count;

            for (int i = 0; i < count; i++)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(GetHistoricalData));
            }
        }//StartHistoricalDataCollection

        private void CreateAllHistoricalTimeRanges()
        {
            //This method will calculate how many historical time ranges to use and what they are; then queue them. 
            Logger.Log("Calculating time ranges for historical data recovery. This could take a minute but have no fear, data transfer will begin shortly!", System.Diagnostics.EventLogEntryType.Information);

            List<int> eventCounts = new List<int>();
            int fractionPointCount = (int)Math.Ceiling((double)(PIPoints.Count / 10));

            //Pull out a random selection of PI points
            Random _random = new Random();
            List<PIPoint> testerPIPoints = new List<PIPoint>();

            for (int i = 0; i < 10 || i < fractionPointCount; i++)
            {
                int randomIndex = _random.Next(PIPoints.Count);
                PIPoint randomPIPoint = PIPoints.CurrentPIPoints_Source[randomIndex];
                testerPIPoints.Add(randomPIPoint);
            }

            //Find the event count for each of the selected test points
            foreach (PIPoint pt in testerPIPoints)
            {
                int cnt = FindEventCount(pt, ConfigurationParameters.HistoryRecoveryStart, ConfigurationParameters.HistoryRecoveryEnd);
                eventCounts.Add(cnt);
            }

            //Determine the average number of events over the whole time range
            double averageCount = eventCounts.Average();

            //Create the ranges and queue them
            CreateRanges(averageCount, ConfigurationParameters.HistoryRecoveryStart, ConfigurationParameters.HistoryRecoveryEnd);

        }//DetermineHistoricalTimeRanges

        private int FindEventCount(PIPoint point, AFTime startTime, AFTime endTime)
        {
            //This method returns the event count for the specified PI point over the specified time range
            int eventCount = 0;

            //Create time range
            AFTimeRange timeRange = new AFTimeRange(startTime, endTime);

            //Call the AFSDK PIPoint.Summary method for Count. This should return a dictionary which contains the event count.
            var summaryResult = point.Summary(timeRange, OSIsoft.AF.Data.AFSummaryTypes.Count, OSIsoft.AF.Data.AFCalculationBasis.EventWeighted, OSIsoft.AF.Data.AFTimestampCalculation.Auto);

            //Pull our desired summary type from the summary result dictionary as an integer
            eventCount = summaryResult[OSIsoft.AF.Data.AFSummaryTypes.Count].ValueAsInt32();

            return eventCount;
        }//FindEventCount

        private void CreateRanges(double avgEventCount, AFTime startTime, AFTime endTime)
        {
            //Creates and queues time ranges for historical data collection based on input parameters

            //Calculate the number of ranges to use
            double totalRangeSeconds = endTime.UtcSeconds - startTime.UtcSeconds;

            int numRanges = CalculateNumberOfRanges(avgEventCount, totalRangeSeconds);

            double timeInterval = totalRangeSeconds / numRanges;

            string logMessage = "";

            for (int i = 1; i <= numRanges; i++)
            {
                //Determine range start time
                double rangeStart_UTCSeconds = startTime.UtcSeconds + (i - 1) * (timeInterval);
                
                //Calculate the end of the current new range
                double rangeEnd_UTCSeconds = startTime.UtcSeconds + (i) * (timeInterval);

                //Ensure that the rangeEnd doesn't exceed the total historical recovery range end time
                if (rangeEnd_UTCSeconds > endTime.UtcSeconds)
                {
                    rangeEnd_UTCSeconds = endTime.UtcSeconds;
                }

                //Create new AFTime objects for the range
                AFTime rangeStart_AFTime = new AFTime(rangeStart_UTCSeconds);
                AFTime rangeEnd_AFTime = new AFTime(rangeEnd_UTCSeconds);

                AFTimeRange range = new AFTimeRange(rangeStart_AFTime, rangeEnd_AFTime);

                //Queue the range!
                Queues.HistoricalRecoveryRange.Enqueue(range);
                logMessage += range.ToString() + "\n";
            }//for i > numRanges

            Logger.Log("The following time ranges will be used for history recovery:\n" + logMessage, System.Diagnostics.EventLogEntryType.Information);
        }//CreateRanges

        private int CalculateNumberOfRanges(double averageEventCount, double overallRangeSeconds)
        {
            //This method calculates the number of ranges to be used/created per supplied parameters
            int numRanges = 0;
            double overallAverageEventCount = averageEventCount * PIPoints.Count;
            double timeRequired = overallAverageEventCount / ConfigurationParameters.SourceArchiveMaxRate;

            //Truncate to nearest integer
            numRanges = (int)(timeRequired / ConfigurationParameters.MaxRangeTime);

            if (numRanges < 1)
            {
                numRanges = 1;
            }

            return numRanges;
        }

        private void GetHistoricalData(object objectState)
        {
            //This method makes the AFSDK archive call to get the data from the source server

            //Get a range from the queue
            AFTimeRange range = (AFTimeRange)Queues.HistoricalRecoveryRange.Dequeue();

            //Create the paging configuration for the AFSDK call
            PIPagingConfiguration pagingConfig = new PIPagingConfiguration(PIPageType.EventCount, 100000);

            //Make the archive call - I.E., get the actual daters
            var resultList = PIPoints.CurrentPIPoints_Source_PIPointList.RecordedValues(range, OSIsoft.AF.Data.AFBoundaryType.Inside, null, false, pagingConfig);

            //Turn it into something the DestinationData function can consume
            //List<AFValue> valueList = new List<AFValue>();

            foreach (AFValues result in resultList)
            {
                Queues.DataToBeSent.Enqueue(result);
            }


        }//GetHistoricalData

        #endregion
    }//SourceData
}
