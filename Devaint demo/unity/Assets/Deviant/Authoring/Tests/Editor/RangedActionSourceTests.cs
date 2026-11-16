using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Deviant.Authoring.Tests
{
    public sealed class RangedActionSourceTests
    {
        string repository;RangedActionWindow window;
        [SetUp] public void SetUp()
        {
            repository=BasicMeleeSourceTests.CopyRepository();
            string directory=Path.Combine(repository,"unity/Assets/Deviant/Combat/Resources");Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"MeleeCatalog.json"),Resources.Load<TextAsset>("MeleeCatalog").text);
        }
        [UnityTearDown] public IEnumerator TearDown(){if(window)window.Close();if(Application.isPlaying)yield return new ExitPlayMode();if(Directory.Exists(repository))Directory.Delete(repository,true);}
        [Test] public void CanonicalTwoFileTransportMatchesFreshGeneratedCatalogForEveryRecord()
        {
            var source=RangedActionSource.Load(repository);Assert.That(source.Catalog.Actions.Count,Is.EqualTo(16));
            RangedActionSource.ValidateGenerated(repository,BasicMeleeSourceTests.Generated(repository));
            var snapshots=source.Catalog.ToSnapshot();Assert.That(RangedActionCatalog.FromSnapshot(snapshots).Actions.Select(a=>a.ContentHash),Is.EqualTo(source.Catalog.Actions.Select(a=>a.ContentHash)));
        }
        [Test] public void AtomicSelectedOwnerSavePreservesOtherFileAndGeneratedBuildGateRequiresRegeneration()
        {
            var saved=RangedActionSource.Load(repository);string rangedPath=Path.Combine(repository,RangedActionSource.RangedPath),launcherPath=Path.Combine(repository,RangedActionSource.LauncherPath);
            byte[] ranged=File.ReadAllBytes(rangedPath);var n=saved.Catalog.Read("launcher_move_special").ToNumericData();n.ranged.speed=17;n.ranged.angles=new[]{-15f,0,15f};n.damages[0]=5;
            var candidate=saved.Catalog.Edit("launcher_move_special",n);var next=RangedActionSource.Save(saved,candidate,"launcher_move_special");
            Assert.That(File.ReadAllBytes(rangedPath),Is.EqualTo(ranged));Assert.That(next.Catalog.Read("launcher_move_special").ContentHash,Is.EqualTo(candidate.Read("launcher_move_special").ContentHash));
            Assert.Catch(()=>RangedActionSource.ValidateGenerated(repository,BasicMeleeSourceTests.Generated(repository)));
            BasicSwordSource.Regenerate(repository);RangedActionSource.ValidateGenerated(repository,BasicMeleeSourceTests.Generated(repository));
            byte[] launcher=File.ReadAllBytes(launcherPath);n=next.Catalog.Read("bomb_basic_0").ToNumericData();n.bomb.speed=8;n.bomb.spawnHeight=1.25f;
            candidate=next.Catalog.Edit("bomb_basic_0",n);next=RangedActionSource.Save(next,candidate,"bomb_basic_0");
            Assert.That(File.ReadAllBytes(launcherPath),Is.EqualTo(launcher));Assert.That(next.Catalog.Read("bomb_basic_special").ToNumericData().bomb.speed,Is.EqualTo(8));
            Assert.That(Directory.GetFiles(Path.Combine(repository,"data/combat"),"*.tmp"),Is.Empty);
        }
        [Test] public void EitherExternalFileEditRejectsBeforeSelectedFileChangesAndInvalidCandidateKeepsAcceptedSource()
        {
            var saved=RangedActionSource.Load(repository);string selected=Path.Combine(repository,RangedActionSource.LauncherPath);byte[] before=File.ReadAllBytes(selected);
            File.AppendAllText(Path.Combine(repository,RangedActionSource.RangedPath),"\n");Assert.Catch(()=>RangedActionSource.Save(saved,saved.Catalog,"launcher_range_special"));Assert.That(File.ReadAllBytes(selected),Is.EqualTo(before));
            var fresh=RangedActionSource.Load(repository);var n=fresh.Catalog.Read("shuriken_move_special").ToNumericData();n.ranged.returnDelay=-1;
            Assert.Catch(()=>fresh.Catalog.Edit("shuriken_move_special",n));Assert.That(File.ReadAllBytes(selected),Is.EqualTo(before));
        }
        [UnityTest,Category("RequiresGraphics")] public IEnumerator ActualWindowEditsUndoReadOnlyGeometryAndSelectedSourceSave()
        {
            window=ScriptableObject.CreateInstance<RangedActionWindow>();window.Show();window.CreateGUI();window.LoadRepository(repository);yield return null;
            var root=window.rootVisualElement;root.Q<DropdownField>("action").value="launcher_move_special";var initial=window.Numeric.ranged.speed;
            root.Q<FloatField>("speed").value=initial+3;Undo.FlushUndoRecordObjects();Undo.PerformUndo();yield return null;Assert.That(window.Numeric.ranged.speed,Is.EqualTo(initial));
            Undo.PerformRedo();yield return null;Assert.That(window.Numeric.ranged.speed,Is.EqualTo(initial+3));
            Assert.That(root.Q<FloatField>("fuse"),Is.Null,"Redo must not materialize inactive bomb controls for shotgun.");
            byte[] before=File.ReadAllBytes(Path.Combine(repository,RangedActionSource.RangedPath));root.Q<FloatField>("speed").value=initial+3;
            var scrub=root.Q<Slider>("geometry-time");scrub.value=.5f;scrub.value=.1f;Assert.That(File.ReadAllBytes(Path.Combine(repository,RangedActionSource.RangedPath)),Is.EqualTo(before));
            window.SaveSource();Assert.That(RangedActionSource.Load(repository).Catalog.Read("launcher_move_special").ToNumericData().ranged.speed,Is.EqualTo(initial+3));
            Assert.That(root.Q<Button>("applyNext").enabledSelf,Is.False,"No runtime is discovered automatically.");
        }
        [UnityTest,Category("RequiresGraphics")] public IEnumerator ActualWindowEveryRecordShowsAndSavesItsConsumedNumericFields()
        {
            window=ScriptableObject.CreateInstance<RangedActionWindow>();window.Show();window.CreateGUI();window.LoadRepository(repository);yield return null;
            var root=window.rootVisualElement;
            foreach(string id in RangedActionCatalog.SupportedIds)
            {
                root.Q<DropdownField>("action").value=id;var before=window.CompileCandidate().Read(id);var numeric=window.Numeric;
                Assert.That(root.Q<FloatField>("activation"),Is.Not.Null,id);Assert.That(root.Q<FloatField>("damage"),Is.Not.Null,id);
                if(before.Execution.Kind=="ranged")
                {
                    Assert.That(root.Q<FloatField>("distance"),Is.Not.Null,id);
                    Assert.That(root.Q<FloatField>(before.Policy==RangedActionPolicy.LaserAllOnce?"thickness":"speed"),Is.Not.Null,id);
                    if(before.Policy==RangedActionPolicy.PiercingPellets)Assert.That(root.Q<TextField>("angles"),Is.Not.Null,id);
                    if(before.Policy==RangedActionPolicy.PiercingVisitReturn)Assert.That(root.Q<FloatField>("returnDelay"),Is.Not.Null,id);
                    if(before.Policy==RangedActionPolicy.PeriodicPiercing)Assert.That(root.Q<FloatField>("hitInterval"),Is.Not.Null,id);
                    if(before.Policy!=RangedActionPolicy.FirstContactBlast)Assert.That(root.Q<FloatField>("blastRadius"),Is.Null,"Straight projectile must not acquire an ignored empty-bomb control after Unity draft serialization.");
                }
                if(before.Execution.Kind=="bomb")
                {
                    Assert.That(root.Q<FloatField>("blastRadius"),Is.Not.Null,id);
                    if(before.Policy==RangedActionPolicy.ContactBombWithFragments)Assert.That(root.Q<TextField>("fragmentAngles"),Is.Not.Null,id);
                    if(before.Policy==RangedActionPolicy.LandingFuseBomb)Assert.That(root.Q<FloatField>("fuse"),Is.Not.Null,id);
                    if(id=="bomb_basic_special")Assert.That(root.Q<FloatField>("speed").enabledSelf,Is.False,"Inherited speed must not masquerade as independently authored.");
                }
                if(before.Execution.Kind=="missile")Assert.That(root.Q<FloatField>("manualDelay"),Is.Not.Null,id);
                root.Q<FloatField>("damage").value=numeric.damages[0]+1;string changed=window.CompileCandidate().Read(id).ContentHash;
                Assert.That(changed,Is.Not.EqualTo(before.ContentHash),id);
                root.Q<Slider>("geometry-time").value=.35f;root.Q<Slider>("geometry-time").value=.05f;
                window.SaveSource();Assert.That(RangedActionSource.Load(repository).Catalog.Read(id).ContentHash,Is.EqualTo(changed),id);
            }
        }
    }
}
