using System;
using UnityEngine;

namespace Wildstone.Networking
{
    public static class FusionBuildIdentity
    {
        public static string Read()
        {
            var asset=Resources.Load<TextAsset>("WildstoneBuild");
            return Parse(asset==null?null:asset.text);
        }
        internal static string Parse(string text)
        {
            if(text==null || text.Length>66) throw new InvalidOperationException("The online build identity is unavailable or invalid. Prepare or rebuild this game.");
            text=text?.Trim();
            if(text==null || text.Length!=64) throw new InvalidOperationException("The online build identity is unavailable. Prepare or rebuild this game.");
            foreach(char value in text)
                if(!((value>='0' && value<='9') || (value>='a' && value<='f')))
                    throw new InvalidOperationException("The online build identity is invalid. Prepare or rebuild this game.");
            return text;
        }
    }
}
