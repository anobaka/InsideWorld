namespace Bakabase.Modules.Property.Models.View;

public record PropertyValueResourceCountsViewModel(bool IsReady, Dictionary<string, int> Counts);
