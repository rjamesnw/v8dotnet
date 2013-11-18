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

    /// <summary>
    /// Applied to V8 related objects to take control of the finalization process from the GC so it can be better coordinated with the native V8 engine.
    /// </summary>
    internal interface IFinalizable
    {
        void DoFinalize(); // (proceed to finalize the object)
        bool CanFinalize { get; set; } // (if this is true, the GC, when triggered again, can finally collect the instance)
    }

    public unsafe partial class V8Engine
    {
        // --------------------------------------------------------------------------------------------------------------------

        internal Thread _Worker;

        /// <summary>
        /// When 'V8NativeObject' objects are no longer in use, they are registered here for quick reference so the worker thread can dispose of them.
        /// </summary>
        internal readonly List<int> _WeakObjects = new List<int>(100);
        int _WeakObjects_Index = -1;

        internal readonly List<IFinalizable> _ObjectsToFinalize = new List<IFinalizable>(100);
        int _ObjectsToFinalize_Index = -1;

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
                //??if (GlobalObject.AsInternalHandle._HandleProxy->Disposed > 0)
                //    System.Diagnostics.Debugger.Break();

                if (_PauseWorker == 1) _PauseWorker = 2;
                else if (_PauseWorker == -1) break;
                else
                {
                    workPending = _WeakObjects.Count > 0 || _ObjectsToFinalize.Count > 0;

                    while (workPending && _PauseWorker == 0)
                    {
                        workPending = _DoWorkStep();
                        DoIdleNotification(1);
                        Thread.Sleep(0);
                    }
                }
                Thread.Sleep(100);
                DoIdleNotification(100);
            }

            _PauseWorker = -2;
        }

        /// <summary>
        /// Does one step in the work process (mostly garbage collection for freeing up unused handles).
        /// True is returned if more work is pending, and false otherwise.
        /// </summary>
        bool _DoWorkStep()
        {
            // ... do one weak object ...

            int objID = -1;

            lock (_WeakObjects)
            {
                if (_WeakObjects_Index < 0)
                    _WeakObjects_Index = _WeakObjects.Count - 1;

                if (_WeakObjects_Index >= 0)
                {
                    objID = _WeakObjects[_WeakObjects_Index];

                    _WeakObjects.RemoveAt(_WeakObjects_Index);

                    _WeakObjects_Index--;
                }
            }

            if (objID >= 0)
            {
                V8NativeObject obj;
                using (_ObjectsLocker.ReadLock())
                {
                    obj = _Objects[objID].Object;
                }
                obj._MakeWeak(); // (don't call this while '_Objects' is locked, because the main thread may be executing script that also may need a lock, but this call may also be blocked by a native V8 mutex)
            }

            // ... and do one object ready to be finalized ...

            IFinalizable objectToFinalize = null;

            lock (_ObjectsToFinalize)
            {
                if (_ObjectsToFinalize_Index < 0)
                    _ObjectsToFinalize_Index = _ObjectsToFinalize.Count - 1;

                if (_ObjectsToFinalize_Index >= 0)
                {
                    objectToFinalize = _ObjectsToFinalize[_ObjectsToFinalize_Index];

                    _ObjectsToFinalize.RemoveAt(_ObjectsToFinalize_Index);

                    _ObjectsToFinalize_Index--;
                }
            }

            if (objectToFinalize != null)
                objectToFinalize.DoFinalize();

            return _WeakObjects_Index >= 0 || _ObjectsToFinalize_Index >= 0;
        }

        /// <summary>
        /// Pauses the worker thread (usually for debug purposes). (Note: The worker thread manages object GC along with the native V8 GC.)
        /// </summary>
        public void PauseWorker()
        {
            if (_Worker.IsAlive)
            {
                _PauseWorker = 1;
                while (_PauseWorker == 1 && _Worker.IsAlive) { }
            }
        }

        /// <summary>
        /// Terminates the worker thread, without a 3 second timeout to be sure.
        /// This is called when the engine is shutting down. (Note: The worker thread manages object GC along with the native V8 GC.)
        /// </summary>
        internal void _TerminateWorker()
        {
            if (_Worker.IsAlive)
            {
                _PauseWorker = -1;
                var timeoutCountdown = 3000;
                while (_PauseWorker == -1 && _Worker.IsAlive)
                    if (timeoutCountdown-- > 0)
                        Thread.Sleep(1);
                    else
                    {
                        _Worker.Abort();
                        break;
                    }
            }
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
