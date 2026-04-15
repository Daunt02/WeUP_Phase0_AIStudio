namespace WeUP.Api.Configuration;

public sealed record PersistenceRuntime(
    PersistenceMode Mode,
    string ConnectionStringName,
    bool UsesDatabase)
{
    public bool UsesStubRepositories => Mode == PersistenceMode.Stub;
}