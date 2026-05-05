namespace AttendTrack.Application.Tests.Helpers;

/// <summary>
/// Builds valid Hikvision DS-K1T320MFWX EventNotificationAlert XML payloads
/// for use in unit and integration tests.
/// </summary>
public static class HikvisionEventXmlBuilder
{
    public static string BuildCheckIn(
        string employeeCode = "EMP-001",
        string employeeName = "John Doe",
        string verifyMode   = "faceAndFp",
        string macAddress   = "AA:BB:CC:DD:EE:FF",
        string dateTime     = "2025-01-15T09:03:25+05:30") => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <EventNotificationAlert version="2.0">
          <ipAddress>192.168.1.50</ipAddress>
          <macAddress>{macAddress}</macAddress>
          <channelID>1</channelID>
          <dateTime>{dateTime}</dateTime>
          <eventType>AccessControllerEvent</eventType>
          <AccessControllerEvent>
            <deviceName>Access Controller</deviceName>
            <majorEventType>5</majorEventType>
            <subEventType>75</subEventType>
            <cardNo></cardNo>
            <employeeNoString>{employeeCode}</employeeNoString>
            <name>{employeeName}</name>
            <currentVerifyMode>{verifyMode}</currentVerifyMode>
            <attendanceStatus>checkIn</attendanceStatus>
            <serialNo>33</serialNo>
            <userType>normal</userType>
            <mask>noMask</mask>
          </AccessControllerEvent>
        </EventNotificationAlert>
        """;

    public static string BuildCheckOut(
        string employeeCode = "EMP-001",
        string employeeName = "John Doe",
        string verifyMode   = "faceAndFp",
        string macAddress   = "AA:BB:CC:DD:EE:FF",
        string dateTime     = "2025-01-15T18:05:00+05:30") => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <EventNotificationAlert version="2.0">
          <ipAddress>192.168.1.50</ipAddress>
          <macAddress>{macAddress}</macAddress>
          <channelID>1</channelID>
          <dateTime>{dateTime}</dateTime>
          <eventType>AccessControllerEvent</eventType>
          <AccessControllerEvent>
            <deviceName>Access Controller</deviceName>
            <majorEventType>5</majorEventType>
            <subEventType>75</subEventType>
            <cardNo></cardNo>
            <employeeNoString>{employeeCode}</employeeNoString>
            <name>{employeeName}</name>
            <currentVerifyMode>{verifyMode}</currentVerifyMode>
            <attendanceStatus>checkOut</attendanceStatus>
            <serialNo>34</serialNo>
            <userType>normal</userType>
            <mask>noMask</mask>
          </AccessControllerEvent>
        </EventNotificationAlert>
        """;

    public static string BuildBreakIn(
        string employeeCode = "EMP-001",
        string macAddress   = "AA:BB:CC:DD:EE:FF",
        string dateTime     = "2025-01-15T13:00:00+05:30") => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <EventNotificationAlert version="2.0">
          <macAddress>{macAddress}</macAddress>
          <dateTime>{dateTime}</dateTime>
          <eventType>AccessControllerEvent</eventType>
          <AccessControllerEvent>
            <employeeNoString>{employeeCode}</employeeNoString>
            <name>John Doe</name>
            <currentVerifyMode>faceAndFp</currentVerifyMode>
            <attendanceStatus>breakIn</attendanceStatus>
          </AccessControllerEvent>
        </EventNotificationAlert>
        """;

    /// <summary>Epoch-based payload — device sends Unix timestamp instead of dateTime.</summary>
    public static string BuildCheckInWithEpoch(
        string employeeCode = "EMP-001",
        long   epochSeconds = 1736918605L,   // 2025-01-15T03:33:25 UTC
        string macAddress   = "AA:BB:CC:DD:EE:FF") => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <EventNotificationAlert version="2.0">
          <macAddress>{macAddress}</macAddress>
          <eventType>AccessControllerEvent</eventType>
          <AccessControllerEvent>
            <employeeNoString>{employeeCode}</employeeNoString>
            <name>John Doe</name>
            <currentVerifyMode>face</currentVerifyMode>
            <attendanceStatus>checkIn</attendanceStatus>
            <time>{epochSeconds}</time>
          </AccessControllerEvent>
        </EventNotificationAlert>
        """;
}
