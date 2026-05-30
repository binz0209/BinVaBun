using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace MemoriesApp.Api.Hubs;

public class MemoriesHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Verify user is authenticated
        // Context.User will be set by JWT middleware if token is valid
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            await base.OnConnectedAsync();
            Console.WriteLine($"SignalR client connected: {Context.ConnectionId}, User: {Context.User?.Identity?.Name}");
        }
        else
        {
            // Abort connection if not authenticated
            Console.WriteLine($"SignalR connection rejected - not authenticated. ConnectionId: {Context.ConnectionId}");
            Context.Abort();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
}

