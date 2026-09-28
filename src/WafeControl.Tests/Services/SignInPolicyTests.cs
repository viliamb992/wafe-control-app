using WafeControl.Core.Services;

namespace WafeControl.Tests.Services;

public class SignInPolicyTests
{
    private static readonly DateTimeOffset SavedAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static bool IsExpired(SignInDuration? duration, TimeSpan after, SignInMethod method = SignInMethod.StaySignedIn, DateTimeOffset? savedAt = null) =>
        SignInPolicy.IsExpired(
            new StoredLogin("alice", "secret", savedAt ?? SavedAt),
            new UserSettings { SignInMethod = method, StaySignedInFor = duration },
            (savedAt ?? SavedAt) + after);

    [Theory]
    [InlineData(SignInDuration.OneDay)]
    [InlineData(SignInDuration.TwoWeeks)]
    [InlineData(SignInDuration.ThirtyDays)]
    [InlineData(SignInDuration.NinetyDays)]
    public void Expires_ExactlyAfterTheChosenDays(SignInDuration duration)
    {
        var days = TimeSpan.FromDays((int)duration);

        Assert.False(IsExpired(duration, days - TimeSpan.FromTicks(1)));
        Assert.True(IsExpired(duration, days));
    }

    [Fact]
    public void NoLimit_NeverExpires() => Assert.False(IsExpired(null, TimeSpan.FromDays(3650)));

    [Fact]
    public void Biometric_NeverExpires() =>
        Assert.False(IsExpired(SignInDuration.OneDay, TimeSpan.FromDays(400), SignInMethod.Biometric));

    [Fact]
    public void NoSavedTime_IsNotExpired() =>
        Assert.False(SignInPolicy.IsExpired(
            new StoredLogin("alice", "secret"),
            new UserSettings { StaySignedInFor = SignInDuration.OneDay },
            SavedAt.AddYears(1)));

    [Theory]
    [InlineData(SignInMethod.Biometric, 299, false)]
    [InlineData(SignInMethod.Biometric, 300, true)]
    [InlineData(SignInMethod.StaySignedIn, 3600, false)]
    public void UnlockOnResume_OnlyForBiometricAfterFiveMinutes(SignInMethod method, int seconds, bool expected) =>
        Assert.Equal(expected, SignInPolicy.NeedsUnlockOnResume(new UserSettings { SignInMethod = method }, TimeSpan.FromSeconds(seconds)));
}
