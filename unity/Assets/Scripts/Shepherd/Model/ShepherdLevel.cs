namespace Shepherd
{
    using System.Collections.Generic;
    using UnityEngine;
    using System;

    /// <summary>
    /// Data container for kings taxes level.
    /// Holds point list for villages and castles and possibly a t-spanner ratio.
    /// </summary>
    [CreateAssetMenu(fileName = "ShepherdLevelNew", menuName = "Levels/Shepherd Level")]
    public class ShepherdLevel : ScriptableObject
    {
        [Header("Sheep")]
        public List<Vector2> SheepList = new List<Vector2>();
        public List<int> SheepTypes = new List<int>();

        public void addSheep(Vector2 loc, int type)
        {
            SheepList.Add(loc);
            SheepTypes.Add(type);
        }
    }
}