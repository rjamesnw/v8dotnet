using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(ASPNetTest.Startup))]
namespace ASPNetTest
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
