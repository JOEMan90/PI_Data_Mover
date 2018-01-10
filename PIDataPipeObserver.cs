using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OSIsoft.AF.Data;
using OSIsoft.AF.Asset;

namespace PI_Data_Mover
{
    internal class PIDataPipeObserver : IObserver<AFDataPipeEvent>
    {

        private string _Name;

        public PIDataPipeObserver(string name)
        {
            _Name = name;
        }

        public void OnCompleted()
        {
            //
        }//OnCompleted

        public void OnError(Exception e)
        {
            Logger.Log("The following exception occurred while retrieving events from the data pipe: " + e.HResult + " - " + e.Message + " - " + e.InnerException, System.Diagnostics.EventLogEntryType.Error);
        }//OnError

        public void OnNext(AFDataPipeEvent valueEvent)
        {
            List<AFValue> valueList = new List<AFValue>();
            valueList.Add(valueEvent.Value);
            Queues.DataToBeSent.Enqueue(valueList);
        }//OnNext
    }
}
