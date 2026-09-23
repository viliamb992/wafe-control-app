namespace RecuperationSystem.Shared;

public static class AppConstants
{
    public const string WafeApiBaseUrl = "https://go2my.wafe.eu/api/";
    public const string SandcastleKeyHeader = "Sandcastle-Key";
    
    // API Endpoints
    public const string AuthContextEndpoint = "auth/context";
    public const string MainEndpoint = "api/v1/main";
    public const string HeaderEndpoint = "api/v1/header";
    public const string InfoEndpoint = "api/v1/info";
    public const string MessagesEndpoint = "api/v1/messages";
    public const string ScheduleEndpoint = "api/v1/schedule";

    // Control Endpoints
    public const string StopActiveEndpoint = "api/v1/main/stop-active";
    public const string SilentActiveEndpoint = "api/v1/main/silent-active";
    public const string HolidayActiveEndpoint = "api/v1/main/holiday-active";
    public const string BoostRemainingEndpoint = "api/v1/main/boost-remaining";
    public const string AuthorityEndpoint = "api/v1/main/authority";
    public const string FlowRequestedEndpoint = "api/v1/main/flow-requested";
    public const string SchedulePlanEndpoint = "api/v1/schedule/plan";
    public const string HeaderNameEndpoint = "api/v1/header/name";

    // The API's own limit for the unit name is unknown; stay well within typical ones.
    public const int MaxUnitNameLength = 28;

    // Flow Speed Constraints
    public const int MinFlowSpeed = 50;
    public const int MaxFlowSpeed = 220;
    
    // Authority Modes
    public const string ModeIntelligent = "intelligent";
    public const string ModeManual = "manual";
    public const string ModeSchedule = "schedule";

    // Schedule entry modes (GET api/v1/schedule "modes")
    public const string ScheduleModeMin = "min";
    public const string ScheduleModeAuto = "auto";
    public const string ScheduleModeNominal = "nom";
    public const string ScheduleModeBoost = "boost";
}
