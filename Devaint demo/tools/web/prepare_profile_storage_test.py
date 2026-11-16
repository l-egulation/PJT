"""Prepare an isolated Web storage fixture using the game's actual profile code and exact Editor/packages/settings."""
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "outputs/profile-storage/project"


def prepare():
    marker = PROJECT / ".deviant-profile-storage-fixture"
    if PROJECT.exists() and any(PROJECT.iterdir()) and not marker.exists():
        raise RuntimeError("Refusing to overwrite an unowned verification project")
    PROJECT.mkdir(parents=True, exist_ok=True)
    marker.write_text("isolated browser profile storage verification\n")
    for name in ["Packages", "ProjectSettings"]:
        shutil.copytree(ROOT / "unity" / name, PROJECT / name, dirs_exist_ok=True)
    shutil.copytree(ROOT / "unity/Assets/Deviant/Run/Runtime", PROJECT / "Assets/Run", dirs_exist_ok=True)
    files = {
        "unity/Assets/ForestCombatV2/Runtime/Chapter1ProfileHost.cs": "Assets/Chapter1ProfileHost.cs",
        "unity/Assets/Deviant/Hub/Resources/RogueliteProgression.json": "Assets/Resources/RogueliteProgression.json",
        "unity/Assets/Deviant/Integration/Unity/WebBuildStorageReset.cs": "Assets/WebBuildStorageReset.cs",
        "unity/Assets/Deviant/Game/Plugins/BrowserPersistence.jspre": "Assets/Plugins/BrowserPersistence.jspre",
        "tools/web/fixtures/ProfilePersistenceProbe.cs": "Assets/ProfilePersistenceProbe.cs",
        "tools/web/fixtures/ProfilePersistenceProbe.jslib": "Assets/Plugins/ProfilePersistenceProbe.jslib",
        "tools/web/fixtures/ProfilePersistenceBuild.cs": "Assets/Editor/ProfilePersistenceBuild.cs",
    }
    for source, target in files.items():
        dest = PROJECT / target
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / source, dest)
    print(PROJECT)


if __name__ == "__main__":
    prepare()
