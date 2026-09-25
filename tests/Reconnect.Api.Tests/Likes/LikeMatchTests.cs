using System.Net;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.Social;

namespace Reconnect.Api.Tests.Likes;

[Collection(ApiTestGroup.Name)]
public sealed class LikeMatchTests(ReconnectApiFactory factory)
{
    [Fact]
    public async Task One_sided_like_does_not_create_a_match()
    {
        var anna = await factory.RegisterAsync();
        var ben = await factory.RegisterAsync();

        var result = await (await anna.Client.PostAsync(ApiRoutes.Likes.ForUser(ben.Id), null)).ReadAsync<LikeResponse>();

        Assert.False(result.IsMatch);
        Assert.Null(result.Match);
    }

    [Fact]
    public async Task Mutual_like_creates_a_match_for_both()
    {
        var anna = await factory.RegisterAsync();
        var ben = await factory.RegisterAsync();

        await anna.Client.PostAsync(ApiRoutes.Likes.ForUser(ben.Id), null);
        var result = await (await ben.Client.PostAsync(ApiRoutes.Likes.ForUser(anna.Id), null)).ReadAsync<LikeResponse>();

        Assert.True(result.IsMatch);
        Assert.NotNull(result.Match);
        Assert.Equal(anna.Id, result.Match.OtherUserId);

        // Liking again is idempotent and returns the same match.
        var again = await (await anna.Client.PostAsync(ApiRoutes.Likes.ForUser(ben.Id), null)).ReadAsync<LikeResponse>();
        Assert.True(again.IsMatch);
        Assert.Equal(result.Match.Id, again.Match!.Id);
        Assert.Equal(ben.Id, again.Match.OtherUserId);
    }

    [Fact]
    public async Task Liking_yourself_is_rejected()
    {
        var anna = await factory.RegisterAsync();

        var response = await anna.Client.PostAsync(ApiRoutes.Likes.ForUser(anna.Id), null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Liking_unknown_user_returns_404()
    {
        var anna = await factory.RegisterAsync();

        var response = await anna.Client.PostAsync(ApiRoutes.Likes.ForUser(Guid.NewGuid()), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
