using Deviant.Combat;

namespace ForestCombatV2
{
    // Optional scene content. Legacy scenes keep their existing catalog and enemy factory.
    public interface IChapter1MonsterContent
    {
        void Prepare(ForestData data);
        FieldMouseDefinition Definition(string species);
        FieldMouseEnemy CreateEnemy(CombatArena arena, CombatEnemySpawn request, MonsterKind kind,
            StaticArenaSurfaceIndex surfaces);
    }
}
