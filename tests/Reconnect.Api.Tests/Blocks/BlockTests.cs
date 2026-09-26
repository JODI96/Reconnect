using System.Net;
using System.Net.Http.Json;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.Common;
using Reconnect.Contracts.Rooms;
using Reconnect.Contracts.Social;
using Reconnect.Modules.City.Public;

namespace Reconnect.Api.Tests.Blocks;

[Collection(ApiTestGroup.Name)]
public sealed class BlockTests(ReconnectApiFactory factory)
{
    [Fact]
    public async Task Blocked_users_cannot_like_each_other_in_either_direction()
    {
        var anna = await factory.RegisterAsync();
        var ben = await factory.RegisterAsync();

        await anna.Client.PostAsync(ApiRoutes.Blocks.ForUser(ben.Id), null);

        Assert.Equal(HttpStatusCode.Forbidden, (await anna.Client.PostAsync(ApiRoutes.Likes.ForUser(ben.Id), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ben.Client.PostAsync(ApiRoutes.Likes.ForUser(anna.Id), null)).StatusCode);
    }

    [Fact]
    public async Task Blocked_users_cannot_see_each_others_profile()
    {
        var anna = await factory.RegisterAsync();
        var ben = await factory.RegisterAsync();
        Assert.Equal(HttpStatusCode.OK, (await anna.Client.GetAsync(ApiRoutes.Profiles.ById(ben.Id))).StatusCode);

        await anna.Client.PostAsync(ApiRoutes.Blocks.ForUser(ben.Id), null);

        Assert.Equal(HttpStatusCode.NotFound, (await anna.Client.GetAsync(ApiRoutes.Profiles.ById(ben.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ben.Client.GetAsync(ApiRoutes.Profiles.ById(anna.Id))).StatusCode);
    }

    [Fact]
    public async Task Blocking_removes_existing_match()
    {
        var anna = await factory.RegisterAsync();
        var ben = await factory.RegisterAsync();
        await anna.Client.PostAsync(ApiRoutes.Likes.ForUser(ben.Id), null);
        var match = await (await ben.Client.PostAsync(ApiRoutes.Likes.ForUser(anna.Id), null)).ReadAsync<LikeResponse>();
        Assert.True(match.IsMatch);

        await ben.Client.PostAsync(ApiRoutes.Blocks.ForUser(anna.Id), null);
        await ben.Client.DeleteAsync(ApiRoutes.Blocks.ForUser(anna.Id));

        // After unblocking, the old likes/match are gone: a single like is not a match again.
        var afterUnblock = await (await anna.Client.PostAsync(ApiRoutes.Likes.ForUser(ben.Id), null)).ReadAsync<LikeResponse>();
        Assert.False(afterUnblock.IsMatch);
    }

    [Fact]
    public async Task Rooms_of_blocked_users_are_hidden()
    {
        var anna = await factory.RegisterAsync();
        var ben = await factory.RegisterAsync();
        var room = await (await ben.Client.PostAsJsonAsync(ApiRoutes.Rooms.Group,
            new CreateRoomRequest(ZurichBuildings.PrimeTowerId, "Ben's Lounge", IsPublic: true), TestUsers.Json)).ReadAsync<RoomDto>();

        Assert.Equal(HttpStatusCode.OK, (await anna.Client.GetAsync(ApiRoutes.Rooms.ById(room.Id))).StatusCode);

        await anna.Client.PostAsync(ApiRoutes.Blocks.ForUser(ben.Id), null);

        Assert.Equal(HttpStatusCode.NotFound, (await anna.Client.GetAsync(ApiRoutes.Rooms.ById(room.Id))).StatusCode);
        var listResponse = await anna.Client.GetAsync($"{ApiRoutes.Rooms.Group}?buildingId={ZurichBuildings.PrimeTowerId}&pageSize=100");
        Assert.True(listResponse.IsSuccessStatusCode, await listResponse.Content.ReadAsStringAsync());
        var list = await listResponse.ReadAsync<PagedResponse<RoomSummaryDto>>();
        Assert.DoesNotContain(list.Items, r => r.Id == room.Id);
    }
}
