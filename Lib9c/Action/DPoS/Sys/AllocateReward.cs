using System.Collections.Immutable;
using System.Diagnostics;
using Bencodex.Types;
using Lib9c;
using Libplanet.Action;
using Libplanet.Action.State;
using Nekoyume.Action.DPoS.Control;
using Nekoyume.Action.DPoS.Misc;
using Nekoyume.Action.DPoS.Model;
using Nekoyume.Model.State;
using Nekoyume.Module;

namespace Nekoyume.Action.DPoS.Sys
{
    /// <summary>
    /// An action for allocate reward to validators and delegators in previous block.
    /// Should be executed at the beginning of the block.
    /// </summary>
    public sealed class AllocateReward : ActionBase
    {
        private readonly ActivitySource ActivitySource = new ActivitySource("Lib9c.Action.AllocateReward");

        /// <summary>
        /// Creates a new instance of <see cref="AllocateReward"/>.
        /// </summary>
        public AllocateReward()
        {
        }

       /// <inheritdoc cref="IAction.PlainValue"/>
        public override IValue PlainValue => new Bencodex.Types.Boolean(true);

        /// <inheritdoc cref="IAction.LoadPlainValue(IValue)"/>
        public override void LoadPlainValue(IValue plainValue)
        {
            // Method intentionally left empty.
        }

        /// <inheritdoc cref="IAction.Execute(IActionContext)"/>
        public override IWorld Execute(IActionContext context)
        {
            var states = context.PreviousState;
            using var allocateRewardActivity = ActivitySource.StartActivity("AllocateReward");
            using var getNativeTokensActivity = ActivitySource.StartActivity("GetNativeTokens");
            var nativeTokens = states.GetNativeTokens();
            getNativeTokensActivity?.Dispose();

            using var getProposerInfoActivity = ActivitySource.StartActivity("GetProposerInfo");
            var nullableProposerInfoState = states.GetDPoSState(ReservedAddress.ProposerInfo);
            getProposerInfoActivity?.Dispose();
            if (nullableProposerInfoState is { } proposerInfoState)
            {
                using var allocateRewardCtrlExecuteActivity = ActivitySource.StartActivity("AllocateRewardCtrl.Execute");
                states = AllocateRewardCtrl.Execute(
                    states,
                    context,
                    nativeTokens,
                    context.LastCommit?.Votes,
                    new ProposerInfo(proposerInfoState),
                    ActivitySource);
                allocateRewardCtrlExecuteActivity?.Dispose();
            };

            allocateRewardActivity?.Dispose();
            return states;
        }
    }
}
