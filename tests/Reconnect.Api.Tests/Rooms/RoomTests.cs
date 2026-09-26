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
    private static readonly UpdateRoomLayoutRequest Sofa = new(
        [new RoomItemDto("sofa_red", new Vector3Dto(1.5f, 0f, -2f), 90f)]);

    [Fact]
    public async Task Owner_can_update_layout_and_it_is_persisted_as_json()
    {
        var owner = await factory.RegisterAsync();
        var room = await CreateRoomAsync(owner);

        var response = await owner.Client.PutAsJsonAsync(ApiRoutes.Rooms.Layout(room.Id), Sofa, TestUsers.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reloaded = await (await owner.Client.GetAsync(ApiRoutes.Rooms.ById(room.Id))).ReadAsync<RoomDto>();
        var item = Assert.Single(reloaded.Layout);
        Assert.Equal("sofa_red", item.ItemId);
        Assert.Equal(new Vector3Dto(1.5f, 0f, -2f), item.Position);
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
