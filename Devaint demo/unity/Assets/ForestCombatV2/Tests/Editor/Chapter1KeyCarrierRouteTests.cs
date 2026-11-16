using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    // The key carrier's landing graph and flee planning on a small hand-built map:
    // a floor across three rooms (A, B, boss) with one drop-through deck and one solid deck in room B.
    public sealed class Chapter1KeyCarrierRouteTests
    {
        const int Cols = 60, Rows = 20;
        static readonly Vector2 Body = new Vector2(1, 1.2f);
        const float FloorY = -18, DeckY = -14;

        static Chapter1KeyCarrierRoute Build()
        {
            var ground = new int[Cols * Rows];
            for (int c = 0; c < Cols; c++) { ground[18 * Cols + c] = 1; ground[19 * Cols + c] = 1; }
            for (int r = 0; r < Rows; r++) { ground[r * Cols] = 1; ground[r * Cols + Cols - 1] = 1; }
            var data = new ForestData
            {
                cols = Cols, rows = Rows, ground = ground,
                rooms = new[]
                {
                    new ForestRoom { id = 0, kind = "combat", x = 1, y = 0, w = 19, h = 19 },
                    new ForestRoom { id = 1, kind = "combat", x = 20, y = 0, w = 20, h = 19 },
                    new ForestRoom { id = 2, kind = "boss", x = 40, y = 0, w = 19, h = 19 },
                },
                platforms = new[]
                {
                    new FoliagePlatform { x = 24 * 32, y = 14 * 32, w = 6 * 32 },
                    new FoliagePlatform { x = 32 * 32, y = 14 * 32, w = 4 * 32, blockDropThrough = true },
                },
            };
            return new Chapter1KeyCarrierRoute(data, Body, 7.1f, 104.17f, 6);
        }

        static Chapter1KeyCarrierRoute.Landing At(Chapter1KeyCarrierRoute route, float x, float y) => route.StandingOn(new Vector2(x, y));

        [Test] public void DropThroughDecksLinkDownwardAndBlockedDecksDoNot()
        {
            var route = Build();
            var open = At(route, 26, DeckY); var blocked = At(route, 34, DeckY);
            Assert.That(open, Is.Not.Null); Assert.That(blocked, Is.Not.Null);
            Assert.That(route.EdgesFrom(open.Index).Any(e => e.Kind == Chapter1KeyCarrierRoute.Move.Drop && route.Landings[e.To].Y == FloorY), Is.True);
            Assert.That(route.EdgesFrom(blocked.Index).Any(e => e.Kind == Chapter1KeyCarrierRoute.Move.Drop), Is.False);
            var floor = At(route, 26, FloorY);
            Assert.That(route.EdgesFrom(floor.Index).Any(e => e.Kind == Chapter1KeyCarrierRoute.Move.Jump && route.Landings[e.To].Y == DeckY), Is.True, "the deck is reached by a jump");
        }

        [Test] public void BossRoomLandingsAreNeverOnTheRoute()
        {
            var route = Build();
            Assert.That(route.Landings.Where(l => l.Mid > 40).All(l => !l.Allowed), Is.True);
            Assert.That(route.Landings.Where(l => l.Mid > 2 && l.Mid < 39).All(l => l.Allowed), Is.True);
        }

        [Test] public void RunsAwayFromThePlayerIntoAnotherRoomAndKeepsItsRoute()
        {
            var route = Build(); var random = new System.Random(1);
            var carrier = new Vector2(16, FloorY);
            route.Plan(0, At(route, carrier.x, carrier.y), carrier, new Vector2(8, FloorY), random);
            Assert.That(route.Fleeing, Is.True);
            var goal = route.Landings[route.Goal];
            Assert.That(goal.Mid, Is.GreaterThan(20).And.LessThan(40), "flees east into room B, never the boss room");
            int first = route.Goal;
            // The player following behind does not turn the run around.
            route.Plan(.4f, At(route, carrier.x, carrier.y), carrier, new Vector2(11, FloorY), random);
            Assert.That(route.Goal, Is.EqualTo(first));
        }

        [Test] public void TurnsWhenThePlayerGetsAheadOnTheRoute()
        {
            var route = Build(); var random = new System.Random(1);
            var carrier = new Vector2(16, FloorY);
            route.Plan(0, At(route, carrier.x, carrier.y), carrier, new Vector2(8, FloorY), random);
            Assert.That(route.Landings[route.Goal].Mid, Is.GreaterThan(carrier.x));
            route.Plan(.4f, At(route, carrier.x, carrier.y), carrier, new Vector2(19, FloorY), random);
            Assert.That(route.Landings[route.Goal].Mid, Is.LessThan(carrier.x), "the player now blocks the east, so it runs west");
        }

        [Test] public void ASteppedDropThroughDeckRestsForTenSecondsOnEveryPiece()
        {
            var route = Build();
            var deck = At(route, 26, DeckY);
            route.Stepped(deck, 10);
            foreach (var piece in route.Landings.Where(l => l.Deck == deck.Deck))
                Assert.That(route.SteppedUntil(piece.Index), Is.EqualTo(10));
            route.Stepped(At(route, 34, DeckY), 10);
            Assert.That(route.SteppedUntil(At(route, 34, DeckY).Index), Is.Zero, "a deck that cannot be dropped through is not rested");
        }

        [Test] public void WandersIntoOtherRoomsWhenThePlayerIsFar()
        {
            var route = Build(); var random = new System.Random(3);
            var carrier = new Vector2(10, FloorY); int other = 0;
            for (int i = 0; i < 20; i++)
            {
                route.ClearGoal();
                route.Plan(i, At(route, carrier.x, carrier.y), carrier, new Vector2(1000, FloorY), random);
                Assert.That(route.Fleeing, Is.False);
                if (route.Goal >= 0 && route.Landings[route.Goal].Room != 0) other++;
            }
            Assert.That(other, Is.GreaterThan(5), "most wandering goals lie in another room");
        }
    }
}
