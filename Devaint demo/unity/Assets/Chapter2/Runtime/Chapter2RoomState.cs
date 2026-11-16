using System;
using System.Collections.Generic;

namespace Deviant.Chapter2
{
    // Registration uses immutable authored room ownership, never the current chasing position.
    public sealed class Chapter2RoomState
    {
        readonly Dictionary<string,HashSet<string>> living=new Dictionary<string,HashSet<string>>();
        readonly HashSet<string> defeated=new HashSet<string>(),cleared=new HashSet<string>();
        public event Action<string> Cleared;
        public void Register(string room,string actor){if(!living.TryGetValue(room,out var set))living[room]=set=new HashSet<string>();set.Add(actor);}
        public bool IsCleared(string room)=>cleared.Contains(room);
        public void RemoveWithoutReward(string room,string actor)
        {if(living.TryGetValue(room,out var set)){set.Remove(actor);if(set.Count==0)cleared.Add(room);}defeated.Add(actor);}
        public int Remaining(string room)=>living.TryGetValue(room,out var set)?set.Count:0;
        public bool Defeat(string room,string actor)
        {
            if(!living.TryGetValue(room,out var set)||!set.Contains(actor)||!defeated.Add(actor))return false;
            set.Remove(actor);
            if(set.Count==0&&cleared.Add(room))Cleared?.Invoke(room);return true;
        }
        public bool CanEngage(string ownerRoom,string playerRoom)=>!string.IsNullOrEmpty(playerRoom)&&ownerRoom==playerRoom;
    }
}
