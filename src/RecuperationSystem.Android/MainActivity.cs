using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using RecuperationSystem.Shared;
using RecuperationSystem.Shared.Services;

namespace RecuperationSystem.Android;

[Activity(Label = "@string/app_name", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
public class MainActivity : Activity
{
    private WafeApiService? _apiService;
    private EditText? _usernameInput;
    private EditText? _passwordInput;
    private Button? _loginButton;
    private LinearLayout? _controlPanel;
    private TextView? _statusText;
    private Button? _startButton;
    private Button? _stopButton;
    private SeekBar? _flowSpeedSlider;
    private TextView? _flowSpeedValue;
    private Spinner? _modeSpinner;
    private Button? _applyFlowButton;
    private Button? _applyModeButton;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        _apiService = new WafeApiService();

        // Initialize views
        _usernameInput = FindViewById<EditText>(Resource.Id.usernameInput);
        _passwordInput = FindViewById<EditText>(Resource.Id.passwordInput);
        _loginButton = FindViewById<Button>(Resource.Id.loginButton);
        _controlPanel = FindViewById<LinearLayout>(Resource.Id.controlPanel);
        _statusText = FindViewById<TextView>(Resource.Id.statusText);
        _startButton = FindViewById<Button>(Resource.Id.startButton);
        _stopButton = FindViewById<Button>(Resource.Id.stopButton);
        _flowSpeedSlider = FindViewById<SeekBar>(Resource.Id.flowSpeedSlider);
        _flowSpeedValue = FindViewById<TextView>(Resource.Id.flowSpeedValue);
        _modeSpinner = FindViewById<Spinner>(Resource.Id.modeSpinner);
        _applyFlowButton = FindViewById<Button>(Resource.Id.applyFlowButton);
        _applyModeButton = FindViewById<Button>(Resource.Id.applyModeButton);

        // Setup mode spinner
        var modeAdapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerItem, 
            new[] { "Intelligent", "Manual", "Schedule" });
        modeAdapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
        _modeSpinner!.Adapter = modeAdapter;

        // Setup flow speed slider
        _flowSpeedSlider!.Max = AppConstants.MaxFlowSpeed - AppConstants.MinFlowSpeed;
        _flowSpeedSlider.Progress = 0;
        _flowSpeedValue!.Text = AppConstants.MinFlowSpeed.ToString();

        _flowSpeedSlider.ProgressChanged += (s, e) =>
        {
            if (e.FromUser)
            {
                var value = AppConstants.MinFlowSpeed + e.Progress;
                _flowSpeedValue!.Text = value.ToString();
            }
        };

        // Event handlers
        _loginButton!.Click += async (s, e) => await LoginAsync();
        _startButton!.Click += async (s, e) => await StartSystemAsync();
        _stopButton!.Click += async (s, e) => await StopSystemAsync();
        _applyFlowButton!.Click += async (s, e) => await UpdateFlowSpeedAsync();
        _applyModeButton!.Click += async (s, e) => await UpdateModeAsync();

        // Initially hide control panel
        _controlPanel!.Visibility = Android.Views.ViewStates.Gone;
    }

    private async Task LoginAsync()
    {
        var username = _usernameInput?.Text ?? "";
        var password = _passwordInput?.Text ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Toast.MakeText(this, "Please enter credentials", ToastLength.Short)?.Show();
            return;
        }

        _statusText!.Text = "Authenticating...";
        var success = await _apiService!.AuthenticateAsync(username, password);

        if (success)
        {
            _controlPanel!.Visibility = Android.Views.ViewStates.Visible;
            _statusText.Text = "Connected";
            Toast.MakeText(this, "Connected successfully", ToastLength.Short)?.Show();
        }
        else
        {
            _statusText.Text = "Authentication failed";
            Toast.MakeText(this, "Authentication failed", ToastLength.Short)?.Show();
        }
    }

    private async Task StartSystemAsync()
    {
        await _apiService!.SetStopActiveAsync(false);
        _statusText!.Text = "System Started";
        Toast.MakeText(this, "System started", ToastLength.Short)?.Show();
    }

    private async Task StopSystemAsync()
    {
        await _apiService!.SetStopActiveAsync(true);
        _statusText!.Text = "System Stopped";
        Toast.MakeText(this, "System stopped", ToastLength.Short)?.Show();
    }

    private async Task UpdateFlowSpeedAsync()
    {
        var speed = AppConstants.MinFlowSpeed + _flowSpeedSlider!.Progress;
        await _apiService!.SetFlowSpeedAsync(speed);
        _statusText!.Text = $"Flow speed set to {speed}";
        Toast.MakeText(this, $"Flow speed: {speed}", ToastLength.Short)?.Show();
    }

    private async Task UpdateModeAsync()
    {
        var modes = new[] { AppConstants.ModeIntelligent, AppConstants.ModeManual, AppConstants.ModeSchedule };
        var selectedMode = modes[_modeSpinner!.SelectedItemPosition];
        
        await _apiService!.SetAuthorityModeAsync(selectedMode);
        _statusText!.Text = $"Mode: {selectedMode}";
        Toast.MakeText(this, $"Mode changed to {selectedMode}", ToastLength.Short)?.Show();
    }

    protected override void OnDestroy()
    {
        _apiService?.Dispose();
        base.OnDestroy();
    }
}
