/// <summary>
/// IWalletService for test scenes. Starts with configurable gold; defaults to effectively unlimited.
/// </summary>
public class MockWalletService : IWalletService
{
    public int Gold { get; private set; }

    public MockWalletService() : this(999999) { }

    public MockWalletService(int startingGold)
    {
        Gold = startingGold;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }
}
