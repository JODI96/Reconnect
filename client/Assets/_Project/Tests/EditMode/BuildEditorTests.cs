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
