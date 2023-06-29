namespace Zoltar
{
    public class ZoltarSettings
    {
        public AppCenterSettings AppCenter { get; set; }

        public OpenAiSettings OpenAi { get; set; }

        public class OpenAiSettings
        {
            public string Key { get; set; }
            public string Prompt { get; set; }
            public int MaxTokens { get; set; }
        }

        public class AppCenterSettings
        {
            public string Secret { get; set; }
        }
    }
}
