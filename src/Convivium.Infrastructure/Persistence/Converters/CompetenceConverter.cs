namespace Convivium.Infrastructure.Persistence.Converters;

using Convivium.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Grava <see cref="Competence"/> como um inteiro AAAAMM (ex.: 202609).
/// Ordenar e comparar competencias vira comparacao de inteiro, que o Postgres
/// resolve com indice comum.
/// </summary>
public sealed class CompetenceConverter() : ValueConverter<Competence, int>(
    competence => competence.ToInt(),
    value => Competence.FromInt(value));
