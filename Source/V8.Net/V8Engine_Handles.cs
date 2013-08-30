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
    // The handles section has methods to deal with creating and disposing of managed handles (which wrap native V8 handles).
    // This helps to reuse existing handles to prevent having to create new ones every time, thus greatly speeding things up.

    public unsafe partial class V8Engine
    {
        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Holds an index of all handle proxies created for this engine instance.
        /// </summary>
        internal HandleProxy*[] _HandleProxies = new HandleProxy*[1000];

        /// <summary>
        /// Total number of handle proxy references in the V8.NET system (for proxy use).
        /// </summary>
        public int TotalHandles
        {
            get
            {
                lock (_HandleProxies)
                {
                    var c = 0;
                    foreach (var item in _HandleProxies)
                        if (item != null) c++;
                    return c;
                }
            }
        }

        /// <summary>
        /// Total number of handle proxy references in the V8.NET system that are ready to be disposed by the V8.NET garbage collector (the worker thread).
        /// </summary>
        public int TotalHandlesPendingDisposal
        {
            get
            {
                lock (_HandleProxies)
                {
                    var c = 0;
                    foreach (var item in _HandleProxies)
                        if (item != null && item->IsDisposeReady) c++;
                    return c;
                }
            }
        }


        /// <summary>
        /// Total number of handles in the V8.NET system that are cached and ready to be reused.
        /// </summary>
        public int TotalHandlesCached
        {
            get
            {
                lock (_HandleProxies)
                {
                    var c = 0;
                    foreach (var item in _HandleProxies)
                        if (item != null && item->IsDisposed) c++;
                    return c;
                }
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        void _Initialize_Handles()
        {
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
