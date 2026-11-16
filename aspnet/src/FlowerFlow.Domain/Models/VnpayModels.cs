namespace FlowerFlow.Domain.Models;
public sealed class VnpayException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
public sealed record VnpaySecrets(string TmnCode, string HashSecret)
{
    public override string ToString() => "VnpaySecrets { credentials = redacted }";
}
public sealed record VnpayConfig(string TmnCode, string HashSecret, string PaymentUrl, string ReturnUrl)
{
    public override string ToString() => "VnpayConfig { credentials = redacted }";
}
