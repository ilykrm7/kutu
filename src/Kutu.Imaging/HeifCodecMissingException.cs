namespace Kutu.Imaging;

public sealed class HeifCodecMissingException : InvalidOperationException
{
    public HeifCodecMissingException()
        : base("HEIF codec is not installed.")
    {
    }
}
