using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Serilog;

namespace Nekoyume.BlockChain
{
    public class Miner
    {
        private BlockChain<PolymorphicAction<ActionBase>> _chain;
        private Swarm<PolymorphicAction<ActionBase>> _swarm;
        private PrivateKey _privateKey;

        public Address Address { get; }

        public async Task MineBlockAsync(CancellationToken cancellationToken)
        {
            var txs = new HashSet<Transaction<PolymorphicAction<ActionBase>>>();

            var timeStamp = DateTimeOffset.UtcNow;
            var prevTimeStamp = _chain?.Tip?.Timestamp;
            // FIXME 년도가 바뀌면 깨지는 계산 방식. 테스트 끝나면 변경해야함
            // 하루 한번 보상을 제공
            // TODO: Move ranking reward logic and condition into block action.
            if (prevTimeStamp is DateTimeOffset t && timeStamp.DayOfYear - t.DayOfYear == 1)
            {
                var rankingRewardTx = RankingReward();
                txs.Add(rankingRewardTx);
            }

            var invalidTxs = txs;

            try
            {
                var block = await _chain.MineBlock(Address, DateTimeOffset.UtcNow, cancellationToken: cancellationToken);

                if (_swarm.Running)
                {
                    _swarm.BroadcastBlock(block);
                }
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Mining was canceled due to change of tip.");
            }
            catch (InvalidTxException invalidTxException)
            {
                var invalidTx = _chain.GetTransaction(invalidTxException.TxId);

                Log.Debug($"Tx[{invalidTxException.TxId}] is invalid. mark to unstage.");
                invalidTxs.Add(invalidTx);
            }
            catch (UnexpectedlyTerminatedActionException actionException)
            {
                if (actionException.TxId is TxId txId)
                {
                    Log.Debug(
                        $"Tx[{actionException.TxId}]'s action is invalid. mark to unstage. {actionException}");
                    invalidTxs.Add(_chain.GetTransaction(txId));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"exception was thrown. {ex}");
            }
            finally
            {
                foreach (var invalidTx in invalidTxs)
                {
                    _chain.UnstageTransaction(invalidTx);
                }
            }
        }

        public Miner(BlockChain<PolymorphicAction<ActionBase>> chain, Swarm<PolymorphicAction<ActionBase>> swarm, PrivateKey privateKey)
        {
            _chain = chain ?? throw new ArgumentNullException(nameof(chain));
            _swarm = swarm ?? throw new ArgumentNullException(nameof(swarm));

            _privateKey = privateKey;
            Address = _privateKey.PublicKey.ToAddress();
        }

        private Transaction<PolymorphicAction<ActionBase>> RankingReward()
        {
            // private 테스트용 임시 로직 변경
            var index = (int) _chain.Tip.Index / GameConfig.WeeklyArenaInterval;
            var weeklyArenaAddress = WeeklyArenaState.DeriveAddress(index);
            var weeklyArenaState = new WeeklyArenaState(_chain.GetState(weeklyArenaAddress));
            var actions = new List<PolymorphicAction<ActionBase>>
            {
                new RankingReward
                {
                    gold1 = 50,
                    gold2 = 30,
                    gold3 = 10,
                    agentAddresses = weeklyArenaState.GetAgentAddresses(3),
                }
            };
            return _chain.MakeTransaction(_privateKey, actions.ToImmutableHashSet());
        }
    }
}
