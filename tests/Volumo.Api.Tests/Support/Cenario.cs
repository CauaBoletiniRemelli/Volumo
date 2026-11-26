using System.Security.Cryptography;
using Volumo.Domain.Exercicios;
using Volumo.Domain.Usuarios;
using Volumo.Infrastructure.Identity;
using Volumo.Infrastructure.Persistencia;

namespace Volumo.Api.Tests.Support;

/// <summary>Cria no banco os dados de que um teste precisa, sempre com Ids novos.</summary>
internal static class Cenario
{
    public static async Task<PerfilPersonal> PersonalAsync(VolumoDbContext db, Cref? cref = null)
    {
        var usuario = NovoUsuario("personal");
        var perfil = PerfilPersonal.Criar(usuario.Id, "Personal de Teste", cref);
        db.Users.Add(usuario);
        db.PerfisPersonal.Add(perfil);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return perfil;
    }

    public static async Task<PerfilAluno> AlunoAsync(VolumoDbContext db)
    {
        var usuario = NovoUsuario("aluno");
        var perfil = PerfilAluno.Criar(usuario.Id, "Aluno de Teste");
        db.Users.Add(usuario);
        db.PerfisAluno.Add(perfil);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return perfil;
    }

    public static async Task<Exercicio> ExercicioAsync(VolumoDbContext db, string nome = "Supino reto")
    {
        var exercicio = Exercicio.CriarDoCatalogo(Guid.CreateVersion7(), nome, GrupoMuscular.Peitoral);
        db.Exercicios.Add(exercicio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return exercicio;
    }

    /// <summary>CREF com número aleatório, para não colidir com o de outros testes (o CREF é único).</summary>
    public static Cref NovoCref(string categoriaEUf) =>
        Cref.Criar($"{RandomNumberGenerator.GetInt32(100_000, 1_000_000)}-{categoriaEUf}");

    private static Usuario NovoUsuario(string prefixo)
    {
        var email = $"{prefixo}-{Guid.NewGuid():N}@volumo.test";
        return new Usuario { UserName = email, Email = email };
    }
}
