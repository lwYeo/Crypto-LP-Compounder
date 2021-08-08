using System;

namespace DTO
{
    public interface ICompounder
    {
        string Name { get; }

        DateTimeOffset LastUpdate { get; }

        string[] Summary { get; }

        ValueSymbol CurrentAPR { get; }

        ValueSymbol OptimalAPY { get; }

        int OptimalCompoundsPerYear { get; }

        ValueSymbol EstimateGasPerTxn { get; }

        TokenValue CurrentDeposit { get; }

        TokenValue UnderlyingTokenA_Deposit { get; }

        TokenValue UnderlyingTokenB_Deposit { get; }

        TokenValue CurrentPendingReward { get; }

        TokenValue TokenA { get; }

        TokenValue TokenB { get; }

        TokenValue Reward { get; }
    }
}
