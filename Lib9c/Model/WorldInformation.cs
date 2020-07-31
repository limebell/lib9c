using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using Nekoyume.Model.State;
using Nekoyume.TableData;

namespace Nekoyume.Model
{
    [Serializable]
    public class WorldInformation : IState
    {
        [Serializable]
        public struct World : IState
        {
            public readonly int Id;
            public readonly string Name;
            public readonly int StageBegin;
            public readonly int StageEnd;
            public readonly long UnlockedBlockIndex;
            public readonly long StageClearedBlockIndex;
            public readonly int StageClearedId;

            public bool IsUnlocked => UnlockedBlockIndex != -1;
            public bool IsStageCleared => StageClearedBlockIndex != -1;

            public World(
                WorldSheet.Row worldRow,
                long unlockedBlockIndex = -1,
                long stageClearedBlockIndex = -1,
                int stageClearedId = -1)
            {
                Id = worldRow.Id;
                Name = worldRow.Name;
                StageBegin = worldRow.StageBegin;
                StageEnd = worldRow.StageEnd;
                UnlockedBlockIndex = unlockedBlockIndex;
                StageClearedBlockIndex = stageClearedBlockIndex;
                StageClearedId = stageClearedId;
            }

            public World(World world, long unlockedBlockIndex = -1)
            {
                Id = world.Id;
                Name = world.Name;
                StageBegin = world.StageBegin;
                StageEnd = world.StageEnd;
                UnlockedBlockIndex = unlockedBlockIndex;
                StageClearedBlockIndex = world.StageClearedBlockIndex;
                StageClearedId = world.StageClearedId;
            }

            public World(World world, long stageClearedBlockIndex, int stageClearedId)
            {
                Id = world.Id;
                Name = world.Name;
                StageBegin = world.StageBegin;
                StageEnd = world.StageEnd;
                UnlockedBlockIndex = world.UnlockedBlockIndex;
                StageClearedBlockIndex = stageClearedBlockIndex;
                StageClearedId = stageClearedId;
            }

            public World(Bencodex.Types.Dictionary serialized)
            {
                Id = serialized.GetInteger("Id");
                Name = serialized.GetString("Name");
                StageBegin = serialized.GetInteger("StageBegin");
                StageEnd = serialized.GetInteger("StageEnd");
                UnlockedBlockIndex = serialized.GetLong("UnlockedBlockIndex");
                StageClearedBlockIndex = serialized.GetLong("StageClearedBlockIndex");
                StageClearedId = serialized.GetInteger("StageClearedId");
            }

            public IValue Serialize()
            {
                return new Bencodex.Types.Dictionary(new Dictionary<IKey, IValue>
                {
                    [(Bencodex.Types.Text) "Id"] = Id.Serialize(),
                    [(Bencodex.Types.Text) "Name"] = Name.Serialize(),
                    [(Bencodex.Types.Text) "StageBegin"] = StageBegin.Serialize(),
                    [(Bencodex.Types.Text) "StageEnd"] = StageEnd.Serialize(),
                    [(Bencodex.Types.Text) "UnlockedBlockIndex"] = UnlockedBlockIndex.Serialize(),
                    [(Bencodex.Types.Text) "StageClearedBlockIndex"] =
                        StageClearedBlockIndex.Serialize(),
                    [(Bencodex.Types.Text) "StageClearedId"] = StageClearedId.Serialize(),
                });
            }

            public bool ContainsStageId(int stageId)
            {
                return stageId >= StageBegin &&
                       stageId <= StageEnd;
            }

            public bool IsPlayable(int stageId)
            {
                return stageId <= GetNextStageIdForPlay();
            }

            public int GetNextStageIdForPlay()
            {
                if (!IsUnlocked)
                    return -1;

                return GetNextStageId();
            }

            public int GetNextStageId()
            {
                return IsStageCleared ? Math.Min(StageEnd, StageClearedId + 1) : StageBegin;
            }
        }

        /// <summary>
        /// key: worldId
        /// </summary>
        private readonly Dictionary<int, World> _worlds = new Dictionary<int, World>();

        public WorldInformation(
            long blockIndex,
            WorldSheet worldSheet,
            bool openAllOfWorldsAndStages = false)
        {
            if (worldSheet is null)
            {
                return;
            }

            var orderedSheet = worldSheet.OrderedList;

            if (openAllOfWorldsAndStages)
            {
                foreach (var row in orderedSheet)
                {
                    _worlds.Add(row.Id, new World(row, blockIndex, blockIndex, row.StageEnd));
                }
            }
            else
            {
                var isFirst = true;
                foreach (var row in orderedSheet)
                {
                    var worldId = row.Id;
                    if (isFirst)
                    {
                        isFirst = false;
                        _worlds.Add(worldId, new World(row, blockIndex));
                    }
                    else
                    {
                        _worlds.Add(worldId, new World(row));
                    }
                }
            }
        }

        public WorldInformation(long blockIndex, WorldSheet worldSheet, int clearStageId = 0)
        {
            if (worldSheet is null)
            {
                return;
            }

            var orderedSheet = worldSheet.OrderedList;

            if (clearStageId > 0)
            {
                foreach (var row in orderedSheet)
                {
                    if (row.StageBegin > clearStageId)
                    {
                        _worlds.Add(row.Id, new World(row));
                    }
                    else if (row.StageEnd > clearStageId)
                    {
                        _worlds.Add(row.Id, new World(row, blockIndex, blockIndex, clearStageId));
                    }
                    else
                    {
                        _worlds.Add(row.Id, new World(row, blockIndex, blockIndex, row.StageEnd));
                    }
                }
            }
            else
            {
                var isFirst = true;
                foreach (var row in orderedSheet)
                {
                    var worldId = row.Id;
                    if (isFirst)
                    {
                        isFirst = false;
                        _worlds.Add(worldId, new World(row, blockIndex));
                    }
                    else
                    {
                        _worlds.Add(worldId, new World(row));
                    }
                }
            }
        }

        public WorldInformation(Bencodex.Types.Dictionary serialized)
        {
            _worlds = serialized.ToDictionary(
                kv => kv.Key.ToInteger(),
                kv => new World((Bencodex.Types.Dictionary) kv.Value)
            );
        }

        public IValue Serialize()
        {
            return new Bencodex.Types.Dictionary(_worlds.Select(kv =>
                new KeyValuePair<IKey, IValue>(
                    (Bencodex.Types.Text) kv.Key.Serialize(),
                    (Bencodex.Types.Dictionary) kv.Value.Serialize())));
        }

        public bool IsWorldUnlocked(int worldId) =>
            TryGetWorld(worldId, out var world)
            && world.IsUnlocked;

        public bool IsStageCleared(int stageId) =>
            TryGetLastClearedStageId(out var clearedStageId)
            && stageId <= clearedStageId;

        public bool TryAddWorld(WorldSheet.Row worldRow, out World world)
        {
            if (worldRow is null ||
                _worlds.ContainsKey(worldRow.Id))
            {
                world = default;
                return false;
            }

            world = new World(worldRow);
            _worlds.Add(worldRow.Id, world);
            return true;
        }

        public bool TryUpdateWorld(WorldSheet.Row worldRow, out World world)
        {
            if (worldRow is null ||
                !_worlds.ContainsKey(worldRow.Id))
            {
                world = default;
                return false;
            }

            var originWorld = _worlds[worldRow.Id];
            world = new World(
                worldRow,
                originWorld.UnlockedBlockIndex,
                originWorld.StageClearedBlockIndex,
                originWorld.StageClearedId
            );
            _worlds[worldRow.Id] = world;
            return true;
        }

        /// <summary>
        /// 인자로 받은 `worldId`에 해당하는 `World` 객체를 얻는다.
        /// </summary>
        /// <param name="worldId"></param>
        /// <param name="world"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public bool TryGetWorld(int worldId, out World world)
        {
            if (!_worlds.ContainsKey(worldId))
            {
                world = default;
                return false;
            }

            world = _worlds[worldId];
            return true;
        }

        public bool TryGetFirstWorld(out World world)
        {
            if (_worlds.Count == 0)
            {
                world = default;
                return false;
            }

            world = _worlds.First(e => true).Value;
            return true;
        }

        /// <summary>
        /// 인자로 받은 `stageId`가 속한 `World` 객체를 얻는다.
        /// </summary>
        /// <param name="stageId"></param>
        /// <param name="world"></param>
        /// <returns></returns>
        public bool TryGetWorldByStageId(int stageId, out World world)
        {
            var worlds = _worlds.Values.Where(e => e.ContainsStageId(stageId)).ToList();
            if (worlds.Count == 0)
            {
                world = default;
                return false;
            }

            world = worlds[0];
            return true;
        }

        /// <summary>
        /// 새롭게 스테이지를 클리어한 시간이 가장 최근인 월드를 얻는다.
        /// </summary>
        /// <param name="world"></param>
        /// <returns></returns>
        public bool TryGetUnlockedWorldByStageClearedBlockIndex(out World world)
        {
            try
            {
                world = _worlds.Values
                    .Where(e => e.IsStageCleared)
                    .OrderByDescending(e => e.StageClearedBlockIndex)
                    .First();
                return true;
            }
            catch
            {
                world = default;
                return false;
            }
        }

        /// <summary>
        /// 마지막으로 클리어한 스테이지 ID를 반환한다.
        /// </summary>
        /// <param name="stageId"></param>
        /// <returns></returns>
        public bool TryGetLastClearedStageId(out int stageId)
        {
            stageId = default;
            var clearedStages = _worlds.Values.Where(world => world.IsStageCleared);

            if (clearedStages.Any())
            {
                stageId = clearedStages.Max(world => world.StageClearedId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 스테이지를 클리어 시킨다.
        /// </summary>
        /// <param name="worldId"></param>
        /// <param name="stageId"></param>
        /// <param name="clearedAt"></param>
        /// <param name="unlockSheet"></param>
        public void ClearStage(
            int worldId,
            int stageId,
            long clearedAt,
            WorldUnlockSheet unlockSheet)
        {
            var world = _worlds[worldId];
            if (stageId <= world.StageClearedId)
            {
                return;
            }

            _worlds[worldId] = new World(world, clearedAt, stageId);

            if (unlockSheet.TryGetUnlockedInformation(worldId, stageId, out var worldIdsToUnlock))
            {
                foreach (var worldIdToUnlock in worldIdsToUnlock)
                {
                    UnlockWorld(worldIdToUnlock, clearedAt);
                }
            }
        }

        /// <summary>
        /// 특정 월드를 잠금 해제한다.
        /// </summary>
        /// <param name="worldId"></param>
        /// <param name="unlockedAt"></param>
        /// <exception cref="KeyNotFoundException"></exception>
        private void UnlockWorld(int worldId, long unlockedAt)
        {
            if (!_worlds.ContainsKey(worldId))
            {
                throw new KeyNotFoundException($"{nameof(worldId)}: {worldId}");
            }

            if (_worlds[worldId].IsUnlocked)
            {
                return;
            }

            var world = _worlds[worldId];
            _worlds[worldId] = new World(world, unlockedAt);
        }
    }
}
