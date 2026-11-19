using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace Bastion.BuildingBlocks.Jobs;

// One hub type serves every module; the group keeps the messages of a module for its own clients.
public sealed class JobsHub : Hub<IJobsClient>
{
    public override async Task OnConnectedAsync()
    {
        var module = Context.GetHttpContext()?.GetEndpoint()?.Metadata.GetMetadata<JobsModule>();
        if (module is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, module.Schema);
        }

        await base.OnConnectedAsync();
    }
}
