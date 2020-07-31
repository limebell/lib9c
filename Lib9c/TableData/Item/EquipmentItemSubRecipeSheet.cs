using System.Collections.Generic;
using static Nekoyume.TableData.TableExtensions;

namespace Nekoyume.TableData
{
    public class EquipmentItemSubRecipeSheet : Sheet<int, EquipmentItemSubRecipeSheet.Row>
    {
        public struct MaterialInfo
        {
            public readonly int Id;
            public readonly int Count;

            public MaterialInfo(int id, int count)
            {
                Id = id;
                Count = count;
            }
        }

        public struct OptionInfo
        {
            public readonly int Id;
            public readonly decimal Ratio;

            public OptionInfo(int id, decimal ratio)
            {
                Id = id;
                Ratio = ratio;
            }
        }

        public class Row : SheetRow<int>
        {
            public override int Key => Id;
            public int Id { get; private set; }
            public int RequiredActionPoint { get; private set; }
            public long RequiredGold { get; private set; }
            public long RequiredBlockIndex { get; private set; }
            public int UnlockStage { get; private set; }
            public List<MaterialInfo> Materials { get; private set; }
            public List<OptionInfo> Options { get; private set; }
            public int MaxOptionLimit { get; private set; }

            public override void Set(IReadOnlyList<string> fields)
            {
                Id = ParseInt(fields[0]);
                RequiredActionPoint = ParseInt(fields[1]);
                RequiredGold = ParseLong(fields[2]);
                RequiredBlockIndex = ParseInt(fields[3]);
                UnlockStage = ParseInt(fields[4]);
                Materials = new List<MaterialInfo>();
                Options = new List<OptionInfo>();
                for (var i = 0; i < 3; i++)
                {
                    var offset = i * 2;
                    if (string.IsNullOrEmpty(fields[5 + offset]) || string.IsNullOrEmpty(fields[6 + offset]))
                        continue;

                    Materials.Add(new MaterialInfo(ParseInt(fields[5 + offset]), ParseInt(fields[6 + offset])));
                }

                for (var i = 0; i < 4; i++)
                {
                    var offset = i * 2;
                    if (string.IsNullOrEmpty(fields[11 + offset]) || string.IsNullOrEmpty(fields[12 + offset]))
                        continue;

                    Options.Add(new OptionInfo(ParseInt(fields[11 + offset]), ParseDecimal(fields[12 + offset])));
                }
                MaxOptionLimit = ParseInt(fields[19]);
            }
        }

        public EquipmentItemSubRecipeSheet() : base(nameof(EquipmentItemSubRecipeSheet))
        {
        }
    }
}
