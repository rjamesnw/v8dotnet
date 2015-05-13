using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using V8.Net;

namespace V8.Net
{
    public class Program
    {
        static V8Engine _JSServer;

        static System.Timers.Timer _TitleUpdateTimer;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                Console.Write(Environment.NewLine + "Creating a V8Engine instance ...");
                _JSServer = new V8Engine();
                Console.WriteLine(" Done!");

                Console.Write("Testing marshalling compatibility...");
                _JSServer.RunMarshallingTests();
                Console.WriteLine(" Pass!");

                _TitleUpdateTimer = new System.Timers.Timer(500);
                _TitleUpdateTimer.AutoReset = true;
                _TitleUpdateTimer.Elapsed += (_o, _e) =>
                {
                    if (!_JSServer.IsDisposed)
                        Console.Title = "V8.Net Console - " + (IntPtr.Size == 4 ? "32-bit" : "64-bit") + " mode (Handles: " + _JSServer.TotalHandles
                            + " / Pending Native GC: " + _JSServer.TotalHandlesBeingDisposed
                            + " / Cached: " + _JSServer.TotalHandlesCached
                            + " / In Use: " + (_JSServer.TotalHandles - _JSServer.TotalHandlesCached) + ")";
                    else
                        Console.Title = "V8.Net Console - Shutting down...";
                };
                _TitleUpdateTimer.Start();

                {
                    Console.WriteLine(Environment.NewLine + "Creating some global CLR types ...");

                    // (Note: It's not required to explicitly register a type, but it is recommended for more control.)

                    _JSServer.RegisterType(typeof(Object), "Object", true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(Type), "Type", true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(String), "String", true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(Boolean), "Boolean", true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(Array), "Array", true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(System.Collections.ArrayList), null, true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(char), null, true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(int), null, true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(Int16), null, true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(Int32), null, true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(Int64), null, true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(UInt16), null, true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(UInt32), null, true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(UInt64), null, true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(Enumerable), null, true, ScriptMemberSecurity.Locked);
                    _JSServer.RegisterType(typeof(System.IO.File), null, true, ScriptMemberSecurity.Locked);

                    ObjectHandle hSystem = _JSServer.CreateObject();
                    _JSServer.DynamicGlobalObject.System = hSystem;
                    hSystem.SetProperty(typeof(Object)); // (Note: No optional parameters used, so this will simply lookup and apply the existing registered type details above.)
                    hSystem.SetProperty(typeof(String));
                    hSystem.SetProperty(typeof(Boolean));
                    hSystem.SetProperty(typeof(Array));
                    _JSServer.GlobalObject.SetProperty(typeof(Type));
                    _JSServer.GlobalObject.SetProperty(typeof(System.Collections.ArrayList));
                    _JSServer.GlobalObject.SetProperty(typeof(char));
                    _JSServer.GlobalObject.SetProperty(typeof(int));
                    _JSServer.GlobalObject.SetProperty(typeof(Int16));
                    _JSServer.GlobalObject.SetProperty(typeof(Int32));
                    _JSServer.GlobalObject.SetProperty(typeof(Int64));
                    _JSServer.GlobalObject.SetProperty(typeof(UInt16));
                    _JSServer.GlobalObject.SetProperty(typeof(UInt32));
                    _JSServer.GlobalObject.SetProperty(typeof(UInt64));
                    _JSServer.GlobalObject.SetProperty(typeof(Enumerable));
                    _JSServer.GlobalObject.SetProperty(typeof(Environment));
                    _JSServer.GlobalObject.SetProperty(typeof(System.IO.File));

                    _JSServer.GlobalObject.SetProperty(typeof(Uri), V8PropertyAttributes.Locked, null, true, ScriptMemberSecurity.Locked); // (Note: Not yet registered, but will auto register!)
                    _JSServer.GlobalObject.SetProperty("uri", new Uri("http://www.example.com"));

                    _JSServer.GlobalObject.SetProperty(typeof(GenericTest<int, string>), V8PropertyAttributes.Locked, null, true, ScriptMemberSecurity.Locked);
                    _JSServer.GlobalObject.SetProperty(typeof(GenericTest<string, int>), V8PropertyAttributes.Locked, null, true, ScriptMemberSecurity.Locked);

                    Console.WriteLine(Environment.NewLine + "Creating a global 'dump(obj)' function to dump properties of objects (one level only) ...");
                    _JSServer.ConsoleExecute(@"dump = function(o) { var s=''; if (typeof(o)=='undefined') return 'undefined';"
                        + @" if (typeof o.valueOf=='undefined') return ""'valueOf()' is missing on '""+(typeof o)+""' - if you are inheriting from V8ManagedObject, make sure you are not blocking the property."";"
                        + @" if (typeof o.toString=='undefined') return ""'toString()' is missing on '""+o.valueOf()+""' - if you are inheriting from V8ManagedObject, make sure you are not blocking the property."";"
                        + @" for (var p in o) {var ov='', pv=''; try{ov=o.valueOf();}catch(e){ov='{error: '+e.message+': '+dump(o)+'}';} try{pv=o[p];}catch(e){pv=e.message;} s+='* '+ov+'.'+p+' = ('+pv+')\r\n'; } return s; }");

                    Console.WriteLine(Environment.NewLine + "Creating a global 'assert(msg, a,b)' function for property value assertion ...");
                    _JSServer.ConsoleExecute(@"assert = function(msg,a,b) { msg += ' ('+a+'==='+b+'?)'; if (a === b) return msg+' ... Ok.'; else throw msg+' ... Failed!'; }");

                    Console.WriteLine(Environment.NewLine + "Creating a global 'Console' object ...");
                    _JSServer.GlobalObject.SetProperty(typeof(Console), V8PropertyAttributes.Locked, null, true, ScriptMemberSecurity.Locked);
                    //??_JSServer.CreateObject<JS_Console>();

                    Console.WriteLine(Environment.NewLine + "Creating a new global type 'TestEnum' ...");
                    _JSServer.GlobalObject.SetProperty(typeof(TestEnum), V8PropertyAttributes.Locked, null, true, ScriptMemberSecurity.Locked);

                    Console.WriteLine(Environment.NewLine + "Creating a new global type 'SealedObject' as 'Sealed_Object' ...");
                    Console.WriteLine("(represents a 3rd-party inaccessible V8.NET object.)");
                    _JSServer.GlobalObject.SetProperty(typeof(SealedObject), V8PropertyAttributes.Locked, null, true);

                    Console.WriteLine(Environment.NewLine + "Creating a new wrapped and locked object 'sealedObject' ...");
                    _JSServer.GlobalObject.SetProperty("sealedObject", new SealedObject(null, null), null, true, ScriptMemberSecurity.Locked);

                    Console.WriteLine(Environment.NewLine + "Dumping global properties ...");
                    _JSServer.VerboseConsoleExecute(@"dump(this)");

                    Console.WriteLine(Environment.NewLine + "Here is a contrived example of calling and passing CLR methods/types ...");
                    _JSServer.VerboseConsoleExecute(@"r = Enumerable.Range(1,Int32('10'));");
                    _JSServer.VerboseConsoleExecute(@"a = System.String.Join$1([Int32], ', ', r);");

                    Console.WriteLine(Environment.NewLine + "Example of changing 'System.String.Empty' member security attributes to 'NoAccess'...");
                    _JSServer.GetTypeBinder(typeof(String)).ChangeMemberSecurity("Empty", ScriptMemberSecurity.NoAcccess);
                    _JSServer.VerboseConsoleExecute(@"System.String.Empty;");
                    Console.WriteLine("(Note: Access denied is only for static types - bound instances are more dynamic, and will hide properties instead [name/index interceptors are not available on V8 Function objects])");

                    Console.WriteLine(Environment.NewLine + "Finally, how to view method signatures...");
                    _JSServer.VerboseConsoleExecute(@"dump(System.String.Join);");

                    var funcTemp = _JSServer.CreateFunctionTemplate<SamplePointFunctionTemplate>("SamplePointFunctionTemplate");
                }

                Console.WriteLine(Environment.NewLine + @"Ready - just enter script to execute. Type '\' or '\help' for a list of console specific commands.");

                string input, lcInput;

                while (true)
                {
                    try
                    {
                        Console.Write(Environment.NewLine + "> ");

                        input = Console.ReadLine();
                        lcInput = input.Trim().ToLower();

                        if (lcInput == @"\help" || lcInput == @"\")
                        {
                            Console.WriteLine(@"Special console commands (all commands are triggered via a preceding '\' character so as not to confuse it with script code):");
                            Console.WriteLine(@"\cls - Clears the screen.");
                            Console.WriteLine(@"\test - Starts the test process.");
                            Console.WriteLine(@"\gc - Triggers garbage collection (for testing purposes).");
                            Console.WriteLine(@"\v8gc - Triggers garbage collection in V8 (for testing purposes).");
                            Console.WriteLine(@"\gctest - Runs a simple GC test against V8.NET and the native V8 engine.");
                            Console.WriteLine(@"\speedtest - Runs a simple test script to test V8.NET performance with the V8 engine.");
                            Console.WriteLine(@"\mtest - Runs a simple test script to test V8.NET integration/marshalling compatibility with the V8 engine on your system.");
                            Console.WriteLine(@"\newenginetest - Creates 3 new engines (each time) and runs simple expressions in each one (note: new engines are never removed once created).");
                            Console.WriteLine(@"\exit - Exists the console.");
                        }
                        else if (lcInput == @"\cls")
                            Console.Clear();
                        else if (lcInput == @"\test")
                        {
                            try
                            {
                                /* This command will serve as a means to run fast tests against various aspects of V8.NET from the JavaScript side.
                                 * This is preferred over unit tests because 1. it takes a bit of time for the engine to initialize, 2. internal feedback
                                 * can be sent to the console from the environment, and 3. serves as a nice implementation example.
                                 * The unit testing project will serve to test basic engine instantiation and solo utility classes.
                                 * In the future, the following testing process may be redesigned to be runnable in both unit tests and console apps.
                                 */

                                Console.WriteLine("\r\n===============================================================================");
                                Console.WriteLine("Setting up the test environment ...\r\n");

                                {
                                    // ... create a function template in order to generate our object! ...
                                    // (note: this is not using ObjectTemplate because the native V8 does not support class names for those objects [class names are object type names])

                                    Console.Write("\r\nCreating a FunctionTemplate instance ...");
                                    var funcTemplate = _JSServer.CreateFunctionTemplate(typeof(V8DotNetTesterWrapper).Name);
                                    Console.WriteLine(" Ok.");

                                    // ... use the template to generate our object ...

                                    Console.Write("\r\nRegistering the custom V8DotNetTester function object ...");
                                    var testerFunc = funcTemplate.GetFunctionObject<V8DotNetTesterFunction>();
                                    _JSServer.DynamicGlobalObject.V8DotNetTesterWrapper = testerFunc;
                                    Console.WriteLine(" Ok.  'V8DotNetTester' is now a type [Function] in the global scope.");

                                    Console.Write("\r\nCreating a V8DotNetTester instance from within JavaScript ...");
                                    // (note: Once 'V8DotNetTester' is constructed, the 'Initialize()' override will be called immediately before returning,
                                    // but you can return "engine.GetObject<V8DotNetTester>(_this.Handle, true, false)" to prevent it.)
                                    _JSServer.VerboseConsoleExecute("testWrapper = new V8DotNetTesterWrapper();");
                                    _JSServer.VerboseConsoleExecute("tester = testWrapper.tester;");
                                    Console.WriteLine(" Ok.");

                                    // ... Ok, the object exists, BUT, it is STILL not yet part of the global object, so we add it next ...

                                    Console.Write("\r\nRetrieving the 'tester' property on the global object for the V8DotNetTester instance ...");
                                    var handle = _JSServer.GlobalObject.GetProperty("tester");
                                    var tester = (V8DotNetTester)_JSServer.DynamicGlobalObject.tester;
                                    Console.WriteLine(" Ok.");

                                    Console.WriteLine("\r\n===============================================================================");
                                    Console.WriteLine("Dumping global properties ...\r\n");

                                    _JSServer.VerboseConsoleExecute("dump(this)");

                                    Console.WriteLine("\r\n===============================================================================");
                                    Console.WriteLine("Dumping tester properties ...\r\n");

                                    _JSServer.VerboseConsoleExecute("dump(tester)");

                                    // ... example of adding a functions via script (note: V8Engine.GlobalObject.Properties will have 'Test' set) ...

                                    Console.WriteLine("\r\n===============================================================================");
                                    Console.WriteLine("Ready to run the tester, press any key to proceed ...\r\n");
                                    Console.ReadKey();

                                    tester.Execute();

                                    Console.WriteLine("\r\nReleasing managed tester object ...\r\n");
                                    tester.Handle.ReleaseManagedObject();
                                }

                                Console.WriteLine("\r\n===============================================================================\r\n");
                                Console.WriteLine("Test completed successfully! Any errors would have interrupted execution.");
                                Console.WriteLine("Note: The 'dump(obj)' function is available to use for manual inspection.");
                                Console.WriteLine("Press any key to dump the global properties ...");
                                Console.ReadKey();
                                _JSServer.VerboseConsoleExecute("dump(this);");
                            }
                            catch
                            {
                                Console.WriteLine("\r\nTest failed.\r\n");
                                throw;
                            }
                        }
                        else if (lcInput == @"\gc")
                        {
                            Console.Write("\r\nForcing garbage collection ... ");
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            Console.WriteLine("Done.\r\n");
                        }
                        else if (lcInput == @"\v8gc")
                        {
                            Console.Write("\r\nForcing V8 garbage collection ... ");
                            _JSServer.ForceV8GarbageCollection();
                            Console.WriteLine("Done.\r\n");
                        }
                        else if (lcInput == @"\gctest")
                        {
                            Console.WriteLine("\r\nTesting garbage collection ... ");

                            V8NativeObject tempObj;
                            InternalHandle internalHandle = InternalHandle.Empty;
                            int i;

                            {
                                Console.WriteLine("Setting 'this.tempObj' to a new managed object ...");

                                tempObj = _JSServer.CreateObject<V8NativeObject>();
                                internalHandle = tempObj.Handle;
                                Handle testHandle = internalHandle;
                                _JSServer.DynamicGlobalObject.tempObj = tempObj;

                                // ... because we have a strong reference to the handle in 'testHandle', the managed and native objects are safe; however,
                                // this block has the only strong reference, so once the reference goes out of scope, the managed GC should attempt to
                                // collect it, which will mark the handle as ready for collection (but it will not be destroyed just yet until V8 is ready) ...

                                Console.WriteLine("Clearing managed references and running the garbage collector ...");
                                testHandle = null;
                            }

                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            // (we wait for the 'testHandle' handle object to be collected, which will dispose the handle)
                            // (note: we do not call 'Set()' on 'internalHandle' because the "Handle" type takes care of the disposal)

                            for (i = 0; i < 3000 && internalHandle.ReferenceCount > 1; i++)
                                System.Threading.Thread.Sleep(1); // (just wait for the worker)

                            if (internalHandle.ReferenceCount > 1)
                                throw new Exception("Handle is still not ready for GC ... something is wrong.");

                            Console.WriteLine("Success! The managed handle instance is pending disposal.");
                            Console.WriteLine("Clearing the handle object reference next ...");

                            // ... because we still have a reference to 'tempObj' at this point, the managed and native objects are safe; however, this 
                            // block scope has the only strong reference to the managed object keeping everything alive (including the underlying handle),
                            // so once the reference goes out of scope, the managed GC will collect it, which will mark the managed object as ready for
                            // collection. Once both the managed object and handle are marked, this in turn marks the native handle as weak. When the native
                            // V8 engine's garbage collector is ready to dispose of the handle, as call back is triggered and the native object and
                            // handles will finally be removed ...

                            tempObj = null;

                            Console.WriteLine("Forcing CLR garbage collection ... ");
                            GC.Collect();
                            GC.WaitForPendingFinalizers();

                            Console.WriteLine("Waiting on the worker to make the object weak on the native V8 side ... ");

                            for (i = 0; i < 6000 && !internalHandle.IsNativelyWeak; i++)
                                System.Threading.Thread.Sleep(1);

                            if (!internalHandle.IsNativelyWeak)
                                throw new Exception("Object is not weak yet ... something is wrong.");

                            Console.WriteLine("The native side object is now weak and ready to be collected by V8.");

                            Console.WriteLine("Forcing V8 garbage collection ... ");
                            _JSServer.DynamicGlobalObject.tempObj = null;
                            for (i = 0; i < 3000 && !internalHandle.IsDisposed; i++)
                            {
                                _JSServer.ForceV8GarbageCollection();
                                System.Threading.Thread.Sleep(1);
                            }

                            Console.WriteLine("Looking for object ...");

                            if (!internalHandle.IsDisposed) throw new Exception("Managed object was not garbage collected.");
                            // (note: this call is only valid as long as no more objects are created before this point)
                            Console.WriteLine("Success! The managed V8NativeObject instance is disposed.");
                            Console.WriteLine("\r\nDone.\r\n");
                        }
                        else if (lcInput == @"\speedtest")
                        {
                            var timer = new Stopwatch();
                            long startTime, elapsed;
                            long count;
                            double result1, result2, result3, result4;

                            Console.WriteLine(Environment.NewLine + "Running the speed tests ... ");

                            timer.Start();

                            //??Console.WriteLine(Environment.NewLine + "Running the property access speed tests ... ");
                            Console.WriteLine("(Note: 'V8NativeObject' objects are always faster than using the 'V8ManagedObject' objects because native objects store values within the V8 engine and managed objects store theirs on the .NET side.)");

                            count = 200000000;

                            Console.WriteLine("\r\nTesting global property write speed ... ");
                            startTime = timer.ElapsedMilliseconds;
                            _JSServer.Execute("o={i:0}; for (o.i=0; o.i<" + count + "; o.i++) n = 0;"); // (o={i:0}; is used in case the global object is managed, which will greatly slow down the loop)
                            elapsed = timer.ElapsedMilliseconds - startTime;
                            result1 = (double)elapsed / count;
                            Console.WriteLine(count + " loops @ " + elapsed + "ms total = " + result1.ToString("0.0#########") + " ms each pass.");

                            Console.WriteLine("\r\nTesting global property read speed ... ");
                            startTime = timer.ElapsedMilliseconds;
                            _JSServer.Execute("for (o.i=0; o.i<" + count + "; o.i++) n;");
                            elapsed = timer.ElapsedMilliseconds - startTime;
                            result2 = (double)elapsed / count;
                            Console.WriteLine(count + " loops @ " + elapsed + "ms total = " + result2.ToString("0.0#########") + " ms each pass.");

                            count = 200000;

                            Console.WriteLine("\r\nTesting property write speed on a managed object (with interceptors) ... ");
                            _JSServer.DynamicGlobalObject.mo = _JSServer.CreateObjectTemplate().CreateObject();
                            startTime = timer.ElapsedMilliseconds;
                            _JSServer.Execute("o={i:0}; for (o.i=0; o.i<" + count + "; o.i++) mo.n = 0;");
                            elapsed = timer.ElapsedMilliseconds - startTime;
                            result3 = (double)elapsed / count;
                            Console.WriteLine(count + " loops @ " + elapsed + "ms total = " + result3.ToString("0.0#########") + " ms each pass.");

                            Console.WriteLine("\r\nTesting property read speed on a managed object (with interceptors) ... ");
                            startTime = timer.ElapsedMilliseconds;
                            _JSServer.Execute("for (o.i=0; o.i<" + count + "; o.i++) mo.n;");
                            elapsed = timer.ElapsedMilliseconds - startTime;
                            result4 = (double)elapsed / count;
                            Console.WriteLine(count + " loops @ " + elapsed + "ms total = " + result4.ToString("0.0#########") + " ms each pass.");

                            Console.WriteLine("\r\nUpdating native properties is {0:N2}x faster than managed ones.", result3 / result1);
                            Console.WriteLine("\r\nReading native properties is {0:N2}x faster than managed ones.", result4 / result2);

                            Console.WriteLine("\r\nDone.\r\n");
                        }
                        else if (lcInput == @"\exit")
                        {
                            Console.WriteLine("User requested exit, disposing the engine instance ...");
                            _JSServer.Dispose();
                            Console.WriteLine("Engine disposed successfully. Press any key to continue ...");
                            Console.ReadKey();
                            Console.WriteLine("Goodbye. :)");
                            break;
                        }
                        else if (lcInput == @"\mtest")
                        {
                            Console.WriteLine("Loading and marshalling native structs with test data ...");

                            _JSServer.RunMarshallingTests();

                            Console.WriteLine("Success! The marshalling between native and managed side is working as expected.");
                        }
                        else if (lcInput == @"\newenginetest")
                        {
                            Console.WriteLine("Creating 3 more engines ...");

                            var engine1 = new V8Engine();
                            var engine2 = new V8Engine();
                            var engine3 = new V8Engine();

                            Console.WriteLine("Running test expressions ...");

                            var resultHandle = engine1.Execute("1 + 2");
                            var result = resultHandle.AsInt32;
                            Console.WriteLine("Engine 1: 1+2=" + result);
                            resultHandle.Dispose();

                            resultHandle = engine2.Execute("2+3");
                            result = resultHandle.AsInt32;
                            Console.WriteLine("Engine 2: 2+3=" + result);
                            resultHandle.Dispose();

                            resultHandle = engine3.Execute("3 + 4");
                            result = resultHandle.AsInt32;
                            Console.WriteLine("Engine 3: 3+4=" + result);
                            resultHandle.Dispose();

                            Console.WriteLine("Done.");
                        }
                        else if (lcInput == @"\memleaktest")
                        {
                            string script = @"
for (var i=0; i < 1000; i++) {
// if the loop is empty no memory leak occurs.
// if any of the following 3 method calls are uncommented then a bad memory leak occurs.
//SomeMethods.StaticDoNothing();
//shared.StaticDoNothing();
shared.InstanceDoNothing();
}
";
                            _JSServer.GlobalObject.SetProperty(typeof(SomeMethods), recursive: true, memberSecurity: ScriptMemberSecurity.ReadWrite);
                            var sm = new SomeMethods();
                            _JSServer.GlobalObject.SetProperty("shared", sm, recursive: true);
                            var hScript = _JSServer.Compile(script, null, true);
                            int i = 0;
                            try
                            {
                                while (true)
                                {
                                    // putting a using statement on the returned handle stops the memory leak when running just the for loop.
                                    // using a compiled script seems to reduce garbage collection, but does not affect the memory leak
                                    using (var h = _JSServer.Execute(hScript, true))
                                    {
                                    } // end using handle returned by execute
                                    _JSServer.DoIdleNotification();
                                    Thread.Sleep(1);
                                    i++;
                                    if (i % 1000 == 0)
                                    {
                                        GC.Collect();
                                        GC.WaitForPendingFinalizers();
                                        _JSServer.ForceV8GarbageCollection();
                                        i = 0;
                                    }
                                } // end infinite loop
                            }
                            catch (OutOfMemoryException ex)
                            {
                                Console.WriteLine(ex);
                                Console.ReadKey();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                                Console.ReadKey();
                            }
                            //?catch
                            //{
                            //    Console.WriteLine("We caught something");
                            //    Console.ReadKey();
                            //}
                        }
                        else if (lcInput.StartsWith(@"\"))
                        {
                            Console.WriteLine(@"Invalid console command. Type '\help' to see available commands.");
                        }
                        else
                        {
                            Console.WriteLine();

                            try
                            {
                                var result = _JSServer.Execute(input, "V8.NET Console");
                                Console.WriteLine(result.AsString);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine();
                                Console.WriteLine();
                                Console.WriteLine(Exceptions.GetFullErrorMessage(ex));
                                Console.WriteLine();
                                Console.WriteLine("Error!  Press any key to continue ...");
                                Console.ReadKey();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine(Exceptions.GetFullErrorMessage(ex));
                        Console.WriteLine();
                        Console.WriteLine("Error!  Press any key to continue ...");
                        Console.ReadKey();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine(Exceptions.GetFullErrorMessage(ex));
                Console.WriteLine();
                Console.WriteLine("Error!  Press any key to exit ...");
                Console.ReadKey();
            }

            if (_TitleUpdateTimer != null)
                _TitleUpdateTimer.Dispose();
        }

        static void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
        }
    }
}

public class SomeMethods
{
    public static void StaticDoNothing()
    {
    }
    public void InstanceDoNothing()
    {
    }
}

public enum TestEnum
{
    A = 1,
    B = 2
}

public class GenericTest<T, T2>
{
    public T Value;
    public T2 Value2;
}

[ScriptObject("Sealed_Object", ScriptMemberSecurity.Permanent)]
public sealed class SealedObject : IV8NativeObject
{
    public static TestEnum _StaticField = TestEnum.A;
    public static TestEnum StaticField { get { return _StaticField; } }

    int _Value;
    public int this[int index] { get { return _Value; } set { _Value = value; } }

    public Uri URI;
    public void SetURI(Uri uri) { URI = uri; }

    public int? FieldA = 1;
    public string FieldB = "!!!";
    public int? PropA { get { return FieldA; } }
    public string PropB { get { return FieldB; } }
    public InternalHandle H1 = InternalHandle.Empty;
    public Handle H2 = Handle.Empty;
    public V8Engine Engine;

    public SealedObject(InternalHandle h1, InternalHandle h2)
    {
        H1.Set(h1);
        H2 = h2;
    }

    public string Test(int a, string b) { FieldA = a; FieldB = b; return a + "_" + b; }
    public InternalHandle SetHandle1(InternalHandle h) { return H1.Set(h); }
    public Handle SetHandle2(Handle h) { return H2.Set(h); }
    public InternalHandle SetEngine(V8Engine engine) { Engine = engine; return Engine.GlobalObject; }

    public void Test<t2, t>(t2 a, string b) { }
    public void Test<t2, t>(t a, string b) { }

    public string Test(string b, int a = 1) { FieldA = a; FieldB = b; return b + "_" + a; }

    public void Test(params string[] s) { Console.WriteLine(string.Join("", s)); }

    public object[] TestD<T1, T2>() { return new object[2] { typeof(T1), typeof(T2) }; }
    public int[] TestE(int i1, int i2) { return new int[2] { i1, i2 }; }

    public void Initialize(V8NativeObject owner, bool isConstructCall, params InternalHandle[] args)
    {
    }

    public void OnDispose()
    {
    }
}

/// <summary>
/// This is a custom implementation of 'V8Function' (which is not really necessary, but done as an example).
/// </summary>
public class V8DotNetTesterFunction : V8Function
{
    public override ObjectHandle Initialize(bool isConstructCall, params InternalHandle[] args)
    {
        Callback = ConstructV8DotNetTesterWrapper;

        return base.Initialize(isConstructCall, args);
    }

    public InternalHandle ConstructV8DotNetTesterWrapper(V8Engine engine, bool isConstructCall, InternalHandle _this, params InternalHandle[] args)
    {
        return isConstructCall ? engine.GetObject<V8DotNetTesterWrapper>(_this, true, false).Initialize(isConstructCall, args).AsInternalHandle : InternalHandle.Empty;
        // (note: V8DotNetTesterWrapper would cause an error here if derived from V8ManagedObject)
    }
}

/// <summary>
/// When "new SomeType()"  is executed within JavaScript, the native V8 auto-generates objects that are not based on templates.  This means there is no way
/// (currently) to set interceptors to support IV8Object objects; However, 'V8NativeObject' objects are supported, so I'm simply creating a custom one here.
/// </summary>
public class V8DotNetTesterWrapper : V8NativeObject // (I can also implement IV8NativeObject instead here)
{
    V8DotNetTester _Tester;

    public override ObjectHandle Initialize(bool isConstructCall, params InternalHandle[] args)
    {
        _Tester = Engine.CreateObjectTemplate().CreateObject<V8DotNetTester>();
        SetProperty("tester", _Tester); // (or _Tester.Handle works also)
        return Handle;
    }
}

public class V8DotNetTester : V8ManagedObject
{
    IV8Function _MyFunc;

    public override ObjectHandle Initialize(bool isConstructCall, params InternalHandle[] args)
    {
        base.Initialize(isConstructCall, args);

        Console.WriteLine("\r\nInitializing V8DotNetTester ...\r\n");

        Console.WriteLine("Creating test property 1 (adding new JSProperty directly) ...");

        var myProperty1 = new JSProperty(Engine.CreateValue("Test property 1"));
        this.Properties.Add("testProperty1", myProperty1);

        Console.WriteLine("Creating test property 2 (adding new JSProperty using the IV8ManagedObject interface) ...");

        var myProperty2 = new JSProperty(Engine.CreateValue(true));
        this["testProperty2"] = myProperty2;

        Console.WriteLine("Creating test property 3 (reusing JSProperty instance for property 1) ...");

        // Note: This effectively links property 3 to property 1, so they will both always have the same value, even if the value changes.
        this.Properties.Add("testProperty3", myProperty1); // (reuse a value)

        Console.WriteLine("Creating test property 4 (just creating a 'null' property which will be intercepted later) ...");

        this.Properties.Add("testProperty4", JSProperty.Empty);

        Console.WriteLine("Creating test property 5 (test the 'this' overload in V8ManagedObject, which will set/update property 5 without calling into V8) ...");

        this["testProperty5"] = (JSProperty)Engine.CreateValue("Test property 5");

        Console.WriteLine("Creating test property 6 (using a dynamic property) ...");

        InternalHandle strValHandle = Engine.CreateValue("Test property 6");
        this.AsDynamic.testProperty6 = strValHandle;

        Console.WriteLine("Creating test function property 1 ...");

        var funcTemplate1 = Engine.CreateFunctionTemplate("_" + GetType().Name + "_");
        _MyFunc = funcTemplate1.GetFunctionObject(TestJSFunction1);
        this.AsDynamic.testFunction1 = _MyFunc;

        Console.WriteLine("\r\n... initialization complete.");

        return Handle;
    }

    public void Execute()
    {
        Console.WriteLine("Testing pre-compiled script ...\r\n");

        Engine.Execute("var i = 0;");
        var pcScript = Engine.Compile("i = i + 1;");
        for (var i = 0; i < 100; i++)
            Engine.Execute(pcScript, true);

        Engine.ConsoleExecute("assert('Testing i==100', i, 100)", this.GetType().Name, true);

        Console.WriteLine("\r\nTesting JS function call from native side ...\r\n");

        ObjectHandle f = (ObjectHandle)Engine.ConsoleExecute("f = function(arg1) { return arg1; }");
        var fresult = f.StaticCall(Engine.CreateValue(10));
        Console.WriteLine("f(10) == " + fresult);
        if (fresult != 10)
            throw new Exception("CLR handle call to native function failed.");

        Console.WriteLine("\r\nTesting JS function call exception from native side ...\r\n");

        f = (ObjectHandle)Engine.ConsoleExecute("f = function() { return thisdoesntexist; }");
        fresult = f.StaticCall();
        Console.WriteLine("f() == " + fresult);
        if (!fresult.ToString().Contains("Error"))
            throw new Exception("Native exception error did not come through.");
        else
            Console.WriteLine("Expected exception came through - pass.\r\n");

        Console.WriteLine("\r\nPress any key to begin testing properties on 'this.tester' ...\r\n");
        Console.ReadKey();

        // ... test the non-function/object propertied ...

        Engine.ConsoleExecute("assert('Testing property testProperty1', tester.testProperty1, 'Test property 1')", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing property testProperty2', tester.testProperty2, true)", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing property testProperty3', tester.testProperty3, tester.testProperty1)", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing property testProperty4', tester.testProperty4, '" + MyClassProperty4 + "')", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing property testProperty5', tester.testProperty5, 'Test property 5')", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing property testProperty6', tester.testProperty6, 'Test property 6')", this.GetType().Name, true);

        Console.WriteLine("\r\nAll properties initialized ok.  Testing property change ...\r\n");

        Engine.ConsoleExecute("assert('Setting testProperty2 to integer (123)', (tester.testProperty2=123), 123)", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Setting testProperty2 to number (1.2)', (tester.testProperty2=1.2), 1.2)", this.GetType().Name, true);

        // ... test non-function object properties ...

        Console.WriteLine("\r\nSetting property 1 to an object, which should also set property 3 to the same object ...\r\n");

        Engine.VerboseConsoleExecute("dump(tester.testProperty1 = {x:0});", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing property testProperty1.x === testProperty3.x', tester.testProperty1.x, tester.testProperty3.x)", this.GetType().Name, true);

        // ... test function properties ...

        Engine.ConsoleExecute("assert('Testing property tester.testFunction1 with argument 100', tester.testFunction1(100), 100)", this.GetType().Name, true);

        // ... test function properties ...

        Console.WriteLine("\r\nCreating 'this.obj1' with a new instance of tester.testFunction1 and testing the expected values ...\r\n");

        Engine.VerboseConsoleExecute("obj1 = new tester.testFunction1(321);");
        Engine.ConsoleExecute("assert('Testing obj1.x', obj1.x, 321)", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing obj1.y', obj1.y, 0)", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing obj1[0]', obj1[0], 100)", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing obj1[1]', obj1[1], 100.2)", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing obj1[2]', obj1[2], '300')", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing obj1[3] is undefined?', obj1[3] === undefined, true)", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing obj1[4].toUTCString()', obj1[4].toUTCString(), 'Wed, 02 Jan 2013 03:04:05 GMT')", this.GetType().Name, true);

        Console.WriteLine("\r\nPress any key to test dynamic handle property access ...\r\n");
        Console.ReadKey();

        // ... get a handle to an in-script only object and test the dynamic handle access ...

        Engine.VerboseConsoleExecute("var obj = { x:0, y:0, o2:{ a:1, b:2, o3: { x:0 } } }", this.GetType().Name, true);
        dynamic handle = Engine.DynamicGlobalObject.obj;
        handle.x = 1;
        handle.y = 2;
        handle.o2.o3.x = 3;
        Engine.ConsoleExecute("assert('Testing obj.x', obj.x, 1)", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing obj.y', obj.y, 2)", this.GetType().Name, true);
        Engine.ConsoleExecute("assert('Testing obj.o2.o3.x', obj.o2.o3.x, 3)", this.GetType().Name, true);

        Console.WriteLine("\r\nPress any key to test handle reuse ...");
        Console.WriteLine("(1000 native object handles will be created, but one V8NativeObject wrapper will be used)");
        Console.ReadKey();
        Console.Write("Running ...");
        var obj = new V8NativeObject();
        for (var i = 0; i < 1000; i++)
        {
            obj.Handle = Engine.GlobalObject.GetProperty("obj"); // (note here that 'obj.Handle' is a *property* and must be assigned, instead of calling 'Set()', in order to change the object's handle)
        }
        Console.WriteLine(" Done.");
    }

    public override InternalHandle NamedPropertyGetter(ref string propertyName)
    {
        if (propertyName == "testProperty4")
            return Engine.CreateValue(MyClassProperty4);

        return base.NamedPropertyGetter(ref propertyName);
    }

    public string MyClassProperty4 { get { return this.GetType().Name; } }

    public InternalHandle TestJSFunction1(V8Engine engine, bool isConstructCall, InternalHandle _this, params InternalHandle[] args)
    {
        // ... there can be two different returns based on the call mode! ...
        // (tip: if a new object is created and returned instead (such as V8ManagedObject or an object derived from it), then that object will be the new object (instead of "_this"))
        if (isConstructCall)
        {
            var obj = engine.GetObject(_this);
            obj.AsDynamic.x = args[0];
            ((dynamic)obj).y = 0; // (native objects in this case will always be V8NativeObject dynamic objects)
            obj.SetProperty(0, engine.CreateValue(100));
            obj.SetProperty("1", engine.CreateValue(100.2));
            obj.SetProperty("2", engine.CreateValue("300"));
            obj.SetProperty(4, engine.CreateValue(new DateTime(2013, 1, 2, 3, 4, 5, DateTimeKind.Utc)));
            return _this;
        }
        else return args.Length > 0 ? args[0] : InternalHandle.Empty;
    }
}

public class SamplePointFunctionTemplate : FunctionTemplate
{
    public SamplePointFunctionTemplate() { }

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }
}



//!!public class __UsageExamplesScratchArea__ // (just here to help with writing examples for documentation, etc.)
//{
//    public void Examples()
//    {
//        var v8Engine = new V8Engine();

//        v8Engine.WithContextScope = () =>
//        {
//            // Example: Creating an instance.

//            var result = v8Engine.Execute("/* Some JavaScript Code Here */", "My V8.NET Console");
//            Console.WriteLine(result.AsString);
//            Console.WriteLine("Press any key to continue ...");
//            Console.ReadKey();

//            Handle handle = v8Engine.CreateInteger(0);
//            var handle = (Handle)v8Engine.CreateInteger(0);

//            var handle = v8Engine.CreateInteger(0);
//            // (... do something with it ...)
//            handle.Dispose();

//            // ... OR ...

//            using (var handle = v8Engine.CreateInteger(0))
//            {
//                // (... do something with it ...)
//            }

//            // ... OR ...

//            InternalHandle handle = InternalHandle.Empty;
//            try
//            {
//                handle = v8Engine.CreateInteger(0);
//                // (... do something with it ...)
//            }
//            finally { handle.Dispose(); }

//            handle.Set(anotherHandle);
//            // ... OR ...
//            var handle = anotherHandle.Clone(); // (note: this is only valid when initializing a variable)

//            var handle = v8Engine.CreateInteger(0);
//            var handle2 = handle;

//            handle.Set(anotherHandle.Clone());

//            // Example: Setting global properties.

//        };
//    }
//}