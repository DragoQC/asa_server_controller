namespace managerwebapp.Models.Home;

public sealed record HomePageModel(
    IReadOnlyList<HomeServerModel> Servers);
