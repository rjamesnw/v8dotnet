using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using V8.Net;

namespace WCFServiceTest

    /* V8.NET integration into ASP.NET:
     * 1. Create a project folder specifically named "V8.NET", or change the name using 'V8Engine.ASPBINSubFolderName'.
     *    Note: This test project has a PRE-Build event that copies files to "$(ProjectDir)V8.NET" - this must match 'V8Engine.ASPBINSubFolderName'.
     *    Note: 'V8Engine.ASPBINSubFolderName' is set to "V8.NET" by default.
     * 2. For all DLLS in the "x86" and "x64" folders under this "$(ProjectDir)V8.NET" folder, change "Copy to Output Directory" to "Copy if newer".  This should also
     *    tell Visual Studio that this content is required for the application.
     * 3. Set any DLLs that you have already referenced in the "$(ProjectDir)V8.NET" root folder to "Do not copy" - the build action will copy referenced DLLs by default.
     * 
     * When setup correctly, Visual Studio will replicate the folder structure for the DLLs in the "V8.NET" folder into the 'bin' folder, and the referenced DLLs will end
     * up in the root of the 'bin' folder.  Thus, you will have this folder structure:
     * - bin\V8.Net.dll
     * - bin\V8.Net.SharedTypes.dll (if referenced)
     * - bin\V8.Net\ (empty - no DLLs)
     * - bin\V8.Net\x86\*.dll
     * - bin\V8.Net\x64\*.dll
     * V8.Net.dll will look for the "V8.NET" folder in the 'bin' folder, and if found, will use that instead to locate dependent libraries.  If NOT found, it will expect
     * the 'x86' and 'x64' folders to be in the same folder as V8.Net.dll, as normal.
     * 
     * Note: ASP.NET may shadow copy some DLLs in the 'bin' folder (http://goo.gl/vXbwGp).  This means that those DLLs may end up elsewhere in a temporary folder
     * during runtime.
     */
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class Service : IService
    {
        public string GetData(int value)
        {
            // V8Engine.ASPBINSubFolderName = "V8.NET"; // It is already "V8.NET" by default, so just delete this line if not needed.  Please see integration steps at the top for more details.
            var engine = new V8Engine();
            Handle result = engine.Execute("'You entered: '+" + value, "V8.NET Web Service Test");
            return result.AsString;
        }

        public CompositeType GetDataUsingDataContract(CompositeType composite)
        {
            if (composite == null)
            {
                throw new ArgumentNullException("composite");
            }
            if (composite.BoolValue)
            {
                composite.StringValue += "Suffix";
            }
            return composite;
        }
    }
}
