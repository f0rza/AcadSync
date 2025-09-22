using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace AcadSync.Web.Hubs
{
    public class RunsHub : Hub
    {
        // Clients subscribe to receive updates about run progress and logs.
        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }
    }
}
