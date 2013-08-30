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
    // Just some basic environment compatibility tests.

    public unsafe partial class V8Engine
    {
        // --------------------------------------------------------------------------------------------------------------------

        void _ThrowMarshalTestError(string structName, string fieldName, int offset)
        {
            throw new ContextMarshalException(string.Format("The field value for '{0}->{1}' (offset {2}) on the native side proxy struct does not align with the managed side struct.",
                structName, fieldName, offset));
        }

        byte _GetMarshalTestByteValue(int ofs)
        {
            if (ofs + 1 > 256) throw new InvalidOperationException("structs over 255 bytes are not supported by this test.");
            IntPtr mem = Marshal.AllocCoTaskMem(1);
            Marshal.WriteByte(mem, (byte)ofs);
            byte* _val = (byte*)mem;
            byte result = *_val;
            Marshal.FreeCoTaskMem(mem);
            return result;
        }

        Int32 _GetMarshalTestInt32Value(int ofs)
        {
            if (ofs + 4 > 256) throw new InvalidOperationException("structs over 255 bytes are not supported by this test.");
            IntPtr mem = Marshal.AllocCoTaskMem(4);
            for (var i = 0; i <= 3; i++)
                Marshal.WriteByte(mem + i, (byte)(ofs + i));
            Int32* _val = (Int32*)mem;
            Int32 result = *_val;
            Marshal.FreeCoTaskMem(mem);
            return result;
        }

        Int64 _GetMarshalTestInt64Value(int ofs)
        {
            if (ofs + 8 > 256) throw new InvalidOperationException("structs over 255 bytes are not supported by this test.");
            IntPtr mem = Marshal.AllocCoTaskMem(8);
            for (var i = 0; i <= 7; i++)
                Marshal.WriteByte(mem + i, (byte)(ofs + i));
            Int64* _val = (Int64*)mem;
            Int64 result = *_val;
            Marshal.FreeCoTaskMem(mem);
            return result;
        }

        double _GetMarshalTestDoubleValue(int ofs)
        {
            if (ofs + 8 > 256) throw new InvalidOperationException("structs over 255 bytes are not supported by this test.");
            IntPtr mem = Marshal.AllocCoTaskMem(8);
            for (var i = 0; i <= 7; i++)
                Marshal.WriteByte(mem + i, (byte)(ofs + i));
            double* _val = (double*)mem;
            double result = *_val;
            Marshal.FreeCoTaskMem(mem);
            return result;
        }

        Int64 _GetMarshalTestPTRValue(int ofs)
        {
            if (ofs + 8 > 256) throw new InvalidOperationException("structs over 255 bytes are not supported by this test.");
            IntPtr mem = Marshal.AllocCoTaskMem(8);
            for (var i = 0; i <= 7; i++)
                Marshal.WriteByte(mem + i, (byte)(ofs + i));
            Int64* _val = (Int64*)mem;
            Int64 result = *_val;
            Marshal.FreeCoTaskMem(mem);
            return result;
        }

        /// <summary>
        /// If there's any marshalling incompatibility, this will throw an exception.
        /// </summary>
        public void RunMarshallingTests(bool showConsoleMessages = true)
        {
            HandleProxy* hp = V8NetProxy.CreateHandleProxyTest();
            NativeV8EngineProxy* nv8ep = V8NetProxy.CreateV8EngineProxyTest();
            NativeObjectTemplateProxy* notp = V8NetProxy.CreateObjectTemplateProxyTest();
            NativeFunctionTemplateProxy* ndtp = V8NetProxy.CreateFunctionTemplateProxyTest();

            if (showConsoleMessages) Console.WriteLine("Testing 'HandleProxy' ...");

            int ofs = 0;

            if ((Int32)hp->_NativeClassType != _GetMarshalTestInt32Value(ofs)) _ThrowMarshalTestError("HandleProxy", "_NativeClassType", ofs);
            ofs += 4;

            if ((Int32)hp->ID != _GetMarshalTestInt32Value(ofs)) _ThrowMarshalTestError("HandleProxy", "ID", ofs);
            ofs += 4;

            if ((Int32)hp->_ObjectID != _GetMarshalTestInt32Value(ofs)) _ThrowMarshalTestError("HandleProxy", "_ObjectID", ofs);
            ofs += 4;

            if ((Int32)hp->_ValueType != _GetMarshalTestInt32Value(ofs)) _ThrowMarshalTestError("HandleProxy", "_ValueType", ofs);
            ofs += 4;

            // region ### HANDLE VALUE ### - Note: This is only valid upon calling 'UpdateValue()'.
            if ((byte)hp->V8Boolean != _GetMarshalTestByteValue(ofs)) _ThrowMarshalTestError("HandleProxy", "V8Boolean", ofs);
            if ((Int64)hp->V8Integer != _GetMarshalTestInt64Value(ofs)) _ThrowMarshalTestError("HandleProxy", "V8Integer", ofs);
            if ((double)hp->V8Number != _GetMarshalTestDoubleValue(ofs)) _ThrowMarshalTestError("HandleProxy", "V8Number", ofs);
            ofs += 8;
            if ((Int64)hp->V8String != _GetMarshalTestPTRValue(ofs)) _ThrowMarshalTestError("HandleProxy", "V8String", ofs);
            ofs += 8;
            // endregion

            if ((Int64)hp->ManagedReferenceCount != _GetMarshalTestInt64Value(ofs)) _ThrowMarshalTestError("HandleProxy", "ManagedReferenceCount", ofs); // The number of references on the managed side.
            ofs += 8;

            if ((Int32)hp->Disposed != _GetMarshalTestInt32Value(ofs)) _ThrowMarshalTestError("HandleProxy", "Disposed", ofs); // (0 = in use, 1 = managed side ready to dispose, 2 = object is weak (if applicable), 3 = disposed/cached)
            ofs += 4;

            if ((Int32)hp->EngineID != _GetMarshalTestInt32Value(ofs)) _ThrowMarshalTestError("HandleProxy", "EngineID", ofs);
            ofs += 4;

            if ((Int64)hp->NativeEngineProxy != _GetMarshalTestPTRValue(ofs)) _ThrowMarshalTestError("HandleProxy", "NativeEngineProxy", ofs); // Pointer to the native V8 engine proxy object associated with this proxy handle instance (used native side to free the handle upon destruction).
            ofs += 8;

            if ((Int64)hp->Handle != _GetMarshalTestPTRValue(ofs)) _ThrowMarshalTestError("HandleProxy", "Handle", ofs); // The native V8 persistent object handle (not used on the managed side).

            V8NetProxy.DeleteTestData(hp);
            V8NetProxy.DeleteTestData(nv8ep);
            V8NetProxy.DeleteTestData(notp);
            V8NetProxy.DeleteTestData(ndtp);
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
