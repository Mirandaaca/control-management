namespace ControlGestion.API.DTOs;

/// <summary>Una página de una lista larga: los registros de esta página y el total, para que el panel arme la paginación.</summary>
public record Pagina<T>(List<T> Items, int Total, int NumeroPagina, int Tamano)
{
    public int TotalPaginas => Total == 0 ? 1 : (int)Math.Ceiling(Total / (double)Tamano);
}
