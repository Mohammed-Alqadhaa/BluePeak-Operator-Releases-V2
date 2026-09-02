using BluePeak.Domain;
using BluePeak.Domain.Seed;

namespace BluePeak.App.Services;

/// <summary>Single source of the estate for the whole application session.</summary>
public sealed class EstateService
{
    private static readonly Lazy<EstateService> Instance = new(() => new EstateService());
    public static EstateService Current => Instance.Value;

    private EstateService() => Model = EstateSeed.Build();

    public EstateModel Model { get; }

    public DateTime Now => Model.Now;

    public string Environment => "Simulation estate · read-only";
}
