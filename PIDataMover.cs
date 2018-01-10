using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;

namespace PI_Data_Mover
{

    public enum DataCollectionModes
    {
        Real,
        Historical
    }

    public partial class PIDataMover : ServiceBase
    {

        private enum TimerTypes
        {
            SourceData,
            DestinationData
        }

        private DestinationData destinationData;
        private SourceData snapshotData;
        private SourceData historicalData;

        public PIDataMover()
        {
            InitializeComponent();
            Logger.Initialize();
        }

        protected override void OnStart(string[] args)
        {
            //PIDMEventLog.WriteEntry("PI Data Mover is starting up.", EventLogEntryType.Information);
            Logger.Log("PI Data Mover is starting up.", EventLogEntryType.Information);

            #region Configuration Parameters

            ConfigurationParameters.Initialize();
            ConfigurationParameters.ErrorTypes configLoadResult = ConfigurationParameters.LoadConfigurationParameters();

            if (configLoadResult != ConfigurationParameters.ErrorTypes.None)
            {
                //PIDMEventLog.WriteEntry("An error occurred while loading the configuration parameters from the file. The error is: \n\n" + ConfigurationParameters.Errors[configLoadResult] + "\n\nThe application will now shut down.", EventLogEntryType.Error);
                Logger.Log("An error occurred while loading the configuration parameters from the file. The error is: \n\n" + ConfigurationParameters.Errors[configLoadResult] + "\n\nThe application will now shut down.", EventLogEntryType.Error);
                throw new Exception();
                //Stop the service, somehow
            }
            else
            {
                //PIDMEventLog.WriteEntry("Configuraiton parameters successfully loaded.", EventLogEntryType.Information);
                Logger.Log("Configuration parameters successfully loaded.", EventLogEntryType.Information);
            }

            #endregion

            Queues.Initialize();

            //Init destination data and start the timer
            destinationData = new DestinationData();
            CreateTimer(TimerTypes.DestinationData);


            #region Start Data Collection

            //Init specific source data and begin relevant collection
            if (ConfigurationParameters.DataCollectionMode == DataCollectionModes.Real)
            {
                snapshotData = new SourceData(DataCollectionModes.Real);
                CreateTimer(TimerTypes.SourceData);
            }
            else if (ConfigurationParameters.DataCollectionMode == DataCollectionModes.Historical)
            {
                historicalData = new SourceData(DataCollectionModes.Historical);
                historicalData.StartHistoricalDataCollection();
            }

            Logger.Log("The application is fully started. The processes for both sending and receiving data have begun.", EventLogEntryType.Information);

            #endregion
        }//OnStart

        protected override void OnStop()
        {
            Logger.Log("PI Data Mover is shutting down.", EventLogEntryType.Information);

            if (ConfigurationParameters.DataCollectionMode == DataCollectionModes.Real)
            {
                snapshotData.StopRealTimeData();
            }
        }

        private void OnDestinationDataTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            //This method controls what happens when the destinationDataTimer elapses (Aka, we need to send data to the destination PI DA if it exists in the queue)
            ThreadPool.QueueUserWorkItem(new WaitCallback(destinationData.ProcessData));


        }//OnDestinationDataTimer

        private void OnSourceDataTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            //This method controls what happens when the sourceDataTimer elapses (Aka, we need to tell the observer to pull events from the data pipe and queue them)
            ThreadPool.QueueUserWorkItem(new WaitCallback(snapshotData.GetRealTimeData));

        }//OnDestinationDataTimer

        private void CreateTimer(TimerTypes timerType)
        {
            //Creates a timer of the specified type

            System.Timers.Timer timer = new System.Timers.Timer();
            
            switch(timerType)
            {
                case TimerTypes.DestinationData:
                    timer.Interval = ConfigurationParameters.DestinationSendDataRate;
                    timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnDestinationDataTimer);
                    break;

                case TimerTypes.SourceData:
                    timer.Interval = ConfigurationParameters.SourceReadDataRate;
                    timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnSourceDataTimer);
                    break;
            }

            timer.Start();

        }//CreateTimer
    }
}
