namespace EmailValidatorPRO.Models
{
    public enum EmailStatus
    {
        Pending,    // Aun no verificado
        Valid,      // Email valido y verificado
        Invalid,    // Email invalido
        Risky,      // Email sospechoso (greylisting, timeout, etc.)
        Disposable  // Dominio desechable/temporal
    }
}
