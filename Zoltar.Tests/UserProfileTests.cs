using Xunit;

using Zoltar.Models;

namespace Zoltar.Tests;

public class UserProfileTests
{
    private static UserProfile WithBirthday(int month, int day) =>
        new() { Birthday = new DateTime(2000, month, day) };

    [Theory]
    [InlineData(3, 21, "Aries")]
    [InlineData(4, 19, "Aries")]
    [InlineData(4, 20, "Taurus")]
    [InlineData(5, 20, "Taurus")]
    [InlineData(5, 21, "Gemini")]
    [InlineData(6, 20, "Gemini")]
    [InlineData(6, 21, "Cancer")]
    [InlineData(7, 22, "Cancer")]
    [InlineData(7, 23, "Leo")]
    [InlineData(8, 22, "Leo")]
    [InlineData(8, 23, "Virgo")]
    [InlineData(9, 22, "Virgo")]
    [InlineData(9, 23, "Libra")]
    [InlineData(10, 22, "Libra")]
    [InlineData(10, 23, "Scorpio")]
    [InlineData(11, 21, "Scorpio")]
    [InlineData(11, 22, "Sagittarius")]
    [InlineData(12, 21, "Sagittarius")]
    [InlineData(12, 22, "Capricorn")]
    [InlineData(1, 19, "Capricorn")]
    [InlineData(1, 20, "Aquarius")]
    [InlineData(2, 18, "Aquarius")]
    [InlineData(2, 19, "Pisces")]
    [InlineData(2, 29, "Pisces")] // Leap day
    [InlineData(3, 20, "Pisces")]
    public void Sign_ReturnsExpectedZodiacSign(int month, int day, string expected)
    {
        var sign = WithBirthday(month, day).Sign;

        Assert.Equal(expected, sign);
    }

    [Fact]
    public void Sign_ReturnsUnknown_WhenNoBirthday()
    {
        var profile = new UserProfile();

        Assert.Equal("Unknown", profile.Sign);
    }

    [Theory]
    [InlineData(0.9, "incredibly fortunate")]
    [InlineData(0.05, "incredibly unfortunate")]
    [InlineData(0.75, "very fortunate")]
    [InlineData(0.15, "very unfortunate")]
    [InlineData(0.65, "somewhat fortunate")]
    [InlineData(0.25, "somewhat unfortunate")]
    [InlineData(0.5, "average")]
    // Boundary cases
    [InlineData(0.85, "incredibly fortunate")]
    [InlineData(0.70, "very fortunate")]
    [InlineData(0.60, "somewhat fortunate")]
    [InlineData(0.30, "somewhat unfortunate")]
    [InlineData(0.20, "very unfortunate")]
    [InlineData(0.10, "incredibly unfortunate")]
    public void GetLuck_ReturnsExpectedPhrase(double value, string expected)
    {
        Assert.Equal(expected, UserProfile.GetLuck(value));
    }
}
