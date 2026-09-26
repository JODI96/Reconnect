using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.Buildings;
using Reconnect.Contracts.Rooms;
using Reconnect.Modules.City.Public;

namespace Reconnect.Api.Tests.Rooms;

[Collection(ApiTestGroup.Name)]
public sealed class RoomTests(ReconnectApiFactory factory)
{
    // A sofa turned sideways, on the build grid (4 × 2 cells → 1 × 2 m once turned).
    private static readonly UpdateRoomLayoutRequest Sofa = new(
        [RoomLayout.Snap(new RoomItemDto("loungeSofa", new Vector3Dto(3.5f, 0f, 3f), 90f))]);

    [Fact]
    public async Task Owner_can_update_layout_and_it_is_persisted_as_json()
    {
        var owner = await factory.RegisterAsync();
        var room = await CreateRoomAsync(owner);

        var response = await owner.Client.PutAsJsonAsync(ApiRoutes.Rooms.Layout(room.Id), Sofa, TestUsers.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reloaded = await (await owner.Client.GetAsync(ApiRoutes.Rooms.ById(room.Id))).ReadAsync<RoomDto>();
        var item = Assert.Single(reloaded.Layout);
        Assert.Equal("loungeSofa", item.ItemId);
        Assert.Equal(Sofa.Items[0].Position, item.Position);
        Assert.Equal(90f, item.Rotation);
        Assert.True(reloaded.UpdatedAt >= reloaded.CreatedAt);
    }

    [Fact]
    public async Task Non_owner_cannot_update_layout()
    {
        var owner = await factory.RegisterAsync();
        var visitor = await factory.RegisterAsync();
        var room = await CreateRoomAsync(owner);

        var response = await visitor.Client.PutAsJsonAsync(ApiRoutes.Rooms.Layout(room.Id), Sofa, TestUsers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Layouts_that_break_the_build_rules_are_rejected_item_by_item()
    {
        var owner = await factory.RegisterAsync();
        var room = await CreateRoomAsync(owner);
        var layout = new UpdateRoomLayoutRequest(
        [
            Sofa.Items[0],
            new RoomItemDto("plantSmall1", new Vector3Dto(6.25f, 0f, 3.25f), 0f),   // small plant on the bare floor
            new RoomItemDto("loungeSofa", new Vector3Dto(3.5f, 0f, 3.5f), 90f),     // into the first sofa
            new RoomItemDto("table", new Vector3Dto(3.13f, 0f, 6.37f), 0f),         // off the grid
        ]);

        var response = await owner.Client.PutAsJsonAsync(ApiRoutes.Rooms.Layout(room.Id), layout, TestUsers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.Equal(["items[1]", "items[2]", "items[3]"], problem.Errors.Keys.Order().ToArray());
        var reloaded = await (await owner.Client.GetAsync(ApiRoutes.Rooms.ById(room.Id))).ReadAsync<RoomDto>();
        Assert.Empty(reloaded.Layout);
    }

    [Fact]
    public async Task Admins_can_build_in_every_room_and_everyone_inside_sees_it_live()
    {
        var owner = await factory.RegisterAsync();
        var admin = await factory.RegisterAdminAsync();
        var room = await CreateRoomAsync(owner, isPublic: false);
        await using var ownerHub = await RoomHubClient.ConnectAsync(factory, owner);
        await ownerHub.JoinAsync(room.Id);

        var response = await admin.Client.PutAsJsonAsync(ApiRoutes.Rooms.Layout(room.Id), Sofa, TestUsers.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Admin", admin.Auth.Roles ?? []);
        await RoomHubClient.Eventually(() => ownerHub.LayoutChanges.Any(c => c.RoomId == room.Id && c.Layout.Count == 1),
            "the owner sees the new layout");
    }

    [Fact]
    public async Task Private_rooms_are_only_visible_to_owner()
    {
        var owner = await factory.RegisterAsync();
        var visitor = await factory.RegisterAsync();
        var room = await CreateRoomAsync(owner, isPublic: false);

        Assert.Equal(HttpStatusCode.OK, (await owner.Client.GetAsync(ApiRoutes.Rooms.ById(room.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.Client.GetAsync(ApiRoutes.Rooms.ById(room.Id))).StatusCode);
    }

    [Fact]
    public async Task Nearby_buildings_are_found_by_distance_in_metres()
    {
        var user = await factory.RegisterAsync();

        // Near Zürich HB: HB and Landesmuseum are within 300 m, Prime Tower (~2 km) is not.
        var buildings = await (await user.Client.GetAsync($"{ApiRoutes.Buildings.Nearby}?lat=47.3780&lng=8.5400&radiusMeters=300"))
            .ReadAsync<List<BuildingDto>>();

        Assert.Equal(ZurichBuildings.HauptbahnhofId, buildings[0].Id);
        Assert.Contains(buildings, b => b.Id == ZurichBuildings.LandesmuseumId);
        Assert.DoesNotContain(buildings, b => b.Id == ZurichBuildings.PrimeTowerId);
        Assert.All(buildings, b => Assert.InRange(b.DistanceMeters!.Value, 0, 300));
    }

    private static async Task<RoomDto> CreateRoomAsync(TestUser owner, bool isPublic = true)
    {
        var response = await owner.Client.PostAsJsonAsync(ApiRoutes.Rooms.Group,
            new CreateRoomRequest(ZurichBuildings.GrossmuensterId, "Test Room", isPublic), TestUsers.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<RoomDto>();
    }
}
