namespace Hookivery.Infrastructure.Persistence.Configurations;

internal static class CheckConstraints
{
    public static string EnumValues<TEnum>(string column)
        where TEnum : struct, Enum =>
        $"{column} IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(name => $"'{name}'"))})";
}
