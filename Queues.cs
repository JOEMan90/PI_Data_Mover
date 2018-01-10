using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PI_Data_Mover
{
    static class Queues
    {
        //This class holds the various data queues used by the application to pass data between different threads and any related methods
        #region Properties

        public static Queue<object> DataToBeSent; //Queues data which needs to be sent to Destination PI Data Archive
        public static Queue<object> HistoricalRecoveryRange; //Queues time ranges for historical data recovery from source PI Data Archive

        #endregion

        #region Methods

        public static Boolean Initialize()
        {
            //This methods initializes the static class (Use when starting the application)

            DataToBeSent = new Queue<object>();
            HistoricalRecoveryRange = new Queue<object>();

            return true;
        }//Initialize

        //public static Boolean Destory()
        //{
        //    //This methods destroys all the data queues (Use when closing application)



        //    return true;
        //}

        #endregion


    }//DataQueues

    public class Queue<T>
    {
        //Queue-related methods and overrides
        //https://stackoverflow.com/a/1360545/6750012

        /// <summary>Used as a lock target to ensure thread safety.</summary>
        //private readonly Locker _Locker = new Locker();
        private readonly object _Locker = new object();

        private readonly System.Collections.Generic.Queue<T> _Queue = new System.Collections.Generic.Queue<T>();

        /// <summary></summary>
        public void Enqueue(T item)
        {
            lock (_Locker)
            {
                _Queue.Enqueue(item);
            }
        }//Enqueue

        /// <summary>Enqueues a collection of items into this queue.</summary>
        public virtual void EnqueueRange(IEnumerable<T> items)
        {
            lock (_Locker)
            {
                if (items == null)
                {
                    return;
                }

                foreach (T item in items)
                {
                    _Queue.Enqueue(item);
                }
            }
        }//EnqueueRange

        /// <summary></summary>
        public T Dequeue()
        {
            lock (_Locker)
            {
                return _Queue.Dequeue();
            }
        }//Dequeue

        /// <summary></summary>
        public void Clear()
        {
            lock (_Locker)
            {
                _Queue.Clear();
            }
        }//Clear

        /// <summary></summary>
        public Int32 Count
        {
            get
            {
                lock (_Locker)
                {
                    return _Queue.Count;
                }
            }
        }//Count

        /// <summary></summary>
        public Boolean TryDequeue(out T item)
        {
            lock (_Locker)
            {
                if (_Queue.Count > 0)
                {
                    item = _Queue.Dequeue();
                    return true;
                }
                else
                {
                    item = default(T);
                    return false;
                }
            }
        }//TryDequeue
    }//Queue
}//namespace PI_Data_Mover
