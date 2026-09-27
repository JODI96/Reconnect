using System.Linq;
using NUnit.Framework;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Rooms;

namespace Reconnect.Client.Tests
{
    /// <summary>The build editor's rules as the player feels them: snapping, green/red, carrying, undo.</summary>
    public sealed class BuildEditorTests
    {
        // Habbo-style room 10 × 8 m: walls north and east, door in the middle of the north wall.
        private static BuildEditor Empty() => new("cafe", 10, 8, new RoomItemDto[0]);

        [Test]
        public void A_new_item_snaps_onto_the_grid_and_can_be_put_down()
        {
            var editor = Empty();

            editor.Pick("table", 3.13f, 2.9f);

            var cells = RoomLayout.Footprint(editor.Selection, editor.SelectionDefinition);
            Assert.AreEqual(RoomLayout.Centre(cells), (editor.Selection.Position.X, editor.Selection.Position.Z));
            Assert.IsTrue(editor.SelectionIsValid);
            Assert.IsTrue(editor.Place());
            Assert.AreEqual(1, editor.Items.Count);
            Assert.IsTrue(editor.IsDirty);
        }

        [Test]
        public void Items_never_leave_the_room()
        {
            var editor = Empty();

            editor.Pick("loungeSofa", -5f, 40f);

            var cells = RoomLayout.Footprint(editor.Selection, editor.SelectionDefinition);
            Assert.IsTrue(editor.Context(editor.Items).Cells.Contains(cells));
        }

        [Test]
        public void A_plant_on_the_floor_is_red_and_on_a_table_green()
        {
            var editor = Empty();
            editor.Pick("table", 3f, 3f);
            editor.Place();

            editor.Pick("plantSmall1", 6f, 6f);
            Assert.IsFalse(editor.SelectionIsValid);
            StringAssert.Contains("Tisch", editor.SelectionProblems.First());
            Assert.IsFalse(editor.Place());

            editor.MoveSelectionTo(3f, 3f);
            Assert.IsTrue(editor.SelectionIsValid);
        }

        [Test]
        public void Paintings_stick_to_the_nearest_wall_and_face_into_the_room()
        {
            var editor = Empty();

            editor.Pick("ph-hanging_picture_frame_01", 9.4f, 3f);   // near the east wall

            var context = editor.Context(editor.Items);
            var cells = RoomLayout.Footprint(editor.Selection, editor.SelectionDefinition);
            Assert.AreEqual(WallSides.East, RoomLayout.WallOf(context, cells));
            Assert.AreEqual(RoomLayout.FacingIntoRoom("ph-hanging_picture_frame_01", WallSides.East), editor.Selection.Rotation);
            Assert.IsTrue(editor.SelectionIsValid);
        }

        [Test]
        public void Picking_up_a_table_takes_what_stands_on_it_along()
        {
            var editor = Empty();
            editor.Pick("table", 3f, 3f);
            editor.Place();
            editor.Pick("laptop", 3f, 3f);
            editor.Place();

            Assert.IsTrue(editor.SelectAt(2.6f, 2.8f));   // the table, not the laptop on it
            Assert.AreEqual("table", editor.Selection.ItemId);
            Assert.AreEqual(1, editor.Carried.Count);
            Assert.AreEqual(0, editor.Items.Count);

            editor.MoveSelectionTo(6f, 3f);
            editor.Rotate();
            Assert.IsTrue(editor.SelectionIsValid, string.Join(", ", editor.SelectionProblems));
            Assert.IsTrue(editor.Place());
            Assert.AreEqual(2, editor.Items.Count);
        }

        [Test]
        public void Dragging_in_turn_mode_makes_the_item_face_the_finger_in_whole_degrees()
        {
            var editor = Empty();
            editor.Pick("loungeSofa", 5f, 4f);
            var centre = RoomLayout.Shape(editor.Selection, editor.SelectionDefinition);
            // Finger 2 m away at 37° from +X.
            var angle = 37f * UnityEngine.Mathf.Deg2Rad;
            editor.AimSelectionAt(centre.CentreX + 2f * UnityEngine.Mathf.Cos(angle), centre.CentreZ + 2f * UnityEngine.Mathf.Sin(angle));

            Assert.IsTrue(RoomLayout.IsWholeDegree(editor.Selection.Rotation));
            Assert.IsFalse(RoomLayout.IsQuarterTurn(editor.Selection.Rotation));
            var (fx, fz) = RoomLayout.FrontVector(editor.Selection.ItemId, editor.Selection.Rotation);
            Assert.Greater(fx * UnityEngine.Mathf.Cos(angle) + fz * UnityEngine.Mathf.Sin(angle), 0.999f, "faces the finger");
            Assert.IsTrue(editor.SelectionIsValid, string.Join(", ", editor.SelectionProblems));
            Assert.IsTrue(editor.Place());
        }

        [Test]
        public void Turning_snaps_parallel_to_a_slanted_wall()
        {
            // A floor whose top edge rises at 20° (like the tower's slanted facades).
            var outline = new[] { new RoomPointDto(0f, 0f), new RoomPointDto(12f, 0f), new RoomPointDto(12f, 8f + 12f * 0.364f), new RoomPointDto(0f, 8f) };
            var editor = new BuildEditor("office", 12, 13, new RoomItemDto[0], outline);
            var wall = RoomLayout.Normalize(-20f);
            Assert.Contains(wall, editor.WallAngles().ToList());

            editor.Pick("loungeSofa", 6f, 6f);
            var centre = RoomLayout.Shape(editor.Selection, editor.SelectionDefinition);
            // Aim 3° off the wall's own angle: it snaps onto the wall.
            var wanted = RoomLayout.Normalize(wall + 3f);
            var (fx, fz) = RoomLayout.FrontVector("loungeSofa", wanted);
            editor.AimSelectionAt(centre.CentreX + fx * 2f, centre.CentreZ + fz * 2f);

            Assert.AreEqual(wall, editor.Selection.Rotation, 0.01f);
        }

        [Test]
        public void A_turned_table_turns_what_stands_on_it()
        {
            var editor = Empty();
            editor.Pick("table", 5f, 4f);
            editor.Place();
            var table = editor.Items[0];
            editor.Pick("laptop", table.Position.X, table.Position.Z);
            editor.Place();

            Assert.IsTrue(editor.SelectAt(table.Position.X + 0.6f, table.Position.Z));
            editor.TurnSelectionTo(30f);

            Assert.AreEqual(30f, editor.Selection.Rotation, 0.01f);
            Assert.AreEqual(1, editor.Carried.Count);
            Assert.AreEqual(30f, editor.Carried[0].Rotation % 180f, 0.01f);
            Assert.IsTrue(editor.SelectionIsValid, string.Join(", ", editor.SelectionProblems));
            Assert.IsTrue(editor.Place());
        }

        [Test]
        public void Tapping_a_laptop_picks_up_the_laptop()
        {
            var editor = Empty();
            editor.Pick("table", 3f, 3f);
            editor.Place();
            editor.Pick("laptop", 3f, 3f);
            editor.Place();
            var laptop = editor.Items[1];

            Assert.IsTrue(editor.SelectAt(laptop.Position.X, laptop.Position.Z));

            Assert.AreEqual("laptop", editor.Selection.ItemId);
            Assert.AreEqual(0, editor.Carried.Count);
        }

        [Test]
        public void Undo_restores_the_layout_step_by_step()
        {
            var editor = Empty();
            editor.Pick("table", 3f, 3f);
            editor.Place();
            editor.SelectAt(3f, 3f);
            editor.Delete();
            Assert.AreEqual(0, editor.Items.Count);

            editor.Undo();
            Assert.AreEqual(1, editor.Items.Count);
            editor.Undo();
            Assert.AreEqual(0, editor.Items.Count);
            Assert.IsFalse(editor.CanUndo);
        }

        [Test]
        public void Cancelling_puts_a_picked_up_item_back()
        {
            var editor = Empty();
            editor.Pick("table", 3f, 3f);
            editor.Place();
            var before = editor.Items[0];

            editor.SelectAt(3f, 3f);
            editor.MoveSelectionTo(7f, 2f);
            editor.CancelSelection();

            Assert.AreEqual(before, editor.Items.Single());
        }

        [Test]
        public void Old_layouts_are_moved_onto_the_grid_when_the_editor_opens()
        {
            var old = new[] { new RoomItemDto("table", new Vector3Dto(3.13f, 0f, 2.71f), 0f) };

            var editor = new BuildEditor("cafe", 10, 8, old);

            Assert.IsTrue(editor.IsDirty);
            Assert.IsNotNull(editor.Notice);
            Assert.IsEmpty(RoomLayout.Validate(editor.Context(editor.Items), editor.Items));
        }
    }
}
