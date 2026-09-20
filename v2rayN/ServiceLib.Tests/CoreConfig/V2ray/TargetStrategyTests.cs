using ServiceLib.Services.CoreConfig;

namespace ServiceLib.Tests.CoreConfig.V2ray;

/// <summary>
/// PattN: per-profile Xray outbound targetStrategy. It is written on the outbound itself, only when it
/// differs from the default (AsIs), it wins over the global "Proxy Target Resolution Strategy", and it
/// never travels in share links.
/// </summary>
public class TargetStrategyTests
{
    private const string Chosen = "UseIPv4";

    private static (V2rayConfig Config, string Json) Generate(Config config, ProfileItem node, Action<CoreConfigContext>? setup = null)
    {
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        setup?.Invoke(context);

        var result = new CoreConfigV2rayService(context).GenerateClientConfigContent();
        if (!result.Success)
        {
            throw new InvalidOperationException("config generation failed: " + result.Msg);
        }
        var json = result.Data!.ToString()!;
        return (JsonUtils.Deserialize<V2rayConfig>(json)!, json);
    }

    [Test]
    [Arguments(null, null)]
    [Arguments("", null)]
    [Arguments("   ", null)]
    [Arguments("AsIs", null)]
    [Arguments("asis", null)]
    [Arguments(" AsIs ", null)]
    [Arguments("UseIPv4", "UseIPv4")]
    [Arguments(" ForceIPv6v4 ", "ForceIPv6v4")]
    public async Task GetTargetStrategy_ShouldDropBlankAndDefault(string? stored, string? expected)
    {
        var node = new ProfileItem { TargetStrategy = stored! };

        await node.GetTargetStrategy().Should().BeEqualTo(expected);
    }

    [Test]
    public async Task TargetStrategies_ShouldBeTheDocumentedSetWithAsIsFirst()
    {
        await Global.TargetStrategies.First().Should().BeEqualTo(Global.AsIs);
        await string.Join(",", Global.TargetStrategies).Should().BeEqualTo(
            "AsIs,UseIP,UseIPv4,UseIPv6,UseIPv4v6,UseIPv6v4,ForceIP,ForceIPv4,ForceIPv6,ForceIPv4v6,ForceIPv6v4");
    }

    [Test]
    public async Task Generate_ShouldWriteTargetStrategyOnTheOutboundItself()
    {
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        node.TargetStrategy = Chosen;

        var (cfg, json) = Generate(CoreConfigTestFactory.CreateConfig(ECoreType.Xray), node);

        var proxy = cfg.outbounds.First(o => o.tag == Global.ProxyTag);
        await proxy.targetStrategy.Should().BeEqualTo(Chosen);

        // outbound level, not inside streamSettings.sockopt (that is where dialMode goes)
        var proxyNode = JsonNode.Parse(json)!["outbounds"]!.AsArray().First(o => o!["tag"]!.GetValue<string>() == Global.ProxyTag)!;
        await proxyNode["targetStrategy"]!.GetValue<string>().Should().BeEqualTo(Chosen);
        await (proxyNode["streamSettings"]?["sockopt"]?["targetStrategy"] is null).Should().BeTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments("AsIs")]
    [Arguments("asis")]
    public async Task Generate_DefaultOrAsIs_ShouldLeaveTheFieldOut(string stored)
    {
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        node.TargetStrategy = stored;

        var (cfg, json) = Generate(CoreConfigTestFactory.CreateConfig(ECoreType.Xray), node);

        await cfg.outbounds.First(o => o.tag == Global.ProxyTag).targetStrategy.Should().BeNull();
        await json.Contains("targetStrategy", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Test]
    public async Task Generate_WireGuard_ShouldCarryTargetStrategy()
    {
        var node = CoreConfigTestFactory.CreateWireguardNode(ECoreType.Xray);
        node.TargetStrategy = Chosen;

        var (cfg, _) = Generate(CoreConfigTestFactory.CreateConfig(ECoreType.Xray), node);

        var proxy = cfg.outbounds.First(o => o.tag == Global.ProxyTag);
        await proxy.protocol.Should().BeEqualTo("wireguard");
        await proxy.targetStrategy.Should().BeEqualTo(Chosen);
    }

    [Test]
    public async Task Generate_ProxyChain_ShouldUseEachMembersOwnChoice()
    {
        var n1 = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, "n1", "node-1");
        var n2 = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, "n2", "node-2");
        n1.TargetStrategy = "UseIPv6";
        var chain = CoreConfigTestFactory.CreateProxyChainNode(ECoreType.Xray, "c1", "chain", [n1.IndexId, n2.IndexId]);

        var (cfg, _) = Generate(CoreConfigTestFactory.CreateConfig(ECoreType.Xray), chain, context =>
        {
            context.AllProxiesMap[n1.IndexId] = n1;
            context.AllProxiesMap[n2.IndexId] = n2;
        });

        var socks = cfg.outbounds.Where(o => o.protocol == "socks").ToList();
        await socks.Count.Should().BeEqualTo(2);
        await socks.Count(o => o.targetStrategy == "UseIPv6").Should().BeEqualTo(1);
        await socks.Count(o => o.targetStrategy is null).Should().BeEqualTo(1);
    }

    [Test]
    public async Task Generate_GlobalProxyStrategy_ShouldApplyOnlyWhenTheProfileHasNoChoice()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
        config.SimpleDNSItem.Strategy4Proxy = "UseIPv6";

        var plain = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        var (cfgPlain, _) = Generate(config, plain);
        await cfgPlain.outbounds.First(o => o.tag == Global.ProxyTag).targetStrategy.Should().BeEqualTo("UseIPv6");

        var chosen = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        chosen.TargetStrategy = Chosen;
        var (cfgChosen, _) = Generate(config, chosen);
        await cfgChosen.outbounds.First(o => o.tag == Global.ProxyTag).targetStrategy.Should().BeEqualTo(Chosen);
    }

    [Test]
    public async Task ShareLinks_ShouldNeverCarryTargetStrategy()
    {
        var nodes = new[]
        {
            CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray),
            CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray),
            CoreConfigTestFactory.CreateWireguardNode(ECoreType.Xray),
        };

        foreach (var node in nodes)
        {
            node.TargetStrategy = Chosen;
            node.DialMode = "code-1";

            var uri = FmtHandler.GetShareUri(node);

            await uri.Should().NotBeNull();
            var readable = uri + "\n" + DecodeBase64Payload(uri!);
            await readable.Contains("targetStrategy", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
            await readable.Contains(Chosen, StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        }
    }

    [Test]
    public async Task InnerShareLink_ShouldNotExportTargetStrategy()
    {
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        node.TargetStrategy = Chosen;
        node.DialMode = "code-1";

        var uri = InnerFmt.ToUri([node]);

        await uri.Should().NotBeNull();
        var payload = DecodeBase64Payload(uri!.Trim());
        // the whole profile travels in this link (dialMode included), only targetStrategy is kept out
        await payload.Contains("code-1").Should().BeTrue();
        await payload.Contains("targetStrategy", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        await payload.Contains(Chosen, StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        // exporting must not change the profile being exported
        await node.TargetStrategy.Should().BeEqualTo(Chosen);
    }

    [Test]
    public async Task InnerShareLink_ShouldIgnoreTargetStrategyOnImport()
    {
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        node.TargetStrategy = Chosen;
        node.DialMode = "code-1";
        // a link that does carry the field, as another build or a hand-made link could
        var json = JsonUtils.Serialize(node, false);
        await json.Contains(Chosen).Should().BeTrue();
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).Replace('+', '-').Replace('/', '_').Replace("=", "");
        var link = $"{Global.InnerUriProtocol}vmess/{encoded}";

        var imported = InnerFmt.Resolve(link, string.Empty);

        await imported.Should().NotBeNull();
        await imported!.Count.Should().BeEqualTo(1);
        await imported[0].DialMode.Should().BeEqualTo("code-1");
        await imported[0].TargetStrategy.Should().BeEqualTo(string.Empty);
        await imported[0].GetTargetStrategy().Should().BeNull();
    }

    [Test]
    public async Task DeepCopy_ShouldKeepTargetStrategy()
    {
        // The editor works on a deep copy of the profile, so the field must survive plain serialization;
        // it is removed from share links explicitly instead of being hidden from the serializer.
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        node.TargetStrategy = Chosen;

        await JsonUtils.DeepCopy(node)!.TargetStrategy.Should().BeEqualTo(Chosen);
    }

    /// <summary>
    /// Returns the decoded text of every base64/base64url looking segment of a share link, so that
    /// payload formats (VMess QR JSON, the internal link) can be searched like plain query strings.
    /// </summary>
    private static string DecodeBase64Payload(string uri)
    {
        var sb = new StringBuilder();
        foreach (var segment in Regex.Split(uri, @"[^A-Za-z0-9+/=_-]+").Where(s => s.Length >= 16))
        {
            var normalized = segment.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - (normalized.Length % 4)) % 4), '=');
            try
            {
                sb.AppendLine(Encoding.UTF8.GetString(Convert.FromBase64String(normalized)));
            }
            catch (FormatException)
            {
                // not base64, already covered by the plain-text check
            }
        }
        return sb.ToString();
    }
}
