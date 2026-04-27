using ControlGestion.API.Models;
using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public class DetalleHPDTO
{
    [Required] public int ProductoId { get; set; }
    [Required][Range(0.001, double.MaxValue)] public decimal Cantidad { get; set; }
    public decimal Descuento { get; set; } = 0;
}

public class HojaPedidoCreateDTO
{
    [Required] public int ClienteId { get; set; }
    [Required] public int ZonaId { get; set; }
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    public float? PrecisionGPS { get; set; }
    public TipoVenta TipoVenta { get; set; } = TipoVenta.Contado;
    public decimal Descuento { get; set; } = 0;
    [Required][MinLength(1)] public List<DetalleHPDTO> Detalles { get; set; } = new();
}

public class HojaPedidoAnularDTO
{
    [Required] public string Motivo { get; set; } = string.Empty;
}
