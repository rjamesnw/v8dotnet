using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;

namespace V8.Net
{
    public static class Loader
    {
        /// <summary>
        /// The sub-folder that is the root for the dependent libraries (x86 and x64).  This is set to "V8.NET" by default.
        /// <para>This setting allows copying the V8.NET libraries to a project, and having the assemblies "Copy if never"
        /// automatically. Typically the path is relative, but an absolute root path can also be given.</para>
        /// </summary>
        public static string AlternateRootSubPath = "V8.NET";

        static bool _LocalPathEnvUpdated;

        static Loader() // (note: don't access 'ValidPaths' here, because 'AlternateRootSubPath' CANNOT be set by the user before this)
        {
            AppDomain.CurrentDomain.AssemblyResolve += _Resolver;
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

                    var path = System.Environment.GetEnvironmentVariable("PATH"); // TODO: Detect other systems if necessary.
                    var newPaths = string.Join(";", ValidPaths.ToArray()) + ";";
                    System.Environment.SetEnvironmentVariable("PATH", newPaths + path);
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

        public static void ResolveDependencies()
        {
            _CheckLocalPathUpdated();

            if (!IsLoaded("V8.Net.SharedTypes"))
                Assembly.Load("V8.Net.SharedTypes");

            if (IntPtr.Size == 4)
            {
                if (!IsLoaded("V8.Net.Proxy.Interface.x86"))
                    Assembly.Load("V8.Net.Proxy.Interface.x86");
            }
            else
            {
                if (!IsLoaded("V8.Net.Proxy.Interface.x64"))
                    Assembly.Load("V8.Net.Proxy.Interface.x64");
            }

            if (!IsLoaded("V8.Net"))
                Assembly.Load("V8.Net");
        }

        public static IEnumerable<string> ValidPaths
        {
            get
            {
                return _ValidPaths.Distinct();
            }
        }

        /// <summary>
        /// Returns a list of valid paths to check for assemblies, in proper order.
        /// </summary>
        static IEnumerable<string> _ValidPaths
        {
            get
            {
                string currentDir = Directory.GetCurrentDirectory();

                // ... always start with the current directory ...

                yield return currentDir;

                // ... check for an 'AlternateRootSubPath' absolute path ...

                if (!string.IsNullOrEmpty(AlternateRootSubPath))
                    if (!String.IsNullOrEmpty(Path.GetPathRoot(AlternateRootSubPath)))
                        foreach (var path in _GetValidPaths(AlternateRootSubPath))
                            yield return path;

                // ... check for a bin folder for ASP.NET sites ...

                if (HttpContext.Current != null)
                    foreach (var path in _GetValidPaths(HttpContext.Current.Server.MapPath("~/bin")))
                        yield return path;

                // ... check 'codebaseuri' - this is the *original* assembly location before it was cached for ASP.NET pages ...

                var codebaseuri = Assembly.GetExecutingAssembly().CodeBase;
                Uri codebaseURI = null;
                if (Uri.TryCreate(codebaseuri, UriKind.Absolute, out codebaseURI))
                    foreach (var path in _GetValidPaths(Path.GetDirectoryName(codebaseURI.LocalPath)))
                        yield return path;

                // ... if not found, try the executing assembly's own location ...
                // (note: this is not done first, as the executing location might be in a cache location and not the original location!!!)

                var thisAssmeblyLocation = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(thisAssmeblyLocation))
                    foreach (var path in _GetValidPaths(Path.GetDirectoryName(thisAssmeblyLocation)))
                        yield return path;

                // ... finally, try the current directory ...

                foreach (var path in _GetValidPaths(currentDir, false))
                    yield return path;
            }
        }

        static IEnumerable<string> _GetValidPaths(string rootPath, bool includeRoot = true)
        {
            if (Directory.Exists(rootPath))
            {
                if (includeRoot)
                    yield return rootPath;

                // ... check for an 'AlternateRootSubPath' sub-folder, which allows for copying the assemblies to a child folder of the project ...
                // (DLLs in the "x86" and "x64" folders of this child folder can be set to "Copy if newer" using this method)
                if (!string.IsNullOrEmpty(AlternateRootSubPath))
                {
                    if (String.IsNullOrEmpty(Path.GetPathRoot(AlternateRootSubPath))) // (add and check relative alt paths only)
                        foreach (var path in _GetValidPaths(Path.Combine(rootPath, AlternateRootSubPath)))
                            yield return path;

                    // ... do a fixed check (always) for a nested "V8.NET" folder ...

                    if (AlternateRootSubPath.ToUpper() != "V8.NET")
                        foreach (var path in _GetValidPaths(Path.Combine(rootPath, "V8.NET")))
                            yield return path;
                }

                // ... check for platform folders in this root path location ...

                string platformDir;

                if (IntPtr.Size == 4)
                    platformDir = Path.Combine(rootPath, "x86");
                else
                    platformDir = Path.Combine(rootPath, "x64");

                if (Directory.Exists(platformDir))
                    yield return platformDir;
            }
        }

        static Exception _TryLoadProxyInterface(string path, string filename, Exception lastError, out Assembly assembly)
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

        /// <summary>
        /// This method watches for and attempts to resolve any assembly requests with names beginning with "V8.Net".
        /// </summary>
        static Assembly _Resolver(object sender, ResolveEventArgs args)
        {
            _CheckLocalPathUpdated();

            string name = args.Name;

            if (name.StartsWith("V8.Net.Proxy.Interface", StringComparison.CurrentCultureIgnoreCase))
                name = "V8.Net.Proxy.Interface.{platform}";
            else if (name.StartsWith("V8.Net", StringComparison.CurrentCultureIgnoreCase))
                name = args.Name.Split(',')[0];
            else
                name = null;

            if (!string.IsNullOrEmpty(name))
            {
                if (IntPtr.Size == 4)
                    name = name.Replace("{platform}", "x86");
                else
                    name = name.Replace("{platform}", "x64");

                string filename = name + ".dll";

                Exception error = null;
                Assembly assembly = GetExistingAssembly(name);
                if (assembly != null) return assembly; // (already exists!)

                // ... first check for a bin folder for ASP.NET sites ...

                foreach (var path in ValidPaths)
                {
                    error = _TryLoadProxyInterface(path, filename, error, out assembly);
                    if (assembly != null) return assembly;
                }

                var bitStr = IntPtr.Size == 8 ? "x64" : "x86";
                var msg = "Failed to load 'V8.Net.Proxy.Interface.x??.dll'.  V8.NET is running in the '" + bitStr + "' mode.  Some areas to check: " + Environment.NewLine
                    + "1. The VC++ 2012 redistributable libraries are included, but if missing  for some reason, download and install from the Microsoft Site." + Environment.NewLine
                    + "2. Did you download the DLLs from a ZIP file? If done so on Windows, you must open the file properties of the zip file and 'Unblock' it before extracting the files." + Environment.NewLine;

                if (HttpContext.Current != null)
                    msg += "3. Review the searched paths in the nested errors and make sure the desired path is accessible to the application pool identity (usually Read & Execute for 'IIS_IUSRS', or a similar user/group)" + Environment.NewLine;
                else
                    msg += "3. Review the searched paths in the nested errors and make sure the desired path is accessible to the application for loading the required libraries." + Environment.NewLine;

                if (error != null)
                    throw new InvalidOperationException(msg + Environment.NewLine, error);
                else
                    throw new InvalidOperationException(msg + Environment.NewLine);
            }

            return null;
        }
    }

}
