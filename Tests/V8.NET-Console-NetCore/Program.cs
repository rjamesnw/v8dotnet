using System;
using V8.Net;

namespace V8.NET_Console_NetCore
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                //var engine = new V8Engine();
                var engine = new V8Engine(false);
                var context = engine.CreateContext();
                engine.SetContext(context);
                using (var result = engine.Execute("'Hello World!'"))
                {
                    Console.WriteLine(result.AsString);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.GetFullErrorMessage());
            }
            Console.ReadKey(true);
        }
    }
}
