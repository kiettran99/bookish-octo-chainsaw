namespace Common.Models;

public class AppSettings
{
    #region JWT
    public string Secret { get; set; } = null!;

    #endregion

    #region AWS S3
    public string BucketCdnEndpoint { get; set; } = null!;
    public string BucketName { get; set; } = null!;
    #endregion
}
