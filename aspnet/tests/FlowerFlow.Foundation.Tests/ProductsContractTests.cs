using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Identity;
using FlowerFlow.Infrastructure.Firebase;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class ProductsContractTests
{
    [Fact]
    public async Task Http_products_match_JavaScript_response_headers_and_dependency_calls()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "flower.com/backend/catalog/products.js"))) root = root.Parent;
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add(Path.Combine(root!.FullName,"aspnet/tests/FlowerFlow.Foundation.Tests/products-oracle.cjs"));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await errors);
        using var cases = JsonDocument.Parse(await output);
        Assert.Equal(29, cases.RootElement.GetArrayLength());
        foreach (var scenario in cases.RootElement.EnumerateArray())
        {
            var backend = new Backend(scenario);
            await using var host = new Host(backend);
            using var client = host.CreateClient();
            var query = new List<string>();
            if (scenario.TryGetProperty("query",out var values))
                foreach(var pair in values.EnumerateObject())
                    foreach(var value in pair.Value.ValueKind == JsonValueKind.Array ? pair.Value.EnumerateArray().ToArray() : new[]{pair.Value})
                        query.Add(Uri.EscapeDataString(pair.Name)+"="+Uri.EscapeDataString(value.GetString()!));
            using var request = new HttpRequestMessage(new HttpMethod(scenario.TryGetProperty("method",out var method)?method.GetString()!:"GET"), "/api/products?"+string.Join("&",query));
            if(scenario.TryGetProperty("authorization",out var auth)) request.Headers.TryAddWithoutValidation("Authorization",auth.GetString());
            var response = await client.SendAsync(request);
            var actual = JsonSerializer.SerializeToNode(new {
                status=(int)response.StatusCode, body=JsonNode.Parse(await response.Content.ReadAsStringAsync()),
                cache=response.Headers.NonValidated.TryGetValues("Cache-Control",out var cache)?string.Join(", ",cache):null,
                allow=response.Content.Headers.TryGetValues("Allow",out var allow)?string.Join(", ",allow):null,
                roleReads=backend.RoleReads, productReads=backend.ProductReads, tokenReads=backend.TokenReads
            });
            var expected=JsonNode.Parse(scenario.GetProperty("expected").GetRawText());
            Assert.True(JsonNode.DeepEquals(expected,actual),$"{scenario.GetProperty("name")}: expected {expected}; actual {actual}");
        }
    }

    [Fact]
    public void Serializer_does_not_mutate_source_and_keeps_archived_products_fields()
    {
        var fields = JsonNode.Parse("{\"id\":\"vn-flower-01\",\"title\":\"?\",\"image\":\"/images/flower-01.webp\"}")!.AsObject();
        var before=fields.ToJsonString();
        var serialized=new ProductSerializer(new EmbeddedProductCatalog()).Serialize(new("doc",fields));
        Assert.Equal(before,fields.ToJsonString());
        Assert.NotEqual("?",serialized["title"]!.GetValue<string>());
    }

    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Http_products_use_real_Firestore_reader_and_preserve_sdk_values()
    {
        var endpoint=Environment.GetEnvironmentVariable("FLOWERFLOW_TEST_FIRESTORE_HOST")!;
        if(!Uri.TryCreate("http://"+endpoint,UriKind.Absolute,out var uri)||!uri.IsLoopback)
            throw new InvalidOperationException("Only loopback emulator endpoints are permitted.");
        var database=new FirestoreDbBuilder {
            ProjectId="demo-products-"+Guid.NewGuid().ToString("N")[..12],Endpoint=endpoint,
            ChannelCredentials=Grpc.Core.ChannelCredentials.Insecure,
            Settings=new Google.Cloud.Firestore.V1.FirestoreSettings {
                CallSettings=Google.Api.Gax.Grpc.CallSettings.FromHeader("Authorization","Bearer owner")}
        }.Build();
        var first=database.Collection("products").Document("a-first");
        var hidden=database.Collection("products").Document("m-hidden");
        var last=database.Collection("products").Document("z-last");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var timestamp=Timestamp.FromDateTime(new DateTime(2026,11,16,3,4,5,123,DateTimeKind.Utc));
            await first.SetAsync(new Dictionary<string,object> { ["title"]="Z",["status"]="active",["createdAt"]=timestamp,
                ["nested"]=new Dictionary<string,object>{["at"]=timestamp},["bytes"]=new byte[]{1,2},["geo"]=new GeoPoint(10,20)},cancellationToken:timeout.Token);
            await hidden.SetAsync(new Dictionary<string,object>{["status"]="hidden"},cancellationToken:timeout.Token);
            await last.SetAsync(new Dictionary<string,object>{["title"]="A",["status"]="archived"},cancellationToken:timeout.Token);
            using var input=JsonDocument.Parse("{\"admin\":true,\"roleExists\":true}");
            var backend=new Backend(input.RootElement);
            await using var host=new Host(backend,new FirestoreProductReader(()=>database));
            using var client=host.CreateClient();
            var response=await client.GetAsync("/api/products",timeout.Token);
            Assert.Equal(System.Net.HttpStatusCode.OK,response.StatusCode);
            var products=JsonNode.Parse(await response.Content.ReadAsStringAsync(timeout.Token))!["products"]!.AsArray();
            Assert.Equal(new[]{"a-first","z-last"},products.Select(p=>p!["id"]!.GetValue<string>()));
            Assert.Equal("2026-11-16T03:04:05.123Z",products[0]!["createdAt"]!.GetValue<string>());
            Assert.Equal(123000000,products[0]!["nested"]!["at"]!["_nanoseconds"]!.GetValue<int>());
            Assert.Equal("Buffer",products[0]!["bytes"]!["type"]!.GetValue<string>());
            Assert.Equal(10,products[0]!["geo"]!["_latitude"]!.GetValue<double>());
            client.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer","admin");
            var managed=await client.GetAsync("/api/products?manage=1",timeout.Token);
            Assert.Equal(System.Net.HttpStatusCode.OK,managed.StatusCode);
            Assert.Equal(3,JsonNode.Parse(await managed.Content.ReadAsStringAsync(timeout.Token))!["products"]!.AsArray().Count);
        }
        finally
        {
            await first.DeleteAsync(cancellationToken:timeout.Token);
            await hidden.DeleteAsync(cancellationToken:timeout.Token);
            await last.DeleteAsync(cancellationToken:timeout.Token);
        }
    }

    [Fact]
    public async Task Flower_prices_dispatch_requires_its_own_authentication()
    {
        using var input=JsonDocument.Parse("{}");
        var backend=new Backend(input.RootElement);
        await using var host=new Host(backend);
        using var client=host.CreateClient();
        var response=await client.GetAsync("/api/products?route=flower-prices");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized,response.StatusCode);
        Assert.Equal("unauthenticated",JsonNode.Parse(await response.Content.ReadAsStringAsync())!["code"]!.GetValue<string>());
        Assert.Equal(0,backend.ProductReads);
        Assert.Equal(0,backend.TokenReads);
    }

    private sealed class Host(Backend backend,IProductReader? reader=null):WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            // An unusable SQL connection makes accidental legacy authentication fail/count twice.
            builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{
                ["ConnectionStrings:FlowerFlow"]="Server=127.0.0.1,1;Database=DoNotUse;Connect Timeout=1;User Id=test;Password=test;Encrypt=False"}));
            builder.ConfigureServices(services=>{
                services.RemoveAll<IProductReader>();services.AddSingleton<IProductReader>(reader??backend);
                services.RemoveAll<IFirebaseTokenVerifier>();services.AddSingleton<IFirebaseTokenVerifier>(backend);
                services.RemoveAll<IAdminRoleReader>();services.AddSingleton<IAdminRoleReader>(backend);
            });
        }
    }
    private sealed class Backend(JsonElement input):IProductReader,IFirebaseTokenVerifier,IAdminRoleReader
    {
        public int RoleReads{get;private set;} public int ProductReads{get;private set;} public int TokenReads{get;private set;}
        public bool IsConfigured=>true;
        private bool Flag(string name)=>input.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.True;
        public void EnsureConfigured(){if(Flag("configError"))throw new FirebaseConfigurationException();}
        public Task<RequestIdentity> VerifyAsync(string token,CancellationToken cancellationToken)
        {
            TokenReads++;
            if(Flag("tokenError"))throw new InvalidFirebaseTokenException();
            var claims=new Dictionary<string,object>();
            if(input.TryGetProperty("admin",out var admin))claims["admin"]=admin.ValueKind==JsonValueKind.String?(object)admin.GetString()!:admin.GetBoolean();
            return Task.FromResult(new RequestIdentity("user-a",claims));
        }
        public Task<AdminRole?> ReadAsync(string uid,CancellationToken cancellationToken)
        {
            Assert.Equal("user-a",uid);RoleReads++;
            if(Flag("roleError"))throw new Exception("private-detail");
            return Task.FromResult<AdminRole?>(Flag("roleExists")?new(null):null);
        }
        public Task<IReadOnlyList<ProductDocument>> ReadAllAsync(CancellationToken cancellationToken)
        {
            ProductReads++;
            if(Flag("readError"))throw new Exception("private-detail");
            return Task.FromResult<IReadOnlyList<ProductDocument>>(input.GetProperty("seed").EnumerateArray().Select(doc=>new ProductDocument(doc.GetProperty("id").GetString()!,
                new JsonObject(doc.GetProperty("fields").EnumerateObject().Select(pair=>KeyValuePair.Create(pair.Name,FirestoreProductReader.ConvertValue(Revive(pair.Value),true)))))).ToArray());
        }
        private static object? Revive(JsonElement value)=>value.ValueKind switch {
            JsonValueKind.Object when value.TryGetProperty("$timestamp",out var timestamp)=>Timestamp.FromDateTime(DateTime.Parse(timestamp.GetString()!,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal)),
            JsonValueKind.Object=>value.EnumerateObject().ToDictionary(pair=>pair.Name,pair=>Revive(pair.Value)!),
            JsonValueKind.Array=>value.EnumerateArray().Select(Revive).ToArray(),JsonValueKind.String=>value.GetString(),
            JsonValueKind.Number=>value.GetDouble(),JsonValueKind.True=>true,JsonValueKind.False=>false,_=>null
        };
    }
}
