namespace Convivium.Domain.Common;

/// <summary>
/// Competencia contabil: o mes de referencia de uma despesa ou cobranca.
/// A conta de luz que chega em outubro pode ser da competencia 09/2026 —
/// e o que importa na prestacao de contas e a competencia, nao a data de pagamento.
/// </summary>
/// <remarks>
/// Persistida como um inteiro no formato AAAAMM (ex.: 202609), o que mantem
/// ordenacao e comparacao baratas no banco.
/// </remarks>
public readonly record struct Competence : IComparable<Competence>
{
    public Competence(int year, int month)
    {
        if (year is < 2000 or > 2999)
        {
            throw new DomainException($"Ano de competencia invalido: {year}.");
        }

        if (month is < 1 or > 12)
        {
            throw new DomainException($"Mes de competencia invalido: {month}.");
        }

        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public static Competence Current => From(DateOnly.FromDateTime(DateTime.UtcNow));

    public static Competence From(DateOnly date) => new(date.Year, date.Month);

    public static Competence FromInt(int value) => new(value / 100, value % 100);

    /// <summary>Aceita "09/2026", "2026-09" ou "202609".</summary>
    public static Competence Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        string trimmed = text.Trim();

        if (trimmed.Length == 6 && int.TryParse(trimmed, out int compact))
        {
            return FromInt(compact);
        }

        string[] parts = trimmed.Split('/', '-');
        if (parts.Length != 2)
        {
            throw new DomainException($"Competencia invalida: '{text}'. Use MM/AAAA ou AAAA-MM.");
        }

        int first = int.Parse(parts[0]);
        int second = int.Parse(parts[1]);

        // "2026-09" tem o ano na frente; "09/2026" tem o mes.
        return first > 12 ? new Competence(first, second) : new Competence(second, first);
    }

    public static bool TryParse(string? text, out Competence competence)
    {
        try
        {
            competence = Parse(text!);
            return true;
        }
        catch (Exception)
        {
            competence = default;
            return false;
        }
    }

    public int ToInt() => (Year * 100) + Month;

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => FirstDay.AddMonths(1).AddDays(-1);

    public Competence AddMonths(int months) => From(FirstDay.AddMonths(months));

    public Competence Next() => AddMonths(1);

    public Competence Previous() => AddMonths(-1);

    public int CompareTo(Competence other) => ToInt().CompareTo(other.ToInt());

    public static bool operator <(Competence a, Competence b) => a.ToInt() < b.ToInt();

    public static bool operator >(Competence a, Competence b) => a.ToInt() > b.ToInt();

    public static bool operator <=(Competence a, Competence b) => a.ToInt() <= b.ToInt();

    public static bool operator >=(Competence a, Competence b) => a.ToInt() >= b.ToInt();

    public override string ToString() => $"{Month:00}/{Year}";
}
