#if NETSTANDARD
using Microsoft.Extensions.Hosting;
#else
//using Microsoft.AspNetCore.Http;
using System.Web;
#endif
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace V8.Net
{
    public static class Loader
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string fileName);

#if NETSTANDARD
        public const int RTLD_NOW = 0x002;
        [DllImport("libdl")] // (could be "libdl.so.2" also: https://github.com/mellinoe/nativelibraryloader/issues/2#issuecomment-414476716)
        public static extern IntPtr DLOpen(string fileName, int flags);

        [DllImport("libdl.so.2")]
        public static extern IntPtr DLOpen2(string fileName, int flags);

        /// <summary>
        ///     Supports ASP.Net Core hosting environments.  If set, and <see cref="AlternateRootSubPath"/> is a relative path, then
        ///     the relative path is combined with the context directory path.
        /// </summary>
        public static IHostingEnvironment HostingEnvironment;
#endif

        static bool TryLoad(string rootPath)
        {
            try
            {
                var libname = "V8_Net_Proxy_" + (Environment.Is64BitProcess ? "x64" : "x86");
                var filepath = Path.Combine(rootPath, libname);
                string filepathPlusExt = null;
#if NETSTANDARD
                // ... check 'codebaseuri' - this is the *original* assembly location before it was shadow-copied for ASP.NET pages ...

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    try { filepathPlusExt = filepath + ".dylib"; if (!File.Exists(filepathPlusExt)) return false; DLOpen(filepathPlusExt, RTLD_NOW); } catch (Exception ex) { DLOpen2(filepathPlusExt, RTLD_NOW); }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    try { filepathPlusExt = filepath + ".os"; if (!File.Exists(filepathPlusExt)) return false; DLOpen(filepathPlusExt, RTLD_NOW); } catch (Exception ex) { DLOpen2(filepathPlusExt, RTLD_NOW); }
                else
                {
                    filepathPlusExt = filepath + ".dll";
                    if (!File.Exists(filepathPlusExt)) return false;
                    LoadLibrary(filepath + ".dll");
                }
                return true;
#else
                filepathPlusExt = filepath + ".dll";
                if (!File.Exists(filepathPlusExt)) return false;
                LoadLibrary(filepath + ".dll");
                return true;
#endif
            }
            catch { return false; }
        }

        /// <summary>
        /// The sub-folder that is the root for the dependent libraries (x86 and x64).  This is set to "V8.NET" by default.
        /// <para>This setting allows copying the V8.NET libraries to a project, and having the assemblies "Copy if never"
        /// automatically. Typically the path is relative, but an absolute root path can also be given.</para>
        /// </summary>
        public static string AlternateRootSubPath = "V8.NET";
        // ... check for an 'AlternateRootSubPath' sub-folder, which allows for copying the assemblies to a child folder of the project ...
        // (DLLs in the "x86" and "x64" folders of this child folder can be set to "Copy if newer" using this method)

        static bool _LocalPathEnvUpdated;

        static string _WebHostPath; // (only if detected/given)

        static Loader() // (note: don't access 'ValidPaths' here, because 'AlternateRootSubPath' CANNOT be set by the user before this)
        {
            //?AppDomain.CurrentDomain.AssemblyResolve += _Resolver;
        }

        static void _CheckLocalPathUpdated()
        {
            if (!_LocalPathEnvUpdated)
            {
                try
                {
                    // ... add the search location to the path so "Assembly.LoadFrom()" can find other dependant assemblies if needed ...
                    // ... attempt to update environment variable automatically for the native DLLs ...
                    // (see: http://stackoverflow.com/questions/7996263/how-do-i-get-iis-to-load-a-native-dll-referenced-by-my-wcf-service
                    //   and http://stackoverflow.com/questions/344608/unmanaged-dlls-fail-to-load-on-asp-net-server)

                    var path = Environment.GetEnvironmentVariable("PATH"); // TODO: Detect other systems if necessary, such as Linux.
                    var newPaths = string.Join(";", ValidPaths.ToArray()) + ";";
                    Environment.SetEnvironmentVariable("PATH", newPaths + path);
                    _LocalPathEnvUpdated = true;
                }
                catch { }
            }
        }

        public static Assembly GetExistingAssembly(string assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => string.Equals(a.FullName.Split(',')[0], assemblyName, StringComparison.CurrentCultureIgnoreCase));
        }

        public static bool IsLoaded(string assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Any(a => string.Equals(a.FullName.Split(',')[0], assemblyName, StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        ///     This is the main method to call to resolve all dependencies. This MUST be the first method called, which is
        ///     automatically called by the V8Engine's static constructor. You can also call this directly in a "main" or "startup"
        ///     file before touching any V8.Net type.
        /// </summary>
        public static void ResolveDependencies()
        {
            _CheckLocalPathUpdated();

            // ... force load the required assemblies now (this is assuming the method is called from the calling type's static constructor or main startup) ...

            //if (!IsLoaded("V8.Net.SharedTypes"))
            //    Assembly.Load("V8.Net.SharedTypes");

            //if (Environment.Is64BitProcess)
            //{
            //    if (!IsLoaded("V8.Net.Proxy.Interface.x64"))
            //        Assembly.Load("V8.Net.Proxy.Interface.x64");
            //}
            //else
            //{
            //    if (!IsLoaded("V8.Net.Proxy.Interface.x86"))
            //        Assembly.Load("V8.Net.Proxy.Interface.x86");
            //}

            //if (!IsLoaded("V8.Net"))
            //    Assembly.Load("V8.Net");

            var paths = ValidPaths.ToArray();

            foreach (var path in paths)
                if (TryLoad(path))
                    return;

            var bitStr = Environment.Is64BitProcess ? "x64" : "x86";
            var msg = $"Could not locate the required V8 native libraries.  V8.NET is running in the '" + bitStr + "' mode.  Some areas to check: " + Environment.NewLine
                + "1. Did you download the DLLs from a ZIP file? If so you may have to unblock the file. On Windows, you must open the file properties of the zip file and 'Unblock' it BEFORE extracting the files." + Environment.NewLine
                + "2. Review the searched paths in the nested errors below and make sure the desired path is accessible to the application ";

            if (!string.IsNullOrWhiteSpace(_WebHostPath))
                msg += "pool identity (usually Read & Execute for 'IIS_IUSRS', or a similar user/group)" + Environment.NewLine;
            else
                msg += "for loading the required libraries under the current program's security context." + Environment.NewLine;

            msg += " Paths searched: " + Environment.NewLine + " * " + string.Join(Environment.NewLine + " * ", paths);

            System.Diagnostics.Debug.WriteLine(msg, "WARNING");

            if (Environment.UserInteractive)
                Console.WriteLine(msg);

            if (System.Diagnostics.Debugger.IsAttached)
                throw new DllNotFoundException(msg + Environment.NewLine + "This exception is thrown as a notice since a debugger is attached. You can always use a try..catch block to ignore it.");
        }

        /// <summary> Compiles all the valid search paths, in proper order, based on the current settings. </summary>
        /// <value> The valid paths. </value>
        public static IEnumerable<string> ValidPaths => _ValidPaths.Distinct();

        static IEnumerable<string> _ValidPaths
        {
            get
            {
                string currentDir = Directory.GetCurrentDirectory();

                // ... always start with the current directory ...

                yield return currentDir;

                // ... check for an 'AlternateRootSubPath' path if explicitly specified ...

                if (!string.IsNullOrEmpty(AlternateRootSubPath))
                    foreach (var path in _GetSubPaths(currentDir, AlternateRootSubPath, false))
                        yield return path;

                // ... check for a bin folder for ASP.NET sites ...

#if !NETSTANDARD // (NETSTANDARD is set for assemblies targeting .Net Standard; which supports BOTH .Net Core and .Net Full)
                _WebHostPath = HttpContext.Current.Server.MapPath("~/bin");
#else
                _WebHostPath = HostingEnvironment?.ContentRootPath;
#endif
                if (!string.IsNullOrWhiteSpace(_WebHostPath))
                    foreach (var path in _GetSubPaths(_WebHostPath))
                        yield return path;

                // ... check 'codebaseuri' - this is the *original* assembly location before it was shadow-copied for ASP.NET pages ...

                var codebaseuri = Assembly.GetExecutingAssembly().CodeBase;
                if (Uri.TryCreate(codebaseuri, UriKind.Absolute, out Uri codebaseURI))
                    foreach (var path in _GetSubPaths(Path.GetDirectoryName(codebaseURI.LocalPath)))
                        yield return path;

                // ... if not found, try the executing assembly's own location ...
                // (note: this is not done first, as the executing location might be in a cache location and not the original location!!!)

                var thisAssmeblyLocation = Assembly.GetExecutingAssembly().Location; // (may be a shadow-copy path!)
                if (!string.IsNullOrEmpty(thisAssmeblyLocation))
                    foreach (var path in _GetSubPaths(Path.GetDirectoryName(thisAssmeblyLocation)))
                        yield return path;

                // ... finally, try the current directory ...

                foreach (var path in _GetSubPaths(currentDir, null, false))
                    yield return path;
            }
        }

        /// <summary> Returns a list of sub-paths to try for the given root path. </summary>
        /// <param name="rootPath"> Full pathname of the root file. </param>
        /// <param name="subPath"> (Optional) Full pathname of the sub file. </param>
        /// <param name="includeRoot"> (Optional) True to include the root path. </param>
        /// <returns> An enumerator that allows foreach to be used to process the valid paths in this collection. </returns>
        static IEnumerable<string> _GetSubPaths(string rootPath, string subPath = null, bool includeRoot = true)
        {
            if (string.IsNullOrWhiteSpace(rootPath) && !string.IsNullOrWhiteSpace(subPath))
            {
                rootPath = subPath;
                subPath = null;
            }

            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                if (includeRoot)
                    yield return rootPath;

                // ... do a fixed check (always) for a nested "V8.NET" folder ...

                if (rootPath.TrimEnd('\\').Split('\\').LastOrDefault()?.ToUpper() != "V8.NET" && subPath == null)
                    yield return Path.Combine(rootPath, "V8.NET");

                // ... check for platform sub-folders as well in this root path location ...

                string platformDir = Environment.Is64BitProcess ? platformDir = Path.Combine(rootPath, "x64") : platformDir = Path.Combine(rootPath, "x86");

                yield return platformDir;

                // ... finally check for commonly named "lib" folders in projects ...

                if (rootPath.ToUpper() != "LIB" && subPath == null)
                    yield return Path.Combine(rootPath, "lib");

                if (rootPath.ToUpper() != "LIBS" && subPath == null)
                    yield return Path.Combine(rootPath, "libs");

                if (rootPath.ToUpper() != "LIBRARIES" && subPath == null)
                    yield return Path.Combine(rootPath, "libraries");

                if (!string.IsNullOrEmpty(subPath))
                {
                    //  ... add and check relative alt paths only; 'GetPathRoot()' looks for '?:\' and '\' ...

                    var subPathIsRelative = string.IsNullOrEmpty(Path.GetPathRoot(subPath));

                    if (subPathIsRelative) // ... alternate path is relative, so combine with the given root ...
                        foreach (var path in _GetSubPaths(Path.Combine(rootPath, subPath)))
                            yield return path;
                    else
                    { // (sub path is also a root)
                        foreach (var path in _GetSubPaths(subPath))
                            yield return path;
                    }
                }
            }
        }

        static IOException _TryLoadProxyInterface(string path, string filename, Exception lastError, out Assembly assembly)
        {
            assembly = null;

            //// ... validate access to the root folder ...
            //var permission = new FileIOPermission(FileIOPermissionAccess.Read, assemblyRoot);
            //var permissionSet = new PermissionSet(PermissionState.None);
            //permissionSet.AddPermission(permission);
            //if (!permissionSet.IsSubsetOf(AppDomain.CurrentDomain.PermissionSet))

            if (!Directory.Exists(path))
                return new DirectoryNotFoundException("The path '" + path + "' does not exist, or is not accessible.");

            var filePath = Path.Combine(path, filename);

            try
            {
                if (File.Exists(filePath))
                    assembly = Assembly.LoadFrom(filePath);
                else
                {
                    var msg = "'" + filePath + "'";
                    if (lastError != null)
                        return new FileNotFoundException(msg + Environment.NewLine + Environment.NewLine, lastError);
                    else
                        return new FileNotFoundException(msg + Environment.NewLine + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                var msg = "Failed to load '" + filePath + "': " + ex.GetFullErrorMessage();
                if (lastError != null)
                    return new FileNotFoundException(msg + Environment.NewLine + Environment.NewLine, lastError);
                else
                    return new FileNotFoundException(msg + Environment.NewLine + Environment.NewLine);
            }

            return null;
        }

        //        /// <summary>
        //        /// This method watches for and attempts to resolve any assembly requests with names beginning with "V8.Net".
        //        /// </summary>
        //        static Assembly _Resolver(object sender, ResolveEventArgs args)
        //        {
        //            _CheckLocalPathUpdated();

        //            string name = args.Name;

        //            if (name.StartsWith("V8.Net.Proxy.Interface", StringComparison.CurrentCultureIgnoreCase))
        //                name = "V8.Net.Proxy.Interface.{bits}"; // (the system is looking for the proxy interface)
        //            else if (name.StartsWith("V8.Net", StringComparison.CurrentCultureIgnoreCase))
        //                name = args.Name.Split(',')[0]; // (there is some other assembly the system is trying to find; perhaps due to ASP.NET shadow-copying)
        //            else
        //                return null; // (we will not deal with non-V8.Net assemblies)

        //            if (!string.IsNullOrEmpty(name))
        //            {
        //                if (Environment.Is64BitProcess)
        //                    name = name.Replace("{bits}", "x64");
        //                else
        //                    name = name.Replace("{bits}", "x86");
        //#if NETSTANDARD
        //                string filename = name
        //                    + (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".dylib" :
        //                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? ".os" : ".dll");
        //#else
        //                string filename = name + ".dll"; // (not sure if anyone uses MONO 4.0+ still, but if so, this will need detection [not supported yet])
        //#endif
        //                IOException error = null;
        //                Assembly assembly = GetExistingAssembly(name);
        //                if (assembly != null) return assembly; // (already exists!)

        //                // ... first check for a bin folder for ASP.NET sites ...

        //                foreach (var path in ValidPaths)
        //                {
        //                    error = _TryLoadProxyInterface(path, filename, error, out assembly); // (each 'error' feeds as "inner" to the next one)
        //                    if (assembly != null) return assembly;
        //                }

        //                var bitStr = Environment.Is64BitProcess ? "x64" : "x86";
        //                var msg = $"Failed to load '{name}'.  V8.NET is running in the '" + bitStr + "' mode.  Some areas to check: " + Environment.NewLine
        //                    // (these are now statically linked) + "1. The VC++ 2017 redistributable libraries are included, but if missing  for some reason, download and install from the Microsoft Site." + Environment.NewLine
        //                    + "1. Did you download the DLLs from a ZIP file? If so you may have to unblock the file. On Windows, you must open the file properties of the zip file and 'Unblock' it BEFORE extracting the files." + Environment.NewLine;

        //                msg += "2. Review the searched paths in the nested errors below and make sure the desired path is accessible to the application ";

        //                if (!string.IsNullOrWhiteSpace(_WebHostPath))
        //                    msg += "pool identity (usually Read & Execute for 'IIS_IUSRS', or a similar user/group)" + Environment.NewLine;
        //                else
        //                    msg += "for loading the required libraries under the current program's security context." + Environment.NewLine;

        //                if (error != null)
        //                    throw new IOException(msg + Environment.NewLine, error);
        //                else
        //                    throw new IOException(msg + Environment.NewLine);
        //            }

        //            return null;
        //        }
    }

}
