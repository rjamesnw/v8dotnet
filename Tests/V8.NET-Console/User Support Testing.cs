// File:	"User Support Testing.cs"
// Summary:	Used to test user supplied code issues.
// Procedure: 
//   1. Put all user code into a file called "temp.ignore.cs" specifically. '.gitignore' will ignore that file so it does not get checked in.
//   2. In that same file, create "partial class UserSupportTesting { partial void RunTest() { ... } }", where "..." is the test code.
//   Since "temp.ignore.cs" is ignored (never checked in) any calls to "RunTest()" when missing will be stripped out by the compiler.
//
//  There is boilerplate code in 'temp.ignore.cs.example.txt' - just copy and rename it.

using V8.Net;

public partial class UserSupportTesting
{
    /// <summary> Main entry-point for this test file. This is called just before the console main menu. </summary>
    /// <param name="engine"> A V8.Net Wrapper Engine instance. </param>
    public static void Main(V8Engine engine)
    {
        new UserSupportTesting().RunTest(engine);
    }

    partial void RunTest(V8Engine engine);
}
