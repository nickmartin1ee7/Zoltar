using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Zoltar;

public partial class PreviousFortunesViewModel : ObservableObject
{
    [ObservableProperty]
    private List<TimestampedGenerateReponse> _previousFortunes;

    public async Task InitializeAsync()
    {
        var previousFortunesJson = await SecureStorage.GetAsync(Constants.PREVIOUS_FORTUNES_KEY);
        if (string.IsNullOrEmpty(previousFortunesJson))
        {
            return;
        }

        var tempPreviousFortunes = JsonSerializer.Deserialize<List<TimestampedGenerateReponse>>(previousFortunesJson);

        // Fault detection
        if (tempPreviousFortunes.Any(x => x.GenerateResponse is null))
        {
            // Clear invalid storage
            PreviousFortunes = null;
            _ = SecureStorage.Remove(Constants.PREVIOUS_FORTUNES_KEY);
            return;
        }

        if (tempPreviousFortunes.Count != 0)
        {
            tempPreviousFortunes = [.. tempPreviousFortunes.OrderByDescending(x => x.Timestamp)];
        }

        PreviousFortunes = tempPreviousFortunes;
    }
}