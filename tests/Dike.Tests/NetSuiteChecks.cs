using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dike.Core;
using Dike.Infrastructure.NetSuite;

internal static class NetSuiteChecks
{
    private sealed class FakeHandler(Func<HttpRequestMessage,int,HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
            => Task.FromResult(respond(request,Calls++));
    }
    private static HttpResponseMessage Page(object[] items,int offset=0,int? total=null,bool more=false)
        => new(HttpStatusCode.OK) { Content=JsonContent.Create(new { items,offset,count=items.Length,totalResults=total ?? items.Length,hasMore=more }) };
    private static object Account(int id) => new { id=id.ToString(),fullname="Bank",acctnumber="100",accttype="Bank",subsidiary="1",currency="SAR" };
    private static object Validation(string currency="SAR") => new { accttype="Bank",accountcurrency=currency,basecurrency="SAR",isprimary="T" };
    private static object Line(string id="10",string debit="0",string credit="125.50")
        => new { transactionid=id,lineid="1",bookid="1",accountid="100",subsidiaryid="1",postingdate="2026-11-01",reference="GL",debit,credit };
    private static NetSuiteConnection Connection() => new() { AccountId="1234567_SB1",ClientId="consumer",ClientSecret="secret",TokenId="token",TokenSecret="token-secret" };
    private static void Check(bool value,string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private static async Task Reject(Func<Task> action,string name)
    {
        try { await action(); } catch(NetSuiteException) { Console.WriteLine("PASS " + name); return; }
        throw new Exception("Expected rejection: " + name);
    }
    public static async Task RunAsync()
    {
        var connection=Connection();
        Check(connection.BaseUri.Host=="1234567-sb1.suitetalk.api.netsuite.com","NetSuite sandbox host normalization");
        await Reject(()=> { var bad=Connection(); bad.AccountId="example.org/path"; bad.Validate(); return Task.CompletedTask; },"Reject arbitrary credential destination");
        var signed=NetSuiteAuthentication.TbaHeader(connection,new Uri(connection.BaseUri,"services/rest/query/v1/suiteql?limit=1000&offset=0"),"fixed",1000);
        var next=NetSuiteAuthentication.TbaHeader(connection,new Uri(connection.BaseUri,"services/rest/query/v1/suiteql?limit=1000&offset=1000"),"fixed",1000);
        Check(signed.Contains("HMAC-SHA256") && signed.Contains("realm=\"1234567_SB1\"") && signed!=next,"TBA signature covers paging parameters");
        using var rsa=RSA.Create(3072);
        var m2m=Connection(); m2m.Mode="M2M"; m2m.CertificateId="certificate"; m2m.PrivateKeyPem=rsa.ExportPkcs8PrivateKeyPem();
        var jwt=NetSuiteAuthentication.ClientAssertion(m2m,DateTimeOffset.FromUnixTimeSeconds(1000)).Split('.');
        static byte[] Decode(string s) => Convert.FromBase64String(s.Replace('-','+').Replace('_','/').PadRight((s.Length+3)/4*4,'='));
        Check(rsa.VerifyData(Encoding.ASCII.GetBytes(jwt[0]+"."+jwt[1]),Decode(jwt[2]),HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"M2M assertion signature verifies");
        using var claims=JsonDocument.Parse(Decode(jwt[1]));
        Check(claims.RootElement.GetProperty("exp").GetInt64()==1300 && claims.RootElement.GetProperty("scope")[0].GetString()=="rest_webservices","M2M short-lived audience-scoped assertion");
        using var paged=new HttpClient(new FakeHandler((r,n)=>
        {
            Check(r.Headers.GetValues("Prefer").Single()=="transient","SuiteQL transient header");
            Check(r.Headers.Authorization?.Scheme=="OAuth","Authenticated SuiteQL request");
            return n==0 ? Page(Enumerable.Range(1,1000).Select(x=>Account(x)).ToArray(),0,1001,true) : Page([Account(1001)],1000,1001);
        }));
        Check((await new NetSuiteReader(paged,connection,"company").GetAccountsAsync(default)).Count==1001,"All account pages collected");
        using var failure=new HttpClient(new FakeHandler((r,n)=> n==0 ? Page(Enumerable.Range(1,1000).Select(x=>Account(x)).ToArray(),0,1001,true) : new(HttpStatusCode.Forbidden)));
        await Reject(async()=>await new NetSuiteReader(failure,connection,"company").GetAccountsAsync(default),"Failed second page rejects partial snapshot");
        using var gl=new HttpClient(new FakeHandler((r,n)=>
        {
            if(n==0) return Page([Validation()]);
            var body=r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Check(body.Contains("params") && body.Contains("al.account = ?"),"GL uses bound parameters");
            return Page([Line()]);
        }));
        var scope=new AccountScope("company","1","1","100","SAR");
        var reader=new NetSuiteReader(gl,connection,"company");
        var snapshot=await reader.ReadPostedMovementsAsync(scope,new(2026,11,1),new(2026,11,4),default);
        Check(snapshot.Complete && snapshot.Rows.Single().Amount==-125.50m,"GL sign is debit minus credit");
        Check(snapshot.Rows.Single().Id=="10:1:1:100","GL identity retains transaction line book and account");
        using var versioned=new HttpClient(new FakeHandler((r,n)=>n==0 ? Page([Validation()]) : Page([Line()])));
        var extract=await new NetSuiteReader(versioned,connection,"company").ReadGlAsync("100","1","1",new(2026,11,1),new(2026,11,4),default);
        Check(extract.QueryVersion==NetSuiteQueryContract.QueryVersion && extract.IdentityVersion==NetSuiteQueryContract.IdentityVersion && extract.QuerySha256==NetSuiteQueryContract.QuerySha256,"New GL extracts retain candidate query/key provenance");
        await Reject(async()=>await reader.ReadPostedMovementsAsync(scope with {Company="other"},new(2026,11,1),new(2026,11,4),default),"Cross-company scope rejected");
        await Reject(async()=>await reader.ReadGlAsync("100 OR 1=1","1","1",new(2026,11,1),new(2026,11,4),default),"Invalid account ID rejected");
        using var duplicates=new HttpClient(new FakeHandler((r,n)=>n==0 ? Page([Validation()]) : Page([Line(),Line()])));
        await Reject(async()=>await new NetSuiteReader(duplicates,connection,"company").ReadGlAsync("100","1","1",new(2026,11,1),new(2026,11,4),default),"Ambiguous source identities rejected");
        using var currency=new HttpClient(new FakeHandler((r,n)=>Page([Validation("USD")])));
        await Reject(async()=>await new NetSuiteReader(currency,connection,"company").ReadGlAsync("100","1","1",new(2026,11,1),new(2026,11,4),default),"Non-SAR source rejected");
        using var precision=new HttpClient(new FakeHandler((r,n)=>n==0 ? Page([Validation()]) : Page([Line(credit:"1.001")])));
        await Reject(async()=>await new NetSuiteReader(precision,connection,"company").ReadGlAsync("100","1","1",new(2026,11,1),new(2026,11,4),default),"No silent monetary rounding");
        await CheckExactMoneyAsync(connection);
        using var limit=new HttpClient(new FakeHandler((r,n)=>Page([Account(1)],0,100000,true)));
        await Reject(async()=>await new NetSuiteReader(limit,connection,"company").GetAccountsAsync(default),"Result cap is explicit failure");
        Console.WriteLine("NetSuite connector checks passed (fixture-based; live account validation still required).");
    }

    private static async Task CheckExactMoneyAsync(NetSuiteConnection connection)
    {
        async Task<GlExtract> Read(object line)
        {
            using var http = new HttpClient(new FakeHandler((_, page) => page == 0 ? Page([Validation()]) : Page([line])));
            return await new NetSuiteReader(http, connection, "company").ReadGlAsync("100", "1", "1", new(2026, 11, 1), new(2026, 11, 4), default);
        }

        foreach (var (value, name) in new[]
        {
            ("125.50000000000000000000000000000001", "GL reader rejects precision hidden by decimal rounding"),
            ("-0.0000000000000000000000000000000001", "GL reader rejects tiny nonzero amounts before they become zero"),
            ("100000000000000000.00", "GL reader rejects positive decimal(19,2) overflow"),
            ("-100000000000000000.00", "GL reader rejects negative decimal(19,2) overflow")
        })
            await Reject(async () => await Read(Line(credit: value)), name);

        using var numericLine = JsonDocument.Parse(JsonSerializer.Serialize(Line(credit: "RAW_NUMBER"))
            .Replace("\"RAW_NUMBER\"", "125.50000000000000000000000000000001", StringComparison.Ordinal));
        await Reject(async () => await Read(numericLine.RootElement.Clone()), "GL reader rejects excess precision in numeric JSON tokens");

        const string maximum = "99999999999999999.99";
        await Reject(async () => await Read(Line(debit: maximum, credit: "-0.01")), "GL reader rejects positive signed movement overflow");
        await Reject(async () => await Read(Line(debit: "-0.01", credit: maximum)), "GL reader rejects negative signed movement overflow");

        var trailingZeros = await Read(Line(credit: "125.50000000000000000000000000000000"));
        Check(trailingZeros.Lines.Single().Credit == 125.50m && trailingZeros.Lines.Single().SignedAmount == -125.50m,
            "GL reader accepts exact money with extra trailing zeros");
        var positiveLimit = await Read(Line(debit: maximum, credit: "0"));
        Check(positiveLimit.Lines.Single().SignedAmount == 99999999999999999.99m, "GL reader preserves maximum positive SAR money");
        var negativeLimit = await Read(Line(debit: "0", credit: maximum));
        Check(negativeLimit.Lines.Single().SignedAmount == -99999999999999999.99m, "GL reader preserves maximum negative SAR money");
    }
}
