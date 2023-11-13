using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(SimpleAuthentication.Startup))]
namespace SimpleAuthentication
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
