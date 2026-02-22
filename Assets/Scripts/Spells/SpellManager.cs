using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using NUnit.Framework;
using System.Collections.Generic;
using System.Numerics;
using Unity.Collections;

public class SpellManager
{
    private List<Spell> Spells = new List<Spell>();
    private double timeInvokedLastSpell = -1000;
    private int indexOfLastInvokedSpell = -1;

    public SpellManager(List<Spell> Spells) { }

    public void AddSpell(Spell spell) { }

    public void RemoveSpell(Spell spell) { }

    public Spell AttemptToUseSpell(float deltaTime, double roundTime)
    {
        bool isReadyToCastNextSpell = roundTime - timeInvokedLastSpell > Spells[indexOfLastInvokedSpell].castCompletionDuration;
        Spell spellToCast = isReadyToCastNextSpell ? NextSpell() : null;
        if (spellToCast != null)
        {
            indexOfLastInvokedSpell = indexOfLastInvokedSpell + 1 % Spells.Count;
            timeInvokedLastSpell = roundTime;
        }
        return spellToCast;
    }

    private Spell NextSpell()
    {
        return Spells[indexOfLastInvokedSpell + 1 % Spells.Count];
    }
}




