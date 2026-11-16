using NUnit.Framework;
namespace Deviant.Stage1Monsters.Game.Tests
{
    public sealed class Stage1ScriptBindingTests
    {
        [Test] public void PlayerCatalogsAndPrefabScriptsResolveAfterSynchronousImport()
        {
            Deviant.Stage1Monsters.Editor.Stage1RuntimeAssetValidation.ReimportAndValidate();
            Deviant.Stage1Monsters.Editor.Stage1RuntimeAssetValidation.Validate();
        }
    }
}
