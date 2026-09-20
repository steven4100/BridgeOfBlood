using BridgeOfBlood.Data.Spells;

/// <summary>
/// Resolved spell modifications for UI and other non-combat readers.
/// Registered by lab/game bootstrap via <see cref="EZServiceLocation.ServiceLocator"/>.
/// </summary>
public interface ISpellModificationService
{
    ModificationSet GetSpellModCollectionForSpell(RuntimeSpell spell);
}

/// <summary>
/// Shop/test scenes with no combat tick. Returns an empty resolved bag.
/// </summary>
public sealed class EmptySpellModificationService : ISpellModificationService
{
    readonly ModificationSet _empty = new ModificationSet();

    public ModificationSet GetSpellModCollectionForSpell(RuntimeSpell spell)
    {
        return _empty;
    }
}
