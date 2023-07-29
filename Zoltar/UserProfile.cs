namespace Zoltar;

public record UserProfile
{
    public string Name { get; set; }
    public DateTime Birthday { get; set; }

    public string Sign
    {
        get
        {
            int month = Birthday.Month;
            int day = Birthday.Day;

            switch (month)
            {
                case 3 when day >= 21:
                case 4 when day <= 19:
                    return "Aries";
                case 4 when day >= 20:
                case 5 when day <= 20:
                    return "Taurus";
                case 5 when day >= 21:
                case 6 when day <= 20:
                    return "Gemini";
                case 6 when day >= 21:
                case 7 when day <= 22:
                    return "Cancer";
                case 7 when day >= 23:
                case 8 when day <= 22:
                    return "Leo";
                case 8 when day >= 23:
                case 9 when day <= 22:
                    return "Virgo";
                case 9 when day >= 23:
                case 10 when day <= 22:
                    return "Libra";
                case 10 when day >= 23:
                case 11 when day <= 21:
                    return "Scorpio";
                case 11 when day >= 22:
                case 12 when day <= 21:
                    return "Sagittarius";
                case 12 when day >= 22:
                case 1 when day <= 19:
                    return "Capricorn";
                case 1 when day >= 20:
                case 2 when day <= 18:
                    return "Aquarius";
                case 2 when day is >= 19 and <= 29:
                // Leap year handling
                case 3 when day <= 20:
                    return "Pisces";
                default:
                    return "Unknown";
            }
        }
    }
}