using System;
using System.Collections.Generic;
using Nekoyume.Model.Stat;
using static Nekoyume.TableData.TableExtensions;

namespace Nekoyume.TableData
{
    public class EquipmentItemOptionSheet : Sheet<int, EquipmentItemOptionSheet.Row>
    {
        public class Row: SheetRow<int>
        {
            public override int Key => Id;
            public int Id { get; private set; }
            public StatType StatType { get; private set; }
            public int StatMin { get; private set; }
            public int StatMax { get; private set; }
            public int SkillId { get; private set; }
            public int SkillDamageMin { get; private set; }
            public int SkillDamageMax { get; private set; }
            public int SkillChanceMin { get; private set; }
            public int SkillChanceMax { get; private set; }

            public override void Set(IReadOnlyList<string> fields)
            {
                Id = ParseInt(fields[0]);
                StatType = string.IsNullOrEmpty(fields[1])
                    ? StatType.NONE
                    : (StatType) Enum.Parse(typeof(StatType), fields[1]);
                StatMin = string.IsNullOrEmpty(fields[2]) ? 0 : ParseInt(fields[2]);
                StatMax = string.IsNullOrEmpty(fields[3]) ? 0 : ParseInt(fields[3]);
                SkillId = string.IsNullOrEmpty(fields[4]) ? 0 : ParseInt(fields[4]);
                SkillDamageMin = string.IsNullOrEmpty(fields[5]) ? 0 : ParseInt(fields[5]);
                SkillDamageMax = string.IsNullOrEmpty(fields[6]) ? 0 : ParseInt(fields[6]);
                SkillChanceMin = string.IsNullOrEmpty(fields[7]) ? 0 : ParseInt(fields[7]);
                SkillChanceMax = string.IsNullOrEmpty(fields[8]) ? 0 : ParseInt(fields[8]);
            }
        }

        public EquipmentItemOptionSheet() : base(nameof(EquipmentItemOptionSheet))
        {
        }
    }
}
