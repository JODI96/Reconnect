using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Reconnect.Api.Common.Auth;
using Reconnect.Api.Common.Endpoints;
using Reconnect.Contracts;
using Reconnect.Contracts.Safety;
using Reconnect.Domain.Safety;
using Reconnect.Infrastructure.Persistence;
using ReportReason = Reconnect.Domain.Safety.ReportReason;

namespace Reconnect.Api.Features.Reports;

public sealed class ReportEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Reports.Group).WithTags("Safety").RequireAuthorization();

        group.MapPost("/", CreateReport);
    }

    /// <summary>Stores a report for moderation. Reports work even if the users blocked each other.</summary>
    private static async Task<Results<Created<ReportCreatedResponse>, NotFound>> CreateReport(
        CreateReportRequest request, ClaimsPrincipal principal, ReconnectDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == request.ReportedUserId, ct))
        {
            return TypedResults.NotFound();
        }

        var report = Report.Create(
            principal.GetUserId(),
            request.ReportedUserId,
            (ReportReason)request.Reason,
            request.Comment,
            await ExistingOrNull(db.Rooms.Select(r => r.Id), request.RoomId, ct),
            await ExistingOrNull(db.Messages.Select(m => m.Id), request.MessageId, ct),
            time.GetUtcNow());

        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created((string?)null, new ReportCreatedResponse(report.Id));
    }

    /// <summary>Context ids are optional; unknown ids are dropped instead of failing the report.</summary>
    private static async Task<Guid?> ExistingOrNull(IQueryable<Guid> ids, Guid? id, CancellationToken ct) =>
        id is { } value && await ids.AnyAsync(x => x == value, ct) ? value : null;
}
