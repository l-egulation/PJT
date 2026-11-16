using System;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // Both the console and retained editor callers use the current world's room navigation.
    public static class Chapter1DebugTravel
    {
        public static int BossIndex(ForestCombatWorld world) => world && world.Data?.rooms != null
            ? Array.FindIndex(world.Data.rooms, room => room.kind == "boss") : -1;

        public static bool TryToBoss(ForestCombatWorld world, out string error, bool consoleCommand = false)
        {
            error = null;
            if (!DevFeatures.Enabled) return Refuse("개발자 콘솔이 비활성화되어 있습니다.", out error);
            if (!world || !world.Ready || !world.proceduralRooms || world.dummyTrainingOnly || !world.arena)
                return Refuse("1챕터 랜덤 스테이지가 준비된 상태에서 사용하세요.", out error);
            var arena = world.arena;
            var player = arena.Player;
            if (!player || player.Dead) return Refuse("살아 있는 플레이어가 필요합니다.", out error);
            if (player.CheckpointBusy) return Refuse("포탈 이동 중에는 이동할 수 없습니다.", out error);
            if (!consoleCommand && (player.InputBlocked || arena.Controls.Current.SimulationPaused))
                return Refuse("다른 화면이나 연출이 게임을 정지·입력 차단하고 있습니다.", out error);
            var binding = world.GetComponent<Chapter1RunBinding>();
            if (binding && binding.Run != null && binding.Run.Ended)
                return Refuse("종료된 런에서는 이동할 수 없습니다.", out error);
            int index = BossIndex(world);
            if (index < 0 || world.Data.views == null || index >= world.Data.views.Length)
                return Refuse("현재 맵에 보스방 이동 지점이 없습니다.", out error);
            var room = world.Data.rooms[index];
            // Use exactly the navigation used by '구간 이동 → 보스방'. The authored entry row is the floor contract;
            // a separate short raycast or the presence of a legacy selector must not prevent room navigation.
            world.Warp(index);
            player.ClearInputState();
            player.Position = new Vector2(room.x + 3, -(room.y + room.entryY) + .02f);
            player.velocity = Vector2.zero;
            player.jumps.Reset();
            Physics2D.SyncTransforms();
            player.grounded = player.ProbeGround();
            world.GetComponent<Chapter1BossRoomEnclosure>()?.Hold();
            world.RefreshCamera(0, true);
            return true;
        }
        static bool Refuse(string message, out string error) { error = message; return false; }
    }
}
