// Note: V8Engine now has two static properties available *only* when the engine is compiled in DEBUG mode with TRACKHANDLES defined:
//    V8Engine.AllInternalHandlesEverCreated: All InternalHandle values set with native handle proxies since application start.
//    V8Engine.AllHandlesEverCreated: All Handle/ObjectHandle objects created since application start.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;

namespace V8.Net
{
    class Program
    {
        static V8Engine _JSServer;
        static System.Timers.Timer _TitleUpdateTimer;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                Console.WriteLine("V8.Net Version: " + V8Engine.Version);

                Console.Write(Environment.NewLine + "Creating a V8Engine instance ...");
                _JSServer = new V8Engine();
                _JSServer.SetFlagsFromCommandLine(args);
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
                            + " / Pending Disposal: " + _JSServer.TotalHandlesPendingDisposal
                            + " / Pending Native GC: " + _JSServer.TotalHandlesPendingV8GC
                            + " / Cached: " + _JSServer.TotalHandlesCached
                            + " / In Use: " + (_JSServer.TotalHandlesInUse) + ")";
                    else
                        Console.Title = "V8.Net Console - Shutting down...";
                };
                _TitleUpdateTimer.Start();

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
                            Console.WriteLine(@"\init - Setup environment for testing (adds 'dump()' and 'assert()'.");
                            Console.WriteLine(@"\flags --flag1 --flag2 --etc... - Sets one or more flags (use '\flags --help' for more details).");
                            Console.WriteLine(@"\cls - Clears the screen.");
                            Console.WriteLine(@"\gc - Triggers garbage collection (for testing purposes).");
                            Console.WriteLine(@"\v8gc - Triggers garbage collection in V8 (for testing purposes).");
                            Console.WriteLine(@"\gctest - Runs a simple GC test against V8.NET and the native V8 engine.");
                            Console.WriteLine(@"\handles - Dumps the current list of known handles.");
                            Console.WriteLine(@"\speedtest - Runs a simple test script to test V8.NET performance with the V8 engine.");
                            Console.WriteLine(@"\exit - Exists the console.");
                        }
                        else if (lcInput == @"\init")
                        {
                            Console.WriteLine(Environment.NewLine + "Creating a global 'dump(obj)' function to dump properties of objects (one level only) ...");
                            _JSServer.ConsoleExecute(@"dump = function(o) { var s=''; if (typeof(o)=='undefined') return 'undefined';"
                                + @" if (typeof o.valueOf=='undefined') return ""'valueOf()' is missing on '""+(typeof o)+""' - if you are inheriting from V8ManagedObject, make sure you are not blocking the property."";"
                                + @" if (typeof o.toString=='undefined') return ""'toString()' is missing on '""+o.valueOf()+""' - if you are inheriting from V8ManagedObject, make sure you are not blocking the property."";"
                                + @" for (var p in o) {var ov='', pv=''; try{ov=o.valueOf();}catch(e){ov='{error: '+e.message+': '+dump(o)+'}';} try{pv=o[p];}catch(e){pv=e.message;} s+='* '+ov+'.'+p+' = ('+pv+')\r\n'; } return s; }");

                            Console.WriteLine(Environment.NewLine + "Creating a global 'assert(msg, a,b)' function for property value assertion ...");
                            _JSServer.ConsoleExecute(@"assert = function(msg,a,b) { msg += ' ('+a+'==='+b+'?)'; if (a === b) return msg+' ... Ok.'; else throw msg+' ... Failed!'; }");
                        }
                        else if (lcInput == @"\cls")
                            Console.Clear();
                        else if (lcInput == @"\flags" || lcInput.StartsWith(@"\flags "))
                        {
                            string flags = lcInput.Substring(6).Trim();
                            if (flags.Length > 0)
                                _JSServer.SetFlagsFromString(flags);
                            else
                                Console.WriteLine(@"You did not specify any options.");
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
                        else if (lcInput == @"\gc")
                        {
                            Console.Write(Environment.NewLine + "Forcing garbage collection ... ");
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            Console.WriteLine("Done.\r\n");
                            Console.WriteLine("Currently Used Memory: " + GC.GetTotalMemory(true));
                        }
                        else if (lcInput == @"\v8gc")
                        {
                            Console.Write(Environment.NewLine + "Forcing V8 garbage collection ... ");
                            _JSServer.ForceV8GarbageCollection();
                            Console.WriteLine("Done.\r\n");
                        }
                        else if (lcInput == @"\handles")
                        {
                            Console.Write(Environment.NewLine + "Active handles list ... " + Environment.NewLine);

                            foreach (var h in _JSServer.Handles_Active)
                            {
                                Console.WriteLine(" * " + h.Description.Replace(Environment.NewLine, "\\r\\n"));
                            }

                            Console.Write(Environment.NewLine + "Managed side dispose-ready handles (usually due to a GC attempt) ... " + Environment.NewLine);

                            foreach (var h in _JSServer.Handles_ManagedSideDisposeReady)
                            {
                                Console.WriteLine(" * " + h.Description.Replace(Environment.NewLine, "\\r\\n"));
                            }

                            Console.Write(Environment.NewLine + "Native side V8 handles now marked 'weak' (though may still be in use) ... " + Environment.NewLine);

                            foreach (var h in _JSServer.Handles_NativeSideWeak)
                            {
                                Console.WriteLine(" * " + h.Description.Replace(Environment.NewLine, "\\r\\n"));
                            }

                            Console.Write(Environment.NewLine + "Native side V8 handles that are now cached for reuse ... " + Environment.NewLine);

                            foreach (var h in _JSServer.Handles_DisposedAndCached)
                            {
                                Console.WriteLine(" * " + h.Description.Replace(Environment.NewLine, "\\r\\n"));
                            }

                            Console.WriteLine(Environment.NewLine + "Done." + Environment.NewLine);
                        }
                        else if (lcInput == @"\gctest")
                        {
                            Console.WriteLine("\r\nTesting garbage collection ... ");

                            V8NativeObject tempObj;
                            InternalHandle internalHandle = InternalHandle.Empty;
                            int i;

                            Console.WriteLine("Setting 'tempObj' to a new managed object ...");

                            _JSServer.DynamicGlobalObject.tempObj = tempObj = _JSServer.CreateObject<V8NativeObject>();
                            internalHandle = InternalHandle.GetUntrackedHandleFromObject(tempObj);

                            Console.WriteLine("Current generation of 'tempObj' instance: " + GC.GetGeneration(tempObj));

                            Console.WriteLine("Nullifying 'tempObj' to release the object ...");
                            tempObj = null;

                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            // (we wait for the object to be sent for disposal by the worker)

                            Console.WriteLine("Generation of 'tempObj' instance after collect: " + GC.GetGeneration(_JSServer.GetObjectByID(internalHandle.ObjectID)));

                            for (i = 0; i < 3000 && !internalHandle.IsDisposing; i++)
                                System.Threading.Thread.Sleep(1); // (just wait for the worker)

                            if (!internalHandle.IsDisposing)
                                throw new Exception("The temp object's handle is still not pending disposal ... something is wrong.");

                            Console.WriteLine("Success! The temp object's handle is going through the disposal process.");
                            Console.WriteLine("Clearing the handle object reference next ...");

                            // object handles will finally be disposed when the native V8 GC calls back regarding them ...

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

                            if (!internalHandle.IsDisposed) throw new Exception("Managed object's handle did not dispose.");
                            // (note: this call is only valid as long as no more objects are created before this point)
                            Console.WriteLine("Success! The managed V8NativeObject native handle is now disposed.");
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
                        else if (lcInput.StartsWith(@"\"))
                        {
                            Console.WriteLine(@"Invalid console command. Type '\help' to see available commands.");
                        }
                        else
                        {
                            Console.WriteLine();

                            try
                            {
                                var result = _JSServer.Execute(input, "V8.NET Console", false, 5000);

                                Console.WriteLine(result.AsString);

                                if (result.WasTerminated)
                                    Console.WriteLine("The script took longer than 5 seconds to run and was aborted.");

                                result.Dispose();
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
