using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class CharacterFootPresentationTests
    {
        [UnityTest] public IEnumerator MissingBootstrapIdleDoesNotOffsetOrStretchLoadedCharacter()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            var arenaBefore=Object.FindAnyObjectByType<CombatArena>();
            var composition=arenaBefore.GetComponent("GameComposition");
            var binding=new SerializedObject(composition); binding.FindProperty("playerSprite").objectReferenceValue=null; binding.ApplyModifiedPropertiesWithoutUndo();
            arenaBefore.loadCharacterArt=true;
            yield return new EnterPlayMode(); yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();
            foreach(var enemy in arena.enemies) enemy.aggressive=false;
            arena.Player.UseTestInput=true;
            yield return null;
            Assert.That(arena.Player.visual.sprite,Is.Not.Null);
            Assert.That(arena.Player.visual.transform.localPosition,Is.EqualTo(Vector3.zero));
            Assert.That(arena.Player.visual.transform.localScale,Is.EqualTo(Vector3.one));
            Assert.That(arena.Player.visual.sprite.pixelsPerUnit,Is.EqualTo(32));
            float until=arena.Now+.3f,deadline=Time.realtimeSinceStartup+10;
            while(arena.Now<until)
            { Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline)); yield return null; }
            Assert.That(arena.Player.grounded,Is.True);
            arena.Player.SyncFacing(-1); yield return null;
            Assert.That(arena.Player.visual.transform.localPosition,Is.EqualTo(Vector3.zero));
            Assert.That(arena.Player.visual.transform.localScale,Is.EqualTo(Vector3.one));
            arena.Player.SyncFacing(1);
            yield return new ExitPlayMode();
        }
    }
}
