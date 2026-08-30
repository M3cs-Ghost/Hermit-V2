namespace Hermit.Networking
{
    /// <summary>
    /// Contract for persisting a session locally between app launches. C3 ships
    /// no implementation of this interface at all — C4 decides secure storage
    /// per platform (Keychain/Keystore/DPAPI, per the Blueprint) before any real
    /// token is ever written to disk.
    /// </summary>
    public interface ISessionStore
    {
        bool HasStoredSession { get; }

        void Save(HermitSession session);

        HermitSession? Load();

        void Clear();
    }
}
