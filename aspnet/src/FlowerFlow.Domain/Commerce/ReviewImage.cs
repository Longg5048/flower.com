namespace FlowerFlow.Domain.Commerce;

public sealed class ReviewImage
{
    public long Id { get; set; }
    public long ReviewId { get; set; }
    public required byte[] Data { get; set; }
    public required string ContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public Review? Review { get; set; }
}
