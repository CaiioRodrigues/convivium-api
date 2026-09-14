namespace Convivium.Application.Auth;

/// <summary>
/// Credencial invalida ou sessao expirada. A API traduz em HTTP 401.
/// </summary>
/// <remarks>
/// A mensagem e sempre generica de proposito: dizer "e-mail nao cadastrado"
/// deixa um atacante descobrir quais e-mails existem na base.
/// </remarks>
public sealed class AuthenticationFailedException(string message = "E-mail ou senha invalidos.")
    : Exception(message);
