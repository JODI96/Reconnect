using System;
using NUnit.Framework;
using Reconnect.Client.Networking;
using Reconnect.Contracts.Auth;
using Reconnect.Contracts.Common;
using Reconnect.Contracts.Rooms;
using Reconnect.Contracts.Safety;

namespace Reconnect.Client.Tests
{
    /// <summary>The backend's JSON (System.Text.Json, camelCase) must deserialize into the shared contracts.</summary>
    public sealed class JsonContractTests
    {
        [Test]
        public void AuthResponse_from_backend_json()
        {
            const string json = "{\"userId\":\"01a0d8b8-25e5-7754-a60f-ec33d41caf0b\",\"accessToken\":\"abc\"," +
                                "\"accessTokenExpiresAt\":\"2026-09-25T13:15:00+00:00\",\"refreshToken\":\"r1\"," +
                                "\"refreshTokenExpiresAt\":\"2026-10-25T13:00:00+00:00\"}";

            var auth = Json.Deserialize<AuthResponse>(json);

            Assert.AreEqual(Guid.Parse("01a0d8b8-25e5-7754-a60f-ec33d41caf0b"), auth.UserId);
            Assert.AreEqual("abc", auth.AccessToken);
            Assert.AreEqual("r1", auth.RefreshToken);
            Assert.AreEqual(new DateTimeOffset(2026, 9, 25, 13, 15, 0, TimeSpan.Zero), auth.AccessTokenExpiresAt);
        }

        [Test]
        public void Paged_room_list_with_layout_from_backend_json()
        {
            const string json = "{\"items\":[{\"id\":\"01a0d8b8-2b15-76ba-82d6-1ff32e6fa188\",\"name\":\"Rooftop Lounge\"," +
                                "\"buildingId\":\"0199a000-0000-7000-8000-000000000003\",\"ownerId\":\"01a0d8b8-25e5-7754-a60f-ec33d41caf0b\"," +
                                "\"ownerDisplayName\":\"Smoke\",\"isPublic\":true,\"updatedAt\":\"2026-09-25T13:00:00+00:00\"}]," +
                                "\"page\":1,\"pageSize\":20,\"totalCount\":1}";

            var page = Json.Deserialize<PagedResponse<RoomSummaryDto>>(json);

            Assert.AreEqual(1, page.TotalCount);
            Assert.AreEqual("Rooftop Lounge", page.Items[0].Name);
            Assert.IsTrue(page.Items[0].IsPublic);
        }

        [Test]
        public void Room_layout_roundtrip()
        {
            const string json = "{\"id\":\"01a0d8b8-2b15-76ba-82d6-1ff32e6fa188\",\"name\":\"R\",\"buildingId\":\"0199a000-0000-7000-8000-000000000003\"," +
                                "\"ownerId\":\"01a0d8b8-25e5-7754-a60f-ec33d41caf0b\",\"ownerDisplayName\":\"Smoke\",\"isPublic\":true," +
                                "\"layout\":[{\"itemId\":\"sofa_red\",\"position\":{\"x\":1.5,\"y\":0,\"z\":-2},\"rotation\":90}]," +
                                "\"createdAt\":\"2026-09-25T13:00:00+00:00\",\"updatedAt\":\"2026-09-25T13:00:00+00:00\"}";

            var room = Json.Deserialize<RoomDto>(json);

            Assert.AreEqual(1, room.Layout.Count);
            Assert.AreEqual("sofa_red", room.Layout[0].ItemId);
            Assert.AreEqual(new Vector3Dto(1.5f, 0f, -2f), room.Layout[0].Position);
            Assert.AreEqual(90f, room.Layout[0].Rotation);
        }

        [Test]
        public void Requests_are_serialized_camelCase_with_string_enums()
        {
            var json = Json.Serialize(new CreateReportRequest(Guid.Empty, ReportReason.Harassment, null, null, null));

            StringAssert.Contains("\"reportedUserId\"", json);
            StringAssert.Contains("\"reason\":\"Harassment\"", json);
            StringAssert.DoesNotContain("comment", json);
        }
    }
}
