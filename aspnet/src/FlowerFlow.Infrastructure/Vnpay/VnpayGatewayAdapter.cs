using System.Security.Cryptography;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Payments;
using FlowerFlow.Domain.Models;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Infrastructure.Vnpay;

public sealed class VnpayGatewayAdapter : IVnpayGateway
{
    public VnpayConfig Configuration() => VnpayGateway.GetConfig();
    public VnpaySecrets Secrets() => VnpayGateway.GetSecrets();
    public bool Verify(JsonObject parameters, string secret) => VnpayGateway.VerifySignature(parameters, secret);
    public string PaymentUrl(string txnRef, double amount, string? ip, DocumentTimestamp createdAt, DocumentTimestamp expiresAt, VnpayConfig configuration) =>
        VnpayGateway.CreatePaymentUrl(txnRef, amount, ip, DateTimeOffset.FromUnixTimeMilliseconds((long)createdAt.Milliseconds),
            DateTimeOffset.FromUnixTimeMilliseconds((long)expiresAt.Milliseconds), configuration);
}
public sealed class CommerceRandom : ICommerceRandom
{
    public string Hex(int bytes) => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(bytes));
}
