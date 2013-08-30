using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace V8.Net
{
    // ========================================================================================================================
    // The handles section has methods to deal with creating and disposing of managed handles (which wrap native V8 handles).
    // This helps to reuse existing handles to prevent having to create new ones every time, thus greatly speeding things up.

    public unsafe partial class V8Engine
    {
        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Holds an index of all handles created for this engine instance.
        /// </summary>
        internal readonly IndexedObjectList<ObservableWeakReference<Handle>> _Handles = new IndexedObjectList<ObservableWeakReference<Handle>>(10000);

        /// <summary>
        /// When handles no longer have any references they are registered here for quick reference so the worker thread can dispose of them.
        /// </summary>
        internal readonly List<Handle> _HandlesPendingDisposal = new List<Handle>(10000);

        /// <summary>
        /// Total number of handles in the V8.NET system (for proxy use).
        /// </summary>
        public int TotalHandles { get { return _Handles.Count; } }

        /// <summary>
        /// Total number of handles in the V8.NET system that are ready to be marked weak so the native V8 engine's garbage collector can claim them.
        /// </summary>
        public int TotalHandlesPendingDisposal { get { return _HandlesPendingDisposal.Count; } }

        /// <summary>
        /// Total number of handles in the V8.NET system that are cached and ready to be reused.
        /// </summary>
        public int TotalHandlesCached
        {
            get
            {
                lock (_Handles)
                {
                    var c = 0;
                    foreach (var item in _Handles.Objects)
                    {
                        var o = item.Object;
                        if (o != null && o.IsDisposed) c++;
                    }
                    return c;
                }
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        void _Initialize_Handles()
        {
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns an existing or new handle to wrap the proxy reference.
        /// </summary>
        internal Handle _GetHandle(HandleProxy* hp)
        {
            _DoWorkStep(); // (attempt to dispose of at least one handle on each call to attempt to keep reusing handles whenever possible)

            if (hp == null) return null;

            if (hp->ManagedHandleID >= 0)
                return _Handles[hp->ManagedHandleID].Object._Reset();

            Handle handle;

            switch (hp->ValueType)
            {
                case JSValueType.Bool: handle = new Handle<bool>(this); break;
                case JSValueType.Date: handle = new Handle<DateTime>(this); break;
                case JSValueType.Int32: handle = new Handle<Int32>(this); break;
                case JSValueType.Number: handle = new Handle<double>(this); break;
                case JSValueType.String: handle = new Handle<string>(this); break;

                case JSValueType.CompilerError: handle = new Handle<string>(this); break;
                case JSValueType.ExecutionError: handle = new Handle<string>(this); break;
                case JSValueType.InternalError: handle = new Handle<string>(this); break;

                default: handle = new Handle<object>(this); break;
            }

            handle._Initialize(hp);

            lock (_Handles) // (whole list may be affected internally, so this needs to be thread protected)
            {
                hp->ManagedHandleID = handle._ID = _Handles.Add(new ObservableWeakReference<Handle>(handle));
            }

            return handle;
        }

        /// <summary>
        /// Returns a managed handle by its ID.  If the handle is ready for garbage collection, it is resurrected upon return.
        /// </summary>
        internal Handle _GetHandle(Int32 id)
        {
            Handle handle = null;
            var owr = _Handles[id];
            if (owr != null)
                if (owr.IsGCReady && !owr.Object.IsDisposed)
                    handle = owr.Reset();
                //??{
                //    handle = owr.Reset();
                //    if (handle != null && handle._NativeHandleIsWeak) handle._MakeStrongNativeHandle();
                //}
                else
                    handle = owr.Object;
            return handle;
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
