using System.Collections.Generic;
using System.Linq;
using Nekoyume.Model;
using Nekoyume.Model.BattleStatus;

namespace Nekoyume.Battle
{
    public class Wave
    {
        private readonly List<Enemy> _enemies = new List<Enemy>();
        public bool HasBoss;

        public void Add(Enemy enemy)
        {
            _enemies.Add(enemy);
        }

        public void Spawn(StageSimulator stageSimulator)
        {
            foreach (var enemy in _enemies)
            {
                stageSimulator.Player.Targets.Add(enemy);
                stageSimulator.Characters.Enqueue(enemy, Simulator.TurnPriority / enemy.SPD);
                enemy.InitAI();
            }

            var enemies = _enemies.Select(enemy => new Enemy(enemy)).ToList();
            var spawnWave = new SpawnWave(null, stageSimulator.WaveNumber, stageSimulator.WaveTurn, enemies, HasBoss);
            stageSimulator.Log.Add(spawnWave);
        }
    }
}
