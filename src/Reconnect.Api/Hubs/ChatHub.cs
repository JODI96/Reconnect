using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Reconnect.Api.Common.Auth;
using Reconnect.Api.Features.Blocks;
using Reconnect.Contracts.Social;
using Reconnect.Domain.Social;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Hubs;

/// <summary>Events pushed to clients. Method names = <c>ChatHubContract.Client</c>.</summary>
public interface IChatClient
{
    Task ReceiveMessage(MessageDto message);
    Task MatchCreated(MatchDto match);
}

/// <summary>
/// Chat between matched users. Scaffold for phase 5: sending + delivery only;
/// history, read receipts and presence (Redis) follow later.
/// </summary>
[Authorize]
public sealed class ChatHub(ReconnectDbContext db, TimeProvider time) : Hub<IChatClient>
{
    public async Task<MessageDto> SendMessage(Guid matchId, string text)
    {
        var senderId = Context.User!.GetUserId();
        var ct = Context.ConnectionAborted;

        var match = await db.Matches.SingleOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is null || !match.Involves(senderId))
        {
            throw new HubException("Match not found.");
        }
        if (await db.IsBlockedBetweenAsync(match.User1Id, match.User2Id, ct))
        {
            throw new HubException("Match not found.");
        }

        var message = Message.Create(match, senderId, text, time.GetUtcNow());
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);

        var dto = new MessageDto(message.Id, message.MatchId, message.SenderId, message.Text, message.SentAt);
        await Clients.Users(match.User1Id.ToString(), match.User2Id.ToString()).ReceiveMessage(dto);
        return dto;
    }
}
