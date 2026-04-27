using BridgeOfBlood.Data.Spells;

namespace BridgeOfBlood.Effects
{
	public class RuntimeSpellValue : IValue<RuntimeSpell>
	{
		public RuntimeSpell runtimeSpell;
		public RuntimeSpellValue(RuntimeSpell runtimeSpell)
		{
			this.runtimeSpell = runtimeSpell;
		}
		public RuntimeSpell Resolve(EffectContext context) => this.runtimeSpell;
	}

    public class RuntimeSpellCondition : ICondition
    {
		RuntimeSpell spellToTarget;

		public RuntimeSpellCondition(RuntimeSpell runtimeSpell)
		{
			spellToTarget = runtimeSpell;
		}
        public bool Evaluate(EffectContext context)
        {
			return context.spellInvocation.spells[context.spellInvocation.spellLoopNumber] == spellToTarget;
        }
    }
}
