using NUnit.Framework;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1RoomActivityTests
    {
        // Rooms in tiles (rows grow downward). 0 -- 1 -- 2 in a row, 3 above 1 with no passage to it.
        static ForestData Data() => new ForestData
        {
            rooms = new[]
            {
                new ForestRoom { id = 0, x = 0, y = 0, w = 40, h = 20 },
                new ForestRoom { id = 1, x = 44, y = 0, w = 40, h = 20 },
                new ForestRoom { id = 2, x = 88, y = 0, w = 40, h = 20 },
                new ForestRoom { id = 3, x = 44, y = -30, w = 40, h = 20 },
            },
            passages = new[]
            {
                new ForestPassage { from = 0, to = 1, x = 40, y = 10, w = 4, h = 4 },
                new ForestPassage { from = 1, to = 2, x = 84, y = 10, w = 4, h = 4 },
            },
        };
        static Vector2 In(ForestRoom r) => new Vector2(r.x + r.w / 2f, -(r.y + r.h / 2f));

        [Test] public void PlayerRoomAndPassageNeighboursAreAwakeOthersRest()
        {
            var data = Data(); long tick = 0; Vector2? player = In(data.rooms[0]);
            var activity = new Chapter1RoomActivity(data, () => tick, () => player);
            Assert.That(activity.Rests(In(data.rooms[0])), Is.False);
            Assert.That(activity.Rests(In(data.rooms[1])), Is.False, "passage neighbour");
            Assert.That(activity.Rests(In(data.rooms[2])), Is.True, "two rooms away");
            Assert.That(activity.Rests(In(data.rooms[3])), Is.True, "close above but not joined by a passage");

            player = In(data.rooms[1]); tick++;
            Assert.That(activity.Rests(In(data.rooms[0])), Is.False);
            Assert.That(activity.Rests(In(data.rooms[2])), Is.False);
            Assert.That(activity.Rests(In(data.rooms[3])), Is.True);
        }

        [Test] public void APlayerInAPassageWakesBothJoinedRooms()
        {
            var data = Data(); Vector2? player = new Vector2(86, -12);
            var activity = new Chapter1RoomActivity(data, () => 1, () => player);
            Assert.That(activity.Rests(In(data.rooms[1])), Is.False);
            Assert.That(activity.Rests(In(data.rooms[2])), Is.False);
            Assert.That(activity.Rests(In(data.rooms[0])), Is.True);
        }

        [Test] public void OutsideAnyRoomTheDistanceRuleDecides()
        {
            var data = Data(); Vector2? player = new Vector2(500, 500);
            var activity = new Chapter1RoomActivity(data, () => 1, () => player);
            Assert.That(activity.Rests(In(data.rooms[0])), Is.Null, "player in no room");
            player = In(data.rooms[0]);
            var fresh = new Chapter1RoomActivity(data, () => 2, () => player);
            Assert.That(fresh.Rests(new Vector2(-50, 50)), Is.Null, "monster in no room");
            Assert.That(new Chapter1RoomActivity(data, () => 3, () => null).Rests(In(data.rooms[0])), Is.Null, "no player");
        }

        [Test] public void APlayerMovedWithinATickIsFollowedAtOnce()
        {
            var data = Data(); long tick = 5; Vector2? player = In(data.rooms[0]);
            var activity = new Chapter1RoomActivity(data, () => tick, () => player);
            Assert.That(activity.Rests(In(data.rooms[2])), Is.True);
            player = In(data.rooms[2]);
            Assert.That(activity.Rests(In(data.rooms[2])), Is.False, "a warp in the same tick wakes the new room");
            Assert.That(activity.Rests(In(data.rooms[0])), Is.True);
        }
    }
}
