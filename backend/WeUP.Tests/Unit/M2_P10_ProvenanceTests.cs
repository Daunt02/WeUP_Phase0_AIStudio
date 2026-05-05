using System;
using System.Threading.Tasks;
using Xunit;

namespace WeUP.Tests.Unit;

public sealed class M2_P10_ProvenanceTests
{
    [Fact(Skip = "Placeholder: needs EF in-memory/Postgres test harness")]
    public async Task Insert_FirstInsertSucceeds_SecondDuplicateIgnored()
    {
        // Placeholder test: implement with InMemory/Postgres harness.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Placeholder: needs EF in-memory/Postgres test harness")]
    public async Task GetLineage_ReturnsEntriesInChronologicalOrder()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Placeholder: verify EF configuration throws on updates; requires Postgres migration")]
    public async Task Update_OnExistingRow_Throws()
    {
        await Task.CompletedTask;
    }
}
