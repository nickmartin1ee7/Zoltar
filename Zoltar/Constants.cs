namespace Zoltar
{
    public abstract class Constants
    {
        /// <summary>
        /// This feature enables unlimited fortunes.
        /// </summary>
        public const string FEATURE_ZOLTAR_UNLIMITED = "zoltarunlimited";

        /// <summary>
        /// This feature enables the user to bypass the fortune time limit through specific hidden actions.
        /// </summary>
        public const string FEATURE_ZOLTAR_SECRET_INTERACTION = "zoltarsecretinteraction";

        public const string LAST_FORTUNE_KEY = "LAST_FORTUNE";
        public const string LAST_FORTUNE_USE_KEY = "LAST_FORTUNE_USE";
        public const string USER_PROFILE_KEY = "USER_PROFILE";
        public const string PREVIOUS_FORTUNES_KEY = "previous_fortunes";
    }
}
