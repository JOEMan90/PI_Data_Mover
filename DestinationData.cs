using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OSIsoft.AF.PI;
using OSIsoft.AF.Asset;
using OSIsoft.AF.Data;

namespace PI_Data_Mover
{
    public class DestinationData
    {
        //This class holds all the methods and required properties to send data to the destination PI Data Archive
        #region Properties

        private PIServer DestinationServer;

        #endregion

        #region Constructors

        public DestinationData(/*string _DestinationPIDataArchive = ""*/)
        {
            //Default constructor

            DestinationServer = ConfigurationParameters.DestinationPIDataArchive;
            
            //If there is no destination server provided, then use the argument from the configuration file
            //if (_DestinationPIDataArchive == "")
            //{
            //    DestinationServer = ConfigurationParameters.DestinationPIDataArchive;
            //}
            //else
            //{
            //    DestinationServer = PIServer.FindPIServer(_DestinationPIDataArchive);
            //}
        }//DestinationData - default

        #endregion

        #region Public Methods

        public void ProcessData(Object stateInfo)
        {
            //This method will grab data from the queue and send it to the destination PI Data Archive.

            List<List<AFValue>> valueLists = new List<List<AFValue>>();

            //Pull all values out of queue
            while (Queues.DataToBeSent.Count != 0)
            {
                List<AFValue> valueList = (List<AFValue>)Queues.DataToBeSent.Dequeue();
                valueLists.Add(valueList);
            }

            //Send the values to the destination PI Data Archive(s)
            if (valueLists.Count != 0)
            {
                SendData(valueLists);
            }

        }//ProcessData

        #endregion

        #region Private Methods

        private void SendData(List<List<AFValue>> valueLists)
        {
            //This method sends data to the destination PI Data Archive

            List<Task<OSIsoft.AF.AFErrors<AFValue>>> sendDataTasks = new List<Task<OSIsoft.AF.AFErrors<AFValue>>>();

            foreach (List<AFValue> valueList in valueLists)
            {
                //Convert each AFValue to have the proper PI point property for the destination PI Data Archive
                Parallel.ForEach(valueList, value => value.PIPoint = PIPoints.CurrentPIPoints_Dictionary[value.PIPoint.Name]);

                //Actually make the call to send the data
                var sendDataTask = DestinationServer.UpdateValuesAsync(valueList, AFUpdateOption.Insert, AFBufferOption.BufferIfPossible);
                sendDataTasks.Add(sendDataTask);
            }

            var allResults = Task.WhenAll(sendDataTasks);

            Parallel.ForEach(allResults.Result, result => LogSendDataError(result));
        }//SendData

        private void LogSendDataError(OSIsoft.AF.AFErrors<AFValue> sendDataResult)
        {
            //This method is used to log the resulting error if there is an issue sending data to destination server using UpdateValuesAsync
            if (sendDataResult != null)
            {
                //This means that there is an error; we should log it.
                foreach (var item in sendDataResult.Errors)
                {
                    //Build error message from input
                    string errorMessage = "";
                    errorMessage += "While sending the following value:\n";
                    errorMessage += item.Key.PIPoint.Name + " : ";
                    errorMessage += item.Key.Value + "\n";
                    errorMessage += "the application received the error:\n";
                    errorMessage += item.Value.HResult + " | " + item.Value.Message + " | " + item.Value.InnerException + ".";

                    //Log error
                    Logger.Log(errorMessage, System.Diagnostics.EventLogEntryType.Error);

                    //Log value for future reference
                    Logger.StoreErroredValue(item.Key.PIPoint.Name, item.Key.Value.ToString());
                }//foreach
            }//if

        }//LogSendDataError

        #endregion
    }
}
