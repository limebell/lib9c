using System;
using System.Collections.Generic;
using Libplanet.Action;
using Nekoyume.Model;
using Nekoyume.Model.BattleStatus;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Priority_Queue;

namespace Nekoyume.Battle
{
    public abstract class Simulator
    {
        public readonly IRandom Random;
        public readonly BattleLog Log;
        public readonly Player Player;
        public BattleLog.Result Result { get; protected set; }
        public SimplePriorityQueue<CharacterBase, decimal> Characters;
        public const decimal TurnPriority = 100m;
        public readonly TableSheets TableSheets;
        protected const int MaxTurn = 200;
        public int TurnNumber;
        public int WaveNumber;
        public int WaveTurn;

        protected Simulator(
            IRandom random,
            AvatarState avatarState,
            List<Guid> foods,
            TableSheets tableSheets)
        {
            Random = random;
            TableSheets = tableSheets;
            Log = new BattleLog();
            Player = new Player(avatarState, this);
            Player.Use(foods);
            Player.Stats.EqualizeCurrentHPWithHP();
        }

        public abstract Player Simulate();
    }
}
