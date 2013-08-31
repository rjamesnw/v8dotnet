using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

#if !(V1_1 || V2 || V3 || V3_5)
using System.Dynamic;
#endif

namespace V8.Net
{
    // ========================================================================================================================
    // The worker section implements a bridge GC system, which marks objects weak when 'V8NativeObject' instances no longer have any references.  Such objects
    // are called "weak" objects, and the worker calls the native side to also mark the native object as weak.  Once the V8 GC calls back, the managed object
    // will then complete it's finalization process.

    public unsafe partial class V8Engine
    {
        // --------------------------------------------------------------------------------------------------------------------

        internal Thread _Worker;

        /// <summary>
        /// When 'V8NativeObject' objects are no longer in use, they are registered here for quick reference so the worker thread can dispose of them.
        /// </summary>
        internal readonly List<int> _WeakObjects = new List<int>(100);

        // --------------------------------------------------------------------------------------------------------------------

        void _Initialize_Worker()
        {
            _Worker = new Thread(_WorkerLoop) { IsBackground = true }; // (note: must set 'IsBackground=true', else the app will hang on exit)
            _Worker.Priority = ThreadPriority.Lowest;
            _Worker.Start();
        }

        // --------------------------------------------------------------------------------------------------------------------

        volatile int _PauseWorker;

        void _WorkerLoop()
        {
            bool workPending;

            while (true)
            {
                if (_PauseWorker == 1) _PauseWorker = 2;
                else
                {
                    workPending = _WeakObjects.Count > 0;

                    while (workPending && _PauseWorker == 0)
                    {
                        WithIsolateScope = () =>
                        {
                            workPending = _DoWorkStep();
                            DoIdleNotification(1);
                        };
                        Thread.Sleep(0);
                    }
                }
                Thread.Sleep(100);
                WithIsolateScope = () =>
                {
                    DoIdleNotification(100);
                };
            }
        }

        int _Worker_Index = -1;

        /// <summary>
        /// Does one step in the work process (mostly garbage collection for freeing up unused handles).
        /// True is returned if more work is pending, and false otherwise.
        /// </summary>
        bool _DoWorkStep()
        {
            int objID = -1;

            lock (_WeakObjects)
            {
                if (_Worker_Index < 0)
                    _Worker_Index = _WeakObjects.Count - 1;

                if (_Worker_Index >= 0)
                {
                    objID = _WeakObjects[_Worker_Index];

                    _WeakObjects.RemoveAt(_Worker_Index);

                    _Worker_Index--;
                }
            }

            if (objID >= 0)
                lock (_Objects)
                {
                    var obj = _Objects[objID].Object;
                    if (obj != null)
                        obj._MakeWeak();
                }

            return _Worker_Index >= 0;
        }

        /// <summary>
        /// Pauses the worker thread (usually for debug purposes). The worker thread clears out orphaned object entries (mainly).
        /// </summary>
        public void PauseWorker()
        {
            _PauseWorker = 1;
            while (_PauseWorker == 1) { }
        }

        /// <summary>
        /// Unpauses the worker thread (see <see cref="PauseWorker"/>).
        /// </summary>
        public void ResumeWorker()
        {
            _PauseWorker = 0;
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
