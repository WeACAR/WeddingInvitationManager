using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace WeddingInvitationManager.Hubs;

[Authorize]
public class QRScanHub : Hub
{
    public async Task JoinEventGroup(string eventId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Event_{eventId}");
    }

    public async Task LeaveEventGroup(string eventId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Event_{eventId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}

public static class QRScanHubExtensions
{
    public static async Task NotifyQRScanAsync(this IHubContext<QRScanHub> hubContext, int eventId, object scanData)
    {
        await hubContext.Clients.Group($"Event_{eventId}").SendAsync("QRScanned", scanData);
    }

    public static async Task NotifyStatsUpdateAsync(this IHubContext<QRScanHub> hubContext, int eventId, object stats)
    {
        await hubContext.Clients.Group($"Event_{eventId}").SendAsync("StatsUpdated", stats);
    }

    public static async Task NotifyInvitationSentAsync(this IHubContext<QRScanHub> hubContext, int eventId, object invitationData)
    {
        await hubContext.Clients.Group($"Event_{eventId}").SendAsync("InvitationSent", invitationData);
    }
}
