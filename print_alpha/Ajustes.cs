using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrintATH;

public sealed class BotonRapido
{
    public string Etiqueta { get; set; } = "";
    public string Impresora { get; set; } = "";
}

/// <summary>Últimas opciones usadas, en %APPDATA%\PrintATH\ajustes.json.</summary>
public sealed class Ajustes
{
    static readonly string Ruta = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrintATH", "ajustes.json");

    public string? Impresora { get; set; }
    public Dictionary<string, string> PapelPorImpresora { get; set; } = new();
    public string? Calidad { get; set; }
    public string? Color { get; set; }
    public bool SinBordes { get; set; }
    public string Diseno { get; set; } = "n1";
    public bool MostrarNombre { get; set; }
    public bool UsarTamanoExacto { get; set; }
    public bool GirarAuto { get; set; } = true;
    public bool Repetir { get; set; }
    public double AnchoPersonalizadoMm { get; set; } = 30;
    public double AltoPersonalizadoMm { get; set; } = 40;

    /// <summary>Fotos por hoja elegidas la última vez en el menú rápido.</summary>
    public int PorHojaRapido { get; set; } = 1;

    /// <summary>Botones del menú rápido: texto del botón y nombre de la impresora en Windows.</summary>
    public List<BotonRapido> BotonesRapidos { get; set; } = new()
    {
        new() { Etiqueta = "920", Impresora = "KONICA MINOLTA 920" },
        new() { Etiqueta = "CANON", Impresora = "CANON C7570" },
        new() { Etiqueta = "RICOH", Impresora = "NEGRO 1" },
    };

    public static Ajustes Cargar()
    {
        try
        {
            if (File.Exists(Ruta))
                return JsonSerializer.Deserialize(File.ReadAllText(Ruta), ContextoJson.Default.Ajustes) ?? new Ajustes();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Archivo dañado o bloqueado: se empieza con los valores por defecto.
        }
        return new Ajustes();
    }

    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
            string temporal = Ruta + ".tmp";
            File.WriteAllText(temporal, JsonSerializer.Serialize(this, ContextoJson.Default.Ajustes));
            File.Move(temporal, Ruta, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No poder guardar las preferencias no debe impedir imprimir.
        }
    }
}

/// <summary>Serializador generado al compilar: evita la reflexión de System.Text.Json y acelera el arranque.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Ajustes))]
partial class ContextoJson : JsonSerializerContext
{
}
