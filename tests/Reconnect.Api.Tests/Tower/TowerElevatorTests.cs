using Microsoft.AspNetCore.SignalR;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.Rooms;
using Reconnect.Modules.City.Public;

namespace Reconnect.Api.Tests.Tower;

[Collection(ApiTestGroup.Name)]
public sealed class TowerElevatorTests(ReconnectApiFactory factory)
{
    [Fact]
    public async Task Prime_tower_has_its_public_floors_with_capacities()
    {
        var user = await factory.RegisterAsync();

        var tower = await GetTowerAsync(user);

        Assert.Equal([0, 12, 24, 34, 35], tower.Floors.Where(f => f.IsPublic).Select(f => f.Floor));
        var lobby = tower.Floors.Single(f => f.Floor == 0);
        Assert.Equal("Lobby", lobby.Name);
        Assert.True(lobby.Capacity > tower.Floors.Where(f => f.Floor > 0).Max(f => f.Capacity), "the lobby holds the most people");
        Assert.Equal("Clouds", tower.Floors.Single(f => f.Floor == 35).Name);
    }

    [Fact]
    public async Task Lift_takes_you_to_another_floor_and_the_others_see_it()
    {
        var anna = await factory.RegisterAsync("Anna");
        var ben = await factory.RegisterAsync("Ben");
        var tower = await GetTowerAsync(anna);
        var (lobby, clouds) = (Floor(tower, 0), Floor(tower, 35));
        await using var annaHub = await RoomHubClient.ConnectAsync(factory, anna);
        await using var benHub = await RoomHubClient.ConnectAsync(factory, ben);
        await annaHub.JoinAsync(lobby.RoomId);
        await benHub.JoinAsync(lobby.RoomId);

        var ride = await annaHub.RideElevatorAsync(clouds.RoomId);

        Assert.Equal(ElevatorStatus.Arrived, ride.Status);
        Assert.Equal(35, ride.Snapshot!.Room.Floor);
        Assert.Contains(ride.Snapshot.Players, p => p.UserId == anna.Id);
        await RoomHubClient.Eventually(() => benHub.Left.Contains(anna.Id), "Ben sees Anna leave the lobby");
        var occupancy = Floor(await GetTowerAsync(anna), 35).Occupancy;
        Assert.True(occupancy >= 1);
    }

    [Fact]
    public async Task Full_floor_queues_and_the_next_person_rides_up_automatically()
    {
        var tower = await GetTowerAsync(await factory.RegisterAsync());
        var (lobby, coworking) = (Floor(tower, 0), Floor(tower, 12));
        var hubs = new List<RoomHubClient>();
        try
        {
            for (var i = 0; i < coworking.Capacity + 1; i++)
            {
                var hub = await RoomHubClient.ConnectAsync(factory, await factory.RegisterAsync($"Gast {i}"));
                await hub.JoinAsync(lobby.RoomId);
                hubs.Add(hub);
            }

            for (var i = 0; i < coworking.Capacity; i++)
            {
                Assert.Equal(ElevatorStatus.Arrived, (await hubs[i].RideElevatorAsync(coworking.RoomId)).Status);
            }
            var waiter = hubs[^1];
            var queued = await waiter.RideElevatorAsync(coworking.RoomId);
            Assert.Equal(ElevatorStatus.Queued, queued.Status);
            Assert.Equal(1, queued.Queue!.Position);
            Assert.Equal(1, Floor(await GetTowerAsync(waiter.User), 12).QueueLength);

            // Joining directly from outside doesn't skip the queue either.
            var outsider = await RoomHubClient.ConnectAsync(factory, await factory.RegisterAsync("Vordrängler"));
            hubs.Add(outsider);
            await Assert.ThrowsAsync<HubException>(() => outsider.JoinAsync(coworking.RoomId));

            // Someone leaves the coworking floor – the lift brings the waiting person up.
            await hubs[0].RideElevatorAsync(lobby.RoomId);
            await RoomHubClient.Eventually(() => !waiter.Arrivals.IsEmpty, "the waiting person rides up");
            Assert.True(waiter.Arrivals.TryPeek(out var arrived));
            Assert.Equal(12, arrived.Room.Floor);
            Assert.Equal(coworking.Capacity, Floor(await GetTowerAsync(waiter.User), 12).Occupancy);
        }
        finally
        {
            foreach (var hub in hubs)
            {
                await hub.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task A_rush_on_the_lift_never_puts_more_people_on_a_floor_than_allowed()
    {
        var tower = await GetTowerAsync(await factory.RegisterAsync());
        var (lobby, skyOffice) = (Floor(tower, 0), Floor(tower, 24));
        var hubs = new List<RoomHubClient>();
        try
        {
            for (var i = 0; i < skyOffice.Capacity + 5; i++)
            {
                var hub = await RoomHubClient.ConnectAsync(factory, await factory.RegisterAsync($"Rush {i}"));
                await hub.JoinAsync(lobby.RoomId);
                hubs.Add(hub);
            }

            var results = await Task.WhenAll(hubs.Select(h => h.RideElevatorAsync(skyOffice.RoomId)));

            Assert.Equal(skyOffice.Capacity, results.Count(r => r.Status == ElevatorStatus.Arrived));
            Assert.Equal(5, results.Count(r => r.Status == ElevatorStatus.Queued));
            Assert.Equal(Enumerable.Range(1, 5), results.Where(r => r.Queue is not null).Select(r => r.Queue!.Position).Order());
            Assert.Equal(skyOffice.Capacity, Floor(await GetTowerAsync(hubs[0].User), 24).Occupancy);
        }
        finally
        {
            foreach (var hub in hubs)
            {
                await hub.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Lift_only_serves_floors_of_the_same_building()
    {
        var anna = await factory.RegisterAsync();
        var lobby = Floor(await GetTowerAsync(anna), 0);
        await using var hub = await RoomHubClient.ConnectAsync(factory, anna);
        await hub.JoinAsync(lobby.RoomId);

        var other = await (await anna.Client.PostAsync(ApiRoutes.Rooms.Group,
            System.Net.Http.Json.JsonContent.Create(new CreateRoomRequest(ZurichBuildings.KunsthausId, "Atelier", true), options: TestUsers.Json)))
            .ReadAsync<RoomDto>();

        await Assert.ThrowsAsync<HubException>(() => hub.RideElevatorAsync(other.Id));
    }

    [Fact]
    public async Task Prime_tower_comes_with_its_ground_plan()
    {
        var user = await factory.RegisterAsync();

        var buildings = await (await user.Client.GetAsync($"{ApiRoutes.Buildings.Nearby}?lat=47.38622&lng=8.51733&radiusMeters=50"))
            .ReadAsync<List<Reconnect.Contracts.Buildings.BuildingDto>>();

        var tower = buildings.Single(b => b.Id == ZurichBuildings.PrimeTowerId);
        Assert.NotNull(tower.Footprint);
        Assert.True(tower.Footprint!.Count >= 8, "octagon-like outline");
        Assert.All(tower.Footprint, p => Assert.InRange(p.Latitude, 47.385, 47.387));
    }

    [Fact]
    public async Task Rooms_cannot_be_created_in_a_tower()
    {
        var user = await factory.RegisterAsync();

        var response = await user.Client.PostAsync(ApiRoutes.Rooms.Group,
            System.Net.Http.Json.JsonContent.Create(new CreateRoomRequest(ZurichBuildings.PrimeTowerId, "Mein Loft", true), options: TestUsers.Json));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static TowerFloorDto Floor(TowerDto tower, int storey) => tower.Floors.Single(f => f.Floor == storey && f.IsPublic);

    private static async Task<TowerDto> GetTowerAsync(TestUser user) =>
        await (await user.Client.GetAsync(ApiRoutes.Rooms.Tower(ZurichBuildings.PrimeTowerId))).ReadAsync<TowerDto>();
}
