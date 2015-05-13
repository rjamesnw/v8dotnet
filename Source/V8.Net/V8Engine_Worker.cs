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
    /// Applied to V8 related objects to better coordinated disposing native resources on the native V8 engine side via the worker thread.
    /// <para>'IV8Disposable' objects are added to an internal "disposal" queue when they are finalized.  This is done because
    /// some tasks, such as disposing handles, cannot be performed in a finalizer method.  Because of this, the event is
    /// deferred to a worker process queue to continue processing as soon as possible.</para>
    /// </summary>
    public interface IV8Disposable : IDisposable
    {
        /// <summary>
        /// The V8.Net engine that his disposable object belongs to.
        /// </summary>
        V8Engine Engine { get; }

        /// <summary>
        /// If this is true, the object is ok to be disposed.
        /// </summary>
        bool CanDispose { get; }
    }

    // ========================================================================================================================

    public static class V8EngineWorkerExtensions
    {
        // --------------------------------------------------------------------------------------------------------------------

        public static void Finalizing<T>(this T v8Object)
            where T : IV8Disposable
        {
            if (v8Object != null)
            {
                var engine = v8Object.Engine; // (get the engine this handle/object belongs to [note: this will be null if an engine gets disposed with objects still floating in the GC])

                // ... get handle and object details ...
                if (engine != null)
                    if (v8Object is Handle || v8Object is InternalHandle)
                    {
                        var h = ((IHandleBased)v8Object).AsInternalHandle;
                        if (!h.IsEmpty && !h.IsDisposed)
                            lock (engine._DisposalQueue)
                            {
                                h.IsBeingDisposed = true;
                                engine._DisposalQueue.Enqueue(h.AsInternalHandle);
                            }
                    }
                    else
                    {
                        // ... queue 
                        lock (engine._DisposalQueue)
                        {
                            engine._DisposalQueue.Enqueue(v8Object); // (queue first, so when 'weakRef.DoFinalize()' calls 'GC.ReRegisterForFinalize()', there's no chance of the finalizer kicking in, just in case)

                            // (note: lock on '_DisposalQueue' is kept so worker doesn't pull something before being finished here)

                            if (v8Object is V8NativeObject)
                            {
                                var v8Obj = v8Object as V8NativeObject;

                                if (v8Obj.ID >= 0)
                                {
                                    var weakRef = engine._GetObjectWeakReference(v8Obj.ID);
                                    if (weakRef != null)
                                        weakRef.DoFinalize(v8Obj); // (the object will be prevented from finalizing; it will be resurrected by being adding it to a strong reference, and will remain abandoned)
                                    // (WARNING: 'lock (Engine._Objects){}' can conflict with the main thread if care is not taken)
                                    // (Past issue: When '{Engine}.GetObjects()' was called, and an '_Objects[?].Object' is accessed, a deadlock can occur since the finalizer has nulled all the weak references)
                                }
                            }
                        }
                    }
            }
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    public unsafe partial class V8Engine
    {
        // --------------------------------------------------------------------------------------------------------------------

        internal Thread _Worker;

        /// <summary>
        /// When 'V8NativeObject' objects are no longer in use, they are registered here for quick reference so the worker thread can dispose of them.  
        /// </summary>
        internal readonly SortedSet<int> _WeakObjects = new SortedSet<int>(); // (note: a 'SortedSet' is used to prevent duplicates getting added)

        /// <summary>
        /// Holds a list of all objects that the GC attempted to finalize. The managed side no longer has ownership of anything
        /// in this list.  These objects/handles need to be disposed in sync with the native side.
        /// </summary>
        internal Queue<IV8Disposable> _DisposalQueue = new Queue<IV8Disposable>(100);

        /// <summary>
        /// Abandoned objects are objects that failed to dispose.  They may be retried later at a slower rate.
        /// One such example is 'ObjectTemplate', where though all CLR references are gone, may still have V8 side references.
        /// In such case, the 'ObjectTemplate' becomes effectively "abandoned" on the managed side, but still must remain.
        /// Because there's no V8 GC for this, the worker will have to check these at a slower pace from time to time.
        /// </summary>
        internal readonly LinkedList<IV8Disposable> _AbandondObjects = new LinkedList<IV8Disposable>(); // TODO: Actually make the worker look into these on a slower frequency. ;)

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
            bool workPending = false;

            while (true)
            {
                //??if (GlobalObject.AsInternalHandle._HandleProxy->Disposed > 0)
                //    System.Diagnostics.Debugger.Break();

                if (_PauseWorker == 1) _PauseWorker = 2;
                else if (_PauseWorker == -1) break;
                else
                {
                    workPending = _WeakObjects.Count > 0 || _DisposalQueue.Count > 0;

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

            //?int objID = -1;

            //?lock (_WeakObjects)
            //{
            //    if (_WeakObjects.Count > 0)
            //    {
            //        objID = _WeakObjects.Min();
            //        _WeakObjects.Remove(objID);
            //    }
            //}

            //?if (objID >= 0)
            //{
            //    IV8NativeObject obj;
            //    using (_ObjectsLocker.ReadLock())
            //    {
            //        obj = _Objects[objID].Object;
            //    }
            //}

            // ... and do one handle ready to be disposed ...

            IV8Disposable disposable;

            lock (_DisposalQueue) // TODO: consider using read/write locks in more places
            {
                disposable = _DisposalQueue.Dequeue();
            }

            if (disposable != null)
                if (disposable.CanDispose)
                    disposable.Dispose();
                else
                {
                    // ... cannot dispose this yet; place it in the "abandoned" queue for later ...

                    lock (_AbandondObjects)
                    {
                        _AbandondObjects.AddLast(disposable);
                    }

                    // ... if this is a manage object, it is now abandoned, so make the native handle weak ...

                    if (disposable is V8NativeObject)
                    {
                        var v8Obj = (V8NativeObject)disposable;
                        if (!v8Obj.Handle.IsEmpty && v8Obj.Handle.__HandleProxy->Disposed == 1)
                        {
                            V8NetProxy.MakeWeakHandle(v8Obj.AsInternalHandle); // ('Disposed' will be 2 after this)
                            /* Once the native GC attempts to collect the underlying native object, then '_OnNativeGCRequested()' will get 
                             * called to finalize the disposal of the managed object.
                             * Note 1: Once the native V8 engine agrees, this object will be removed due to a global V8 GC callback 
                             *         registered when the V8.Net wrapper engine was created.
                             * Note 2: Don't call this while '_Objects' is locked, because the main thread may be executing script that may 
                             *         also need a lock, and this call may become blocked by a native side mutex.
                             */
                        }
                    }
                }

            return _WeakObjects.Count + _DisposalQueue.Count > 0;
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
