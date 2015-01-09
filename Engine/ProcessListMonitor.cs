using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CDFM.Engine
{
    internal class ProcessList
    {
        #region Public Fields

        public uint Id = 0;
        public string ProcessName = string.Empty;
        public DateTime StartTime = DateTime.MinValue;
        public DateTime StopTime = DateTime.MinValue;

        #endregion Public Fields
    }

    internal class ProcessListMonitor
    {
        #region Private Fields

        private const int MAX_PROCESS_LIST_COUNT = 100000;
        private const int MAX_TOMBSTONE_MINUTES = 10;
        private const int THREAD_SLEEP_INTERVAL = 200;
        private static ProcessListMonitor _Instance;
        private bool _enabled;
        private int _interruptCounter;
        private List<ProcessList> _processList = new List<ProcessList>();
        private Thread _processListThread;
        private ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        #endregion Private Fields

        #region Private Constructors

        /// <summary>
        /// private ctor
        /// </summary>
        private ProcessListMonitor()
        {
            PopulateProcessList();
        }

        #endregion Private Constructors

        #region Public Properties

        /// <summary>
        /// Returns singleton instance
        /// </summary>
        public static ProcessListMonitor Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new ProcessListMonitor();
                }

                return _Instance;
            }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Disables WMI Process Creation and Deletion Callbacks
        /// </summary>
        public void Disable()
        {
            if (_enabled)
            {
                DisableProcessListThread();
                CDFMonitor.LogOutputHandler(string.Format("ProcessListMonitor: disabled: interrupt counter:{0} list size:{1}", _interruptCounter, _processList.Count));
                _enabled = false;
            }

            _processList.Clear();
        }

        /// <summary>
        /// Disposes WMI handles
        /// </summary>
        public void Dispose()
        {
            if (_enabled)
            {
                this.Disable();
            }
        }

        /// <summary>
        /// Enables WMI Process Creation and Deletion Callbacks
        /// </summary>
        public void Enable()
        {
            if (!_enabled)
            {
                _enabled = true;
                PopulateProcessList();
                EnableProcessListThread();
            }
        }

        /// <summary>
        /// returns formatted string of processname(id). example explorer.exe(123)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public string GetProcessNameFromId(uint id)
        {
            cacheLock.EnterReadLock();
            try
            {
                if ((int)id > 0 && _processList.Exists(p => p.Id == id))
                {
                    return string.Format("{0}.exe({1})", _processList.First(p => p.Id == id).ProcessName, id);
                }
                else
                {
                    // wake up thread to get new list for next time
                    if (_processListThread.ThreadState == System.Threading.ThreadState.WaitSleepJoin)
                    {
                        _processListThread.Interrupt();
                        _interruptCounter++;
                    }

                    return id.ToString();
                }
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler(
                string.Format("DEBUG:Exception:GetProcessNameFromId:{0}", e));

                return id.ToString();
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// adds process to _processList
        /// </summary>
        /// <param name="process"></param>
        private bool AddProcessToList(Process process)
        {
            cacheLock.EnterWriteLock();
            try
            {
                if (_processList.Exists(p => p.Id == process.Id))
                {
                    return false;
                }

                // special perms for starttime?
                DateTime startTime = DateTime.Now;

                _processList.Add(new ProcessList()
                {
                    Id = (uint)process.Id,
                    ProcessName = process.ProcessName, // + ".exe",
                    StartTime = startTime
                });
                return true;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler(
               string.Format("DEBUG:Exception:AddProcessToList:{0}", e));
                return false;
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Disables ProcessList Thread
        /// </summary>
        private void DisableProcessListThread()
        {
            if (_processListThread != null)
            {
                _enabled = false;
                _processListThread.Interrupt();
            }
        }

        /// <summary>
        /// Enables the ProcessList Thread
        /// </summary>
        private void EnableProcessListThread()
        {
            _processListThread = new Thread(new ThreadStart(ProcessListThreadProc));
            _processListThread.Name = "_processListMonitor";
            _processListThread.Start();
        }

        /// <summary>
        /// Manages the ProcessList
        /// </summary>
        /// <param name="processID"></param>
        private void ManageProcessList()
        {
            // clean up any tombstoned records
            cacheLock.EnterUpgradeableReadLock();
            try
            {
                foreach (ProcessList process in new List<ProcessList>(_processList))
                {
                    if (process.StopTime != DateTime.MinValue && DateTime.Now.Subtract(process.StopTime).Minutes > MAX_TOMBSTONE_MINUTES)
                    {
                        cacheLock.EnterWriteLock();
                        try
                        {
                            _processList.Remove(process);
                            CDFMonitor.LogOutputHandler(
                                string.Format("ManageProcessList: removing process:{0}.exe({1})", process.ProcessName, process.Id), JobOutputType.Etw);
                        }
                        catch (Exception ex1)
                        {
                            CDFMonitor.LogOutputHandler("DEBUG:ProcessListMonitor:ManageProcessList:remove stale Exception:{0}" + ex1.ToString());
                        }
                        finally
                        {
                            cacheLock.ExitWriteLock();
                        }
                    }
                }

                // make sure list doesnt get too big by cleaning cache of stale entries
                if (_processList.Count > MAX_PROCESS_LIST_COUNT)
                {
                    cacheLock.EnterWriteLock();
                    try
                    {
                        _processList.RemoveAll(p => p.StopTime != DateTime.MinValue);
                    }
                    catch (Exception ex2)
                    {
                        CDFMonitor.LogOutputHandler("DEBUG:ProcessListMonitor:ManageProcessList:Over max count Exception:{0}" + ex2.ToString());
                    }
                    finally
                    {
                        cacheLock.ExitWriteLock();
                    }
                }
            }
            catch (Exception ex)
            {
                CDFMonitor.LogOutputHandler("DEBUG:ProcessListMonitor:ManageProcessList:Exception:{0}" + ex.ToString());
            }
            finally
            {
                cacheLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Populates Process Dictionary with Process ID and Process Name
        /// currently disabled
        /// </summary>
        private void PopulateProcessList()
        {
            cacheLock.EnterUpgradeableReadLock();
            try
            {
                _processList.Clear();

                foreach (Process process in Process.GetProcesses())
                {
                    if (process.Id < 1)
                    {
                        continue;
                    }

                    AddProcessToList(process);
                }
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler(
                string.Format("DEBUG:Exception:PopulateProcessList:{0}", e));
            }
            finally
            {
                cacheLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Thread Main Loop to Monitor Process List
        /// </summary>
        private void ProcessListThreadProc()
        {
            ProcessListMonitor pI = ProcessListMonitor.Instance;
            ProcessList tempProcess;
            List<ProcessList> processList;
            List<Process> newList;
            int sleep = THREAD_SLEEP_INTERVAL;

            while (pI._enabled)
            {
                cacheLock.EnterUpgradeableReadLock();
                try
                {
                    if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(sleep) | !pI._enabled)
                    {
                        return;
                    }

                    sleep = THREAD_SLEEP_INTERVAL;
                    processList = new List<ProcessList>(pI._processList);
                    newList = Process.GetProcesses().ToList();

                    // look for new processes
                    foreach (Process process in newList)
                    {
                        if (process.Id < 1)
                        {
                            continue;
                        }

                        tempProcess = processList.FirstOrDefault(p => p.Id == process.Id);

                        if (tempProcess != null && tempProcess.ProcessName == process.ProcessName)
                        {
                            continue;
                        }
                        else if (tempProcess != null && tempProcess.ProcessName != process.ProcessName)
                        {
                            cacheLock.EnterWriteLock();

                            try
                            {
                                pI._processList.Remove(tempProcess);
                            }
                            catch { }
                            finally
                            {
                                cacheLock.ExitWriteLock();
                            }
                        }

                        AddProcessToList(process);

                        CDFMonitor.LogOutputHandler(
                        string.Format("CDFMONITOR:Process Started:{0}.exe({1}) session:{2}",
                            process.ProcessName,
                            process.Id,
                            process.SessionId),
                            JobOutputType.Etw);
                    }

                    // look for terminated processes
                    foreach (ProcessList proc in processList)
                    {
                        if (!newList.Exists(p => p.Id == proc.Id))
                        {
                            tempProcess = pI._processList.FirstOrDefault(p => p.Id == proc.Id);
                            if (tempProcess.StopTime == DateTime.MinValue)
                            {
                                tempProcess.StopTime = DateTime.Now;

                                CDFMonitor.LogOutputHandler(
                                    string.Format("CDFMONITOR:Process Terminated:{0}({1})", proc.ProcessName, proc.Id), JobOutputType.Etw);
                                pI.ManageProcessList();
                            }
                        }
                    }
                }
                catch (ThreadInterruptedException)
                {
                    // run immediately with no wait
                    sleep = 0;
                    continue;
                }
                catch (Exception e)
                {
                    CDFMonitor.LogOutputHandler("ProcessListMonitor:ProcessList:Exception:{0}" + e.ToString());
                    continue;
                }
                finally
                {
                    cacheLock.ExitUpgradeableReadLock();
                }
            }
        }

        #endregion Private Methods
    }
}