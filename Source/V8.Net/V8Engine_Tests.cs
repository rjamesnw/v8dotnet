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

        void _ThrowMarshalTestError(string structName, string fieldName, byte offset, byte[] goodData, byte* badValues, int badValLen = 0)
        {
            var details = "";
            if (goodData != null && goodData.Length > 0)
                details += " \r\nBytes expected (sequential order in memory): " + goodData.Join(", ");
            if (badValues != null)
            {
                var badData = new byte[goodData != null ? goodData.Length : badValLen > 0 ? badValLen : IntPtr.Size];
                for (var i = 0; i < badData.Length; i++)
                    badData[i] = badValues[i];
                details += " \r\nBytes received (sequential order in memory): " + badData.Join(", ");
            }
            throw new ContextMarshalException(string.Format("The field value for '{0}->{1}' (offset {2}) on the native side proxy struct does not align with the managed side struct." + details,
                structName, fieldName, offset));
        }

        byte _GetMarshalTestByteValue(byte ofs, out byte[] data)
        {
            IntPtr mem = Marshal.AllocCoTaskMem(1);
            data = new byte[1];
            Marshal.WriteByte(mem, data[0] = ofs);
            byte* _val = (byte*)mem;
            byte result = *_val;
            Marshal.FreeCoTaskMem(mem);
            return result;
        }

        Int32 _GetMarshalTestInt32Value(byte ofs, out byte[] data)
        {
            IntPtr mem = Marshal.AllocCoTaskMem(4);
            data = new byte[4];
            for (byte i = 0; i < data.Length; i++)
                Marshal.WriteByte((IntPtr)((int)mem + i), data[i] = (byte)(ofs + i));
            Int32* _val = (Int32*)mem;
            Int32 result = *_val;
            Marshal.FreeCoTaskMem(mem);
            return result;
        }

        Int64 _GetMarshalTestInt64Value(byte ofs, out byte[] data)
        {
            IntPtr mem = Marshal.AllocCoTaskMem(8);
            data = new byte[8];
            for (byte i = 0; i < data.Length; i++)
                Marshal.WriteByte((IntPtr)((int)mem + i), data[i] = (byte)(ofs + i));
            Int64* _val = (Int64*)mem;
            Int64 result = *_val;
            Marshal.FreeCoTaskMem(mem);
            return result;
        }

        double _GetMarshalTestDoubleValue(byte ofs, out byte[] data)
        {
            IntPtr mem = Marshal.AllocCoTaskMem(8);
            data = new byte[8];
            for (byte i = 0; i < data.Length; i++)
                Marshal.WriteByte((IntPtr)((int)mem + i), data[i] = (byte)(ofs + i));
            double* _val = (double*)mem;
            double result = *_val;
            Marshal.FreeCoTaskMem(mem);
            return result;
        }

        Int64 _GetMarshalTestPTRValue(byte ofs, out byte[] data)
        {
            IntPtr mem = Marshal.AllocCoTaskMem(8);
            data = new byte[IntPtr.Size];
            for (byte i = 0; i < data.Length; i++)
                Marshal.WriteByte((IntPtr)((int)mem + i), data[i] = (byte)(ofs + i));
            Int32* _val32 = (Int32*)mem;
            Int64* _val64 = (Int64*)mem;
            Int64 result = data.Length == 8 ? *_val64 : *_val32;
            Marshal.FreeCoTaskMem(mem);
            return result;
        }

        /// <summary>
        /// If there's any marshalling incompatibility, this will throw an exception.
        /// </summary>
        public void RunMarshallingTests()
        {
            HandleProxy* hp = V8NetProxy.CreateHandleProxyTest();
            NativeV8EngineProxy* nv8ep = V8NetProxy.CreateV8EngineProxyTest();
            NativeObjectTemplateProxy* notp = V8NetProxy.CreateObjectTemplateProxyTest();
            NativeFunctionTemplateProxy* nftp = V8NetProxy.CreateFunctionTemplateProxyTest();

            byte[] data;
            byte ofs = 0; // (skip type)

            try
            {
                ofs = (byte)((int)&hp->NativeClassType - (int)hp);
                if (hp->NativeClassType != ProxyObjectType.HandleProxyClass) _ThrowMarshalTestError("HandleProxy", "NativeClassType", ofs, null, (byte*)&hp->NativeClassType, 4);
                ofs = (byte)((int)&hp->ID - (int)hp);
                if ((Int32)hp->ID != _GetMarshalTestInt32Value(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "ID", ofs, data, (byte*)&hp->ID);
                ofs = (byte)((int)&hp->_ObjectID - (int)hp);
                if ((Int32)hp->_ObjectID != _GetMarshalTestInt32Value(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "_ObjectID", ofs, data, (byte*)&hp->_ObjectID);
                ofs = (byte)((int)&hp->_CLRTypeID - (int)hp);
                if ((Int32)hp->_CLRTypeID != _GetMarshalTestInt32Value(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "_CLRTypeID", ofs, data, (byte*)&hp->_CLRTypeID);
                ofs = (byte)((int)&hp->_ValueType - (int)hp);
                if ((Int32)hp->_ValueType != _GetMarshalTestInt32Value(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "_ValueType", ofs, data, (byte*)&hp->_ValueType);
                // region ### HANDLE VALUE ### - Note: This is only valid upon calling 'UpdateValue()'.
                ofs = (byte)((int)&hp->V8Boolean - (int)hp);
                if ((byte)hp->V8Boolean != _GetMarshalTestByteValue(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "V8Boolean", ofs, data, (byte*)&hp->V8Boolean);
                if ((Int64)hp->V8Integer != _GetMarshalTestInt64Value(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "V8Integer", ofs, data, (byte*)&hp->V8Integer);
                if ((double)hp->V8Number != _GetMarshalTestDoubleValue(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "V8Number", ofs, data, (byte*)&hp->V8Number);
                ofs = (byte)((int)&hp->V8String - (int)hp);
                if ((Int64)hp->V8String != _GetMarshalTestPTRValue(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "V8String", ofs, data, (byte*)&hp->V8String);
                // endregion
                ofs = (byte)((int)&hp->ManagedReferenceCount - (int)hp);
                if ((Int64)hp->ManagedReferenceCount != _GetMarshalTestInt64Value(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "ManagedReferenceCount", ofs, data, (byte*)&hp->ManagedReferenceCount); // The number of references on the managed side.
                ofs = (byte)((int)&hp->Disposed - (int)hp);
                if ((Int32)hp->Disposed != _GetMarshalTestInt32Value(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "Disposed", ofs, data, (byte*)&hp->Disposed); // (0 = in use, 1 = managed side ready to dispose, 2 = object is weak (if applicable), 3 = disposed/cached)
                ofs = (byte)((int)&hp->EngineID - (int)hp);
                if ((Int32)hp->EngineID != _GetMarshalTestInt32Value(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "EngineID", ofs, data, (byte*)&hp->EngineID);
                ofs = (byte)((int)&hp->NativeEngineProxy - (int)hp);
                if ((Int64)hp->NativeEngineProxy != _GetMarshalTestPTRValue(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "NativeEngineProxy", ofs, data, (byte*)&hp->NativeEngineProxy); // Pointer to the native V8 engine proxy object associated with this proxy handle instance (used native side to free the handle upon destruction).
                ofs = (byte)((int)&hp->NativeV8Handle - (int)hp);
                if ((Int64)hp->NativeV8Handle != _GetMarshalTestPTRValue(ofs, out data)) _ThrowMarshalTestError("HandleProxy", "NativeV8Handle", ofs, data, (byte*)&hp->NativeV8Handle); // The native V8 persistent object handle (not used on the managed side).

                ofs = (byte)((int)&nv8ep->NativeClassType - (int)nv8ep);
                if (nv8ep->NativeClassType != ProxyObjectType.V8EngineProxyClass) _ThrowMarshalTestError("NativeV8EngineProxy", "NativeClassType", ofs, null, (byte*)&nv8ep->NativeClassType, 4);
                ofs = (byte)((int)&nv8ep->ID - (int)nv8ep);
                if ((Int32)nv8ep->ID != _GetMarshalTestInt32Value(ofs, out data)) _ThrowMarshalTestError("NativeV8EngineProxy", "ID", ofs, data, (byte*)&nv8ep->ID);
                ofs += (byte)data.Length;

                ofs = (byte)((int)&notp->NativeClassType - (int)notp);
                if (notp->NativeClassType != ProxyObjectType.ObjectTemplateProxyClass) _ThrowMarshalTestError("NativeObjectTemplateProxy", "NativeClassType", ofs, null, (byte*)&notp->NativeClassType, 4);
                ofs = (byte)((int)&notp->NativeEngineProxy - (int)notp);
                if ((Int64)notp->NativeEngineProxy != _GetMarshalTestPTRValue(ofs, out data)) _ThrowMarshalTestError("NativeObjectTemplateProxy", "NativeEngineProxy", ofs, data, (byte*)&notp->NativeEngineProxy);
                ofs = (byte)((int)&notp->EngineID - (int)notp);
                if ((Int32)notp->EngineID != _GetMarshalTestInt32Value(ofs, out data)) _ThrowMarshalTestError("NativeObjectTemplateProxy", "EngineID", ofs, data, (byte*)&notp->EngineID);
                ofs = (byte)((int)&notp->ObjectID - (int)notp);
                if ((Int32)notp->ObjectID != _GetMarshalTestInt32Value(ofs, out data)) _ThrowMarshalTestError("NativeObjectTemplateProxy", "ObjectID", ofs, data, (byte*)&notp->ObjectID);
                ofs = (byte)((int)&notp->NativeObjectTemplate - (int)notp);
                if ((Int64)notp->NativeObjectTemplate != _GetMarshalTestPTRValue(ofs, out data)) _ThrowMarshalTestError("NativeObjectTemplateProxy", "NativeObjectTemplate", ofs, data, (byte*)&notp->NativeObjectTemplate);

                ofs = (byte)((int)&nftp->NativeClassType - (int)nftp);
                if (nftp->NativeClassType != ProxyObjectType.FunctionTemplateProxyClass) _ThrowMarshalTestError("NativeFunctionTemplateProxy", "NativeClassType", ofs, null, (byte*)&nftp->NativeClassType, 4);
                ofs = (byte)((int)&nftp->NativeEngineProxy - (int)nftp);
                if ((Int64)nftp->NativeEngineProxy != _GetMarshalTestPTRValue(ofs, out data)) _ThrowMarshalTestError("NativeFunctionTemplateProxy", "NativeEngineProxy", ofs, data, (byte*)&nftp->NativeEngineProxy);
                ofs = (byte)((int)&nftp->EngineID - (int)nftp);
                if ((Int32)nftp->EngineID != _GetMarshalTestInt32Value(ofs, out data)) _ThrowMarshalTestError("NativeFunctionTemplateProxy", "EngineID", ofs, data, (byte*)&nftp->EngineID);
                ofs = (byte)((int)&nftp->NativeFucntionTemplate - (int)nftp);
                if ((Int64)nftp->NativeFucntionTemplate != _GetMarshalTestPTRValue(ofs, out data)) _ThrowMarshalTestError("NativeFunctionTemplateProxy", "NativeFucntionTemplate", ofs, data, (byte*)&nftp->NativeFucntionTemplate);
            }
            finally
            {
                V8NetProxy.DeleteTestData(hp);
                V8NetProxy.DeleteTestData(nv8ep);
                V8NetProxy.DeleteTestData(notp);
                V8NetProxy.DeleteTestData(nftp);
            }
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
