using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using Libplanet;
using Libplanet.Crypto;
using Nekoyume.TableData;

namespace Nekoyume.Model.State
{
    [Serializable]
    public class RedeemCodeState : State
    {
        public static readonly Address Address = new Address(new byte[]
            {
                0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x5
            }
        );

        public IReadOnlyDictionary<PublicKey, Reward> Map => _map;

        public class Reward
        {
            public Address? UserAddress;
            public readonly int RewardId;

            public Reward(int rewardId)
            {
                RewardId = rewardId;
            }

            public Reward(Dictionary serialized)
            {
                if (serialized.TryGetValue((Text) "userAddress", out var ua))
                {
                    UserAddress = ua.ToAddress();
                }
                RewardId = serialized["rewardId"].ToInteger();
            }

            public IValue Serialize()
            {
                var values = new Dictionary<IKey, IValue>
                {
                    [(Text) "rewardId"] = RewardId.Serialize(),
                };
                if (UserAddress.HasValue)
                {
                    values.Add((Text) "userAddress", UserAddress.Serialize());
                }

                return new Dictionary(values);
            }
        }

        private Dictionary<PublicKey, Reward> _map = new Dictionary<PublicKey, Reward>();

        public RedeemCodeState(RedeemCodeListSheet sheet) : base(Address)
        {
            //TODO 프라이빗키 목록을 받아서 주소대신 퍼블릭키를 키로 써야함.
            foreach (var row in sheet.Values)
            {
                _map[row.Key] = new Reward(row.RewardId);
            }
        }

        public RedeemCodeState(Dictionary serialized) : base(serialized)
        {
            _map = ((Dictionary) serialized["map"]).ToDictionary(
                kv => kv.Key.ToPublicKey(),
                kv => new Reward((Dictionary) kv.Value)
            );
        }

        public RedeemCodeState(IValue iValue) : base(iValue)
        {
        }

        public override IValue Serialize() =>
            new Dictionary(new Dictionary<IKey, IValue>
            {
                [(Text) "map"] = new Dictionary(_map.Select(kv => new KeyValuePair<IKey, IValue>(
                    (Binary) kv.Key.Serialize(),
                    kv.Value.Serialize()
                )))
            }.Union((Dictionary) base.Serialize()));

        public int Redeem(PublicKey key, Address userAddress)
        {
            if (!_map.ContainsKey(key))
            {
                throw new InvalidRedeemCodeException();
            }

            var result = _map[key];
            if (result.UserAddress.HasValue)
            {
                throw new DuplicateRedeemException($"Code already used by {result.UserAddress}");
            }

            result.UserAddress = userAddress;
            _map[key] = result;
            return result.RewardId;
        }
    }

    [Serializable]
    public class InvalidRedeemCodeException : KeyNotFoundException
    {
    }

    [Serializable]
    public class DuplicateRedeemException : InvalidOperationException
    {
        public DuplicateRedeemException(string s) : base(s)
        {
        }
    }
}
