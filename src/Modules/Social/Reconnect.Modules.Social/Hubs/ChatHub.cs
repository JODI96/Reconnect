using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Reconnect.Contracts.Social;
using Reconnect.Modules.Safety.Public;
using Reconnect.Modules.Social.Domain;
using Reconnect.Modules.Social.Infrastructure;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.Social.Hubs;

/// <summary>
/// Events pushed to clients. Method names = <c>ChatHubContract.Client</c>.
/// Public because SignalR generates a typed proxy for it.
/// </summary>
public interface IChatClient
{
    Task ReceiveMessage(MessageDto message);
    Task MatchCreated(MatchDto match);
}

/// <summary>
/// Chat between matched users. Sending + delivery only; history, read receipts and
/// presence follow later.
/// </summary>
[Authorize]
internal sealed class ChatHub(SocialDbContext db, IBlockQueries blocks, TimeProvider time) : Hub<IChatClient>
{
    public async Task<MessageDto> SendMessage(Guid matchId, string text)
    {
        var senderId = Context.User!.GetUserId();
        var ct = Context.ConnectionAborted;

        var match = await db.Matches.SingleOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is null || !match.Involves(senderId) || await blocks.IsBlockedBetweenAsync(match.User1Id, match.User2Id, ct))
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
