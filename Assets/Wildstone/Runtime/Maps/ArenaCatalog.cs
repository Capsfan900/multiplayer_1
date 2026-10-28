using System;
using UnityEngine;

namespace Wildstone.Maps
{
    [CreateAssetMenu(menuName = "Wildstone/Arena Catalog")]
    public sealed class ArenaCatalog : ScriptableObject
    {
        public ArenaDefinition[] Maps = Array.Empty<ArenaDefinition>();
        public ArenaDefinition Find(string id)
        {
            foreach (var map in Maps) if (map != null && map.Id == id) return map;
            throw new ArgumentException("Map is unavailable: " + id, nameof(id));
        }
    }
}
