namespace AttendTrack.Application.Options;

/// <summary>
/// JWT issuance options bound from "Jwt" section in appsettings.json.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey     { get; set; } = "";
    public string Issuer        { get; set; } = "AttendTrack";
    public string Audience      { get; set; } = "AttendTrackUsers";
    public int    ExpiryMinutes { get; set; } = 480;
}
