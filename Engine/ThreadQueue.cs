// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="ThreadQueue.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Engine
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// Class ThreadQueue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ThreadQueue<T>
    {
        #region Public Fields

        public static List<ThreadQueue<T>> ThreadQueues = new List<ThreadQueue<T>>();

        #endregion Public Fields

        #region Private Fields

        private const int MAX_QUEUE_SIZE = 100000;
        private const int QUEUE_WAIT_TIME = 250;
        private readonly Action<T> _action;
        private readonly string _name = string.Empty;
        private readonly Queue<T> _queue = new Queue<T>(MAX_QUEUE_SIZE); //(MAX_QUEUE_SIZE00);
        private volatile bool _clearQueue;
        private Queue<T> _copyQueue = new Queue<T>(MAX_QUEUE_SIZE); //(MAX_QUEUE_SIZE00);
        private volatile bool _disableQueue;
        private volatile bool _isActive;
        private int _maxQueueLength = MAX_QUEUE_SIZE;
        private Int64 _maxQueueMissedEvents;
        private Int64 _processedCounter;
        private Int64 _queuedCounter;
        private volatile bool _shutdown;
        private Thread _thread;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadQueue&lt;T&gt;" /> class.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="name">The name.</param>
        public ThreadQueue(Action<T> action, string name)
        {
            _action = action;
            _name = name;
            ThreadQueues.Add(this);
            StartThread();
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// Gets a value indicating whether this instance is active.
        /// </summary>
        /// <value><c>true</c> if this instance is active; otherwise, /c>.</value>
        public bool IsActive
        {
            get
            {
                bool retval = (_isActive | (QueueLength() > 0));
                Debug.Print("IsActive:" + retval.ToString());
                return retval;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is enabled.
        /// </summary>
        /// <value><c>true</c> if this instance is enabled; otherwise, /c>.</value>
        public int MaxQueueLength
        {
            get { return (_maxQueueLength); }
            set
            {
                // If maxqueuelength 0 then set to max
                if (value == 0)
                {
                    _maxQueueLength = int.MaxValue;
                }
                else
                {
                    _maxQueueLength = value;
                }
            }
        }

        /// <summary>
        /// Gets the max Queue counter.
        /// </summary>
        /// <value>The processed counter.</value>
        public Int64 MaxQueueMissedEvents
        {
            get { return _maxQueueMissedEvents; }
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets the processed counter.
        /// </summary>
        /// <value>The processed counter.</value>
        public Int64 ProcessedCounter
        {
            get { return _processedCounter; }
        }

        /// <summary>
        /// Gets the queued counter.
        /// </summary>
        /// <value>The queued counter.</value>
        public Int64 QueuedCounter
        {
            get { return _queuedCounter; }
        }

        /// <summary>
        /// If enabled waits for queue to come available. disabled by default.
        /// </summary>
        public bool WaitForQueue { get; set; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Clears the queue.
        /// </summary>
        public void ClearQueue()
        {
            lock (_queue)
            {
                _clearQueue = true;
                _queue.Clear();
                Monitor.Pulse(_queue);
                _queuedCounter = 0;
                _processedCounter = 0;
            }
        }

        /// <summary>
        /// Disables the queue.
        /// </summary>
        public void DisableQueue()
        {
            lock (_queue)
            {
                _disableQueue = true;
                Monitor.Pulse(_queue);
            }
        }

        /// <summary>
        /// Enables the queue.
        /// </summary>
        public void EnableQueue()
        {
            lock (_queue)
            {
                _clearQueue = false;
                _disableQueue = false;
                Monitor.Pulse(_queue);
            }
        }

        /// <summary>
        /// Determines whether this instance is alive.
        /// </summary>
        /// <returns><c>true</c> if this instance is alive; otherwise, /c>.</returns>
        public bool IsAlive()
        {
            if (!_thread.IsAlive && !_disableQueue)
            {
                StartThread();
            }
            return (_thread.IsAlive);
        }

        /// <summary>
        /// Queues this instance.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool Queue()
        {
            return (Queue(default(T)));
        }

        /// <summary>
        /// Queues the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool Queue(T data)
        {
            lock (_queue)
            {
                while (WaitForQueue && (_queue.Count + _copyQueue.Count >= _maxQueueLength))
                {
                    if (_shutdown | _disableQueue | _clearQueue)
                    {
                        return false;
                    }

                    Monitor.Wait(_queue, QUEUE_WAIT_TIME);
                }

                if (_queue.Count + _copyQueue.Count < _maxQueueLength || WaitForQueue)
                {
                    _queue.Enqueue(data);
                    Monitor.Pulse(_queue);
                    _queuedCounter++;
                    return true;
                }
                else
                {
                    Debug.Print("Queue:Fail: Max queue limit reached. skipping" + _thread.Name);
                    _maxQueueMissedEvents++;
                    return false;
                }
            }
        }

        /// <summary>
        /// Queues the length.
        /// </summary>
        /// <returns>System.Int32.</returns>
        public int QueueLength()
        {
            //return (_queue.Count);
            return _queue.Count + _copyQueue.Count;
        }

        /// <summary>
        /// Shutdowns this instance.
        /// </summary>
        public void Shutdown()
        {
            _shutdown = true;
            _disableQueue = false;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Starts the thread.
        /// </summary>
        private void StartThread()
        {
            _thread = new Thread(ThreadProc);
            _thread.Name = _name;
            _thread.Start();
        }

        /// <summary>
        /// Threads the proc.
        /// </summary>
        private void ThreadProc()
        {
            while (true)
            {
                Monitor.Enter(_queue);
                if (_queue.Count == 0)
                {
                    // so threads will clean up
                    Monitor.Wait(_queue, QUEUE_WAIT_TIME);
                }

                if (_shutdown)
                {
                    Debug.Print("Queue:exiting");
                    Monitor.Exit(_queue);
                    return;
                }
                if (_clearQueue)
                {
                    _clearQueue = false;
                    Monitor.Exit(_queue);
                    continue;
                }
                if (_disableQueue)
                {
                    Monitor.Exit(_queue);
                    if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(QUEUE_WAIT_TIME))
                    {
                        _shutdown = true;
                        return;
                    }

                    continue;
                }

                _isActive = true;

                _copyQueue = new Queue<T>(_queue);
                _queue.Clear();

                Monitor.Exit(_queue);

                while (_copyQueue.Count > 0)
                {
                    while (_disableQueue)
                    {
                        _isActive = false;
                        if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(QUEUE_WAIT_TIME))
                        {
                            _shutdown = true;
                            return;
                        }

                        Debug.Print("Queue:disabled:" + Name);
                    }

                    if (_shutdown)
                    {
                        Debug.Print("Queue:exiting:" + Name);
                        return;
                    }

                    if (_clearQueue)
                    {
                        _clearQueue = false;
                        break;
                    }

                    // call callback
                    _action(_copyQueue.Dequeue());

                    _processedCounter++;
                }

                _isActive = false;
            }
        }

        #endregion Private Methods
    }
}