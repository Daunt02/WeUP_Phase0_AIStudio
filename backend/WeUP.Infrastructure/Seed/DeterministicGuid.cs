using System.Security.Cryptography;
using System.Text;

namespace WeUP.Infrastructure.Seed;

internal static class DeterministicGuid
{
    public static Guid Create(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }
}