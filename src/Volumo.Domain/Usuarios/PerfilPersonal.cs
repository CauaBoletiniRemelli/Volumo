namespace Volumo.Domain.Usuarios;

/// <summary>Dados do personal trainer.</summary>
public sealed class PerfilPersonal : Perfil
{
    private PerfilPersonal()
    {
    }

    private PerfilPersonal(Guid usuarioId, string nome, Cref? cref)
        : base(usuarioId, nome) => Cref = cref;

    public Cref? Cref { get; private set; }

    public static PerfilPersonal Criar(Guid usuarioId, string nome, Cref? cref = null) => new(usuarioId, nome, cref);

    public void DefinirCref(Cref cref)
    {
        ArgumentNullException.ThrowIfNull(cref);
        GarantirAtivo();
        Cref = cref;
    }

    protected override void ApagarDadosPessoais() => Cref = null;
}
