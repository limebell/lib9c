using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using Bencodex;
using Bencodex.Types;
using Libplanet;
using Nekoyume.Action;
using Nekoyume.Battle;
using Nekoyume.Model.BattleStatus;
using Nekoyume.Model.Item;
using Nekoyume.Model.WeeklyArena;
using Nekoyume.TableData;

namespace Nekoyume.Model.State
{
    [Serializable]
    public class WeeklyArenaState : State, IDictionary<Address, ArenaInfo>, ISerializable
    {
        #region static

        private static Address _baseAddress = new Address(new byte[]
        {
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 10
        });

        public static Address DeriveAddress(int index)
        {
            return _baseAddress.Derive($"weekly_arena_{index}");
        }

        #endregion

        public BigInteger Gold;

        public long ResetIndex;

        public bool Ended;

        private readonly Dictionary<Address, ArenaInfo> _map;
        private Dictionary<TierType, BigInteger> _rewardMap = new Dictionary<TierType, BigInteger>();

        public List<ArenaInfo> OrderedArenaInfos { get; private set; }

        public WeeklyArenaState(int index) : base(DeriveAddress(index))
        {
            _map = new Dictionary<Address, ArenaInfo>();
            ResetOrderedArenaInfos();
        }

        public WeeklyArenaState(Address address) : base(address)
        {
            _map = new Dictionary<Address, ArenaInfo>();
            ResetOrderedArenaInfos();
        }

        public WeeklyArenaState(Dictionary serialized) : base(serialized)
        {
            _map = ((Dictionary)serialized["map"]).ToDictionary(
                kv => kv.Key.ToAddress(),
                kv => new ArenaInfo((Dictionary)kv.Value)
            );

            ResetIndex = serialized.GetLong("resetIndex");

            if (serialized.ContainsKey((IKey)(Text)"rewardMap"))
            {
                _rewardMap = ((Dictionary)serialized["rewardMap"]).ToDictionary(
                    kv => (TierType)((Binary)kv.Key).First(),
                    kv => kv.Value.ToBigInteger());
            }

            Gold = serialized["gold"].ToBigInteger();
            Ended = serialized["ended"].ToBoolean();
            ResetOrderedArenaInfos();
        }

        public WeeklyArenaState(IValue iValue) : this((Dictionary)iValue)
        {
        }

        protected WeeklyArenaState(SerializationInfo info, StreamingContext context)
            : this((Dictionary)new Codec().Decode((byte[]) info.GetValue("serialized", typeof(byte[]))))
        {
        }

        public override IValue Serialize() =>
            new Dictionary(new Dictionary<IKey, IValue>
            {
                [(Text)"map"] = new Dictionary(_map.Select(kv =>
                   new KeyValuePair<IKey, IValue>(
                       (Binary)kv.Key.Serialize(),
                       kv.Value.Serialize()
                   )
                )),
                [(Text)"resetIndex"] = ResetIndex.Serialize(),
                [(Text)"rewardMap"] = new Dictionary(_rewardMap.Select(kv =>
                   new KeyValuePair<IKey, IValue>(
                       new Binary(new[] { (byte)kv.Key }),
                       kv.Value.Serialize()
                   )
                )),
                [(Text)"gold"] = Gold.Serialize(),
                [(Text)"ended"] = Ended.Serialize(),
            }.Union((Dictionary)base.Serialize()));

        private void ResetOrderedArenaInfos()
        {
            OrderedArenaInfos = _map.Values
                .OrderByDescending(pair => pair.Score)
                .ThenBy(pair => pair.CombatPoint)
                .ToList();
        }

        /// <summary>
        /// 인자로 넘겨 받은 `avatarAddress`를 기준으로 상위와 하위 범위에 해당하는 랭킹 정보를 얻습니다.
        /// </summary>
        /// <param name="avatarAddress"></param>
        /// <param name="upperRange">상위 범위</param>
        /// <param name="lowerRange">하위 범위</param>
        /// <returns></returns>
        public List<(int rank, ArenaInfo arenaInfo)> GetArenaInfos(
            Address avatarAddress,
            int upperRange = 10,
            int lowerRange = 10)
        {
            var avatarIndex = -1;
            for (var i = 0; i < OrderedArenaInfos.Count; i++)
            {
                var pair = OrderedArenaInfos[i];
                if (!pair.AvatarAddress.Equals(avatarAddress))
                {
                    continue;
                }

                avatarIndex = i;
                break;
            }

            if (avatarIndex == -1)
            {
                return new List<(int rank, ArenaInfo arenaInfo)>();
            }

            var firstIndex = Math.Max(0, avatarIndex - upperRange);
            var lastIndex = Math.Min(avatarIndex + lowerRange, OrderedArenaInfos.Count - 1);
            var offsetIndex = 1;
            return OrderedArenaInfos.GetRange(firstIndex, lastIndex - firstIndex + 1)
                .Select(arenaInfo => (firstIndex + offsetIndex++, arenaInfo))
                .ToList();
        }

        public ArenaInfo GetArenaInfo(Address avatarAddress)
        {
            return OrderedArenaInfos.FirstOrDefault(info => info.AvatarAddress.Equals(avatarAddress));
        }

        private void Update(AvatarState avatarState, CharacterSheet characterSheet, bool active = false)
        {
            Add(avatarState.address, new ArenaInfo(avatarState, characterSheet, active));
        }

        public void Update(ArenaInfo info)
        {
            Add(info.AvatarAddress, info);
        }

        public void Set(AvatarState avatarState, CharacterSheet characterSheet)
        {
            Update(avatarState, characterSheet);
        }

        public void ResetCount(long ctxBlockIndex)
        {
            foreach (var info in _map.Values)
            {
                info.ResetCount();
            }

            ResetIndex = ctxBlockIndex;
        }

        public void End()
        {
            SetRewardMap();
            Ended = true;
        }

        public void Update(WeeklyArenaState prevState, long index)
        {
            var filtered = prevState.Where(i => i.Value.Active).ToList();
            foreach (var kv in filtered)
            {
                var value = new ArenaInfo(kv.Value);
                _map[kv.Key] = value;
            }
            ResetIndex = index;
        }

        public void SetReceive(Address avatarAddress)
        {
            _map[avatarAddress].Receive = true;
        }

        public TierType GetTier(ArenaInfo info)
        {
            var sorted = _map.Values.Where(i => i.Active).OrderBy(i => i.Score).ThenBy(i => i.CombatPoint).ToList();
            if (info.ArenaRecord.Win >= 5)
            {
                return TierType.Platinum;
            }

            if (info.ArenaRecord.Win >= 4)
            {
                return TierType.Gold;
            }

            if (info.ArenaRecord.Win >= 3)
            {
                return TierType.Silver;
            }

            if (info.ArenaRecord.Win >= 2)
            {
                return TierType.Bronze;
            }

            return TierType.Rookie;
        }

        private void SetRewardMap()
        {
            var map = new Dictionary<TierType, BigInteger>
            {
                [TierType.Platinum] = 200,
                [TierType.Gold] = 150,
                [TierType.Silver] = 100,
                [TierType.Bronze] = 80,
                [TierType.Rookie] = 70,
            };
            _rewardMap = map;
        }
        public BigInteger GetReward(TierType tier)
        {
            return _rewardMap[tier];
        }

        public Address[] GetAgentAddresses(int count)
        {
            var sorted = _map.Values
                .Where(i => i.Active)
                .OrderByDescending(i => i.Score)
                .ThenBy(i => i.CombatPoint)
                .ToList();
            var result = new HashSet<Address>();
            foreach (var info in sorted)
            {
                result.Add(info.AgentAddress);
                if (result.Count == count)
                    break;
            }

            return result.ToArray();
        }

        #region IDictionary

        public IEnumerator<KeyValuePair<Address, ArenaInfo>> GetEnumerator()
        {
            return _map.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<Address, ArenaInfo> item)
        {
            _map[item.Key] = item.Value;
            ResetOrderedArenaInfos();
        }

        public void Clear()
        {
            _map.Clear();
            ResetOrderedArenaInfos();
        }

        public bool Contains(KeyValuePair<Address, ArenaInfo> item)
        {
            return _map.Contains(item);
        }

        public void CopyTo(KeyValuePair<Address, ArenaInfo>[] array, int arrayIndex)
        {

            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<Address, ArenaInfo> item)
        {
            return Remove(item.Key);
        }

        public int Count => _map.Count;
        public bool IsReadOnly => false;

        public void Add(Address key, ArenaInfo value)
        {
            Add(new KeyValuePair<Address, ArenaInfo>(key, value));
        }

        public bool ContainsKey(Address key)
        {
            return _map.ContainsKey(key);
        }

        public bool Remove(Address key)
        {
            var result = _map.Remove(key);
            ResetOrderedArenaInfos();
            return result;
        }

        public bool TryGetValue(Address key, out ArenaInfo value)
        {
            return _map.TryGetValue(key, out value);
        }

        public ArenaInfo this[Address key]
        {
            get => _map[key];
            set
            {
                _map[key] = value;
                ResetOrderedArenaInfos();
            }
        }

        public ICollection<Address> Keys => _map.Keys;
        public ICollection<ArenaInfo> Values => _map.Values;

        #endregion

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("serialized", new Codec().Encode(Serialize()));
        }
    }

    public class ArenaInfo : IState
    {
        public class Record : IState
        {
            public int Win;
            public int Lose;
            public int Draw;

            public Record()
            {
            }

            public Record(Dictionary serialized)
            {
                Win = serialized.GetInteger("win");
                Lose = serialized.GetInteger("lose");
                Draw = serialized.GetInteger("draw");
            }

            public IValue Serialize() =>
                Dictionary.Empty
                    .Add("win", Win.Serialize())
                    .Add("lose", Lose.Serialize())
                    .Add("draw", Draw.Serialize());
        }
        public readonly Address AvatarAddress;
        public readonly Address AgentAddress;
        public readonly string AvatarName;
        public readonly Record ArenaRecord;
        public int Level { get; private set; }
        public int CombatPoint { get; private set; }
        public int ArmorId { get; private set; }
        public bool Active { get; private set; }
        public int DailyChallengeCount { get; private set; }
        public int Score { get; private set; }
        public bool Receive;

        public ArenaInfo(AvatarState avatarState, CharacterSheet characterSheet, bool active)
        {
            AvatarAddress = avatarState.address;
            AgentAddress = avatarState.agentAddress;
            AvatarName = avatarState.NameWithHash;
            ArenaRecord = new Record();
            Level = avatarState.level;
            var armor = avatarState.inventory.Items.Select(i => i.item).OfType<Armor>().FirstOrDefault(e => e.equipped);
            ArmorId = armor?.Id ?? GameConfig.DefaultAvatarArmorId;
            CombatPoint = CPHelper.GetCP(avatarState, characterSheet);
            Active = active;
            DailyChallengeCount = GameConfig.ArenaChallengeCountMax;
            Score = GameConfig.ArenaScoreDefault;
        }

        public ArenaInfo(Dictionary serialized)
        {
            AvatarAddress = serialized.GetAddress("avatarAddress");
            AgentAddress = serialized.GetAddress("agentAddress");
            AvatarName = serialized.GetString("avatarName");
            ArenaRecord = serialized.ContainsKey((IKey)(Text)"arenaRecord")
                ? new Record((Dictionary)serialized["arenaRecord"])
                : new Record();
            Level = serialized.GetInteger("level");
            ArmorId = serialized.GetInteger("armorId");
            CombatPoint = serialized.GetInteger("combatPoint");
            Active = serialized.GetBoolean("active");
            DailyChallengeCount = serialized.GetInteger("dailyChallengeCount");
            Score = serialized.GetInteger("score");
            Receive = serialized["receive"].ToBoolean();
        }

        public ArenaInfo(ArenaInfo prevInfo)
        {
            AvatarAddress = prevInfo.AvatarAddress;
            AgentAddress = prevInfo.AgentAddress;
            ArmorId = prevInfo.ArmorId;
            Level = prevInfo.Level;
            AvatarName = prevInfo.AvatarName;
            CombatPoint = 100;
            Score = 1000;
            DailyChallengeCount = 5;
            Active = false;
            ArenaRecord = new Record();
        }

        public IValue Serialize() =>
            new Dictionary(new Dictionary<IKey, IValue>
            {
                [(Text)"avatarAddress"] = AvatarAddress.Serialize(),
                [(Text)"agentAddress"] = AgentAddress.Serialize(),
                [(Text)"avatarName"] = AvatarName.Serialize(),
                [(Text)"arenaRecord"] = ArenaRecord.Serialize(),
                [(Text)"level"] = Level.Serialize(),
                [(Text)"armorId"] = ArmorId.Serialize(),
                [(Text)"combatPoint"] = CombatPoint.Serialize(),
                [(Text)"active"] = Active.Serialize(),
                [(Text)"dailyChallengeCount"] = DailyChallengeCount.Serialize(),
                [(Text)"score"] = Score.Serialize(),
                [(Text)"receive"] = Receive.Serialize(),
            });

        public void Update(AvatarState state, CharacterSheet characterSheet)
        {
            ArmorId = state.GetArmorId();
            Level = state.level;
            CombatPoint = CPHelper.GetCP(state, characterSheet);
        }

        public int Update(AvatarState avatarState, ArenaInfo enemyInfo, BattleLog.Result result)
        {
            switch (result)
            {
                case BattleLog.Result.Win:
                    ArenaRecord.Win++;
                    break;
                case BattleLog.Result.Lose:
                    ArenaRecord.Lose++;
                    break;
                case BattleLog.Result.TimeOver:
                    ArenaRecord.Draw++;
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }

            var score = ArenaScoreHelper.GetScore(Score, enemyInfo.Score, result);
            var calculated = Score + score;
            var current = Score;
            Score = Math.Max(1000, calculated);
            DailyChallengeCount--;
            ArmorId = avatarState.GetArmorId();
            Level = avatarState.level;
            return Score - current;
        }

        public void Activate()
        {
            Active = true;
        }

        public void ResetCount()
        {
            DailyChallengeCount = 5;
        }
    }
}
