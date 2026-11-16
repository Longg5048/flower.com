using System.Text.Json.Nodes;
using FlowerFlow.Domain.Models;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Application.Payments;

public interface IVnpayGateway
{
    VnpayConfig Configuration();
    VnpaySecrets Secrets();
    string PaymentUrl(string txnRef, double amount, string? ip, DocumentTimestamp createdAt, DocumentTimestamp expiresAt, VnpayConfig configuration);
    bool Verify(JsonObject parameters, string secret);
}
public interface ICommerceRandom { string Hex(int bytes); }
