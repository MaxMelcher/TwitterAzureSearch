using Owin;

namespace MaxMelcher.AzureSearch.DataHub
{
    class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
}