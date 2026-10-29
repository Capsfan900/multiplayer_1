using NUnit.Framework;
using UnityEngine;
using Wildstone.Maps;

namespace Wildstone.Tests
{
    [Category("Managed")]
    public sealed class ArenaDefinitionTests
    {
        private static ArenaDefinition Valid() => new ArenaDefinition
        {
            Id = "atrium", DisplayName = "Atrium", Minimum = new Vector3(-20, -1, -20), Maximum = new Vector3(20, 10, 20),
            Pieces = new[] { new ArenaPiece { Name = "Floor", Size = new Vector3(40, 1, 40) } },
            Spawns = new[] {
                new ArenaSpawn { Team = 0, Position = new Vector3(-16, 0, -16) }, new ArenaSpawn { Team = 0, Position = new Vector3(-14, 0, -16) },
                new ArenaSpawn { Team = 1, Position = new Vector3(16, 0, -16) }, new ArenaSpawn { Team = 1, Position = new Vector3(14, 0, -16) },
                new ArenaSpawn { Team = 2, Position = new Vector3(16, 0, 16) }, new ArenaSpawn { Team = 2, Position = new Vector3(14, 0, 16) },
                new ArenaSpawn { Team = 3, Position = new Vector3(-16, 0, 16) }, new ArenaSpawn { Team = 3, Position = new Vector3(-14, 0, 16) } },
            Pickups = new[] { new ArenaPickup { Item = "pulse-lobber", Position = Vector3.zero } },
            ZoneRoute = new[] { new Vector3(-8, 0, -8), new Vector3(8, 0, -8), new Vector3(8, 0, 8), new Vector3(-8, 0, 8) }
        };

        [Test] public void CompleteDefinitionPasses() => Assert.That(Valid().Validate(), Is.Empty);
        [Test] public void ThreeDefaultMapsHaveDistinctTopologiesAndCompleteStarts()
        {
            var maps = ArenaLayouts.CreateAll();
            Assert.That(maps.Length, Is.EqualTo(3));
            Assert.That(new[] { maps[0].Id, maps[1].Id, maps[2].Id }, Is.EquivalentTo(new[] { "atrium", "split-gallery", "terrace-steps" }));
            Assert.That(maps[0].ZoneRoute.Length, Is.Not.EqualTo(maps[1].ZoneRoute.Length));
            Assert.That(maps[0].Pieces.Length, Is.Not.EqualTo(maps[2].Pieces.Length));
            foreach (var map in maps) Assert.That(map.Validate(), Is.Empty, map.Id);
        }
        [Test] public void MissingTeamStartIsRejected()
        {
            var map = Valid(); map.Spawns[0].Team = 1;
            Assert.That(map.Validate(), Has.Some.Contains("Team 0 needs two"));
        }
        [Test] public void EveryArenaOffersTheThreeLaserWeaponPickups()
        {
            foreach(var map in ArenaLayouts.CreateAll())
                foreach(string item in new[]{"laser-pistol","laser-shotgun","laser-sniper"})
                {
                    Assert.That(System.Array.FindAll(map.Pickups,p=>p.Item==item).Length,Is.EqualTo(1),map.Id+" "+item);
                    Assert.That(map.Revision,Is.EqualTo(2));
                }
        }
        [Test] public void UnknownPickupCannotShip()
        {
            var map = Valid(); map.Pickups[0].Item = "typo";
            Assert.That(map.Validate(), Has.Some.Contains("Invalid pickup"));
        }
        [Test] public void DuplicateCapsulesCannotSpawnOnOneAnother()
        {
            var map = Valid(); map.Spawns[1].Position = map.Spawns[0].Position;
            Assert.That(map.Validate(), Has.Some.Contains("Spawn capsules overlap"));
        }
        [Test] public void RouteCannotContainTeleportSegments()
        {
            var map = Valid(); map.ZoneRoute[1] = map.ZoneRoute[0] + Vector3.up * 4;
            Assert.That(map.Validate(), Has.Some.Contains("unwalkable segment"));
        }
        [Test] public void NonfiniteCoordinateFailsClosed()
        {
            var map = Valid(); map.ZoneRoute[0].x = float.NaN;
            Assert.That(map.Validate(), Has.Some.Contains("outside playable bounds"));
        }
    }
}
