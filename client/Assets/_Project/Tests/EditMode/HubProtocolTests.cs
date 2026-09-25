using System;
using System.Text;
using NUnit.Framework;
using Reconnect.Client.Networking.Realtime;

namespace Reconnect.Client.Tests
{
    public sealed class HubProtocolTests
    {
        [Test]
        public void Invocation_is_camelCase_json_terminated_by_record_separator()
        {
            var roomId = Guid.Parse("01a0d8b8-2b15-76ba-82d6-1ff32e6fa188");

            var message = HubProtocol.InvocationMessage("7", "JoinRoom", new object[] { roomId });

            Assert.AreEqual('\u001e', message[message.Length - 1]);
            StringAssert.Contains("\"type\":1", message);
            StringAssert.Contains("\"invocationId\":\"7\"", message);
            StringAssert.Contains("\"target\":\"JoinRoom\"", message);
            StringAssert.Contains("\"arguments\":[\"01a0d8b8-2b15-76ba-82d6-1ff32e6fa188\"]", message);
        }

        [Test]
        public void Several_messages_in_one_frame_are_split()
        {
            var buffer = new StringBuilder("{}\u001e{\"type\":6}\u001e");

            var messages = HubProtocol.ExtractMessages(buffer);

            CollectionAssert.AreEqual(new[] { "{}", "{\"type\":6}" }, messages);
            Assert.AreEqual(0, buffer.Length);
        }

        [Test]
        public void Incomplete_message_waits_for_the_next_frame()
        {
            var buffer = new StringBuilder("{\"type\":1,\"target\":\"Pla");

            Assert.IsEmpty(HubProtocol.ExtractMessages(buffer));

            buffer.Append("yerLeft\",\"arguments\":[]}\u001e");
            var messages = HubProtocol.ExtractMessages(buffer);
            Assert.AreEqual(1, messages.Count);
            StringAssert.Contains("PlayerLeft", messages[0]);
        }

        [Test]
        public void Server_error_prefix_is_removed_for_display()
        {
            var error = new HubException("An unexpected error occurred invoking 'JoinRoom' on the server. HubException: Room not found.");

            Assert.AreEqual("Room not found.", error.Message);
        }
    }
}
