using System;
using UnityEngine;

namespace ForestCombatV2
{
    [Serializable] public sealed class Chapter1RoomDecoration
    {
        public string id,assetId;
        public float x,y,width,height,rotation;
        public int order;
        public bool flipX;
    }
    [Serializable] public sealed class Chapter1SelectedLayouts
    {
        [Serializable] public sealed class Room { public string id;public Chapter1RoomDecoration[] decorations; }
        public Room[] rooms;
        public static Chapter1RoomDecoration[] Decorations(string id)=>Find(id)?.decorations;
        static Room Find(string id)
        {
            var text=Resources.Load<TextAsset>("Chapter1SelectedRooms");
            if(!text)return null;
            var data=JsonUtility.FromJson<Chapter1SelectedLayouts>(text.text);
            return Array.Find(data.rooms,r=>r.id==id);
        }
    }
}
