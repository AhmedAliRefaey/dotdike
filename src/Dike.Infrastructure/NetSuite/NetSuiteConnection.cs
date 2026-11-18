using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dike.Infrastructure.NetSuite;

public sealed class NetSuiteConnection
{
    public string AccountId { get; set; } = "";
    public string Mode { get; set; } = "TBA";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string TokenId { get; set; } = "";
    public string TokenSecret { get; set; } = "";
    public string CertificateId { get; set; } = "";
    public string PrivateKeyPem { get; set; } = "";
    public Uri BaseUri => new($"https://{AccountId.ToLowerInvariant().Replace('_', '-')}.suitetalk.api.netsuite.com/");
    public void Validate()
    {
        if (!Regex.IsMatch(AccountId, @"\A[0-9A-Za-z]+(?:[_-](?:SB|RP)[0-9]+)?\z"))
            throw new NetSuiteException("Enter a NetSuite account ID, not a URL (for example 1234567_SB1).");
        if (string.IsNullOrWhiteSpace(ClientId)) throw new NetSuiteException("An integration client/consumer ID is required.");
        if (Mode == "TBA")
        {
            if (new[] { ClientSecret, TokenId, TokenSecret }.Any(string.IsNullOrWhiteSpace))
                throw new NetSuiteException("Token-based authentication requires consumer secret, token ID and token secret.");
        }
        else if (Mode == "M2M")
        {
            if (string.IsNullOrWhiteSpace(CertificateId)) throw new NetSuiteException("A mapped certificate ID is required.");
            try { using var rsa = RSA.Create(); rsa.ImportFromPem(PrivateKeyPem); rsa.ExportParameters(true); }
            catch (Exception e) when (e is ArgumentException or CryptographicException)
            { throw new NetSuiteException("Provide an RSA private key in unencrypted PEM format for PS256 signing."); }
        }
        else throw new NetSuiteException("Select TBA or OAuth 2.0 certificate authentication.");
    }
}
public sealed class NetSuiteException(string message) : Exception(message);

public static class NetSuiteAuthentication
{
    private static string Encode(string value) => Uri.EscapeDataString(value);
    public static string TbaHeader(NetSuiteConnection connection, Uri uri, string nonce, long timestamp)
    {
        var oauth = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["oauth_consumer_key"] = connection.ClientId, ["oauth_token"] = connection.TokenId,
            ["oauth_nonce"] = nonce, ["oauth_timestamp"] = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["oauth_signature_method"] = "HMAC-SHA256", ["oauth_version"] = "1.0"
        };
        var all = oauth.Select(x => new KeyValuePair<string,string>(Encode(x.Key), Encode(x.Value))).ToList();
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            all.Add(new(Encode(Uri.UnescapeDataString(pair[0])), Encode(Uri.UnescapeDataString(pair.Length > 1 ? pair[1] : ""))));
        }
        var normalized = string.Join("&", all.OrderBy(x => x.Key, StringComparer.Ordinal).ThenBy(x => x.Value, StringComparer.Ordinal).Select(x => $"{x.Key}={x.Value}"));
        var signatureBase = $"POST&{Encode(uri.GetLeftPart(UriPartial.Path))}&{Encode(normalized)}";
        var key = Encoding.UTF8.GetBytes($"{Encode(connection.ClientSecret)}&{Encode(connection.TokenSecret)}");
        oauth["oauth_signature"] = Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(signatureBase)));
        return $"OAuth realm=\"{Encode(connection.AccountId.ToUpperInvariant().Replace('-', '_'))}\", " +
            string.Join(", ", oauth.Select(x => $"{x.Key}=\"{Encode(x.Value)}\""));
    }
    public static string ClientAssertion(NetSuiteConnection connection, DateTimeOffset now)
    {
        static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "PS256", typ = "JWT", kid = connection.CertificateId }));
        var payload = B64(JsonSerializer.SerializeToUtf8Bytes(new { iss = connection.ClientId, scope = new[] { "rest_webservices" },
            aud = new Uri(connection.BaseUri, "services/rest/auth/oauth2/v1/token").AbsoluteUri,
            iat = now.ToUnixTimeSeconds(), exp = now.AddMinutes(5).ToUnixTimeSeconds() }));
        using var rsa = RSA.Create(); rsa.ImportFromPem(connection.PrivateKeyPem);
        return header + "." + payload + "." + B64(rsa.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }
}
